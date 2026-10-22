#!/usr/bin/env python3
"""Fail-closed source contract for groups auth and multi-store provisioning.

These checks complement (and do not replace) authenticated HTTP integration tests.
"""
from pathlib import Path
import re

api = Path("NexVerse/Server/Api/NexGroupsApi.cs").read_text(encoding="utf-8")
service = Path("OpenSim/Addons/Groups/Service/GroupsService.cs").read_text(encoding="utf-8")
viewer = Path("NexVerse/RegionModules/Economy/NexVerseMoneyModule.cs").read_text(encoding="utf-8")


def segment(source, start, end):
    assert source.count(start) == 1, f"ambiguous or missing symbol: {start}"
    assert end in source.split(start, 1)[1], f"missing method after {start}"
    return source.split(start, 1)[1].split(end, 1)[0]


account = segment(api, 'parts[1].Equals("accounting"', 'parts[1].Equals("capabilities"')
assert "CanReadGroupAccounting(authPrincipal, authAccount, groupId)" in account
assert account.index("CanReadGroupAccounting(") < account.index("economy.EnsureWalletAccount(")
assert "GroupPowers.Accountable" in segment(api, "private bool CanReadGroupAccounting(", "private bool EnsureGroupWallet(")
assert "GetAgentGroupMembership(resident, resident, groupId)" in api
assert "GetAgentGroupRoles(resident, resident, groupId)" in api
assert "account == null || !account.LocalToGrid" in api
assert "principal.HasScope(NexScopes.AdminAll)" in api

invites = segment(api, 'parts[1].Equals("invites"', 'parts[1].Equals("notices"')
assert invites.count("invite.GroupID != groupId") == 2, "GET and DELETE must bind invitation to URL group"
assert invites.count("m_Groups.GetAgentToGroupInvite(") == 2
assert "m_Groups.RemoveAgentToGroupInvite(requester, inviteId)" in invites
assert "WriteFailure(response, e);" in invites
guard = segment(service, "private bool CanAccessInvitation(", "public GroupInviteInfo GetAgentToGroupInvite(")
for marker in ("invite.PrincipalID", "GroupPowers.Invite", "IsOwner(", "string.IsNullOrWhiteSpace"):
    assert marker in guard, f"invite authorization missing {marker}"
assert "if (!CanAccessInvitation(RequestingAgentID, data))" in service
assert "if (!CanAccessInvitation(RequestingAgentID, invite))" in service

actor = segment(api, "private static UUID Actor(", "private static UUID ActorForMutationQuery(")
assert "if (!principal.HasScope(NexScopes.AdminAll))" in actor
actor_query = segment(api, "private static UUID ActorForMutationQuery(", "private static string ActorForRead(")
assert "if (!principal.HasScope(NexScopes.AdminAll))" in actor_query
assert api.count('QueryUuid(request, "actor_id")') == 1, "bypass of delegated mutation authorization"
requester = segment(api, "private static string Requester(", "private void Audit(")
assert "if (principal.HasScope(NexScopes.AdminAll))" in requester
assert requester.index("principal.HasScope(NexScopes.AdminAll)") < requester.index("UUID.TryParse(raw")
assert "return principal.Subject;" in requester

create = segment(api, 'if (path == "/api/v1/groups")', 'if (path == "/api/v1/groups/mine")')
assert 'wallet_status = walletReady ? "ready" : "pending"' in create
assert "walletReady = EnsureGroupWallet(group);" in create
assert 'group_id = createdGroupId.ToString()' in create
assert create.index("CreateGroup(") < create.index("walletReady = EnsureGroupWallet(group);")
assert "catch (Exception walletError)" in create
assert "catch (Exception auditError)" in create
assert 'HttpStatusCode.Created' in create

assert "out uint balance" in viewer
assert "ledgerBalance < 0 || ledgerBalance > uint.MaxValue" in viewer
assert "ledgerBalance > int.MaxValue ? int.MaxValue" not in viewer
assert "balance = (uint)ledgerBalance;" in viewer

print("NexGroups authorization, provisioning and viewer-range contracts: OK")
