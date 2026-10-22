# NexGroups – gridweite Landliste und NV$-Gruppenhistorie

## Neue World-API-Endpunkte

Beide Endpunkte benötigen eine gültige Authentifizierung und
`groups:read`. Der Dienst prüft für Bewohner die echte Gruppenmitgliedschaft.
Die Finanzhistorie verlangt zusätzlich `GroupPowers.Accountable`.

```
GET /api/v1/groups/{group_id}/land
GET /api/v1/groups/{group_id}/transactions
```

Die Landabfrage liest ausschließlich `IsGroupOwned=1` und
`GroupUUID={group_id}` aus dem gemeinsamen OpenSim-`land`-RegionStore.
Mit dem GridService werden die tatsächlichen Regionen, globalen
Koordinaten und Namen aufgelöst. Regionen, die nicht mehr im Grid
registriert sind, werden nicht angezeigt. Es sind keine neuen SQL-Tabellen
oder Produktionsmigrationen notwendig.

Die Transaktionsliste liest die letzten sieben Tage aus dem echten
NV$-Ledger (maximal 200 Einträge). Pro Transaktion wird ausschließlich
die signierte Gruppenbuchung (positiv = Gutschrift, negativ = Belastung)
zurückgegeben; fremde Gegenkonto-Buchungen werden nicht preisgegeben.
Eine noch nicht angelegte Gruppenwallet liefert eine leere Historie,
ohne sie nur durch die Leseabfrage zu erzeugen.

## Firestorm und Simulator konfigurieren

ROBUST: `[NexGroups] Enabled = true`, `EnableGridWideLand = true`.
Die World API braucht Zugriff auf den gemeinsamen
`[DatabaseService]`-`RegionStore` und den `IGridService`.

In der World API einen **dedizierten**, serverseitigen API-Key mit dem
expliziten Scope `groups:read` für die Simulator-Landverzeichnisse
erstellen. Nicht das Admin-Token und keinen Avatar-Token verwenden.
Dieser Schlüssel muss nur für den Simulator erreichbar sein.

Im Simulator:

```ini
[NexDiscoveryViewer]
Enabled = true
LegacyFirestormCompatibility = true
WorldApiBaseUrl = "https://world.stadt-nexverse.de"
GroupLandApiKey = "${Environment|NEXVERSE_GROUP_LAND_API_KEY}"
```

Das `NEXVERSE_GROUP_LAND_API_KEY`-Environment-Secret darf niemals in
CAPS-URLs, Avatar-Inventar, Viewer-Code oder Logs auftauchen.
Der Simulator prüft die Gruppenmitgliedschaft, bevor er diesen
serverseitigen Schlüssel zur World API sendet.

Fehlt der Schlüssel oder ist das zentrale Verzeichnis nicht erreichbar,
bleibt die bisherige **lokale**, unvollständige Landliste verfügbar
und die Warnung erscheint im Simulatorlog. Für vollständige Gridabdeckung
muss eine gemeinsam genutzte RegionStore-Datenbank vorhanden sein.
Parzellen auf separaten, unabhängigen SQL-Instanzen sind nicht enthalten.

## Firestorm-Gruppenabrechnung

Die API liefert echte Finanzbuchungen der letzten sieben Tage unter
`/api/v1/groups/{id}/transactions`. Der Simulator prüft vor dem Abruf
die tatsächliche `GroupPowers.Accountable`-Berechtigung des verbundenen
Avatars und überträgt die Buchungszeilen anschließend in die nativen
`GroupAccountDetailsReply`- und `GroupAccountTransactionsReply`-Pakete.

**Konfiguration:** Der Simulator-Key unter `[NexEconomyViewer] ApiKey`
muss für diese neue Funktion zusätzlich zu `economy:read` auch
`groups:read` enthalten. Die World API akzeptiert für den
Gruppenverlauf nur einen ausdrücklich doppelt berechtigten Maschinen-Key
oder einen tatsächlich berechtigten Bewohner-Token. Ohne die zusätzliche
Berechtigung bleibt der Kontostand funktionsfähig, der Verlauf kann jedoch
nicht geladen werden.

Die Buchungsdaten sind Ledger-Daten. Gegenkonten und nicht verifizierte
Käufernamen werden nicht angezeigt. Der Firestorm-Verkaufsreiter filtert
echte positive `sale`-/`commerce`-/`purchase`-Buchungen, während die
Detailansicht sämtliche Gruppenbuchungen innerhalb der Periode enthält.
Negative, zu große oder nicht lesbare Buchungen werden nicht stillschweigend
als gültige Werte dargestellt.

Das neue HTTP-Regressionstestpaket kontrolliert die Rechte- und
Landabfrage samt SQL-Daten einer Region auf einem anderen Simulator.
