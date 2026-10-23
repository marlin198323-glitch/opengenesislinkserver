# Issue #289 – Persönliche World-API-Fachscopes / kontrollierte Betreiberabnahme

**Status:** Quellcode-Fix und Regressionen; **kein Zugriff auf produktive Benutzerzuweisungen, kein produktives OAuth-Client-Provisioning und kein bestätigter Portal-Neulogin**. Darum Issue #289 **nicht** allein aufgrund grüner CI schließen.

## Root Cause

Der Resident-Login (`POST /api/v1/auth/session`) verwendet
`NexAuthorizationPolicy.GetEffectiveScopes(UserLevel, Roles, ExplicitScopes)`.
Administratoren besitzen regelmäßig `admin:*`, ohne dass die benötigten
einzelnen Scopes `statistics:read`, `regions:read`, `simulators:read`,
`estates:read` usw. notwendigerweise **ausgeschrieben** werden. Der World
API-Server akzeptiert `admin:*` gegebenenfalls als Wildcard, aber das
Stadtportal darf diesen String nicht zu expliziten Fachscopes erfinden.
Maschinen-API-Keys sind hiervon **unabhängig**.

Die bestehenden World-Routen für die Benutzerrechte funktionieren bereits:

- `GET /api/v1/auth/authorization-model` – zulässige Rollen und Scopes
- `GET /api/v1/users/{principalId}/authorization` – gespeicherte persönliche Zuweisung
- `PATCH /api/v1/users/{principalId}/authorization` – gezielte persönliche Zuweisung mit `reauthentication_required=true`

Der PATCH bleibt ausschließlich über einen **persönlichen Admin-Bearer** mit
`security:manage` nutzbar; für Administrator-Zielkonten zusätzlich
`admin:*`. Es wird **kein** Maschinen-API-Key zum Bearbeiten oder
Ausstellen persönlicher Rechte verwendet.

## Umsetzung: Control Center

Nach Installation des neuen Builds:

1. HTTPS-`https://world.stadt-nexverse.de/api/v1/docs` öffnen und im
   **geschützten Control Center** als Grid-Administrator einloggen.
2. **Schlüssel & Einrichtung** öffnen, nach unten zu
   **„5. Persönliche World-API-Berechtigungen (keine Maschinen-Schlüssel)”**.
   Das ist ausdrücklich **nicht** die Schlüsselverwaltung aus Abschnitt 4.
3. Für das *autorisierte* Zielkonto die Principal-UUID eingeben; alternativ
   **„Eigenes Admin-Konto verwenden”**. Persönliche Rollen/Scopes aus World
   laden und prüfen.
4. Nur wirklich erforderliche Lese-Scopes markieren, z. B. für den
   Betreiberadministrator `statistics:read`, `regions:read`,
   `simulators:read`, `estates:read`; für die eigene Bürger-Finanzseite
   bei einem berechtigten Bewohner `economy:read`.
   **Nicht** allen Bürgern alle Admin-, Eigentümer-, Service- oder
   Finanztransferrechte zuweisen.
5. Vor dem Schreiben lädt das Frontend Rollen und explizite Scopes erneut.
   Die API unterstützt optionale `expected_roles` und
   `expected_scopes`; eine erkennbare Abweichung führt zu
   HTTP 409 `authorization_changed` (erneut laden statt blind ersetzen).
   Diese Prüfung ist **kein verteilter SQL-CAS** bei gleichzeitig
   schreibenden unabhängigen ROBUST-Prozessen; größere Installationen
   benötigen einen einzigen autoritativen Rechte-Schreibpfad.
6. Änderung explizit bestätigen, anschließend **vollständig abmelden
   und neu anmelden**: Sicherheitsstempel wird erhöht, Refresh-Sessions
   werden widerrufen. Bei eigenen Admin-Zuweisungen muss auch die
   Control-Center-Sitzung erneuert werden.
7. Im Stadtportal nach Neuanmeldung `GET /api/me` prüfen: nur die von
   World tatsächlich ausgestellten Fachscopes, nicht synthetisch aus
   `admin:*` abgeleitete Werte, dürfen erscheinen.

## Technischer API-Vertrag

Das bestehende `PATCH /api/v1/users/{principalId}/authorization` behält
die Rückwärtskompatibilität. Neue **optionale** Vorbedingungen:

```json
{
  "scopes": ["regions:read", "statistics:read"],
  "expected_scopes": ["regions:read"],
  "expected_roles": ["administrator"]
}
```

Dieses Beispiel ist **keine vollständige echte Kontozuweisung**. `scopes`
ersetzt die **vollständige explizite Liste**, daher muss sie aus dem
unmittelbar zuvor gelesenen GET-Wert gebildet werden. Alle bestehenden
Rollen bleiben unangetastet, sofern `roles` nicht übermittelt wird.
Eine Fehlermeldung `authorization_changed` heißt, die Zuweisung wurde
seit dem letzten GET verändert. Privilegierte Zuweisungen benötigen
weiterhin den bisherigen Admin-Nachweis; `security:manage` alleine
reicht für ein Administrator-Zielkonto nicht aus.

## Persönlichen Token UND World-GET beweisen

Einen **frisch ausgestellten** persönlichen World-Bearer in der
laufenden Shell geschützt bereitstellen; keinen Maschinen-Key
verwenden, keine Secrets ins Issue kopieren. Beispiel auf dem Betreiberhost:

```bash
read -r -s -p "Frischer persönlicher World-Bearer: " WORLD_PERSONAL_BEARER
printf '\n'
export WORLD_PERSONAL_BEARER
python3 tools/diagnostics/verify_world_personal_scope_289.py \
  --expect statistics:read regions:read
unset WORLD_PERSONAL_BEARER
```

Die Diagnose prüft `GET /api/v1/users/me` mit dem Bearer und danach
für `statistics:read` die tatsächlich autorisierte
`GET /api/v1/statistics/summary`-Antwort mit
`protected_details=true`. Der JWT-Payload wird nur als
**unverifizierter Scope-Hinweis** gelesen. Eine erfolgreiche, vom
World-Server überprüfte Antwort mit demselben Bearer ist entscheidend.
Die Diagnose gibt keine Tokens, Hashes, Principal-IDs oder World-Rohdaten
aus. `200` ohne `protected_details` reicht für Issue #289 nicht aus.

Nach dem Login im Stadtportal unbedingt vergleichen, ob
`GET https://stadt-nexverse.de/api/me` die expliziten Scopes in
der **neuen** Portal-Sitzung enthält. Die Portaländerung wurde im
separaten Repository `marlin198323-glitch/nexverse` umgesetzt, ist
aber weder ein Ersatz für den World-Bearer noch ein Beleg für einen
Live-World-Deploy.

## OAuth / OIDC-Client für das Stadtportal – nicht automatisch provisionieren

Der World-Server stellt bereits `/oauth/authorize`, `/oauth/token`,
`/.well-known/openid-configuration` und die Adminregistrierung
`POST /api/v1/auth/clients` bereit. `public` (kein Secret)
und `confidential` (Secret einmalig) sind unterstützte interaktive
Clienttypen, `service` ist **nicht** für persönliche Logins gedacht.

Das Stadtportal verwendet aktuell den nativen Resident-Session-Login.
Ein echtes OAuth-Cutover erfordert vorab eine abgestimmte und im
Portal **implementierte** HTTPS-Callback-Route, genehmigte
`redirect_uri`, freigegebene `allowed_scopes`, `client_id` und
serverseitiges PKCE-S256/State-Handling. Im vorgeschlagenen Beispiel
`https://stadt-nexverse.de/auth/world/callback` ist die Route
**nicht** als vorhanden bestätigt. **Keine fiktiven URLs/Client-IDs
registrieren**, keine `client_credentials` in der Bürgeranmeldung.
Wenn das Portal den Callback bereitstellt, Clientmetadaten durch
`GET /api/v1/auth/clients` prüfen, Client nur mit exakt
abgestimmtem Redirect registrieren und die geschützte End-to-End-
Abnahme einschließlich verweigertem `invalid_scope` ausführen.

## Akzeptanzmatrix

- [x] Native Token-Ausgabe aus `Roles + ExplicitScopes` ist im Quellcode vorhanden
- [x] Admin-PATCH/GET/Rollenmodell existieren bereits; jetzt sichere UI mit nur Lese-Scopes
- [x] Sicherheits-/Token-/Audience-Grenzen werden durch Regressionen verifiziert
- [ ] Neuer Build **produktiv** deployed; aktivierte privilegierte World-Routen geprüft
- [ ] Produktives autorisiertes Konto gezielt geändert, ursprüngliche Scopes und Rollen erhalten
- [ ] Frisch ausgestellter persönlicher World-Token zeigt explizite erwartete Fachscopes
- [ ] Live-Fach-GET mit genau diesem Bearer liefert 200 **und** `protected_details=true`
- [ ] Neuer Stadtportal-Login und `/api/me` zeigen dieselben tatsächlich erteilten Scopes
- [ ] OAuth-Client/PKCE/Callback nur bei produktiver Portalunterstützung abgestimmt und getestet

**Issue #289 erst schließen, wenn die erforderlichen produktiven
Abnahmepunkte belegt sind.**
