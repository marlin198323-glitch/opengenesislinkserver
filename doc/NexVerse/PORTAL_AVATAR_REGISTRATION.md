# Stadt-NexVerse – Avatar-Registrierungs-API

**Entwicklungsstatus:** Feature-Branch, noch keine produktive Freigabe.
Das Portal verwendet ausschließlich seinen **Backendserver** für diese API;
Browser und JavaScript dürfen niemals den API-Schlüssel erhalten.

## Route

`POST /api/v1/portal/avatars/register` auf der bestehenden World API.

Die Funktion liegt hinter `[NexVerseWorldApi] EnablePrivilegedEndpoints=true`
und zusätzlich `[NexPortalRegistration] Enabled=true`. Initial ist sie
**deaktiviert**. Der Stadtportal-Server benötigt einen **eigenen**,
klein privilegierten API-Schlüssel mit
`portal:avatars:register`, nicht `admin:*`.

Ein TLS-geschützter Transport ist zwingend. Optional kann
`TrustedTlsProxy=true` mit `TrustedProxyIp=127.0.0.1` gesetzt
werden, wenn der vorgeschaltete Reverse-Proxy allein die
`X-Forwarded-Proto: https`-Angabe bestimmt und sein interner
Listener per Firewall ausschließlich vom Proxy erreichbar ist.
HTTP mit Passwörtern über die öffentliche Port-80-Adresse ist
**nicht erlaubt**.

### Anfrage

```json
{
  "first_name": "Beispiel",
  "last_name": "",
  "password": "ein-sicheres-passwort",
  "email": "beispiel@example.org",
  "gender": "female"
}
```

- `first_name` erforderlich.
- `last_name` optional oder leer ⇒ automatisch `Resident`.
- `password` erforderlich, 8–256 Zeichen; nur über Backend/TLS.
- `email` erforderlich, syntaktisch geprüft, maximal 64 Zeichen.
  Eine E-Mail-**Besitzbestätigung** ist **nicht** implementiert.
- `gender`: `male` / `female` (oder `maennlich`,
  `männlich` / `weiblich`). Es geht ausschließlich um die Auswahl
  des Startermodells, nicht um ein dauerhaft gespeichertes Geschlecht.
- `model_name`, Account-UUID und Berechtigungen werden vom
  Client **nicht** angenommen. Der Server legt bei männlicher
  Auswahl `ogl:benjamin` (logisch **Benjamin Resident**),
  bei weiblicher `ogl:luna` (logisch **Luna Resident**) fest.
- Passwort und E-Mail werden weder in Erfolgsausgaben
  noch im Auditprotokoll zurückgegeben.
- Höchstens 8 KiB JSON und 20 Anfragen je Minute pro
  authentifiziertem Portal/IP-Schlüssel, zusätzlich zum World-API-Gate.

### Erfolg (HTTP 201)

```json
{
  "success": true,
  "message": "Avatar Beispiel Resident wurde erfolgreich registriert.",
  "avatar_name": "Beispiel Resident",
  "first_name": "Beispiel",
  "last_name": "Resident",
  "model_name": "Luna Resident",
  "avatar_id": "hier-die-neue-uuid"
}
```

### Fehler

- `400 invalid_avatar_name`, `invalid_password`, `invalid_email`
  oder `invalid_gender`: Formular korrigieren.
- `401 portal_key_required` / `403 portal_access_denied`: fehlende
  Berechtigung des Portalservers.
- `409 avatar_name_taken`: Name ist bereits vergeben.
- `429 rate_limited`: zu viele Kontoanlagen.
- `426 https_required`: Verbindung nicht ausreichend geschützt.
- `503 registration_unavailable` oder
  `starter_model_unavailable`: Backend/Startervorlagen sind nicht
  freigegeben bzw. Modell-/Inventar-Dienste fehlen.
- `503 registration_failed`: Registrierung nicht abgeschlossen;
  Administrator muss vorhandene Datensätze prüfen.

Fehler erscheinen immer als JSON mit `success:false`,
`error` und verständlichem `message`.

## Modellfreigabe (Pflicht vor Live-Schaltung)

```ini
[NexPortalRegistration]
Enabled = false
TrustedTlsProxy = false
TrustedProxyIp = "127.0.0.1"

[OglAvatarTemplates]
Enabled = false
BenjaminModel = "Benjamin Resident"
LunaModel = "Luna Resident"
RequirePublishedModels = true
BenjaminPublishedModel = ""
LunaPublishedModel = ""
```

Vor echter Verwendung müssen beide Modellavatare bzw.
versionierte veröffentlichte Modellaccounts vollständig
vorbereitet, die Copy-/Transfer-Rechte geprüft, Avatar-Appearance
und Wearables getestet und die Freigabeoptionen bewusst aktiviert
werden. Bei `RequirePublishedModels=true` müssen
`BenjaminPublishedModel` und `LunaPublishedModel` auf die
**tatsächlich freigegebenen** Accountnamen zeigen. Eine
leere Freigabe führt zur absichtlichen Ablehnung **vor der
Kontoanlage**.

Der bestehende OpenSimulator-CreateUser-Prozess nutzt
einen **Best-Effort**-Cleanup; Konto-, Auth- und
Inventarspeicherung sind **keine atomare Transaktion**.
Vor öffentlichem Betrieb: Duplikat-/Paralleltests, eine vollständige
Cleanup-Prozedur und reale Login-/Starter-Avatar-Tests nachweisen.
Ein aktives Konto darf nach Teilfehler nicht als erfolgreich
ausgegeben werden.
