# OGL Viewer – separate JSON-Grid-Discovery v1

**Status:** erster serverseitiger Protokollbaustein für OpenGenesisLINK
0.9.3.11/0.9.3.12, **kein eigener OGL-Login** und kein neuer Stable-Release.

## Endpunkt und Kompatibilität

Die bereits bestehende öffentliche Robust-Instanz registriert mit dem
`GridInfoServerInConnector` zusätzlich:

```http
GET http://hg.stadt-nexverse.de/ogl/v1/grid-info
Accept: application/json
```

Die tatsächliche öffentliche Adresse und das Protokoll richten sich
nach der Robust-/Reverse-Proxy-Konfiguration. Es wird **kein zweiter
TCP-Listener** benötigt: bei NexVerse ist das der vorhandene
Robust-PublicPort **80**. Hinter einem TLS-Proxy ist derselbe Pfad
über **443/HTTPS** abrufbar.

Unverändert bleiben:
- `GET /get_grid_info` – klassische OpenSim-GridInfo (XML),
- `GET /json_grid_info` – bestehende OpenSim-/OSSL-GridInfo (JSON),
- `login_to_simulator` – bestehender Firestorm-/SL-kompatibler
  XML-RPC-Login am bisherigen Robust-Listener,
- der bisherige LLSD-Login sowie alle bestehenden CAPS,
  Simulator- und Hypergrid-Wege.

Ein OGL Viewer kann zuerst den neuen Endpunkt abrufen und bei
älteren/externen Grids den **klassischen Login/Discovery-Weg** nutzen.
Firestorm benötigt **keine** Änderung und wird nicht anhand
`User-Agent`, Channel-String oder IP-Adresse umgeleitet.

## Beispiel (vereinfacht)

```json
{
  "schema": "org.opengenesislink.viewer.grid-info.v1",
  "protocol_version": 1,
  "server": {
    "product": "OpenGenesisLINK",
    "version": "OpenGenesisLINK 0.9.3.10"
  },
  "grid": {
    "name": "NexVerse",
    "nick": "NexVerse",
    "home_uri": "http://hg.stadt-nexverse.de/",
    "gatekeeper_uri": "http://hg.stadt-nexverse.de/",
    "website": "https://stadt-nexverse.de/"
  },
  "endpoints": {
    "legacy_login": "http://hg.stadt-nexverse.de/",
    "legacy_grid_info": "/get_grid_info",
    "legacy_grid_info_json": "/json_grid_info",
    "ogl_grid_info": "/ogl/v1/grid-info",
    "ogl_login": null
  },
  "features": {
    "legacy_login": true,
    "ogl_grid_info_v1": true,
    "ogl_native_login": false,
    "ogl_experiences_2": false,
    "ogl_pathfinding_2": false,
    "ogl_my_outfits_2": false
  },
  "security": {
    "discovery_is_public": true,
    "tls_required_for_credentials": true,
    "viewer_identity_verified_by_discovery": false
  }
}
```

**Wichtig:** Beispielwerte für `grid` sind illustrativ.
Nur explizit freigegebene Informationen aus
`[GridInfoService]` werden ausgegeben; **niemals** sämtliche
Konfigurationsfelder. Ungültige oder mit eingebetteten
Zugangsdaten versehene URLs werden ausgelassen (`null`).
`POST` oder andere Methoden werden mit **405** abgewiesen.

## Native OGL-Login-Architektur – nächste Entwicklungsstufe

Eine spätere, getrennte `POST /ogl/v1/login`-Route darf erst als
`ogl_native_login: true` beworben werden, wenn sie tatsächlich
serverseitig registriert und Ende-zu-Ende getestet wurde.

Sie soll die **bestehende** `ILoginService`-Authentifizierung,
Sperren und Rechte, Region-Placement, Caps-Erzeugung und
Sessions wiederverwenden. Keine zweite Benutzerkonten- oder
Passwortdatenbank, keine separaten Avataridentitäten.

Ein öffentlich vertriebener Viewer kann nicht allein durch
seinen `User-Agent` oder seine Versionsmeldung kryptographisch
authentifiziert werden. Der native Zugang benötigt eine eigene
Version-/Capability-Aushandlung und authentisierte
Benutzersessions; ein echter Nachweis der Viewerinstallation
wäre nur mit eigenem vertrauenswürdigem Geräte-/Build-Nachweis
und expliziter Schlüsselverwaltung denkbar. Ebenso müssen
Login-Rate-Limits, fehlgeschlagene Zugangsdaten, Anti-Replay
und Weiterleitung/Proxy-Konfiguration vor Produktivbetrieb
geprüft werden.

**Passwörter/Tokens niemals über unverschlüsseltes HTTP
auf Port 80 übertragen.** Der Port 80 bleibt für öffentliche
Discovery/Legacy-Kompatibilität verfügbar; der zukünftige
OGL-Login erfordert TLS (z. B. HTTPS auf Port 443 oder
gesicherter Frontend-Reverse-Proxy). Bei einem Grid ohne TLS
muss der OGL Viewer die native Anmeldung verweigern statt
stillschweigend Zugangsdaten zu übertragen.

## CI/Regression

Die CI startet denselben Robust-Testprozess mit beiden
Konnektoren auf Port 19092 und prüft:
- echten `login_to_simulator`-XML-RPC-Login-Fehler für einen
  nicht existierenden Firestorm-Testbenutzer,
- JSON v1, öffentliche Felder, Version und abgeschaltete
  zukünftige OGL-Features,
- existierende XML/JSON GridInfo-Routen,
- `POST` → 405 und keine Ausgabe eines absichtlich
  in die Testkonfiguration aufgenommenen Geheimfelds.

Das ersetzt **keinen echten Login-/CAPS-/Viewer-Integrationstest**.
