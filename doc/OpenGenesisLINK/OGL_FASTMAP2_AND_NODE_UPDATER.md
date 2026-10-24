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
