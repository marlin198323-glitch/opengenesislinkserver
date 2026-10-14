# OpenGenesisLINK 0.9.3.12 – Stable-Build

**14. Oktober 2026.** Diese Fassung setzt die vom Betreiber ausdrücklich gewünschte **Stable-Versionskennung** im OpenGenesisLINK-Server auf `0.9.3.12` (`Flavour.Release`). Eine gesonderte GitHub-Release-Veröffentlichung und ein unveränderlicher `v0.9.3.12`-Tag sind **nicht** durch die Versionsumstellung allein entstanden.

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

**Die Kennzeichnung als Release ist nicht gleichbedeutend mit vollständiger Betreiber-Evidence oder einem veröffentlichten Stable-Tag.** Die 22-Fälle-Matrix unter `09312_RC1_STABLE_ACCEPTANCE.md` und das fail-closed-Gate unter `tools/release/ogl_09312_gate.py` bleiben erhalten. Insbesondere sind noch explizite Dokumentation bzw. isolierte Live-Nachweise für PORTAL01/02, IAR03 (konkurrierende Importe und TOCTOU), vollständige Backups/Restore, vergleichbare Performance-Messungen und eine 24-Stunden-Soak-Probe erforderlich.

Geplante oder eigenständig nicht freigegebene neue Funktionen (z. B. OGLVoice, Viewer Native Login, Experiences 2.0, Pathfinding 2.0 und HydroEngine/Renderer) sind nicht automatisch Teil des Stable-Supportumfangs. Optionale Dienste bleiben ohne gesonderte Freigabe deaktiviert.

## Upgrade / Rückfall

Ein Merge nach `main` aktualisiert nur GitHub. Auf dem Live-Server muss der Betreiber den aktuellen SHA selbst bauen und die laufende Anwendung kontrolliert ersetzen. SQL-Datenbanken, FSAssets, Regionen und Runtime-Konfiguration konsistent sichern. `v0.9.3.10` zusammen mit einem **passenden Daten-Snapshot** als Rollback-Stand bereithalten. Siehe `09312_LIVE_TEST_DEPLOYMENT.md` und `09312_RC1_CANDIDATE_PLAN.md`.

**Kein automatischer Server-Restart, kein Deployment, kein Stable-Tag und keine vorgetäuschte RC1-Evidence werden durch diesen Versionswechsel ausgelöst.**
