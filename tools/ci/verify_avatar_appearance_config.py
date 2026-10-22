#!/usr/bin/env python3
from pathlib import Path

EXPECTED = {
    "PersistBakedTextures": "true",
    "ReuseTextures": "true",
    "DelayBeforeAppearanceSend": "1",
    "DelayBeforeAppearanceSave": "3",
    "ResendAppearanceUpdates": "false",
    "WearablesRequestDelayMs": "1000",
}

for path in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    content = Path(path).read_text(encoding="utf-8")
    assert "\n[Appearance]\n" in content, f"{path}: missing [Appearance] section"

    section = content.split("\n[Appearance]\n", 1)[1].split("\n[", 1)[0]

    for key, value in EXPECTED.items():
        marker = f"{key} = {value}"
        assert marker in section, f"{path}: missing {marker}"

    xbakes_index = content.index("\n[XBakes]\n")
    appearance_index = content.index("\n[Appearance]\n")
    assert appearance_index < xbakes_index, f"{path}: [Appearance] should be declared before [XBakes]"

# Guard the simulator baseline and the runtime wiring, not only profile text.
defaults = Path("bin/OpenSimDefaults.ini").read_text(encoding="utf-8")
assert "WearablesRequestDelayMs = 4000" in defaults, "legacy default must stay at 4000 ms"

factory = Path("OpenSim/Region/CoreModules/Avatar/AvatarFactory/AvatarFactoryModule.cs").read_text(encoding="utf-8")
for token in (
    'm_wearablesRequestDelayMs = 4000',
    'appearanceConfig.GetInt("WearablesRequestDelayMs", m_wearablesRequestDelayMs)',
    'requestedDelay >= 500 && requestedDelay <= 4000',
    'Thread.Sleep(replyDelayMs);',
):
    assert token in factory, f"missing FastRez runtime guard: {token}"
assert 'Thread.Sleep(4000);' not in factory, "legacy hard-coded 4000ms sleep remains"

for path in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    content = Path(path).read_text(encoding="utf-8")
    assert "\\n[InterestManagement]\\n" in content, f"{path}: missing interest-management section"
    section = content.split("\\n[InterestManagement]\\n", 1)[1].split("\\n[", 1)[0]
    assert "FastRezNearbyFirst = true" in section, f"{path}: FastRez world-rez opt-in missing"

scene = Path("OpenSim/Region/Framework/Scenes/Scene.cs").read_text(encoding="utf-8")
presence = Path("OpenSim/Region/Framework/Scenes/ScenePresence.cs").read_text(encoding="utf-8")
assert "public bool FastRezNearbyFirst = false;" in scene, "fast-rez opt-in default must remain off"
assert 'interestConfig.GetBoolean("FastRezNearbyFirst", FastRezNearbyFirst)' in scene
assert "if (m_scene.FastRezNearbyFirst)" in presence
assert "Vector3.DistanceSquared(origin, grp.AbsolutePosition)" in presence
assert "nearby.AddRange(distant);" in presence
assert "if (cacheEmpty || !grp.IsViewerCachable)" in presence, "viewer-cache probe handling must survive"

print("OpenGenesisLINK FastRez (avatar and near-world) configuration: OK")
