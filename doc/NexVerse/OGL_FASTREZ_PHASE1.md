# OGL FastRez – Phase 1: schnellere Avatar-Wearables nach Login

Stand: 2026-10-23 · OpenGenesisLINK 0.9.3.13 Kandidat · zunächst **nicht produktiv freigegeben**

## Umfang und technische Begründung

Der Server verzögerte jede Antwort auf `OnRequestWearables` fest um `Thread.Sleep(4000)`,
obwohl die Avatar-Präsenz und ihre Appearance häufig bereits früher vorhanden sind.
Der neue Parameter `[Appearance] WearablesRequestDelayMs` ersetzt allein diese
Wartezeit im vorhandenen asynchronen AvatarFactory-Handler.

* Upstream-/Kompatibilitätsstandard: **4000 ms**, unverändert.
* NexVerse `OpenSim.ini` und `OpenSim.ini.example`: **1000 ms** als Pilot.
* Gültig: **500–4000 ms**. Ungültige Werte behalten den sicheren 4000-ms-Standard
  und erzeugen eine Warnung beim Modulstart.
* Der Handler prüft weiterhin, ob die ScenePresence existiert, bevor Wearables
  gesendet werden. Es wird nichts an Avatar-Daten, Session-Zustand,
  Hypergrid-Rechten, Bake-UUIDs oder dem AssetStore geändert.

**Keine garantierte Gesamtersparnis von drei Sekunden:** Die feste Wearables-Wartezeit
wird um drei Sekunden reduziert. Gesamt-Rezzeit hängt zusätzlich von Viewer,
Netzwerk, Cache, Asset-Server, gültigen gebackenen Texturen und Attachments ab.
Wird der Avatar weiterhin als Wolke angezeigt, sind insbesondere fehlende/
ungültige Bakes, Avatar-Inventar, Asset-HTTP-CAPS und Negative-Caching separat
zu untersuchen. Eine Wolke lässt sich nicht durch einen künstlichen "ready"-Status
wegoptimieren.

## Getrennte Staging-Prüfung (kein Eingriff in den Live-Grid)

1. Aktuellen `main`-Stand und Feature-Branch per SHA vergleichen. Den Branch
   nicht mit dem noch nicht freigegebenen OGL AssetService-PR #237 vermischen.
2. `python3 tools/ci/verify_avatar_appearance_config.py` ausführen und den
   vollständigen NexVerse-CI-/Build-Lauf abschließen.
3. In einer isolierten Region fünf kalte und fünf warme Logins mit Firestorm
   messen; jeweils mit `WearablesRequestDelayMs = 4000` (Baseline) und
   `WearablesRequestDelayMs = 1000` (Pilot). Gleiche Avatare, Regionen,
   Bandbreite und Viewer-Einstellungen verwenden.
4. Getrennt messen: Login bis Wearables erhalten, Login bis eigene Kleidung
   sichtbar, Login bis vollständig sichtbare eigene/andere Avatare,
   Login bis erste nahe Objekte und bis komplette Sichtweite geladen sind.
   Median und p95 aus mindestens fünf Wiederholungen je Konfiguration
   dokumentieren (bei fünf Werten p95 als deskriptiven Näherungswert kennzeichnen).
5. Mindestens je einen Fall mit frisch geändertem Outfit/COF, einem kalten
   Viewer-Cache, Teleport innerhalb des Grids, Grenzübergang und einem
   Hypergrid-Gast testen. Keine verlorenen Wearables, hängenden Wolken,
   falschen Outfits oder fehlenden Attachments akzeptieren.
6. Bei fehlerhaftem Login/Teleport den Parameter auf **4000** zurückstellen
   und den Simulator kontrolliert neustarten. Keine Cache-Dateien löschen,
   keine Asset-Migration starten.

### Diagnose

* Avatar bleibt Wolke: prüfen, ob `[AVFACTORY]`-Fehler, fehlende Bake-Assets,
  Rebake-Anforderungen oder HTTP-Assetfehler vorliegen.
* Avatar vollständig, Welt langsam: initiale Objektupdates, Viewer-Culling,
  Mesh-/Texture-CAPS und `fcache status` prüfen; das ist **nicht** Bestandteil
  dieser Änderung.
* Viele wiederholte Asset-Misses: Flotsam-Negative-Cache (aktuell 120 s)
  als eigenes, messpflichtiges Folgeexperiment prüfen; nicht blind deaktivieren.
* Die existierende Konfiguration `PersistBakedTextures=true`,
  `ReuseTextures=true`, `DelayBeforeAppearanceSend=1` bleibt bestehen.

## Folgephasen

**Phase 2 – World Rez:** nahe Objekte und bereits im Viewer-Cache vorhandene
Objekte bei der Initialauslieferung messen und optional priorisieren.
`SendInitialData` / `ReprioritizeUpdates` besitzen bereits Cache- und
Culling-Pfade; bevor man sie umbaut, müssen Paket-/Queue- und Objekt-Metriken
den Engpass belegen.

**Phase 3 – Avatar Rez und Bakes:** End-to-End-Telemetrie für
Appearance/Wearables/COF/Bakes/Texture-CAPS ergänzen; prüfen, ob kurze
Negative-Caching-TTLs und asynchrone Bake-Recovery tatsächlich die Wolkenzeit
senken. Keine erfundenen Bake-UUIDs oder unvalidierten Fallbacks senden.

**Abgrenzung:** Keine Änderungen an `OGLAssetConnector`, zentralen FSAssets,
Datenbank, ROBUST-Konfiguration, produktivem Cache oder Viewer-Fork.
