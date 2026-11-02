# R632 – Persönliche IAR-API: Eigentümer-Status, kein Scheinbackup

Stand: 2. November 2026. Implementierungs-Branch für den OpenGenesisLINK-World-Server; **nicht produktiv freigegeben**.

## Ausgangslage

Das Stadtportal erwartet `/api/v1/iar/me/export`, `/api/v1/iar/me/import`, persönliche Operationen samt Status, Upload, Download, Prüfsummen und Löschung. World bietet bisher die administrative IAR-API unter `/api/v1/iar/export`, `/import` und `/operations/{id}` mit `regions:manage` und expliziter Regions- und User-ID. Diese Administrationsrechte dürfen nicht für Bürger verwendet werden.

## Implementierte, gesicherte Teilfunktion

- `GET /api/v1/iar/me/operations/{id}`: Benötigt persönlichen World-Bearer mit `inventory:read`, ein authentifiziertes World-Bürgerkonto und dieselbe Principal-ID. API-Key- und Service-Principals werden abgewiesen. Nur bestehende IAR-Jobs mit der passenden `user_id` im persistenten World-Jobstore können gelesen werden; fremde und unbekannte IDs erhalten gleichförmiges HTTP 404.
- Die Erfolgsantwort enthält ausschließlich `ok`, `operation_id`, `status` und `progress`, nie Pfade, Metadaten, vollständige Jobobjekte, Protokolle oder intern gespeicherte Fehlermeldungen.
- Persönliche Export- und Import-Aufrufe antworten nach Authentifizierung explizit mit HTTP 503 und dem maschinenlesbaren Code `personal_iar_backend_unavailable`. Sie erstellen **keine** Datei und keinen Auftrag. Damit wird kein IAR-Backup vorgetäuscht.
- Persönliche Upload-, Start-, Download-, Prüfsummen- und Löschrouten sind noch nicht implementiert. Die administrative IAR-API wird nicht verändert.

## Noch erforderlich für einen tatsächlich funktionierenden persönlichen Export

1. Einen berechtigten, erreichbaren Simulator für das Inventar des authentifizierten Owners bestimmen; nie ungeprüft eine vom Browser übermittelte Regions- oder User-ID übernehmen.
2. Korrelierte, idempotente Exportjobs im bestehenden Simulator-IAR-Archiver ausführen; tatsächliche Inventarhierarchie, Skripte und Assetbytes als vollständiges GZip/TAR-IAR sichern.
3. Fertiggestellte Archivdaten privat und vollständig über den World-/Portal-API-Kanal bereitstellen, mit Größenlimits, Ablauf, Content-Type, Content-Length, Prüfsummen, Archivprüfung und geschütztem Lifecycle.
4. Import per vorbereiteter Prüfung, begrenzten Upload-Blöcken, Sandbox, Schutz gegen Traversal, Hardlinks und Dekompressionsbomben, expliziter Bestätigung und konsistentem Rollback implementieren.
5. Eigentümerrechte für **alle** Operationen, Quota, Rate-Limits, OpenAPI-Dokumentation und authentifizierte Export-/Wiederherstellungs-E2E-Tests ergänzen.

## Prüfung

`tools/ci/test_iar_personal_owner_status_r632.py` führt vier **statische** Source-Guards durch. Diese ersetzen keine kompilierten C#-Tests. Die GitHub-CI enthält den vollständigen C#-Build. **Keine World-Produktionsfreigabe vor authentifizierter End-to-End-Abnahme der Archivierung.**

## R633 – Persönliche Exportaufträge (Entwicklung, noch kein Download)

- Der World-Bearer muss einem lokalen World-Bürger mit `inventory:read` gehören; API-Keys, Service-Accounts und fremde Owner-IDs bleiben ausgeschlossen. Für den neuen `POST /api/v1/iar/me/export` muss die JSON-Nutzlast **exakt** `{"confirm":true}` enthalten (maximal 256 Zeichen; andere Felder und benutzerdefinierte Regionen werden abgelehnt).
- Nur die serverseitig für den authentifizierten Account ermittelten `GridUserInfo.LastRegionID` beziehungsweise `HomeRegionID` werden zur Simulatorauswahl benutzt. Voraussetzung sind ein online gemeldeter, nicht drainender/nicht im Wartungsmodus befindlicher Node und eine tatsächlich freigeschaltete IAR-Exportberechtigung. Bei fehlenden Daten wird **kein Auftrag** angelegt.
- Erst nach diesen Prüfungen wird ein persistenter Job `archive.iar.export` mit `personal_iar_owner`-Besitzerbindung, ausschließlich serverseitig erzeugtem zufälligen `.iar`-Dateinamen und Inventarpfad `/` registriert. Ein korrelierter `archive.iar.requested`-Event startet den bestehenden Simulator-Archiver. Erfolgsantwort ist HTTP 202 mit **Status `queued`**, nicht `completed`.
- Die fertiggestellte Archivdatei liegt zunächst in der Simulator-IAR-Verwaltung. **Ein privater Browserdownload, Dateitransport zwischen Simulator und World, Prüfsumme, Lebenszyklus sowie ein verifizierter Import fehlen weiterhin.** Die Antwort weist ausdrücklich darauf hin, dass ein Download noch nicht verfügbar ist. R633 darf nicht als vollständig nutzbare Bürger-IAR-Funktion ausgegeben oder ohne diese Nacharbeiten produktiv freigegeben werden.
- Die statischen Prüfungen `test_iar_personal_export_r633.py` und `test_iar_personal_owner_status_r632.py` sowie der World-API-Test müssen vor dem C#-Build grün sein. Ein authentifizierter End-to-End-Export auf einem World-Stagingkonto bleibt zusätzlich erforderlich.
CI-Freigabe: Dieser Entwurfs-Branch wird gegen `main` geprüft; keine automatische World-Produktionsausrollung.
