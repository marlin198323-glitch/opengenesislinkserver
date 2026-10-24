# OGL AssetService 2.0 — paralleler Speicher und FSAssets-Bestandsmigration

**Status (23.10.2026): Staging-Implementierung in PR #283. Keine Freigabe für den laufenden Stadt-NexVerse-ROBUST.**

## Architektur

Der bestehende `FSAssetConnector` bleibt innerhalb des OGL-Connectors der **autoritative
Schreiber für SQL-Metadaten und FSAssets**. Es wird **kein zweiter FSAssets-Schreiber**
gestartet. Nach einem erfolgreichen Store wird der persistierte Nutzdateninhalt
in einem eigenen OGL-Verzeichnis unter seinem SHA-256 abgelegt und der
UUID-zu-Hash-Verweis in einer separaten SQLite-Datenbank veröffentlicht.

Ein Lesezugriff prüft **immer zuerst den aktuellen FSAssets-SQL-Hash**.
Nur wenn der OGL-Index dieselbe Prüfsumme nennt und die OGL-Datei den
SHA-256-Test besteht, wird diese bevorzugt ausgeliefert. Sonst wird mit
dem bisherigen FSAssets-Dienst gelesen. Die ursprünglichen UUIDs,
Assets, Referenzen, Berechtigungen und Inventare bleiben unverändert.

- **Neue Assets** werden bei aktiviertem Parallelmodus direkt mitgespiegelt.
- **Alte Assets** bleiben über FSAssets erreichbar, bis sie per Snapshot
  importiert sind. Ein fehlendes OGL-Objekt ist kein gelöschtes Asset.
- **Fehlgeschlagener Spiegel-Schreibvorgang** blockiert einen bereits
  erfolgreich abgeschlossenen FSAssets-Store nicht. Das Asset kann später
  durch den Snapshot-Import nachgezogen werden.
- **Delete/Update:** SQL bleibt autoritativ. Veraltete OGL-Verweise werden
  wegen des SQL-Hashvergleichs nicht bedient. Alte, nicht mehr referenzierte
  Inhaltsdateien werden **nicht automatisch gelöscht**.
- **Grenze:** Beide lokalen Backends laufen im selben ROBUST-Prozess,
  nicht als unabhängige entfernte Microservices. Dezentrale Replikation,
  Failover ohne SQL sowie mehrere unabhängige Asset-Writer sind noch
  **nicht** implementiert.

## Aktivierung ausschließlich im isolierten Staging

Kopiere den zusammengehörigen FSAssets-/SQL-Snapshot in vollständig getrennte
Verzeichnisse und starte einen Staging-ROBUST mit eigener Datenbank, eigenen
Ports und einem getrennten OGL-Objektverzeichnis. Die echte
`[AssetService]`-Konfiguration für `StorageProvider`, `ConnectionString`,
`Realm`, `BaseDirectory` und `SpoolDirectory` muss auf diesen **Klon**
zeigen. Nach dem Backup **nur dort** ändern:

```ini
[AssetService]
    LocalServiceModule = "OpenGenesisLINK.Addons.OGLAssetService.dll:OGLAssetConnector"
    ; Bestehende FSAssets- und SQL-Konfiguration auf den STAGING-Klon zeigen lassen

[OGLAssetService]
    Enabled = true
    Mode = "compatibility"
    MemoryCacheMiB = 0
    SingleWriterCacheEnabled = false
    DiskCacheEnabled = false
    RedisEnabled = false
    ExperimentalAtomicUpdatesEnabled = false
    ParallelStoreEnabled = true
    ParallelReadPreferred = true
    ParallelObjectDirectory = "/staging/ogl/objects"
    ParallelIndexPath = "/staging/ogl/index/locations.sqlite"

[OGLAssetServiceAdmin]
    Enabled = false
```

Die OGL-Verzeichnisse müssen vom alten FSAssets-Base-/Spool-Verzeichnis
getrennt sein. `ParallelStoreEnabled` und `ParallelReadPreferred` sind
standardmäßig **false**. Ein regulärer `git pull main` aktiviert sie nicht.

Im Staging anschließend Texturen, Meshes, Sounds und Notecards hochladen,
über Get/GetMetadata/GetData lesen, ROBUST neu starten und dieselben Assets
erneut prüfen. Zusätzlich den FSAssets-Payload eines **Wegwerf-Testassets**
vorübergehend aus dem Staging entfernen und den OGL-Read-First-Pfad prüfen.
Das darf niemals am produktiven Datenbestand erfolgen.

## Bestehende FSAssets migrieren (offline, resumierbar)

1. Sämtliche Schreiber zu einem definierten Zeitpunkt anhalten und
   zusammengehörige SQL-Metadaten, FSAssets-Base **und Spool** sichern.
   Die Live-Verzeichnisse niemals als Quellpfade angeben.
2. SQL in einer **eigenständigen Testdatenbank** wiederherstellen.
3. UUID und Hash als TSV ohne Header aus dieser Testdatenbank exportieren
   (MariaDB/PostgreSQL-Befehle: [EXPORT.md](../OGLAssetService.Migration/EXPORT.md)).
   Den unabhängigen `COUNT(*)`-Wert derselben Snapshot-Tabelle notieren.
4. Vor dem Schreiben immer einen reinen Prüflauf machen:

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- \
  --parallel-snapshot /snapshot/fsassets/data /snapshot/fsassets/tmp \
  /snapshot/uuid-hash.tsv /staging/ogl/objects /staging/ogl/index/locations.sqlite \
  --expected-rows 123456
```

5. Wenn alle Zeilen vorhanden und alle Dateien hash-validiert sind,
   ausdrücklich die Kopie freigeben. Auch bei `--copy` prüft die Migration
   jetzt den kompletten Snapshot **zunächst schreibgeschützt erneut** und
   erstellt erst nach bestandenem Preflight das OGL-Zielverzeichnis bzw. den
   SQLite-Index. Das verdoppelt die Lese-/Hash-Prüfung, schützt jedoch vor
   Teilimporten bei bereits beschädigten oder unvollständigen Exporten.
   Nach einem erfolgreichen Preflight bleibt der Snapshot unveränderlich zu
   halten: eine spätere I/O-Störung im Kopierdurchlauf kann weiterhin einen
   resumierbaren Teilimport erzeugen:

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- \
  --parallel-snapshot /snapshot/fsassets/data /snapshot/fsassets/tmp \
  /snapshot/uuid-hash.tsv /staging/ogl/objects /staging/ogl/index/locations.sqlite \
  --expected-rows 123456 --copy --offline-snapshot-confirmed
```

**123456 ist ein Beispiel:** unbedingt den tatsächlichen SQL-Snapshot-Count
verwenden. Bei `UseOsgridFormat=true` zusätzlich `--osgrid` anfügen.

Die Ausgabe enthält `Rows`, `Valid`, `Imported`, `VerifiedExisting`,
`Bytes` und `Failed`. Fehler führen zu einem Nicht-Null-Exitcode.
Der Import ist idempotent: Ein erneuter Lauf verifiziert bereits vorhandene
UUID-/Hash-Zuordnungen und speichert nur fehlende Inhalte.
Weder die SQL-Quelle noch FSAssets-Base/Spool werden beschrieben.

**Bei laufender Produktion:** Nach einem sicheren Snapshot entstandene
neue Assets werden vom aktivierten Parallel-Store sofort mitgespiegelt.
Eine spätere Bestandsmigration braucht einen Nachlauf aus einem neuen
konsistenten Snapshot, bevor eine exklusive Umschaltung erwogen werden kann.

## Rückfall

Solange FSAssets vollständig erhalten bleibt, lässt sich bei Problemen
im Staging `ParallelReadPreferred=false` setzen: Lesen erfolgt wieder direkt
über FSAssets. Bei vollständig deaktiviertem Parallelmodus
`ParallelStoreEnabled=false` setzen. Die originale FSAssetConnector-Konfiguration
kann ohne Änderung von Inventar/Asset-UUIDs wiederhergestellt werden.
Schreiber vor dem Wechsel kontrolliert stoppen. Die OGL-Spiegeldateien
bleiben zur Analyse liegen, sie sind **kein** Ersatz für ein FSAssets-Backup.

## Abnahme vor Produktion

- [ ] Konsistenter, wiederherstellbarer SQL-/FSAssets-/Spool-Snapshot
- [ ] TSV-Export vollständig, keine Hash- oder Zeilenfehler
- [ ] OGL-Snapshot-Import ohne Fehler, zweiter Lauf idempotent
- [ ] Upload und Download für Mesh, PBR, Texturen, Animationen, Sounds, Scripts
- [ ] Legacy-Assets, neue Assets, UUID-/Hash-Parität nach Restart
- [ ] Viewer, Regionswechsel und Hypergrid-Lesepfade
- [ ] Mehrere ROBUST-Instanzen und veränderte SQL-Mappings unter Last
- [ ] Ausfall von OGL-Dateisystem und SQLite → FSAssets-Fallback
- [ ] FSAssets-Rollback im isolierten Staging erfolgreich

Redis-Distributed-Cache, Replikation und experimentelle Atomic Updates
bleiben weiterhin deaktiviert. **Keine automatische Löschung** des alten
FSAssets-Datenbestands und keine automatische Produktivmigration.
