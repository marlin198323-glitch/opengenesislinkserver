# OpenGenesisLINK 0.9.3.13 — Release- und Goal-Abnahme

**Stand: 24.10.2026. Status: IN ARBEIT, NICHT als Stable freigegeben.**

Dieser Nachweisplan ergänzt [ROADMAP.md](ROADMAP.md). Nach der Betreiberentscheidung
besteht der Meilenstein **0.9.3.13** ausschließlich aus **17.1 NexAsset / OGL
AssetService** und **17.2 Modern material/content pipeline**. NexMarketplace
und NexMedia sind keine Bestandteile dieses Meilensteins.

Ein grüner CI-Lauf oder ein Merge ist keine Produktionsfreigabe.
FSAssets, SQL, UUIDs, Inventare, Berechtigungen und Hypergrid-Kompatibilität
dürfen nicht ohne ausdrückliche, belegte Umschaltung verändert werden.

## 1. Nachweisregeln

Jedes Gate hat den Status PASS, FAIL, PARTIAL oder NOT_RUN sowie:
- exakten Git-SHA, .NET-/Simulator-/ROBUST-/Viewer-Versionen und CI-Run;
- Testumgebung, Datenbankbackend, Kommandos, erwartetes/tatsächliches Ergebnis;
- datierte, anonymisierte Belege ohne Zugangsdaten, Secrets oder Bürgerdaten;
- bei Fehlern: Fix-PR und erneuten Test auf dem neuen Commit.

Erforderliche Gates mit FAIL, PARTIAL oder NOT_RUN verhindern eine
0.9.3.13-Stable-Freigabe. Optionale Backend-Funktionen dürfen nur als
nicht verfügbar/deaktiviert deklariert werden, wenn der Release-Scope diese
Einschränkung ausdrücklich festlegt.

## 2. Code- und CI-Gates

| ID | Erforderlicher Nachweis | Stand |
| --- | --- | --- |
| C01 | Vollständiger Release-Build auf identischem Commit | NOT_RUN |
| C02 | Mesh-Fee: Legacy/Typed LLSD, reale ZLib-LOD- und Convex-Hull-Blöcke sowie Negativtests | NOT_RUN |
| C03 | AssetService-Unit-, Migration-, MariaDB-/PostgreSQL- und ROBUST-HTTP-/Restart-Tests | NOT_RUN |
| C04 | Security-, Inventar-, HG- und LSL-Regressionsläufe weiterhin erfolgreich | NOT_RUN |
| C05 | Keine veralteten Draft-PRs oder inkompatiblen Altpatches blind mergen | NOT_RUN |

Testkommandos im isolierten Buildverzeichnis:

~~~bash
dotnet build OpenSim.sln -c Release
dotnet run --project tools/ci/NexMeshUploadFeeRegression/NexMeshUploadFeeRegression.csproj -c Release
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c Release
dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release
dotnet build addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release
~~~

PR #238 hat einen historischen ZLib-Fix ohne E2E-Freigabe. Er darf nicht
blind in main gemergt werden. Der bereits gemergte Legacy-LLSD-Fix aus
PR #284 muss erhalten bleiben.

## 3. Storage-/AssetService-Gates (17.1)

| ID | Erforderlicher Nachweis | Stand |
| --- | --- | --- |
| A01 | Konsistenter SQL-/FSAssets-Base-/Spool-Snapshot mit isoliertem Restore | NOT_RUN |
| A02 | Vollständige UUID/Hash-Prüfung, Zeilenzahl, idempotenter SHA-256-Snapshot-Import | NOT_RUN |
| A03 | RAM-/SSD-Cache-Grenzen und Restart-/Invalidierungsverhalten | NOT_RUN |
| A04 | Dual-Write für Texturen, Mesh, Animation, Sound, Skript, Notecard | NOT_RUN |
| A05 | OGL-first nur bei passendem autoritativem FSAssets-SQL-Hash | NOT_RUN |
| A06 | OGL-Datei-/SQLite-Ausfall: FSAssets-Fallback und kontrollierter Restart | NOT_RUN |
| A07 | Mehrere ROBUST-Prozesse, konkurrierende Zugriffe, Viewer/Hypergrid | NOT_RUN |
| A08 | Sicheres Referenz-/Lösch-/GC-Verhalten, keine automatische Entfernung der FSAssets | NOT_RUN |
| A09 | Admin-HTTP, Berechtigungen, Secrets, Auditierung und Rate-Limits abgesichert | NOT_RUN |

Staging-Verfahren:
[OGL AssetService Parallel-Migration](../../addons/OGLAssetService/PARALLEL-MIGRATION-DE.md).
Bis zur gesonderten Cutover-Freigabe bleiben SQL/FSAssets autoritativ,
paralleler OGL-Speicher standardmäßig deaktiviert. Es existiert noch kein
abgenommener eigenständiger verteilter OGL-Assetdienst.

## 4. Mesh-/Material-/Rendering-Gates (17.2)

| ID | Erforderlicher Nachweis | Stand |
| --- | --- | --- |
| M01 | Firestorm: Fee-Antwort state=upload, gültige URL, Upload, Rezzing, Relog | NOT_RUN |
| M02 | LOD- und Hull-Physik, Gebühren und echte ZLib-Parser-/Negativtests | NOT_RUN |
| M03 | PBR-Material-Roundtrip: Metallic/Roughness, Normal/Occlusion und Emissive | NOT_RUN |
| M04 | Assets/Rechte nach Regionwechsel, Neustart und Hypergrid korrekt | NOT_RUN |
| M05 | glTF-Pfad, sichere Validierung und Collada-Rückwärtskompatibilität | NOT_RUN |
| M06 | Terrain-/Reflection-/Environment-Unterstützung mit Viewer-Belegen | NOT_RUN |
| M07 | Ungültige LLSD-/Mesh-/PBR-/Upload-Daten und Berechtigungsverletzungen sicher abgewiesen | NOT_RUN |

Ein vorhandener Parser, ein Material-API-Endpunkt oder eine grüne Unit-Test-
Suite sind kein vollständiger Nachweis der Viewer- und Materialpipeline.

## 5. Betrieb und Stable-Release

| ID | Erforderlicher Nachweis | Stand |
| --- | --- | --- |
| R01 | Vergleich mit Baseline: CPU, RAM, Latenz, Storage, Fehlerquote | NOT_RUN |
| R02 | Recovery-/Rollback-Test mit vollständigem Snapshot ohne Assetverluste | NOT_RUN |
| R03 | Expliziter Scope-Freeze: unterstützte und deaktivierte S3/CDN/GC/glTF-Funktionen | NOT_RUN |
| R04 | RC-Commit/Build/CI und Live-Abnahme mit Operator-Evidence | NOT_RUN |
| R05 | Gesonderte Stable-Entscheidung, Release-Artefakte, Tag, dokumentierter Rollback | NOT_RUN |

Die bestehende [0.9.3.12-RC-/Stable-Matrix](09312_RC1_STABLE_ACCEPTANCE.md)
weist noch offene Formalbelege aus. Ein neues Versionslabel hebt diese
nicht auf. Betroffene Inventory-/Viewer-/LSL-/Recovery-Funktionen müssen
auf dem jeweiligen 0.9.3.13-Kandidaten erneut geprüft werden.

## 6. Implementierungsstand

**Technische Teilabnahme (24.10.2026):** Die Entwicklungs-PRs #293, #295 und #296 sind nach erfolgreichen GitHub-Actions-Läufen in `main` gemergt. Diese CI-Nachweise betreffen die isolierten Code-/Regressionstests, nicht die produktive Storage-, Viewer-, Hypergrid- oder Stable-Freigabe. Die obenstehenden Release-Gates behalten ihren Status, bis sämtliche erforderlichen Tests auf dem exakten Releasekandidaten durchgeführt sind.


- [x] OGL AssetService Gen 1 als deaktiviertes Opt-in im Build (PR #237).
- [x] Opt-in-Parallel-Store, OGL-first und Snapshot-Migration im Code (PR #283).
- [x] Legacy-LLSD-Mesh-Fee-Payloads im Code korrigiert (PR #284).
- [x] ZLib-LOD-/Hull-Decoder und Runtime-Regressionen mit grüner Gesamt-CI in main integriert (PR #293, Merge `8ff93d5c58aa2118e595e8cb2512d7b46ce7d6d9`).
- [x] PBR-Face-Array-Bounds und glTF-JSON-zu-Materialoverride-Regressionen mit grüner Gesamt-CI in main integriert (PR #295, Merge `2d352b612f17dd3b7d68f14fd8eeea66c314b884`).
- [x] OGL-Snapshot-Preflight vor dem Kopieren und SHA-256-Shard-Symlink-Schutz mit automatisierten Negativtests in `main` integriert (PR #296, Merge `69eabe3f99a5baeb523ca054db5f21074907e5b7`; OGL AssetService Addon, Cache and Migration, Atomic Integration Gate und NexVerse CI erfolgreich).
- [ ] AssetService-, Migration-, Fallback- und Restore-Gates nachweisen.
- [ ] Modernen Material-/glTF-/PBR-Scope und Firestorm E2E abschließen.
- [ ] RC/Stable-Testbelege und Betreiberfreigabe dokumentieren.

**Keine Änderungen an produktiven INI-Dateien, FSAssets-Dateien oder Datenbanken.
Kein automatischer Wechsel des AssetConnectors.**
