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

**Wichtig:** Der PR #237 ist nicht in `main` enthalten. Ein regulärer
`main`-Build ist deshalb kein Nachweis für die weiterentwickelte OGL-Version.

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

## 6. Was weiterhin gesperrt ist

Keine produktive Umschaltung, kein Entfernen bisheriger FSAssets,
kein Redis-Distributed-Cache und kein Aktivieren des Admin-HTTP-Listeners,
bevor die vollständigen Testgates, Mehrprozess-Invalidierung,
Ausfallwiederherstellung und Rollback-Prüfung bestanden sind.

Der neue GitHub-Workflow `OGL AssetService Addon Modules Build` kompiliert
den normalen `compile.sh`-Weg und führt zusätzlich einen isolierten
ROBUST/MariaDB-Starttest aus. Er ist ein notwendiger **technischer Vorabtest**,
kein Ersatz für echte Asset-End-to-End-Tests im Staging oder auf realen Sicherungen.
