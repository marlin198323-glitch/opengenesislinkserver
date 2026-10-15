# OpenGenesisLINK – World API Control Center

**Version:** UI-Neugestaltung für OGL 0.9.3.12. Diese Dokumentation beschreibt den OGL-Server, **kein einzelnes Grid**.

## Markenmodell

- **OpenGenesisLINK / OGL:** Server-Software, World API, Control Center, Entwicklungsplan, Infrastruktur, Nodes und Software-Dokumentation.
- **Stadt-NexVerse / NexVerse:** nur Eigenname eines Grids, das OpenGenesisLINK betreibt. Das Admin-Interface darf keinen festen Grid-Namen als Produktname einsetzen.
- Die bestehenden C#-Namespaces `NexVerse.*`, die REST-Routen `/api/v1/*`, Header `X-NexVerse-Api-Key`, Umweltvariablen `NEXVERSE_*`, Datenbanknamen und Protokollfelder bleiben zur **Rückwärtskompatibilität** bestehen. Ein technischer API-Vertragswechsel erfordert eine gesonderte, versionierte Migrationsphase und Client-/Viewer-Tests.

## Browser-Oberfläche / einfache Anmeldung

Die Startadresse für Stadt-NexVerse lautet
`https://world.stadt-nexverse.de/api/v1/docs`.

Der Browser verwendet für **den gesamten Login und das Laden des
Control Centers denselben Pfad**:

1. `GET /api/v1/docs`: Anmeldeformular mit Grid-Benutzername (z. B.
   `Sleimer.Akina`) und Passwort anzeigen.
2. `POST /api/v1/docs`: dieselben zwei Eingaben serverseitig im
   bestehenden GridUser-/AuthenticationService prüfen, danach
   `UserLevel >= 200` und freigegebenen Account bestätigen.
   Sofern dieses Konto aktiv TOTP nutzt, wird auch dessen bestehende
   Sicherheitsprüfung eingehalten.
3. Bei gültigen Daten wird intern ein signiertes, zeitlich begrenztes
   Session-Token erstellt. Die UI zeigt keine technische Token-Eingabe.
4. `GET /api/v1/docs` mit `Authorization: Bearer ...`: vollständiges
   Control Center, nur nach erneuter Prüfung des lokalen Accounts.
5. Logout entfernt das Token und den eingeblendeten Admin-Frame.

**Kompatibilität:** `GET /api/v1/docs/panel`,
`POST /api/v1/auth/session` und
`POST /api/v1/auth/admin/session` bleiben für vorhandene Clients
weiterhin verfügbar. Die Web-Anmeldeseite benötigt diese zusätzlichen
Pfade jedoch nicht mehr.

**Sicherheit:** Unangemeldete Besucher bekommen niemals das
geschützte Dashboard. Weder Passwörter noch signierte Tokens werden
in HTML gespeichert oder in URL-Parametern übertragen. Der Browser
bewahrt sein Token ausschließlich im Arbeitsspeicher auf. Anfragen
werden per HTTPS und mit `Cache-Control: no-store` ausgeführt.
Geschützte Daten bleiben an `LoginAllowed`, den lokalen
`UserLevel >= 200`, die Principal-ID und die serverseitig
berechneten Administratorscopes gebunden.

### KeyHelp Reverse Proxy

Für `world.stadt-nexverse.de` muss KeyHelp HTTPS für die Domain
terminieren und **alle Methoden** (GET/POST) sowie den unveränderten
Pfad `/api/v1/docs` an den laufenden ROBUST-HTTP-Port weiterreichen.
Insbesondere den `Authorization`-Header bei GET nicht unterdrücken.
Für die restlichen Dashboard-Funktionen müssen auch `/api/v1/*`
weitergeleitet werden. Die genutzte ROBUST-Portnummer hängt von der
vorhandenen Konfiguration ab; hier wird keine neue Portnummer vorgegeben.

In der ROBUST-Konfiguration:

~~~ini
[NexVerseWorldApi]
Enabled = true
EnablePrivilegedEndpoints = true
AdminMinimumUserLevel = 200
EnableNativeTokens = true
PublicBaseUrl = "https://world.stadt-nexverse.de"
NativeTokenIssuer = "https://world.stadt-nexverse.de"
~~~

Außerdem muss `NativeTokenSigningKey` auf das bestehende geschützte
Secret (mindestens 32 UTF-8-Bytes) verweisen. Keinen Schlüssel in
GitHub oder Chat kopieren. Die bereits ausgelieferte
`bin/Robust.HG.ini` enthält die betreffenden Einstellungen.

**Prüfung ohne echte Zugangsdaten:**

~~~bash
curl -si https://world.stadt-nexverse.de/api/v1/docs
curl -si -H 'Content-Type: application/json' \
  --data '{"username":"ungueltig","password":"ungueltig"}' \
  https://world.stadt-nexverse.de/api/v1/docs
~~~

Erwartet: die erste Anfrage zeigt das Formular (`HTTP 200`);
die zweite darf bei aktivem Admin-Backend **niemals HTTP 200** liefern.
Sie muss das falsche Passwort mit `HTTP 401` und JSON ablehnen.
Bei inaktivem Admin-Backend wird `HTTP 503` zurückgegeben.
Ein `HTTP 200` mit Login-HTML oder fremdem JSON bei POST bedeutet,
dass die Domain noch an eine falsche/stale Anwendung oder KeyHelp-Regel
gebunden ist. In diesem Fall lässt sich der Fehler **nicht allein durch
Code auf GitHub** korrigieren.

## Manuelle Sicherheits-/Browser-Abnahme

- Unangemeldet `GET /api/v1/docs`: Login-Formular sichtbar, Admin-Oberfläche noch nicht heruntergeladen.
- `GET /api/v1/docs/panel` ohne Bearer und mit ungültigem Bearer: **401, kein HTML**.
- `GET /api/v1/docs` mit ungültigem Bearer: **401 oder 403, niemals Dashboard-HTML**.
- `POST /api/v1/docs` mit falschem Passwort: **401 als JSON**, nicht 200.
- GridUser Level 0 / 100: Anmeldung **403**; kein Admin-Token.
- GridUser Level 200 mit `admin:*`, korrektem Passwort und gegebenenfalls TOTP: Dashboard zugänglich.
- Level 200 ohne effektives `admin:*`, gesperrter User oder nachträglich gesenkter UserLevel: Zugang verweigert.
- Maschinen-API-Key, Servicetoken, gefälschtes JWT und abgelaufenes/revoziertes JWT: Zugang verweigert.
- Nach Abmelden: In-Memory-Token gelöscht, Dashboard-Frame entfernt; erneute Anmeldung erforderlich.
- Chrome/Firefox, Mobilgerät, Fokus-/Tastaturführung und reduzierte Animationen prüfen.

Automatisierte Regression unter `tools/ci/verify_ogl_docs_admin_ui.py` und anonymer Negativtest im World-API-GitHub-CI-Smoke. Die erfolgreiche statische/CI-Prüfung ersetzt keinen vollständig integrierten Login mit realem Benutzerkonto.

## Weitere OGL-Branding-Migration

Die Oberfläche verwendet als Produktbezeichnung OpenGenesisLINK. Zukünftig sichtbare Betreiber-/Grid-Namen sollen per GridInfo/API dynamisch geliefert und **nicht** als Produktlabel fest verdrahtet werden. Eine spätere Entkopplung interner `NexVerse`-Namespaces sowie Legacy-Header-/Env-Namen erfolgt ausschließlich mit Migrationsplan und Rückwärtskompatibilität für Firestorm, Viewer, Plugins und APIs.
