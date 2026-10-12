# OpenGenesisLINK Viewer – verbindliche Entwicklungsübergabe (Living Contract)

> **Dieses Dokument ist ausschließlich für die Entwicklung des eigenen OGL Viewers bestimmt.**
> Es ist der zentrale, fortlaufend zu pflegende Vertrag zwischen **OpenGenesisLINK Server**
> und dem **eigenen OpenGenesisLINK Viewer**. Andere Viewer wie Firestorm, Alchemy,
> Cool VL oder der offizielle SL-Viewer müssen weiterhin unverändert mit dem
> vorhandenen OpenSim/Second-Life-Protokoll arbeiten.
>
> **Referenz:** Server-Repository `opengenesislink/opengenesislinkserver`,
> `main`, `doc/NexVerse/OGL_VIEWER_DEVELOPMENT.md`.
> **Zuletzt aktualisiert:** 12.10.2026.
> **Aktuelle veröffentlichte Server-Stable:** `v0.9.3.10`.
> **Aktive Entwicklung:** `0.9.3.11/0.9.3.12`; noch kein `0.9.3.12 Stable`.
> **Viewer-Fork:** separat gepflegt; Änderungen hier sind kein Viewer-Build.

## Anweisung für den anderen OGL-Viewer-Chat

**Lies vor jeder Änderung an Login, Netzwerk, CAPS, Grafik, Inventar oder
Viewer-Funktionen zuerst diese Datei und danach die jeweils verlinkten
technischen Spezifikationen.** Stelle den Viewer nicht allein nach
vermuteten OpenSim-/Firestorm-Standards um.

Arbeite **funktionsweise**: Implementierung im Viewer, Integrationstest
mit tatsächlich laufendem OpenGenesisLINK Server, und Dokumentation
des getesteten Server-/Viewer-Commits. Wenn eine erforderliche Server-API
noch nicht existiert oder deaktiviert ist, **nicht fälschlich behaupten,
sie würde bereits funktionieren**. Stattdessen den Server-Change
konkret benennen und dessen Implementierung anfordern.

### Laufend zu pflegende Regeln

1. **Jede neue OGL-spezifische Server-/Viewer-Funktion** erhält in
   dieser Datei einen eigenen Eintrag mit API, JSON-Beispiel,
   Versions-/Feature-Flag, Authentifizierung, Fehlerantworten,
   Fallback, offenen Viewer-Aufgaben und nachvollziehbaren Tests.
2. Der Server-PR für eine neue OGL-Viewer-Capability aktualisiert
   **diese** Datei **zusammen mit** Servercode, JSON-Discovery, Tests
   und ggf. INI. Viewer-PRs ergänzen hier den Integrationsstatus,
   sobald der Viewer-Commit verfügbar ist.
3. Verwende die folgenden Status strikt:
   `RELEASED` (in veröffentlichtem Stable-Tag),
   `MERGED` (nur Server-`main`, noch nicht Stable),
   `DEVELOPMENT` (offener Branch/PR),
   `PLANNED` (Spezifikation ohne Implementierung),
   `PAUSED` (ausgesetzt).
4. **Server-`main` ist nicht automatisch die öffentlich installierte
   Grid-Version.** Live-URLs funktionieren erst nach Betreiber-Deployment
   und Neustart. Ein offener PR ist keine produktive Funktion.
5. Keine Features als aktiv markieren, nur weil Schnittstellen, Typen
   oder Stub-Methoden im C#-/Viewer-Code existieren.
6. Änderungen an Protokollfeldern möglichst additiv und versioniert
   (`/ogl/v1/...`); Breaking Changes benötigen `v2`, explizite
   Negotiation und Migrationsplan. Veraltete JSON-Keys nicht
   ohne eigenen Kompatibilitätstest entfernen.
7. Negative Rechte-/Login-Tests, ältere Viewer und Firestorm-Fallback
   sind bei jedem Login-/CAPS-Change zwingend mitzudenken.

## A. Implementierter gemeinsamer Netzwerkeinstieg

### A1. OGL GridInfo v1 (Server: MERGED, Viewer: zu implementieren)

Öffentlicher Grid-/Viewer-Bootstrap **auf demselben Robust-HTTP-Listener
wie die bestehenden Legacy-Services**, bei NexVerse üblicherweise
`PublicPort=80`:

```http
GET /ogl/v1/grid-info HTTP/1.1
Host: hg.stadt-nexverse.de
Accept: application/json
```

**Schema-Kennung:** `org.opengenesislink.viewer.grid-info.v1`.
**Servercode:** `OpenSim/Server/Handlers/Grid/GridInfoHandlers.cs`;
Registration: `GridInfoServerInConnector.cs`.
**Detaillierte Spezifikation:** [OGL_VIEWER_DISCOVERY.md](OGL_VIEWER_DISCOVERY.md).

Antwort (gekürzt, tatsächliche Werte kommen aus der GridInfo-Konfiguration):

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

Das Beispiel ist ein **Schema-Beispiel**, kein bestätigter Live-Aufruf.
Discovery ist öffentlich, benötigt keinen Viewer-Schlüssel und
ist **kein** Login. JSON-HTTP-Fehler: `405` bei anderer HTTP-Methode.

**Konkrete Viewer-Arbeit:**
- Beim Hinzufügen/Auswählen eines Grids zunächst
  `GET /ogl/v1/grid-info` versuchen; JSON Schema/Version validieren.
- Nur vorhandene, typisierte `endpoints` und `features`
  verwenden. Keine neuen APIs aus Produktnamen oder URL vermuten.
- Falls `404`, inkompatibles Schema oder kein OGL-Server:
  **auf `/get_grid_info` bzw. den bisherigen Firestorm-GridManager
  und Legacy-Login zurückfallen**.
- Für `features.ogl_native_login=false` ausschließlich Legacy-
  Login verwenden; `ogl_login=null` niemals aufrufen.
- Kein Login-Token, Passwort oder Session-Secret in Discovery,
  URL-Query, Analytics oder Logs ablegen.
- Bei `grid.home_uri` und `endpoints.legacy_login` bewusst
  zwischen öffentlichen HTTP-Discovery-URLs und für Passwörter
  geeigneten **HTTPS**-URLs unterscheiden.

### A2. Bestehender Standard-Login (Server: RELEASED)

Firestorm, der alte Viewer-Modus und fremde SL-/OpenSim-Viewer nutzen
weiterhin die bisherigen:
- XML-RPC `login_to_simulator` am vorhandenen Robust-Listener,
- bestehenden LLSD-Login und bestehende Simulator-CAPS,
- `GET /get_grid_info` und `GET /json_grid_info`.

**Es wird nichts per `User-Agent`, Viewer-Name, Channel oder
Client-IP „automatisch“ auf OGL umgeleitet.** Der OGL Viewer wählt
die API nach korrekt ausgehandelter Discovery aktiv selbst.

### A3. Nativer OGL JSON-Login (Server: DEVELOPMENT, Viewer: PLANNED)

**Implementierter Entwicklungs-Pfad:** `POST /ogl/v1/login` auf **demselben Robust-Dienst/
PublicPort**. Der Server bietet einen JSON-Adapter zur bestehenden
`ILoginService`-Benutzer-/Session-/CAPS-Pipeline.
**Standard in den ausgelieferten `Robust.HG.ini*`-Vorlagen: `[OglViewerLogin] Enabled=false`.**
Die Discovery bewirbt ihn nur bei `Enabled=true` **und**
konfigurierter gültiger externer `PublicLoginUrl` mit
**HTTPS** und dem exakten Pfad `/ogl/v1/login`.
Solange `ogl_native_login=false` oder `endpoints.ogl_login=null`
ist, bleibt er aus Sicht des Viewers **nicht verwendbar**.

**Aktueller Entwicklungsvertrag v1** (die folgenden Beispiele sind
noch **nicht live** im NexVerse-Grid abgenommen):

```http
POST https://hg.stadt-nexverse.de/ogl/v1/login
Content-Type: application/json
Accept: application/json
```

```json
{
  "protocol_version": 1,
  "first": "Beispiel",
  "last": "Resident",
  "passwd": "<nur über TLS gesendetes Konto-Passwort>",
  "start": "last",
  "scope_id": "00000000-0000-0000-0000-000000000000",
  "version": "OpenGenesisLINK Viewer Dev",
  "channel": "OpenGenesisLINK Viewer",
  "mac": "Unknown",
  "id0": "Unknown"
}
```

`first`, `last`, `passwd` und `protocol_version=1` sind
Pflichtfelder; alle anderen sind optional. Der JSON-Body ist
auf **8192 Byte** begrenzt. Der Operator begrenzt Versuche über
`[OglViewerLogin] MaxAttemptsPerMinute` (Standard 5 pro Minute und
Remote-IP; nicht mit dem Legacy-DOS-Schutz verwechseln). Der Viewer darf `passwd`, Sitzungs-IDs
und CAPS-Tokens nicht in Klartextlogs aufnehmen. Die bisherige
OpenSim-Loginlogik entscheidet weiterhin über gesperrte Benutzer,
Passwortgültigkeit, Regionszuweisung und die Session.

**Erfolgsantwort** (vereinfacht; tatsächliche Felder entsprechen
dem OpenSim-`LoginResponse.ToOSDMap()`):

```json
{
  "protocol_version": 1,
  "login_response": {
    "login": "true",
    "agent_id": "<UUID>",
    "session_id": "<UUID>",
    "secure_session_id": "<UUID>",
    "seed_capability": "<URL>",
    "sim_ip": "<SIM-IP>",
    "sim_port": 9000
  }
}
```

Die vollständige `login_response` umfasst zusätzlich Appearance-,
Inventory-, Region- und Login-Felder des bestehenden Servers.
**Nicht** als gespeicherten User-Token über Sitzungen hinweg behandeln.
HTTP-Fehler bestehen aus `protocol_version`, `error`, `message`;
wichtige Fälle: `404 not_available` (Feature aus), `405 method_not_allowed`,
`426 tls_required`, `415 unsupported_media_type`,
`413 request_too_large`, `400 invalid_json/unsupported_protocol/invalid_request`,
`401 login_failed`, `429 rate_limited`,
`503 login_unavailable`. Die Fehlermeldungen geben weder Kontodetails
noch Original-Passwort preis.

**TLS-Termination:** Direkte HTTPS-Verbindung
(`request.IsSecured`) **oder** fest eingetragener, kontrollierter
Reverse-Proxy (`TrustedTlsProxyAddress`, exakte Remote-IP
**und** `X-Forwarded-Proto: https`). Der Proxy muss
über eine nicht öffentlich zugängliche Backend-Verbindung
mit Robust kommunizieren. `User-Agent`, `X-Forwarded-For`
und ein ungesichertes `X-Forwarded-Proto`-Header alleine
beweisen nichts. Ohne TLS wird **426** ausgeliefert;
ein Passwort darf dabei nicht verarbeitet werden.

**Release-Status:** DEV-Branch; produktive Aktivierung,
positiver Login mit echtem Avatar und vollständige
CAPS-/Logout-/Teleport-/Relogin-Abnahme **noch offen**.

**Verbindlicher Rahmen:**
- JSON über **HTTPS**, niemals ein Passwort/Hash/Web-Login-Key
  über unverschlüsseltes HTTP; TLS kann an einem autorisierten,
  vertrauenswürdigen Reverse-Proxy auf 443 terminieren, der
  denselben internen Robust-Listener auf 80 anspricht.
- Bestehende OpenGenesisLINK-`ILoginService`, Account-/Userlevel-
  Sperren, Sitzungs-/Region-Platzierung und CAPS wiederverwenden.
  **Keine zweite Avatar-/Passwort-/Inventar-Datenbank.**
- Separate versionierte JSON-Ein-/Ausgabe, Session- und
  Capability-Negotiation, Größenlimits, Anmelde-Limits, generische
  Fehlermeldungen, kein Passwort in Logs und keine offenen Redirects.
- `User-Agent`, ausführbare Viewer-Binärdatei oder
  `channel=OGL` sind **kein kryptographischer Beleg** für einen
  exklusiven Viewer. Authentisierung des **Benutzers** ist
  Voraussetzung; echte Geräte-/Build-Attestierung wäre ein
  separates, explizit zu entwerfendes Verfahren.
- Die finale JSON-Anfrage-/Antwortstruktur erst als **IMPLEMENTED**
  markieren, wenn im PR mit Runtime-/Negativtests bestätigt.
- **Viewer-Aufgabe:** Native-Login-Adapter hinter Feature-Flag,
  TLS-Fehler streng behandeln; Sessions/CAPS wie im bestehenden
  Viewer leben lassen; nie stillschweigend auf unsicheren
  HTTP-Login zurückfallen.

## B. Neuer Viewer – verschobene Funktionsfamilien

| Funktion | Serverstand | Viewerstand | Zeitpunkt / Vertrag |
|---|---|---|---|
| OGL GridInfo JSON v1 | MERGED | PLANNED | Jetzt integrieren; siehe A1 |
| OGL JSON Native Login | DEVELOPMENT | PLANNED | Erst nach Security-/CI-/Live-Abnahme; A3 |
| OGL LSL-Parität 0.9.3.11 | MERGED (Grundlage) | Nicht erforderlich | Funktionsinventar und Tests, keine Vollabnahme |
| OGL LSL-Profiler 0.9.3.12 | MERGED (interner Snapshot) | PLANNED | Noch keine öffentliche Admin-/Viewer-API |
| OGL Experiences 2.0 | PAUSED/PLANNED | PLANNED | **Nach** 0.9.4.0 Generation 1 |
| OGL Pathfinding 2.0 | PLANNED | PLANNED | **Nach** 0.9.4.0 Generation 1 |
| OGL My Outfits 2.0 | PLANNED | PLANNED | **Nach** 0.9.4.0 Generation 1 |
| OGLVoice, eigener Renderer, Raytracing | PLANNED | PLANNED | Eigene spätere Tracks, keine aktuellen API-Zusagen |

**Wichtig bei Experiences:** Die unter OpenSimulator/Firestorm
existierende alte Experience-Implementierung bleibt im
ausgelieferten Profil deaktiviert. Keine Erfolgsmeldungen
zum Experience-Consent-/LSL-/K/V-Workflow aus alten Stubs.

**Wichtig bei Pathfinding:** Keine fiktive Havok-/
Firestorm-NavMesh-Kompatibilität und keine behauptete aktuelle
Navigation-CAPS. Der spätere Pfad muss serverautoritative
Simulation und eigene Viewer-Editierbarkeit spezifizieren.

**Wichtig bei My Outfits:** Core-Inventar/Assets werden weiter
unterstützt. Die eigene Outfit-Galerie, COF-/Cache-/
Appearance-Kompatibilität benötigen eigene Viewer-Tests.

## C. Freigabetests, die der andere Chat durchführen soll

Für jeden neu implementierten Viewer-Netzwerkpfad:
1. GridInfo-v1-Daten einmal über echte NexVerse-URL und einmal
   über CI-/Test-Grid abrufen; HTTP/HTTPS und Port prüfen.
2. Alte Firestorm-`login_to_simulator`-Anmeldung einschließlich
   negativer Zugangsdaten unverändert testen.
3. OGL-Feature an/aus, `404`, `405`, falsches JSON,
   Timeout und ältere Serverversion abdecken.
4. Benutzer authentifizieren, Session/CAPS/Teleport/Region-
   Wechsel nur bei tatsächlich implementierter native Route
   testen; kein Login bei fehlender TLS-Sicherung.
5. Mit dem jeweiligen Viewer-Commit, Server-SHA,
   Datum, Test-Grid, beobachteter Antwort und Fehlercode
   dokumentieren.

## D. Pflege- und Übergabeprotokoll

| Datum | Server-Referenz | Änderung | Viewer-Auftrag |
|---|---|---|---|
| 12.10.2026 | PR #186, `main` | Separate versionierte OGL GridInfo JSON v1 auf dem Robust-PublicPort, Legacy-Login unverändert | JSON-Discovery + Fallback im Viewer bauen |
| 12.10.2026 | PR #183, `main` | LSL-Matrix und interner YEngine-Profiling-Snapshot | Noch kein LSL-Profiler-Viewer-UI ohne sichere API |
| 12.10.2026 | Dieser Entwicklungszweig | Eigenständiger nativer JSON-Login in Vorbereitung | Bis zur aktivierten, getesteten Route weiterhin Legacy-Login |

**Bei jeder folgenden OGL-spezifischen Änderung genau diese Tabelle
erweitern**, sowie den betreffenden Abschnitt und den
Implementierungs-/Teststatus aktualisieren. Keine bereits vorhandenen
Verträge stillschweigend überschreiben; ältere Verträge bleiben
in GitHub historisch nachvollziehbar.

Weitere Dokumente:
- [OGL_VIEWER_DISCOVERY.md](OGL_VIEWER_DISCOVERY.md) –
  detaillierte GridInfo-v1-Spezifikation
- [FUTURE_VIEWER_NAVIGATION.md](FUTURE_VIEWER_NAVIGATION.md) –
  spätere Navigation-/Pathfinding-Architektur
- [LSL_PARITY_09311_09312.md](LSL_PARITY_09311_09312.md) –
  LSL-Verifikationsstand
- [ROADMAP.md](ROADMAP.md) –
  verbindlicher Versions- und Scope-Plan
