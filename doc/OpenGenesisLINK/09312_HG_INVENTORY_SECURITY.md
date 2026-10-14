# OpenGenesisLINK 0.9.3.12 — Sicherheit des öffentlichen Hypergrid-Inventars

## Sicherheitsbefund

Der öffentliche ROBUST-Hypergrid-Inventardienst ist über
`HGInventoryService@.../XInventoryInConnector` auf dem PublicPort erreichbar.
`[HGInventoryService] AuthType = None` dient der Kompatibilität zwischen Grids.
Der gemeinsam benutzte `POST /xinventory`-Handler hatte aber auch schreibende
Operationen freigeschaltet, ohne die Eigentümerschaft oder Sitzung des
anfragenden Avatars authentifiziert zu überprüfen. Request-Felder wie
`PRINCIPAL`, `Owner` und `ID` sind keine Identitätsnachweise.
Suitcase-Baum-Prüfungen verhindern nicht, dass ein fremder Anrufer
sich als Eigentümer eines anderen Suitcase ausgibt.

**Befund:** Unautorisierte Änderungen im HG-Suitcase sind anhand des
Codes plausibel. Kein Angriff auf ein produktives Grid wurde nachgewiesen.

## Sofortschutz in 0.9.3.12

Der `XInventoryInConnector` erhält bei `HGInventoryService` einen
read-only-Modus. Öffentliche HG-Aufrufe akzeptieren ausschließlich die
explizit geprüften lesenden Inventaroperationen. Alle anderen Methoden,
darunter CREATEUSERINVENTORY, ADDFOLDER, UPDATEFOLDER, MOVEFOLDER,
DELETEFOLDERS, PURGEFOLDER, ADDITEM, UPDATEITEM, MOVEITEMS und
DELETEITEMS, erhalten HTTP 403 und den kompatiblen `RESULT=False`.
Unbekannte neue Methoden bleiben ebenfalls standardmäßig gesperrt.

Der private `InventoryService`-Connector und die lokalen Simulator-
Inventaroperationen bleiben unverändert. Eine absichtlich unter
`HGInventoryService` konfigurierte alte REST-Connector-Variante
`InventoryServiceInConnector` bricht aus Sicherheitsgründen den Start
dieses Dienstes ab, weil sie auch nicht-authentifizierte Legacy-Routen
einschließlich `PUT /inventory` registrieren könnte.

**Kompatibilität:** Nutzer können weiterhin ihr bereits vorhandenes
HG-Suitcase auf fremden Grids lesen. Änderungen (insbesondere Uploads,
Neuanlagen und Verschieben in diesem Koffer über den öffentlichen HG-Pfad)
sind vorerst nicht möglich. Es gibt keinen Schalter zum unsicheren
Reaktivieren dieser Aufrufe. Eine spätere Write-Unterstützung benötigt
authentifizierte, zeitlich begrenzte und avatar-/aktionsgebundene
Autorisierungsnachweise.

## Wichtige verbleibende Einschränkung

Der bisherige lesende HG-Protokollpfad authentifiziert fremde Leser nicht
pro Avatar. Dieser Patch verhindert **Manipulationen**, aber gewährleistet
nicht die Vertraulichkeit des freigegebenen Suitcase-Inhalts gegenüber
Dritten, die bekannte IDs abfragen können. Keine vertraulichen Inhalte im
HG-Suitcase bereithalten, bis der Lesezugriff separat authentifiziert ist.

## Betriebsanweisung / Rollout

1. **Sofortmaßnahme ohne Code-Rollout:** Wenn kein öffentlicher HG-
   Inventarzugang benötigt wird, den externen Zugriff auf den
   `/xinventory`-Pfad am Reverse Proxy / der Firewall temporär
   blockieren. Interne private Inventardienste nicht versehentlich sperren.
2. Vor Upgrade ROBUST-Konfiguration, DB, Inventar und Regionen sichern.
3. ROBUST und ggf. Standalone-HG-Simulatoren mit dem gepatchten
   `OpenSim.Server.Handlers.dll` neu bauen und kontrolliert neu starten.
   Ein GitHub-Merge schützt laufende Altprozesse nicht.
4. Verifizieren: HG-Inventarlesezugriff weiterhin möglich; nicht
   authentifizierte `ADDITEM`, `UPDATEITEM`, `MOVEITEMS`,
   `ADDFOLDER`, `UPDATEFOLDER`, `DELETEITEMS` und unbekannte Methoden
   am PUBLIC HG-Port geben HTTP 403 ohne Datenmutation zurück.
   Auf dem PRIVATE-Port funktionieren reguläre lokale Inventarvorgänge.
5. Public und Private Port in Firewalls streng trennen. Keine internen
   `InventoryService`-Routen unkontrolliert aus dem Internet anbieten.
6. Für die vollständige CI-Regressionsprüfung:
   `python3 tools/ci/verify_hg_inventory_security_09312.py` und
   `dotnet run -c Release --project tools/ci/HgInventorySecurityRegression/HgInventorySecurityRegression.csproj`

Dies ist eine Fail-Closed-Notfallkorrektur und kein vollständiges
neues Hypergrid-Authentifizierungsprotokoll.
