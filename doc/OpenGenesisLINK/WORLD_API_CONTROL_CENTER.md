# OpenGenesisLINK – World API Control Center

**Version:** UI-Neugestaltung für OGL 0.9.3.12. Diese Dokumentation beschreibt den OGL-Server, **kein einzelnes Grid**.

## Markenmodell

- **OpenGenesisLINK / OGL:** Server-Software, World API, Control Center, Entwicklungsplan, Infrastruktur, Nodes und Software-Dokumentation.
- **Stadt-NexVerse / NexVerse:** nur Eigenname eines Grids, das OpenGenesisLINK betreibt. Das Admin-Interface darf keinen festen Grid-Namen als Produktname einsetzen.
- Die bestehenden C#-Namespaces `NexVerse.*`, die REST-Routen `/api/v1/*`, Header `X-NexVerse-Api-Key`, Umweltvariablen `NEXVERSE_*`, Datenbanknamen und Protokollfelder bleiben zur **Rückwärtskompatibilität** bestehen. Ein technischer API-Vertragswechsel erfordert eine gesonderte, versionierte Migrationsphase und Client-/Viewer-Tests.

## Browser-Oberfläche

`GET /api/v1/docs` gibt nur die **OGL-Administrator-Anmeldung** aus. Das vollständige Dashboard mit Übersicht, Statistik, Grid-Planer, Simulatoren, Roadmap, API-Explorer und Schlüsselverwaltung liegt unter:

`GET /api/v1/docs/panel`

Dieser zweite Endpunkt liefert **kein Dashboard-HTML**, bevor der Server eine gültige lokale Administrator-Session geprüft hat.

Design: dunkles Glas-/Neon-Layout, cyan-violette Rahmenbeleuchtung, animierter Aurora-Hintergrund, Rastereffekt, schwebende Cards, responsive Sidebar. `prefers-reduced-motion` deaktiviert die Animationen. Keine externen JavaScript-/Font-/Bild-CDNs.

## Authentifizierung (serverseitig)

1. Die Login-Maske sendet über **HTTPS** `POST /api/v1/auth/admin/session` mit `username`, `password`, bei aktiviertem MFA `totp`. Der bereits bestehende OGL-Admin-Login lehnt Nicht-Administratoren **vor** einer Tokenerstellung ab.
2. Für die vollständige Oberfläche sendet der Browser `Authorization: Bearer <token>` an `/api/v1/docs/panel`.
3. Der Server verifiziert das Access-Token kryptografisch und prüft die aktuelle Kontenlage über `NexApiAuthenticator`, nicht anhand von Browserdaten: gültiger lokal eingeloggter GridUser (keine API-Key-/Service-Identität), `LoginAllowed`, `UserLevel >= max(200, AdminMinimumUserLevel)`, `admin:*`, korrekte Principal-ID.
4. Bei jedem neu angefragten Dashboard ist diese Prüfung erneut erforderlich. Token im Browser nur **im Arbeitsspeicher**, keine LocalStorage-/SessionStorage-/Cookie-Persistierung durch die Oberfläche. Der Login-Shell-Frame empfängt Dashboard-HTML erst nach erfolgreichem authentifiziertem Fetch. `Cache-Control: no-store`.
5. Bei 401/fehlendem Rechteumfang wird kein Admin-HTML geliefert. Die API-Zugriffsrechte aller übrigen Endpunkte bleiben unabhängig geschützt, und bestehende Maschinen-API-Keys funktionieren für ihre bisherigen Endpunkte weiter.

**Wichtig:** Der Administrator-Login setzt für die Ausgabe eigener Access-Tokens `[NexVerseWorldApi] EnableNativeTokens = true` mit produktionsgeeignetem Signing-Key und der bestehenden OAuth-/Token-Konfiguration voraus. Diese Konfiguration wird **nicht** automatisch durch die UI aktiviert. Als Reverse-Proxy HTTPS korrekt terminieren und die Anmeldung nicht über unverschlüsseltes HTTP erlauben. Bei einer Reverse-Proxy-Verbindung erkennt die Dashboard-Route `X-Forwarded-Proto: https` ausschließlich von einem lokalen Loopback-Peer an.

Beispiel **nur für das Berechtigungsniveau**:

```ini
[NexVerseWorldApi]
AdminMinimumUserLevel = 200
```

Der Zugangslevel ist eine **Untergrenze** und kann mit einem höheren bereits eingestellten Wert nicht abgesenkt werden.

## Manuelle Sicherheits-/Browser-Abnahme

- Unangemeldet `GET /api/v1/docs`: Login-Formular sichtbar, Admin-Oberfläche noch nicht heruntergeladen.
- `GET /api/v1/docs/panel` ohne Bearer und mit ungültigem Bearer: **401, kein HTML**.
- GridUser Level 0 / 100: Anmeldung **403**; kein Admin-Token.
- GridUser Level 200 mit `admin:*`, korrektem Passwort und gegebenenfalls TOTP: Dashboard zugänglich.
- Level 200 ohne effektives `admin:*`, gesperrter User oder nachträglich gesenkter UserLevel: Zugang verweigert.
- Maschinen-API-Key, Servicetoken, gefälschtes JWT und abgelaufenes/revoziertes JWT: Zugang verweigert.
- Nach Abmelden: In-Memory-Token gelöscht, Dashboard-Frame entfernt; erneute Anmeldung erforderlich.
- Chrome/Firefox, Mobilgerät, Fokus-/Tastaturführung und reduzierte Animationen prüfen.

Automatisierte Regression unter `tools/ci/verify_ogl_docs_admin_ui.py` und anonymer Negativtest im World-API-GitHub-CI-Smoke. Die erfolgreiche statische/CI-Prüfung ersetzt keinen vollständig integrierten Login mit realem Benutzerkonto.

## Weitere OGL-Branding-Migration

Die Oberfläche verwendet als Produktbezeichnung OpenGenesisLINK. Zukünftig sichtbare Betreiber-/Grid-Namen sollen per GridInfo/API dynamisch geliefert und **nicht** als Produktlabel fest verdrahtet werden. Eine spätere Entkopplung interner `NexVerse`-Namespaces sowie Legacy-Header-/Env-Namen erfolgt ausschließlich mit Migrationsplan und Rückwärtskompatibilität für Firestorm, Viewer, Plugins und APIs.
