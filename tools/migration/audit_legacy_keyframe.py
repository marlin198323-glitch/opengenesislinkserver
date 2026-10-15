#!/usr/bin/env python3
"""Read-only inventory of KeyframeMotion encoding before an OpenGenesisLINK .NET 10 upgrade.

Accepts SQLite region-store snapshots and OAR files. Never deserializes
BinaryFormatter and never writes to the supplied data. This is an *audit*
tool; legacy data still needs a separate, tested offline migration.
"""
from __future__ import annotations

import argparse
import base64
import binascii
from collections import Counter
import json
from pathlib import Path
import sqlite3
import sys
import tarfile
import xml.etree.ElementTree as ET

MAGIC = b"OGKM"
MAX_BLOB_BYTES = 4 * 1024 * 1024
MAX_XML_BYTES = 8 * 1024 * 1024
MAX_OAR_MEMBERS = 200_000


def classify(blob: bytes) -> str:
    """Classify a blob by its header, without parsing or executing its content."""
    if not blob:
        return "empty"
    if len(blob) > MAX_BLOB_BYTES:
        return "oversized"
    if blob.startswith(MAGIC):
        if len(blob) < 8:
            return "truncated-v1-header"
        version = int.from_bytes(blob[4:8], "little")
        return "typed-v1-header" if version == 1 else "unknown-typed-version"
    # Do not claim every non-OGKM value is a BinaryFormatter document:
    # it may also be garbage or data from a future/third-party format.
    return "non-v1-legacy-or-unknown"


def audit_sqlite(path: Path) -> dict:
    """Query the region prims table without modifying the SQLite file."""
    if not path.is_file():
        raise FileNotFoundError(path)
    # mode=ro avoids accidental DB creation; only pass paths, never SQL from CLI.
    uri = path.resolve().as_uri() + "?mode=ro"
    result = Counter()
    with sqlite3.connect(uri, uri=True, timeout=3) as db:
        tables = {row[0].lower(): row[0] for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type='table'")}
        if "prims" not in tables:
            raise ValueError("SQLite region store has no prims table")
        columns = {row[1].lower(): row[1] for row in db.execute(
            'PRAGMA table_info("prims")')}
        if "keyframemotion" not in columns:
            raise ValueError("SQLite prims table has no KeyframeMotion column")
        result["examined_nonnull"] = 0
        for (blob,) in db.execute('SELECT "KeyframeMotion" FROM "prims" '
                                  'WHERE "KeyframeMotion" IS NOT NULL'):
            result["examined_nonnull"] += 1
            result[classify(bytes(blob))] += 1
    return {"kind": "sqlite", "source": str(path), "counts": dict(result)}


def audit_oar(path: Path) -> dict:
    """Scan only OAR objects/*.xml streams, never extract archive members."""
    if not path.is_file():
        raise FileNotFoundError(path)
    result = Counter()
    with tarfile.open(path, mode="r:*") as archive:
        for index, entry in enumerate(archive):
            if index >= MAX_OAR_MEMBERS:
                raise ValueError("OAR member count exceeds safety limit")
            if not (entry.isfile() and entry.name.startswith("objects/")
                    and entry.name.lower().endswith(".xml")):
                continue
            result["object_xml_files"] += 1
            if entry.size > MAX_XML_BYTES:
                result["oversized-object-xml"] += 1
                continue
            stream = archive.extractfile(entry)
            if stream is None:
                result["unreadable-object-xml"] += 1
                continue
            raw = stream.read(MAX_XML_BYTES + 1)
            if len(raw) > MAX_XML_BYTES or b"<!DOCTYPE" in raw.upper():
                result["unsafe-object-xml"] += 1
                continue
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                result["invalid-object-xml"] += 1
                continue
            for elem in root.iter():
                if elem.tag.rsplit("}", 1)[-1] != "KeyframeMotion":
                    continue
                result["keyframe_fields"] += 1
                txt = "".join((elem.text or "").split())
                # Max encoded size before decoding to prevent huge allocations.
                if len(txt) > (MAX_BLOB_BYTES + 1) * 4 // 3 + 16:
                    result["oversized"] += 1
                    continue
                try:
                    data = base64.b64decode(txt, validate=True)
                except (ValueError, binascii.Error):
                    result["invalid-base64"] += 1
                    continue
                result[classify(data)] += 1
    return {"kind": "oar", "source": str(path), "counts": dict(result)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sqlite", action="append", default=[], type=Path,
                        help="Read-only SQLite region-store snapshot (repeatable)")
    parser.add_argument("--oar", action="append", default=[], type=Path,
                        help="OAR tar/tar.gz archive (repeatable)")
    parser.add_argument("--json", action="store_true",
                        help="Emit machine-readable JSON report")
    parser.add_argument("--fail-on-legacy", action="store_true",
                        help="Return code 2 for any non-v1, invalid or oversized data")
    args = parser.parse_args(argv)
    if not args.sqlite and not args.oar:
        parser.error("Pass at least one --sqlite or --oar")
    reports = []
    for path in args.sqlite:
        reports.append(audit_sqlite(path))
    for path in args.oar:
        reports.append(audit_oar(path))
    invalid = {"non-v1-legacy-or-unknown", "unknown-typed-version",
               "truncated-v1-header", "oversized", "invalid-base64",
               "oversized-object-xml", "unsafe-object-xml",
               "unreadable-object-xml", "invalid-object-xml"}
    legacy_found = any(any(r["counts"].get(k, 0) for k in invalid)
                       for r in reports)
    report = {"warning": "Read-only header audit; typed-v1 does NOT prove full "
                         "payload validity; never use legacy BinaryFormatter "
                         "on untrusted data.",
              "needs_migration_review": legacy_found,
              "sources": reports}
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
    else:
        print(report["warning"])
        for source in reports:
            print(f"{source['kind']} {source['source']}")
            for name, count in sorted(source["counts"].items()):
                print(f"  {name}: {count}")
        print("REVIEW REQUIRED" if legacy_found else "No legacy-format headers detected")
    return 2 if args.fail_on_legacy and legacy_found else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, sqlite3.DatabaseError, tarfile.TarError) as exc:
        print(f"Audit failed without changing source files: {exc}", file=sys.stderr)
        sys.exit(3)
