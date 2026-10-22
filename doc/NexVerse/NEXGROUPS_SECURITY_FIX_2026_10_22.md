# NexGroups security and wallet consistency hardening

Date: 2026-10-22. Target: OpenGenesisLINK World API / native GroupsService / Firestorm.

## Authorization fixes

- `GET /api/v1/groups/{group_id}/accounting` requires a local resident's
  real group membership **and** the union of the resident's assigned group
  roles to include `GroupPowers.Accountable`. An explicitly privileged
  `admin:*` principal can inspect accounts for support.
- `GET` and `DELETE /api/v1/groups/{group_id}/invites/{invite_id}`
  validate the invitation's group against the URL. The underlying native
  GroupsService now permits retrieval or deletion only by the invitation
  recipient or an actual group member with Invite/owner authority.
- Scoped service principal tokens with `groups:manage` **cannot impersonate**
  arbitrary group members by providing `actor_id`. Delegated group mutations
  require `admin:*`; resident mutations use the authenticated resident UUID.
- `requester_id` query impersonation is likewise accepted only with
  `admin:*`. Ordinary resident and scoped-service tokens use their own subject.
- Native GroupsService group invitations still support invited Hypergrid
  identities: the check compares the canonical requesting agent ID with the
  stored invitee ID.

## Transaction boundary

Native group storage and NV$ account storage are separate authorities.
When group creation succeeds but wallet provisioning is unavailable or fails,
the API now returns HTTP 201 with the committed group ID and
`wallet_status: "pending"` instead of incorrectly returning HTTP 500.
`wallet_status: "ready"` means both the Ledger group wallet and its NVBAN
mapping were ensured. Group accounting can idempotently provision a pending
group wallet on the next authorized request. Operator logs record failures
without leaking connection strings or sensitive data.

Neither this patch nor the account self-deletion service removes historical
financial ledger entries, deeds, or external portal data.

## Firestorm money range

Firestorm group summary packets carry a 32-bit unsigned balance. A negative
or > UINT32_MAX ledger value is now explicitly rejected with a service
warning and an unavailable-account reply instead of silent clamping.

## Operational verification

1. Build ROBUST and simulator from the **same** commit and restart both.
2. As group accountant, check accounting endpoint and Firestorm group balance.
3. As a member without Accountable, as a nonmember, and using scoped
   service credentials, verify accounting is denied.
4. Verify an invited avatar can read/decline its own invite; a stranger cannot
   read or delete that invite, even with a known UUID.
5. Test a group creation with the Economy service intentionally unavailable:
   it must return HTTP 201 and `wallet_status=pending`; after recovery, an
   authorized accounting call must complete wallet provisioning.
6. Test `groups:manage` service token plus `actor_id` on create, group
   deletion, role mutation, ban and membership routes: must reject.
7. Run `python3 tools/ci/verify_nexgroups_security.py`; GitHub CI also runs
   `dotnet build --configuration Release OpenSim.sln`.

These are recommended live/CI test cases, not claims that they have already
run against the production grid.
