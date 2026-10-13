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
