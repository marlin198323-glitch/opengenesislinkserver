# NexGroups – SQL-backed authorization regression

## Automated coverage

Run after generating OpenSim project files and building the Release configuration.
The runner uses OpenSim's **real** `GroupsService`, `OpenSim.Data.MySQL`
migrations and MariaDB persistence. The dedicated CI database has no
production accounts. Connection details are supplied only by the CI job.

```bash
NEX_GROUPS_MARIADB='Data Source=127.0.0.1;Port=33306;Database=ogl_ledger_ci;User ID=ledger;Password=ledgerpass;Old Guids=true;SslMode=None;' \
  dotnet run --configuration Release \
  --project tools/ci/NexGroupsSqlAuthorizationRegression/NexGroupsSqlAuthorizationRegression.csproj
```

This integration test:

- Creates a group and tests persisted group-accounting permission.
- Denies strangers, empty actor IDs and nonexistent group IDs.
- Removes `Accountable` from the Everyone role and verifies ordinary
  membership cannot read money.
- Assigns and revokes an accountant-specific role; verifies changes immediately
  and after the service is reopened.
- Creates a real SQL-backed invitation and verifies only the invited resident
  or a group member with Invite/Owner permission can read or delete it.
- Verifies a known invite ID does not allow a stranger to erase the database
  row or obtain group-accounting privileges.
- Removes its test group at the end.

The World API delegates its **resident** group-accounting rights check to
the same database-backed `GroupsService.CanReadGroupAccounting` routine.

## Scope and boundaries

This is a runtime SQL integration test, **not yet an HTTP bearer/JWT test**.
It proves actual group membership, roles and invitation access at the native
storage authority. The original `tools/ci/verify_nexgroups_security.py`
also guards endpoint wiring, `actor_id` scoping and URL/invitation group
binding statically. Full authenticated HTTP coverage with resident bearer tokens
and isolated account/economy fixtures remains the follow-up.
It must include positive accountant, negative accountant, stranger, invite
recipient, cross-group route and wallet-pending scenarios.

CI uses the existing `ledger-db-matrix` MariaDB service. Do not run this
regression against production databases: group migrations and fixtures write
to the configured database.
