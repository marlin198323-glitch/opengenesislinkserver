# Stadt NexVerse: wechselnde Login-Begruessungen und Firestorm-ToS

Stand: 21. Oktober 2026

## Umgesetzt: zufaellige Willkommensmeldungen

Im Abschnitt [LoginService] koennen in WelcomeMessages mehrere mit | getrennte
Meldungen hinterlegt werden. Bei jedem erfolgreichen Login wird unabhaengig und
zufaellig eine dieser Meldungen ausgewaehlt. Der Platzhalter <USERNAME> wird mit
dem vollstaendigen Avatarnamen ersetzt. Laufende Logins beeinflussen einander nicht.

Konfiguration in bin/Robust.HG.ini:

    [LoginService]
    WelcomeMessage = "Willkommen in der Stadt NexVerse! ...Beyond the Reality."
    WelcomeMessages = "Willkommen, <USERNAME>! ...Beyond the Reality.|Viel Spass, <USERNAME>!"

Prioritaet: WelcomeMessages, sofern nicht leer, vor MessageUrl, sofern erfolgreich
geladen, vor WelcomeMessage als Fallback. Die Sequenz \n wird als Zeilenumbruch
interpretiert. Ohne WelcomeMessages bleibt das bisherige Verhalten unveraendert.

Die produktive versionierte bin/Robust.HG.ini enthaelt fuenf Varianten. Der Server
liefert die ausgewaehlte Begruessung im normalen Loginantwort-Feld message aus.
Kein Viewer-Patch erforderlich.

Rollout: C#-Code neu bauen und die tatsaechlich eingesetzte Robust.HG.ini
mit der Repository-Version abgleichen. ROBUST neu starten. Mehrere echte
Firestorm-Logins durchfuehren, um wechselnde Meldungen zu pruefen.

## Implementiert im Entwicklungsbranch: Firestorm-Bürgerordnung als ToS

Dokumentquelle: `doc/NexVerse/STADT_NEXVERSE_BUERGERORDNUNG_1.0.md`,
vollständig und unverändert aus der vom Stadtbetreiber bereitgestellten MD-Datei.
Version **1.0**, Dokumentstand **20.10.2026**.
Original-SHA256: `a57bee6dd48116684a872b736276c6c2ced3dbfff85acc81dfe7597b3ad13204`.
Die Bürgerordnung nennt als Geltungsbeginn die **offizielle Veröffentlichung**.
Der abschließende Entwurfshinweis wurde unverändert beibehalten: Betreiber muss
vor Aktivierung die Freigabe und Konsistenz mit dem Portal bestätigen.

Die aktuelle verbindliche Seite ist
`https://stadt-nexverse.de/service/tos`. ROBUST liefert diese exakte
HTTPS-Adresse im Login-Fehler `reason=tos` als `message` an den Viewer.
Die Seite muss dort die vollständige freigegebene Bürgerordnung anzeigen.

**Kompatibilitätshinweis:** Einige ältere OpenSim-Firestorm-Builds erkennen
nur `http://` als Anlass, im ToS-Fenster einen eingebetteten Browser zu
öffnen. Mit der vom Betreiber verlangten direkten HTTPS-Adresse kann die
Anzeige deshalb je nach Viewer-Version auf reinen URL-Text zurückfallen.
Die serverseitige Zustimmungsspeicherung bleibt davon unabhängig; im
Firestorm 7.2.4.80712 den echten Anzeige- und Bestätigungsablauf prüfen.
Zurück zu HTTP wechseln oder die TLS-Sicherheit herabsetzen ist hier
**keine** automatische Lösung.

### Konfiguration

```ini
[LoginService]
NexVerseTosEnabled = true
NexVerseTosVersion = "1.0"
NexVerseTosUrl = "https://stadt-nexverse.de/service/tos"
```

**Aktiv in der mitgelieferten `bin/Robust.HG.ini`** für Stadt NexVerse.
Andere Beispielkonfigurationen bleiben standardmäßig deaktiviert und werden
nicht automatisch für fremde Grids umgestellt. Bei bereits laufenden Servern
kann eine lokale `Robust.HG.ini` abweichen: die drei Schlüssel dort gezielt
eintragen bzw. den Enabled-Wert auf `true` setzen und ROBUST neu starten.

**Deployment:** Zunächst Datenbank und `Robust.HG.ini` sichern, dann
den neuen Build einspielen. Die UserAccounts-Migration läuft über den
konfigurierten Datenbankprovider. Bei unerwarteten Loginproblemen den
Wert auf `false` zurücksetzen und ROBUST neu starten; bereits
gespeicherte Zustimmungen bleiben dabei erhalten.
Prüfe die Erreichbarkeit mit `curl -IL https://stadt-nexverse.de/service/tos` und die Darstellung in Firestorm. Für direkte HTTPS-Links ist keine HTTP-Weiterleitung erforderlich.

### Verbindlicher Login-Ablauf

1. ROBUST prüft Passwort, Konto- und Scope-Status wie bisher.
2. Fehlt die Zustimmung zur **aktuell konfigurierten** Version, liefert ROBUST
   `login=false`, `reason=tos`, `message=<ToS-Start-URL>`. Zu diesem
   Zeitpunkt existiert noch **keine Simulator-Presence**.
3. Firestorm blendet sein natives ToS-Fenster ein, dessen eingebetteter Browser
   das gesamte Regelwerk anzeigt. Ablehnen führt zum Login-Abbruch.
4. Nach Zustimmung sendet Firestorm `agree_to_tos=true` und führt den
   Loginprozess erneut durch. Erst **nach erneuter Authentifizierung** speichert
   ROBUST die Zustimmung: `PrincipalID`, `NexVerseTosAcceptedVersion`,
   `NexVerseTosAcceptedAtUtc`.
5. Die Speicherung erfolgt über den gemeinsamen UserAccountService; ROBUST
   liest die Version unmittelbar erneut aus der Datenbank. Ein fehlgeschlagener
   Write oder Readback führt zu einem **Login-Fehler** statt Umgehung.
6. Bei späterem Login entfällt der Dialog für Version 1.0. Erhöht der Betreiber
   `NexVerseTosVersion`, ist erneutes Zustimmen erforderlich.

Die Speicherung erfolgt über versionierte `UserAccounts`-Migrationen für
SQLite, MariaDB/MySQL und PostgreSQL. Bei **remote** angebundenem
UserAccountService werden die Felder zusätzlich im `setaccount`-Handler
übernommen und können nur bei autorisiertem/aktiviertem `AllowSetAccount`
persistiert werden. Die produktive HG-Konfiguration verwendet dagegen ein
lokales UserAccountService-Plugin für den Login. Es erfolgt **keine** Speicherung von
Passwort, API-Schlüssel oder Sitzungstoken im TOS-Protokoll.

Die Zustimmung gilt **pro Account**. Bestehende Hypergrid-Gast-Einreisen
gehen über einen anderen Pfad (HG Gatekeeper) und unterliegen derzeit **nicht**
dieser ToS-Voranmeldung. Die Verpflichtung von HG-Gästen gemäß Bürgerordnung
ist ein separater Integrationspunkt, kein automatisch verfügbares Feature.

### Abnahme vor Live-Freigabe

- Datei-Hash prüfen; publizierte Webseite mit identischer freigegebener Version abgleichen.
- `curl -IL https://stadt-nexverse.de/service/tos`: die Seite liefert einen erfolgreichen HTTPS-Abruf (gegebenenfalls nach regulärer Weiterleitung).
- Vollständige Bürgerordnung inklusive §§ 1–22 und abschließendem Hinweis
  im TOS-Firestorm-Fenster sichtbar. Im derzeitigen Firestorm-OpenSim-Code
  kann die Checkbox bereits vor Abschluss des Webseitenabrufs freigegeben
  werden: Ein erzwungenes Lesen oder Scrollen bis zum Textende ist damit
  **nicht** garantiert; die Zustimmung ist ein expliziter Klick.
- Bei fehlender Portal-Seite oder ungültigem Zertifikat muss die Erreichbarkeit vor dem produktiven Einsatz korrigiert werden. Der Server kann den
  erfolgreichen Webseitenabruf durch Firestorm nicht selbst nachweisen.
- Wird die Bürgerordnung im Portal inhaltlich geändert, **muss** die
  NexVerseTosVersion mit erhöht und die archivierte MD-Fassung aktualisiert
  werden, sonst bestätigt der Avatar keine eindeutig referenzierte Fassung.
- Falsches Passwort (auch mit `agree_to_tos=true`): kein Write und kein Login.
- Erstlogin: TOS erscheint, weder Presence noch Region wird vorher betreten.
- Akzeptieren: Avatar kommt online; Zustimmung wird samt UTC-Datum gespeichert.
- Ablehnen: kein Login, keine Zustimmung gespeichert.
- Späterer Login und ROBUST-Neustart: kein Dialog bei derselben Version.
- Versionserhöhung: Dialog wird erneut angezeigt.
- Datenbankmatrix SQLite, PostgreSQL und MariaDB; zwei parallele Logins.
- Bestehende Hypergrid- und Firestorm-Login-Prozesse dürfen nicht regressieren.

Das zugehörige Repository-CI-Gate prüft Quellkontrakte und den exakten
MD-Dokument-Hash. Es ersetzt **keinen** echten Firestorm- und DB-Livetest.

### Umgang mit lokaler Robust.HG.ini

Die produktive INI enthält Secrets und lokale Anpassungen. Vor `git pull`
sichern, mit `git stash push -- bin/Robust.HG.ini` schützen und nach Pull
beziehungsweise Merge neue Konfigurationsoptionen gezielt manuell übernehmen.
`git stash pop` kann bei geänderter Repo-INI Merge-Konflikte verursachen;
sicherheitshalber ggf. die Datei aus der externen Sicherung wiederherstellen
und die drei TOS-Schlüssel manuell ergänzen, statt produktive Werte zu ersetzen.


## Firestorm 7.2.4: ToS-Fenster erscheint nach Bestätigung erneut

Im XML-RPC-Login kann Firestorm die LLSD-Zustimmung als Boolean `true`
oder als numerischen Wert `1` serialisieren. Beide bedeuten Zustimmung.
Der Login-Handler normalisiert deshalb `true` und `1`; andere Werte
einschließlich `false`, `0`, leer oder nicht übermittelte Felder werden
nicht als Zustimmung anerkannt. Passwortprüfung, Versionsprüfung und
persistentes Speichern bleiben unverändert erforderlich.

Diagnose nach Deployment: Die ROBUST-Konsole muss bei Bestätigung zuerst
`[NEX-TOS]: XML-RPC Zustimmungsflag empfangen.` (alternativ LLSD)
und anschließend `[NEX-TOS]: Zustimmung am LoginService angekommen ...`
melden. Danach muss `Buergerordnung 1.0 akzeptiert ...` erscheinen.
Ohne die erste Meldung kommt das Flag nicht durch den Protokoll-Handler.
Ohne die letzte Meldung DB-Migration/Account-Service-Protokoll prüfen.
Keine Passwörter, Login-Tokens oder API-Schlüssel protokollieren.
