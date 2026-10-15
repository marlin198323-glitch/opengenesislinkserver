#!/usr/bin/env python3
"""Read-only YEngine script state XML and embedded OAR regression."""
import base64
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
from collections import Counter

root = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("yengine_audit",
    root / "tools/migration/audit_yengine_states.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

def xml(snapshot, engine="YEngine"):
    return ('<ScriptState Engine="' + engine + '"><Snapshot>' +
            base64.b64encode(snapshot).decode("ascii") +
            '</Snapshot></ScriptState>').encode()

with tempfile.TemporaryDirectory() as temp:
    folder = Path(temp)
    states = folder / "state-files"
    states.mkdir()
    (states / "good.state").write_bytes(xml(bytes([11, 16, 0, 1, 2])))
    (states / "old.state").write_bytes(xml(bytes([10, 16, 1])))
    (states / "xengine.state").write_bytes(xml(bytes([0]), "XEngine"))
    (states / "broken.state").write_bytes(b"<ScriptState")
    original = {p.name: p.read_bytes() for p in states.iterdir()}
    c = Counter()
    audit.states_dir(states, c)
    assert c["xml_examined"] == 4, c
    assert c["yengine_states"] == 2, c
    assert c["v11_requires_runtime_validation"] == 1, c
    assert c["unknown_migration_version"] == 1, c
    assert c["xml_invalid"] == 1, c
    assert audit.main(["--state-dir", str(states), "--json", "--fail-on-review"]) == 2
    assert original == {p.name: p.read_bytes() for p in states.iterdir()}

    oar = folder / "scripts.oar"
    embedded = b'<SceneObjectGroup><ScriptStates>' + xml(bytes([11, 16, 7])) + b'</ScriptStates></SceneObjectGroup>'
    with tarfile.open(oar, "w:gz") as archive:
        m = tarfile.TarInfo("objects/test.xml")
        m.size = len(embedded)
        archive.addfile(m, io.BytesIO(embedded))
        skip = tarfile.TarInfo("assets/not-an-object.xml")
        skip.size = len(embedded)
        archive.addfile(skip, io.BytesIO(embedded))
    before = oar.read_bytes()
    oc = Counter()
    audit.oar(oar, oc)
    assert oc["xml_examined"] == 1 and oc["yengine_states"] == 1, oc
    assert oc["v11_requires_runtime_validation"] == 1, oc
    assert oar.read_bytes() == before
    assert audit.main(["--oar", str(oar), "--fail-on-review"]) == 2

    clean = folder / "empty"
    clean.mkdir()
    assert audit.main(["--state-dir", str(clean), "--fail-on-review"]) == 0
    malformed = Counter()
    audit.classify(b'<!DOCTYPE ScriptState><ScriptState/>', malformed)
    assert malformed["xml_unsafe_or_oversized"] == 1

print("YEngine .state / OAR read-only migration audit: PASS")
