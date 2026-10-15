#!/usr/bin/env python3
"""Guard legacy keyframe data preservation across DB, OAR and clone paths."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def source(path):
    return (root / path).read_text(encoding="utf-8")

part = source("OpenSim/Region/Framework/Scenes/SceneObjectPart.cs")
mysql = source("OpenSim/Data/MySQL/MySQLSimulationData.cs")
sqlite = source("OpenSim/Data/SQLite/SQLiteSimulationData.cs")
xml = source("OpenSim/Region/Framework/Scenes/Serialization/SceneObjectSerializer.cs")
group = source("OpenSim/Region/Framework/Scenes/SceneObjectGroup.cs")
motion = source("OpenSim/Region/Framework/Scenes/KeyframeMotion.cs")
runtime = source("tools/ci/KeyframeMotionSerializationRegression/Program.cs")

for key in (
    "m_unrestoredKeyframeMotion",
    "LoadPersistedKeyframeMotion(SceneObjectGroup group, byte[] data)",
    "HasPersistedKeyframeMotion",
    "SerializePersistedKeyframeMotion()",
    "CopyUnrestoredKeyframeMotionTo(SceneObjectPart destination)",
    "OpenSim.Region.Framework.Scenes.KeyframeMotion.FromData(group, data)",
    "throw new System.IO.InvalidDataException(",
    "m_unrestoredKeyframeMotion = (byte[])data.Clone();",
    "m_unrestoredKeyframeMotion = null;",
):
    assert key in part, "Unsafe or incomplete keyframe persistence: " + key

for name, body in (("MySQL", mysql), ("SQLite", sqlite)):
    assert "prim.LoadPersistedKeyframeMotion(null, data);" in body, name
    assert "prim.SerializePersistedKeyframeMotion()" in body, name
    assert "prim.KeyframeMotion.Serialize()" not in body, name + " loses unsupported motion on save"
    assert "prim.KeyframeMotion = KeyframeMotion.FromData(null, data);" not in body

assert xml.count("LoadPersistedKeyframeMotion(sceneObject, Convert.FromBase64String(") == 2
assert xml.count("HasPersistedKeyframeMotion") == 2, "all OAR/XML writers must retain opaque payload"
assert xml.count("SerializePersistedKeyframeMotion()") == 2
assert "KeyframeMotion.FromData(sceneObject," not in xml
assert "part.CopyUnrestoredKeyframeMotionTo(newPart);" in group
assert "return null;" in motion.split("public static KeyframeMotion FromData", 1)[1].split("public void UpdateSceneObject", 1)[0], "Untrusted blobs may never become executable"
assert "BinaryFormatter" not in part.split("public void LoadPersistedKeyframeMotion", 1)[1].split("public bool HasPersistedKeyframeMotion", 1)[0]

for contract in (
    "legacy motion never becomes executable",
    "inbound motion bytes are defensively copied",
    "outbound motion bytes are defensively copied",
    "unrestored motion survives object duplication",
    "explicit replacement clears preserved rejected state",
    "oversized rejected state aborts loading",
):
    assert contract in runtime, "Missing runtime regression: " + contract

print("Safe .NET10 KeyframeMotion preservation for MySQL/SQLite/OAR/copy: PASS")
