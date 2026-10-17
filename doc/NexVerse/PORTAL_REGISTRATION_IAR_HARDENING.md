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

### Konfigurierbare Home- und Last-Position (Stadtportal-Admin)

Das Stadtportal kann seine eigene Startregion mitsamt region-lokalen XYZ-Koordinaten
**serverseitig** verwalten. Beispiel: `Magdeburg / 116 / 120 / 22`.
Der POST-Aufruf akzeptiert dafür zusätzlich ein optionales `home_region`-Objekt:

```json
{
  "first_name": "Alex",
  "last_name": "Resident",
  "password": "ein-sehr-langes-sicheres-passwort",
  "email": "alex@example.org",
  "gender": "male",
  "home_region": {
    "name": "Magdeburg",
    "position": { "x": 116, "y": 120, "z": 22 }
  }
}
```

Alternativ kann `home_region.id` die UUID der Zielregion enthalten.
Wenn `id` und `name` angegeben werden, müssen beide dieselbe Region
bezeichnen. Ist `home_region` nicht angegeben, gelten als rückwärtskompatible
Standardeinstellung `Magdeburg / 116 / 120 / 22`.

- Die World API löst den Regionnamen über den Grid-Service zur tatsächlichen UUID auf.
- `position.x`, `position.y`, `position.z` sind Zahlen. Wird das
  `position`-Objekt angegeben, müssen alle drei Koordinaten enthalten sein.
- X/Y müssen innerhalb der Region liegen; Z ist auf 0 bis 4096 begrenzt.
- `SetHome` und `SetLastPosition` speichern dieselbe Region-UUID und Position;
  die Blickrichtung ist `<0,1,0>`.
- Erfolgreiche Antworten enthalten zusätzlich `home_region` mit Region-ID,
  Regionname und den gespeicherten XYZ-Koordinaten.
- Unbekannte Region: `422 home_region_not_found`; ungültige Koordinaten:
  `400 invalid_home_position`; fehlender Grid-Service:
  `503 grid_service_unavailable`.
- Bei fehlgeschlagenem SetHome/SetLastPosition wird **kein** HTTP 201 gemeldet.
  Der Account-Service versucht den neu angelegten Account-Datensatz
  bestmöglich zu entfernen. Es ist **kein atomarer Rollback**;
  verwaiste Datensätze müssen betrieblich geprüft werden.

**Sicherheitsgrenze:** Das öffentliche Registrierungsformular darf
`home_region` **nicht** selbst setzen. Nur ein berechtigter Portaladministrator
ändert diese Einstellungen im Go-Backend (rollen-/CSRF-geschützte Admin-Route).
Das Portal liest beim Registrieren die gespeicherten Admin-Werte und ergänzt
sie in seinem authentifizierten Server-zu-Server-JSON an die World API.
Der `users:register`-API-Schlüssel bleibt ausschließlich im Backend.
Für einen erfolgreichen Viewer-Login muss die konfigurierte Region
erreichbar und der Spawnpunkt zugänglich sein; ein erfolgreicher
Datenbankschreibvorgang garantiert allein keinen Login.

### Freigabe

In der aktiven Robust-INI (Standard **false**):

```ini
[NexVerseWorldApi]
PortalAvatarRegistrationEnabled = true
```

Dazu einen **eigenen**, auf genau `users:register` begrenzten
World-API-Maschinenschlüssel ausstellen und **nur im Backend**
des Stadtportals speichern. Es darf weder ein breiter
`users:write`-Key noch ein Admin-Token an den Browser gelangen.
`HTTPS` ist zwingend; bei TLS-Offloading wird
`X-Forwarded-Proto: https` nur von einer lokalen Loopback-Verbindung
akzeptiert. Über einen unverschlüsselten öffentlichen Port 80
ist die Registrierung gesperrt.

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
