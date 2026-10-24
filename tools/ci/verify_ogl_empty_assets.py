#!/usr/bin/env python3
"""Offline black-box tests for OGL's read-only empty asset audit script."""
import csv
import gzip
import hashlib
import importlib.util
import json
import tempfile
from pathlib import Path
from uuid import uuid4

SCRIPT = Path(__file__).resolve().parents[1] / "ops" / "ogl-empty-assets-audit.py"
spec = importlib.util.spec_from_file_location("ogl_empty_assets_audit", SCRIPT)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

with tempfile.TemporaryDirectory(prefix="ogl-empty-assets-") as d:
    root = Path(d)
    log = root / "OpenSim.log"
    base = root / "fsassets"
    base.mkdir()
    ids = [str(uuid4()) for _ in range(4)]
    content = b"genuine nonempty asset bytes"
    digest = hashlib.sha256(content).hexdigest()
    valid = audit.asset_path(base, digest)
    valid.parent.mkdir(parents=True)
    with gzip.open(valid.with_name(valid.name + ".gz"), "wb") as dst:
        dst.write(content)
    wrong = "a" * 64
    if wrong == digest:
        wrong = "b" * 64
    corrupt = audit.asset_path(base, wrong)
    corrupt.parent.mkdir(parents=True, exist_ok=True)
    corrupt.write_bytes(b"wrong hash content")
    absent = "c" * 64
    log.write_text(
        "2026-10-24 12:20:20,123 WARN  [GETASSET]: asset with empty data: "
        + ids[0] + " type 0\n"
        "2026-10-24 12:20:21,123 WARN  [GETASSET]: asset with empty data: "
        + ids[0] + " type 0\n"
        "2026-10-24 12:20:22,123 WARN  [GETASSET]: asset with empty data: "
        + ids[1] + " type 49\n"
        "2026-10-24 12:20:23,123 WARN  [GETASSET]: asset with empty data: "
        + ids[2] + " type 0\n"
        "2026-10-24 12:20:24,123 WARN  [GETASSET]: asset with empty data: "
        + ids[3] + " type 0\n")
    meta = root / "uuid-hash.tsv"
    meta.write_text(ids[0] + "\t" + digest + "\t0\n"
                    + ids[1] + "\t" + wrong + "\t49\n"
                    + ids[2] + "\t" + absent + "\t0\n")
    result = {entry["uuid"]: entry for entry in audit.audit(log, meta, base)}
    assert len(result) == 4
    assert result[ids[0]]["warning_count"] == 2
    assert result[ids[0]]["payload"] == "hash_ok"
    assert result[ids[1]]["payload"] == "hash_mismatch"
    assert result[ids[2]]["payload"] == "missing_file"
    assert result[ids[3]]["metadata"] == "not_found_in_snapshot"
    assert all("delete" not in row["decision"].replace("do_not_delete", "") for row in result.values())
    # A linked shard MUST be refused rather than read outside snapshot root.
    linked = audit.asset_path(base, "d" * 64)
    external = root / "outside"
    external_payload = external / "dd" / "dd" / "dddd" / ("d" * 64)
    external_payload.parent.mkdir(parents=True)
    external_payload.write_bytes(b"outside target is not trusted")
    (base / "dd").symlink_to(external, target_is_directory=True)
    result_code, _ = audit.verify_asset(base, "d" * 64)
    assert result_code == "read_error_OSError", result_code
    # No audit may write to FSAssets or SQL; only snapshots/output reports.
    assert corrupt.read_bytes() == b"wrong hash content"

print("OpenGenesisLINK empty assets: read-only audit, hash integrity and missing metadata OK")
