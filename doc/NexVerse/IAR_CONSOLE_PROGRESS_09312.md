# OpenGenesisLINK 0.9.3.12 — IAR console progress

The classic `save iar` and `load iar` commands emit a phase-aware progress bar through log4net / the region console. The managed IAR API consumes the same optional progress stream and updates `ProgressPercent` and `ProgressPhase`.

## Progress semantics

- **Export:** inventory collection starts at 5%; completion of that phase is 40%; asset transfers advance from 40% to 95% using the actual asset count, including missing assets; finalization is 98%; only successful archive completion emits 100%.
- **Import:** validated entry count is obtained before import; processing advances from 5% to 95% based on entries consumed by the TAR reader, including folders and control files; finalization is 98%; only a successful completion event emits 100%.
- Console output is throttled to 5-percentage-point steps. Inventory counting has no known denominator until inventory traversal is complete; it reports processed items at 250-item intervals instead of inventing a percentage.
- Progress is tied to the request UUID so managed/API jobs do not produce unrelated console progress logs.
- An optional `IInventoryArchiverProgress` interface avoids changing the existing `IInventoryArchiverModule` implementation contract.
- The existing `OperationChanged` API event is emitted when the phase or 5% bucket changes. Consumers may read `ProgressPercent`, `ProgressPhase` and terminal state.

### Examples (illustrative)

```text
[OGL-IAR] Pruefe Archivstruktur, Groessenlimits und TAR/GZip-Integritaet...
[OGL-IAR] Archivpruefung erfolgreich: 5000 Eintraege.
[OGL-IAR] import [#-------------------] 5% (0/5000 Eintraege)
[OGL-IAR] import [###########---------] 55% (2800/5000 Eintraege)
[OGL-IAR] Abschluss: Daten werden finalisiert und geprueft (98%).
[OGL-IAR] [####################] 100% - Import abgeschlossen.
```

## Security and compatibility

The legacy `load iar` console command now requests `IInventoryArchiveInspector`, provided by the OpenGenesisLINK IAR module. The inspector uses the same bounded `OglIarInspector.Inspect` as managed imports. If the inspector is unavailable or rejects the archive, **the console import is denied before mutation**.

The inspector registers even if managed IAR jobs are disabled; no `[OpenGenesisLINKIAR] Enabled=true` setting is required to use console validation. Legacy console imports require a **local archive file**; URI-based IAR imports are not supported in the protected console path.

This does not yet protect unrelated direct programmatic `DearchiveInventory` callers, address file replacement between check and import, or verify every asset's semantic integrity. Those production security gates remain open.

## Test status

- `python3 tools/ci/verify_iar_progress.py` verifies integration invariants.
- `python3 tools/ci/verify_portal_registration_iar_preflight.py` checks existing registration and preflight wiring.
- `dotnet run --project tools/ci/OglIarPreflightRegression/OglIarPreflightRegression.csproj` exercises 11 legitimate/adversarial TAR/GZip fixtures.
- Live console import/export with large representative inventories and a complete server build are required before stable release.
