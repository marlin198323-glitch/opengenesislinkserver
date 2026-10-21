# OpenGenesisLINK – Starter-Avatar-Vorlagen (Phase 1)

> Stand 21.10.2026: Der geschützte Registrierungs-Endpunkt
> `POST /api/v1/portal/avatars/register` ist implementiert.
> In der versionierten `bin/Robust.HG.ini` sind die vorhandenen
> Modellavatare `Benjamin Resident` und `Luna Resident` explizit
> freigegeben; die anderen Beispiele bleiben aus Sicherheitsgründen
> deaktiviert. Der produktive Betrieb setzt die Prüfung der
> getragenen Itemrechte und einen echten Viewer-Erstlogin voraus.

## Ziel

Das Stadtportal soll bei der Registrierung einen Starter-Avatar auswählen lassen:

| Portal-ID | Modellavatar |
| --- | --- |
| `ogl:benjamin` | Benjamin Resident |
| `ogl:luna` | Luna Resident |

Es werden **nur die aktuell getragenen Wearables, Bodyparts und Attachments**
(einschließlich getragener Mesh-Objekte) übernommen, nicht das vollständige
Inventar des Modellavatars.

## Implementierter Integrationspunkt

`OpenSim/Services/UserAccountService/UserAccountService.cs` verarbeitet
`CreateUser(..., model)` bereits mit `EstablishAppearance` und
`CopyWearablesAndAttachments`. Die OGL-Erweiterung löst die expliziten
Portal-IDs zu den Modellavataren auf. Der bestehende OpenSim-Mechanismus
kopiert Inventar-Einträge, legt Current-Outfit-Links an und setzt die Appearance.

Die Funktion ist bewusst **standardmäßig ausgeschaltet**.

```ini
[UserAccountService]
CreateDefaultAvatarEntries = true

[OglAvatarTemplates]
Enabled = false
BenjaminModel = "Benjamin Resident"
LunaModel = "Luna Resident"
RequirePublishedModels = true
BenjaminPublishedModel = ""
LunaPublishedModel = ""
```

Nach expliziter Freigabe kann `Enabled = true` gesetzt werden.
Die Portal-Registrierung muss den Modellparameter `ogl:benjamin` oder
`ogl:luna` über einen **authentifizierten serverseitigen**
Registrierungs-Endpunkt an `CreateUser` übergeben. Eine direkte anonyme Registrierung wird weiterhin nicht angeboten:
Der vorhandene Endpunkt akzeptiert ausschließlich einen serverseitigen
`users:register`-API-Key nach der E-Mail-Bestätigung im Stadtportal.

## Versionierte Modellaccounts (vorbereiteter Publish-Workflow)

Mit `RequirePublishedModels = true` (Standard) akzeptiert der Server
OGL-Startervorlagen **nur**, wenn `BenjaminPublishedModel` beziehungsweise
`LunaPublishedModel` auf einen bestehenden, vollständig vorbereiteten
Modellaccount mit Vor- und Nachnamen gesetzt ist. Leere Werte und veröffentlichte Modelle ohne Wearables werden abgelehnt. Auch bei deaktiviertem
`CreateDefaultAvatarEntries` wird eine Starter-Registrierung vor
Kontoerstellung abgewiesen.

Beispiel: `BenjaminPublishedModel = "BenjaminV1 Resident"`.
Für eine neue Outfit-Version wird ein separater Account vorbereitet und
nach manueller Freigabe der Konfigurationswert geändert. Der Live-Avatar
`Benjamin Resident` bleibt davon getrennt. Die veröffentlichten Accounts
müssen administrativ gegen weitere Bearbeitung und Logins geschützt werden.

**Wichtig:** Dies ist eine Version-Pinning-Konfiguration, **noch kein**
automatischer, unveränderlicher Snapshot-Export und kein Admin-Publish-Endpunkt.

## Weitere Registrierungsprüfungen

Leere Vor- oder Nachnamen werden abgewiesen. Ein Modellavatar darf sich
nicht mit sich selbst als Startervorlage registrieren (UUID- und Namensprüfung).
Diese Prüfungen ersetzen keine vollständige Portalvalidierung.
Fehlt der Authentifizierungsdienst, wird die Starter-Registrierung
vor Kontoanlage abgelehnt. Schlägt das Setzen des Passworts fehl,
liefert sie einen Fehler zurück, ohne weitere Initialisierung.
Schlägt die Inventarinitialisierung fehl, liefert der Starter-Registrierungsweg
nun einen Fehler zurück. **Achtung:** Da die Kontoanlage im bestehenden
OpenSimulator-Ablauf davor erfolgt, ist das kein atomarer Rollback: Der Dienst versucht bei Passwort-, Inventar- oder Appearance-Fehlern nun,
den zuvor angelegten Account-Datensatz über die Account-Datenbank zu löschen.
Diese **Best-Effort-Kompensation ist keine Transaktion**: Authentifizierungsdaten,
Inventar, Avatar-Appearance und GridUser-Daten können zurückbleiben. Scheitert
die Löschung, wird ein Fehler mit der betroffenen UUID protokolliert. Vor
der Abnahme ist ein vollständiger, überprüfbarer Cleanup-Pfad erforderlich.

## Aktuelle Grenzen

1. Die Übernahme nutzt derzeit den **Live-Appearance-Zustand** der
   Modellavatare. Unveränderliche Snapshots und Admin-Publish fehlen noch.
2. Der vorhandene OpenSimulator-Kopierpfad muss vor Freigabe auf
   Transfer-/Copy-Rechte, Objektinhalte und verlinkte Attachments geprüft
   und gegebenenfalls gehärtet werden. Die OGL-Alias-Funktion allein
   gewährleistet diese Prüfung noch nicht.
3. Das Stadtportal muss den vorhandenen Registrierungs-POST serverseitig
   **erst nach erfolgreicher E-Mail-Bestätigung** aufrufen; das eigentliche
   Portal-Projekt wird nicht mit diesem Repository deployt.
4. Unbekannte oder deaktivierte `ogl:`-IDs werden nun **vor der Kontoerstellung**
   abgelehnt. Ebenso werden fehlende Modellaccounts, Appearance oder
   benötigte Services abgewiesen. Die Portal-API muss diese Ablehnung
   korrekt als Registrierungsfehler behandeln.
5. GitHub-CI für den serverseitigen Registrierungsvertrag läuft; der
   vollständige Browser→Portal→KeyHelp→ROBUST→Firestorm-Live-Test muss
   beim Betreiber stattfinden und wird durch einen Merge nicht ersetzt.

## Zusätzliche Sicherheitsgrenze

Die gemeinsame Inventarauflösung unterstützt direkte Einträge und genau eine
Current-Outfit-Verknüpfung für Wearables **und** Attachments. Verschachtelte
und zyklische Links werden verworfen. Die neue Vorabprüfung kontrolliert nun, ob alle in der Appearance
referenzierten Wearables und Attachments vorhanden sind und die
Current-/Next-Owner-Berechtigungen **Copy + Transfer** besitzen.
Eine erneute Validierung unmittelbar vor dem Kopieren reduziert die
Zeitlücke zwischen Registrierungsvorabprüfung und Übernahme.
Leere Item-UUIDs werden als ungültig behandelt. Sie garantiert
**noch nicht**, dass verschachtelte Inhalte von
Objektinventaren ebenfalls freigegeben sind. Die bestehende
`CopyWearablesAndAttachments`-Routine wird weiterhin verwendet.
Ein vollständiger atomarer Registrierungs-/Inventar-Workflow und
Snapshot-Publishing sind nicht implementiert. Fehlgeschlagene Starterregistrierungen
versuchen lediglich die Account-Zeile zu entfernen; die übrigen Service-Daten
werden noch nicht transaktional zurückgerollt. Die Vorlage bleibt
bis zur Rechteprüfung und echten Integrationstests deaktiviert.

## Geplante Phase 2

- Tiefenprüfung verschachtelter Attachment-Inhalte, Link-Ketten und
  Race-Conditions zwischen Vorabprüfung und tatsächlichem Kopieren.
- Veröffentlichbare, versionierte Snapshots, getrennt vom Live-Avatar.
- Authentifizierte API für das Stadtportal mit ausschließlich
  freigegebenen Template-IDs.
- Tests für Mesh-Body, Mesh-Head, Kleidung, COF und Erstlogin.

**Integrationsstand:** Änderungen liegen inzwischen auf `main` und können dort getestet werden. Keine Stable-Freigabe und keine Zugriffe auf Entwicklerrechner.
