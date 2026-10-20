# Stadt NexVerse – Registrierung über stadt-nexverse.de

**Betriebsvertrag für das Go-Stadtportal und ROBUST / OpenGenesisLINK Gen1**  
Stand: 20.10.2026. Umsetzung im Server implementiert, produktive Freigabe nur nach Erstlogin-Abnahme.

## 1. Datenfluss

Browser auf `https://stadt-nexverse.de/registrieren` → **eigenes
Go-Backend** (CSRF, Rate-Limits, Cloudflare-Turnstile-Verifikation,
E-Mail-Verifikation, Einwilligungen, Passwortbestätigung) → HTTPS
`POST https://world.stadt-nexverse.de/api/v1/portal/avatars/register` →
ROBUST → UserAccount/Authentication/Inventory/Avatar/GridUser.

Der Browser erhält **niemals** `NEXVERSE_REGISTER_API_KEY`. Er
kommuniziert nur mit seinem eigenen Portal-Backend. Der Schlüssel gehört
in die geschützte Backend-`.env`, nie in HTML, JS oder öffentlichen Quellcode.

## 2. ROBUST-Einstellungen (aktive bin/Robust.HG.ini)

```ini
[NexVerseWorldApi]
Enabled = true
EnablePrivilegedEndpoints = true
PublicBaseUrl = "https://world.stadt-nexverse.de"
PortalAvatarRegistrationEnabled = true
PortalRegistrationTrustedProxyIPs = "<GENAUE_PROXY_QUELL_IP>"
PortalRegistrationTrustedHttpPeers = ""
PortalRegistrationDefaultRegionName = "Magdeburg"
PortalRegistrationDefaultX = 116
PortalRegistrationDefaultY = 120
PortalRegistrationDefaultZ = 22

[UserAccountService]
CreateDefaultAvatarEntries = true

[OglAvatarTemplates]
Enabled = true
RequirePublishedModels = true
BenjaminPublishedModel = "<GEPRUEFTER_MODELLACCOUNT_MIT_VOR_UND_NACHNAME>"
LunaPublishedModel = "<GEPRUEFTER_MODELLACCOUNT_MIT_VOR_UND_NACHNAME>"
```

Keine Sektionen duplizieren. Die angezeigten Winkelklammern sind
**Platzhalter**. Registrierungsfreigabe erst aktivieren, wenn beide
Modellaccounts vollständig und mit Viewer getestet sind. Die Modellvorlagen
müssen veröffentlichte Wearables/Attachments und zulässige Copy-/Transferrechte
besitzen. Die registrierte Startregion `Magdeburg` muss den
Spawn `116/120/22` erlauben. Der Account-Service setzt Home **und**
Last; nur bei beiden Erfolgen kommt `201 ready:true`.

Verbindungsabsicherung: KeyHelp-Proxies dürfen `X-Forwarded-Proto: https`
nur selbst setzen. Der genaue TCP-Peer muss im erlaubten IP-Verzeichnis
stehen; HTTP zum internen ROBUST nur im abgesicherten Privatnetz/Tunnel.
`PortalRegistrationTrustedHttpPeers` ausschließlich für explizite private
Backend-Verbindungen. Öffentliche Ursprungsports per Firewall schützen.

## 3. Schlüssel anlegen

Unter `https://world.stadt-nexverse.de/api/v1/docs` mit einem
World-API-Administrator (UserLevel ≥ 200, `admin:*`) anmelden.
**Schlüssel & Einrichten → Nur Registrierungs-API-Key erstellen**;
exakt Scope `users:register`. Klartext einmalig kopieren.

```dotenv
NEXVERSE_WORLD_API_URL=https://world.stadt-nexverse.de/api/v1
NEXVERSE_REGISTER_API_KEY=nxk_GEHEIMES_BEISPIEL
NEXVERSE_TURNSTILE_SITE_KEY=PUBLIC_SITE_KEY
NEXVERSE_TURNSTILE_SECRET=PRIVATE_SECRET
```

Turnstile wird **im Backend** serverseitig verifiziert, nicht von ROBUST.
E-Mail-Verifikation und Bot-/CSRF-/IP-Schutz leistet ebenfalls das Portal.

## 4. Exakter POST

```http
POST /api/v1/portal/avatars/register
Content-Type: application/json
X-NexVerse-Api-Key: <BACKEND_API_KEY>
```

```json
{
  "first_name": "Alex",
  "last_name": "Resident",
  "email": "alex@example.org",
  "password": "Langes-sicheres-Passwort-2026!",
  "gender": "female",
  "home_region": {
    "name": "Magdeburg",
    "position": { "x": 116, "y": 120, "z": 22 }
  }
}
```

`first_name`, `last_name`: 2–31 Zeichen, Unicode-Buchstaben am Anfang,
danach Buchstaben/Ziffern/`_`/`-`; ein leerer Nachname wird `Resident`.
Passwort 12–128 Zeichen, E-Mail gültig/maximal 254 Zeichen.
`gender`: `male`/`maennlich`/`männlich` → `ogl:benjamin`;
`female`/`weiblich` → `ogl:luna`. Die Vorlagen sind serverseitig fixiert,
kein frei wählbarer `model_alias`-Parameter. `home_region` ist optional,
kann aber durch das **berechtigte Portal-Backend** aus seinen
Admin-Einstellungen befüllt werden. `home_region.id` ist die echte
Regions-UUID; ID und Name müssen bei kombinierter Übergabe übereinstimmen.

**Erfolg:** HTTP `201 Created`, `success:true`, `ready:true`,
`avatar_id`, `avatar_name`, `first_name`, `last_name`,
`model_name`, `home_region`, `last_position`.

## 5. Go-Backend-Kernaufruf

```go
payload, _ := json.Marshal(worldRegisterRequest)
base := strings.TrimRight(os.Getenv("NEXVERSE_WORLD_API_URL"), "/")
if !strings.HasPrefix(base, "https://") { return errors.New("HTTPS erforderlich") }
req, _ := http.NewRequestWithContext(ctx, "POST", base + "/portal/avatars/register", bytes.NewReader(payload))
req.Header.Set("Content-Type", "application/json")
req.Header.Set("X-NexVerse-Api-Key", os.Getenv("NEXVERSE_REGISTER_API_KEY"))
client := &http.Client{Timeout: 15 * time.Second}
resp, err := client.Do(req)
// Fehler behandeln, Antwort JSON auswerten; nur 201 + success:true
// + ready:true als vollständig registriert betrachten.
```

Die Imports und der vollständige Handler sind dem Go-Projekt anzupassen.
Vor diesem Aufruf müssen die Sicherheitsprüfungen durchgeführt werden.

## 6. Fehlercodes und Betrieb

| HTTP | Code | Prüfung |
|---|---|---|
| 400 | `invalid_json`, `invalid_avatar_name`, `invalid_password`, `invalid_email`, `invalid_gender`, `invalid_home_region` | Eingaben |
| 401 | `unauthorized_portal` | Maschinenkey / `users:register` |
| 403 | `https_required` | HTTPS, Proxy-Quell-IP, Forwarded-Proto |
| 409 | `avatar_name_taken`, `email_registered` | Bereits registriert |
| 413 | `invalid_body_size` | Requestgröße |
| 422 | `registration_failed` | Vorlage, Inventar, Account, Home/Last, Region |
| 503 | `registration_disabled`, `registration_unavailable` | Konfiguration / Server-Log / Korrelations-ID |

Bei einem Verbindungsabbruch nach einem POST **nicht blind erneut
registrieren**: Der Avatar könnte bereits existieren. Konto-Zustand
kontrollieren und ggf. das unvollständige Provisioning reparieren. Der
Prozess ist derzeit **keine** atomare Multi-Service-Transaktion.

**Produktionsabnahme:** Registrieren → Viewer-Login → korrektes
Benjamin-/Luna-Outfit → Inventar → Magdeburg/116/120/22 → Home/Last →
erneuter Login; anschließend Fehler- und Missbrauchsprüfungen.

## 7. Quellen im selben Repository

- `NexVerse/Server/Api/OglPortalRegistrationApi.cs`
- `NexVerse/Server/Api/NexVerseWorldApiConnector.cs`
- `OpenSim/Services/UserAccountService/UserAccountService.cs`
- `doc/NexVerse/PORTAL_REGISTRATION_IAR_HARDENING.md`
- `doc/NexVerse/OGL_AVATAR_STARTER_TEMPLATES.md`

**Die ausführliche Betreiberanleitung mit vollständigen Browser-/Go-
Beispielen ist zusätzlich als Markdown-Download in der Chat-Ausgabe verfügbar.**
