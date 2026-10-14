# OpenGenesisLINK 0.9.3.12 – RC1-Vorbereitung und Scope-Freeze

**Stand 14.10.2026 – PREPARATION ONLY; keine RC1- oder Stable-Freigabe.**
Basis-Commit bei Beginn: `fae3b6876b4e54ab1878b2eae0b7a67ea8376e78` (PR #202).
Die letzte veröffentlichte Stable `v0.9.3.10` bleibt unverändert und
als Ausgangspunkt für Wiederherstellungsverfahren erhalten.

## Entwicklungsumfang einfrieren

Für 0.9.3.12 sind vorgesehen: kompatible Robust-/Simulator-Services,
Firestorm-Legacy-Login, getestete LSL-Kernfunktionalität, stationäre
Touch-Hold-Ereignisse, regionsübergreifende Chat-Nachrichten, IAR-/OAR-
Import- und Exportpfade, deutsche IAR-Fortschrittsanzeige, notwendige
Inventar-/Security-Fixes und bestehende NV$-Ledger-Funktionen.

**Nicht in den Stable-Scope:** OGLVoice, natives OGL Viewer-Login ohne
eigene E2E-Abnahme, Experiences 2.0, Pathfinding 2.0, My Outfits 2.0,
dynamisches Wasser / HydroEngine / neue Renderer-Engine. Die vorhandenen
Opt-in-Schnittstellen bleiben im produktiven Profil ausgeschaltet,
solange ihre eigene Live-Abnahme aussteht.

Vor RC1 sollten keine neuen Großfeatures mehr aufgenommen werden.
Nach erfolgreicher unabhängiger Operator-Evidence die RC1-Version
erst auf einem frischen Candidate-Branch auf `0.9.3.12 RC1` ändern,
mit Build, Regression und Inworld-Prüfungen auf **demselben** SHA.

## Was technisch bereits nachgewiesen ist

Automatisierte Code-Prüfungen auf dem Entwicklungsstand:
- `tools/ci/verify_portal_registration_iar_preflight.py`: bestanden;
  Registrierungsroute ist standardmäßig ausgeschaltet.
- `tools/ci/verify_economy_policy.py` und
  `tools/ci/NexEconomyPolicyRegression`: bestanden.
- `tools/ci/OglIarPreflightRegression`: 11 gültige und manipulierte
  TAR/GZip-Fixtures bestanden.
- `tools/ci/verify_native_backup_worker.py`,
  `tools/ci/verify_native_restore_worker.py`: bestanden.
- Letzter main-Release-Build sowie IAR-Console-Tests: CI erfolgreich.

Das sind **keine** Belege für einen vollständigen Produktionslauf.

## Betreiber-Rückmeldungen (noch nicht als formales PASS gewertet)

Am 14. Oktober 2026 bestätigte der Betreiber im Projektchat funktional:
BOOT01, VIEW01, LSL02/03, bidirektionalen CHAT01/02, IAR-
Import/Export, Inventarintegrität und einen Backup-/Restore-Versuch.
Ein protokollierter LSL-Smoke-Test ergab 16/16, inklusive 154
stationären Touch-Events innerhalb 3,265 Sekunden. Chat zeigte
Nachrichten auf Kanal 0 und 7 in beiden Richtungen. Beim großen IAR-
Export wurden 474 Inventareinträge und 2.832 angeforderte Assets
gemeldet, **14 Asset-Referenzen waren fehlend/beschädigt/ungültig**.
Der verbesserte interaktive IAR-Status wurde danach ebenfalls
vom Betreiber positiv bestätigt.

Diese Aussagen sind funktional ermutigend, ersetzen jedoch nicht
vollständige Build-SHAs, anonyme Logs, isolierte Negativtests, Vergleichs-
Lastprofile und gesonderte Restore-Nachweise. Die 14 Assets müssen
identifiziert und vor einem vollständigen Backup-Integritäts-PASS
geklärt bzw. als explizite akzeptierte Einschränkung dokumentiert sein.

## Aktuelle verbleibende Releaseblocker

- **PORTAL01:** End-to-End-Anmeldung Benjamin/Luna, E-Mail-Verifikation,
  Duplicate/Race, Testaccount-Login, Fehlerkompensation und Bereinigung
  verwaister Auth-/Inventory-/Appearance-Daten im isolierten Test.
  Siehe `PORTAL_REGISTRATION_IAR_HARDENING.md`. API ist opt-in.
- **PORTAL02:** Überprüfen, dass deaktivierte Funktionen im realen
  Produktionsprofil tatsächlich keine ungeschützte Registrierung
  erlauben und keinerlei Schlüssel im Browser erscheinen.
- **ECON01:** Im isolierten MariaDB-/Ledger-Test Masterbank, echte
  Transaktionen, Doppelausführung, Überziehung und Negativrechte mit
  Ergebnis-Salden und Rollback verifizieren.
- **IAR02/03:** Zusätzlich zu den 11 Fixtures alle direkten Stream-/
  Code-Einstiegspunkte, konkurrierende Importe, Quotas, TOCTOU-
  Dateiaustausch und danach unveränderte DB/FSAssets-Datensätze
  nachweisen. **Die statische und Fixture-Prüfung schließt diese Lücke nicht.**
- **INV01/02:** Item-/Ordner-Thumbnails inklusive Reconnect,
  Account-Erstausstattung und Rechte auf SQLite/MariaDB belegen.
- **PERF01/PROF01:** Reproduzierbare 0.9.3.10-Baseline und
  0.9.3.12-Vergleichslast mit CPU, RAM/GC, Region-FPS, YEngine und
  Login/Teleport-Latenzen protokollieren.
- **REC01:** SQL + FSAssets + OAR/IAR + Konfiguration auf eine
  vollständig isolierte Restore-Umgebung zurückspielen, Avatar-
  Inventar/Objekte/Skriptzustände und referenzierte Assets prüfen.
- **RC01/02:** Candidate-SHA, grüner Build/CI auf identischem SHA und
  schriftliche, datierte Betreiber-/Maintainer-Freigabe.
- **STABLE01/02:** Erst danach 24-h-Soak, zusätzlicher Rollback-Test,
  RC1-Vorfahren-Tag, separate Stable-Freigabe.

## Verbindlicher RC1-Evidence-Workflow

Die neuen Tools erzeugen selbst **keine** Tests als PASS:

```bash
# Auf dem auszugebenden, identischen Candidate-Commit ausführen
python3 tools/release/ogl_09312_gate.py init \
    --manifest /srv/ogl-release-evidence/09312/manifest.json

python3 tools/release/ogl_09312_gate.py inventory \
    --manifest /srv/ogl-release-evidence/09312/manifest.json

# Nach Durchführung und Dokumentation aller Fälle:
python3 tools/release/ogl_09312_gate.py rc1 \
    --manifest /srv/ogl-release-evidence/09312/manifest.json \
    --evidence-dir /srv/ogl-release-evidence/09312/logs
```

Die Manifestdatei enthält **exakt 22** Fälle. Jedem bestätigten PASS
müssen lokale Evidence-Datei, passender SHA-256-Fingerabdruck, Datum
und **exakt dieselbe getestete Git-SHA** zugeordnet sein; `NOT_RUN` und
`FAIL` blockieren die Freigabe. Die CI-Run-URL, ausführende Person,
Limitations-Review sowie Robust + zwei Simulatoren auf demselben SHA
sind für RC1 ebenfalls Pflicht.

**Das Evidence-Verzeichnis liegt außerhalb des Git-Repositories.**
Vor Speicherung Logs anonymisieren (Avatar-IDs, E-Mail, IP,
Authentifizierungs- und API-Schlüssel, Geldkonten). Keine Secret-Dateien
committen. Die Gate-Software prüft Vollständigkeit und SHA-Bezüge; sie
kann menschliche Logfälschung **nicht** kryptografisch ausschließen.

Vor dem tatsächlichen RC1-Tag ist der Release-Commit mit
`AssemblyVersionNumber = "0.9.3.12"`,
`VersionNumber = "0.9.3.12"` und
`VERSION_FLAVOUR = Flavour.RC1` in `VersionInfo.cs` erforderlich.
Erst danach werden CI- und Operator-Belege auf diesem SHA gesammelt.
Kein automatischer Tag/Deployment durch den Evidence-Checker.

## Backup- und Rollback-Protokoll (REC01/STABLE01)

**Nur im freigegebenen Wartungsfenster und mit verfügbaren Backups:**

1. Exakte Build-SHAs und Runtime-Konfigurationen aller Robust- und
   Simulatorknoten erfassen; Secrets nicht ins Prüfprotokoll schreiben.
2. Konsistentes Snapshot-Set der SQL-Datenbanken erstellen (mit
   transaktionskonsistenter Datenbanksicherung), alle FSAssets-
   Verzeichnisse und Konfigurations-/Regionsdateien archivieren.
   Bei aktiven Writes die Konsistenz/Koordination beachten.
3. Für jedes Backup SHA-256, Datum, Umfang, Datenträger und unabhängige
   Aufbewahrung erfassen. Version `v0.9.3.10` als unveränderlichen
   Rollback-Quellstand vorhalten.
4. Ein **isoliertes** Test-Grid mit **anderen Ports, Hostnamen,
   Datenbanken und Asset-Pfaden** starten. Niemals ein Restore
   testweise in die produktive DB oder FSAssets-Verzeichnisse fahren.
5. SQL und FSAssets gemeinsam wiederherstellen; OAR/IAR-Testdatensätze
   lesen, Avatar, Inventar, Attachments, Region-Objekte, Skripte und
   Login/Teleport prüfen. Fehler/fehlende Asset-UUIDs dokumentieren.
6. Separater Rückweg: Kandidat beenden, isolierten Snapshot-
   Vorzustand und Baseline-Binaries zurückspielen; Prüfsummen/
   Dienste/Skriptdaten nach dem Rollback vergleichen.
7. Bei Datenmigrationen nicht davon ausgehen, dass alte Binaries
   ein neueres Schema lesen können; **zusammengehörende Daten-
   und Code-Snapshots** zurückspielen. Erst nach erfolgreicher
   Probe ein Produktions-Rollback-Runbook unterzeichnen.

**Kein Rollback-Test, keine Datensicherung oder Versionumschaltung
wurde durch diese Vorbereitung auf dem Produktionsgrid ausgeführt.**
