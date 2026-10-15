# OpenGenesisLINK v0.9.3.13: staging acceptance and rollback

**STATUS: DRAFT.** Do not deploy PR #220 to live grids until real consistent backups,
legacy state compatibility and a complete restore/rollback on an isolated
staging grid have been verified.

## Release-blocking legacy data

- The .NET10 typed KeyframeMotion reader deliberately rejects old .NET8
  BinaryFormatter BLOBs from region databases and OARs. The .NET10 branch
  now retains rejected bytes separately on the owning prim and passes them
  through normal SQLite/MariaDB saves and XML2/OAR exports; this does not
  enable old motion playback. Explicit script/user motion changes can clear
  the retained data, and unknown snapshots still require offline migration.
- YEngine old SYSERIAL / THROWNEX tokens are not supported by .NET10.
- The updated YEngine now **preserves an incompatible .state file and aborts
  that script's startup** rather than deleting the file and replaying
  state_entry. A stopped script is safer than duplicate payouts or purchases,
  but the user-visible failure requires operator intervention.
- A v11 YEngine snapshot header is not evidence its nested object graph
  can be loaded. The offline audit demands isolated runtime validation.
- Never deserialize untrusted OAR or HG state with BinaryFormatter.
  A legacy converter requires a separate proven and sandboxed migration
  design using trusted representative samples.
- Legacy public HG inventory remains enabled at the operator's explicit
  request via PR #224 and still has the authorization risk tracked in #219.

## Prepare an offline, consistent full snapshot

During a coordinated maintenance/freeze, retain the **matching** .NET8
binaries, ROBUST/region database backups (user inventory, ledger, payments),
OARs, local YEngine .state files, simulator configs, NodeIDs, physics binaries,
and authoritative /fsassets (not just Flotsam cache).

Use the SQLite online backup API, for example: sqlite3 SOURCE_DB
".backup '/backup-location/region.db'". A plain live WAL-file copy may
be inconsistent. For MariaDB, use a consistent backup or a
mariadb-dump --single-transaction performed by a backup-only account.
Use locked-down credential option files. Do not push personal assets,
secrets or inventory dumps to GitHub.

Put only immutable backup **copies** into a directory such as
/srv/ogl-upgrade-09313/backup. The manifest and restore test destinations
must be outside that source folder.

    python3 tools/migration/net10_backup_gate.py manifest --snapshot /srv/ogl-upgrade-09313/backup --manifest /srv/ogl-upgrade-09313/backup.sha256.json
    python3 tools/migration/net10_backup_gate.py verify --snapshot /srv/ogl-upgrade-09313/backup --manifest /srv/ogl-upgrade-09313/backup.sha256.json
    python3 tools/migration/net10_backup_gate.py stage --snapshot /srv/ogl-upgrade-09313/backup --manifest /srv/ogl-upgrade-09313/backup.sha256.json --destination /srv/ogl-upgrade-09313/restore-staging

Staging **never overwrites an existing directory**. It verifies all
SHA-256 hashes, copies into a new isolated directory, then rechecks every
file. This is a byte-identical **backup restore rehearsal**, NOT a database
import, live rollback, working ROBUST boot or real viewer test.

### Inspect state format on immutable backups

    python3 tools/migration/audit_legacy_keyframe.py --sqlite /srv/ogl-upgrade-09313/backup/region.db --oar /srv/ogl-upgrade-09313/backup/region.oar --json --fail-on-legacy
    python3 tools/migration/audit_yengine_states.py --state-dir /srv/ogl-upgrade-09313/backup/yengine --oar /srv/ogl-upgrade-09313/backup/region.oar --json --fail-on-review

For MariaDB, the existing read-only SELECT HEX(KeyframeMotion) export and
--hex-lines option in DOTNET10_MIGRATION_PRE_09313.md remain supported.

The audits do not deserialize arbitrary CLR graphs. A legacy or malformed
keyframe record BLOCKS the release. Every preexisting YEngine snapshot
requires a separate actual script-resume test, including snapshots with
matching v11 migration markers.

## Isolated full restore/rollback test (must be performed on real data)

1. Provision a separate staging database, isolated network, unique region
   ports, dedicated test identities, and zero access to production payments.
2. Restore the byte-verified backup into staging; start the known-good
   production .NET8 binaries. Check asset, inventory, object, script,
   account and NV$ ledger balances, IDs and counts.
3. Snapshot staging, stop ROBUST/simulators, upgrade STAGING ONLY to .NET10.
   Verify YEngine .state resume (no state resets), KeyframeMotion playback,
   Firestorm Bridge HTTP, inventory, OAR and physics.
4. Stop staging again. Restore the original .NET8 binaries and complete
   pre-upgrade database/assets/state/config snapshot. Confirm it boots and
   preserves original UUIDs, balances, scripts and region behavior.
5. Record timestamps, build SHA, manifest hashes, program logs, failed
   state IDs, deviations and operator approval. Do NOT test purchases with
   real user balances.

## Two-simulator / multi-host crossing acceptance

Test two adjacent standard 256-m regions and a VAR region, with two
genuine simulator processes. Repeat from two different hosts over the
normal private site network. Capture **actual Viewer metrics** for both
Firestorm and OGL Viewer.

| Scenario | Repetitions | Verify |
| --- | --- | --- |
| Walk/run four cardinal boundaries | 10 each | Position, no lost avatar |
| Fly at 20 / 50 / 100 m/s | 10 each | P50/P95/P99 frame time, hitch duration |
| Vehicle with passenger, linked/scripted object | 10 each | Positions, attachments, scripts preserved |
| ubOde and BulletS configurations | Each | Native load, collision state |
| VAR 1024-m region and mixed-size boundary | 10 each | No invalid crossing/teleport |
| Heavy inventory, 2+ observers, llListen | 10 each | Complete inventory, ordered chat |
| Hypergrid visit/return and TEST purchase | Each | Asset ownership and ledger idempotency |

Record machine specs, OS/runtime, scene load, network latency/loss,
physics engine and viewer versions. Compare measurements against the
same route on production .NET8. No invented crossing latency or
'no stutter' claim is acceptable.

The GitHub regression verify_multi_process_transport_soak.py runs three
concurrent two-process signed NexBus/llListen transports. This tests
network/event dispatch under concurrency but is NOT an avatar/physics/
Viewer crossing test.

## Production release/rollback policy

Do not release without independently verified backups, representative
legacy migration tests, isolated full restore/rollback, two-host crossing
results and successful final CI on the integration SHA. Rollback requires
the matching binaries, config, database, /fsassets and .state snapshots.
Never point an older .NET8 server at a potentially mutated .NET10 DB
without restoring a compatible backup.

**Execution constraint as of 15 October 2026:** the authorized development
PC and server were offline. No real region restore, old script conversion
or Viewer/physics crossing has been executed. This file establishes an
operator test procedure and release stop gates, not production approval.
