# OGL AssetService cutover gates

The compatibility connector is **not yet** a complete replacement for FSAssetConnector. Never disable the existing storage implementation or migrate/delete FSAssets on a production instance based solely on a cache benchmark.

## Baseline microbenchmark

Run from repository root:

```bash
dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release -- --benchmark
```

The JSON output reports warmed RAM cache hits, operations/s and MiB/s. It is **not** a comparison against the legacy service, and does not measure ROBUST requests, FSAssets reads, Redis, network transfer, or cold-start latency. Run several times on the same hardware and record median and variability.

## Mandatory end-to-end acceptance gates before exclusive routing

1. Test a disposable ROBUST instance with a consistent FSAssets data directory and matching SQL metadata snapshot. Verify `Get`, `GetMetadata`, `GetData`, `Store`, `UpdateContent`, `Delete` and restart persistence.
2. Route all relevant interface calls through the new service; audit direct/base-class and remote Hypergrid write paths. Ensure no legacy service is registered in parallel for the same endpoint.
3. Verify cache invalidation across two ROBUST processes, simultaneous writes, failed writes, process restart and Redis outage/recovery. No stale or deleted payload may be returned.
4. Benchmark legacy and OGL connectors against **the same backed-up dataset**, concurrent client loads, cache-warm and cache-cold conditions. Record p50/p95/p99 latency, throughput, CPU, memory, errors and Redis hit ratio.
5. Perform staged rollout with backup, monitoring and a tested rollback to the existing FSAssetConnector configuration. Keep authoritative FSAssets payloads and SQL metadata unchanged.

Redis revision keys alone are insufficient for coherent distributed caching. Until these gates pass, leave `RedisEnabled=false` on production.

## Redis revision smoke test (opt-in)

On a staging server with Redis reachable and the connection variable already loaded by the shell:

```bash
OGL_ASSET_REDIS_DATABASE=2 dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release -- --redis-smoke
```

The test uses a random, non-production asset ID, asserts revision 0 → 1 → 2, and deletes its temporary revision key after the test. It does not flush or delete existing production keys or prove cross-process cache coherence. It does validate experimental payload caching. Do not print the Redis connection string in diagnostics.

## Experimental Redis payload primitives

`OGLRedisAssetCoordinator.PutPayloadAsync` and `GetPayloadAsync` store and retrieve SHA-256-addressed payloads with a required TTL, an 8 MiB maximum item size, and hash verification on read. The opt-in `--redis-smoke` test now also checks payload round-trip through two independent connections; it creates one additional expiring Redis key. **These methods are not wired into OGLAssetConnector's read path.** Content addressing alone does not make UUID-to-content-hash mappings coherent across writers, and existing asset update/delete paths may bypass the connector. Do not enable distributed payload caching in production until the invalidation and atomic mapping protocol has been reviewed and tested.

The coordinator also exposes `PayloadHits`, `PayloadMisses`, and `PayloadIntegrityFailures` counters. These cover only calls to its experimental `GetPayloadAsync` API, not actual ROBUST traffic. The smoke test asserts counters, malformed SHA-256 rejection, and the 8 MiB payload cap. Redis payload keys are content-addressed and TTL-bound. Some additional randomly generated test revision keys are not removed by the smoke test.

## Experimental revision-scoped asset mapping

`TryPublishMappingAsync(assetId, expectedRevision, contentHash, ttl)` atomically compares the current revision key and publishes an expiring UUID-to-content-hash mapping only when revisions match. `GetMappedHashAsync` retrieves a mapping for a specific revision. The Redis smoke test covers a stale publisher after another connection increments the revision. These primitives are **not used in the production connector**. A correct read protocol must read the revision, fetch authoritative FSAssets, check revision again, then publish; a correct write protocol must invalidate consistently across every writer (including legacy and HG paths). Mapping publication is not an authoritative write. Redis Cluster deployments additionally need same-slot key design before using the Lua script. Until these requirements are implemented and tested, leave distributed cache reads disabled.

`TryGetMappedPayloadAsync(assetId)` is an experimental read helper: it reads the current revision, resolves the revision-scoped mapping, verifies the SHA-256 payload, then rechecks the revision before returning bytes. It returns null on a cache miss or revision change; callers must use authoritative storage. It is not wired into the connector, does not solve writers that bypass invalidation, and does not establish a transaction with FSAssets. Redis outages still raise exceptions and must be handled by any future connector integration.

## Fail-closed cache validation

Malformed or negative Redis revision values now raise an error rather than being treated as revision zero. Payload and mapping TTLs must be positive and no longer than 24 hours. The opt-in smoke test verifies invalid revisions and TTLs on a random test asset ID. These protections do not make the experimental cache production-safe: a durable writer protocol and outage fallback are still required.

## Experimental read-through helper

`ReadThroughAsync(assetId, readAuthoritative, ttl)` provides a stage-only cache miss/hit path and publishes a revision-scoped mapping only when the revision remained stable during the authoritative fetch. It never negative-caches absent assets or caches payloads above 8 MiB. The smoke test covers first miss, subsequent hit, and a second Redis client's invalidation. **Do not wire this into ROBUST production:** the helper does not make legacy/HG writers participate in invalidation, and Redis exceptions still need an explicit authoritative fallback. A concurrent write during an authoritative fetch may still cause the caller to receive the bytes read during that race; no snapshot transaction is provided. The smoke test creates an additional randomly named revision key for the read-through scenario, which is not automatically removed yet.

## Audited legacy API dispatch (October 2026)

Source: `OpenSim/Services/FSAssetService/FSAssetService.cs`. The base `FSAssetConnector.GetData`, `Store`, and `Delete` methods are virtual, so the OGL compatibility connector now overrides them rather than relying on explicit `IAssetService` implementations. This is necessary to cover calls made through a `FSAssetConnector` reference as well as `IAssetService` dispatch. The legacy `UpdateContent` method is **not virtual and currently always returns false**; the OGL connector's explicit interface implementation still delegates to that no-op. `Get`, `GetMetadata`, asynchronous callback overloads, `GetCached`, and `AssetsExist` are not yet replaced or fully audited. This change does not constitute a complete cutover. Compile and test both interface and base-typed dispatch before merging.

## Store identity and failure audit

The compatibility connector now invalidates the original input asset ID as well as the ID returned by `FSAssetConnector.Store`, since the base connector can assign an ID and may report failure using the all-zero UUID. It skips Redis invalidation of that failure sentinel. This does **not** make Store transactional or solve multi-process write races. The connector deliberately continues to initialize its legacy parent with the existing `[AssetService]` configuration (not the separate OGL settings section). A real UpdateContent implementation still requires an audited metadata/hash replacement transaction.

## Read-through race regression

The experimental read-through helper now checks the outer revision snapshot again after its nested cache lookup, and takes a new snapshot before reading authoritative FSAssets on a miss. A deterministic opt-in Redis smoke test increments a test asset revision *inside* the authoritative read callback, then checks that neither the old nor the new revision receives a stale mapping. The callback still returns its read bytes; this is **not** a transactional snapshot guarantee. These changes remain disconnected from the ROBUST read path. All CI and integration gates above still apply.

## Virtual read coverage and failed Store hardening

The compatibility connector now overrides `Get(string)`, `GetMetadata(string)` and `AssetsExist(string[])` as well as `GetData(string)`. All of these take the same in-process lock used for Store/Delete. The inherited callback `Get(string, object, handler)` calls the virtual Get implementation; foreign-service overloads and GetCached still use legacy behavior. This lock is not a distributed transaction and does not protect writes made by another FSAssetConnector instance.

`Store` now invalidates the original, possibly mutated, and returned asset IDs in a `finally` block, including on partial failures. This reduces the chance of retaining stale local entries if FSAssets wrote a payload before failing metadata persistence. Redis invalidation is still a best-effort hint after a successful returned ID and is not a durable outbox. The existing `UpdateContent` implementation remains a legacy no-op. Compile and run staging E2E tests before any cutover.

## Safety gate: single-writer cache opt-in

`[OGLAssetService] SingleWriterCacheEnabled` defaults to **false**. In this default, `GetData` reads the authoritative FSAssets connector directly rather than process-local RAM or SSD cache. `DiskCacheEnabled=true` without `SingleWriterCacheEnabled=true` now fails configuration validation. Enabling both `RedisEnabled` and `SingleWriterCacheEnabled` is rejected; Redis mode continues to bypass local cache reads. **SingleWriterCacheEnabled=true is safe only when exactly one writer can modify the relevant asset IDs and no Hypergrid, legacy connector, admin process, or external database writer can change them.** The flag is an operator assertion, not an automatically verified exclusive lock. Avoid enabling it in production.

## SQL update atomicity blocker

The audited `OpenSim/Data/IFSAssetData.cs` interface exposes `Get(id, out hash)`, `Store(metadata, hash)` and `Delete(id)` but no conditional metadata update, revision token or transaction-scoped compare-and-swap primitive. A new `UpdateContent` based only on Get + Store would be susceptible to concurrent writers and inconsistent metadata/file publication. Do not implement or enable a mutable asset update until the SQL storage backends expose an appropriate atomic operation and integration tests cover concurrent writes, rollback, file persistence and restart. The inherited `FSAssetConnector.UpdateContent` continues to return false.

## Atomic SQL hash compare-and-swap (feature branch)

The optional `OpenSim.Data.IFSAssetHashCompareExchange` interface introduces `TryReplaceHash(id, expectedHash, newHash)` without breaking existing `IFSAssetDataPlugin` implementers. Both `MySQLFSAssetData` (MariaDB/MySQL) and `PGSQLFSAssetData` implement a **single conditional UPDATE statement** of the form `UPDATE ... SET hash = new WHERE id = id AND hash = expected`, with parameterized values and success only when exactly one row is affected. The operation never inserts a missing asset. It must not be confused with a distributed FSAssets+SQL transaction: the new content-addressed file must already exist durably before publishing its hash, and orphaned files must be tolerated after a failed CAS.

**Required integration tests:** create a disposable asset and verify successful CAS; stale expected hash rejection; missing asset rejection; two simultaneous CAS attempts with the same expected hash (exactly one succeeds); restart/re-read; metadata and payload consistency. Test independently against MariaDB/MySQL and PostgreSQL, including their driver affected-row semantics. Do not route `IAssetService.UpdateContent` to this API until those tests and the atomic FSAssets file-staging workflow are complete. SQLite has no FSAssetData implementation in the currently audited provider set. No database migrations or production schema changes are included.

## Experimental atomic content update pipeline (feature branch)

`[OGLAssetService] ExperimentalAtomicUpdatesEnabled = false` is the default. When explicitly enabled on a **disposable staging ROBUST**, interface-dispatched `IAssetService.UpdateContent(id, data)` calls the new protected `FSAssetConnector.TryUpdateContentAtomic` method. The helper reads the current SQL metadata and expected content hash, normalizes Object XML before hashing, writes a unique temporary file into the existing FSAssets spool directory with a flush, publishes the immutable hash-named spool file, verifies that `GetFsData(newHash)` returns the exact staged bytes, and finally calls the optional provider CAS to change the SQL hash only if its previous value still matches. The old payload is not deleted. Failed CAS may leave a harmless unreferenced content file; garbage collection is deliberately out of scope.

**Important limitations:** this is not a distributed transaction or production-ready mutation API. No crash-consistent directory fsync, SQL/file cross-store transaction, cache coherence across other writers, or tested rollback guarantee exists yet. The inherited nonvirtual `FSAssetConnector.UpdateContent` still returns false for base-typed calls; only `IAssetService` dispatch on OGLAssetConnector uses the opt-in path. The opt-in feature has not been compiled or integration-tested on MariaDB/MySQL and PostgreSQL. Verify concurrent update/delete, partial disk failure, read-after-write, object normalization, large assets, service restart, Redis outage, and permission errors before promotion. Never enable on production.

## Disposable concurrent SQL CAS CI regression

`addons/OGLAssetService.Migration/test-atomic-cas.sh mysql|postgres` runs only against the local loopback CI database services, creates an isolated FSAssets-like metadata table, and checks missing IDs, mismatched expected hashes, a successful replacement, readback, stale rejection and two concurrent conditional UPDATE statements with exactly one winner. The asset CI workflow now triggers for `feature/ogl-assetservice-cutover-benchmarks` and executes both provider cases using its existing MariaDB/PostgreSQL services. Test fixtures are removed on exit.

**Scope:** this verifies the database SQL conditional-update semantics, not direct invocation of `MySQLFSAssetData.TryReplaceHash` or `PGSQLFSAssetData.TryReplaceHash`, nor a real ROBUST FSAssets update. Those provider-level and end-to-end tests, plus crash/restart/fault injection, remain release blockers. A successful workflow run must be observed before treating this regression as passed.

## Full compilation gate for atomic updates

`.github/workflows/ogl-assetservice-atomic-integration.yml` runs on the cutover feature branch and relevant pull requests. It generates the OpenSimulator solution, builds the full solution (including MySQL and PostgreSQL FSAsset providers), separately builds `addons/OGLAssetService/OGLAssetService.csproj`, and runs isolated cache tests. **Do not treat this as successful until the actual GitHub Actions run is green.** SQL concurrency fixture tests remain in the separate `ogl-assetservice-tests.yml` workflow; neither workflow currently drives the full ROBUST server through live provider-level `UpdateContent` calls or crash-injection/restart tests. These remain production blockers.

No-op updates now re-read authoritative metadata before reporting success when the computed SHA-256 is unchanged. This does not provide a serializable no-op transaction against concurrent writers, so no-op update races still need an explicit consistency contract before production.

## Failure-path cache coherence hardening

The OGL connector now invalidates RAM and SSD entries in `finally` for both interface-dispatched `UpdateContent` and `Delete`, including exceptions from payload staging, object XML normalization, SQL metadata operations, and legacy deletion. When experimental atomic updates are enabled, it also attempts best-effort Redis revision invalidation even if the call throws (a SQL commit could have preceded a later error). Redis failures increment `RedisInvalidationFailures` and cannot be considered a durable invalidation guarantee; Redis-backed reads remain bypassed. The legacy delete success flag is **not** proof that SQL deletion occurred.

For unchanged hashes, the opt-in atomic update now rechecks current SQL metadata **and** compares the actual FSAssets bytes with the normalized request; a missing/corrupt existing payload must not yield a successful no-op. This still does not close the cross-process no-op race or guarantee atomicity between file publication and SQL CAS. Validate failure injection, filesystem crash recovery, concurrent deletion and read-after-write against real ROBUST before production.

## Atomic FSAssets spool publisher and fault regression

The opt-in FSAssets `TryUpdateContentAtomic` path now uses `AtomicFsAssetPayload.TryPublish` for content-addressed immutable spool files. The helper validates the 64-character SHA-256 filename against the exact payload, writes to a random temporary file in the spool staging directory, flushes file content to disk, moves without overwrite, verifies the published destination, and cleans up its temporary file. Existing FSBase digest files remain authoritative and are byte-checked through `GetFsData` before SQL compare-and-swap. No existing FSAssets file is rewritten or migrated.

The standalone `addons/OGLAssetService.AtomicTests` suite checks initial and idempotent publication, digest/path rejection, corrupt pre-existing digest rejection, concurrent publication by 16 tasks, temporary file cleanup, and a staging directory failure. It is part of the Atomic Integration Gate. These tests do not prove database transaction crash recovery, directory fsync durability, or live ROBUST integration. The opt-in path remains experimental until those tests pass.

## Cross-process publication and interrupted staging regression

The atomic spool regression now launches eight independent `dotnet` processes writing the same SHA-256 asset to the same spool directory, checks all exit codes, checks final payload integrity and ensures no temporary files remain from successful writers. It also simulates a crash **before rename** by leaving a partial staging file and confirms that a subsequent publisher can publish the correct digest without mistaking the orphan for an authoritative asset. The test intentionally does not delete unrelated orphan files: safe orphan garbage collection requires age checks and coordination with active writers.

The GitHub Actions pull-request path filter now includes `addons/OGLAssetService.AtomicTests/**`. This is a filesystem-level regression, **not** a real power-loss or kernel-crash test, a directory-fsync durability guarantee, nor a test of SQL CAS against a running ROBUST instance. These remain mandatory cutover gates.

## SQL CAS rollback and delete races

The disposable MariaDB/PostgreSQL CAS regression now verifies transaction rollback does not publish a metadata hash and that an in-flight writer cannot recreate a deleted metadata row through compare-and-swap. This is a SQL-level regression against CI databases, not yet an end-to-end ROBUST connector test. The previous cross-process .NET test had a CS0161 return-path compilation error; corrected in commit `281edf831eb1e8e45404aee4b1d623293edb34cf`. Both new gates require fresh green CI before merging.

## Filesystem redirection hardening

The immutable spool publisher rejects filesystem symbolic links at the spool root, staging directory and digest destination, both before and after publication. The isolated atomic regression creates each of these symlink cases on Linux/macOS and asserts that publication fails closed. These checks reduce accidental path redirection but do not establish full protection against a privileged adversary replacing directories between checks (TOCTOU); production storage roots must be owned by the service account, with restrictive permissions and no untrusted writers. No existing FSAssets files are rewritten by this change.
