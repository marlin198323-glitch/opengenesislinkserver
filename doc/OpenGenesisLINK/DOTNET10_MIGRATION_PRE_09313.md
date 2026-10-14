# OpenGenesisLINK: .NET 10 migration gate (before v0.9.3.13)

## Scope of this migration branch

All OGL SDK-style `.csproj` files now target `net10.0` (except
the separate legacy Prebuild source project). The server and simulator
runtime configuration files use `Microsoft.NETCore.App 10.0.0`, and
GitHub CI provisions the .NET 10 SDK.

**This is a development and compatibility-testing branch, NOT a
production release.** Merge only after complete CI and operator end-to-end
smoke tests, especially physics and HTTP scripting.

The bundled `bin/prebuild.dll` is still a .NET 8 bootstrap that
understands the old `net8_0` generation option but not `net10_0`.
`runprebuild.sh` and `runprebuild.bat` preserve this bootstrap and
immediately retarget generated SDK projects using
`tools/build/retarget_net10.py`. The bootstrap runtime enables
major-version roll-forward for hosts with only .NET 10 installed. This
compatibility shim is temporary; updating the bundled Prebuild
executable is a separate follow-up.

## Critical migration blocker: BinaryFormatter

The codebase still uses the unsafe BinaryFormatter API, including:
- `OpenSim/Framework/Util.cs`
- `OpenSim/Region/Framework/Scenes/KeyframeMotion.cs`
- `OpenSim/Region/ScriptEngine/YEngine/XMRInstAbstract.cs`
- `OpenSim/Region/CoreModules/Asset/FlotsamAssetCache.cs` **(resolved in this branch: replaced with bounded, typed v1 cache serializer; old cache entries are disposable and will be re-fetched from the authoritative Asset Service)**

BinaryFormatter stopped being supported as a regular runtime serializer
from .NET 9 onward. An old runtimeconfig switch is **not** a
replacement. Restoring the old vulnerable serializer through an
unsupported package, especially for untrusted grid assets or script
state, is not an acceptable production fix. Replace deserialization
with constrained, versioned, type-safe encodings and explicitly
document compatibility/migration of existing cache and persisted
objects. Block production rollout until affected workflows pass
targeted serialization and malicious-payload tests.


### Flotsam disk cache migration

`AssetDiskCacheCodec` uses a versioned, length-bounded, fixed-schema binary
cache representation with no runtime class construction from the payload.
Legacy BinaryFormatter cache files are not deserialized: the cache reader
rejects the header and Flotsam deletes/re-fetches them. This is expected to
cause temporary extra Asset Service traffic on first requests. Back up the
cache if diagnostics are important; do not copy old cached serialized blobs
into the new cache after migration. The persistent asset database / fsassets
is not changed by this step. CI tests round trips, invalid IDs, corrupt
lengths, truncation, and trailing garbage.

### Persisted keyframe motion

`KeyframeMotion` writes and reads a **new** bounded typed `O G K M` v1
encoding rather than invoking `BinaryFormatter` on region database or OAR
payloads. A regression exercises active motion frames, nullable keyframe
rotation/position data, iteration counters, exact roundtrip bytes, malformed
headers, truncation, trailing payload and oversized input.

**Operational blocker: existing MySQL/SQLite `KeyframeMotion` columns and
older OAR files may contain legacy BinaryFormatter blobs.** The new reader
refuses to interpret them and currently returns `null` (motion unavailable).
Back up those records and supply an offline, trusted migration/conversion path
before any live deployment. Do not deserialize attacker-provided OAR data
with an unsafe legacy serializer as a shortcut. Do not merge this change
into production until representative existing regions and OARs have passed
a data-preservation trial, including rollback.

**Remaining blockers:** `Util.SerializeToFile` /
`DeserializeFromFile` and YEngine script-state migration still contain
legacy BinaryFormatter calls. Some tests may pass without exercising those
paths; do not treat a green build alone as production readiness.

## Additional release gates

- Full .NET 10 Release build, CI and API/database matrix.
- ROBUST startup, inventory/assets persistence, HG login.
- YEngine LSL HTTP `http_request`, Firestorm Bridge and script resume.
- ubOde/BulletS/meshing native load and physics crossings.
- Avatar walking/running/flying with varregions and mixed-host regions.
- Full backups; restore points and tested downgrade process.
- Windows and Linux AMD64 and ARM64 build where supported.

## Building on a test machine

```bash
dotnet --list-sdks
dotnet --list-runtimes
./runprebuild.sh
python3 tools/ci/verify_net10_migration.py
dotnet build -c Release OpenSim.sln
```

Depending on the Linux distribution, the SDK can be installed
using `dotnet-sdk-10.0` from the appropriate package feed.
The production OGL binaries require the **10.x runtime**. Do not
replace a working production .NET 8 installation before these
compatibility gates pass.

## Hypergrid security

The unauthenticated public HG inventory connector stays disabled.
A .NET runtime migration does not address avatar authorization.
Re-enabling it would restore unauthorized read/write operations.
See issue #219 for an authenticated transfer replacement.
