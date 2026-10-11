#!/usr/bin/env python3
"""Regression guard for OpenGenesisLINK's compact simulator port/Experience setup."""
from configparser import ConfigParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for rel, architecture, auto in (
    ("bin/OpenSim.ini", "config-include/GridHypergrid.ini", True),
    ("bin/OpenSim.ini.example", "config-include/Standalone.ini", False),
):
    config = ConfigParser(interpolation=None, strict=True)
    with (ROOT / rel).open(encoding="utf-8") as stream:
        config.read_file(stream)
    assert config.sections(), rel
    assert all(config.items(section) for section in config.sections()), rel
    assert config.get("Architecture", "Include-Architecture").strip('"') == architecture, rel
    assert config.getboolean("NexVerseAutoPorts", "Enabled") is auto, rel
    assert config.getint("Network", "http_listener_port") == (0 if auto else 9000), rel
    assert config.getint("XMLRPC", "XmlRpcPort") == -1, rel
    assert not config.getboolean("OGLPathfinding", "Enabled"), rel
    assert not config.getboolean("OGLNpcNavigation", "Enabled"), rel
    assert not config.getboolean("NexVerseNodeAgent", "ManagedRegionCommands"), rel
    experience = config["NexExperiencesViewer"]
    for flag in ("Enabled", "FirestormReadCaps", "FirestormPermissionCaps",
                 "FirestormManageCaps", "ScriptPendingConsent", "NativeExperiencePrompt"):
        assert experience.getboolean(flag), (rel, flag)
    for item, env in (
        ("ApiKey", "NEXVERSE_EXPERIENCES_API_KEY"),
        ("ViewerPermissionsApiKey", "NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY"),
        ("ViewerManageApiKey", "NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY"),
    ):
        assert experience[item] == '"${Environment|' + env + '}"', (rel, item)
source = (ROOT / "OpenSim/Region/Application/ConfigurationLoader.cs").read_text()
assert "NexVerseHttpPortAllocator.Apply(m_config.Source);" in source
assert "lease.Lock(0, 1);" in source
assert "NexVersePorts" in source and 'network.Set("http_listener_port", port);' in source
xml = (ROOT / "OpenSim/Region/CoreModules/Scripting/XMLRPC/XMLRPCModule.cs").read_text()
assert "if (m_remoteDataPort == -1)" in xml
assert 'network?.GetInt("http_listener_port", 0)' in xml
print("OpenGenesisLINK compact INI and shared XML-RPC/automatic HTTP profile: OK")
