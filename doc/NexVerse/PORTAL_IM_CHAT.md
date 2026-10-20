# OpenGenesisLINK – Bidirektionaler IM-Webchat im Bürgerportal

**Implementiert im Serverbranch `feature/citizen-im-inbox-history` (nach Merge in `main`); keine automatische Produktivfreigabe.**  
Ziel: Inworld-IM aus Firestorm empfängt der Avatar im Bürgerportal und umgekehrt. Auch Portal↔Portal-Nachrichten werden über OpenSims bestehende IM-Zustellung versandt.

## Bestehendes IM-System wird nicht ersetzt

- Der reguläre `HGInstantMessageService` und der Viewer-Chat bleiben maßgeblich.
- `HGMessageTransferModule` meldet **erfolgreich angenommene** direkte Avatar-IMs per internem HMAC-Transport an Robust, unabhängig von der Region.
- Portalnachrichten werden erst dem OpenSim-IM-Dienst übergeben und anschließend im opt-in Posteingang archiviert. Kein Rückschreiben oder Manipulieren der OpenSim-OfflineIM-Tabellen.
- Direktnachrichten sind **nur zwischen bestätigten lokalen Freunden** über die Web-API zulässig. Andere IM-Typen (Gruppen, Objekte, Inventory Offers) werden nicht ins Portalarchiv übernommen.
- `202 Accepted` bedeutet vom Server akzeptiert, nicht „gelesen“. Eine Firestorm-Lesebestätigung existiert nicht.
- Neues Verhalten setzt sowohl Robust- als auch Simulator-Konfiguration voraus; ohne Aktivierung bleibt die bisherige Web-IM-Versandfunktion erhalten.

## Diagnose: World-Nachrichtendienst meldet HTTP 503

Das Stadtportal kann einen World-API-Upstream-Fehler als
`world_upstream_error` zusammenfassen. Dieser Code ist **kein**
spezifischer World-API-Fehler. Bei
`POST /api/v1/messages` ist der echte World-Response-Body und
`X-Correlation-Id` fuer die Ursachensuche ausschlaggebend.
`relationships:write` wird zum Senden benoetigt; ein
`relationships:read`-Token allein reicht nicht.

| HTTP-Code / World-Fehler | Bedeutung |
| --- | --- |
| 503 `instant_message_service_unavailable` | Der konfigurierte `HGInstantMessageService` wurde weder beim World-API-Start noch beim spaeteren Nachladen aufgeloest |
| 503 `recipient_lookup_unavailable` | Benutzerkonto-Abfrage beim `UserAccountService` hat eine Exception geworfen |
| 503 `friendship_lookup_unavailable` | Freundschafts-Datenquelle hat eine Exception geworfen |
| 503 `message_delivery_unavailable` | `HGInstantMessageService.IncomingInstantMessage` hat eine Exception geworfen |
| 503 `message_not_delivered` | Der IM-Dienst hat weder Online-Zustellung noch eine akzeptierte Offline-IM bestaetigt |
| 503 `portal_im_archive_disabled` | Ein Archiv- oder Opt-in-Schreibzugriff wurde ohne aktivierten `NexPortalIM`-Store angefordert |
| 503 `portal_im_store_unavailable` | Die optionale SQLite-Archivabfrage ist fehlgeschlagen |

Bei den IM-Zustellungsfehlern protokolliert ROBUST jetzt den genauen
Schritt und die `correlation_id`; der Nachrichtentext wird nicht
protokolliert. Der World-Response bei diesen Fehlern enthaelt
`error` und `correlation_id`. Ein blosses HTTP 503 im Portal ohne
World-Body darf nicht als eindeutig festgestellte Ausfallursache
interpretiert werden.

Der bestehende `HGInstantMessageService` wird beim World-API-Start
geladen und bei noch fehlendem Transport zum ersten Versand
erneut aufgeloest. Erzeugen mehrere Dienste unabhaengige
IM-Serviceinstanzen, lesen **alle** ihre `[Messaging].MessageKey`-Einstellung.
Ein fehlgeschlagener nachtraeglicher Audit-Eintrag verwandelt eine
bereits akzeptierte IM nicht in HTTP 503; damit verhindert der
Dienst, dass das Stadtportal wegen eines Auditproblems die
Nachricht automatisch doppelt versendet.

Zur Pruefung mit einem echten Einwohner-Token und bestaetigter
lokaler Freundschaft:

```bash
curl -i -X POST "https://world.stadt-nexverse.de/api/v1/messages" \
  -H "Authorization: Bearer ${WORLD_TOKEN}" \
  -H "Content-Type: application/json" \
  --data '{"to_agent_id":"<EMPFAENGER-UUID>","message":"Nachrichtentest"}'
```

Den zurueckgegebenen `error`-Wert und die `X-Correlation-Id`
mit dem ROBUST-Log abgleichen. **Nachrichten nach einem
unklaren HTTP-Fehler nicht blind erneut senden:** Eine
Netzwerkunterbrechung kann nach erfolgreicher Zustellung
eingetreten sein. Ob eine IM im Simulator ankam oder im
OfflineIMService archiviert wurde, ist gesondert zu pruefen.
`NexPortalIM` ist nur fuer das optionale Portalarchiv zustaendig,
nicht fuer die eigentliche Hypergrid-IM-Zustellung.

## HTTP-Endpunkte

Basis: konfigurierte World API, bei NexVerse voraussichtlich `https://hg.stadt-nexverse.de/api/v1`. **Auf dem tatsächlich genutzten Host prüfen.**

| Methode | Pfad | Beschreibung |
|---|---|---|
| POST | `/messages` | Bisheriger authentifizierter Nachrichtenversand an bestätigten lokalen Freund |
| GET | `/messages/settings` | Eigene Opt-in-Einstellung lesen |
| PATCH | `/messages/settings` | Opt-in aktivieren/deaktivieren (false löscht gespeicherten Verlauf) |
| GET | `/messages` | Eigene Konversationsübersicht |
| GET | `/messages/events?after=0&limit=50` | Neue **eigene** Nachrichten, chronologisch nach Cursor |
| GET | `/messages/with/{peerId}?after=0&limit=50` | Eigene Konversation mit einem Avatar |
| DELETE | `/messages/history` | Eigene gespeicherte Portalhistorie löschen |

Alle Endpunkte verlangen ein OAuth-Bearer-Token des betroffenen lokalen Einwohners; GET benötigt `relationships:read`, PATCH/POST/DELETE `relationships:write`. Die Avatar-ID wird aus dem Token bestimmt. Selbst `admin:*` erlaubt hier **kein** Ausgeben als fremder Avatar. Browser dürfen **niemals** den geheimen internen HMAC-Endpunkt verwenden.

### Opt-in vor der ersten Unterhaltung

```http
PATCH /api/v1/messages/settings
Authorization: Bearer <authentifiziertes Einwohner-Token>
Content-Type: application/json

{"enabled":true}
```

Standard ist `false`. Erst nach ausdrücklicher Aktivierung werden **neue** Chatnachrichten für den Einwohner erfasst. Es gibt keinen rückwirkenden Zugriff auf zuvor übertragene IMs.

Wenn der Nutzer den Chat ausschaltet, wird seine **gesamte Portalhistorie automatisch gelöscht**. Zusätzlich bietet der Menüpunkt „Chat → Einstellungen“ eine Schaltfläche „Nachrichtenverlauf löschen“ (`DELETE /messages/history`). Das löscht **nicht** bereits in Firestorm/OfflineIM oder bei anderen Teilnehmern gespeicherte Nachrichten.

### Senden

```http
POST /api/v1/messages
Content-Type: application/json
Authorization: Bearer <Token>

{"to_agent_id":"<UUID eines bestätigten Freundes>","message":"Hallo aus dem Portal!"}
```

Maximal 1024 UTF-8-Bytes; der Server übernimmt Avatar-ID und Anzeigenamen aus dem authentifizierten Konto. Das Ergebnis enthält `status: "accepted"`, `event_id`, `archived`, `to_agent_id` und `correlation_id`. `archived=false` bedeutet **nicht**, dass die IM gescheitert ist; mindestens ein zuständiges Portalarchiv wurde nicht bestätigt.

### Empfang und Verlauf

```http
GET /api/v1/messages/events?after=0&limit=50
Authorization: Bearer <Token>
```

Beispielstruktur:

```json
{
  "messages": [{
    "seq": 177,
    "event_id": "11111111-2222-3333-4444-555555555555",
    "from_agent_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "to_agent_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    "peer_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "sender_name": "Beispiel Avatar",
    "message": "Hallo zurück!",
    "source": "viewer",
    "accepted_at": "2026-10-09T12:00:00Z"
  }],
  "count": 1,
  "next_cursor": 177,
  "poll_after_seconds": 5
}
```

Das Stadtportal speichert den `next_cursor` **nur für das eigene eingeloggte Konto** und fragt ihn etwa alle **5 Sekunden** erneut ab. Bei Fehlern Exponential Backoff, bei Logout Polling stoppen und den gespeicherten Token/Cursor löschen. Die Nachrichtenansicht niemals über die neue Avatar-UUID eines anderen Kontos weiterverwenden.

Mit `GET /messages/with/{peerId}` erhält man die betreffende Unterhaltung. `GET /messages` listet gespeicherte Gesprächspartner mit letzter Nachrichtenzeit (keine globalen Nachrichtenlisten). `after` ist ein monoton steigender Sequenzcursor, kein Zeitstempel; `limit` 1–100.

## HTTP 503 bei Nachrichten-Einstellungen

`GET /api/v1/messages/settings` benötigt `relationships:read` eines
angemeldeten **lokalen** Einwohners, `PATCH` benötigt
`relationships:write`. Das ist kein `profile:read`-Problem.

Wenn `[NexPortalIM]` fehlt oder `Enabled = false` ist, wird
**kein** Chatarchiv initialisiert. Zuvor antwortete selbst
`GET /messages/settings` mit HTTP 503
(`portal_im_archive_disabled`). Jetzt meldet der lesende Endpunkt
**HTTP 200** mit:

```json
{
  "enabled": false,
  "available": false,
  "archive": "opt_in",
  "retention_days": "configured",
  "polling_interval_seconds": 5
}
```

Bei aktivem, funktionsfähigem Store kommt ebenfalls HTTP 200, jetzt
mit `available: true` und dem echten nutzerspezifischen
`enabled`-Wert. Das Portal muss `available: false` als
**Betreiber-Konfiguration fehlt** darstellen und die
Chat-Aktivierung deaktivieren. Es darf diesen Status nicht als
erfolgreich eingeschalteten Chat ausgeben. Solange kein Store
existiert, bleiben `PATCH /messages/settings` und Nachrichtenabrufe
mit `503 portal_im_archive_disabled` gesperrt. Ein laufender Store mit
Datenbankproblemen meldet dagegen
`503 portal_im_store_unavailable`; Details stehen im ROBUST-Log unter
`[NEX-PORTAL-IM]`. Keine automatischen Datenbankneuanlagen durch
einen bloßen Lesezugriff außerhalb des konfigurierten Store-Starts.

Die produktive `bin/Robust.HG.ini` und `bin/OpenSim.ini` enthalten
nun beide die `[NexPortalIM]`-Sektion, aus Sicherheitsgründen zunächst
`Enabled = false`. Für die tatsächliche Chat-Nutzung:

1. EINE geheime, zufällige Schlüsselzeichenfolge mit mindestens
   32 UTF-8-Bytes erzeugen, z. B. `openssl rand -hex 32`.
   Sie geschützt als `NEX_PORTAL_IM_SECRET` in
   `/etc/nexverse/nexverse.env` (oder der tatsächlich benutzten
   Secret-Quelle) auf ROBUST **und jedem beteiligten Simulator**
   mit demselben Wert hinterlegen. Bereits existierende Secrets
   nicht ohne Migrationsplanung ersetzen.
2. In `bin/Robust.HG.ini` `[NexPortalIM] Enabled = true`
   setzen. Der ROBUST-Dienstbenutzer muss den Ordner
   `data/` sowie `data/nexverse-portal-im.sqlite`
   schreibend erreichen; `StorePath` wird relativ zum
   **Arbeitsverzeichnis** des ROBUST-Prozesses aufgelöst.
3. Für Firestorm-Empfang in jedem beteiligten Simulator
   `bin/OpenSim.ini` auf `[NexPortalIM] Enabled = true`
   stellen. `IngestUrl` muss auf den geschützten ROBUST-
   Endpunkt `/internal/nexportal/im/v1` zeigen. Loopback
   `127.0.0.1` nur verwenden, wenn ROBUST tatsächlich auf
   **demselben Host** läuft; andernfalls einen privaten,
   abgesicherten HTTPS-Endpunkt nutzen und den Ingest-Zugang
   für Fremde sperren.
4. ROBUST und betroffene Simulatoren vollständig neu starten.
   `GET /api/v1/messages/settings` mit einem lokalen
   `relationships:read`-Token prüfen (`available: true`,
   `enabled: false`). Erst danach per `PATCH` mit
   `relationships:write` und `{"enabled": true}`
   das persönliche Opt-in aktivieren.

Ein Tokenproblem liefert 401/403 statt der
`portal_im_archive_disabled`-Antwort. HTTP 503 aufgrund
fehlender Speicheranbindung darf **nicht** durch ein allgemeines
`enabled: true` verdeckt werden.

## Robust-Konfiguration

Die Beispielsektionen liegen in `bin/Robust.HG.ini.example` und `bin/Robust.ini.example`:

```ini
[NexPortalIM]
    Enabled = true
    StorePath = "data/nexverse-portal-im.sqlite"
    RetentionDays = 30
    SharedSecret = "${Environment|NEX_PORTAL_IM_SECRET}"
```

Generiere **einen neuen zufälligen Wert mit mindestens 32 Bytes** als Umgebungsvariable `NEX_PORTAL_IM_SECRET` und verwende **denselben** Wert in Robust und allen beteiligten Simulatorprozessen. Nicht mit OAuth-, OIDC- oder NexBus-Schlüsseln gleichsetzen und den Wert weder committen noch protokollieren. Der Datenbankpfad muss mit restriktiven Dateirechten für den Robust-Prozess beschreibbar sein.

Der separate SQLite-Store ist allein für den Portalchat. Er führt keine Migrationen an den bestehenden OpenSim-Nutzerdatenbanken durch. Datensicherung und Löschrichtlinie separat festlegen.

## OpenSim-Simulator-Konfiguration

In `bin/OpenSim.ini.example`:

```ini
[NexPortalIM]
    Enabled = true
    IngestUrl = "http://127.0.0.1/internal/nexportal/im/v1"
    SharedSecret = "${Environment|NEX_PORTAL_IM_SECRET}"
```

`http://127.0.0.1` ist nur bei Robust **auf demselben Host** zulässig. Bei getrennten Hosts ausschließlich geschütztes HTTPS verwenden und den Ingest-Pfad durch Firewall/Reverse Proxy auf die Simulatoren beschränken. Intern verlangt der Endpoint `X-NexPortal-IM-Time` und `X-NexPortal-IM-Signature`; Signaturen werden über die **rohen Nutzdaten** geprüft, mit engem Zeitfenster und idempotenter Ereignis-ID. Portal-Browser benötigen und erhalten dieses Secret niemals.

**Wichtig:** Aktivierung erfolgt in Robust **und jedem Simulator**, dessen Viewer-IMs im Portal erscheinen sollen. Ein Update auf GitHub ändert keine laufende INI.

## Sicherheit und Datenschutz

- Nur aktivierte Empfänger/Absender besitzen einen eigenen Nachrichtenbestand. Keine nachträgliche Übernahme alter Viewer-Nachrichten.
- Ein Viewer-Absender ohne Portal-Opt-in kann auf eine IM an einen Portal-Empfänger antworten: Nur dessen bereits aktivierter Posteingang enthält die neue Nachricht.
- Der Store trennt Daten pro Eigentümer, nutzt Parameterbindung und dedupliziert signierte Simulatorereignisse. Standardaufbewahrung 30 Tage (konfigurierbar 7–365), maximale 10.000 Einträge je Eigentümer.
- Nachrichtentext wird **nicht** in Audit-Protokollen erfasst. Lokale Dateisystemberechtigungen/verschlüsselte Datenträger und Zugriffskontrolle des Portalservers bleiben erforderlich.
- `POST /messages` hat zusätzlich eine Begrenzung von 30 Nachrichten pro Minute je Avatar. Für HTTP gilt weiterhin der zentrale World-API-Rate-Limiter.
- Bei Verbindungsproblemen übergibt der Simulator die IM trotzdem an Firestorm. Das optionale Webarchiv ist **best effort** mit drei kurzen Versuchen, kein garantierter Event-Stream. Eine nicht archivierte IM darf nicht als archiviert angezeigt werden.
- Die UI darf **niemals** behaupten, dass eine Nachricht gelesen wurde. `accepted_at` entspricht der serverseitigen Aufnahmezeit.
- Für Nutzerrecht auf Löschung in Portal eigenen `DELETE /messages/history` anbieten; Konto-Löschworkflow zusätzlich überprüfen (nicht als automatisch integriert behaupten).

## Abnahmetests vor Portal-Freigabe

1. Opt-in für Avatar A über das Stadtportal aktivieren; Avatar B bleibt in Firestorm.
2. A sendet aus Portal an B in anderer Region/Simulator: B empfängt echte Inworld-IM.
3. B antwortet in Firestorm: A sieht neue `source: "viewer"` Nachricht beim nächsten Poll (`GET /messages/events`).
4. Beide Teilnehmer melden sich im Portal an und optieren ein: Web→Web-Kommunikation und Gesprächsverlauf prüfen.
5. Zwei Nutzer außerhalb der Freundschaft dürfen keine Portal-IMs senden; auch mit einem fremden Bearer dürfen sie keine Inbox lesen.
6. Opt-out von A löscht dessen Verlauf; B behält nur seinen eigenen Bestand, falls B weiter optiert ist.
7. Falsche HMAC-Signatur/alter Timestamp muss auf dem internen Ingest-Pfad zu 401 führen.
8. Kein Webportal-Schlüssel erscheint in API-Antworten, Browser-JavaScript, Logs oder HTML.
9. Ausfall des Ingest-Dienstes: Firestorm-Nachricht funktioniert weiter; Portal meldet keine falsche Zustellung/Archivierung.
10. Aufbewahrung, Neustart-Persistenz, Lastbegrenzung und Kontolöschung im Grid testen.

## Portalimplementierung

Für die Stadtportal-Oberfläche eine Karte-/Tab-Lösung „Freunde“ und „Nachrichten“ anbieten, mit Opt-in-Einwilligungsdialog und personenbezogenem Cursor-Polling. Die Weltkarte `GET /api/v1/world-map/view` bleibt unabhängig. Der Browser benötigt **keine direkte SQL-Verbindung**, kein Administrations-Token und kein serverseitiges HMAC-Secret.

**Status:** Code in GitHub ist kein Nachweis des produktiven Live-Verhaltens. Vor Werbung als vollständigen Live-Chat alle oben genannten Abnahmetests durchführen.
