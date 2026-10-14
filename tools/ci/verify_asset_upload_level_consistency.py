#!/usr/bin/env python3
"""Guard both uploader transports against divergent LevelUpload configuration."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
read = lambda path: (root / path).read_text(encoding="utf-8")

helper = read("OpenSim/Framework/AssetUploadLevelPolicy.cs")
udp = read("OpenSim/Region/CoreModules/Agent/AssetTransaction/AssetTransactionModule.cs")
caps_module = read("OpenSim/Region/ClientStack/Linden/Caps/BunchOfCaps/BunchOfCapsModule.cs")
caps = read("OpenSim/Region/ClientStack/Linden/Caps/BunchOfCaps/BunchOfCaps.cs")
framework_project = read("OpenSim/Framework/OpenSim.Framework.csproj")
defaults = read("bin/OpenSimDefaults.ini")

assert '<Compile Include="AssetUploadLevelPolicy.cs"' in framework_project
assert 'config.Configs["Permissions"]' in helper
assert 'permissions.Contains("LevelUpload")' in helper
assert 'permissions.GetInt("LevelUpload", 0)' in helper
assert 'config.Configs["Startup"]' in helper
assert 'startup.GetInt("LevelUpload", 0)' in helper
assert helper.index('permissions.GetInt("LevelUpload", 0)') < helper.index('startup.GetInt("LevelUpload", 0)')
assert 'm_levelUpload = AssetUploadLevelPolicy.Resolve(source);' in udp
assert 'ConfigOptions.levelUpload = AssetUploadLevelPolicy.Resolve(config);' in caps_module
assert 'GodController.UserLevel < m_levelUpload' in udp
assert 'GodController.UserLevel < ConfigOptions.levelUpload' in caps
assert '[Permissions]' in defaults and ';LevelUpload = 0' in defaults
assert 'A separate [Mesh] LevelUpload is NOT implemented.' in defaults
print("Asset upload LevelUpload UDP/CAPS consistency: OK")
