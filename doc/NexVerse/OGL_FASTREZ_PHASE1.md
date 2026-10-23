# OpenGenesisLINK 0.9.3.13 – OGL FastRez (kleines Performance-Paket)

Stand: 23.10.2026 | PR #281 | separater Simulator-Patch, **nicht** Teil von PR #237

## Genau zwei Änderungen

1. **Avatar-Wearables:** Die feste Wartezeit vor der Antwort auf
   `RequestWearables` beträgt im NexVerse-Profil jetzt 1.000 statt
   4.000 ms. `[Appearance] WearablesRequestDelayMs = 1000`.
   Gültige Werte: 500–4000 ms; bei ungültigen Werten fällt die
   Implementierung auf den sicheren Standard von 4000 ms zurück.
   Das reduziert die feste Serverwartezeit dieses Request-Pfads um 3 Sekunden,
   **nicht zwingend** die Zeit bis zum vollständig gerenderten Avatar.

2. **Weltaufbau:** `[InterestManagement] FastRezNearbyFirst = true`
   reiht beim initialen Regionseintritt Objekte innerhalb 64 m um die
   Avatarposition zuerst ein; danach kommen die übrigen Objekte.
   Die vorhandene Distanz-Priorisierung, der Viewer-Objektcache,
   das Updateformat und die Zugriffsregeln bleiben unverändert.
   Dies gilt für den bestehenden vollständigen Objekt-Synchronisationspfad,
   nicht für den gesonderten Distanz-Culling-Pfad.
   Der OpenSim-Standard bleibt `false`.

### Login-Chat (FastRez 2.0)
Nach dem erfolgreichen normalen Grid-Login und nach Verarbeitung der Viewer-CAPS-Seeds
sieht **nur der eingeloggte Avatar** genau einmal die lokale Meldung:

`OpenGenesisLINK FastRez 2.0 aktiviert`

Die Meldung wird ausschließlich bei aktiviertem `FastRezNearbyFirst` über
`ChatTypeEnum.Owner` direkt an den Viewer geschickt. Keine Region-Broadcasts,
keine Wiederholung bei Teleports oder Regionswechseln. Hypergrid-Ankünfte
mit `ViaHGLogin` sind von dieser einmaligen Grid-Login-Meldung ausgeschlossen.
Der Text ist eine Aktivierungsinformation, kein Versprechen über eine feste
gemessene Beschleunigung.

Kein neuer Dienst, keine Datenbank, keine Assets-Migration, kein Redis,
keine Änderungen an Hypergrid-Autorisierung oder Avatar-Bake-Daten.
Der noch offene OGL AssetService PR #237 ist unabhängig und unverändert.

## Einmaliger Test und Abschluss

* CI: `python3 tools/ci/verify_avatar_appearance_config.py`
  und NexVerse-Build müssen erfolgreich sein.
* Im **Staging** je drei Loginläufe mit Firestorm in derselben Region:
  einmal beide FastRez-Einstellungen aktiv und einmal deaktiviert
  (WearablesRequestDelayMs=4000; FastRezNearbyFirst=false).
  Vergleichbar sind nur Läufe mit identischem Viewer-Cache, Outfit,
  Region und Netzwerk.
* Zwei Zeiten notieren: **bis eigener Avatar sichtbar** und
  **bis erste Gebäude um den Loginpunkt sichtbar**.
  Zusätzlich einen zweiten Avatar und einen HG-Gast kontrollieren,
  insbesondere Outfit-Wechsel und Teleport.
* **Freigabe nur bei sichtbarem Vorteil ohne Fehler.** Keine versprochenen
  Prozentwerte oder fixen Sekunden für den gesamten Bildaufbau.
  Bei fehlenden Assets, dauerhaft roter Wolke, fehlerhaftem Outfit
  oder Objektauslassungen nicht freigeben.
* **Rollback:** Beide Werte in der Simulator-Konfiguration auf
  `WearablesRequestDelayMs=4000` und `FastRezNearbyFirst=false`
  zurückstellen und Simulator kontrolliert neu starten.

## Grenzen

Eine hartnäckige rote Wolke durch fehlende oder ungültige Bake-Assets,
fehlerhafte Wearables oder gescheiterte Texture-CAPS wird von FastRez
nicht repariert. Solche Fälle separat als Fehler untersuchen.

**Fertig-Definition:** CI grün + kurzer Stagingvergleich + keine
Darstellungsregressionen. Kein weiteres FastRez-Teilprojekt vorgesehen.
