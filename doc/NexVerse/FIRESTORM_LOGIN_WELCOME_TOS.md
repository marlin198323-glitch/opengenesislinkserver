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

## Firestorm-ToS: technisch moeglich, noch nicht implementiert

Der Viewer unterstuetzt die klassische Second-Life-kompatible ToS-Aufforderung.
Antwortet ROBUST beim Login mit login=false, reason=tos und message=Text,
zeigt Firestorm einen Dialog zur Annahme. Nach Zustimmung versucht der Viewer
den Login erneut und setzt agree_to_tos=true.

Der Dialog erscheint nach der Passwortpruefung und vor dem Betreten der Welt.
Ein umfangreiches HTML-Regelwerk ist nicht garantiert; daher soll das
StadtPortal die Volltextfassung ueber eine dauerhafte HTTPS-Seite bereitstellen.

### Erforderlicher sicherer Login-Ablauf

1. ROBUST prueft Identitaet, Passwort, Accountstatus und Scope.
2. ROBUST prueft die gespeicherte Annahme der aktuell gueltigen ToS-Version.
3. Fehlt diese, antwortet ROBUST mit login=false, reason=tos und einer Meldung
   mit Versionsnummer sowie Link auf die vollstaendigen Bedingungen.
4. Bei agree_to_tos=true MUSS ROBUST das Passwort erneut korrekt pruefen,
   dann die Annahme dauerhaft speichern: PrincipalID, Version/Hash, UTC-Zeit.
5. Scheitert die Speicherung oder lehnt der User ab, wird kein Login gewaehrt.
   Es duerfen zuvor keine Presence- oder Simulator-Sessions erstellt werden.
6. Beim naechsten normalen Login bleibt der Dialog fuer diese Version aus.
   Eine neue ToS-Version loest den Dialog erneut aus.

Die Annahme muss im autoritativen gemeinsamen Datenbankdienst persistiert
werden (SQLite, PostgreSQL, MariaDB), nicht in einer fluechtigen Datei.
Passwoerter, Tokens oder Session-Geheimnisse gehoeren nicht in Audit-Protokolle.
Es darf keine Sicherheitsumgehung durch ein gefaelschtes agree_to_tos geben.

Die Login-Datenschnittstellen fuer XML-RPC, LLSD und WebSocket muessen das
Flag weitergeben. Hypergrid-Gaeste sowie bestehende Loginwege sind getrennt
zu pruefen, um die Interoperabilitaet nicht zu beschaedigen.

### Abnahmetests vor der Aktivierung

- Firestorm 7.2.4: Erstlogin zeigt Dialog; noch kein Simulator-Eintritt.
- Annehmen fuehrt zu dauerhaft gespeicherter Versionszustimmung und Login.
- Ablehnen blockiert Login ohne gespeicherte Zustimmung.
- Zweiter Login oder ROBUST-Neustart: Dialog bei gleicher Version unterdrueckt.
- Neue Version: erneute Aufforderung.
- Falsches Passwort zusammen mit agree_to_tos=true: kein Login, kein Store.
- Parallele Logins, SQLite/PostgreSQL/MariaDB und HG-Reisen pruefen.

Status: ToS ist konzeptionell spezifiziert, aber NICHT aktiviert oder als
Sicherheitsfunktion implementiert. Die Willkommensrotation ist unabhaengig
davon bereits Bestandteil dieses Aenderungsbranches.
