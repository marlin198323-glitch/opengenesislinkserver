# NexGroups HTTP bearer/JWT + MariaDB regression

This is a **runtime, real-TCP HTTP integration test**. It instantiates the
production `NexGroupsApi` handler, `NexApiAuthenticator`, `GroupsService`,
`UserAccountService`, `NexEconomyService` and the SQL-backed NVD ledger.
It uses OpenSim's `BaseHttpServer` to process actual `HttpClient`
requests on an isolated loopback port, with HMAC-signed bearer tokens.

```bash
# Requires an isolated MariaDB test database, generated OpenSim projects
# and the private CI-only connection string.
NEX_GROUPS_MARIADB='Data Source=127.0.0.1;Port=33306;Database=ogl_ledger_ci;User ID=ledger;Password=ledgerpass;Old Guids=true;SslMode=None;' \
dotnet run --configuration Release --project tools/ci/NexGroupsHttpRegression/NexGroupsHttpRegression.csproj
```

## Scenarios

- Unauthenticated and tampered JWTs return 401.
- Insufficient JWT scopes, strangers and non-accountant members receive 403.
- Spoofed `requester_id` does not impersonate a group accountant.
- Group founder and assigned accountants get 200 from the accounting route,
  with real NV$ balance and NVBAN.
- Revoking Accountable reverts access to 403 immediately.
- Invitations are readable by their legitimate recipient and group owner
  but not by other residents.
- A known invite UUID cannot be read or deleted by a stranger, including
  via `requester_id` impersonation.
- Cross-group URL/invite mismatch returns 404.
- When the Economy delegate is temporarily unavailable, a committed group
  creation reports `201 Created` and `wallet_status=pending`;
  subsequent accounting is successfully provisioned after recovery.
- Created groups are deleted after successful test completion.

## Security boundaries

The test issues **nonproduction bearer tokens** for freshly created local
CI users; the issuer, signing key and database are dedicated to the test
run and never used by a production ROBUST instance. The listener binds only
to loopback. It does not need full ROBUST bootstrap to exercise the actual
HTTP transport and handler/authenticator code.

This test does not verify TLS termination or proxy identity trust, and does
not run against the production grid. Those remain separate deployment checks.
