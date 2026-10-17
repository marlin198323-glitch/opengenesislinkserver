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

The test uses a random, non-production asset ID, asserts revision 0 → 1 → 2, and deletes its temporary revision key after the test. It does not flush or delete existing keys, validate payload caching, or prove cross-process cache coherence. Do not print the Redis connection string in diagnostics.

## Experimental Redis payload primitives

`OGLRedisAssetCoordinator.PutPayloadAsync` and `GetPayloadAsync` store and retrieve SHA-256-addressed payloads with a required TTL, an 8 MiB maximum item size, and hash verification on read. The opt-in `--redis-smoke` test now also checks payload round-trip through two independent connections; it creates one additional expiring Redis key. **These methods are not wired into OGLAssetConnector's read path.** Content addressing alone does not make UUID-to-content-hash mappings coherent across writers, and existing asset update/delete paths may bypass the connector. Do not enable distributed payload caching in production until the invalidation and atomic mapping protocol has been reviewed and tested.

The coordinator also exposes `PayloadHits`, `PayloadMisses`, and `PayloadIntegrityFailures` counters. These cover only calls to its experimental `GetPayloadAsync` API, not actual ROBUST traffic. The smoke test asserts counters, malformed SHA-256 rejection, and the 8 MiB payload cap. Redis payload keys are content-addressed and TTL-bound; revision keys are not removed by the smoke test.

## Experimental revision-scoped asset mapping

`TryPublishMappingAsync(assetId, expectedRevision, contentHash, ttl)` atomically compares the current revision key and publishes an expiring UUID-to-content-hash mapping only when revisions match. `GetMappedHashAsync` retrieves a mapping for a specific revision. The Redis smoke test covers a stale publisher after another connection increments the revision. These primitives are **not used in the production connector**. A correct read protocol must read the revision, fetch authoritative FSAssets, check revision again, then publish; a correct write protocol must invalidate consistently across every writer (including legacy and HG paths). Mapping publication is not an authoritative write. Redis Cluster deployments additionally need same-slot key design before using the Lua script. Until these requirements are implemented and tested, leave distributed cache reads disabled.

`TryGetMappedPayloadAsync(assetId)` is an experimental read helper: it reads the current revision, resolves the revision-scoped mapping, verifies the SHA-256 payload, then rechecks the revision before returning bytes. It returns null on a cache miss or revision change; callers must use authoritative storage. It is not wired into the connector, does not solve writers that bypass invalidation, and does not establish a transaction with FSAssets. Redis outages still raise exceptions and must be handled by any future connector integration.

## Fail-closed cache validation

Malformed or negative Redis revision values now raise an error rather than being treated as revision zero. Payload and mapping TTLs must be positive and no longer than 24 hours. The opt-in smoke test verifies invalid revisions and TTLs on a random test asset ID. These protections do not make the experimental cache production-safe: a durable writer protocol and outage fallback are still required.

## Experimental read-through helper

`ReadThroughAsync(assetId, readAuthoritative, ttl)` provides a stage-only cache miss/hit path and publishes a revision-scoped mapping only when the revision remained stable during the authoritative fetch. It never negative-caches absent assets or caches payloads above 8 MiB. The smoke test covers first miss, subsequent hit, and a second Redis client's invalidation. **Do not wire this into ROBUST production:** the helper does not make legacy/HG writers participate in invalidation, and Redis exceptions still need an explicit authoritative fallback. A concurrent write during an authoritative fetch may still cause the caller to receive the bytes read during that race; no snapshot transaction is provided. The smoke test creates an additional randomly named revision key for the read-through scenario, which is not automatically removed yet.

## Audited legacy API dispatch (October 2026)

Source: `OpenSim/Services/FSAssetService/FSAssetService.cs`. The base `FSAssetConnector.GetData`, `Store`, and `Delete` methods are virtual, so the OGL compatibility connector now overrides them rather than relying on explicit `IAssetService` implementations. This is necessary to cover calls made through a `FSAssetConnector` reference as well as `IAssetService` dispatch. The legacy `UpdateContent` method is **not virtual and currently always returns false**; the OGL connector's explicit interface implementation still delegates to that no-op. `Get`, `GetMetadata`, asynchronous callback overloads, `GetCached`, and `AssetsExist` are not yet replaced or fully audited. This change does not constitute a complete cutover. Compile and test both interface and base-typed dispatch before merging.
