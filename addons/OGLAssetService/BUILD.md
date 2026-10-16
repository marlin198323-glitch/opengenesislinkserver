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
