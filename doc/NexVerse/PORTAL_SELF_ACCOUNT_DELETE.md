# Stadt NexVerse Bürgerportal – Konto & Sicherheit: eigenes Avatar-Konto löschen

Stand: 21.10.2026 · World API auf ROBUST. Die Implementierung stellt die
serverseitige Schnittstelle bereit; die eigentliche Stadtportal-Weboberfläche
befindet sich in einem separaten Projekt.

## Endpunkt

```http
POST /api/v1/users/me/delete
Authorization: Bearer <VERIFIED_RESIDENT_ACCESS_TOKEN>
Content-Type: application/json
```

```json
{
  "password": "AKTUELLES_AVATAR_PASSWORT",
  "confirmation": "KONTO LÖSCHEN"
}
```

**Es werden weder Vorname, Nachname noch Avatar-UUID uebermittelt.**
ROBUST ermittelt die eigene UUID aus dem signierten World-API-Bearer-Token
und prueft, dass ein lokal registrierter aktiver Bewohner dahintersteht.
Ein fremder Avatar, ein Service-Principal, ein API-Key oder ein Administrator
kann diesen Selbstloeschungsendpunkt nicht nutzen. Ein normaler Portal-Cookie
allein ist **kein** World-API-Bearer-Token: Das Stadtportal-Backend muss
den bereits geprueften Bewohner-Token der angemeldeten Session besitzen.
Nicht auf Basis von Browser-Formularfeldern eine UUID nachtragen oder den
Maschinenschluessel fuer Avatar-Registrierung verwenden.

Vor der Loeschung werden eine vertrauenswuerdige HTTPS-Verbindung
(direkt oder TLS-Proxy mit erlaubter Quell-IP), die Passwort-Neupruefung
und die exakte Bestaetigung `KONTO LÖSCHEN` erzwungen.
Keine Passwoerter oder Tokens loggen. Dem Browser keinen
`X-NexVerse-Api-Key` ausliefern.

### ROBUST-Konfiguration

```ini
[NexVerseWorldApi]
EnableNativeTokens = true
PortalAccountSelfDeleteEnabled = true
PortalRegistrationTrustedProxyIPs = "109.91.20.9"
```

Die benoetigte Proxy-IP entspricht der tatsaechlichen TCP-Quelladresse
des KeyHelp-Proxys. Der HTTPS-VHost muss einen sauberen
`X-Forwarded-Proto: https` Header setzen. Die Funktion ist in der
NexVerse-Produktions-Konfiguration aktiviert; Beispiel-INI-Dateien
bleiben konservativ deaktiviert.

### Erfolg – HTTP 200

```json
{
  "success": true,
  "account_deleted": true,
  "deletion_scope": "core_identity_only",
  "logout_required": true,
  "remaining_external_data_may_exist": true,
  "correlation_id": "..."
}
```

Bei fehlender Authentifizierung `401`, bei fehlendem Passwort,
falscher Bestaetigung oder Formularidentitaeten `400`, bei
falschem Passwort `403`, bei noch online befindlichem Avatar
`409 avatar_online`, bei nicht verfuegbarem Backend oder
getrennten Datenbanken `503`. Bei jedem Fehler bleiben die
Portal-Session und das Konto erhalten.

## Portal-UI: Bürgerportal → Konto & Sicherheit

Am Ende von „Konto & Sicherheit“ einen deutlich abgegrenzten Bereich
**Konto löschen** mit Button **„Mein Avatar-Konto endgültig löschen“**
vorsehen. Der erste Klick zeigt einen zweiten Dialog mit dem automatisch
aus der lokalen, validierten Sitzung geholten Anzeigenamen und dem
Hinweis auf Datenverlust. Der Bewohner muss das aktuelle Passwort
eingeben und exakt `KONTO LÖSCHEN` eintippen. Den finalen API-Request
ausschliesslich serverseitig aus dem Portal-Backend absenden.

### Server-Backend-Ablauf (Pseudocode)

```text
POST /buergerportal/konto-sicherheit/konto-loeschen
  -> bestehende Portal-Session + CSRF-Token pruefen
  -> World-API-Bearer-Token aus serverseitiger Session holen
  -> JSON password + confirmation an POST /api/v1/users/me/delete
  -> bei 200 und account_deleted=true:
       Browser- und serverseitige Portal-Sitzung invalidieren
       Sessions/Refresh-Tokens und Cookies loeschen
       Antwort redirect=/login?account_deleted=1
  -> bei jedem Fehler:
       Dialog mit begrenzter Fehlermeldung anzeigen, Session beibehalten
```

Die Weiterleitung aus der Webseite muss vom **Portal selbst**
implementiert werden: Die World API kontrolliert keine Portal-Cookies
und kann Besucher daher nicht ohne Portal-Anpassung ausloggen.

Wegen der hohen Tragweite mindestens eine serverseitige
Rate-Limit-/Abuse-Kontrolle, CSRF-Schutz und sichere Session-Cookies
verwenden. Nach dem finalen POST **kein automatisches Retry** ausfuehren.

## Loeschumfang und offene Punkte

Die Implementierung entfernt das zentrale lokale Avatar-Kernkonto
einschliesslich Login-Credentials/Tokens, zentralem Inventar,
Avatar-Informationen, GridUser-/Presence- und Friends-Eintraegen in
einer gemeinsamen Datenbanktransaktion. Der Bewohner darf dabei nicht
im Grid online sein.

**Noch nicht geloescht:** NV$-Transaktionen, Gruppen, Land,
gerezzte Objekte, Profile, Experiences, Marketplace sowie
Daten fremder Addons oder externe Portal-Systeme. Es handelt
sich deshalb noch **nicht** um die vollstaendige Loeschung aller
personenbezogenen Grid-Daten. Der Portaltext muss diese Grenze
klar benennen und gegebenenfalls ein separates vollstaendiges
Datenloeschverfahren vorsehen. Keine gemeinsam verwendeten
Asset-Dateien ungeprueft vernichten.

Tests: unautorisierte Anfrage, Admin-/API-Key, fremde UUID-Felder,
fehlender Bestaetigungstext, falsches Passwort, online Avatar,
fehlende TLS-Proxy-Vertrauensstellung, getrennter DB-Dienst,
korrekt authentifizierter Offline-Avatar. Fuer den Livebetrieb
zuvor Sicherungen der gesamten Backend- und Portaldaten erstellen.
