# OGL AssetService build integration

This directory connects the SDK-style addon in `addons/OGLAssetService/` to the standard OpenGenesisLINK build.

The legacy prebuild include (`addon-modules/*/prebuild*.xml`) only supports prebuild project declarations. The OGL AssetService uses an SDK-style .NET project and a NuGet Redis dependency; inserting a second prebuild project would duplicate the assembly and lose NuGet resolution.

Instead, `runprebuild.sh` generates `compile.sh` which first builds `OpenSim.sln`, then calls this directory's `build.sh`. The wrapper builds the addon and copies the addon DLL and its relevant NuGet dependencies to `bin/`. It does not change the running ROBUST configuration.

```bash
./runprebuild.sh
./compile.sh
# Or build the addon independently:
bash addon-modules/OGLAssetService/build.sh Release
```

The addon is **not activated** by the build. The legacy FSAssetService remains the default. See `addons/OGLAssetService/BUILD.md` before configuring a separate staging ROBUST instance.
