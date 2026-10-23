#!/usr/bin/env python3
from pathlib import Path

security = Path("NexVerse/Core/Security/NexSecurity.cs").read_text(encoding="utf-8")
identity = Path("NexVerse/Core/Identity/INexUserService.cs").read_text(encoding="utf-8")
api = Path("NexVerse/Server/Api/NexVerseUserApi.cs").read_text(encoding="utf-8")
oauth = Path("NexVerse/Server/Api/NexOAuthApi.cs").read_text(encoding="utf-8")
account = Path("OpenSim/Services/Interfaces/IUserAccountService.cs").read_text(encoding="utf-8")
account_service = Path("OpenSim/Services/UserAccountService/UserAccountService.cs").read_text(encoding="utf-8")
remote_account_handler = Path("OpenSim/Server/Handlers/UserAccounts/UserAccountServerPostHandler.cs").read_text(encoding="utf-8")
mysql = Path("OpenSim/Data/MySQL/Resources/UserAccount.migrations").read_text(encoding="utf-8")
pgsql = Path("OpenSim/Data/PGSQL/Resources/UserAccount.migrations").read_text(encoding="utf-8")
sqlite = Path("OpenSim/Data/SQLite/Resources/UserAccount.migrations").read_text(encoding="utf-8")

for marker in (
    'public const string Resident = "resident";',
    'public const string Support = "support";',
    'public const string Moderator = "moderator";',
    'public const string RegionManager = "region_manager";',
    'public const string Administrator = "administrator";',
    "TryNormalizeAssignment(",
    "GetEffectiveScopes(",
    "NexScopes.SecurityManage",
    "NexScopes.AdminAll",
    "administrator_role_requires_admin",
    "privileged_scope_requires_admin",
):
    assert marker in security, f"missing authorization policy marker: {marker}"

for marker in (
    "IReadOnlyList<string> Roles",
    "IReadOnlyList<string> ExplicitScopes",
    "SetAuthorization(",
):
    assert marker in identity, f"missing user authorization model marker: {marker}"

for marker in (
    "NexVerseRoles",
    "NexVerseScopes",
):
    assert marker in account, f"missing UserAccount authorization persistence: {marker}"
    assert marker in mysql, f"missing MySQL authorization migration: {marker}"
    assert marker in pgsql, f"missing PostgreSQL authorization migration: {marker}"
    assert marker in sqlite, f"missing SQLite authorization migration: {marker}"

for marker in (
    '"/api/v1/auth/authorization-model"',
    '"authorization"',
    "HandleAuthorizationModel(",
    "HandleUserAuthorization(",
    "NexScopes.SecurityManage",
    "admin_target_requires_admin",
    "reauthentication_required = true",
    '"users.authorization.update"',
    '"user.authorization.changed"',
    "RevokeSubjectRefreshTokens(",
    "account.NexVerseStateChanged = Math.Max(",
):
    assert marker in api, f"missing World API authorization marker: {marker}"

for marker in (
    "NexAuthorizationPolicy.GetEffectiveScopes(",
    "user.Roles",
    "user.ExplicitScopes",
    "NexScopes.RegionsRead",
    "NexScopes.SecurityManage",
):
    assert marker in oauth, f"missing OAuth role/scope marker: {marker}"

# Regression: SQL migrations already had NexVerseRoles/Scopes columns,
# but the UserAccountService read/write mapper omitted both properties and
# PATCH could report success while dropping the new assignments.
# The remote /accounts setaccount handler must preserve them too.
make = account_service.split("private UserAccount MakeUserAccount(", 1)[1].split(
    "public UserAccount GetUserAccount(", 1
)[0]
store = account_service.split("public bool StoreUserAccount(", 1)[1].split(
    "public List<UserAccount> GetUserAccounts(", 1
)[0]
remote = remote_account_handler.split("byte[] StoreAccount(", 1)[1].split(
    "byte[] CreateUser(", 1
)[0]
for field in ("NexVerseRoles", "NexVerseScopes"):
    assert f'd.Data.ContainsKey("{field}")' in make, f"SQL account read omitted {field}"
    assert f'u.{field} = d.Data["{field}"].ToString()' in make, f"SQL hydrate omitted {field}"
    assert f'd.Data["{field}"]' in store, f"SQL account store omitted {field}"
    assert f'request.TryGetValue("{field}"' in remote, f"remote account handler dropped {field}"
    assert f'existingAccount.{field} =' in remote, f"remote account handler ignored {field}"

# A successful PATCH must verify persisted state, not only the transient
# UserAccount instance mutated before the store call.
assignment = api.split("public NexUserRecord SetAuthorization(", 1)[1].split(
    "public bool SetPassword(", 1
)[0]
for marker in (
    "m_UserAccounts.StoreUserAccount(account)",
    "m_UserAccounts.InvalidateCache(id)",
    "m_UserAccounts.GetUserAccount(UUID.Zero, id)",
    "verified.NexVerseRoles",
    "verified.NexVerseScopes",
    "authorization_persistence_failed",
    "return Convert(verified)",
):
    assert marker in assignment, f"Persisted authorization readback missing: {marker}"

# A 100k-user grid derives read scopes on login, never by materializing
# identical assignments in the UserAccounts table. Protected write/transfer
# scopes must stay absent from the automatic resident defaults.
resident_defaults = security.split("private static readonly string[] s_DefaultResidentScopes", 1)[1].split(
    "public static IReadOnlyCollection<string> DefaultResidentScopes", 1
)[0]
for safe in ("InventoryRead", "EconomyRead", "RegionsRead", "DiscoveryRead", "ExperiencesRead"):
    assert f"NexScopes.{safe}" in resident_defaults, f"Missing automatic {safe}"
for unsafe in ("AdminAll", "StatisticsRead", "EconomyTransfer", "SecurityManage",
               "SimulatorsManage", "InventoryWrite", "EstatesManage"):
    assert f"NexScopes.{unsafe}" not in resident_defaults, f"Resident baseline exposed {unsafe}"
assert "foreach (string scope in GetRoleScopes(NexRoles.Administrator))" in security
assert "default_scopes = NexAuthorizationPolicy.DefaultResidentScopes" in api

# Legacy UserLevel changes must also invalidate bearer tokens.
level_method = api.split("public bool SetUserLevel", 1)[1].split("public NexUserRecord SetAuthorization", 1)[0]
assert "NexVerseStateChanged" in level_method

print("User role/scope authorization contract: OK")
