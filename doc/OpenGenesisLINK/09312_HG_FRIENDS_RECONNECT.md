# OGL 0.9.3.12 – Hypergrid-Freunde nach Relog dauerhaft korrekt anzeigen

## Symptom

Nach dem Bestätigen einer Hypergrid-Freundschaft wird der fremde Avatar anfangs
als online angezeigt. Nachdem beide Nutzer offline waren, zeigt die
Freundesliste den fremden Avatar trotz erneutem Login auf beiden Grids
mitunter dauerhaft als offline.

## Behobene serverseitige Fehler

1. **Nur der erste Empfänger erhielt den Status:** Sowohl
   `HGFriendsService.StatusNotification` als auch
   `UserAgentService.StatusNotification` suchten in
   `PresenceService.GetAgents(...)` nach einem aktiven Freund und brachen
   danach mit `break` ab. Bei beispielsweise zwei lokalen Freunden wurde
   deshalb nur einer über das erneute Online-/Offline-Gehen des fremden
   Avatars benachrichtigt. Beide Implementierungen bearbeiten jetzt
   **alle** aktiven, zuvor anhand gegenseitiger Freundschafts-/Secret-Daten
   validierten Empfänger; mehrfache Presence-Datensätze während eines
   Regionenwechsels lösen keine doppelten Statusmeldungen aus.
2. **Dauerhaft negative Server-URL im Cache:** Scheiterte
   `UserManagementModule.GetUserServerURL(..., "FriendsServerURI")`
   während das fremde Grid offline oder vorübergehend nicht erreichbar
   war, wurde bisher in beiden Overloads eine leere
   `ServerURLs`-Dictionary im Avatarcache gespeichert. Wiederholte
   Online-Abfragen verwendeten den leeren Eintrag permanent statt
   den inzwischen wieder erreichbaren Home-Agent zu kontaktieren.
   Netzwerkausfälle oder fehlende `FriendsServerURI` setzen nun
   **keine dauerhafte negative Discovery-Antwort** mehr fest.
   Neue Versuche erfolgen nach dem bestehenden, begrenzten
   `BADURLEXPIRE` von 120 Sekunden (keine Dauer-HTTP-Schleife).

Die Online-Benachrichtigung bleibt abgesichert: HG-Freundeszuordnung,
Secret-Prüfung und gültige lokale Presence bleiben Voraussetzung.
Die gewohnten Firestorm-/Viewer-Pakete `SendAgentOnline` und
`SendAgentOffline` sowie bestehende Hypergrid-Protokolle bleiben
unverändert.

## Live-Abnahme

Die Änderung muss auf dem eigenen OGL-Grid bei **Robust UND den
betroffenen Simulatoren** neu gebaut und geladen werden. Um beiderseitige
Zustellung sicherzustellen, muss auch das fremde Home-Grid seine
Statusbenachrichtigungen korrekt an dieses Grid ausliefern; dessen
Software kann OGL nicht von sich aus reparieren.

- Auf zwei lokalen Avataren A und B Hypergrid-Freundschaft zu einem
  fremden Avatar F herstellen und beiderseitige Online-Sichtbarkeit
  erlauben. A und B befinden sich möglichst in verschiedenen Regionen.
- A, B, F offline gehen lassen; A und B wieder einloggen, danach F.
  Beide lokale Clients müssen F ohne Neu-Anlegen der Freundschaft
  online sehen.
- F offline und wieder online gehen lassen, während A und B online
  bleiben. Beide müssen die Offline-/Online-Änderung jeweils empfangen.
- Reihenfolge umkehren: F bleibt online, während A und B sich neu
  einloggen. Beim lokalen Relog soll die Liste F unmittelbar nach der
  Home-Friends-Abfrage online anzeigen.
- Kurzzeitiges Ausfallen des fremden Home-Grids simulieren bzw.
  feststellen: Die `FriendsServerURI` darf nicht für die gesamte
  Simulatorlaufzeit als fehlend festhängen. Nach Wiederkehr und
  maximal der zweiminütigen Backoff-Zeit muss eine folgende
  Statusabfrage wieder möglich sein.
- Bei identischem local user mehrfachen Presence-Records keinen
  doppelten Online-Status senden. Alte Freundes-UUIDs oder Secrets
  dürfen niemals zu fremden/unberechtigten Empfängern geleitet werden.

## Diagnose bei verbleibendem Fehler

Die folgenden Informationen helfen ohne Freigabe persönlicher
Zugangsdaten:

- Quelle und Ziel (z. B. OGL ↔ OSgrid), aktuelle Uhrzeit, Reihenfolge
  der Logins und ob die Statusänderung in beiden Richtungen fehlt.
- Robust: `[HGFRIENDS SERVICE]`-Log zu
  `statusnotification` und etwaigen Weiterleitungen.
- Simulator: `[HGFRIENDS MODULE]` und
  `[HGFRIENDS CONNECTOR]`, fehlgeschlagene Discovery der
  `FriendsServerURI` sowie gegebenenfalls Präsenz-/UserAgent-Logs.
- Wenn nur Avatar A die Meldung bekommt, B aber nicht, weiterhin
  Service-Presence und per-Region-Statusversand prüfen.

**Unverändert offener Abnahmepunkt:** Ohne einen echten zweiten
Hypergrid-Server kann der vollständige beidseitige
Cross-Grid-Login-Zyklus nicht allein mit statischen Tests nachgewiesen
werden. Die neue CI schützt die beiden betroffenen
Produktions-Codepfade und kompiliert den Server, ersetzt aber keinen
Live-Test.
