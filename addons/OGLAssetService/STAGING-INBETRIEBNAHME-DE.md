# OGL AssetService – isolierte Inbetriebnahme (Generation 1)

**Status:** Staging-Anleitung, **keine** Freigabe zum Umstellen des produktiven Stadt-NexVerse-ROBUST.
Die bestehende FSAssets-Dateiablage und ihre SQL-Metadaten bleiben maßgeblich.
Der OGL-Connector nutzt im Kompatibilitätsmodus denselben FSAssetConnector als Basisklasse;
er ist noch kein eigenständiger, verteilter Objekt-Storage.

## 1. Getrennte Umgebung aufbauen

- Verwende ein separates Arbeitsverzeichnis, eigene ROBUST-Ports, eigene Anmeldedaten,
  eine **wiederhergestellte Testdatenbank** und einen **Klon** der Asset-Dateien.
- Niemals einen zweiten laufenden ROBUST als FSAssets-Schreiber gegen die
  produktive Datenbank oder den produktiven Spool starten.
- Erst alle FSAssets-Schreiber kontrolliert anhalten und den Spool abschließen.
  Anschließend SQL-Datenbank, Datenverzeichnis **und Spool** als konsistenten
  Stand sichern. Snapshot und Zeitpunkt dokumentieren.
- Für MariaDB/MySQL und PostgreSQL stehen geprüfte SQL-Export-/Auditwerkzeuge
  unter [Migration/EXPORT.md](../OGLAssetService.Migration/EXPORT.md) bereit.
  Der Datei-Kopierer ersetzt keine Datenbankwiederherstellung.

## 2. Feature-Branch separat kompilieren

Führe den Build im Wurzelverzeichnis eines **separaten Staging-Checkouts** aus:

```bash
./runprebuild.sh
bash ./compile.sh
```

Der OGL-Build schreibt ab diesem Feature-Branch direkt nach `bin/`.
Das Build-Skript `addon-modules/OGLAssetService/build.sh` prüft nun die
Runtime-DLLs an genau diesem Ort. Für diesen Checkout sind .NET 8 und
die üblichen OpenGenesisLINK-Build-Abhängigkeiten erforderlich.

Prüfe:

```bash
test -s bin/Robust.dll
test -s bin/OpenGenesisLINK.Addons.OGLAssetService.dll
test -s bin/StackExchange.Redis.dll
test -s bin/Microsoft.Data.Sqlite.dll
```

**Wichtig:** Solange PR #237 offen ist, nur den aktualisierten Feature-Branch
isoliert bauen. Nach einer möglichen Code-Integration in `main` ist das Addon
zwar mitkompiliert, wird aber in produktiven ROBUST-Konfigurationen **nicht
automatisch aktiviert**. Die Integration von Quellcode ist keine Freigabe zum
Umstellen des Asset-Speichers.

## 3. Aus einer Kopie der ROBUST-Konfiguration einen Staging-Start erstellen

`[AssetService]` muss die bereits funktionsfähigen
`BaseDirectory`, `SpoolDirectory`, `Realm` und gegebenenfalls
`StorageProvider`-Angaben des **Staging-Datenbestandes** enthalten.
Nur dort den lokalen Dienst ersetzen:

```ini
[AssetService]
    LocalServiceModule = "OpenGenesisLINK.Addons.OGLAssetService.dll:OGLAssetConnector"
    BaseDirectory = "/staging/fsassets/data"
    SpoolDirectory = "/staging/fsassets/tmp"

[OGLAssetService]
    Enabled = true
    Mode = "compatibility"
    MemoryCacheMiB = 0
    SingleWriterCacheEnabled = false
    DiskCacheEnabled = false
    RedisEnabled = false
    ExperimentalAtomicUpdatesEnabled = false

[OGLAssetServiceAdmin]
    Enabled = false
```

Die Pfade sind **Beispiele**, keine gültigen Produktionspfade. Die
DB-Zugangsdaten müssen auf die restaurierte Staging-Datenbank zeigen.
Lege keine Passwörter im Repository ab. Werden in `[ServiceList]` sowohl
`AssetServiceConnector` als auch `HGAssetServiceConnector` gestartet,
müssen die **beiden** Abschnittskonfigurationen gesondert geprüft werden.
Kein zweiter Asset-Dienst darf versehentlich parallel auf denselben
Staging-Datenbestand schreiben.

## 4. ROBUST starten und echten OGL-Start nachweisen

Im Staging-Verzeichnis:

```bash
cd bin
dotnet Robust.dll -inifile Robust.HG.OGLStaging.ini
```

Die tatsächliche Staging-INI muss zuvor im `bin/`-Verzeichnis angelegt
und auf isolierte Ports, Datenbank und Asset-Pfade angepasst werden.
Ein erfolgreicher Start zeigt neben der geerbten
`[FSASSETS]: FS-Asset-Dienst ist aktiviert`-Meldung zusätzlich:

```text
[OGL-ASSETS]: OGL AssetService geladen; authoritative=FSAssets; mode=compatibility; local_cache=False; redis_coordination=False; atomic_updates=False
```

**Nur diese neue OGL-Meldung beweist, dass der OGL-Konstruktor erfolgreich
abgeschlossen wurde.** Die FSAssets-Meldung allein belegt das nicht.

## 5. Testfälle auf dem isolierten Staging-Bestand

1. Auf echte vorhandene Asset-UUIDs `Get`, `GetMetadata`, `GetData` anwenden
   und Bytes/Metadaten mit dem originalen FSAsset-Connector vergleichen.
2. Neue Test-Assets speichern, nach Neustart wieder lesen und danach löschen.
   `UpdateContent` **separat** prüfen; die Standard-Kompatibilitätsmethode
   ist nicht mit einem vollständig produktionsfertigen Update gleichzusetzen.
3. Fehlende Assets, korrupte Hash-Dateien, abgebrochene Writes und
   die getrennte HG-Asset-Konfiguration prüfen.
4. Nach Rückschaltung den ursprünglichen FSAsset-Connector gegen die
   unveränderte Staging-Sicherung erneut starten und Rücklesetests durchführen.
5. Alle Ergebnisse und Rollback-Protokolle dokumentieren. Für Freigabekriterien
   siehe [CUTOVER-BENCHMARKS.md](CUTOVER-BENCHMARKS.md) und
   [CUTOVER-ROLLBACK.md](../OGLAssetService.Migration/CUTOVER-ROLLBACK.md).

## 6. Staging-Vergleich vorhandener Assets (ohne Schreibzugriff)

Dieser Test verwendet **ausschließlich** eine konsistente, isolierte Kopie der Datenbank und des FSAssets-Verzeichnisses. Die Quelle niemals parallel durch zwei ROBUST-Prozesse beschreiben.

1. Alle Produktionsschreiber nach vereinbartem Wartungsverfahren stoppen, Spool vollständig leeren, zusammengehörige SQL- und FSAssets-Snapshots anfertigen und die Produktivdienste nach Sicherung regulär wiederherstellen.
2. Die Snapshots in einen eigenen, privaten Staging-Datenbanknamen sowie getrennte Asset- und Spoolpfade wiederherstellen; den vollständigen Hash-/Metadaten-Audit aus CUTOVER-ROLLBACK.md bestehen.
3. Zwischen 10 und 100 wirklich vorhandene UUIDs verschiedener Assettypen als eine UUID je Zeile in einer geschützten Datei /private/staging-asset-ids.txt hinterlegen.
4. Den **alten FSAssetConnector** auf dem Staging-Klon starten, ausschließlich über Loopback und Testports erreichbar. Baseline erfassen:

```bash
python3 tools/ci/verify_ogl_assetservice_staging_readback.py capture \
  --endpoint http://127.0.0.1:19496 \
  --ids /private/staging-asset-ids.txt \
  --manifest /private/ogl-legacy-baseline.json
```

5. Alten Staging-ROBUST vollständig stoppen, keine Schreiber mehr zulassen und erst danach den **OGLAssetConnector** im Kompatibilitätsmodus auf demselben **Staging-Klon** starten.
6. Die zuvor gesicherten Inhalte erneut abrufen und vergleichen:

```bash
python3 tools/ci/verify_ogl_assetservice_staging_readback.py verify \
  --endpoint http://127.0.0.1:19496 \
  --manifest /private/ogl-legacy-baseline.json
```

Das Readback-Werkzeug verwendet nur HTTP GET, vergleicht Get/GetMetadata/GetData, Bytes per SHA-256, Länge und relevante Metadaten. Es folgt keinen Redirects, akzeptiert nur Loopback-HTTP, erlaubt maximal 100 UUIDs und verändert keine Assetdaten. Das Manifest enthält UUIDs und Metadaten, aber **keine Asset-Nutzdaten**; es wird mit Berechtigung 0600 angelegt.

7. Für den Rollback-Gate OGL-Staging vollständig stoppen, einen weiteren **separaten Wiederherstellungsklon** des Original-Snapshots aufbauen und den alten Connector darauf erneut erfolgreich prüfen. Kein Datenbank- oder FSAssets-Restore auf dem laufenden Produktivserver.

Der Vergleich ersetzt noch nicht Viewer-/Hypergrid-Praxistests, Mehrprozess-Tests und die Fehler-/Ausfallbehandlung.

## 7. Was weiterhin gesperrt ist

Keine produktive Umschaltung, kein Entfernen bisheriger FSAssets,
kein Redis-Distributed-Cache und kein Aktivieren des Admin-HTTP-Listeners,
bevor die vollständigen Testgates, Mehrprozess-Invalidierung,
Ausfallwiederherstellung und Rollback-Prüfung bestanden sind.

Der GitHub-Workflow `OGL AssetService Addon Modules Build` kompiliert
den normalen `compile.sh`-Weg und startet einen **isolierten ROBUST mit MariaDB**.
Zusätzlich prüft `tools/ci/verify_ogl_assetservice_http_persistence.py`
über die tatsächlichen Asset-HTTP-Endpunkte das Speichern, `Get`,
`GetMetadata` und `GetData` für synthetische Textur-, Mesh- und Notecard-Daten.
Der Test kontrolliert den komprimierten FSAssets-Datenträgerinhalt, startet
ROBUST neu und liest dieselben Assets erneut aus der persistenten Ablage.

**Grenze:** Dieser Test prüft den OGL-Kompatibilitätsmodus mit eigenen
Wegwerf-Assets. Er ersetzt weder einen Test mit der produktiven
FSAssets-Sicherung noch Viewer-Uploads, Hypergrid-Zugriffe, gleichzeitige
Asset-Schreiber, Redis-Ausfälle und einen realen Rollback-Test.

**Wichtig für Hypergrid:** Der OGL-Connector initialisiert seine
FSAssets-Basisklasse jetzt anhand des vom ROBUST-Connector angegebenen
Abschnitts (`AssetService` oder `HGAssetService`). Beide Abschnitte
müssen getrennt geprüft werden. Das Ändern beider
`LocalServiceModule`-Werte in der produktiven Konfiguration ist
ausdrücklich **noch nicht freigegeben**.
