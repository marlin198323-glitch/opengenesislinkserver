# NexExperiences – SingleWriterJson Betrieb (OpenGenesisLINK 0.9.3.10 Dev)

**Stand 11.10.2026.** Der bisherige JSON-Store bleibt die autoritative
Experience-Verwaltung, aber ist **kein transaktionales Multi-Writer-/SQL-Backend**.
Dieser Härtungsschritt verhindert konkurrierende Writer auf **demselben
lokalen Dateisystem** und sichert Snapshot-Commits ab. Firestorm-Consent und
Multi-Simulator-E2E-Tests bleiben vor RC1 offen.

## Architektur und Grenzen

* Ein Robust-Prozess hält die Datei `<StorePath>.writer.lock` während der
  gesamten Store-Lebenszeit über einen exklusiven offenen `FileStream`
  (`FileShare.None`). Bereits beim Start eines zweiten Writers mit
  **demselben lokalen Pfad** wird ein Fehler ausgelöst und die betreffende
  World-API-Initialisierung verweigert. Das `.writer.lock`-Dateiobjekt
  **bleibt auch nach dem Prozessende bestehen**; entscheidend ist der
  offene Handle, nicht eine gelöschte/verwaiste Dateimarke.
* Alle öffentlichen Store-Operationen prüfen, dass der Store nicht
  disposed oder nach fehlgeschlagener Speicherung inkonsistent ist.
* Änderungen werden zunächst in `<StorePath>.tmp` geschrieben,
  einschließlich Flush auf das Dateisystem, dann auf demselben Volume
  per `File.Move(temp, primary, overwrite: true)` ersetzt. Die letzte
  gültige Primärdatei bleibt bei einem Abbruch *vor dem Rename* erhalten.
  Ein alter `.tmp` ersetzt sie beim Neustart **niemals automatisch**.
* Bei fehlgeschlagenem Commit wird der Store bis zum Prozessneustart
  **fail-closed**. Dies ist ausdrücklich keine automatische Transaktion:
  nach Disk-/I/O-Fehlern gilt die Anfrage als fehlgeschlagen, Robust
  muss gestoppt und der letzte bestätigte Snapshot geprüft werden.
* Fehlende Primärdatei **mit** vorhandenem `.tmp`, invalide JSON-Daten
  und eine JSON-`null`-Root verhindern die Inbetriebnahme statt einer
  stillen leeren Experience-Verwaltung. Bei einer noch nie verwendeten
  Installation ohne Primärdatei oder `.tmp` ist ein leerer Start zulässig.
* Der Lock ist **kein** verteiltes Fencing, hat keine Leader-Election,
  garantiert keine atomaren NFS-/SMB-Locks und kann keine Netzwerkpartition
  erkennen. Auch Flush/Rename ersetzt weder Datenbanktransaktionen noch
  getestete Stromausfallsicherheit einschließlich Directory-Fsync.

## Konfiguration

In Robust.ini bzw. Robust.HG.ini:

```ini
[NexExperiences]
    StoreBackend = "SingleWriterJson"
    StorePath = "data/nexverse-experiences.json"
```

`StoreBackend` ist absichtlich restriktiv. Nicht unterstützte Werte wie
`Sql`, `Distributed` oder `ActiveActive` müssen einen **Startfehler**
auslösen – keine vorgetäuschte SQL-Implementierung. Bereits laufende
Installationen ohne `StoreBackend` verwenden automatisch SingleWriterJson.
Simulatoren verwenden weiterhin den authentifizierten zentralen World-API
Dienst, nicht direkte JSON-/SQL-Dateizugriffe.

Für zwei oder mehr Simulatoren darf **ein** zentraler Robust die
Experience-Autorität anbieten. Für mehrere Robust-Prozesse dieselbe JSON
Datei *nicht* per NFS/SMB freigeben. Ein Load-Balancer mit mehreren aktiven
unabhängigen JSON-Stores ist **nicht zulässig**, weil Berechtigungen,
Scriptbindungen und K/V auseinanderlaufen würden. Für echte aktive
Redundanz ist ein gesonderter SQL-Transaktionsstore mit CAS-/Migration-/
Failover-/Fencing-Tests nötig; diese Aufgabe ist weiterhin offen.

## Sichere Wartung und Wiederherstellung

1. Vor Änderungen StorePath und zugehörige Konfiguration identifizieren,
   alle Secrets aus Logs heraushalten. Nur einen Robust-Writer betreiben.
2. Für **konsistente Backups** Schreiboperationen anhalten, Robust sauber
   stoppen, die Primärdatei samt Version/Prüfsumme sichern. Die Lock-Datei
   darf bestehen bleiben; niemals zum Erzwingen einer zweiten Instanz
   löschen. Ein `.tmp` ist *nicht* automatisch der gültige Snapshot.
3. Backup testweise auf einem isolierten Robust-Prozess mit **eigenem**
   lokalem Pfad laden. Erfahrungen, K/V, Scriptbindungen, Allow/Block/Forget
   und Rollback mit zwei Avataren überprüfen.
4. Bei I/O-Fehlern World API nicht weiter beschreiben; Robust stoppen,
   Datenträger und `.json` prüfen, erfolgreich bestätigtes Backup
   kontrollieren. Keine `null`-, leere oder beschädigte Datei durch
   blindes Löschen ersetzen.
5. Für einen geplanten Wechsel auf einen zweiten Robust zuerst den
   bisherigen Autoritätsprozess beenden, dann die nachweislich gleiche
   bestätigte Snapshotversion zum **lokalen** Datenpfad des zweiten
   Robust übertragen, und erst dort starten. Kein automatisches Failover.

## Tests / Stable Gate

CI testet:
- Exklusiven Writer-Lock und Ablehnung eines zweiten Stores
- Freigabe des Locks erst nach Dispose; alter Store kann danach nichts lesen
- Persistierung und Wiederöffnung durch die nachfolgende Store-Instanz
- Ablehnung `null` und defekter JSON-Dateien sowie unterbrochener Erstanlage
- Schutz der gültigen Primärdatei gegen eine veraltete `.tmp`
- Bestehende Experience-ACL-/KV-/CAS-/Rollen-/LSL-Regressionen

**Nicht damit bestanden:** Zwei-Avatar-Firestorm-Allow/Block/Forget,
Regionswechsel, gleichzeitige K/V-CAS-Anfragen aus mehreren Simulatoren,
Multi-Host-Redundanz, Restore-Probe oder harte Last-/IO-Störungstests.
Siehe [RC1_OPERATOR_ACCEPTANCE_09310.md](RC1_OPERATOR_ACCEPTANCE_09310.md).

**Freigabestatus: Dev; keine 0.9.3.10 RC1/Stable-Aussage.**
