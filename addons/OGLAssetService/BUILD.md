# OGL AssetService — first runnable compatibility module

This first increment is a separately compiled .NET 8 DLL that inherits the existing FSAssetConnector. It **does not yet implement** the distributed object store, caching, World API metrics or JPEG2000 validation. All existing asset behavior remains delegated to FSAssetConnector.

Build from repository root:

```bash
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c Release
```

Copy `OpenGenesisLINK.Addons.OGLAssetService.dll` into the same `bin/` directory as the other OpenSim services if the build output is elsewhere. The existing FSAssetService assembly and its dependencies must remain available.

**Only on a disposable staging ROBUST instance with a backed-up FSAssets directory:**

```ini
[AssetService]
    LocalServiceModule = "OpenGenesisLINK.Addons.OGLAssetService.dll:OGLAssetConnector"
    ; Preserve all existing FSAssetConnector settings (BaseDirectory, SpoolDirectory, StorageProvider, etc.).

[OGLAssetService]
    Enabled = true
    Mode = "compatibility"
```

Do not use this on production yet. This is an integration smoke-test entry point, not a performance upgrade. A production switchover requires startup-order checks, service registration tests, and end-to-end asset read/write validation.

**Important:** Because the legacy base constructor initializes first, this module must only be enabled in staging with existing working FSAssetConnector configuration. Feature gating will be moved ahead of initialization before production release.

## Current cache increment

The connector now implements `IAssetService.GetData` with a bounded RAM read-through cache. Configure `[OGLAssetService] MemoryCacheMiB = 64` (0 disables caching). Cache invalidation is wired to interface-dispatched `Store`, `UpdateContent`, and `Delete` calls. Full `Get` asset requests still use the legacy implementation. This is not a distributed cache, and no runtime build or integration test has been verified yet.


## Optional SSD read-through (experimental)

The `IAssetService.GetData` interface path now reads RAM, then an optional checksummed SSD cache, then authoritative FSAssets. Writes through the interface invalidate both caches. Other FSAssetConnector methods (including full-asset Get and direct base-class calls) are not cached. Cache consistency across multiple processes or writers is **not** guaranteed.

Staging-only configuration:

```ini
[OGLAssetService]
Enabled = true
Mode = "compatibility"
MemoryCacheMiB = 64
DiskCacheEnabled = true
DiskCacheMaxMiB = 1024
DiskCacheDirectory = "/var/cache/ogl-assetservice"
```

**Do not enable the disk cache in production:** it now has a best-effort disk quota, but still lacks expiry, cross-process invalidation and verified integration testing. Use a dedicated disposable cache directory, never the FSAssets data directory. The disk cache is not an asset replica or backup. This feature requires a successful build and runtime integration tests before any deployment.


The disk quota is enforced after each successful write using cache-file timestamps. This is a single-process staging implementation, not a hard filesystem quota: concurrent external writers and filesystem errors can exceed the configured limit. Cache metrics are currently in-process properties, **not** published through the World API.

## Redis coordination and FSAssets migration (development only)

`OGLRedisAssetCoordinator` provides Redis revision get/increment primitives using StackExchange.Redis. **It is not wired into the live connector** and does not yet provide a distributed cache or enforce cross-process coherence. Redis persistence and recovery semantics must be designed before using revisions as correctness guarantees. Redis credentials must come from secured runtime configuration, not source control.

`OGLFsassetsMigrator.Run(source, destination, dryRun)` implements a filesystem copy/verify primitive. Dry-run is the default. A successful second run verifies existing target files and skips copying them. It does **not** migrate the FSAssets SQL metadata, validate database-to-file relationships, coordinate live writes, provide a transactional cutover or handle arbitrary symlinked directory trees. Use only on an offline, backed-up staging copy; do not point it at production FSAssets.

Build and smoke test from the repository root:

```bash
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c Release
dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release
```

No successful build or runtime test has yet been observed for these changes.
