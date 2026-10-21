#!/usr/bin/env python3
"""Fail-closed regression contract for citizen self-account-deletion API."""
from pathlib import Path

def read(p):
    return Path(p).read_text(encoding="utf-8")

api = read("NexVerse/Server/Api/NexVerseUserApi.cs")
account = read("OpenSim/Services/UserAccountService/UserAccountService.cs")
iface = read("OpenSim/Services/Interfaces/IUserAccountService.cs")
connector = read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
openapi = read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")

for marker in (
    '"/api/v1/users/me/delete"',
    "HandleDeleteOwnAccount(request, response)",
    "private void HandleDeleteOwnAccount(",
    'RequireMethod(request, response, "POST")',
    'request.Headers?["X-NexVerse-Api-Key"]',
    "m_TrustedSelfDeletionProxyIps.Contains(peerIp)",
    'request.Headers?["X-Forwarded-Proto"]',
    'string.Equals(x.Trim(), "https"',
    "owner.PrincipalID.IsZero()",
    "owner.LocalToGrid",
    "owner.UserLevel >= m_AdminMinimumLevel",
    'root.TryGetProperty("avatar_id"',
    'root.TryGetProperty("principal_id"',
    'root.TryGetProperty("first_name"',
    'root.TryGetProperty("last_name"',
    '"KONTO LÖSCHEN"',
    "m_Users.VerifyPassword(owner.PrincipalID.ToString(), password)",
    "m_LocalDeletion.TryDeleteLocalAvatar(owner.PrincipalID",
    "m_OAuthStore?.RevokeSubjectRefreshTokens(deletedSubject)",
    'deletion_scope = "core_identity_only"',
    "logout_required = true",
):
    assert marker in api, "Missing self-delete security condition: " + marker

for marker in (
    "IOglLocalAvatarDeletion",
    "TryDeleteLocalAvatar(UUID avatarId, out string failureReason)",
    "GridUserInfo state = m_GridUserService.GetGridUserInfo",
    "m_GridUserService.GetGridUserInfo(avatarId.ToString())",
    "UserAccount account = GetUserAccount(UUID.Zero, avatarId)",
    "account.UserLevel >= 200",
    'new[] { "inventoryitems", "avatarID", "text" }',
    'new[] { "inventoryfolders", "agentID", "text" }',
    '"UserAccounts", "PrincipalID"',
    "tx.Commit()",
    "InvalidateCache(avatarId)",
):
    assert marker in account, "Missing transactional purge boundary: " + marker

assert "public interface IOglLocalAvatarDeletion" in iface
assert 'GetBoolean("PortalAccountSelfDeleteEnabled", false)' in connector
assert 'GetString("PortalRegistrationTrustedProxyIPs", "")' in connector
assert '"AccountSelfDeleteRequest"' in openapi
assert '"AccountSelfDeleteResponse"' in openapi

for path, expected in (
    ("bin/Robust.HG.ini", "true"),
    ("bin/Robust.HG.ini.example", "false"),
    ("bin/Robust.HG.OGLAsset.ini.example", "false"),
):
    assert ("PortalAccountSelfDeleteEnabled = " + expected) in read(path), path

docs = read("doc/NexVerse/PORTAL_SELF_ACCOUNT_DELETE.md")
for marker in ("Konto & Sicherheit", "CSRF", "Bearer", "logout_required",
               "core_identity_only", "109.91.20.9"):
    assert marker in docs, marker

print("Citizen self-account-deletion API and explicit portal logout contract: OK")
