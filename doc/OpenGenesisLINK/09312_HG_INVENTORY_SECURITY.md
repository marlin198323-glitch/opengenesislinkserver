# OpenGenesisLINK 0.9.3.12 – HG-Inventar: Sicherheitsnotfallsperre

## Befund

Die HTTP-Anbindung des öffentlich bereitgestellten `HGInventoryService`
verwendete den generischen `XInventoryConnectorPostHandler`. Ohne eine
vertrauenswürdige, an einen konkreten Avatar und dessen aktive Sitzung gebundene
Autorisierung konnte dieser Einstiegspunkt Inventaranfragen verarbeiten.
Die HG-Suitcase-Beschränkung ist **keine** Client-Authentifizierung und kann
keine Berechtigung zum Bearbeiten fremden Inventars nachweisen. Auch ein
gemeinsam verwendeter Grid-Schlüssel wäre dafür kein Ersatz.

**Bewertung:** Kritische fehlende Zugriffskontrolle (read/write, CWE-862 /
CWE-639 möglich). Der Befund stammt aus dem Quellcode. Keine Aussage darüber,
ob produktive Server tatsächlich angegriffen wurden.

## Sofortige Gegenmaßnahme

Die öffentlich konfigurierte `HGInventoryService`-Instanz wird vom
`XInventoryInConnector` eindeutig identifiziert. Ihr HTTP-Handler antwortet
für alle Inventaroperationen **vor** dem Auswerten von Anfrageparametern mit
`HTTP 403` (`OGL-HG-INV-403`). Weder eine frei übermittelte UUID noch
BasicAuth oder eine gefälschte Besitzerkennung schalten diese Sperre frei.

Der private Standard-`InventoryService`-Connector bleibt unverändert.
Der Mechanismus greift für die ROBUST-Hypergrid-Konfigurationen und den
Standalone-Hypergrid-`InventoryServiceInConnectorModule`. Die einzige
notwendige Voraussetzung für diesen Schutz ist die aktualisierte
OpenGenesisLINK-Serverbinary; ein `.ini`-Toggle kann ihn nicht aushebeln.

## Betrieb / Rollout

1. Zuerst vollständiges Backup von Inventar-Datenbank und Konfigurationen.
2. OGL 0.9.3.12 mit diesem Security-Fix auf ROBUST und allen betroffenen
   Standalone-Simulatoren aktualisieren, Release-Build verteilen, Prozesse
   kontrolliert neu starten. Ein Git-Merge alleine aktualisiert laufende
   Instanzen nicht.
3. Auf dem öffentlichen HG-Endpunkt bestätigen, dass eine Inventaranfrage
   ohne Autorisierung HTTP 403 ergibt. **Keine echten Nutzerdaten** verwenden,
   keine produktiven Inventare verändern. Schreiber- und Lesezugriffe sind
   gleichermaßen gesperrt.
4. Die interne Inventarschnittstelle am PrivatePort nur für vertrauenswürdige
   Dienste erreichbar lassen (Firewall/ACL); insbesondere nicht direkt über
   das öffentliche Internet oder Reverse-Proxy-Routen freigeben.
5. Bis alle betroffenen Instanzen aktualisiert sind, den externen HTTP-Pfad
   `/xinventory` an der öffentlichen Netzwerkgrenze blockieren. Andere
   öffentliche HG-Endpunkte dürfen nicht pauschal mitgesperrt werden.
6. Nach Deployment Tests von lokalem Login/Inventar und HG-Reisen durchführen.
   Die externen HG-Inventarfunktionen (Suitcase lesen, hinzufügen, ändern,
   DeRez / fremde Grid-Inventaroperationen) sind bewusst beeinträchtigt.
   **Nicht** zur Wiederherstellung der Funktion die interne API nach außen
   öffnen oder diese Sperre herauspatchen.
7. Zugriff-/Fehlerlogs auf verdächtige HG-Inventaranfragen untersuchen; die
   bloße Häufigkeit eines Inventar-Requests beweist keinen Angriff.

## Nachhaltiger Ersatz (eigener Sicherheits-Change)

Für die Wiederaktivierung des Hypergrid-Inventars braucht OGL eine
authentifizierte, pro Avatar begrenzte und nur kurz gültige HG-Sitzungs-
berechtigung. Diese muss mindestens an Avatar-ID, Ziel-Grid, Node,
konkrete zulässige Inventaroperationen, Eigentum und Gültigkeitsdauer
gebunden sein, mit Revocation und Replay-Schutz. Autorisierung muss
**serverseitig** erfolgen, nicht über `PRINCIPAL`-/`Owner`-Felder aus dem
Request. Die normale Suitcase-Grenze bleibt eine zusätzliche Beschränkung.
Lesen und Schreiben erfordern separate Berechtigungen; jede Änderung wird
pro Benutzer auditiert. Authentifizierung und Autorisierung müssen mit
echten Fremd-Grid-Integrations- und Negativtests abgenommen werden.

Regressionsschutz: `tools/ci/verify_hg_inventory_security_09312.py`;
zusätzlich GitHub Release-Build und Smoke-Tests.
