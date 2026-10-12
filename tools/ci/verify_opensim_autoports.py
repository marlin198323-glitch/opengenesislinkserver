#!/usr/bin/env python3
"""Validate compact simulator INIs and optional HTTP TCP auto-port allocation."""
from configparser import ConfigParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for rel, arch, enabled, http, xmlrpc in (
    ("bin/OpenSim.ini", "config-include/GridHypergrid.ini", True, 0, -1),
    ("bin/OpenSim.ini.example", "config-include/Standalone.ini", False, 9000, 20800),
):
    src = (ROOT / rel).read_text(encoding="utf-8")
    cfg = ConfigParser(interpolation=None, strict=True)
    cfg.optionxform = str
    cfg.read_string(src)
    assert len(src.splitlines()) < 320, rel
    assert "[Startup] section below." not in src
    assert not any(k in cfg.sections() for k in ("OGLPathfinding", "OGLNpcNavigation"))
    assert cfg.get("Architecture", "Include-Architecture").strip('"') == arch
    assert cfg.getboolean("NexVerseAutoPorts", "Enabled") is enabled
    assert cfg.getint("NexVerseAutoPorts", "HttpPortMin") == 9000
    assert cfg.getint("NexVerseAutoPorts", "HttpPortMax") == 9099
    assert cfg.getint("Network", "http_listener_port") == http
    assert cfg.getint("XMLRPC", "XmlRpcPort") == xmlrpc
    assert cfg.get("Startup", "physics") == "BulletSim"
    assert cfg.get("Startup", "meshing") == "Meshmerizer"
    assert cfg.get("Economy", "economymodule") == "NexVerseMoneyModule"
    # Hypergrid guest NV$ wallets are enabled in operator templates, but
    # unauthorized Masterbank payouts remain disabled by default.
    assert not cfg.getboolean("NexEconomyViewer", "MasterBankEnabled")
    assert cfg.getboolean("NexEconomyViewer", "AllowHypergridGuests")
    # Experiences are paused for 0.9.3.10. Keep inactive opt-in
    # flag definitions for a later train; never activate them by default.
    assert not cfg.getboolean("NexExperiencesViewer", "Enabled")
    for key in ("FirestormReadCaps", "FirestormPermissionCaps",
                "FirestormManageCaps", "ScriptPendingConsent", "NativeExperiencePrompt"):
        assert cfg.getboolean("NexExperiencesViewer", key)
    for key, var in (("ApiKey","NEXVERSE_EXPERIENCES_API_KEY"),
                     ("ViewerPermissionsApiKey","NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY"),
                     ("ViewerManageApiKey","NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY")):
        assert cfg.get("NexExperiencesViewer", key) == '"' + "$" + '{Environment|' + var + '}"'

code = (ROOT / "OpenSim/Region/Application/ConfigurationLoader.cs").read_text()
assert code.index("NexVerseHttpPortAllocator.Apply(m_config.Source);") < code.index("m_networkServersInfo.loadFromConfiguration(m_config.Source);")
for token in ('if (configuredPort != 0)', 'lease.Lock(0, 1);',
              'probe.Start();', 'network.Set("http_listener_port", port);',
              'Path.Combine(Util.configDir(), "data", "NexVersePorts")',
              'GetBoolean("http_listener_ssl", false)',
              'GetBoolean("https_listener", false)'):
    assert token in code, token
xml = (ROOT / "OpenSim/Region/CoreModules/Scripting/XMLRPC/XMLRPCModule.cs").read_text()
assert "if (m_remoteDataPort == -1)" in xml
assert 'network?.GetInt("http_listener_port", 0)' in xml
print("OpenGenesisLINK INIs and HTTP automatic port selection: OK")
