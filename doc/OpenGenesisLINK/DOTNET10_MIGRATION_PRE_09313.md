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

### Retired unsafe generic persistence

The unused in-repository `Util.SerializeToFile` and
`Util.DeserializeFromFile` methods now explicitly throw
`NotSupportedException`, instead of calling `BinaryFormatter`.
No internal source callers were found. Third-party modules still using
these methods need an explicit typed serializer; the public method
signatures remain only to produce an actionable failure message.

### YEngine serialized script state (new typed exception format)

YEngine no longer uses `BinaryFormatter` in `XMRInstAbstract`.
`ScriptStateExceptionCodec` reconstructs only explicitly supported CLR
exception types, with bounded message lengths and no reflection.
Script-thrown exceptions have a new typed marker retaining the thrown
value through the native `SendObjValue` graph format. Unrecognized
CLR graph types fail closed rather than invoking arbitrary deserialization.
Legacy `Ser.SYSERIAL` / `Ser.THROWNEX` blocks are explicitly rejected.

**Important data-compatibility blocker:** Existing saved scripts and in-flight
script crossings can contain old `BinaryFormatter`-serialized exception
frames. These frames will **not** load on the .NET 10 preview; attempts
must not silently discard the script state. Operators must back up script
state and provide a controlled, isolated migration or reinitialization
procedure, with ownership permissions and user-facing notices.
Only explicitly listed CLR exceptions roundtrip at this stage;
unknown application exceptions fail instead of being serialized.
The new codec is covered by positive and malformed-payload tests,
but those tests do not prove arbitrary persisted script snapshots work.

**Further YEngine hardening in this draft:** `String2SysType` no longer
uses `Type.GetType` on script-controlled persisted array element names.
Only an explicit set of built-in and LSL wrapper types is supported;
nested one-dimensional arrays are limited to depth four. Writes now
reject unrecognized CLR array element types instead of emitting
arbitrary CLR names. CI verifies the guard, and runtime regressions cover
valid and malicious type tags. This reduces type-loading risk, but does
**not** prove that all historic script states remain loadable.

**Remaining production blockers:** A safe, tested migration/reinitialization
strategy for legacy KeyframeMotion and YEngine state in real OAR/region
data; additional bounds on other script-state graphs and strings;
full on-grid bridge/physics/crossing tests; operator-tested rollback.
A green CI build alone is **not** production approval.

### Read-only legacy KeyframeMotion inventory audit

Before planning a .NET 10 production cutover, run the new **read-only**
SQLite/OAR audit against **offline, consistent backups**, not an actively
mutating production database:

```bash
python3 tools/migration/audit_legacy_keyframe.py \
  --sqlite /path/to/region-snapshot.db \
  --oar /path/to/region-backup.oar \
  --json --fail-on-legacy
```

For grids using MariaDB/MySQL region storage, run a **SELECT-only**
export on a consistent backed-up copy / test replica with a database user
who has only SELECT permission. Do not put database passwords on the
command line or commit any export containing private asset data:

```bash
mariadb --defaults-extra-file=/secure/path/region-readonly.cnf \
  --database=REGION_DATABASE --batch --raw --skip-column-names \
  -e 'SELECT HEX(KeyframeMotion) FROM prims WHERE KeyframeMotion IS NOT NULL' \
  > /secure/backups/keyframes.hex
python3 tools/migration/audit_legacy_keyframe.py \
  --hex-lines /secure/backups/keyframes.hex --fail-on-legacy
```

The audit scans SQLite `prims.KeyframeMotion` BLOBs, OAR
`objects/*.xml` and exported MariaDB HEX lines. It classifies binary headers as
`typed-v1-header`, `non-v1-legacy-or-unknown`, invalid or oversized;
it **never deserializes** BinaryFormatter, executes script data or
modifies the original file. Exit status `2` with `--fail-on-legacy`
means migration review is required, `3` means audit failure.
A recognized typed-v1 **header alone does not prove the payload is valid**.
Keep copies of the complete database/OAR and assets for migration and rollback.

The runtime preview now also emits a throttled
`[OGL .NET10 KEYFRAME]` warning when old or corrupted persisted
motion data is rejected. This does **not** protect the original BLOB
from subsequent region/object saves: unsupported motion is not restored,
so **do not deploy the preview to production regions** before verified
offline migration and tested rollback. The log intentionally prints
only error type/count, not user asset contents.

This tool is an **inventory and readiness aid, not a conversion utility**.
No automatic legacy data rewriting or destructive cleanup is introduced.
Its SQLite/OAR/MariaDB-HEX fixtures are part of the CI regression suite.

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

## Hypergrid security and temporary legacy exception

The operator explicitly requested temporary legacy HG Suitcase interoperability
for cross-grid purchases; this exception was merged to `main` in PR #224.
The shipped HG profiles now set `AllowLegacyPublicHGInventory = true`
and register the legacy HG inventory endpoint, **without per-avatar
authentication**. This is an acknowledged risk, not a vulnerability fix.
A .NET runtime migration does not address avatar authorization.

**The HG inventory changes from main (PR #224) have been reconciled
into this .NET 10 draft and its Git history.** Before eventual release,
check for any newer main commits and keep the opt-in plus its rollback
contract intact. Do not replace this operator setting with the older
403/404-only assumptions.
Keep the default behavior when the option is absent fail-closed, and
document how to revoke the override. Follow issue #219 for an
authenticated, interoperable replacement.
