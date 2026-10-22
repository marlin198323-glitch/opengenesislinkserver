# OGL AssetService addon — buildable compatibility preview

The addon builds as a separate .NET 8 assembly and leaves the original FSAssetService and existing FSAssets authoritative. It is **disabled by default** and is not automatically loaded by ROBUST. No migration or service switchover occurs by building it.

## Implemented

- `OGLAssetConnector` extending the existing `FSAssetConnector` in `Mode=compatibility`.
- Optional bounded RAM cache and SHA-256-verified SSD cache for interface-dispatched `GetData`; interface-dispatched writes invalidate local cache entries.
- Experimental Redis revision invalidation. **Do not enable it in production:** cross-process coherence, durable invalidation and failure recovery are not implemented.
- Offline FSAssets file copier with dry-run and SHA-256 verification.
- Offline UUID-to-hash metadata audit, duplicate detection and MariaDB/PostgreSQL isolated backup/restore test fixtures.
- GitHub Actions build and smoke tests.
- Isolated ROBUST + MariaDB HTTP persistence smoke with synthetic texture, mesh and notecard payloads: `Store`, `Get`, `GetMetadata`, `GetData`, compressed FSAssets backing and restart readback. Passing CI does not cover production data or viewer flows.
- Atomic metadata/hash compare-and-swap and immutable staging regression tests against disposable MariaDB/PostgreSQL fixtures; experimental update support remains disabled by default.

## Not implemented / release blockers

- Distributed object store, node registration, authenticated replication and replica healing.
- Full production metadata migration orchestration, spool consistency, transactional cutover and exercised rollback.
- Production-equivalent staging acceptance on a **consistent clone of real SQL metadata plus FSAssets file contents**. Requires readback parity, `Store`/`Delete`/`UpdateContent` tests and a rehearsed switch back to the original connector. Legacy `UpdateContent` is not implemented as a normal mutable update; the atomic alternative is experimental and opt-in.
- Verified multi-process cache coherency, Hypergrid interoperability, JPEG2000 diagnostics and production observability.

## Build

From the repository root:

```bash
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c Release
dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release
dotnet build addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release
```

For staging-only configuration and limitations, see [BUILD.md](BUILD.md). For the offline SQL verification process see [EXPORT.md](../OGLAssetService.Migration/EXPORT.md), and for operational acceptance gates see [CUTOVER-ROLLBACK.md](../OGLAssetService.Migration/CUTOVER-ROLLBACK.md).

**Never enable the connector on a live ROBUST merely because the addon is merged into `main`.** The original FSAssets remain unchanged, and the addon must be explicitly configured for a disposable staging instance first.

## Local ROBUST administration API (development)

The addon now exposes `OGLAssetConnector.CreateAdmin(ClaimsPrincipal principal)`, returning a local-only `OGLAssetServiceAdmin` facade. The principal must be supplied by a trusted ROBUST authentication layer; a caller-created or unvalidated principal is not proof of authentication. The facade rejects unauthenticated callers, requires `OGLAssetService.Viewer` or `OGLAssetService.Administrator` for `GetStatus()`, and requires `OGLAssetService.Administrator` for `GetConfiguration()`. Authorization is checked on each call; the parameterless factory was removed. `GetStatus()` returns a snapshot with one of `compatibility`, `experimental` or `degraded`, a reason, and the existing operational diagnostics counters. `GetConfiguration()` exposes only effective mode and feature flags, without secrets, Redis connection strings, filesystem paths, or asset identifiers. `degraded` means an error counter was observed since process start; it is **not** a live database, filesystem or Redis connectivity probe. The facade is read-only and does not register HTTP routes, change settings, or bypass authorization. A future ROBUST administrative HTTP integration must provide authentication, role-based authorization and request throttling before exposing any endpoint. Full functional testing and acceptance are reserved for the final acceptance phase.

### Role integration contract

The ROBUST host must authenticate the request (for example through its existing administrative identity provider), construct a trusted `ClaimsPrincipal` and map operator permissions to the exact roles `OGLAssetService.Viewer` and `OGLAssetService.Administrator`. Only then may it invoke `CreateAdmin(principal)`. A viewer can read `GetStatus()`; only an administrator can read `GetConfiguration()`. Do not accept role names or identity claims from unsigned HTTP headers or query parameters. An application running in the same process remains trusted code and can forge a principal; this is an in-process authorization boundary, not a sandbox. An authenticated, rate-limited ROBUST HTTP adapter is still a separate development task; no route is currently exposed. Comprehensive tests remain scheduled for final acceptance.

## Opt-in ROBUST HTTP administration adapter

`OGLAssetServiceAdminHttpHandler` and `OGLAssetServiceAdminHttpRegistration.Register(server, connector, resolvePrincipal)` provide two **read-only** GET routes: `/ogl/admin/assetservice/status` (Viewer or Administrator) and `/ogl/admin/assetservice/configuration` (Administrator). Responses are JSON with `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`. Denied or failed requests return generic errors rather than credentials or stack traces.

**Not activated by default.** The addon does not register any route during construction. The ROBUST host must supply a trusted `Func<IOSHttpRequest, ClaimsPrincipal>` that validates an existing authenticated session and derives role claims from a server-controlled identity store. Never trust client-supplied `X-User`, `X-Role`, query-string identity, unsigned cookies, or unauthenticated reverse-proxy headers. The resolver must fail closed. A host-provided principal is not proof of identity unless the host validated it first.

Before exposing these routes beyond localhost, the host must additionally enforce TLS, listener/network restrictions, request rate limits, and audit logging. The adapter does not implement these infrastructure controls and is not yet connected to the existing ROBUST identity provider. Production exposure is therefore not authorized. Integration and acceptance tests remain deferred to the final acceptance phase.

## Administrative request controls (development)

Both opt-in read-only HTTP routes share a process-local fixed-window limiter (60 requests per minute across both routes) and an in-memory audit outcome counter set (`allowed`, `unauthorized`, `forbidden`, `throttled`, `errors`). No credentials, identities, remote IPs, asset IDs or request bodies are retained by the counters. This is not a durable security audit trail, not a per-identity rate limiter and not a multi-node limiter. The ROBUST host must still implement trusted authentication, TLS, network restrictions, durable audit logging, and distributed request limiting before production exposure. The handlers are not automatically registered. Final build, integration and acceptance testing are deferred.

## Durable administrative audit journal (optional)

The HTTP registration API now accepts an optional `OGLAssetServiceAdminAuditJournal`. Its JSONL records contain only UTC timestamp, endpoint category (`status` or `configuration`), and outcome (`allowed`, `unauthorized`, `forbidden`, `throttled`, `error`). The journal writes and flushes each entry to durable storage; the host must create and secure the parent directory and retain ownership of journal disposal, file permissions, rotation, retention, and monitoring. It is not activated by default. Example host-side integration after trusted authentication: construct `new OGLAssetServiceAdminAuditJournal("/var/log/ogl/asset-admin.jsonl")`, then call `OGLAssetServiceAdminHttpRegistration.Register(server, connector, trustedPrincipalResolver, journal)`. The host must restrict the listener to an administrative network and validate identities from a server-controlled session. File paths and log destinations must not be accepted from HTTP callers. This does not yet connect the resolver to ROBUST's identity provider. The append-only file is not a tamper-evident security log; storage errors require operator handling and acceptance verification.

## Audit failure handling (implementation)

When a persistent journal is configured, administrative HTTP responses fail closed with HTTP 503 if the audit append/flush operation fails. The in-memory error counter also increments. Journal disposal is idempotent, and writing after disposal raises an error. Existing linked journal targets are rejected on opening; this is not yet a complete defense against ancestor-directory symlinks or filesystem race conditions. Operators must place the journal in a restricted directory, provision it with appropriate ownership/permissions, configure log rotation/retention outside the process, and monitor journal write failures. The ROBUST identity provider remains to be integrated by the host; HTTP routes remain opt-in. Build and acceptance testing are intentionally deferred.

## Configurable local audit retention (development)

The opt-in journal now supports `new OGLAssetServiceAdminAuditJournal(path, maxBytes, retainedFiles)`. A zero/zero pair preserves unbounded legacy append mode; both limits must be set together to enable rotation. `maxBytes` is capped at 1 GiB, and `retainedFiles` at 100. At the size threshold the journal rotates `path` to `path.1`, shifts existing generations and deletes the oldest generation. Rotation and writes are synchronized within one journal instance. The host must ensure **exclusive single-process ownership** of the journal directory and path, and restrict directory permissions; concurrent processes or external logrotate must not manipulate the same generations. Linked generation files are rejected, but ancestor symlink traversal, race-free filesystem access, crash-consistent multi-file rotation and tamper evidence are **not implemented**. A failed rotation makes the administrative HTTP request fail closed (503), and the operator must investigate before re-enabling access. The host still needs authenticated identity integration and independent infrastructure controls. No live ROBUST activation or production cutover is part of this change.

## Dedicated ROBUST service authentication (development)

`OGLAssetServiceRobustAuth` adapts an existing, non-null `IServiceAuth` to an authenticated `ClaimsPrincipal` for a **single dedicated service credential**. `OGLAssetServiceAdminHttpRegistration.RegisterRobustServiceAccount(server, connector, authentication, serviceName, administrator, journal)` is an explicit opt-in integration point. It does not auto-register routes. The credential must be dedicated to this administrative interface and must **never** be the shared public asset-service or grid-to-grid credential. Because `IServiceAuth` returns only success/failure, it cannot identify an individual operator or retrieve operator roles; the role is fixed by the trusted host configuration, not by the request. The host must ensure the provided authenticator is configured to require actual credentials (for example, a dedicated `BasicHttpAuthentication` instance), and must reject `AuthType=None` or policy-only authenticators. The adapter cannot independently prove that a supplied `IServiceAuth` instance checks credentials. Run only behind TLS and restricted administrative network access. Per-operator accounts, secure session management, persistent identity-based audit trails and automated ROBUST host registration remain future integration work. Final tests/acceptance are deferred.
