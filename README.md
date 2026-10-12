# OpenGenesisLINK

**OpenGenesisLINK v0.9.3.10 Stable** concludes the stabilization train under a documented operator-approved release exception. The delivered feature scope includes .NET 8, login/Hypergrid, economy and authorized GRID SERVICES Masterbank, inventory/asset foundations, World API and operational recovery. **NexExperiences / Firestorm development is paused** and disabled in the RC deployment profile while existing security tests remain. **OGL Pathfinding 2.0, OGL My Outfits 2.0 and any OGL Experiences 2.0 are deferred to the own-viewer project**. OGLVoice remains deferred to its own train. The previous stable release was **v0.9.3.8**.

This repository starts from the published **OpenSimulator 0.9.3.0 ("Nessie") Release** source package. It is intentionally maintained as its own repository rather than as a GitHub fork. The long-term objective is an independent OpenGenesisLINK development line whose architecture, services, protocols, modules, tooling and branding may diverge substantially from OpenSimulator.

## Entwicklung des eigenen OpenGenesisLINK Viewers

**Verbindliche, fortlaufend gepflegte technische Übergabe an den eigenen Viewer-Fork:**
[OGL_VIEWER_DEVELOPMENT.md](doc/NexVerse/OGL_VIEWER_DEVELOPMENT.md).
Darin stehen sämtliche OGL-spezifischen Server-/Viewer-Protokolle, Implementierungsstatus,
JSON-Verträge, Sicherheitsanforderungen, Firestorm-Fallbacks und konkreten Viewer-Arbeitsaufträge.
**Bei jeder Änderung einer eigenen OGL-Viewer-Funktion diese Datei mit aktualisieren.**
Die eigene OGL-Grid-Discovery existiert; nativer JSON-Login befindet sich im Entwicklungszweig
und ist im ausgelieferten Robust-Standardprofil zunächst deaktiviert.

## Upstream baseline

- Upstream project: OpenSimulator
- Baseline release: 0.9.3.0 ("Nessie" Release)
- Release date: 8 November 2024
- Imported archive: `opensim-0.9.3.0-source.tar.gz`
- SHA-256: `8dc78639e47859e76a36a03304c1d44c905aac7807f44c1c0af671d156342eab`
- Original OpenSimulator copyright and license notices remain preserved. The primary OpenSimulator BSD license is stored at `LICENSES/BSD/OpenSimulator-0.9.3.0.txt`; contributor and third-party notices remain available in `CONTRIBUTORS.txt`, `LICENSES/` and `ThirdPartyLicenses/`.

## OpenGenesisLINK licensing

New original OpenGenesisLINK contributions are licensed under the **Mozilla Public License 2.0 (MPL-2.0)** unless a file or directory explicitly states otherwise.

Imported OpenSimulator files do **not** lose or replace their existing BSD/third-party licensing. OpenGenesisLINK changes to inherited files must preserve all applicable copyright and license notices. See `LICENSE_POLICY.md`, `NOTICE.md` and `LICENSES/BSD/README.md`.

## Versioning

The current stable release is **OpenGenesisLINK v0.9.3.10** (operator-approved, with explicitly documented waived evidence requirements). The previous release **v0.9.3.8** remains intact. Version 0.9.3.10 includes runtime stabilization, NV$ ledger/Viewer/Masterbank, inventory foundations and World API. Experience viewer-CAPS, consent/revoke and K/V remain unaccepted and **out of release scope**, with the Experience viewer adapter disabled; historical code/tests remain. My Outfits 2.0, OGL Pathfinding 2.0, any Experiences 2.0 and their viewer-specific acceptance gates are deferred; OGLVoice is also deferred. The standard RC1/Stable evidence policy required 22/23 individually documented tests; this one-time operator-approved Stable closure does **not** assert that the entire formal evidence record was completed. See [release decision](doc/NexVerse/09310_OPERATOR_RELEASE_DECISION.md) and [release notes](doc/NexVerse/RELEASE_0.9.3.10.md). No separate 0.9.3.9 Stable release is planned.

**Deferred Viewer navigation:** [`doc/NexVerse/FUTURE_VIEWER_NAVIGATION.md`](doc/NexVerse/FUTURE_VIEWER_NAVIGATION.md). The current server provides no OGL Pathfinding engine or NavMesh CAPS. The old Experiences/LSL/KV implementation is retained but paused and is not an active 0.9.3.10 release feature.

**Inventory thumbnail compatibility (0.9.3.10 Stable):** The server-side `InventoryThumbnailUpload` capability, persistent item/folder previews, and inventory fetch metadata are implemented. **Known limitation:** Firestorm 7.2.4.80712 on the tested NexVerse/OpenSim configuration does not reliably refresh a replaced outfit-folder thumbnail from its existing inventory cache after relog; rebuilding the inventory cache restores the persisted preview. INV01 is closed as an accepted compatibility limitation, **not** as a fixed viewer refresh bug. See [`doc/NexVerse/FIRESTORM_INVENTORY_THUMBNAIL_09310.md`](doc/NexVerse/FIRESTORM_INVENTORY_THUMBNAIL_09310.md). This does not change the remaining stable release gates.

OpenGenesisLINK continues the existing 0.9.3.x development line. OpenSimulator 0.9.3.0 remains the historical source baseline; upstream OpenSimulator version numbers do not automatically become OpenGenesisLINK version numbers.

The former product name **NexVerse Server** is retired as the server-platform name. **NexVerse remains the virtual world/grid deployment built on OpenGenesisLINK.** Existing internal `NexVerse.*` namespaces, API paths and compatibility-sensitive configuration keys may remain temporarily until they can be migrated without breaking existing deployments.

Every release or development milestone that changes the product version must update `OpenSim/Framework/VersionInfo.cs` together with the corresponding release documentation.

See `VERSIONING.md` for the versioning policy.

## Roadmap

The active development roadmap is maintained in:

**[`doc/NexVerse/ROADMAP.md`](doc/NexVerse/ROADMAP.md)**

The roadmap covers the NEXJAST legacy-cleanup milestone and the World API, identity/social platform, simulator/region/estate control plane, inventory/OAR/IAR, NV$ economy and banking, Groups, Experiences, Search/Places/Land/Destination services, OGLVoice (centralized LiveKit/WebRTC voice), Pathfinding, LSL parity, modern assets/rendering, marketplace, advertising, monitoring, developer platform and the OpenGenesisLINK v0.9.4.0 consolidation target.

See **[`doc/NexVerse/RELEASE_TRAIN_0.9.3.10.md`](doc/NexVerse/RELEASE_TRAIN_0.9.3.10.md)** for the revised 0.9.3.10 runtime/stabilization release gates and **[`doc/NexVerse/OGLVOICE_ARCHITECTURE.md`](doc/NexVerse/OGLVOICE_ARCHITECTURE.md)** for the central voice design.

## Aktueller Versionsstand

**Aktuelle Stable-Version: [OpenGenesisLINK 0.9.3.10](https://github.com/opengenesislink/opengenesislinkserver/releases/tag/v0.9.3.10)** (veröffentlicht am 12. Oktober 2026). Der vorherige Stable-Stand `v0.9.3.8` bleibt erhalten.

**Nächste geplante Entwicklungslinie: 0.9.4.0 – OpenGenesisLINK Generation 1.** Die Planung ist noch keine veröffentlichte oder bereits gestartete Version. OGL Pathfinding 2.0, OGL My Outfits 2.0 und gegebenenfalls OGL Experiences 2.0 sind auf die eigene Viewer-Generation verschoben.

## Historischer Meilenstein 0.9.3.5 – abgeschlossen

`0.9.3.5 Dev` war eine frühere Entwicklungsphase für Inventory, OAR/IAR, den persistenten Job Engine und NexBus. Auch `0.9.3.8 Stable` wurde danach abgeschlossen. **Diese Versionsnummern bezeichnen keine aktuelle Entwicklung.**

Die damals eingeführten Kompatibilitäts- und Servicekomponenten sind in den späteren OpenGenesisLINK-Versionen aufgegangen. Die vollständige historische Abfolge steht in [VERSIONING.md](VERSIONING.md).

## NexVerse 0.9.3.1 NEXJAST release foundation

The NexVerse 0.9.3.1 release contains the first NexVerse-native assemblies:

- `NexVerse.Core` — platform metadata, RBAC/scope contracts, audit contracts and NexBus foundation.
- `NexVerse.Server.Api` — Robust-hosted World API foundation.

Current World API base:

`http://world.stadt-nexverse.de/api/v1`

See `doc/NexVerse/WORLD_API.md`, `doc/NexVerse/DISPLAY_NAMES.md` and `doc/NexVerse/LEGACY_COMPONENTS.md`.

## Development model

OpenGenesisLINK is a continuation and substantial independent development based on the OpenSimulator 0.9.3.0 source baseline. Compatibility with Firestorm, Second Life protocols, Hypergrid and LSL is retained where useful, while obsolete administration and service implementations are progressively replaced by OpenGenesisLINK-owned components.

New major systems should be API-first, testable, observable and documented. Compatibility-sensitive code must be assessed before removal.

## Build baseline

The imported OpenSimulator 0.9.3.0 baseline uses the .NET 8 SDK. The original build instructions are retained in this repository.
