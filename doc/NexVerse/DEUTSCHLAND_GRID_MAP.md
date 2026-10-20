# Stadt NexVerse – Deutschland-Karte und OpenGenesisLINK-Gridplaner

Stand: 20. Oktober 2026. Implementierung im Feature-Branch; **nicht automatisch produktiv bereitgestellt**.

## Hintergrund

Der Gridplaner im World-API-Control-Center kombiniert jetzt das authentifizierte
256-m-Regionsraster (frei, belegt, reserviert, Konflikt) mit einer **optionalen
Warp3D-Bildansicht** auf den tatsächlich belegten Zellen. Regionserstellung,
Verschieben, Lifecycle und Placement-Prüfung bleiben unverändert an ihre
bisherigen Berechtigungen und Sicherheitsprüfungen gebunden. Die Kartenbilder
sind kein Beweis dafür, dass eine Zelle frei ist.

Die öffentlich zugängliche Karte verwendet OpenSim-Koordinaten, **keine
geographischen WGS84-Breiten-/Längengrade**. Auch die unter dem sprechenden
Alias `/api/v1/deutschland` bereitgestellte Karte zeigt das *gesamte*
Stadt-NexVerse-Grid in beliebigen einzeln abgefragten Ausschnitten; der Alias
filtert nicht auf echte Bundesländer und projiziert kein geographisches
Deutschland. Sollte später ein offizielles georeferenziertes
Deutschland-Terrain vorliegen, muss die Georeferenzierung separat modelliert werden.

## Öffentliche API

Origin: der tatsächliche World-API-Host, im Stadtportal typischerweise
`https://world.stadt-nexverse.de`. Der genutzte Reverse Proxy muss
`/api/v1/deutschland` mit Unterpfaden und Bildern an ROBUST weiterleiten.

| HTTP | Pfad | Antwort |
| --- | --- | --- |
| GET | `/api/v1/deutschland` | JSON eines Standard-Ausschnitts mit `bounds`, `regions`, `tile_url_template` |
| GET | `/api/v1/deutschland?min_x=995&max_x=1010&min_y=1000&max_y=1015` | Maximal 16 × 16 Grid-Zellen pro Ausschnitt |
| GET | `/api/v1/deutschland?q=Freiburg` | Namenssuche, maximal 32 sichtbare Treffer |
| GET | `/api/v1/deutschland/view` | Responsive, einbettbare HTML-Karte mit Pan/Zoom/Suche/Teleport |
| GET | `/api/v1/deutschland/tiles/1/1000/1010.jpg` | Warp3D-Bild einer 256-m-Zelle als JPEG |
| GET | `/api/v1/deutschland/tiles/2/1000/1010.jpg` | Übergeordnete JPEG-Kachel für 2 × 2 Zellen, sofern beide Koordinaten durch 2 teilbar |
| GET | `/api/v1/deutschland/tiles/{zoom}/{x}/{y}.jpg` | Zoom-Level 1–8: Breite je Level = 2^(zoom−1) Zellen; x und y müssen zum Level ausgerichtet sein |

* **Komplette Karte:** Die API stellt *alle über den GridService erreichbaren,
  veröffentlichten, aktiven Regionsbereiche* durch Navigation/Pagination der
  Fenster bereit. Es gibt **keine unbeschränkte Anfrage**, die eine riesige
  Karte in einen einzelnen RAM-intensiven Bitmap-Export zusammensetzt.
* **Bildmaterial:** Bereits gerenderte Maptiles aus dem bestehenden
  `MapImageService`, der die Warp3D-Kacheln der Simulatoren erhält. Keine
  zusätzlichen Warp3D-Renderer auf ROBUST. Die Level 2–8 stammen aus den
  bestehenden OpenSim-Mehrfachauflösungen und werden zeitversetzt generiert.
* **Kacheln:** Einzig vorgegebene Dateinamen und die öffentliche Scope-ID
  `UUID.Zero` werden abgefragt. Koordinaten, Zoom und Alignment werden
  validiert. Ohne sichtbare Region folgt 404, ohne MapImageService 503.
  Die implementierte MapImageService-Schnittstelle kann bei fehlender
  Datei eine Wasser-/Fallback-Kachel zurückliefern.
* **Privatsphäre:** Keine Simulator-Netzadressen, Eigentümer, NodeIDs oder
  reservierte private Placement-Einträge. Administrative Gridplanung
  bleibt hinter `regions:read` / `regions:manage`.
* **Cache:** Regionsmetadaten 20 Sekunden, JPEG-Kacheln 60 Sekunden. Für
  deutlich größere Grids ist ein vorgeschalteter bildgeeigneter CDN-Cache
  oder ein eigenes Tile-Caching mit Limits empfehlenswert.
* **Sicherheit:** Nur GET, keine Dateipfad-Parameter, maximal 2 MiB je Bild.
  Der bestehende World-API-Rate-Limiter bleibt aktiv.
* **VarRegions:** Die Regionseigenschaften `cells_x` und `cells_y`
  zeigen die tatsächliche Größe. Die Bildkacheln werden je Basiszelle
  geliefert, entsprechend der bestehenden Warp3D-Upload-Pipeline.

Beispielantwort (Struktur):

```json
{
  "cell_size_meters": 256,
  "mode": "viewport",
  "bounds": {"min_x": 995, "max_x": 1010, "min_y": 1000, "max_y": 1015},
  "tile_url_template": "/api/v1/deutschland/tiles/1/{x}/{y}.jpg",
  "max_window_axis": 16,
  "zoom_levels": 8,
  "regions": [
    {
      "region_id": "00000000-0000-0000-0000-000000000002",
      "name": "Beispielregion",
      "grid_x": 1000,
      "grid_y": 1010,
      "cells_x": 1,
      "cells_y": 1,
      "size_x": 256,
      "size_y": 256,
      "maturity": 0,
      "teleport_uri": ""
    }
  ],
  "count": 1
}
```

Das ist **nur ein Schema-Beispiel**, keine Behauptung über tatsächlich
vorhandene Regions-UUIDs oder aktuelle Antwortdaten.

## MapLibre GL JS als Karten-Renderer

Die einbettbare Ansicht unter `/api/v1/deutschland/view` sowie die
bestehende `/api/v1/world-map/view` verwenden jetzt **MapLibre GL JS 6.15.0**
als echte Rasterkarten-Engine. Damit übernimmt MapLibre Pan, Zoom,
Mausbedienung, interaktive Regionenpolygone, Navigationsbuttons und
Suchsprünge. Die eigenen Warp3D-Rasterbilder werden über eine
standardisierte XYZ-Schicht dargestellt:

`GET /api/v1/deutschland/xyz/{z}/{x}/{y}.jpg`

Die XYZ-Schnittstelle ist nur ein **Kachelindex-Adapter** für den vorhandenen
OpenSimulator MapImageService: z = 24 entspricht nativem Level 1 (eine
256-m-Zelle pro Kachel); z = 17 entspricht Level 8 (128 Zellen pro Kachel).
Intern gilt `zoom = 25 - z`; der Ausschnitt wird mit einem festen
Offsetsystem in die zulässigen XYZ-Indizes projiziert. Es handelt sich um
**synthetische** Mercator-Koordinaten; dargestellte Geokoordinaten
dürfen niemals als echte Standorte/Stadtgeografie interpretiert werden.

Der XYZ-Server prüft Zoom (17–24), gültige nichtnegative XYZ-Indizes,
Grenzen und Existenz veröffentlichter Regionen und reicht nur bereits
vorliegende JPEG-Kacheln durch. Keine freien Dateipfade, administrativen
Metadaten, MapLibre-/Google-API-Schlüssel oder unnötigen Renderjobs.

**Externe Bibliothek:** MapLibre 6.15.0 wird in dieser Ausbaustufe über
eine fest versionierte unpkg-ESM-/CSS-URL bezogen. Für streng abgesicherte
Produktivportale MapLibre CSS/JS später in der Webauslieferung selbst
hosten und die eigene Content-Security-Policy entsprechend konfigurieren.
Es werden keine Google-Satelliten-/OSM-Kartenkacheln abgerufen.

## Stadtportal-Einbindung

Der einfachste Einstieg ist ein Link oder ein gleichoriginiges iframe zu
`/api/v1/deutschland/view`. Bei separater Stadtportal-Origin entweder
die Kartenansicht direkt als iframe einbetten (wenn Frame-Schutz/Proxy
dies zulässt) oder im Portal-Backend die JSON-/JPEG-Endpunkte per
konfiguriertem Reverse Proxy unter einer eigenen, gleichoriginigen Route
freigeben. Keine API-Keys an den Browser schicken. Für eine eigene Karte:

1. `GET /api/v1/deutschland` abrufen und die Rückgabe `bounds`,
   `regions` und `tile_url_template` auswerten.
2. Bei Pan/Zoom jeweils das sichtbare Gridfenster (max. 16 × 16) anfordern,
   **nicht** alle möglichen Millionen Gridzellen vorab laden.
3. Rasterbilder pixelgenau platzieren: Y-Achse für den Browser umkehren;
   X wächst ostwärts, Grid-Y nordwärts.
4. Regionseinträge samt `cells_x/cells_y` darüberlegen, nur sichere
   DOM-`textContent`-Operationen verwenden und Teleportlinks wie
   vorhanden validieren.

## Google Maps / Google Maps Platform

Die Google-Maps-API ist für *echte Erdkoordinaten* konzipiert. Auf einer
rein virtuellen Gridkarte ergeben sich Probleme durch WGS84/Mercator,
abweichende Skalen, Lizenz-/Abrechnungsregeln und Google-Attribution.
Google-Basiskarten dürfen nicht einfach heruntergeladen und als
Warp3D-Kacheln auf eigene Tile-Server umkopiert werden.

Empfohlene Basis: **Leaflet mit CRS.Simple**, oder MapLibre mit einer
bewusst definierten planaren Projektion. Dadurch erhält Stadt NexVerse
Google-Maps-artiges Pan/Zoom, Marker, Suche und Layer-Umschaltung
auf den **eigenen** Warp3D-Bildern, ohne Google-Dienste vorauszusetzen.
Eine echte Erde/Deutschland-Überlagerung mit Google Maps ergibt erst
Sinn, wenn ein explizites und überprüftes Transformationsmodell
virtuelle Grid-Koordinaten auf Lat/Lon projiziert.

## Rollout / Tests

1. Auf PR-CI warten und alle Build-/Regression-Gates überprüfen.
2. Sicherung der produktiven .ini und der MapTile-Daten durchführen.
3. Nur nach Freigabe ROBUST mit neuem Build starten; keine Datenbankmigration.
4. `/api/v1/deutschland` muss JSON, `/api/v1/deutschland/view`
   eine durchsuchbare Karte und ein bekannter Kachelpfad `image/jpeg`
   liefern.
5. Negative Tests: nicht ausgerichtete LOD-Kacheln, ungültiges Zoom,
   extreme Zahlen, fremde Scope-IDs und unbekannte Routen ablehnen.
6. Administratoren testen Gridplaner auf kleine/große Viewports,
   Filter, selektierte Zelle, Placement-Prüfung, VarRegions sowie
   unveränderte Rechteprüfung für Regionserstellung/-verschiebung.
7. Sim ohne Warp3D-Kachel, abgeschaltete Region, verzögerte Multi-LOD-
   Regeneration und Neustart des MapImageService testen.
8. Einbettung im Stadtportal hinter dem tatsächlichen Reverse Proxy
   und Cache-/CORS-/Frame-Regeln separat überprüfen.

**Abgrenzung:** Die Referenzkarte von Dorenas World konnte automatisiert
nicht geladen werden. Die Funktionen orientieren sich daher an der
gewünschten Interaktion (Pan/Zoom, Regionensuche, Kartenbilder, Planung),
nicht an einer behaupteten pixelgenauen Nachbildung ihres Quellcodes
oder Designs. Live-Auslieferung und Build-Abnahme sind durch
GitHub-Änderungen allein nicht bewiesen.
