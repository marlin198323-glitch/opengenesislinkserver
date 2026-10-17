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
