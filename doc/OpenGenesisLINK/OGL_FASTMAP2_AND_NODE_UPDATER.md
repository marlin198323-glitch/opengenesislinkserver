# OGL FastMap 2.0 und ROBUST Node-Update-Staging

Stand: 25.10.2026. Beide Funktionen sind optional. Produktivfreigabe erst nach Tests.

## FastMap 2.0: schnelle Regionsregistrierung

Bisher muss das komplexe Warp3D-Kartenbild vor der Grid-Registrierung einer
Region komplett gerendert werden. Im aktivierten FastMap-Modus registriert
OpenGenesisLINK zuerst die **zuletzt gespeicherte Map-Asset-UUID** und
generiert das aktualisierte Bild später aus einer begrenzten Warteschlange.
Eine frisch berechnete UUID wird nach Erfolg erneut an den GridService gemeldet.

Konfiguration in der aktiven OpenSim.ini:

    [Map]
    GenerateMaptiles = true
    MapImageModule = "Warp3DImageModule"
    DrawPrimOnMapTile = true
    TextureOnMapTile = true
    TexturePrims = true
    RenderMeshes = true
    FastMapAsyncStartup = true
    FastMapStartupDelaySeconds = 120
    MaptileRefresh = 0

- Ohne Aktivierung erfolgt das bisherige synchrone Rendering.
- Bei einer neuen Region ohne gültige alte Karten-UUID kann die Karte zunächst
  leer sein. Der erste Render liefert sie später nach.
- Pro Simulatorprozess arbeitet höchstens ein verzögerter FastMap-Auftrag
  gleichzeitig; dies begrenzt Renderkonkurrenz, aber nicht sämtliche CPU-Spitzen.
- Während des Shutdowns entfallen noch nicht gestartete Jobs. Ein schon
  laufender Render muss beendet werden, bevor seine Region entsorgt wird.
  Daher kann Shutdown im ungünstigsten Fall auf das Rendern warten.
- Ein manueller Befehl 'generate map' bleibt synchron. Eine Timer-Aktualisierung
  wird nur gestartet, wenn MaptileRefresh positiv ist.

Abnahme: Mehrere Regionen starten; Zeiten, Grid-Registrierung, sichtbare
Karten-UUIDs, Firestorm-Map, manuellen Refresh, Shutdown und fehlende
Asset-UUID prüfen. CPU-/RAM-/Watchdog-Vergleich auf identischen Assets messen.

## Updater-Stufe 1: von ROBUST angestoßenes Source/Build-Staging

ROBUST bietet bereits einen authentifizierten Node-API-Endpunkt. Die
Berechtigung **simulators:manage** ist für Updatebefehle erforderlich.
Jede Node muss die Funktion zusätzlich ausdrücklich einschalten:

    [NexVerseNodeAgent]
    Enabled = true
    UpdateStagingEnabled = true
    UpdateStagingRoot = "/opt/opengenesislink-updates"
    UpdateStagingBuild = true

Die Updater-Konfigurationen sind im Repository aus Sicherheitsgründen mit
UpdateStagingEnabled=false vorbelegt. Der NodeAgent muss mit einem signierten
NexBus an ROBUST angebunden sein. Das Skript ist im Repository unter
tools/ops/ogl-node-stage-update.sh verfügbar und wird relativ zur
Simulatorkopie (neben dem bin-Verzeichnis) aufgerufen. Bash, Git, flock,
realpath, Python 3 sowie beim Build das .NET-8-SDK sind erforderlich.

Einzelne Node:

    POST /api/v1/nodes/<node-id>/control
    Authorization: Bearer <simulators:manage-token>
    Content-Type: application/json

    {"action":"stage_update"}

Alle online gemeldeten Nodes:

    POST /api/v1/nodes/updates
    Authorization: Bearer <simulators:manage-token>
    Content-Type: application/json

    {"action":"stage_update"}

Der Status in GET /api/v1/nodes steht unter update.state,
update.operation_id und update.updated_at. HTTP 202 quittiert **nur das
Anstoßen**; die Node antwortet später completed/failed.

Der Node lädt ausschließlich
https://github.com/opengenesislink/opengenesislinkserver.git Branch main.
Der Download erfolgt in ein privates temporäres Verzeichnis, anschließend
optional ein Release-Build und die SHA-basierte Ablage im Staging.
Eine bereits bestehende Staging-SHA wird nicht überschrieben.

**Keine produktive Datei wird bei diesem Schritt modifiziert.**
Kein git pull im laufenden Checkout, kein automatischer Dienstneustart,
keine automatische Datenbankmigration, keine Änderung einer aktiven .ini,
kein Deploy. Das eigentliche Ausrollen bleibt eine getrennte Aktion mit
Operator-Freigabe, Backup, Rollback und Healthcheck.

### INI-Updatekonflikte

Das Tool tools/ops/ogl-config-example-sync.py vergleicht die Einstellungsnamen
der heruntergeladenen OpenSim.ini und der config-include/*.ini mit den aktiven
Dateien. Fehlende Schlüssel erscheinen in einer JSON-Datei, passende
vollständige neue Vorlagen ausschließlich als .example.ini im Staging:
config-review/missing-settings.json und config-review/*.example.ini.

Es werden **keine vorhandenen INI-Werte, Secrets oder Zugangsdaten** in
den generierten Bericht kopiert.

Manuelle Analyse zweier Installationen:

    python3 tools/ops/ogl-config-example-sync.py \
      --active-bin /opt/opensim/bin \
      --downloaded-bin /opt/ogl-new-checkout/bin \
      --output /opt/ogl-ini-review

### Grenzen

Der Download-/Build-Staging-Workflow ist kein fertig abgenommener
Orchestrator für Produktionsrollouts, Drain, Dienstneustart und Rollback.
Die Zeitvorgabe für den Node-Staging-Prozess beträgt 20 Minuten, sofern das
lokale Tooling vorhanden ist. Ein fehlender lokaler Updater wird als Fehler
gemeldet, statt unkontrollierte Shell-Kommandos anzunehmen.

## Prüfung der 222 gemeldeten leeren Assets von server.lubda.net

Im bereitgestellten OpenSim(2).log liegen 1.388 Warnungen für **222 unterschiedliche UUIDs**
(221-mal Asset-Typ 0, eine UUID Typ 49). Diese Meldungen beweisen nur, dass der
Asset-Abruf eine leere Datenantwort gesehen hat. Sie beweisen **nicht**, dass
alle Datenbankeinträge oder Inventar-/Objektreferenzen gelöscht werden dürfen.

Die Prüfliste tools/ops/ogl-empty-assets-audit.py ist ausdrücklich
**read-only**. Für die erste logbasierte Übersicht:

    python3 tools/ops/ogl-empty-assets-audit.py \
      --log "/private/OpenSim(2).log" \
      --out /private/ogl-assets-audit

Zur Gegenprüfung einen **konsistenten Offline-Snapshot** aus FSAssets,
Spool und SQL erstellen und darin die betroffenen Asset-UUIDs mit ihrer
SHA-256-Datei vergleichen (FSAssets-Export siehe
addons/OGLAssetService.Migration/EXPORT.md):

    python3 tools/ops/ogl-empty-assets-audit.py \
      --log "/private/OpenSim(2).log" \
      --metadata-tsv /private/snapshot/uuid-hash.tsv \
      --fs-root /private/snapshot/fsassets \
      --out /private/ogl-assets-audit

Bei UseOsgridFormat=true: Option --osgrid anhängen. Das Tool erzeugt nur
empty-assets-audit.csv und empty-assets-summary.json, niemals DELETE-
Anweisungen oder Quarantäneoperationen. Bei fehlendem SQL-Eintrag ist auch
eine fremde Hypergrid-Asset-UUID, ein Fallback-Cache oder ein früherer
Fehlerzustand möglich. Validierter Dateihash plus leere Antwort deutet
stattdessen auf Cache-/Read-/Netzwerkpfade.

**Sichere Reihenfolge:** SQL/Spool/FSAssets-Backup und Restore prüfen;
offline UUID/hash/parity prüfen; beschädigte Payloads nur aus verifizierten
Sicherungen auf einer **Staging-Kopie** wiederherstellen; Inventar,
Mesh-Materialien, Regionobjekte, Avatarwearables und HG-Verweise im
Staging abgleichen; Firestorm erneut testen. Erst nach Betreiberabnahme
dürfen nachweislich verwaiste Daten separat und reversibel quarantänisiert
werden. Ein globales oder automatisches Löschen von 222 UUIDs ist
nicht Bestandteil dieses Patches.

## Zusätzliche Sicherheit ab Patch: Veröffentlichung und privilegierter Updateauftrag

**Karten-Publikation:** Alte Terrain-/Parcel-Map-Assets werden erst
nach erfolgreichem `GridService.RegisterRegion` freigegeben. Eine Störung
während der erneuten ROBUST-Registrierung darf die auf dem Grid noch
beworbene Karten-UUID nicht ungültig machen. Unterbrochene Versuche können
vorübergehend verwaiste Kartenasset-Kopien hinterlassen; diese dürfen
nicht ohne Referenz-/GC-Audit gelöscht werden.

**Getrennte Update-Autorisierung:** Die gemeinsame NexBus-Transport-HMAC
genügt absichtlich NICHT mehr für Code-Update-Staging. ROBUST und jede zur
Updateverteilung freigegebene Node benötigen zusätzlich dieselbe,
separate Umgebungsvariable mit mindestens 32 UTF-8-Bytes:

    NEXVERSE_UPDATE_CONTROL_KEY=<separater-langer-zufaelliger-schluessel>

In einer geschützten Systemd-EnvironmentFile bereitstellen und die
betroffenen Prozesse kontrolliert neu starten. Nicht mit
`NEXVERSE_NEXBUS_SHARED_KEY` gleichsetzen, nicht in Git committen.
ROBUST antwortet ohne signierte Transportverbindung und diesen separaten
Schlüssel mit HTTP 503; Nodes verwerfen fremde, manipulierte, wiederholte
oder abgelaufene Aufträge. Das Zeitfenster beträgt maximal 5 Minuten
plus 60 Sekunden für geringfügig abweichende Systemuhren.
`/api/v1/nodes` zeigt nur den Ergebnisstatus, niemals den Schlüssel.
Die Aktivierung bleibt optional und nach Tests/Backup freizugeben.

**Update-Staging-Wiederholung:** Bereits vorhandene Verzeichnisse werden
gegen die verifizierte Commit-SHA/Quelle und das gewünschte Build-Flag
überprüft. Nicht prüfbare Ablagen werden abgewiesen, ungenutzte temporäre
Klone entfernt. Das Staging darf nicht innerhalb oder oberhalb des
laufenden Simulator-Checkouts liegen. Der Konfigurationsvergleich nimmt
auch offiziell ausgelieferte `*.ini.example`-Dateien auf und verweigert
symlinkende Zielpfade.

**Leere FSAssets:** Bei verfügbarer zweiter, unveränderlicher FSAssets-
Sicherung kann zusätzlich das folgende Argument angegeben werden:

    --backup-fs-root /private/verified-backup/fsassets

Das Audit bezeichnet nur solche Dateien als
`verified_backup_candidate_manual_restore_on_staging_only`,
deren SHA-256-Hash mit dem SQL-Metadatensatz übereinstimmt und deren
aktuelle FSAssets-Datei fehlt/beschädigt ist. Ein tatsächlicher Restore
erfolgt ausschließlich nach isolierter Validierung und Betreiberfreigabe.

## Warp3D: RenderMeshes mit validierten visual LODs

Bei aktiviertem `RenderMeshes = true` prüft Warp3D die im Asset
enthaltenen binären LLSD-Header und komprimierten Visual-LOD-Blöcke,
bevor der OpenMetaverse-Meshdecoder aufgerufen wird. Für die
ausgewählte LOD werden Offsets, Größen, zlib-Komprimierung, binäres
Face-Array und referenzierte Vertex-Indizes geprüft. Bei einer defekten
gewünschten LOD wird nach einer alternativen gültigen visuellen LOD
gesucht.

Gleiche Mesh-UUIDs auf einer Karte werden je LOD pro Renderdurchlauf
wiederverwendet (max. 128 dekodierte positive Cacheeinträge), wodurch
identische Mesh-Instanzen nicht mehrfach dekodiert werden. Fehlversuche
werden für denselben Kartenlauf zwischengespeichert; eine eindeutige
UUID mit Region-Prim/Position erscheint einmalig als
`[WARP3D MESH]`-Warnung.

Die Texturklassifizierung überspringt den JPEG2000-Decoder bei
erkennbaren PNG/JPEG/GIF/BMP-Payloads. **Ungültige JPEG2000-Assets
oder defekte Meshes lassen sich damit nicht automatisch reparieren.**
Auch andere Simulator-Subsysteme (z. B. Physik, Upload, Terrain)
können weiterhin eigene `Failed to decode mesh asset`-Warnungen
ausgeben. Die Dateien müssen in solchen Fällen über das separate
FSAssets-Audit und verifizierte Backups überprüft werden.

Empfohlener isolierter Performance-Vergleich:

    [Map]
    MapImageModule = "Warp3DImageModule"
    RenderMeshes = true
    TexturePrims = true
    FastMapAsyncStartup = true

Vorher/nachher vergleichen: Dauer `generate map`, Speicher-/CPU,
Anzahl `[WARP3D MESH]`-Warnungen, sichtbare Mesh-LOD-Geometrie und
Karten nach Region-/ROBUST-Neustart. Bei `RenderMeshes=false` ist die
echte Mesh-Geometrie weiterhin nicht auf der Karte sichtbar.
