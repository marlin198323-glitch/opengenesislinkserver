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

## Zusatzfix 20.10.2026: HG-Online-Status nach erneutem Login

Bei lokalen Freunden wurde die Presence bereits korrekt aktualisiert.
Bei auswärtigen Freunden wie `Sven.Wagner@...` stellte der Simulator
dagegen beim eigenen Viewer-Login **nur eine einmalige Anfrage** an den
fremden `FriendsServerURI`-Endpunkt. War der externe Home-Agent während
dieses kurzen Zeitfensters nicht erreichbar, antwortete leer, oder
lieferte die URI-Discovery wegen eines vorübergehenden Ausfalls
noch nichts, bekam Firestorm danach **keinen zweiten Versuch**. Bei
erneuter Freundschaft wurde der Fehler durch einen neuen Online-Lookup
vorübergehend umgangen.

Die HG-Freunde-Abfrage nach dem Login wird jetzt durch eine begrenzte
Wiederherstellungsprüfung ergänzt:

- Beim Login liefert die bisherige Abfrage sofort alle bestätigten
  Online-Freunde. Bereits bestätigte HG-Freunde werden danach nicht
  noch einmal abgefragt.
- Ausschließlich **noch offline dargestellte HG-Freunde** mit aktivem
  `CanSeeOnline`-Recht werden nach 135, 270 und 540 Sekunden
  Warteintervall erneut beim autoritativen Home-Grid nachgefragt;
  spätestens beim Disconnect endet die Prüfung. Die 135 Sekunden
  überschreiten den Backoff von `UserManagementModule` für
  vorübergehend fehlende Home-Server.
- Nur vom Home-Grid bestätigte UUIDs, die auch in der angefragten
  Freundesliste stehen, lösen `SendAgentOnline` an Firestorm aus.
  Ein fehlgeschlagener Remote-Lookup führt **nie** zu einer
  erfundenen Online- oder Offline-Meldung.
- Lokale Freundschaften, Login-Benachrichtigungen und das
  HG-Protokoll werden nicht verändert. Die bestehende
  `statusnotification`-Methode wird wiederverwendet; diese teilt
  dem Gegenserver den aktuellen Online-Zustand des anfragenden
  Avatars zusätzlich mit. Deshalb ist die Zahl der Wiederholungen
  begrenzt, um fremde Home-Grids nicht dauerhaft zu belasten.

```ini
[HGFriendsModule]
PresenceRetrySeconds = 135
PresenceRetryAttempts = 3
```

`PresenceRetryAttempts = 0` deaktiviert diese Nachprüfung.
Die maximal unterstützten Werte sind 4 Wiederholungen und
30 Minuten Basisintervall. Die Einstellungen gehören auf
**jeden betroffenen Simulator** (z. B. `config-include/GridCommon.ini`),
nicht nur in ROBUST.

**Grenzen:** Ein fremdes Grid muss den interoperablen
`/hgfriends`-Statusdienst unterstützen und eine gültige
gegenseitige Freundschaft mit passendem Secret und
Online-Sichtbarkeitsrecht speichern. Ohne diese Bedingungen
kann das eigene Grid keine korrekte Präsenz ermitteln. Der Fix
kann keine komplett ausbleibenden Statusmeldungen des fremden
Home-Grids reparieren. Er dient der verzögerten Wiederherstellung
des beim Relog verpassten Status; er ersetzt keine
permanente, spekulative Anwesenheitsüberwachung.

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
