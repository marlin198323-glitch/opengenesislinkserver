# OpenGenesisLINK 0.9.3.12 – Stable-Build

**14. Oktober 2026.** OpenGenesisLINK 0.9.3.12 ist vom Betreiber ausdrücklich als GitHub-Release angefordert; der Quellstand verwendet `Flavour.Release`. Die GitHub-Publikation ist eine ausdrückliche Betreiberentscheidung **vor Abschluss der vollständigen 22-Fälle-Stable-Abnahme**. Die fehlende Live-Evidence bleibt offen und wird nicht als bestanden dargestellt. Eine Veröffentlichung ist nur nach erfolgreicher GitHub-CI auf dem exakt veröffentlichten SHA vorgesehen.

## Umfang

- Robust, Simulatoren, Firestorm-Legacy-Kompatibilität sowie LSL-Funktionsabnahme.
- Regionsübergreifender Skript-Chat mit Kanälen 0 und /7 und Reichweitenprüfung.
- IAR-Import und -Export mit deutschsprachiger Fortschrittsanzeige, lokalem Live-Fortschrittsbalken und `show iar progress`.
- IAR-Vorabprüfung, Archivierungs- und Inventarfunktionen, vorhandenes NV$-Ledger und World-API-Integrationen.
- API- und Serverversion melden `OpenGenesisLINK 0.9.3.12` (nicht `Dev`).
- **Kritische HG-Inventar-Sicherheitskorrektur:** Unauthentifizierte öffentliche HG-Inventaranfragen werden fail-closed gesperrt; die öffentliche HG-Inventory-Route ist im ROBUST-HG-Profil deaktiviert (404) und bei älteren aktiven Konfigurationen zusätzlich direkt im C#-Handler gesperrt (403). Der private lokale Inventardienst bleibt erhalten.

## Validierung / Nachweisstand

- Bei früheren Tests im Projekt bestätigte der Betreiber Firestorm-Login, Inventar, Teleport, 16/16 LSL-Testfälle, Child-Prim/Touch-Hold, bidirektionalen Regionschat, IAR-Import/Export und die deutschsprachige Fortschrittsanzeige.
- Die CI verifiziert automatisiert Build und diverse Runtime-/Security-Tests einschließlich der NV$-Datenbankmatrix. Die Stable-Kennung wird über das Release-Branch-CI geprüft.
- Die frühere IAR-Logausgabe berichtete **14 fehlende, beschädigte oder ungültige Asset-Referenzen**. Diese sind inhaltlich nicht durch die erfolgreiche Export-Fertigstellung behoben.

## Release-Limitierungen / offener Freigabestatus

**Die Kennzeichnung als Release und der GitHub-Tag sind kein Nachweis einer abgeschlossenen Betreiber-Evidence.** Die 22-Fälle-Matrix unter `09312_RC1_STABLE_ACCEPTANCE.md` und das fail-closed-Gate unter `tools/release/ogl_09312_gate.py` bleiben erhalten. Insbesondere sind noch explizite Dokumentation bzw. isolierte Live-Nachweise für PORTAL01/02, IAR03 (konkurrierende Importe und TOCTOU), vollständige Backups/Restore, vergleichbare Performance-Messungen und eine 24-Stunden-Soak-Probe erforderlich. Die Betreiberanforderung, den GitHub-Release bereits jetzt zu publizieren, ist **keine** fiktive PASS-Evidence.

Geplante oder eigenständig nicht freigegebene neue Funktionen (z. B. OGLVoice, Viewer Native Login, Experiences 2.0, Pathfinding 2.0 und HydroEngine/Renderer) sind nicht automatisch Teil des Stable-Supportumfangs. Optionale Dienste bleiben ohne gesonderte Freigabe deaktiviert.

## Wichtig: Hypergrid-Inventar vorübergehend eingeschränkt

Durch die notwendige Notfallsperre ist der **Inventarzugriff von fremden Hypergrid-Regionen auf das lokale Heimat-Inventar vorübergehend nicht verfügbar**, einschließlich Suitcase-Inventarfunktionen. Avatar-Reisen und andere HG-Dienste müssen nach dem Rollout gesondert auf Kompatibilität getestet werden. Die Sperre darf nicht durch Freischalten des internen Inventar-HTTP-Handlers umgangen werden. Wiederherstellung erfordert eine spätere HG-Protokollerweiterung mit verifizierter, avatarspezifischer Autorisierung. Siehe `doc/OpenGenesisLINK/09312_HG_INVENTORY_SECURITY.md`.

## Feature Control: Voraussetzung für ROBUST und Simulatoren

Die ausgelieferten Konfigurationen aktivieren `[OGLFeatureControl] Enabled = true`. Vor dem Start muss ROBUST den privaten ES256-P-256-Schlüssel besitzen und jeder Simulator den passenden öffentlichen Schlüssel, eine eindeutige NodeID und funktionierendes NexBus. Sonst startet die ROBUST-World-API nicht bzw. IAR-/OAR-Operationen werden fail-closed gesperrt. **Vor der Aktualisierung** Backup und Schlüsselkonfiguration prüfen; keine privaten Schlüssel in GitHub hochladen.

## Upgrade / Rückfall

Ein Merge nach `main` aktualisiert nur GitHub. Auf dem Live-Server muss der Betreiber den aktuellen SHA selbst bauen und die laufende Anwendung kontrolliert ersetzen. SQL-Datenbanken, FSAssets, Regionen und Runtime-Konfiguration konsistent sichern. `v0.9.3.10` zusammen mit einem **passenden Daten-Snapshot** als Rollback-Stand bereithalten. Siehe `09312_LIVE_TEST_DEPLOYMENT.md` und `09312_RC1_CANDIDATE_PLAN.md`.

**Der GitHub-Release aktualisiert keine laufenden Server und erfindet keine RC1-/Stable-Evidence.** Bestehende lokal betriebene Konfigurationen, Schlüssel und Datenspeicher bleiben von GitHub unverändert. Sicherheitsfixes werden erst nach Deployment der aktualisierten Binary wirksam.
