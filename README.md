# OpenGenesisLINK Server — Generation 1

**Deutsch** · [English](#english)

OpenGenesisLINK (OGL) ist eine eigenständig weiterentwickelte Serverplattform für virtuelle 3D-Welten. Das Projekt ist aus dem Quellcode von OpenSimulator 0.9.3.0 hervorgegangen, hat sich aber durch eigene Dienste, Schnittstellen, Sicherheitsmechanismen und Betriebsfunktionen deutlich über diese historische Ausgangsbasis hinausentwickelt.

**OpenGenesisLINK ist die Plattform. Stadt NexVerse ist eine darauf betriebene virtuelle Welt.** Der frühere Produktname „NexVerse Server“ wird nicht mehr für die Plattform verwendet; bestehende `NexVerse.*`-Namensräume und Konfigurationsschlüssel bleiben dort erhalten, wo Kompatibilität erforderlich ist.

## Plattform und Funktionen

- **Simulator und ROBUST:** Mehrregionenbetrieb, Regions- und Estate-Verwaltung, Login, Teleportation sowie kompatible Viewer- und Hypergrid-Schnittstellen.
- **World API v1:** HTTP-basierte Schnittstellen für Plattformverwaltung, Identität, Profile, Regionen, Statistiken und weitere freigeschaltete Dienste. Geschützte Aktionen verwenden Authentifizierung, Berechtigungen und Scopes.
- **Inventar und Assets:** Persistente Inventar- und Asset-Dienste, Inventar-Thumbnails sowie OAR-/IAR-Verwaltung und Hintergrundaufträge.
- **NV$-Ökonomie:** Konten, Transaktionen und Double-Entry-Ledger; besonders sensible Bankfunktionen sind separat autorisiert und standardmäßig nicht automatisch freigeschaltet.
- **Kommunikation und Dienste:** NexBus für dienstübergreifende Ereignisse, Gruppen-, Profil-, Such- und Weltkartenfunktionen.
- **Sicherheit und Betrieb:** Zugriffskontrollen, Auditierung, Konfigurationsprüfungen, Regressionstests und CI-Prüfungen.

Die tatsächliche Verfügbarkeit hängt von Version, Konfiguration und aktivierten Modulen ab. Vorhandener Code ist nicht automatisch eine produktiv freigegebene Funktion.

## Entwicklungsstand

Der Quellcode meldet derzeit **OpenGenesisLINK 0.9.3.12**. Der Versionsstring allein ist kein Nachweis einer veröffentlichten oder vollständig abgenommenen Stable-Version. Maßgeblich für freigegebene Pakete sind die [GitHub-Releases](https://github.com/opengenesislink/opengenesislinkserver/releases) und die jeweilige Betreiberabnahme.

Die Plattform wird weiterhin aktiv entwickelt. Insbesondere der **OGL AssetService** mit Cache-, Migrations- und Verwaltungsfunktionen befindet sich in gesonderter Integrations- und Abnahmeprüfung. Ein exklusiver Produktivwechsel von FSAssets auf OGL AssetService darf nicht aus einem erfolgreichen Build abgeleitet werden.

OGLVoice, die eigene Viewer-Generation sowie weitergehende Rendering-, Pathfinding- und Experience-Funktionen sind eigenständige Entwicklungsbereiche. Experimentelle oder pausierte Komponenten werden nicht als allgemein verfügbare Stable-Funktionen beworben.

## Architektur auf einen Blick

| Bereich | Rolle |
| --- | --- |
| ROBUST | Zentrale Grid- und Plattformdienste |
| Simulator | Regionen, Szenen, Avatare und Skriptausführung |
| World API | Authentifizierte Verwaltungs- und Integrationsschnittstelle |
| NexBus | Ereignis- und Dienstkommunikation |
| NV$ | Virtuelle Währung und Buchungslogik |
| OGL-Addons | Optionale, separat konfigurierte Erweiterungen |

Die Plattform setzt derzeit auf **.NET 8**. Kompatibilität mit bestehenden OpenSimulator-/Firestorm-Workflows bleibt ein Entwicklungsziel, wird jedoch für neue Funktionen einzeln geprüft.

## Bauen und mitarbeiten

Voraussetzung ist eine zur Repository-Konfiguration passende .NET-8-SDK-Umgebung. Die vorhandene Prebuild-Generierung erstellt Projektdateien für die OpenSimulator-Lösung. Ein typischer Entwicklungsbuild lautet:

```bash
./runprebuild.sh
dotnet build OpenSim.sln -c Release
```

Ein erfolgreicher Build ersetzt weder Konfigurationsprüfung noch Laufzeit-, Sicherheits- oder Migrationstests. Bestehende produktive `bin/`-Konfigurationen, Datenbanken und Asset-Verzeichnisse vor Updates sichern; keine lokalen Betreiberdateien blind überschreiben.

Fehlerberichte und Beiträge können über [Issues](https://github.com/opengenesislink/opengenesislinkserver/issues) und [Pull Requests](https://github.com/opengenesislink/opengenesislinkserver/pulls) eingereicht werden. Änderungen an kompatibilitätskritischen Komponenten sollten vorhandene Protokolle und gespeicherte Daten berücksichtigen.

## Herkunft und Lizenzen

Historische Quellbasis ist **OpenSimulator 0.9.3.0 („Nessie“)**, nicht der aktuelle Funktionsstand von OpenGenesisLINK. Originale Urheberrechts- und Lizenzhinweise bleiben erhalten. Neue eigenständige OpenGenesisLINK-Beiträge stehen, soweit nicht anders gekennzeichnet, unter **MPL-2.0**. Übernommener OpenSimulator-Code und Drittanbieterkomponenten behalten ihre jeweiligen Lizenzbedingungen. Einzelheiten stehen in `LICENSE_POLICY.md`, `NOTICE.md` und `LICENSES/`.

---

## English

OpenGenesisLINK (OGL) is an independently developed server platform for virtual 3D worlds. It originated from the OpenSimulator 0.9.3.0 source release, but its services, APIs, security controls and operational architecture have evolved substantially beyond that historical baseline.

**OpenGenesisLINK is the server platform; Stadt NexVerse is a virtual world running on it.** The former platform name “NexVerse Server” has been retired. Compatibility-sensitive `NexVerse.*` namespaces and configuration keys may remain in existing integrations.

### Capabilities

- **Simulator and ROBUST:** Multi-region hosting, region and estate administration, login, teleportation, and compatible viewer/Hypergrid interfaces.
- **World API v1:** HTTP APIs for platform administration, identity, profiles, regions, statistics and other enabled services, with authentication and scoped authorization for protected operations.
- **Inventory and assets:** Persistent inventory and asset services, inventory thumbnails, OAR/IAR workflows and background jobs.
- **NV$ economy:** Accounts, transactions and double-entry accounting; sensitive banking operations require explicit authorization.
- **Platform services:** NexBus events, groups, profiles, search and world-map integration.
- **Operations and security:** Access controls, auditing, configuration checks, regression coverage and continuous integration.

Availability depends on the deployed version, configuration and enabled modules. Code present in the repository does not necessarily represent an accepted production feature.

### Current status

The current source version string is **OpenGenesisLINK 0.9.3.12**. This does not by itself establish a published or fully accepted stable release. Consult [GitHub Releases](https://github.com/opengenesislink/opengenesislinkserver/releases) for published artifacts and release status.

The **OGL AssetService** is undergoing separate integration and acceptance work; successful compilation alone does not authorize a production cutover from FSAssets. OGLVoice, the future OGL viewer and advanced rendering, pathfinding and experience features follow separate development and validation tracks.

### Architecture

| Component | Responsibility |
| --- | --- |
| ROBUST | Central grid and platform services |
| Simulator | Regions, scenes, avatars and scripts |
| World API | Authenticated management and integration API |
| NexBus | Cross-service event communication |
| NV$ | Virtual currency and accounting |
| OGL addons | Optional, separately configured extensions |

The current build targets **.NET 8**. Firestorm and OpenSimulator compatibility are important goals, with feature-specific verification required.

### Building and contributing

Use a compatible .NET 8 SDK and the repository's project-generation workflow:

```bash
./runprebuild.sh
dotnet build OpenSim.sln -c Release
```

A successful build is not equivalent to runtime, security or migration acceptance. Back up production configuration, databases and asset storage before upgrading. Do not overwrite local operator files indiscriminately.

Contributions and bug reports are welcome through [Issues](https://github.com/opengenesislink/opengenesislinkserver/issues) and [Pull Requests](https://github.com/opengenesislink/opengenesislinkserver/pulls).

### Provenance and licensing

The historical source baseline is **OpenSimulator 0.9.3.0 (“Nessie”)**, not the present OpenGenesisLINK feature set. Original copyright and license notices are retained. New original OpenGenesisLINK contributions are generally licensed under **MPL-2.0** unless stated otherwise. Imported OpenSimulator code and third-party dependencies retain their applicable licenses. See `LICENSE_POLICY.md`, `NOTICE.md` and `LICENSES/`.

---

**OpenGenesisLINK Generation 1** · Independent virtual-world server development
