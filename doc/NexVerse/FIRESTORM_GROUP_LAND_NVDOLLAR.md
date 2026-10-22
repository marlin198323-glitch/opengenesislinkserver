# Firestorm 7.2.4 – Gruppe → Land und NV$

## Befund (22.10.2026)

Das Bild zeigt in der OpenGenesisLINK-Gruppe
**„Sie sind nicht berechtigt, Landeigentum der Gruppe anzuzeigen.“**
sowie bei Gruppen-NV$ **„Wird geladen…“**.

Die vorhandene SQL-Datensicherung `nexverse_sql2.sql` enthält Daten
in `os_groups_groups` (vier Gruppen), `os_groups_membership` (32
Mitgliedschaften), `os_groups_roles` (12 Rollen), weiteren
`os_groups_*`-Tabellen und der separaten Grundstückstabelle `land`
(~150 Parzellen). Eine zusätzliche `os_groups_land`- oder
`os_groups_accounting`-Tabelle ist für diesen Viewer-Reiter **nicht**
erforderlich. Das sind Daten eines Exports, kein verifizierter
Live-Datenbankzustand.

## Technische Ursache

- Firestorm zeigt den Berechtigungshinweis, wenn die Viewerfunktion
  `gAgent.isInGroup(groupUUID)` **false** liefert. Dazu die
  Gruppenmitgliedschaft im Viewer nach einem neuen Login prüfen.
  In Firestorm 7.2.4 hängt diese Meldung **nicht** direkt von einer
  fehlenden SQL-Tabelle oder der Land-Berechtigungsmaske ab.
- Der Viewer schickt `PlacesQuery` mit `DFQ_GROUP_OWNED=0x100`,
  Gruppen-UUID im `QueryID` und eigener `TransactionID`. Der
  bisherige NexDiscoveryViewerModule bediente nur `DirPlacesQuery`,
  nicht `PlacesQuery`. Mit konfiguriertem `GroupLandApiKey` wird
  jetzt die gridweite Gruppenlandliste aus dem zentralen SQL-RegionStore
  abgefragt. Ohne diesen Schlüssel bleibt die Abfrage auf lokale
  Simulatorregionen beschränkt. Die Gruppenmitgliedschaft wird
  vor der Anfrage geprüft.
- Für Gruppen-NV$ existierten bereits `GroupAccountSummaryRequest`,
  `DetailsRequest`, `TransactionsRequest` im UDP-Layer. Das
  NexVerseMoneyModule antwortete nicht. Jetzt werden die Events
  verarbeitet. Für Kontostände wird das echte NV$-Ledger über die
  geschützte World API abgefragt. Nur `GroupPowers.Accountable`
  (normalerweise Gruppen-Eigentümerrolle) darf Geldbeträge sehen.
- Die Gruppen-NV$-Antwort muss exakt `MoneyData.RequestID` aus
  dem Viewer übernehmen; zuvor nutzte der LLClientView-Sendeweg
  irrtümlich die Avatar-UUID. Deshalb konnte Firestorm eine
  Abrechnungsantwort nicht der geöffneten Registerkarte zuordnen.
- Seit PR #274 werden autorisierte NV$-Ledgerbuchungen der letzten
  sieben Tage über `/api/v1/groups/{id}/transactions` gelesen und
  auf Firestorm-Gruppenabrechnungs-/Verkaufspakete abgebildet.
  Gegenkonten und erfundene Verkäufe werden nicht offengelegt.

## Nach Deployment prüfen

1. ROBUST und Simulator gemeinsam aus dem aktuellen `main`
   bauen/aktualisieren. Die Regionmodule
   `NexVerseMoneyModule` und `NexDiscoveryViewerModule`
   müssen aktiviert und geladen sein. Firestorm **neu anmelden**.
2. Firestorm → Gruppen → OpenGenesisLINK → „Mitglieder & Rollen“
   prüfen: ist der eingeloggte Avatar dort Mitglied?
   Wenn nicht, Berechtigungsanzeige und zentrale
   `os_groups_membership`-Zeile sowie Viewer-Gruppen-Updates
   prüfen. Kein Rechtebypass per SQL einführen.
3. „Land und NV$“ erneut öffnen: bei Mitgliedschaft sollte die
   zentrale Gruppenland-Abfrage Parzellen aus anderen Simulatorprozessen
   ebenfalls anzeigen. Voraussetzung: gemeinsamer SQL-RegionStore,
   `[NexGroups] EnableGridWideLand = true` und gültiger
   `[NexDiscoveryViewer] GroupLandApiKey` mit `groups:read`.
   Ohne Backend-Schlüssel gilt weiterhin die lokale Fallback-Liste.
4. Für Gruppen-NV$ benötigt der Simulator-API-Key
   `economy:read` und für den Firestorm-Gruppenverlauf zusätzlich
   `groups:read`. Zum erstmaligen Anlegen fehlender
   Gruppen-Wallets (ohne Startguthaben) zusätzlich
   `economy:transfer`. Das Geld-/Gruppenkonto darf nicht
   anhand eines ungeprüften Avatar-/Gruppennamens identifiziert
   werden.
5. In Gruppenrollen `Accountable` nur autorisierten
   Gruppenbuchhaltern/Eigentümern zuweisen. Ohne diesen
   Eintrag zeigt der Adapter keinen fremden Kontostand.

## Diagnostische SQL-Abfragen (nur lesen)

```sql
-- Nach Anmeldung die tatsächlichen UUIDs aus der Gruppe ermitteln.
SELECT GroupID, Name FROM os_groups_groups
WHERE Name = 'OpenGenesisLINK';

-- Gruppenmitgliedschaft des betroffenen Avatars prüfen.
SELECT GroupID, PrincipalID, SelectedRoleID, Contribution
FROM os_groups_membership
WHERE GroupID = '<GROUP_UUID>'
  AND PrincipalID = '<AVATAR_UUID>';

-- Gruppenland: nur tatsächliche Deeds / Gruppenbesitz.
SELECT UUID, Name, RegionUUID, OwnerUUID, GroupUUID, Area
FROM land
WHERE IsGroupOwned = 1 AND GroupUUID = '<GROUP_UUID>';
```

Keine neue Gruppentabelle anlegen. Fehlende Viewer-Mitgliedschaft
zuerst über den GroupsService/HGGroups-Service und die Login-
`AgentGroupDataUpdate`-Übertragung prüfen.
