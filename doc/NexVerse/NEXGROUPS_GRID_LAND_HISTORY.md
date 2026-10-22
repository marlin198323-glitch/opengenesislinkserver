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

Die API bietet jetzt **echte** Finanzbuchungen unter
`/api/v1/groups/{id}/transactions`. Das historische Firestorm-UDP-
Details-/Sales-Protokoll bildet noch keine vollständigen Ledger-Buchungen
ab: Die vorhandenen Paketantworten bleiben leer statt erfundene
Einträge zu senden. Der Kontostand im Viewer kommt weiterhin aus
dem NV$-Ledger.

Das neue HTTP-Regressionstestpaket kontrolliert die Rechte- und
Landabfrage samt SQL-Daten einer Region auf einem anderen Simulator.
