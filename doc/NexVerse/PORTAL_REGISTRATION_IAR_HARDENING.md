# OGL Stadtportal – Avatarregistrierung und IAR-Anti-Flood

**Stand:** 13.10.2026. Development, keine automatische Aktivierung auf dem Live-Grid.

## Avatarregistrierung (Stadtportal-Backend)

`POST /api/v1/portal/avatars/register` wird von der OpenGenesisLINK
World API bereitgestellt. Der Aufruf ist **ausschließlich Server-zu-Server**
erlaubt: Der Browser sendet niemals den geheimen API-Schlüssel.
Das Portal-Backend muss CAPTCHA, E-Mail-Zustellung/-Verifizierung, CSRF-
Schutz, eigene IP-/E-Mail-Drosselung und die Nutzungsbedingungen
durchsetzen. Die World API ist kein öffentliches anonymes Signup-Formular.

Zurückgegeben wird JSON mit `success`, `avatar_name`,
`first_name`, `last_name`, `model_name`, `avatar_id` und `message`.
Bei Fehlern: `success: false`, `error`, `message` und passender
HTTP-Code (400/401/403/409/413/422/503).

### Vertrag

```http
POST /api/v1/portal/avatars/register
Content-Type: application/json
X-NexVerse-Api-Key: <SERVER_ONLY_KEY>
```

```json
{
  "first_name": "Alex",
  "last_name": "",
  "password": "ein-sehr-langes-sicheres-passwort",
  "email": "alex@example.org",
  "gender": "male"
}
```

- Fehlender/leer übergebener Nachname wird `Resident`.
- `gender: male`/`maennlich`/`männlich` aktiviert
  `model_alias: ogl:benjamin` ⇒ `Benjamin Resident`.
- `gender: female`/`weiblich` aktiviert `ogl:luna` ⇒
  `Luna Resident`.
- Das Modell kann **nicht durch einen Client-Parameter überschrieben**
  werden. Die vorhandene `UserAccountService.CreateUser(..., model)`-
  Pipeline übernimmt den Outfit- und Berechtigungspfad.
- Nur gültige Avatarnamen (2–31 Zeichen; Unicode-Buchstaben,
  Ziffern, Unterstrich, Bindestrich), E-Mail und Passwörter
  zwischen 12 und 128 Zeichen werden akzeptiert.
- Doppelter Avatarname ⇒ `409 avatar_name_taken`; registrierte
  E-Mail ⇒ `409 email_registered`. Nicht vorbereitete Startervorlage
  ⇒ `422 registration_failed`.

### KeyHelp-Reverse-Proxy, HTTP-Backend und Startposition

Das Stadtportal nutzt öffentlich HTTPS. KeyHelp beendet TLS am
VHost und leitet die Registrierung intern per HTTP an ROBUST
weiter; ROBUST selbst muss deshalb **kein HTTPS-Zertifikat**
bereitstellen. Der bereits vorhandene `POST
/api/v1/portal/avatars/register`-Handler erkennt die HTTPS-Verbindung
über `X-Forwarded-Proto: https` **nur**, wenn der tatsächliche
TCP-Absender eine lokale Loopback-Adresse oder eine unter
`PortalRegistrationTrustedProxyIPs` exakt freigegebene Proxy-IP ist.
`X-Forwarded-For` wird hierfür **nicht** ausgewertet, damit ein
beliebiger Browser keine interne Proxy-IP vortäuschen kann.

```ini
[NexVerseWorldApi]
PortalAvatarRegistrationEnabled = false
; Die IP muss die bei ROBUST ankommende KeyHelp-IP sein, nicht die Besucher-IP!
PortalRegistrationTrustedProxyIPs = "10.78.0.2"
PortalRegistrationTrustedHttpPeers = ""
PortalRegistrationDefaultRegionName = "Magdeburg"
PortalRegistrationDefaultX = 116
PortalRegistrationDefaultY = 120
PortalRegistrationDefaultZ = 22
```

Die IP `10.78.0.2` ist nur ein **Beispiel**; die tatsächliche
Quell-IP des KeyHelp-Proxys ermitteln und einsetzen. Beim KeyHelp-VHost
die vorhandene `X-Forwarded-Proto`-Angabe serverseitig auf
`https` setzen/überschreiben (nur im HTTPS-VHost).
Der HTTP-Ursprungsport von ROBUST muss per Firewall nur für
KeyHelp beziehungsweise die relevanten Dienste erreichbar sein.
Ein KeyHelp-Proxy über das öffentliche Internet ohne VPN oder
anderweitig geschützte Backend-Strecke verschlüsselt die
**Verbindung KeyHelp → ROBUST nicht**: API-Schlüssel und
Registrierungspasswort könnten dabei mitgelesen werden.

Falls das Stadtportal-Backend statt über KeyHelp direkt
`http://<ROBUST-INTERN>:80/api/v1/portal/avatars/register`
aufrufen soll, kann ausschließlich dessen tatsächliche feste
Quell-IP in `PortalRegistrationTrustedHttpPeers` eingetragen
werden. Dies gilt **nur für eine private, geschützte
Backend-Verbindung (Loopback oder VPN)** und ist bewusst
standardmäßig leer. Eine unbeschränkte Freigabe von HTTP oder
`X-Forwarded-Proto` für beliebige öffentliche Clients wird
nicht unterstützt. Beide Listen akzeptieren kommagetrennte
**literale IPv4-/IPv6-Adressen**, keine Hostnamen oder
`0.0.0.0/0`.

Bei aktivierter Registrierung kann das Stadtportal-Backend
eine Startregion/Position im Request auswählen; dies ist kein
frei zugängliches Browser-Formular, sondern eine autorisierte
serverseitige Auswahl über den Key mit `users:register`:

```json
{
  "first_name": "Alex",
  "last_name": "Resident",
  "email": "alex@example.org",
  "password": "ein-langes-sicheres-passwort",
  "gender": "female",
  "home_region": {
    "name": "Magdeburg",
    "position": { "x": 116, "y": 120, "z": 22 }
  }
}
```

Statt `home_region.name` kann `home_region.id` die
existierende Region-UUID bestimmen. Werden beide übergeben,
müssen Name und UUID zusammenpassen. Ohne `home_region`
gelten die `PortalRegistrationDefault*`-Einstellungen.
Die Region muss im **autoritativen GridService** registriert
sein; die Position muss innerhalb der Region liegen
(`z` 1–4096). ROBUST erstellt dann die kopierte
Benjamin- bzw. Luna-Starterausstattung und schreibt **sowohl
Home als auch Last** in den GridUser-Service. Nur wenn beide
Positionen erfolgreich gespeichert wurden, antwortet die
API mit **HTTP 201**, `success: true`, `ready: true`
und der bestätigten Startposition. Bei teilweisem
Provisionierungsfehler wird keine Erfolgsmeldung ausgegeben,
und der Account wird nach Möglichkeit gesperrt;
wegen fehlender Multi-Service-Transaktion kann eine
manuelle Bereinigung erforderlich sein.

**Prüfbefehl** (nur vom autorisierten Backend/Proxy aus;
nicht in einem Browser oder auf einem öffentlichen Netz
mit nacktem HTTP ausführen):

```bash
curl -i 'https://world.stadt-nexverse.de/api/v1/portal/avatars/register' \
  -H "X-NexVerse-Api-Key: ${NEXVERSE_REGISTER_API_KEY}" \
  -H 'Content-Type: application/json' \
  --data '{"first_name":"Alex","last_name":"Resident","email":"alex@example.org","password":"ein-sehr-langes-sicheres-passwort","gender":"male"}'
```

Bei `403 https_required` kommt die Anfrage nicht als
autorisierte HTTPS-Proxy-Anfrage an; Proxy-IP und
`X-Forwarded-Proto` prüfen. `503 registration_disabled`
bedeutet fehlende Freigabe oder einen nicht verfügbaren
`IOglPortalAccountProvisioner`. `422 registration_failed`
erfordert Prüfung von Modell-Freigabe und Home-/Last-
Initialisierung im ROBUST-Log. `401 unauthorized_portal`
deutet auf fehlenden/falschen Key oder Scope hin.
`409 avatar_name_taken`/ `email_registered` bedeutet,
dass ein Name beziehungsweise die E-Mail bereits existiert.
Bei einem Netzwerkausfall nach dem POST vor erneuter
Registrierung nachsehen, ob der Avatar bereits angelegt wurde.

### Freigabe

In der aktiven Robust-INI (Standard **false**):

```ini
[NexVerseWorldApi]
PortalAvatarRegistrationEnabled = true
```

Dazu einen **eigenen**, auf genau `users:register` begrenzten
World-API-Maschinenschlüssel ausstellen und **nur im Backend**
des Stadtportals speichern.

### Registrierungsschlüssel im World API Control Center erstellen

1. Die geschützte World-API-Oberfläche unter
   `https://world.stadt-nexverse.de/api/v1/docs` aufrufen und als
   Administrator (UserLevel mindestens 200 mit `admin:*`) anmelden.
2. Im Menü **Schlüssel & Einrichten** den eigenständigen Button
   **Nur Registrierungs-API-Key erstellen** betätigen. Er ignoriert die
   Vorauswahl der Experiences-/NexBus-Schlüssel und stellt **nur**
   `users:register` aus. Alternativ das neue Kontrollkästchen
   **Stadtportal / Avatarregistrierung** bei der gemeinsamen Ausgabe wählen.
3. Die einmalige Ausgabe `NEXVERSE_REGISTER_API_KEY=nxk_...` über
   **Konfiguration kopieren** sichern und **ausschließlich** in die
   geschützte `.env` des Stadtportal-Backends einfügen. Nicht in HTML,
   Frontend-JavaScript, einen öffentlichen API-Explorer oder einen
   Quellcode-Commit übernehmen. Die API erwartet den Key im
   HTTP-Header `X-NexVerse-Api-Key`.
4. Bei einem bereits vorhandenen aktiven Schlüssel mit gleichem Namen
   und Scope erscheint eine Duplikatmeldung; der alte Klartextschlüssel
   wird **nicht erneut angezeigt**. Vor einer Rotation bestehenden
   Schlüssel kontrolliert deaktivieren und das Backend ohne Unterbrechung
   auf den neuen Schlüssel umstellen.

Das Anlegen des Schlüssels aktiviert **nicht** automatisch
`PortalAvatarRegistrationEnabled`. Die Modelle und Sicherheitsgates
müssen weiterhin wie unten beschrieben geprüft werden. Die
Cloudflare-Turnstile-Schlüssel `NEXVERSE_TURNSTILE_SITE_KEY` und
`NEXVERSE_TURNSTILE_SECRET` werden unabhängig davon im Cloudflare
Dashboard eingerichtet; deren Tokenvalidierung erfolgt ausschließlich
im Stadtportal-Backend.
 Es darf weder ein breiter
`users:write`-Key noch ein Admin-Token an den Browser gelangen.
Externes HTTPS bleibt Voraussetzung. Internes HTTP funktioniert nur
fuer explizit freigegebene KeyHelp-Proxy-IPs mit vom Proxy gesetztem
`X-Forwarded-Proto: https` oder fuer gesondert freigegebene
private Backend-IPs. Weitere Details und Firewall-Regeln stehen oben.

Die bestehenden OGL-Starter-Vorlagen müssen separat freigegeben
werden: `[OglAvatarTemplates] Enabled=true`,
`RequirePublishedModels=true`, veröffentlichte Model-Accounts
mit geprüftem Outfit/Transferrechten und
`[UserAccountService] CreateDefaultAvatarEntries=true`.
Ohne vorbereitete Vorlagen **nicht aktivieren**.

**Offener Produktions-Gate:** Der Account-Service nutzt bei
Fehlschlägen eine Best-Effort-Kompensation, keine atomare
Multi-Service-Transaktion. Vollständige Datenbereinigung,
Testregistrierung/Erstlogin, Mailverifikation und
Frontend-Missbrauchstests sind vor öffentlichem Launch erforderlich.

## IAR-Anti-Flood

Der vorhandene **verwaltete** IAR-Import ruft
`OglIarInspector.Inspect(path)` vor
`DearchiveInventory` auf. Die Inspektion prüft:

- maximale komprimierte Größe **1 GiB**, maximale entpackte
  Daten (TAR-Payload + Header) **2 GiB**;
- maximal **100.000** Einträge und **64 MiB** pro TAR-Eintrag;
- gültige TAR-Header-Prüfsummen, Oktalgrößen, zulässige
  Datei-/Ordnertypen, keine Symlinks/PAX/Sparse-Payloads;
- keine absoluten oder Traversal-Pfade und keine doppelten
  Dateinamen; nur `archive.xml`, `inventory/` und `assets/`;
- Kontroll-XML mit Major-Version maximal 1;
  DTD/XXE verboten; Inventareinträge vor Assets;
- vollständiges GZip-Ende, ausschließlich Nullpadding am TAR-Ende
  und Erkennung extremer Kompressionsverhältnisse.

Bei Ablehnung erfolgt **kein** Aufruf des verwalteten
Archiver-Imports. Das schützt den regulären World-API-
und Region-Managementpfad gegen Archivmüll,
übermäßige Dekompression und offensichtlich falsche Strukturen.

**Update für 0.9.3.12:** Der klassische Konsolenbefehl
`load iar` verwendet jetzt dieselbe validierende Vorprüfung
über `IInventoryArchiveInspector`. Bei fehlendem Prüfdienst oder
ungültigen Archiven wird der Import vor jeder Inventar-/Asset-
Änderung verweigert. Die Vorprüfung ermittelt gleichzeitig die
Gesamtzahl der Archiveinträge für die Fortschrittsanzeige.
Der geschützte Konsolenpfad akzeptiert lokale IAR-Dateien,
keine Remote-URIs.

**Grenze:** Die Prüfung validiert weiterhin nicht sämtliche
LSL-/Asset-Semantik. Andere direkte Archiver-Einstiege, die
nicht über den verwalteten Manager oder `load iar` laufen,
müssen noch auf dieselbe Prüflogik gebracht werden.
Parallelimporte, Quotas pro Betreiber/Nutzer,
Dateiänderung zwischen Inspektion und Import (TOCTOU)
und Asset-Inhaltsprüfung benötigen weitere Härtung.
Ein IAR ohne Freigabe sollte nicht unkontrolliert in
Produktion importiert werden.

Tests: `tools/ci/OglIarPreflightRegression` erstellt echte
gzip/TAR-Archive mit gültigen und manipulierten Einträgen.
