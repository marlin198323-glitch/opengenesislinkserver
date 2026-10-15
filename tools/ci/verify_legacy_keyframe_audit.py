#!/usr/bin/env python3
"""Regression of read-only legacy KeyframeMotion audit (no .NET required)."""
import base64
import importlib.util
import io
from pathlib import Path
import sqlite3
import struct
import tarfile
import tempfile

root = Path(__file__).resolve().parents[2]
path = root / "tools/migration/audit_legacy_keyframe.py"
spec = importlib.util.spec_from_file_location("keyframe_audit", path)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

# Valid v1 blob that matches the exact new KeyframeMotion field sequence:
# magic/version, play mode/data flags/running/counters, base fields, current
# frame, active-frame count and template-frame count (-1 means null).
FRAME = (b"\x00\x00" + struct.pack("<4f", 0, 0, 0, 1) +
         struct.pack("<ii", 50, 50) + struct.pack("<3f", 0, 0, 0) * 2)
VALID = (b"OGKM" + struct.pack("<I", 1) +
         struct.pack("<iiBii", 1, 3, 0, 0, 0) +
         struct.pack("<3f", 0, 0, 0) * 2 +
         struct.pack("<4f", 0, 0, 0, 1) +
         FRAME + struct.pack("<ii", 0, -1))
LEGACY = b"\x00" * 20
assert len(FRAME) == 50 and len(VALID) == 123
assert audit.classify(VALID) == "typed-v1-valid"
assert audit.classify(LEGACY) == "non-v1-legacy-or-unknown"
assert audit.classify(b"") == "empty"
assert audit.classify(b"OGKM") == "truncated-v1-header"
assert audit.classify(b"OGKM" + (99).to_bytes(4, "little")) == "unknown-typed-version"
assert audit.classify(b"x" * (audit.MAX_BLOB_BYTES + 1)) == "oversized"
assert audit.classify(VALID[:-1]) == "typed-v1-invalid"
assert audit.classify(VALID + b"\x00") == "typed-v1-invalid"
assert audit.classify(VALID[:8] + struct.pack("<i", 9) + VALID[12:]) == "typed-v1-invalid"
assert audit.classify(VALID[:12] + struct.pack("<i", 16) + VALID[16:]) == "typed-v1-invalid"
assert audit.classify(VALID[:25] + struct.pack("<f", float("nan")) +
                      VALID[29:]) == "typed-v1-invalid"
assert audit.classify(VALID[:115] + struct.pack("<i", 4097) +
                      VALID[119:]) == "typed-v1-invalid"


with tempfile.TemporaryDirectory() as td:
    folder = Path(td)
    db = folder / "region.sqlite"
    with sqlite3.connect(db) as con:
        con.execute('CREATE TABLE prims (UUID TEXT, KeyframeMotion BLOB)')
        con.executemany('INSERT INTO prims (UUID, KeyframeMotion) VALUES (?, ?)',
                        [("a", VALID), ("b", LEGACY), ("c", None),
                         ("d", VALID[:-1])])
    raw_before = db.read_bytes()
    counts = audit.audit_sqlite(db)["counts"]
    assert counts["examined_nonnull"] == 3, counts
    assert counts["typed-v1-invalid"] == 1, counts
    assert counts["typed-v1-valid"] == 1, counts
    assert counts["non-v1-legacy-or-unknown"] == 1, counts
    assert db.read_bytes() == raw_before, "audit modified SQLite!"

    xml = ('<SceneObjectGroup><RootPart><KeyframeMotion>' +
           base64.b64encode(VALID).decode("ascii") +
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
    assert oar_counts["typed-v1-valid"] == 1, oar_counts
    assert oar_counts["non-v1-legacy-or-unknown"] == 1, oar_counts
    assert oar_counts["invalid-object-xml"] == 1, oar_counts
    assert tar_path.read_bytes() == tar_before, "audit modified OAR!"
    hex_file = folder / "mariadb-keyframes.hex"
    hex_file.write_text(VALID.hex() + "\n" + LEGACY.hex() +
                        "\n" + "not-hex\n", encoding="ascii")
    hex_before = hex_file.read_bytes()
    mysql_counts = audit.audit_hex_lines(hex_file)["counts"]
    assert mysql_counts["examined_nonnull"] == 3, mysql_counts
    assert mysql_counts["typed-v1-valid"] == 1, mysql_counts
    assert mysql_counts["non-v1-legacy-or-unknown"] == 1, mysql_counts
    assert mysql_counts["invalid-hex"] == 1, mysql_counts
    assert hex_file.read_bytes() == hex_before, "audit modified MariaDB export!"
    assert audit.main(["--hex-lines", str(hex_file),
                       "--fail-on-legacy"]) == 2

    assert audit.main(["--sqlite", str(db), "--oar", str(tar_path),
                       "--json", "--fail-on-legacy"]) == 2

print("Legacy keyframe SQLite/OAR/MariaDB read-only audit: PASS")
