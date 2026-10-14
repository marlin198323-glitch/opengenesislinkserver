# OpenGenesisLINK – Entfernung der Remote-Asset-Löschfunktion

**Sicherheitsänderung / 0.9.3.12 nach Tag-Erstellung**

## Befund und Bedrohungsmodell

Der bisherige ROBUST-Asset-HTTP-Dienst registrierte einen Handler für
`DELETE /assets/{assetId}`. Über zwei historische Konfigurationsschalter
konnte der Handler Remote-Löschungen erlauben. Bei Freigabe aller
Asset-Typen ließ sich jedes über diesen Dienst erreichbare Asset-ID-Ziel
löschen. Eine ausreichend privilegierte oder ungeschützte
Netzwerkverbindung zum Asset-Dienst stellt damit eine gefährliche
Löschoberfläche dar.

Diese Quellcodefeststellung belegt **keinen** tatsächlichen Malware-Vorfall
auf einem bestimmten Grid und noch keinen alleinigen Zusammenhang mit
einem LSL-Skript. Die Angriffsbedingungen hängen von Listener-Exposition,
HTTP-Authentifizierung, alten INI-Dateien, Netzwerkzugriff und
möglichen zusätzlichen Schwachstellen ab. Das Risiko ist dennoch
hoch und die exponierte Löschfunktion unnötig.

## Dauerhafte Entfernung

1. Die historische Klasse `AssetServerDeleteHandler` einschließlich des
   Typs `AllowedRemoteDeleteTypes` ist aus Quellcode und Build entfernt.
2. `AssetServiceConnector` registriert ausschließlich die verbleibenden
   GET-/POST-/Existenz-Endpunkte, **keine** `DELETE /assets`-Route.
3. `AssetServicesConnector.Delete(id)` sendet keine HTTP-Anfrage mehr,
   sondern gibt unveränderlich `false` zurück. Damit können auch
   interne Aufrufer nicht versehentlich ein Remote-DELETE auslösen.
4. Die früheren beiden Remote-Löschschalter sind aus den ausgelieferten
   ROBUST-/HG-/Testkonfigurationen entfernt und werden vom Code
   grundsätzlich nicht mehr eingelesen. Selbst wenn eine ältere lokal
   gepflegte INI sie weiterhin auf `true` setzt, kann sie die entfernte
   Route **nicht** reaktivieren.
5. Die Funktion `delete asset <UUID>` an der lokalen,
   zugriffsgeschützten ROBUST-Administratorkonsole bleibt als explizite
   Betreiberwartung erhalten. Lokale `IAssetService.Delete`-Aufrufe
   werden nicht pauschal entfernt. Das ist keine Remote-HTTP-Schnittstelle.

## Betriebliche Auswirkungen

- Das bisherige automatische Aufräumen historischer MapTile-Assets über
  Remote-HTTP-DELETE ist nicht mehr möglich. Nicht mehr referenzierte
  Assets müssen im Bedarfsfall über einen **lokalen**, auditierbaren und
  mit Backups abgesicherten Wartungsprozess bereinigt werden.
- Normales Abrufen und Speichern von Assets über GET/POST bleibt
  unverändert. Ein vollständiger Sicherheitsreview dieser anderen
  Asset-Endpunkte ist von dieser Änderung nicht abgedeckt.
- **Keine alte Release-DLL verwenden:** Der bestehende GitHub-Tag
  `v0.9.3.12` wurde vor diesem Patch erzeugt. Er bleibt unveränderlich.
  Erst ein neu gebauter OGL-Server mit dieser Änderung ist geschützt.
- Der Schutz greift nach Deployment der neuen
  `OpenSim.Server.Handlers.dll` auf ROBUST und gegebenenfalls betroffenen
  Standalone-Instanzen sowie nach Neustart des laufenden Prozesses.

## Sofortmaßnahmen für Grid-Betreiber

1. Noch vor dem Upgrade den externen `DELETE /assets/*`-Zugriff an
   Firewall oder Reverse-Proxy sperren; nicht den gesamten Asset-Abruf
   sperren. Netzfreigabe des PrivatePort prüfen.
2. Verdächtige Zugriffe anhand von Reverse-Proxy-/ROBUST-Logs
   untersuchen, ohne einen Vorfall allein aus der Existenz der Option
   zu behaupten.
3. Vollständige Sicherung von Inventardatenbank, Asset-Datenbank,
   FSAssets-Verzeichnis und Konfiguration erstellen und
   Wiederherstellbarkeit stichprobenartig prüfen.
4. Gesicherte Serverfassung mit diesem Patch bauen, kontrolliert
   ausrollen und neu starten.
5. Negativtest: HTTP `DELETE /assets/{testUUID}` darf **keinen**
   Löschhandler erreichen und **keinen** Test-Asset-Datensatz entfernen.
   Test ausschließlich in einer isolierten Testinstallation
   oder mit ausdrücklich erstelltem Test-Asset durchführen.
6. `python3 tools/ci/verify_no_remote_asset_delete_09312.py`
   muss erfolgreich sein. CI prüft zusätzlich den gesamten .NET-Build.

Eine reine Änderung in `.ini` reicht bei älteren Binärdateien **nicht**
aus, um die alte Löschmöglichkeit endgültig zu beseitigen.
