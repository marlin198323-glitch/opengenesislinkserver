#!/usr/bin/env python3
from pathlib import Path

opensim = Path("bin/OpenSim.ini").read_text(encoding="utf-8")
opensim_example = Path("bin/OpenSim.ini.example").read_text(encoding="utf-8")
robust_hg = Path("bin/Robust.HG.ini").read_text(encoding="utf-8")
robust_hg_example = Path("bin/Robust.HG.ini.example").read_text(encoding="utf-8")
login_source = Path("OpenSim/Services/LLLoginService/LLLoginService.cs").read_text(encoding="utf-8")

expected_profile = 'ProfileServiceURL = ${Const|BaseURL}:${Const|PublicPort}'
for name, text in (("bin/OpenSim.ini", opensim), ("bin/OpenSim.ini.example", opensim_example)):
    assert expected_profile in text, f"{name}: UserProfiles must use the configured Robust public port"
    assert 'ProfileServiceURL = ${Const|BaseURL}:8002' not in text, f"{name}: stale profile port 8002"

assert 'BaseHostname = "hg.stadt-nexverse.de"' in robust_hg
assert 'PublicPort = "80"' in robust_hg
login_section = robust_hg.split("\n[LoginService]\n", 1)[1].split("\n[", 1)[0]
assert 'Currency = "NV$"' in login_section, "Firestorm login currency must be NV$"
assert 'WelcomeMessage = "Willkommen in der Stadt NexVerse! ...Beyond the Reality."' in login_section
assert 'WelcomeMessages = "' in login_section, "NexVerse welcome rotation must be enabled"
assert "<USERNAME>" in login_section, "NexVerse welcome rotation must support personalized greetings"
assert 'Random.Shared.Next(m_WelcomeMessages.Length)' in login_source, "Welcome selection must happen per login"
assert "NexVerseTosEnabled = true" in login_section, "NexVerse HG login must enforce versioned ToS"
assert 'NexVerseTosVersion = "1.0"' in login_section
assert 'NexVerseTosUrl = "http://stadt-nexverse.de/buergerverordnung"' in login_section

tos_document = Path("doc/NexVerse/STADT_NEXVERSE_BUERGERORDNUNG_1.0.md").read_bytes()
import hashlib
assert hashlib.sha256(tos_document).hexdigest() == "a57bee6dd48116684a872b736276c6c2ced3dbfff85acc81dfe7597b3ad13204", "Buergerordnung source must remain byte-for-byte intact"
assert "NexVerseTosEnabled" in login_source
assert '"tos", m_NexVerseTosUrl, "false"' in login_source
assert "if (!agreeToTos)" in login_source
assert "StoreUserAccount(currentAccount)" in login_source
assert "m_GridUserService.GetGridUserInfo" in login_source
assert login_source.index("StoreUserAccount(currentAccount)") < login_source.index("m_GridUserService.GetGridUserInfo(PrincipalIDstr)"), "Approval must precede presence"
handler = Path("OpenSim/Server/Handlers/Login/LLLoginHandlers.cs").read_text(encoding="utf-8")
assert 'requestData.ContainsKey("agree_to_tos")' in handler
assert 'map.TryGetValue("agree_to_tos"' in handler
account_handler = Path("OpenSim/Server/Handlers/UserAccounts/UserAccountServerPostHandler.cs").read_text(encoding="utf-8")
account_interface = Path("OpenSim/Services/Interfaces/IUserAccountService.cs").read_text(encoding="utf-8")
account_service = Path("OpenSim/Services/UserAccountService/UserAccountService.cs").read_text(encoding="utf-8")
remote_connector = Path("OpenSim/Services/Connectors/UserAccounts/UserAccountServicesConnector.cs").read_text(encoding="utf-8")
# Regression guard: user-account consent values must survive local AND remote setaccount.
# Previously the connector sent both keys, but the remote handler silently ignored them.
for field in ("NexVerseTosAcceptedVersion", "NexVerseTosAcceptedAtUtc"):
    assert f'if (request.TryGetValue("{field}"' in account_handler, f"Remote setaccount drops {field}"
    assert f'kvp.ContainsKey("{field}")' in account_interface, f"Account hydration drops {field}"
    assert f'result["{field}"]' in account_interface, f"Account serialization drops {field}"
    assert f'd.Data["{field}"]' in account_service, f"Local DB storage drops {field}"
assert 'Dictionary<string, object> structData = data.ToKeyValuePairs();' in remote_connector
assert 'm_UserAccountService.StoreUserAccount(existingAccount)' in account_handler

for dialect, version in (("SQLite", "7"), ("MySQL", "10"), ("PGSQL", "9")):
    migrations = Path(f"OpenSim/Data/{dialect}/Resources/UserAccount.migrations").read_text(encoding="utf-8")
    assert f":VERSION {version}" in migrations
    assert "NexVerseTosAcceptedVersion" in migrations
    assert "NexVerseTosAcceptedAtUtc" in migrations

assert 'processedMessage = welcomeTemplate.Replace("<USERNAME>"' in login_source
assert 'UserProfilesServiceConnector = "${Const|PublicPort}/OpenSim.Server.Handlers.dll:UserProfilesConnector"' in robust_hg

profile_section = robust_hg.split("\n[UserProfilesService]\n", 1)[1].split("\n[", 1)[0]
assert "Enabled = true" in profile_section
assert 'LocalServiceModule = "OpenSim.Services.UserProfilesService.dll:UserProfilesService"' in profile_section

discovery_section = robust_hg.split("\n[NexDiscovery]\n", 1)[1].split("\n[", 1)[0]
assert 'TeleportBaseUri = "hop://${Const|BaseHostname}"' in discovery_section
assert "login.mynexverse.de" not in discovery_section

assert 'hop://hg.stadt-nexverse.de' in robust_hg_example

print("OpenGenesisLINK HG deployment endpoints: OK")
