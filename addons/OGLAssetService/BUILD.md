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

## Migration metadata dependency and cutover gate

The legacy FSAssetConnector uses `IFSAssetDataPlugin` for the asset-ID-to-content-hash mapping and metadata, and `BaseDirectory` for the content files. Copying `BaseDirectory` alone is not a full migration. Before switching storage, export/restore the matching SQL database snapshot and verify representative IDs through `IAssetService.Get`, `GetMetadata`, and `GetData`.

The copy primitive now rejects file and directory symlinks/reparse points. This is a conservative staging safeguard, **not** a hardened filesystem sandbox; destination path links and concurrent filesystem modifications remain a risk. Use an isolated, operator-controlled destination, with both source and destination offline. A database-consistent snapshot, audit manifest, incremental reconciliation and rollback procedure are still pending.

Redis revision primitives remain unconnected to the live read/write path; never enable a shared cache on the assumption that these primitives guarantee coherence.

## Redis revision integration (experimental, NOT production-safe)

`[OGLAssetService] RedisEnabled = true` activates the Redis revision coordinator. Set the connection string in the `OGL_ASSET_REDIS_CONNECTION` environment variable, not in INI or git. `RedisDatabase = 2` is the default.

In this mode, `IAssetService.GetData` bypasses both local caches and reads FSAssets directly. Successful interface-dispatched `Store`, `Delete`, and `UpdateContent` operations increment a Redis revision. **This is not yet coherent distributed caching**: other write paths bypass this mechanism, and a Redis failure after an authoritative write can throw even though the write succeeded. Redis outage and reconnect behavior, resource disposal, and a durable invalidation outbox must be addressed before production. Leave RedisEnabled=false in production and do not switch to this experimental connector on the live ROBUST.

## Follow-up hardening

Redis revision increment failures no longer change the return value of an already completed authoritative FSAssets write. The connector exposes `RedisInvalidationFailures` as an in-process diagnostic counter. **This is not a durable retry mechanism**: failed invalidations can be lost and therefore shared Redis/local caches must stay disabled. Redis-enabled `GetData` deliberately bypasses the caches.

The migration copier now checks existing destination path components for symlinks and rejects symlink source entries. This does not protect against concurrent path substitution (TOCTOU); run only against quiesced, access-controlled staging directories. Do not treat file copying as a replacement for the metadata database migration.

## Standalone offline migration CLI

The independent .NET 8 console application does not start ROBUST or load FSAssets SQL credentials. Dry-run is the default; only `--copy` writes to the separate target.

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- /path/to/offline/fsassets /path/to/separate/target
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- /path/to/offline/fsassets /path/to/separate/target --copy
```

The CLI returns exit code 1 on any reported file copy failure or unhandled exception and prints JSON counts. It does not copy SQL metadata or switch live service configuration. Take a consistent database backup and stop the FSAssets writer before any copy. The standalone tool and CI workflow have been committed but their successful execution has not yet been verified.

## Offline FSAssets metadata hash audit

From a consistent FSAssets metadata database snapshot, export the distinct **content hashes** (not asset UUIDs), one lowercase or uppercase SHA-256 hex digest per line. The database export procedure is provider-specific and is not yet automated. Do not export credentials or personally identifying asset metadata into the hash file.

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- --verify-hashes /offline/fsassets/base /offline/hashes.txt
# Add --osgrid when [AssetService] UseOsgridFormat=true
```

The command returns nonzero for missing, mismatched or invalid hashes. It supports the FSAssetService's standard and OSGrid directory layouts and checks both raw files and `.gz` files. It does not inspect the active spool directory; stop and drain the writer before taking the filesystem snapshot. It does not validate asset UUID-to-hash associations, metadata row counts, or consistency between the SQL snapshot and the file snapshot. **A clean hash audit is necessary but not sufficient for cutover.**
