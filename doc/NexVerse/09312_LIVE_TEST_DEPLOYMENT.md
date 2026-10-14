# OpenGenesisLINK 0.9.3.12 Stable – kontrolliertes Upgrade

**Stand: 14.10.2026.** Die Server-Versionskennung wurde auf **0.9.3.12 Release** umgestellt. Dies ersetzt weder die technische Prüfung des echten Produktivdeployments noch die dokumentierten RC1-/Stable-Abnahmen. Ein formeller GitHub-Stable-Tag ist separat auszustellen.

## Versionskennung

- `OpenSim/Framework/VersionInfo.cs`: `VersionNumber = "0.9.3.12"`,
  `AssemblyVersionNumber = "0.9.3.12"`, `VERSION_FLAVOUR = Flavour.Release`.
- `NexVerse/Core/NexVersePlatform.cs`: API-Milestone `0.9.3.12`.
- CI testet die sichtbare World-API-Version `OpenGenesisLINK 0.9.3.12`.
- Die Kennung ist auf Betreiberwunsch auf Release gesetzt; die in der
  RC1-Freigabematrix geforderten Nachweise bleiben bis zur tatsächlichen
  Dokumentation separat offen.

## Vor dem Umschalten – zwingender Live-Preflight

Die produktive Serverkonfiguration und die Installation werden **nicht**
vom GitHub-Repository bestimmt. Alle Befehle/Dateipfade müssen auf
`s38541.isp.slmm.de` **gelesen und verifiziert** werden, bevor Daten oder
Binaries ausgetauscht werden.

1. Authentifizierten Fernzugang herstellen und **Hostname, Root-/Service-
   Berechtigung, laufende Systemd-Units, Robust-/Simulator-Prozessliste,
   Ports und Programmverzeichnisse** auslesen. Nicht anhand früherer
   Chat-Beispiele raten.
2. Aktive Konfiguration der einzelnen Robust-/Simulator-Instanzen, DB-
   Anbindung und FSAssets-Verzeichnisse identifizieren; API-Schlüssel,
   Passwörter oder Token nicht ausgeben oder ins Git-Repository schreiben.
3. Einen konsistenten Backup-Satz für **alle SQL-Datenbanken, FSAssets,
   Regionen, OAR/IAR, Konfigurations-/Secret-Dateien und bisherige Binaries**
   erstellen. Vorhersagbare Wiederherstellungsprozedur inkl. Prüfsummen
   erfassen. Produktive Daten nicht löschen oder überschreiben.
4. Die letzten stabilen `v0.9.3.10`-Binaries und den **zugehörigen
   Daten-Snapshot** als Rollback-Basis gesondert vorhalten.
5. Auf dem Entwickler-Rechner aus einem **sauberen Release-Checkout**
   alle Komponenten mit exakt derselben Git-SHA bauen und testen:
   `./runprebuild.sh`, `dotnet build OpenSim.sln -c Release`,
   die integrierte Github-CI sowie Archiv-/Ökonomie-Tests.
6. Binaries in ein **neues** Staging-Verzeichnis auf den Live-Host
   übertragen, niemals unkontrolliert in die laufende `bin/`-Instanz.
   Aktive INI-, Secret-, DB- und FSAssets-Dateien nicht durch
   Repository-Templates ersetzen.
7. Nur wenn Backup, Rollback, Staging-Build und Version-SHA nachgewiesen
   sind: Wartungsfenster; Dienste geordnet stoppen, Binaries wechseln,
   Robust starten, dann alle Simulatorprozesse. Individuelle Ports/Node-IDs,
   Login, CAPS, NexBus, HG und Feature-Gates kontrollieren.
8. Firestorm-Login, Inventory, Teleport, Inworld-LSL, Regionschat, IAR-
   Export/Import, NV$ und die World-API-Version prüfen. Im Fehlerfall
   geordnet stoppen und auf **zusammenpassende Binaries/Daten** rollen.

## Wichtige offene Grenzen

- Ein Remote-Desktop-Commander-Agent mit der Kennung `s38541.isp.slmm.de`
  war am 14.10.2026 offline. Solange keine authentifizierte Shell existiert,
  kann **keine Live-Installation verlässlich bestätigt oder durchgeführt**
  werden. Ein offener SSH-Port bedeutet keine gültige Anmeldung.
- IAR03 hat noch nicht alle Concurrent-/TOCTOU-Fälle abgedeckt;
  Portal-Signup ist opt-in/standardmäßig aus. Nicht während des
  Testdeployments ungeprüft aktivieren.
- Die 14 ungültigen/fehlenden Asset-Referenzen eines früheren IAR-Exports
  erfordern separate Inventar-/Backup-Klärung.
- Das 22-Fälle-RC1-Evidence-Gate bleibt aktiv; Betreibertests müssen dem
  wirklich deployten SHA zugeordnet werden. Ein bloßer Build hat **kein**
  RC1- oder Stable-Recht.

## Status

Versionskennzeichnung/CI-Umstellung: Branch
`release/09312-stable-version`. Ein Merge nach `main` stellt die
Stable-Versionskennung im Quellcode bereit, führt aber **keinen automatischen
Produktiv-Rollout oder die Veröffentlichung eines Release-Tags** durch.
Die Betreiber-Evidence und die noch offenen Sicherheitsnachweise sind
im Freigabedokument weiterhin explizit erfasst.
