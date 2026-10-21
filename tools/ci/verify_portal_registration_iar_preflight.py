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
assert '"email_registered"' not in api, "Email contact address is reusable for multiple avatars"
assert "m_Accounts.GetUserAccount(UUID.Zero, email)" not in api, "Do not treat shared email as unique avatar identity"
assert "if (!IsValidEmail(email))" in api, "Contact email remains mandatory and syntactically validated"
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
    'PortalRegistrationDefaultX", 134f',
    'PortalRegistrationDefaultY", 111f',
    'PortalRegistrationDefaultZ", 23f',
):
    assert marker in connector, "Missing proxy/spawn config binding: " + marker
for marker in (
    "private static HashSet<string> ParseIpAddresses(",
    "private static bool TrustedPeer(",
    "request.RemoteIPEndPoint?.Address",
    'request.Headers?["X-Forwarded-Proto"]',
    "private static bool IsForwardedHttps(string header)",
    "bool forwardedHttps = trustedProxy && IsForwardedHttps(forwardedProto)",
    "HTTPS-Proxy-Pruefung verweigert: peer={0}",
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
    if path != "bin/Robust.HG.ini":
        assert 'PortalRegistrationTrustedProxyIPs = ""' in config, path
    for marker in ("PortalRegistrationTrustedHttpPeers = \"\"",
                   'PortalRegistrationDefaultRegionName = "Magdeburg"',
                   "PortalRegistrationDefaultX = 134",
                   "PortalRegistrationDefaultY = 111",
                   "PortalRegistrationDefaultZ = 23"):
        assert marker in config, path + ": " + marker
# Production models are existing, worn users, not empty template aliases.
# The signed backend may select them only by the two whitelisted model IDs.
for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/Robust.HG.OGLAsset.ini.example", "bin/Robust.ini.example"):
    config = Path(path).read_text()
    section = config.split("\n[OglAvatarTemplates]\n", 1)[1].split("\n[", 1)[0]
    for marker in (
        'BenjaminModel = "Benjamin Resident"',
        'LunaModel = "Luna Resident"',
        "RequirePublishedModels = true",
        'BenjaminPublishedModel = "Benjamin Resident"',
        'LunaPublishedModel = "Luna Resident"',
    ):
        assert marker in section, path + ": " + marker
assert "Enabled = true" in Path("bin/Robust.HG.ini").read_text().split(
    "\n[OglAvatarTemplates]\n", 1)[1].split("\n[", 1)[0]
for marker in (
    'Value(root, "start_avatar")',
    'Value(root, "avatar_model")',
    'Value(root, "model")',
    'selectedAvatar == "benjamin resident"',
    'selectedAvatar == "luna resident"',
    'modelAlias = selectedMale || maleModel',
    "root.EnumerateObject()",
    'string.Equals(property.Name.Replace("_", "").Replace("-", "")',
    'request.QueryString?["FirstName"]',
    'request.QueryString?["LastName"]',
    "[OGL-REGISTRIERUNG] FEHLER: HTTP",
    "[OGL-REGISTRIERUNG] ERFOLG:",
):
    assert marker in api, "Portal input compatibility / diagnostics: " + marker
assert 'return null;\n                }\n            }\n            else\n            {\n                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: A user' in account, (
    "Persisted user failure must not be reported as a newly-created account")
assert 'public const string UsersRegister = "users:register"' in security
assert "NexScopes.UsersRegister," in key_store
# Production HG configuration now enables registration only for the
# users:register machine key and explicitly trusted KeyHelp/HTTP backend.
assert "PortalAvatarRegistrationEnabled = true" in Path("bin/Robust.HG.ini").read_text()
assert 'PortalRegistrationTrustedProxyIPs = "109.91.20.9"' in Path("bin/Robust.HG.ini").read_text(), "Production HG proxy IP must be configured"
for path in ("bin/Robust.HG.ini.example", "bin/Robust.ini.example",
             "bin/Robust.HG.OGLAsset.ini.example"):
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
