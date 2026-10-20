#!/usr/bin/env python3
"""Guard Stadtportal signup integration and bounded managed IAR preflight."""
from pathlib import Path

api = Path("NexVerse/Server/Api/OglPortalRegistrationApi.cs").read_text()
connector = Path("NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
iface = Path("OpenSim/Services/Interfaces/IUserAccountService.cs").read_text()
account = Path("OpenSim/Services/UserAccountService/UserAccountService.cs").read_text()
security = Path("NexVerse/Core/Security/NexSecurity.cs").read_text()
key_store = Path("NexVerse/Core/Security/NexApiKeyStore.cs").read_text()
inspector = Path("NexVerse/RegionModules/Archives/OglIarManagement.cs").read_text()
manager = Path("NexVerse/RegionModules/Archives/OglIarOperationManager.cs").read_text()

for marker in (
    '"/api/v1/portal/avatars/register"',
    "apiGate.Wrap(portalRegistration.Handle)",
    'GetBoolean("PortalAvatarRegistrationEnabled", false)',
):
    assert marker in connector, "Missing disabled-by-default portal route: " + marker
# Backend-only live availability must use the same machine key and
# normalized names as the authoritative final registration POST.
for marker in (
    '"/api/v1/portal/avatars/name-availability"',
    "apiGate.Wrap(portalRegistration.HandleNameAvailability)",
):
    assert marker in connector, "Live name availability registration missing: " + marker

for marker in (
    "public void HandleNameAvailability(",
    'string.Equals(request.HttpMethod, "GET"',
    "if (!AuthorizePortal(request, response))",
    'request.QueryString?["first_name"]',
    'request.QueryString?["last_name"]',
    'last = "Resident";',
    "m_Accounts.GetUserAccount(UUID.Zero, first, last) == null",
    "status = available ? \"available\" : \"taken\"",
    "HttpStatusCode.OK",
    '"name_check_unavailable"',
    '"invalid_avatar_name"',
):
    assert marker in api, "Live name availability API security/contract missing: " + marker
assert api.count("if (!AuthorizePortal(request, response))") == 2, "Both routes must share identical security checks."
assert api.count("m_Accounts.GetUserAccount(UUID.Zero, first, last)") >= 2, "Live lookup must match final POST lookup."
assert api.index("NexScopes.UsersRegister") < api.index("public void HandleNameAvailability(")
assert api.index("public void HandleNameAvailability(") < api.index("public void Handle(")
assert "X-NexVerse-Api-Key" in api and "HttpStatusCode.Created" in api

for marker in (
    "IOglPortalAccountProvisioner",
    "CreatePortalAvatar(",
    "CreateUser(UUID.Zero, UUID.Random(), firstName, lastName, password, email, modelAlias)",
):
    assert marker in iface + account, "Starter registration pipeline missing: " + marker

for marker in (
    "NexScopes.UsersRegister",
    'request.Headers?["X-NexVerse-Api-Key"]',
    'principal.Subject.StartsWith("api-key:"',
    'IPAddress.IsLoopback(',
    '"X-Forwarded-Proto"',
    'string.IsNullOrWhiteSpace(last)',
    'last = "Resident"',
    '"ogl:benjamin"',
    '"ogl:luna"',
    '"Benjamin Resident"',
    '"Luna Resident"',
    '"avatar_name_taken"',
    '"invalid_email"',
    '"registration_failed"',
    'response.StatusCode = (int)HttpStatusCode.Created',
    "avatar_name = newAccount.FirstName",
):
    assert marker in api, "Portal API missing security or response requirement: " + marker
# HTTPS at KeyHelp + explicit HTTP backend transport:
# headers from unknown remote IPs may not assert TLS. Only the dedicated
# users:register key can request registration, and disabled defaults remain.
for marker in (
    "PortalRegistrationTrustedProxyIPs",
    "PortalRegistrationTrustedHttpPeers",
    'PortalRegistrationDefaultRegionName", "Magdeburg"',
    'PortalRegistrationDefaultX", 116f',
    'PortalRegistrationDefaultY", 120f',
    'PortalRegistrationDefaultZ", 22f',
):
    assert marker in connector, "Missing proxy/spawn config binding: " + marker
for marker in (
    "private static HashSet<string> ParseIpAddresses(",
    "private static bool TrustedPeer(",
    "request.RemoteIPEndPoint?.Address",
    'request.Headers?["X-Forwarded-Proto"]',
    'string.Equals(request.Headers?["X-Forwarded-Proto"], "https"',
    "bool forwardedHttps = trustedProxy &&",
    "bool trustedBackendHttp = TrustedPeer(request, m_TrustedHttpPeers)",
    "bool secured = request.IsSecured || forwardedHttps || trustedBackendHttp",
    "TryReadHomeRegion(root, out string homeRegionId,",
    "new Vector3(m_DefaultHomeX, m_DefaultHomeY, m_DefaultHomeZ)",
    "m_Provisioner.CreatePortalAvatar(",
    '"invalid_home_region"',
    "home_region = new {",
    "last_position = new {",
    "ready = true",
    "correlation_id = NexApiRequestContext.CurrentCorrelationId",
):
    assert marker in api, "Registration API proxy/spawn regression: " + marker
assert api.index("if (!secured)") < api.index('NexScopes.UsersRegister')
assert api.index("NexScopes.UsersRegister") < api.index("TryReadHomeRegion(root")
assert api.index("TryReadHomeRegion(root") < api.index("m_Provisioner.CreatePortalAvatar(")
assert 'request.Headers?["X-Forwarded-For"]' not in api
assert 'request.Headers?["X-Real-IP"]' not in api

for marker in (
    "m_GridService.GetRegionByUUID(UUID.Zero, id)",
    "m_GridService.GetRegionByName(UUID.Zero, homeRegionName.Trim())",
    "home.RegionSizeX",
    "home.RegionSizeY",
    "m_GridUserService.SetHome(",
    "m_GridUserService.SetLastPosition(",
    'account.NexVerseState = "provisioning_failed";',
    "account.Active = false;",
):
    assert marker in account, "Missing atomic-ish starter spawn gate: " + marker
account_portal = account.split("public UserAccount CreatePortalAvatar(string firstName, string lastName,", 2)[2].split("public UserAccount CreateUser(", 1)[0]
assert account_portal.index("m_GridService.GetRegionByName") < account_portal.index("UserAccount account = CreateUser(")
assert account_portal.index("SetHome(") < account_portal.index("SetLastPosition(")
assert account_portal.index("SetLastPosition(") < account_portal.index("return account;")
for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/Robust.HG.OGLAsset.ini.example", "bin/Robust.ini.example"):
    config = Path(path).read_text()
    for marker in ("PortalRegistrationTrustedProxyIPs = \"\"",
                   "PortalRegistrationTrustedHttpPeers = \"\"",
                   'PortalRegistrationDefaultRegionName = "Magdeburg"',
                   "PortalRegistrationDefaultX = 116",
                   "PortalRegistrationDefaultY = 120",
                   "PortalRegistrationDefaultZ = 22"):
        assert marker in config, path + ": " + marker
assert 'public const string UsersRegister = "users:register"' in security
assert "NexScopes.UsersRegister," in key_store
for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example", "bin/Robust.ini.example"):
    assert "PortalAvatarRegistrationEnabled = false" in Path(path).read_text(), path

for marker in (
    "MaxCompressedBytes",
    "MaxExpandedBytes",
    "MaxFileBytes",
    "MaxEntries",
    "expectedChecksum != calculatedChecksum",
    "visitedFiles.Add(path)",
    "ValidatePath(path)",
    "ValidateControlFile(xml)",
    "DtdProcessing.Prohibit",
    "CheckTrailingZeros(tar)",
    "type != (byte)'0'",
    "Start",
):
    assert marker in inspector, "Managed IAR preflight missing validation: " + marker
assert "OglIarInspector.Inspect(path)" in manager
assert manager.index("OglIarInspector.Inspect(path)") < manager.index("m_Archiver.DearchiveInventory(")

print("OGL Stadtportal avatar registration and bounded managed IAR preflight integration: OK")
