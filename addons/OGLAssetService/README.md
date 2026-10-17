# OGL AssetService addon — buildable compatibility preview

The addon builds as a separate .NET 8 assembly and leaves the original FSAssetService and existing FSAssets authoritative. It is **disabled by default** and is not automatically loaded by ROBUST. No migration or service switchover occurs by building it.

## Implemented

- `OGLAssetConnector` extending the existing `FSAssetConnector` in `Mode=compatibility`.
- Optional bounded RAM cache and SHA-256-verified SSD cache for interface-dispatched `GetData`; interface-dispatched writes invalidate local cache entries.
- Experimental Redis revision invalidation. **Do not enable it in production:** cross-process coherence, durable invalidation and failure recovery are not implemented.
- Offline FSAssets file copier with dry-run and SHA-256 verification.
- Offline UUID-to-hash metadata audit, duplicate detection and MariaDB/PostgreSQL isolated backup/restore test fixtures.
- GitHub Actions build and smoke tests.

## Not implemented / release blockers

- Distributed object store, node registration, authenticated replication and replica healing.
- Full production metadata migration orchestration, spool consistency, transactional cutover and exercised rollback.
- Running ROBUST `Get`, `GetMetadata`, `GetData`, `Store`, `Delete` and `UpdateContent` staging integration tests.
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
