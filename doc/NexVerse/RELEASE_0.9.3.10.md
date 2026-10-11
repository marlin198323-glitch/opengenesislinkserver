# OpenGenesisLINK 0.9.3.10 – Stable

**Release-Typ:** Stabilisierungsrelease für OpenGenesisLINK/NexVerse.  
**Freigabegrundlage:** GitHub-CI plus direkte Betreiberentscheidung vom 12.10.2026.
**Transparente Ausnahme:** Die vorher vorgesehenen ausführlichen
`ogl-release-evidence-v2`-Einzelnachweise und eine 24-stündige
Stable-Soak-/Rollback-Abnahme wurden nicht vollständig vorgelegt.
Details: [09310_OPERATOR_RELEASE_DECISION.md](09310_OPERATOR_RELEASE_DECISION.md).

## Änderungen

- **.NET 8 / Simulator-Runtime:** Bereinigung geerbter
  ThreadPool-Einstellungen, verlässlichere HTTP-Port-Zuweisung für
  mehrere Simulatorinstanzen, Start-/Login-/HG-/LSL-Regressionsprüfungen.
- **NV$-Economy:** zentraler Double-Entry-Ledger, Viewer-Balance,
  Transaktionen und dedizierter `GRID SERVICES`-Masterbank-Payoutpfad
  mit expliziter Operator-/Skript- und Debit-Autorisierung. Die
  Masterbank ist standardmäßig **deaktiviert**.
- **Inventar & Assets:** persistente Item-/Ordner-Thumbnails und
  gesicherte World-API-Integrationen; bestehende Inventardaten- und
  Systemordner-Kompatibilität.
- **World API / R287:** Auth-/Scope-geschützte Profilbilder, Gruppen,
  Discovery und Regionendetails; OpenAPI-Verträge und Regressionen.
- **Betrieb und Sicherheit:** NexBus, Health/Monitoring, Versionierung,
  Release-Vorlagen und gesicherte Standardwerte für unfertige Module.

## Nicht enthalten

- **NexExperiences/Firestorm:** bleibt pausiert und im
  ausgelieferten Simulatorprofil `Enabled=false`. Der bestehende
  experimentelle Code bleibt im Repository, ist **nicht produktionsreif**.
- **OGL Pathfinding 2.0, OGL My Outfits 2.0** und eine mögliche
  **OGL Experiences 2.0** werden erst für den eigenen Viewer neu geplant.
- OGLVoice/WebRTC und eigener Renderer werden separat entwickelt.

## Bekannte Einschränkungen und Risiken

- Firestorm kann ein ersetztes Outfit-Ordner-Thumbnail aus vorhandenem
  Inventar-Cache erst nach Cache-Neuaufbau aktualisiert anzeigen.
  Dies ist **kein** behobener Viewer-Cache-Fehler.
- My-Outfits-2.0-Galerie, Experience-UI und Pathfinding wurden bewusst
  aus der Abnahme entfernt und sind **keine** Release-Funktionen.
- Nicht sämtliche behaupteten Betriebsprüfungen sind mit prüfbaren
  Einzelprotokollen hinterlegt; insbesondere ist eine durchgehende
  24-h-Soak-Abnahme nicht verifiziert. Fehler werden im Betrieb
  aufgenommen und separat korrigiert.
- Es wird kein garantierter CPU-/RAM-Gewinn behauptet, solange
  vergleichbare Lastmessungen gegen v0.9.3.8 fehlen.
- World API und Simulatoren müssen zusammen auf kompatiblen
  Programmstand gebracht werden; der GitHub-Tag installiert nichts.

## Betreiber-Upgrade

1. `v0.9.3.8` und Datenbank-/Region-/Asset-/Inventarbackups behalten.
2. Lokale `OpenSim.ini`, Includes und Umgebungsvariablen vor Pull,
   Build und Neustart sichern; **nicht** blind `git reset --hard`.
3. World API/Robust und Simulatoren konsistent aktualisieren,
   `git rev-parse HEAD` und tatsächliche laufende DLLs vergleichen.
4. `NexExperiencesViewer.Enabled=false` effektiv prüfen; Masterbank
   nur separat und sicher autorisiert aktivieren.
5. Bei Problemen Logs, Zeitpunkt, Region/Avatar-Kontext, Build-SHA
   und Reproduktion erfassen; Rollback nur mit Datenbank-Schema-/
   Migrationsbewertung durchführen.

**Vorheriger Stable-Stand:** `v0.9.3.8`.  
**Nächste Entwicklungslinie:** `0.9.4.0` (noch kein Release).
