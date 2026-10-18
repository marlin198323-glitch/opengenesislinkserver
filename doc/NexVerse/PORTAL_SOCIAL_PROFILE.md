# NexVerse Bürgerportal – Freunde, Profil, Nachrichten, Gruppen und Display Name

Diese Schnittstellen nutzen die vorhandenen OpenSimulator-Datenspeicher
und OpenGenesisLINK-Authentifizierung. **Keine direkte SQL-Verbindung**
aus dem Stadtportal zulassen; ausschließlich OAuth2/OIDC mit PKCE S256
und die World API verwenden.

## Freundschaften

- `GET /api/v1/relationships/{principalId}` — Freundesliste,
  ausgehende/eingehende Anfragen, Rechte und sichtbarer Online-Status.
- `POST /api/v1/relationships/{principalId}/{friendId}` — Anfrage versenden
  (Status 202); wiederholte Anfragen werden mit 409 zurückgewiesen.
- `PUT /api/v1/relationships/{principalId}/{friendId}` — **nur eingehende**
  Anfrage akzeptieren; kein eigenmächtiges Akzeptieren durch Absender.
- `DELETE /api/v1/relationships/{principalId}/{friendId}` —
  Anfrage ablehnen/zurückziehen oder Freundschaft beenden.
- `PATCH /api/v1/relationships/{principalId}/{friendId}` mit
  `{"rights":1}` — Rechte einer bestätigten Freundschaft anpassen.
- `PUT /api/v1/relationships/{principalId}/blocks/{friendId}` —
  blockiert Avatar und beendet Freundschaft.

Der API-Scope ist `relationships:read` bzw. `relationships:write`.
**Ownerprüfung gilt zusätzlich**. Der Online-Status eines Freundes
wird nur gezeigt, soweit dieser die notwendigen Viewer-Freundesrechte
gewährt hat. Die Viewer-Benachrichtigung für neue Portal-Einladungen
soll im Live-Test verifiziert werden; ausstehende Angebote werden beim
Login vom bestehenden OpenSim FriendsModule erkannt.

## Direktnachrichten

`POST /api/v1/messages` mit Body:

```json
{"to_agent_id":"<UUID eines bestätigten lokalen Freundes>","message":"Hallo!"}
```

Der authentifizierte Avatar ist der **einzige** zulässige Absender.
Die Route akzeptiert ausschließlich bestätigte lokale Freundschaften,
maximal 1024 UTF-8-Bytes und benutzt die reguläre OpenSim
`HGInstantMessageService`-Zustellung. Wenn der Dienst fehlt oder die
Zustellung fehlschlägt, meldet die API einen Fehler anstatt Erfolg
zu behaupten. Offline-Zustellung hängt von der Installation und
Konfiguration des vorhandenen `OfflineIMService` ab.

**Neu:** Eine optionale, eigentümergeschützte Portal-Chat-Historie und
cursorbasierter Empfang sind implementiert. Vor dem ersten Empfang
muss der Einwohner ausdrücklich zustimmen. Die Server- und
Simulator-Konfiguration, API-Endpunkte, Datenschutzregeln und
Abnahmetests stehen in [PORTAL_IM_CHAT.md](PORTAL_IM_CHAT.md).
Es gibt weiterhin keinen öffentlichen/globalen IM-Verlauf,
keinen Hypergrid-Fremdadressen-Webchat und keine Lesebestätigung.
Audits speichern nur Empfänger-ID und Nachrichtengröße, **nie**
Nachrichteninhalt.

## Avatarprofil

Die World API verwendet die autoritative `userprofile`-Tabelle. Das
Stadtportal kann das eigene Avatarprofil mit einem OAuth-Bearer-Token
(Scope **`profile:write`**) bearbeiten; eine andere Avatar-UUID erfordert
`admin:*`. Ohne Token oder mit unzureichendem Scope erfolgen keine Änderungen.

**Schreibgeschützt, auch für Admin-Token und bei direkten API-Anfragen:**

- `useruuid`: Identität wird ausschließlich über den URL-Pfad bestimmt
- `profileImage`: Profilbild-Asset-UUID; nur über Viewer/anderweitig autorisierte Mechanismen
- `profileFirstImage`: First-Life-Bild-UUID; ebenfalls gesperrt

Ein `PATCH /api/v1/profiles/{principalId}` darf dagegen die **elf**
verbleibenden `userprofile`-Spalten ändern:

| Datenbankspalte | JSON-Feld im Portal |
| --- | --- |
| `profilePartner` | `partner_id` |
| `profileAllowPublish` | `visibility` (`public`/`private`) |
| `profileMaturePublish` | `mature` (Boolean) |
| `profileURL` | `web_url` |
| `profileWantToMask` | `interests.want_to_mask` |
| `profileWantToText` | `interests.want_to_text` |
| `profileSkillsMask` | `interests.skills_mask` |
| `profileSkillsText` | `interests.skills_text` |
| `profileLanguages` | `interests.language` |
| `profileAboutText` | `about` |
| `profileFirstText` | `first_life_text` |

Die Datenbankspaltennamen selbst werden als alternative JSON-Schlüssel
ebenfalls akzeptiert (z. B. `profileURL` statt `web_url`). Doppelte
Benennung derselben Spalte in einem PATCH ist unzulässig. Der Partner muss
ein vorhandener anderer lokaler Avatar sein oder als `null` gelöscht werden.

Beispiel für einen vollständigen zulässigen PATCH-Body:

```json
{
  "about": "Willkommen in Stadt NexVerse",
  "web_url": "https://stadt-nexverse.de",
  "partner_id": null,
  "visibility": "public",
  "mature": false,
  "first_life_text": "Mein First-Life-Profil",
  "interests": {
    "want_to_mask": 0,
    "want_to_text": "Neue Kontakte",
    "skills_mask": 0,
    "skills_text": "Stadtentwicklung",
    "language": "Deutsch"
  }
}
```

Nur übermittelte Felder werden aktualisiert. Die SQL-UPDATE-Anweisung enthält
**weder** `useruuid`, `profileImage` **noch** `profileFirstImage` in
ihrer `SET`-Liste; `useruuid` wird ausschließlich als `WHERE`-Schlüssel
genutzt. Versuche, eines dieser Felder zu ändern (oder Felder außerhalb
`userprofile`, etwa `online_visibility`), werden mit HTTP 403
`profile_field_readonly` abgewiesen. Falsche Feldtypen werden mit HTTP 400
zurückgewiesen. Keine Änderung erfolgt bei einem ungültigen Request.

**Lesen:** `GET /api/v1/profiles/{principalId}` enthält auch die
gesperrten Bildreferenzen als Nur-Lese-Informationen. Private Profile
benötigen den Eigentümer oder `admin:*`. Der Profilbild-Endpunkt
`GET /api/v1/profiles/{principalId}/image` liefert nur autorisierte Bilder.
Ein direkter Bild-Upload gehört **nicht** zum aktuellen Profil-PATCH.

Die Profile-API erfordert `[UserProfilesService] Enabled = true` mit
konfiguriertem Datenbankprovider.

## Display Name

`PATCH /api/v1/users/{principalId}` mit
`{"display_name":"Gewünschter Anzeigename"}`.
Die bestehende Normalisierung und Self-Service-Sperre von sieben Tagen
bleiben unverändert. Das unveränderliche Login-`username` bleibt
davon getrennt.

## Gruppenmitteilungen – auch Adminbereich

- `GET /api/v1/groups` — Gruppen suchen.
- `GET /api/v1/groups/{groupId}` — Profil und Einstellungen lesen.
- `GET /api/v1/groups/{groupId}/members` — Mitgliederliste.
- `GET /api/v1/groups/{groupId}/roles` — Rollen und Powers.
- `GET /api/v1/groups/{groupId}/notices` — Mitteilungen lesen.
- `POST /api/v1/groups/{groupId}/notices` mit
  `{"from_name":"...","subject":"...","message":"..."}` —
  Mitteilung mit **bestehender Gruppenberechtigung** versenden.
- Einladungen, Bans, Rollenverwaltung und Gruppenaccounting sind
  bereits über weitere `/groups/`-Routen vorhanden.

Gruppen-Scopes `groups:read` und `groups:manage` öffnen
**nicht automatisch fremde Gruppen zur Verwaltung**; die
OpenSim-Gruppenpowers bleiben maßgeblich.
Im Stadtportal-Adminbereich sollen Änderungen fremder Gruppen nur
erfolgen, wenn dafür eine **ausdrücklich implementierte und
geprüfte** Administrationsrichtlinie besteht.

## Technische Grenzen

- Das Stadtportal-Frontend ist nicht Teil dieses GitHub-Repositories.
  Navigation, Formulare und Darstellungsansichten müssen im separaten
  Website-Projekt implementiert werden.
- Kein automatisches Live-Deployment, keine DB-Migration.
- Nach einem Update Firestorm-Live-Tests für Anfragen, Online-Status,
  blockierte Nutzer, Gruppenrechte, Profile und IM durchführen.
