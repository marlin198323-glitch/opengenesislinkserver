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

print("OpenGenesisLINK avatar appearance cache / FastRez wearables configuration: OK")
