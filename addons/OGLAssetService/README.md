# OGL AssetService Addon (development branch)

This directory defines an independently deployable asset-service plugin for OpenGenesisLINK.

## Components

- **OGL AssetService**: a ROBUST-side `IAssetService` implementation, loaded through the existing `[AssetService] LocalServiceModule` configuration.
- **OGL Storage Agent**: an optional simulator-host process exposing authenticated object storage operations.
- **OGL Asset Directory**: maps asset UUIDs to immutable content hashes and verified storage replicas.
- **Legacy FSAssets adapter**: reads existing `fsassets` without changing their layout, UUIDs, metadata or payload bytes.

## Non-negotiable compatibility rules

1. The original FSAssets repository remains authoritative until an operator explicitly switches the storage mode.
2. No migration operation deletes or overwrites an original asset.
3. A local cache is disposable; a durable object replica is not.
4. An object is committed only after checksum verification and the configured minimum number of durable replicas acknowledge persistence.
5. A simulator node may leave the grid without orphaning its objects: replica health and relocation are required before decommissioning.
6. Existing OpenSim asset API and Hypergrid interfaces remain intact.
7. JPEG2000 validation must never silently rewrite an original asset; decoding and optional derived previews are separate operations.

## Planned configuration

```ini
[AssetService]
    ; Existing provider remains enabled until the addon has passed integration tests.
    ; LocalServiceModule = "OpenSim.Services.OGLAssetService.dll:OGLAssetConnector"

[OGLAssetService]
    Enabled = false
    Mode = "shadow"
    LegacyBaseDirectory = "./fsassets/data"
    CacheDirectory = "./ogl-asset-cache"
    CacheMaxGiB = 20
    MinimumDurableReplicas = 2
    ValidateJ2K = true
```

The above is a **design contract**, not an operational configuration yet. Do not enable the module until its implementation, dependency registration, and tests have been completed.

## Milestones

1. Implement and test the compatibility adapter and read-through cache.
2. Add a separately compiled `IAssetService` connector with a safe feature flag.
3. Implement authenticated node registration, content-addressed object storage and replica verification.
4. Implement background migration, JPEG2000 diagnostics, metrics and failure recovery.
5. Run integration tests against existing FSAssets, HG asset flows and a multi-node outage scenario.

InventoryGuard is explicitly out of scope.
