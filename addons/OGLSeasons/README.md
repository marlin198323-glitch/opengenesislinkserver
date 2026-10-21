# OGL Seasons – Jahreszeiten & Wetter für Stadt NexVerse (Deutschland)

**Stand:** 21.10.2026 · Mergevorbereitung/optionaler Addon-Betrieb
`feature/ogl-seasons-germany-weather-addon-20261021` · Phase 1 / technisch
testbar, **nicht** für unkontrollierten Produktiveinsatz freigeschaltet.

## Ziel

Ein optionales OpenGenesisLINK-Regionenmodul, das auf der **nördlichen
Halbkugel nach dem deutschen meteorologischen Jahreszeitenkalender** arbeitet
und pro Region über die gesamte Stadt-NexVerse-Welt konsistente,
zeitlich stabile Wetterzustände bereitstellt. Ausgaben und Module auf Deutsch.

| Jahreszeit | Deutsche meteorologische Monate | Beispiele |
|---|---|---|
| Frühling | März bis Mai | sonnig, wechselhaft, Regen, Schauer, Gewitter |
| Sommer | Juni bis August | Sonne, Wärme/Hitzewelle, Starkregen, Gewitter, Hagel |
| Herbst | September bis November | Nebel, Nieselregen, Wind, Sturm, Regen |
| Winter | Dezember bis Februar | Frostnähe, Schnee, Schneesturm, Regen, Nebel |

Die Umschaltung erfolgt um **00:00 Uhr Europe/Berlin** (MEZ/MESZ),
nicht nach der UTC-Monatsgrenze. Die astronomischen Jahreszeiten haben
bewegliche Grenztermine und sind nicht Bestandteil dieser ersten Stufe.

**Wichtig:** Die implementierten Wahrscheinlichkeiten sind eine synthetische,
reproduzierbare Simulation mit groben deutschen Temperaturankern. Das Addon
bezieht **keine Live-DWD-Daten** und behauptet keine echte Wettervorhersage.

## Funktionsumfang Phase 1

- eigenständiges `INonSharedRegionModule`, pro Region getrennter Wetterzustand;
- standardmäßig **deaktiviert**, optionale Regions-Whitelist;
- optionale `WeatherZone`: eine zusammenhängende Wetterlage über mehrere Simulatoren;
- `TemperatureOffsetC`: lokale Temperaturkorrektur für Höhenlagen (-15 bis +15 °C);
- Zustandsberechnung in 30-Minuten-Fenstern (konfigurierbar, 5–1440 Minuten,
  nur Teiler von 1440), mit regionalem Seed und stabilen Wetterwerten;
- zwölf Wetterarten: heiter, bewölkt, Nieselregen, Regen, Starkregen,
  Schneefall, Schneesturm, Nebel, Gewitter, Sturm, Hagel, Hitzewelle;
- Temperatur in °C, Wind in km/h, Luftfeuchtigkeit und Stärke 0–100 %;
- Schutz vor Schneefall bei synthetischer Temperatur über 2 °C;
- `IOglSeasonsService.Current` und `StateChanged` für spätere Addons;
- optionales Region-Chatprotokoll über `IWorldComm`, ausschließlich
  auf einem negativen Kanal, ohne IM- oder Avatar-Chatspam;
- LSL-Partikel-Emitter für Regen/Schnee/Hagel/Sturm/Gewitter mit begrenzter Partikellast und Senderprüfung, der
  ausschließlich **lokale Partikel** erzeugt und bei Ausfall nach
  zehn Minuten den Niederschlag abschaltet;
- LSL-Wetterinformationstafel als Beispielintegration;
- keine automatischen Änderungen an Terrain, Eigentum, Windphysik,
  Wasserstand, Region-EEP, Objekten oder Parzellen.

### Technische Grenze der Viewer

OpenSim und Firestorm bieten **keinen universellen regionsweiten
Wetter-Schalter**, der automatisch alle Viewer Regen-/Schneepartikel
rendern lässt. Für sichtbaren Niederschlag werden **Wetter-Emitter-Prims**
mit LSL-Skripten benötigt. Große VarRegions benötigen mehrere verteilte
Emitter. `Nebel`, `Hitzewelle` und `Sturm` sind im Kern bereits
Wetterdaten. Flächendeckende EEP-Wetterstimmungen, dynamische
Lichtblitze, Boden-Schneedecken oder physische Sturmeinflüsse
sind Folgephasen und **nicht in Phase 1 implementiert**.

## Installation auf Test-Simulator

1. Branch auschecken und mit aktuellem `main` abgleichen.
2. Build ausführen:
   ```bash
   ./runprebuild.sh
   ./compile.sh
   # oder wenn die OpenSim-Bibliotheken bereits gebaut sind:
   bash addon-modules/OGLSeasons/build.sh Release
   ```
3. `addons/OGLSeasons/OGLSeasons.ini.example` nach
   `bin/config-include/OGLSeasons.ini` kopieren und diese Datei
   **explizit** über die aktiven Simulator-INI-Includes einbinden.
4. `Enabled = true` nur auf einem Testsimulator setzen.
5. Optional `AllowedRegions = "OGL Developer Gen1,Freiburg Sued"`
   einstellen (Regionsnamen exakt, Groß-/Kleinschreibung egal).
6. Ergänze `Include-OGLSeasons = "config-include/OGLSeasons.ini"` im aktiven Include-Bereich `[Architecture]` der Simulator-Konfiguration, falls die Datei nicht bereits eingebunden ist. Bestehende Includes unbedingt erhalten.
7. Simulator neu starten und in dessen Log nach
   `[OGL-JAHRESZEITEN]: Region ... aktiviert` suchen.
8. In einem geeigneten Weather-Emitter-Prim `WeatherEmitter.lsl`
   einfügen. Prim über dem gewünschten Gebiet positionieren;
   gegebenenfalls mehrere Emitter einsetzen. Die Skriptdatei
   `WeatherInfoBoard.lsl` eignet sich für eine Wetteranzeige.
9. Emitter-`CHANNEL` und Modul-`LSLChannel` müssen identisch sein.
   Bei deaktiviertem Modul bleibt der Emitter ohne Niederschlag.

## Konfigurationsvertrag

```ini
[OGLSeasons]
Enabled = false
AllowedRegions = ""
WeatherIntervalMinutes = 30
WeatherZone = ""
TemperatureOffsetC = 0
Seed = 2026
CheckSeconds = 60
BroadcastSeconds = 180
LSLChannel = -835011
```

`WeatherZone` muss für benachbarte Regionen auf allen beteiligten Simulatoren identisch sein (z. B. `Freiburg`). Leer bedeutet unabhängige Wetterzonen. Auf allen Simulatoren müssen `Seed`, `WeatherIntervalMinutes` und die Systemzeit übereinstimmen. `TemperatureOffsetC` erlaubt Gebirgs-/Küstenklima, kann jedoch den simulierten Niederschlag (Schnee/Regen) abweichen lassen. Der UTC-Zeitstempel der doppelten Stunde beim Winterzeitwechsel berücksichtigt den jeweils tatsächlichen UTC-Offset.

`AllowedRegions` gilt für die Region auf dem jeweiligen Simulator, nicht
für alle Grids global. Ein leerer Wert erlaubt alle Regionen des Prozesses,
wenn `Enabled = true` gesetzt ist. Unzulässige Einstellungen für Intervall
oder Chatkanal deaktivieren das Modul statt den Simulator abzubrechen.
Die Simulationswerte ändern sich in Zeitfenstern; das Broadcastintervall
ist nur ein Heartbeat für frisch hinzugefügte LSL-Skripte.

## Nachrichtenprotokoll OGLSEASONS/1

```text
OGLSEASONS|1|1792569600|herbst|regen|57|11|15|84
```

Felder: Protokoll, Version, UTC-Unix-Sekunden des Wetterfensters,
Jahreszeit, Wetterart, Stärke (0–100), Temperatur (°C), Wind (km/h),
relative Feuchtigkeit (%). Die Zahlen sind ein **Strukturbeispiel**.
Sendername `OGLSeasons`, Standardkanal `-835011`.
Das Protokoll ist nicht kryptografisch authentifiziert: Es darf
nur unverfängliche visuelle Effekte auslösen, niemals Inventarzugriffe,
Geldtransaktionen, Estate-Aktionen oder Benutzerrechte ändern.

## Leistung und Betrieb

Das Modul hängt sich an den regionalen `OnFrame`-Event, führt pro
Frame aber lediglich eine Zeitprüfung aus. Die eigentliche
Wetterberechnung erfolgt höchstens alle `CheckSeconds`; LSL-Signale
werden höchstens alle `BroadcastSeconds` gesendet. Die Rechenlast
der **Partikelemitter** hängt von ihrer Anzahl und LSL-Burstfrequenz ab:
Emitter zunächst nur an zentralen Orten aktivieren und FPS prüfen.
In `RemoveRegion` und `Close` wird das Event abgemeldet, um
verwaiste Zustände zu vermeiden.

## Roadmap (separat zu entwickeln)

1. **Phase 2 – Viewer-EEP:** freigegebene, versionierte Himmel-/Nebelpreset-
   Assets; opt-in und sichere Wiederherstellung ursprünglicher EEP-Zustände,
   Parzellenrechte respektieren; keine pauschale Überschreibung.
2. **Phase 3 – Vegetation und Boden:** Jahreszeiten-Varianten für
   ausgewiesene Objekte, Schnee-/Laubtexturen mit expliziter Besitzer-
   Zustimmung, niemals fremde Contents überschreiben.
3. **Phase 4 – Wetterphänomene:** synchronisierte Blitze/Donner,
   Nebelvolumen, Böen und sichere hydrologische Integration mit HydroEngine.
4. **Phase 5 – Atlas/Portal-API:** regionale Wetterdaten, Wetterwarnstufen,
   historische Zustände, ggf. externer Wetterprovider, Caching und
   Rechte-/Ratenbegrenzung. Kein ungesicherter HTTP-Endpunkt.
5. **Phase 6 – Tests:** echte Firestorm-Viewer-Tests, VarRegion-Übertritte,
   24h-Dauerlauf, Neustart, Sommerzeit, viele simultane Emitter.

## Test- und Freigabeprozess

- `python3 tools/ci/verify_ogl_seasons.py` prüft das
  deaktivierte Defaultprofil, Modul-Registrierung und Buildvertrag.
- `dotnet run --configuration Release --project
  tools/ci/OglSeasonsRegression/OglSeasonsRegression.csproj` prüft
  deutschen Jahreszeitenwechsel, Sommerzeit, Stabilität und Grenzen.
- `bash addon-modules/OGLSeasons/build.sh Release` kompiliert das Modul
  mit dem OpenGenesisLINK-Region-Framework.
- PR-CI darf die funktionierende `main`-Version nicht verändern.
- **Ein grüner Build bedeutet noch keine nachgewiesene Partikelwirkung im
  Viewer**. Inworld-Testergebnis und Speicher-/Lagprofil separat abnehmen.

Dateien:
`SeasonWeatherModel.cs` (Modell),
`OglSeasonsRegionModule.cs` (Regionen-Addon),
`WeatherEmitter.lsl` und `WeatherInfoBoard.lsl` (optionale Inworld-Effekte),
`OGLSeasons.ini.example` (Konfiguration).
