# Stadtportal → World API: Passwort vergessen / Password Recovery

Stand: 25.10.2026 | Implementiert in OpenGenesisLINK: PR #303, MariaDB-Fix PR #305.
Dies ist der **verbindliche Backend-Integrationsvertrag**, keine Freigabe einer ungetesteten Produktionsaktivierung.

## 1. Serverbereitstellung (World-/ROBUST-Betreiber)

Die World API stellt bereits bereit:

- `POST https://world.stadt-nexverse.de/api/v1/auth/password-recovery/challenges` mit Scope `password-recovery:issue`
- `POST https://world.stadt-nexverse.de/api/v1/auth/password-recovery/complete` mit Scope `password-recovery:complete`

Prüfen, dass auf dem tatsächlich laufenden World-/ROBUST-Host ein Build **mit beiden Endpunkten und MariaDB-Fix #305** läuft. Der Repository-Merge ist **kein** Deployment-Nachweis.

In der aktiven ROBUST-Konfiguration, zunächst ausschließlich auf Staging:

```ini
[NexVerseWorldApi]
Enabled = true
EnablePrivilegedEndpoints = true
PasswordRecoveryEnabled = true

; Nur falls TLS am eigenen Reverse Proxy terminiert:
; Exakte Backend-Quell-IP des vertrauenswürdigen Proxys, niemals 0.0.0.0/0.
PortalRegistrationTrustedProxyIPs = ""
; Direktes HTTP ausschließlich über ausdrücklich abgesichertes privates Netz,
; sonst unbedingt leer lassen:
PortalRegistrationTrustedHttpPeers = ""
```

Wichtig: Beim KeyHelp/Apache-Reverse-Proxy den Header `X-Forwarded-Proto: https` **serverseitig überschreiben** und den internen ROBUST-Port per Firewall gegen direkten Internetzugriff sperren. Bei direkter TLS-Verbindung sind die Proxy-Ausnahmen nicht erforderlich.

Es wird **keine separate Datenbank** erstellt: Der vorhandene `[UserAccountService]`- bzw. Fallback-`[DatabaseService]`-MariaDB-ConnectionString wird genutzt. Die eigene InnoDB-Tabelle `ogl_password_recovery` benötigt beim ersten Start `CREATE`, danach `SELECT`, `INSERT`, `UPDATE` und `DELETE`. Keine bestehenden `UserAccounts`- oder `auth`-Tabellen manuell ändern. Fehler bei der Tabelleninitialisierung führen zu Recovery-503, sollen aber die übrige World API nicht stoppen.

Im World Admin Center (`/api/v1/docs` → **Schlüssel & Einrichtung**) einen **eigenen Maschinen-API-Key für Stadtportal-Recovery** mit ausschließlich
`password-recovery:issue` und `password-recovery:complete`
anlegen, idealerweise getrennte Keys pro Scope und Umgebung. Beide Scopes sind in der serverseitigen Schlüssel-Whitelist enthalten. Keine `admin:*`, keine `users:register`-Ausweitung, keine Wiederverwendung eines Bürger-Bearers. Den Key nur einmal über einen geschützten Weg zum Portalbetreiber übertragen; nie in Git/Chat/Logs.

## 2. Verbindlicher Vertrag für das Stadtportal

**Nur der Go-/Backend-Prozess spricht mit World.** Browser-JavaScript ruft die beiden Endpunkte niemals selbst auf und bekommt den API-Key niemals zu sehen.

### A – Start: E-Mail angeben

```http
POST /api/v1/auth/password-recovery/challenges HTTP/1.1
Host: world.stadt-nexverse.de
Content-Type: application/json
X-NexVerse-Api-Key: <geheimer-recovery-issue-key>

{"email":"testkonto@example.invalid"}
```

Erfolgreich ermittelte, eindeutige aktive lokale World-Konten erhalten **nur im geschützten Backend** HTTP `201`:

```json
{
  "status": "issued",
  "token": "<einmaliger-world-token>",
  "expires_in": 900,
  "delivery_email": "testkonto@example.invalid",
  "correlation_id": "<korrelations-id>"
}
```

World versendet **keine** E-Mail selbst. Das Portal stellt die Mail mit seinem SMTP-Dienst **ausschließlich an `delivery_email`** zu (nicht an eine vom Browser separat gelieferte Adresse). Beispiel-Link:

```text
https://stadt-nexverse.de/passwort-zuruecksetzen?token=<URL-codierter-world-token>
```

Unbekannte, mehrdeutige oder gedrosselte E-Mail-Zuordnungen: `202 {"status":"accepted","correlation_id":"..."}` **ohne Token**. Dem öffentlichen Benutzer bei **201 und 202 stets dieselbe neutrale Bestätigung** anzeigen („Falls ein Konto existiert, erhältst du eine E-Mail“). Bei `202` keine Reset-Mail mit einem erfundenen Token versenden.

### B – Abschluss: neues Passwort absenden

```http
POST /api/v1/auth/password-recovery/complete HTTP/1.1
Host: world.stadt-nexverse.de
Content-Type: application/json
X-NexVerse-Api-Key: <geheimer-recovery-complete-key>

{"token":"<world-token-aus-dem-link>","new_password":"ExampleNewPassword123"}
```

Bei Erfolg: HTTP `200` mit `{"status":"password_updated","correlation_id":"..."}`. Das neue Passwort wird **ausschließlich durch World/ROBUST** geschrieben. Das Portal darf weder den Legacy-MD5-Hash berechnen noch direkt die Authentifizierungsdatenbank verändern, noch beim Completion-Request eine beliebige UUID als Autorisierung verwenden.

Passwortregel (World-Code): **10–256 UTF-8-Bytes**, mindestens ein ASCII-Großbuchstabe, ein ASCII-Kleinbuchstabe und eine ASCII-Ziffer, kein Null-Zeichen. Frontend- und Backend-Validierung darauf abstimmen; der Server bleibt die letzte Instanz.

### HTTP-Codes

| Code | Bedeutung | Portalreaktion |
|---|---|---|
| `201 issued` | Einmal-Token nur am Backend geliefert | Mail an `delivery_email` schicken |
| `202 accepted` | Kein Token für diese Anfrage | Neutral bestätigen, keine Mail erfinden |
| `200 password_updated` | Passwort geändert | Erfolg anzeigen, erneute Anmeldung anbieten |
| `400 invalid_email/invalid_password/invalid_json` | Ungültiger Input | Formularkorrektur |
| `401 service_key_required/invalid_service_key` | Maschinenkey fehlt/falsch | Backend-Fehler; keine Secrets anzeigen |
| `403 insufficient_scope/https_required` | Scope-/Transportprüfung gescheitert | Konfiguration prüfen; nie Admin-Schlüssel improvisieren |
| `410 invalid_or_expired_challenge` | Einmal-Token ungültig, abgelaufen oder verbraucht | Neue Reset-Mail anfordern |
| `429 rate_limited` | Peer-Rate-Limit | Kurzfristig drosseln, nicht unlimitiert wiederholen |
| `503 recovery_disabled/recovery_backend_unavailable` | World-Konfiguration/MariaDB nicht bereit | Kontrollierter Servicefehler; nicht als erfolgreichen Reset anzeigen |

Jede Antwort enthält eine `correlation_id`. Als Diagnose nur Fehlercode und Korrelations-ID protokollieren. **Keine** Tokens, Passwörter, vollständigen Mailadressen, API-Key-Header oder Reset-URLs in Logs/Analytics.

## 3. Portal-Implementierungsauftrag (bestehender Go-Resetablauf)

1. Einen **dedizierten Portal-Backend-Client** `WorldPasswordRecovery` mit `Issue(email)` und `Complete(token,newPassword)` implementieren, ausschließlich über HTTPS.
2. Eigene bisherige Portal-Reset-Token-Ausstellung für World-Konten ablösen. **Vorhandene Portal-Tokens sind keine World-Challenges**. Insbesondere keine `verified:true`-Abkürzung und kein Aufruf des Self-/Admin-Endpunkts `POST /api/v1/users/{principalId}/password` als Resetersatz.
3. Für den Backend-Client eigene geschützte Konfiguration verwenden, etwa `NEXVERSE_WORLD_RECOVERY_ISSUE_KEY` und `NEXVERSE_WORLD_RECOVERY_COMPLETE_KEY` (***neue empfohlene Variablennamen, im Portal noch zu implementieren***). Nicht den vorhandenen globalen `NEXVERSE_WORLD_API_KEY` ohne eindeutige Scope-Trennung mit Recovery vermischen.
4. Frontend `/passwort-vergessen` ruft **Portal-Backend** auf; Backend fordert World-Challenge an und sendet die E-Mail. `/passwort-zuruecksetzen` übergibt Token und Passwort an das **Portal-Backend**; Backend ruft World-`/complete` auf.
5. Token in URLs URL-kodieren und sichere Referrer-/CSP-Regeln setzen (z. B. `Referrer-Policy: no-referrer`, keine externen Tracker/Analytics auf Reset-Seiten). Kein Token in lokalen Logs; Backends mit `Cache-Control: no-store`.
6. E-Mail nur von World-`delivery_email` verwenden. Bei Fehlversuchen/Zustellproblemen keine Tokeninhalte protokollieren. 15-Minuten-TTL beachten; erneuter Start rotiert ältere Challenges.
7. Nach erfolgreichem Reset die **Portal-eigene** Session-/Refresh-Token-Invalidierung ergänzen, soweit die Portal-Sitzungen unabhängig von World verwaltet werden. World widerruft eigene Refresh-Tokens, aber das allein beweist keine Aufhebung bereits laufender Portal-Cookies.

## 4. Freigabe & Tests

- **Zuerst Staging:** Bereitgestelltes ROBUST-Binary, `PasswordRecoveryEnabled=true`, MariaDB-Tabelle, Proxy/TLS, zwei dedizierte Keys mit exakt erlaubten Scopes.
- Kein Key → 401, falscher Scope → 403, deaktivierte Recovery → 503; **ohne** echte Konto-Passwortänderung testen.
- Mit einem ausdrücklich freigegebenen Staging-Konto E-Mail → Challenge → Link → neues Passwort → World-/Firestorm-Login; altes Passwort muss abgelehnt werden.
- Token zweites Mal verwenden → 410; >15 Minuten → 410; erneute Ausstellung entwertet alten Token; unbekannte Mail legt keine Existenz offen.
- Gegenprüfen, dass andere Accounts, Login, Registrierung und World API weiterhin funktionieren. Datenbank-Backup/Restore sowie Rollback bereithalten.
- **Erst danach** genehmigter Produktiv-Rollout des World-Builds, anschließende Portal-Migration und aktivierte Recovery. Nie einen produktiven Test mit einem echten Einwohnerkonto ohne Freigabe durchführen.

## Quellen

- Code: `NexVerse/Server/Api/NexPasswordRecoveryApi.cs`
- Bootstrap: `NexVerse/Server/Api/NexVerseWorldApiConnector.cs`
- Datastore: `NexVerse/Server/Api/NexPasswordRecoveryStore.cs`
- Bestehende Dokumentation: [WORLD_API_PASSWORD_RECOVERY_P0_301.md](WORLD_API_PASSWORD_RECOVERY_P0_301.md)
- API-Key-Verwaltung: [WORLD_API.md](WORLD_API.md), Abschnitt „Restricted machine API keys“

**Status:** World-Code und MariaDB-Tests vorhanden; produktives Deployment und Portal-Integration nicht durch diesen Text bestätigt.
