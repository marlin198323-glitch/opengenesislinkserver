# OpenGenesisLINK 0.9.3.13 – temporaere Legacy-HG-Inventarfreigabe

**Betreiberentscheidung, 15.10.2026:** Der offene Hypergrid-Inventardienst wird vorlaeufig wieder bereitgestellt, weil die Sicherheitsnotfallsperre aus 0.9.3.12 Kaeufe und Suitcase-Transfers fremder Hypergrid-Gaeste unterbrechen kann. Dieser Kompatibilitaetsmodus soll nur bis zu einer mit OpenSimulator abgestimmten, interoperablen Loesung bestehen.

## Wichtig: Die Sicherheitsluecke ist NICHT behoben

Das klassische Hypergrid-XInventory-Protokoll stellt keine nachweisbar avatar- und sitzungsgebundene Autorisierung fuer externe Inventaroperationen bereit. Ein vom anfragenden Fremd-Grid gelieferter Principal/Owner-UUID-Wert ist kein Berechtigungsnachweis. Auch die Suitcase-Einschraenkung und eine gemeinsame Grid-Authentifizierung loesen dieses Problem nicht. Wenn der oeffentliche Legacy-Dienst aktiv ist, koennen unberechtigte Lese-/Schreibzugriffe auf Inventardaten moeglich sein. Wir haben keinen Angriff im Live-Betrieb nachgewiesen, die Zugriffskontrollluecke ergibt sich jedoch aus dem Protokoll und dem Handler.

## Verhalten und explizite Konfiguration

* **Standardverhalten im C#-Handler:** Ausliefern am `HGInventoryService` ohne explizite Option fuehrt weiterhin zur bisherigen HTTP-403-Sperre. Der lokale `InventoryService` bleibt unveraendert.
* **Freigegebene HG-Profile:** `bin/Robust.HG.ini` und `bin/Robust.HG.ini.example` registrieren die Legacy-Route `HGInventoryServiceConnector` am `PublicPort` und setzen im Abschnitt `[HGInventoryService]` `AllowLegacyPublicHGInventory = true`.
* **Standalone-HG:** `bin/config-include/StandaloneHypergrid.ini` setzt die gleiche Opt-in-Option fuer den bereits vorhandenen HG-Inventar-Connector.
* Beim Start wird eine ausdrueckliche `[OGL SECURITY][LEGACY HG]`-Warnung ausgegeben. Das ist keine neue Autorisierung, keine Signaturpruefung und kein Sicherheitsgewinn.

```ini
[HGInventoryService]
LocalServiceModule = "OpenSim.Services.HypergridService.dll:HGSuitcaseInventoryService"
AllowLegacyPublicHGInventory = true
```

Nur den **Suitcase**-Service fuer HG einsetzen; niemals den unbeschraenkten `XInventoryService` als oeffentliches Ersatzmodul ausliefern. Den privaten `InventoryInConnector` nicht am PublicPort anmelden.

## Deployment

1. Vor dem Deploy DB, FSAssets, Inventar, Runtime-Konfigurationen und Region-Backups konsistent sichern. Bestehende, lokal abweichende `Robust.HG.ini`-Dateien werden durch ein Git-Update eventuell nicht ersetzt oder duerfen nicht ungeprueft ueberschrieben werden.
2. ROBUST bzw. Standalone mit aktueller Binary und aktivem Legacy-HG-Konfigurationsprofil neu starten. Den Start-Warnhinweis im Log bestaetigen. Ein neues GitHub-Commit allein aendert keine laufende Instanz.
3. Mit zwei **Testavatare** auf voneinander getrennten Grids einen kleinen Kauf / Gegenstandstransfer pruefen: Zahlung, Inventareintrag und Assetabruf getrennt dokumentieren. Vorher/Nachher mit Datenbank-/Inventar-Snapshots vergleichen.
4. PublicPort nur fuer benoetigte HG-Dienste oeffnen; privaten Serviceport nicht direkt aus dem Internet erreichbar machen. Logs und verdächtige Inventar-Mutationen ueberwachen. Eine allgemeine IP-Allowlist kann offene Hypergrid-Interoperabilitaet einschraenken und ersetzt keine Avatar-Autorisierung.

## Ruecknahme (Sicherheitsnotfall oder nach Upstream-Fix)

In `[HGInventoryService]` `AllowLegacyPublicHGInventory = false` setzen; in ROBUST-HG zusaetzlich `HGInventoryServiceConnector` im `[ServiceList]`-Abschnitt auskommentieren. Danach den betroffenen Dienst neu starten. Ohne Route gibt es 404, bei registrierter Route ohne Freigabe 403.

## Offene Punkte

* Offizielle OpenSimulator-Entscheidung bzw. interoperable, avatar-spezifische Autorisierung abwarten.
* Echte HG-Kauf- und Asset-Zustelltests im produktionsnahen Testgrid sowie Zugriffskontroll- und Abuse-Monitoring erforderlich.
* Keine bestehende Stable-Tag-Version umetikettieren; `v0.9.3.12` bleibt unveraenderlich. Die Legacy-Freigabe ist eine spaetere Entwicklungsentscheidung.
* Diese Freigabe darf **nicht** als security fix / CVE-Remediation kommuniziert werden.

Referenz: `doc/OpenGenesisLINK/09312_HG_INVENTORY_SECURITY.md`.
