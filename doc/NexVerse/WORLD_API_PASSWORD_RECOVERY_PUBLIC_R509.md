# World-Passwortwiederherstellung ohne Portal-API-Schlüssel (R509)

Stand: 25.10.2026 – **neuer optionaler World-API-Vertrag, noch nicht produktiv bereitgestellt**.

## Hintergrund und Sicherheitsgrenze

Das bestehende World-PR-#308-Verfahren kann **nicht** ohne eigenen Maschinen-API-Key genutzt werden. Der bekannte `POST /api/v1/auth/password-recovery/challenges`-Endpunkt liefert einen World-Challenge-Token und muss deshalb geschützt bleiben; die Berechtigungen `password-recovery:issue` und `password-recovery:complete` sind keine Identitätsnachweise. Beim Betreiber sind beide Portal-Key-Variablen nachweislich leer.

R509 ergänzt **zusätzlich** einen eigenen öffentlichen Self-Service-Ablauf. Die bisherigen serverseitig authentifizierten Endpunkte bleiben ausdrücklich unverändert.

## Neu – anonyme, aber durch Empfängerbesitz geschützte Recovery

- `POST https://world.stadt-nexverse.de/api/v1/auth/password-recovery/request` – JSON `{"email":"testkonto@example.invalid"}`; **kein API-Key, kein Bearer erforderlich**. World prüft die E-Mail nur in der vorhandenen UserAccount-MariaDB, stellt bei genau einem aktiven lokalen Konto eine zufällige 256-Bit-Challenge über den bestehenden `NexPasswordRecoveryStore` aus und sendet eine SMTP-Mail **selbst an die bei World bestätigte Kontoadresse**. An jeden Anfrager wird immer `202 {"status":"accepted","correlation_id":"..."}` zurückgegeben, ob Konto unbekannt, mehrfach vorhanden, gültig, gedrosselt oder SMTP fehlgeschlagen. **Token, UUID und Empfängeradresse kommen niemals in der öffentlichen Antwort vor.**
- `POST https://world.stadt-nexverse.de/api/v1/auth/password-recovery/confirm` – JSON `{"token":"<World-Einmal-Token-aus-E-Mail>","new_password":"<neues-Passwort>"}`; **kein API-Key**, denn die 256-Bit-Challenge aus der zugestellten Mail ist selbst der Besitznachweis. World validiert TTL, Hash, Besitzerbindung, Einmalverwendung und Accountsperren mit dem bereits vorhandenen World-Recovery-Store, ändert das Passwort in ROBUST und widerruft World-Refresh-Sitzungen. Token nie erneut verwendbar.
- Die existierenden Maschinenendpunkte `/challenges` und `/complete` mit API-Key-Pflicht bleiben vollständig erhalten.
- Verbindungen dürfen nur über geschützte TLS-/autorisierte Reverse-Proxy-Konfiguration erfolgen. World-Gate und eine zusätzliche per-IP-Ratebegrenzung greifen. Öffentliche Anforderung ist nach E-Mail-Hash höchstens **dreimal pro Stunde und World-Prozess** zulässig; die bestehende persistente Datenbank setzt zusätzlich die Grenze pro eindeutigem Konto durch. Für hohen Betrieb ist ein Proxy-/IP-bewusstes verteiltes Anfragerate-Limit zusätzlich erforderlich. Keine ungeschützten Tokens oder URLs loggen.
- Die Funktion ist **standardmäßig deaktiviert** (`PasswordRecoveryPublicEnabled=false`). World-Mailserver muss separat sicher konfiguriert werden, auch wenn kein **World-API-Key** mehr benötigt wird.

## World-/ROBUST-Konfiguration

In der tatsächlich geladenen World-Konfiguration `[NexVerseWorldApi]` (zunächst **nur Staging**):

```ini
[NexVerseWorldApi]
Enabled = true
EnablePrivilegedEndpoints = true
PasswordRecoveryEnabled = true
PasswordRecoveryPublicEnabled = true
```

Zusätzlich müssen **auf dem World-/ROBUST-Prozess** folgende SMTP-Umgebungsvariablen gesetzt werden. Die Mailcredentials gehören **nicht** ins Repository oder Chat, sondern in eine nur für den World-Dienst lesbare Umgebung:

```dotenv
NEXVERSE_WORLD_RECOVERY_SMTP_HOST=<SMTP-Server>
NEXVERSE_WORLD_RECOVERY_SMTP_PORT=587
NEXVERSE_WORLD_RECOVERY_SMTP_USER=<SMTP-Benutzer>
NEXVERSE_WORLD_RECOVERY_SMTP_PASSWORD=<SMTP-Passwort>
NEXVERSE_WORLD_RECOVERY_SMTP_FROM=<verifizierter Absender>
```

Die SMTP-Verbindung erfordert TLS/STARTTLS; keine unverschlüsselte Mailauthentifizierung. Öffentliche Anfrage ohne SMTP-Konfiguration liefert 503, kein Erfolg wird vorgetäuscht. Der Reset-Link wird ausschließlich auf `https://stadt-nexverse.de/passwort-zuruecksetzen?token=...` gesetzt. World-MariaDB verwendet unverändert die vorhandene `ogl_password_recovery`-Tabelle. Kein zweiter Account-Datastore.

## Stadtportal ohne Maschinen-API-Key

Erst **nach** World-Build, TLS, World-MariaDB-Initialisierung, aktiviertem öffentlichen World-Recovery und Staging-SMTP-Test wird im geschützten Stadtportal-Environment aktiviert:

```dotenv
NEXVERSE_WORLD_RECOVERY_PUBLIC=true
```

`NEXVERSE_WORLD_RECOVERY_API_KEY` wird dann nicht benötigt; die Stadtportal-Anwendung ruft nur die beiden öffentlichen World-Routen mit JSON über HTTPS auf und versendet selbst **keinen Reset-Link**. Die bisherige Portal-SMTP-Implementierung für „Benutzername vergessen“ bleibt unverändert. Der allgemeine `NEXVERSE_WORLD_API_KEY` wird für andere World-Funktionen weiterhin separat behandelt.

Nach dem autorisierten Portal-Deployment:

```bash
sudo systemctl restart nexverse-stadtportal
sudo bash /home/users/stadtnexverse/www/stadt-nexverse.de/scripts/diagnose-password-reset-r495.sh
```

Die neue Diagnose meldet `NEXVERSE_WORLD_RECOVERY_PUBLIC_FILE=TRUE` sowie `NEXVERSE_WORLD_RECOVERY_PUBLIC_PROCESS=TRUE` und `RESULT=PUBLIC_WORLD_RECOVERY_CONFIGURED_WORLD_SMTP_AND_ENDPOINT_NOT_VERIFIED`. **Diese Meldung beweist ausschließlich die Portal-Konfiguration, nicht SMTP-Zustellung oder World-Freischaltung.** Es ist bewusst kein unbekannter Secret-Wert in der Diagnose.

## Staging- und Produktionsabnahme

1. World-/ROBUST-Build mit R509 tatsächlich bereitstellen, World-Config + SMTP aus sicherem Secretspeicher lesen, Firewall, Reverse-Proxy-`X-Forwarded-Proto` und HTTPS prüfen.
2. Unter Staging ohne API-Key die `/request`-Route mit freigegebenem Testkonto aufrufen und die echte E-Mail am verifizierten Konto empfangen. Keine Tokenbytes in öffentlicher Response.
3. Anonyme/mehrdeutige Adresse und SMTP-Störung geben dieselbe neutrale 202 aus und offenbaren keine Account-Identität. Bei deaktivierter Funktion 503; ohne HTTPS 403; bei zu vielen Anfragen 429. SMTP-Störung wird nur intern als generischer Fehler ohne Adress-/Tokenlogging registriert.
4. Einmaligen World-Token binnen 15 Minuten an `/confirm` senden. Der neue Passwort-Login muss funktionieren, der alte fehlschlagen. Token-Replay und Ablauftimeout → 410. World-Refresh-Sessions widerrufen. Getrennte Portal-Cookie-Invalidierung ist ohne Account-ID in World-Response noch ein offener Punkt.
5. Unerwartete Systemfehler und SMTP-Timeouts dürfen weder World-Login noch andere APIs stoppen; nach Staging-Freigabe erst World, dann Portal produktiv deployen. Authentifizierte alte Recovery-Endpunkte testen, damit diese nicht versehentlich öffentlich sind.
6. **Keine produktive Passwortänderung für Einwohnerkonten ohne Betreiberfreigabe** und kein automatisches Testen mit echten Konten.

## Transparente Grenze

Ohne einen ausgestellten Maschinen-API-Key und **ohne die neue R509-World-Erweiterung samt World-SMTP** ist ein sicherer End-to-End-Passwort-Reset mit der vorhandenen World-API technisch nicht möglich. Eine öffentliche Herausgabe von World-Kontodaten oder eines gültigen Passwort-Reset-Tokens wäre eine schwerwiegende Sicherheitslücke und wird ausdrücklich nicht implementiert.
