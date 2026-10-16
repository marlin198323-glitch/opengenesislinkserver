# OGL AssetService — World API telemetry contract (draft)

This document defines the intended World API integration. It does not claim an implemented endpoint.

## Access and transport
- World API uses its existing authenticated server-to-server authorization. Do not expose internal node URLs, filesystem paths, or asset identifiers in public aggregate responses.
- Prefer an internal metrics provider registered with the World API over a publicly accessible storage-agent endpoint.
- Read-only endpoints must not scan all assets synchronously: counters are maintained incrementally, reconciled by background jobs, and timestamped.
- Report unknown/unavailable values as null with a freshness timestamp, never fabricated zeros.

## Proposed routes
- `GET /v1/world/assets/status`: health and capacity summary.
- `GET /v1/world/assets/nodes`: per-node capacity, health, replica counts (operator scope).
- `GET /v1/world/assets/types`: aggregate counts and logical bytes by asset type (operator scope).
- `GET /v1/world/assets/diagnostics`: corruption, missing replicas, backlog (operator scope).

Route names are provisional and must be reconciled with existing World API conventions before implementation.

## Example summary response (illustrative numbers only)
```json
{
  "service": "ogl-assetservice",
  "status": "degraded",
  "as_of": "2026-10-16T08:00:00Z",
  "statistics_complete": true,
  "assets": {
    "total": 12450000,
    "logical_bytes": 2400000000000,
    "unique_objects": 12000000,
    "unique_object_bytes": 2100000000000
  },
  "storage": {
    "physical_object_bytes": 3100000000000,
    "cache_bytes": 80000000000,
    "capacity_bytes": 6000000000000,
    "free_bytes": 2820000000000
  },
  "cache": {
    "hit_ratio": 0.942,
    "requests": 1000000,
    "hits": 942000
  },
  "nodes": {
    "registered": 6,
    "healthy": 5
  },
  "integrity": {
    "invalid_j2k_assets": 23,
    "under_replicated_assets": 142
  }
}
```

## Semantics
- `assets.total`: distinct registered asset UUIDs, not physical file count.
- `assets.logical_bytes`: sum of original asset payload sizes per UUID.
- `assets.unique_objects`: unique content hashes; deduplicated objects counted once.
- `storage.physical_object_bytes`: actual durable replica bytes across all nodes, excluding cache.
- `storage.cache_bytes`: cached payload bytes, separate from durable storage.
- `storage.capacity_bytes` / `free_bytes`: summed capacity of eligible configured storage volumes; avoid counting overlapping mounts twice.
- `cache.hit_ratio`: hits / requests for a clearly documented rolling window, null when there are no requests.
- `integrity.invalid_j2k_assets`: confirmed invalid JPEG2000 assets; decoder warnings alone are not definitive proof.
- `status`: online if directory and configured durability threshold are healthy; degraded for partial node or replica failures; offline if unable to serve requests.

## Operational requirements
- Per-type statistics: textures, meshes, animations, sounds, scripts, notecards, objects, and other, based on OpenSim asset type identifiers.
- Support historical time series in monitoring without storing raw asset bytes in the World API.
- Collect node heartbeats and queue depth, and report ingestion/replication failures.
- Legacy FSAssets totals require a background index before completeness can be claimed.
- All counters use 64-bit integers; sizes are expressed in bytes.
- Do not block asset reads if the metrics subsystem is unavailable.
