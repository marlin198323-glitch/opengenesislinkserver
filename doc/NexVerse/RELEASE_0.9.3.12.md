# OpenGenesisLINK 0.9.3.12 – Stable-Build

**14. Oktober 2026.** Diese Fassung setzt die vom Betreiber ausdrücklich gewünschte **Stable-Versionskennung** im OpenGenesisLINK-Server auf `0.9.3.12` (`Flavour.Release`). Der separat eingerichtete GitHub-Publisher erstellt den unveränderlichen Release-Tag `v0.9.3.12` nur nach erfolgreicher CI auf dem aktuellen `main`-Commit und bestandener Sicherheitsprüfung.

## Umfang

- Robust, Simulatoren, Firestorm-Legacy-Kompatibilität sowie LSL-Funktionsabnahme.
- Regionsübergreifender Skript-Chat mit Kanälen 0 und /7 und Reichweitenprüfung.
- IAR-Import und -Export mit deutschsprachiger Fortschrittsanzeige, lokalem Live-Fortschrittsbalken und `show iar progress`.
- IAR-Vorabprüfung, Archivierungs- und Inventarfunktionen, vorhandenes NV$-Ledger und World-API-Integrationen.
- API- und Serverversion melden `OpenGenesisLINK 0.9.3.12` (nicht `Dev`).

## Validierung / Nachweisstand

- Bei früheren Tests im Projekt bestätigte der Betreiber Firestorm-Login, Inventar, Teleport, 16/16 LSL-Testfälle, Child-Prim/Touch-Hold, bidirektionalen Regionschat, IAR-Import/Export und die deutschsprachige Fortschrittsanzeige.
- Die CI verifiziert automatisiert Build und diverse Runtime-/Security-Tests einschließlich der NV$-Datenbankmatrix. Die Stable-Kennung wird über das Release-Branch-CI geprüft.
- Die frühere IAR-Logausgabe berichtete **14 fehlende, beschädigte oder ungültige Asset-Referenzen**. Diese sind inhaltlich nicht durch die erfolgreiche Export-Fertigstellung behoben.

## Release-Limitierungen / offener Freigabestatus

**Die Kennzeichnung als Release und ein GitHub-Tag sind kein Nachweis einer vollständigen produktiven 24-Stunden-Abnahme.** Die 22-Fälle-Matrix unter `09312_RC1_STABLE_ACCEPTANCE.md` und das fail-closed-Gate unter `tools/release/ogl_09312_gate.py` bleiben erhalten. Insbesondere sind noch explizite Dokumentation bzw. isolierte Live-Nachweise für PORTAL01/02, IAR03 (konkurrierende Importe und TOCTOU), vollständige Backups/Restore, vergleichbare Performance-Messungen und eine 24-Stunden-Soak-Probe erforderlich.

Geplante oder eigenständig nicht freigegebene neue Funktionen (z. B. OGLVoice, Viewer Native Login, Experiences 2.0, Pathfinding 2.0 und HydroEngine/Renderer) sind nicht automatisch Teil des Stable-Supportumfangs. Optionale Dienste bleiben ohne gesonderte Freigabe deaktiviert.

## Upgrade / Rückfall

Ein Merge nach `main` aktualisiert nur GitHub. Auf dem Live-Server muss der Betreiber den aktuellen SHA selbst bauen und die laufende Anwendung kontrolliert ersetzen. SQL-Datenbanken, FSAssets, Regionen und Runtime-Konfiguration konsistent sichern. `v0.9.3.10` zusammen mit einem **passenden Daten-Snapshot** als Rollback-Stand bereithalten. Siehe `09312_LIVE_TEST_DEPLOYMENT.md` und `09312_RC1_CANDIDATE_PLAN.md`.

**Kein automatischer Server-Restart, kein Deployment und keine vorgetäuschte RC1-Evidence werden durch die GitHub-Veröffentlichung ausgelöst.**

## Sicherheitsupdate: Hypergrid-Inventar

Der bisherige öffentliche `HGInventoryService` stellte über
`POST /xinventory` ohne avatarbezogene Authentifizierung auch
schreibende Methoden bereit. Ab diesem Tag ist der öffentliche
Cross-Grid-Connector **read-only**: POST-Schreiboperationen wie
`ADDITEM`, `UPDATEITEM`, `MOVEITEMS`, `ADDFOLDER` und
`UPDATEFOLDER` werden vor der Verarbeitung mit HTTP 403 abgewiesen.
Private lokale Inventaroperationen bleiben möglich.

**Einschränkung:** Das HG-Suitcase lässt sich von fremden Grids aus
vorübergehend nur lesen und nicht bearbeiten. Auch für den anonymen
HG-Lesezugriff fehlt weiterhin eine avatarbezogene Autorisierung.
Keine vertraulichen Assets in einem öffentlich abrufbaren Suitcase ablegen.
Siehe [Sicherheitsanleitung](../OpenGenesisLINK/09312_HG_INVENTORY_SECURITY.md).

## Vor dem Betreiber-Upgrade: Feature Control

`[OGLFeatureControl] Enabled = true` ist in aktiven OGL-Vorlagen
aktiv. ROBUST muss **vor dem Neustart** den privaten ES256-Schlüssel
besitzen, Simulatoren den öffentlichen Schlüssel, passende NodeIDs,
NexBus und gültige signierte OAR-/IAR-Freigaben. Ohne diese
Voraussetzungen kann ROBUST die World API nicht starten bzw. ein
Simulator Archivimporte/-exporte blockieren.

## GitHub Release und Pakete

Der GitHub-Publisher erstellt nach erfolgreich abgeschlossener CI auf
`main` einen unveränderlichen Tag `v0.9.3.12` mit diesen Release Notes.
Als Download stehen die automatischen GitHub-Quellcodearchive (.zip
und .tar.gz) bereit; für einen produktiven Einsatz müssen die
Server-DLLs mit .NET 8 gebaut werden. Es handelt sich **nicht**
um bereits kompilierte AMD64-/ARM64-Binärdistributionen.
