#!/usr/bin/env python3
"""Regression of read-only legacy KeyframeMotion audit (no .NET required)."""
import base64
import importlib.util
import io
from pathlib import Path
import sqlite3
import tarfile
import tempfile

root = Path(__file__).resolve().parents[2]
path = root / "tools/migration/audit_legacy_keyframe.py"
spec = importlib.util.spec_from_file_location("keyframe_audit", path)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

HEADER = b"OGKM" + (1).to_bytes(4, "little") + b"sample"
LEGACY = b"\x00" * 20
assert audit.classify(HEADER) == "typed-v1-header"
assert audit.classify(LEGACY) == "non-v1-legacy-or-unknown"
assert audit.classify(b"") == "empty"
assert audit.classify(b"OGKM") == "truncated-v1-header"
assert audit.classify(b"OGKM" + (99).to_bytes(4, "little")) == "unknown-typed-version"
assert audit.classify(b"x" * (audit.MAX_BLOB_BYTES + 1)) == "oversized"

with tempfile.TemporaryDirectory() as td:
    folder = Path(td)
    db = folder / "region.sqlite"
    with sqlite3.connect(db) as con:
        con.execute('CREATE TABLE prims (UUID TEXT, KeyframeMotion BLOB)')
        con.executemany('INSERT INTO prims (UUID, KeyframeMotion) VALUES (?, ?)',
                        [("a", HEADER), ("b", LEGACY), ("c", None)])
    raw_before = db.read_bytes()
    counts = audit.audit_sqlite(db)["counts"]
    assert counts["examined_nonnull"] == 2, counts
    assert counts["typed-v1-header"] == 1, counts
    assert counts["non-v1-legacy-or-unknown"] == 1, counts
    assert db.read_bytes() == raw_before, "audit modified SQLite!"

    xml = ('<SceneObjectGroup><RootPart><KeyframeMotion>' +
           base64.b64encode(HEADER).decode("ascii") +
           '</KeyframeMotion></RootPart></SceneObjectGroup>').encode()
    xml_legacy = ('<SceneObjectGroup><KeyframeMotion>' +
                  base64.b64encode(LEGACY).decode("ascii") +
                  '</KeyframeMotion></SceneObjectGroup>').encode()
    tar_path = folder / "backup.oar"
    with tarfile.open(tar_path, "w:gz") as tar:
        for name, data in [("objects/modern.xml", xml),
                           ("objects/old.xml", xml_legacy),
                           ("assets/not-an-object.xml", xml),
                           ("objects/bad.xml", b"<broken")]:
            member = tarfile.TarInfo(name)
            member.size = len(data)
            tar.addfile(member, io.BytesIO(data))
    tar_before = tar_path.read_bytes()
    oar_counts = audit.audit_oar(tar_path)["counts"]
    assert oar_counts["object_xml_files"] == 3, oar_counts
    assert oar_counts["keyframe_fields"] == 2, oar_counts
    assert oar_counts["typed-v1-header"] == 1, oar_counts
    assert oar_counts["non-v1-legacy-or-unknown"] == 1, oar_counts
    assert oar_counts["invalid-object-xml"] == 1, oar_counts
    assert tar_path.read_bytes() == tar_before, "audit modified OAR!"
    assert audit.main(["--sqlite", str(db), "--oar", str(tar_path),
                       "--json", "--fail-on-legacy"]) == 2

print("Legacy keyframe SQLite/OAR read-only audit: PASS")
