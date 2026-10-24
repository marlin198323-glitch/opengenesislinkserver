#!/usr/bin/env python3
"""READ-ONLY OpenGenesisLINK FSAssets triage for repeated empty asset warnings.

Does not delete or mutate any asset, SQL table, cache, spool or inventory entry.
Use a mutually consistent OFFLINE FSAssets SQL/file snapshot for validation.
"""
import argparse
import csv
import gzip
import hashlib
import json
import re
from collections import Counter
from pathlib import Path
from uuid import UUID

WARNING = re.compile(
    r"^(.{23}).*?\[GETASSET\]: asset with empty data: "
    r"([0-9a-fA-F-]{36}) type (-?\d+)"
)
HASH = re.compile(r"^[0-9a-f]{64}$")


def parse_log(path):
    observations = {}
    for line in path.open(encoding="utf-8", errors="replace"):
        match = WARNING.search(line)
        if not match:
            continue
        time, raw, asset_type = match.groups()
        try:
            uid = str(UUID(raw))
        except ValueError:
            continue
        info = observations.setdefault(uid, {
            "uuid": uid, "warnings": 0, "first_seen": time,
            "last_seen": time, "logged_types": set(),
        })
        info["warnings"] += 1
        info["last_seen"] = time
        info["logged_types"].add(asset_type)
    return observations


def load_metadata(path, targets):
    """Metadata TSV: uuid<TAB>hash[<TAB>type], exported from ONE offline DB snapshot."""
    rows = {}
    if path is None:
        return rows
    with path.open(encoding="utf-8") as handle:
        for raw in handle:
            cols = raw.rstrip("\r\n").split("\t")
            if len(cols) < 2:
                raise ValueError("Malformed metadata TSV row")
            try:
                uid = str(UUID(cols[0].strip()))
            except ValueError as exc:
                raise ValueError("Invalid metadata UUID") from exc
            if uid not in targets:
                continue
            if uid in rows:
                raise ValueError("Duplicate metadata UUID in snapshot: " + uid)
            digest = cols[1].strip().lower()
            if not HASH.fullmatch(digest):
                raise ValueError("Invalid SHA-256 for metadata UUID " + uid)
            rows[uid] = (digest, cols[2].strip() if len(cols) > 2 else "")
    return rows


def asset_path(root, digest, osgrid=False):
    if osgrid:
        return root / digest[:3] / digest[3:6] / digest
    return root / digest[:2] / digest[2:4] / digest[4:6] / digest[6:10] / digest


def safe_file(path, root):
    """Reject symlink components and files, including the FSAssets root."""
    root = root.absolute()
    path = path.absolute()
    if root.is_symlink():
        raise OSError("FSAssets root is a symlink")
    for parent in (root, *[root / Path(*path.relative_to(root).parts[:i])
                           for i in range(1, len(path.relative_to(root).parts) + 1)]):
        if parent.is_symlink():
            raise OSError("FSAssets path contains a symlink")
    return path


def verify_asset(root, digest, osgrid=False):
    path = asset_path(root, digest, osgrid)
    compressed = path.with_name(path.name + ".gz")
    if path.is_file():
        candidate = path
        method = "raw"
    elif compressed.is_file():
        candidate = compressed
        method = "gzip"
    else:
        return "missing_file", ""
    try:
        safe_file(candidate, root)
        hasher = hashlib.sha256()
        opener = gzip.open if method == "gzip" else open
        length = 0
        with opener(candidate, "rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                hasher.update(chunk)
                length += len(chunk)
                if length > 512 * 1024 * 1024:
                    return "oversized_payload_requires_manual_review", str(candidate)
        if length == 0:
            return "zero_length_payload", str(candidate)
        return ("hash_ok" if hasher.hexdigest() == digest else "hash_mismatch",
                str(candidate))
    except (OSError, EOFError, ValueError) as exc:
        return "read_error_" + type(exc).__name__, str(candidate)


def audit(log_path, metadata=None, fs_root=None, osgrid=False, backup_root=None):
    observations = parse_log(log_path)
    snapshot = load_metadata(metadata, observations)
    rows = []
    for uid, info in sorted(observations.items(), key=lambda item: -item[1]["warnings"]):
        row = {
            "uuid": uid, "warning_count": info["warnings"],
            "logged_types": ",".join(sorted(info["logged_types"])),
            "first_seen": info["first_seen"], "last_seen": info["last_seen"],
            "metadata": "not_checked" if metadata is None else "not_found_in_snapshot",
            "database_type": "", "sha256": "", "payload": "not_checked",
            "payload_path": "", "backup_payload": "not_checked",
            "backup_payload_path": "", "decision": "investigate_do_not_delete",
        }
        if uid in snapshot:
            digest, db_type = snapshot[uid]
            row.update(metadata="present", database_type=db_type, sha256=digest)
            if fs_root is not None:
                payload, asset_file = verify_asset(fs_root, digest, osgrid)
                row.update(payload=payload, payload_path=asset_file)
                if payload == "hash_ok":
                    row["decision"] = "valid_on_disk_check_service_cache_and_network"
                elif payload in ("missing_file", "hash_mismatch", "zero_length_payload"):
                    row["decision"] = "restore_from_verified_backup_before_any_quarantine"
            if backup_root is not None and row["payload"] != "hash_ok":
                backup_status, backup_file = verify_asset(backup_root, digest, osgrid)
                row.update(backup_payload=backup_status, backup_payload_path=backup_file)
                if backup_status == "hash_ok":
                    row["decision"] = "verified_backup_candidate_manual_restore_on_staging_only"
        rows.append(row)
    return rows


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--log", type=Path, required=True)
    p.add_argument("--metadata-tsv", type=Path)
    p.add_argument("--fs-root", type=Path)
    p.add_argument("--backup-fs-root", type=Path,
                   help="Optional offline backup copy to identify safe restore candidates")
    p.add_argument("--osgrid", action="store_true")
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    if (args.fs_root or args.backup_fs_root) and not args.metadata_tsv:
        p.error("FSAssets roots need the matching --metadata-tsv offline snapshot")
    if ((args.fs_root and args.fs_root.is_symlink()) or
            (args.backup_fs_root and args.backup_fs_root.is_symlink())):
        p.error("FSAssets root symlinks are not supported")
    try:
        rows = audit(args.log, args.metadata_tsv, args.fs_root, args.osgrid,
                     args.backup_fs_root)
    except (ValueError, OSError) as error:
        p.error(str(error))
    args.out.mkdir(parents=True, exist_ok=True)
    columns = [
        "uuid", "warning_count", "logged_types", "first_seen", "last_seen",
        "metadata", "database_type", "sha256", "payload", "payload_path",
        "backup_payload", "backup_payload_path", "decision",
    ]
    with (args.out / "empty-assets-audit.csv").open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=columns)
        writer.writeheader()
        writer.writerows(rows)
    counts = Counter(row["payload"] for row in rows)
    summary = {
        "unique_asset_ids": len(rows),
        "warning_occurrences": sum(row["warning_count"] for row in rows),
        "log_asset_types": dict(Counter(
            t for row in rows for t in row["logged_types"].split(",") if t)),
        "metadata_checked": args.metadata_tsv is not None,
        "payload_checked": args.fs_root is not None,
        "backup_checked": args.backup_fs_root is not None,
        "verified_backup_candidates": sum(
            row["backup_payload"] == "hash_ok" for row in rows),
        "payload_results": dict(counts),
        "mutations": 0,
        "note": "A warning is not proof of an orphan. No deletion or quarantine performed.",
    }
    (args.out / "empty-assets-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
