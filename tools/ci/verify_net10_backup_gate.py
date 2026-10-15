#!/usr/bin/env python3
"""Regression: byte-identical isolated backup staging, tamper and overwrite refusal."""
import importlib.util
import json
from pathlib import Path
import tempfile

root = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("net10_backup_gate",
    root / "tools/migration/net10_backup_gate.py")
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

def rejected(fn, message):
    try:
        fn()
    except (ValueError, OSError):
        return
    raise AssertionError("Expected refusal: " + message)

with tempfile.TemporaryDirectory() as tmp:
    p = Path(tmp)
    snapshot = p / "production-copy"
    snapshot.mkdir()
    (snapshot / "region").mkdir()
    (snapshot / "region" / "sim.sqlite").write_bytes(b"SQLite format 3\x00" + bytes(range(70)))
    (snapshot / "region" / "region.oar").write_bytes(b"synthetic-oar-" * 80)
    (snapshot / "yengine").mkdir()
    (snapshot / "yengine" / "script.state").write_bytes(b"<ScriptState Engine='YEngine' />")
    (snapshot / "fsassets").mkdir()
    (snapshot / "fsassets" / "asset.bin").write_bytes(bytes(range(256)) * 6)
    original = gate.manifest(gate.check_root(snapshot))
    manifest = p / "backup.json"
    assert gate.main(["manifest", "--snapshot", str(snapshot),
                      "--manifest", str(manifest)]) == 0
    assert gate.main(["verify", "--snapshot", str(snapshot),
                      "--manifest", str(manifest)]) == 0
    stage = p / "staging"
    assert gate.main(["stage", "--snapshot", str(snapshot),
                      "--manifest", str(manifest),
                      "--destination", str(stage)]) == 0
    assert gate.manifest(stage) == original, "staged data differ"
    rejected(lambda: gate.stage(snapshot, original, stage), "overwrite stage")
    rejected(lambda: gate.write_manifest(snapshot, snapshot / "inside.json"),
             "writing manifest into snapshot")
    rejected(lambda: gate.stage(snapshot, original, snapshot / "inside"),
             "writing stage into snapshot")
    rejected(lambda: gate.stage(snapshot, original, p), "nonempty existing target")
    (stage / "region" / "region.oar").write_bytes(b"changed")
    rejected(lambda: gate.verify(stage, original), "corrupted staged backup")
    (snapshot / "region" / "sim.sqlite").write_bytes(b"tampered")
    rejected(lambda: gate.verify(snapshot, original), "corrupted source snapshot")
    (snapshot / "region" / "sim.sqlite").write_bytes(b"SQLite format 3\x00" + bytes(range(70)))
    assert gate.verify(snapshot, original) == 4
    invalid = json.loads(manifest.read_text())
    invalid["files"][0]["path"] = "../danger.bin"
    rejected(lambda: gate.verify(snapshot, invalid), "manifest traversal")
    invalid = json.loads(manifest.read_text())
    invalid["files"][0]["sha256"] = "0" * 64
    rejected(lambda: gate.verify(snapshot, invalid), "manifest hash tampering")
    invalid = json.loads(manifest.read_text())
    invalid["files"].append(invalid["files"][0])
    invalid["file_count"] += 1
    rejected(lambda: gate.verify(snapshot, invalid), "duplicate file")
    (snapshot / "evil-link").symlink_to(p / "outside", target_is_directory=False)
    rejected(lambda: gate.manifest(snapshot), "backup symlink")
    (snapshot / "evil-link").unlink()
    assert original == gate.manifest(snapshot)

print("OGL .NET10 backup integrity / non-destructive staging: PASS")
