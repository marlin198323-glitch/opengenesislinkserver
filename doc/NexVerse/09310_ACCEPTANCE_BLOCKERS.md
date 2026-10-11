> **ARCHIV – nicht die aktuelle RC1-Checkliste (12.10.2026).**
> Die unten dokumentierten Experience-/NAV-/Firestorm-Tests und
> 11/38-Haken stammen von einem historischen Entwicklerbuild.
> Ab sofort gelten ausschliesslich die **22 RC1-/23 Stable-Pflichtfälle**
> aus [09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md).
> NexExperiences, OGL Pathfinding 2.0 und OGL My Outfits 2.0 sind vertagt.
> Die untenstehenden Aktivierungsbeispiele sind **nicht** fuer
> den 0.9.3.10-RC-Betrieb gedacht.

# 0.9.3.10 – Abnahme-Blocker nach Firestorm-7.2.4-Pruefprotokoll

> **Historische Momentaufnahme:** Nach der Scope-Entscheidung vom 11.10.2026 entfallen OGL Pathfinding und NAV01–NAV08 vollstaendig aus dem aktuellen Server-Release. Alte NAV-Tests, INI-Auszüge und deren Blockerbeschreibung gelten nicht mehr. Die aktuelle Pflichtmatrix umfasst 40 statt 48 Faelle. Verbindlich: `09310_RC1_RELEASE_READINESS.md`.


**Hinweis 11.10.2026 nach PR #158/#159:** Diese Datei ist eine **historische
Momentaufnahme fuer Build a6778c9**, kein aktueller Blocker-Beweis
fuer HEAD. Der ehemals GET-only Viewer-CAP kann im aktuellen
Quellcode bei Opt-in `FirestormManageCaps` kostenfrei erwerben und
grundlegende Profile bearbeiten. Die Live-Abnahme fehlt unveraendert.
Aktueller Pflichtumfang und Sperren: [09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md).
Die neue Experience-Funktion benoetigt **drei** getrennte Serviceschluessel,
siehe [FIRESTORM_EXPERIENCE_ACQUIRE_09310.md](FIRESTORM_EXPERIENCE_ACQUIRE_09310.md).
Die folgenden Befunde beschreiben weiterhin ausschliesslich den damaligen Test.

**Stand:** 11.10.2026, gemeldeter Build `a6778c9`, Region
`OGL Developer Gen1`, Firestorm `7.2.4.80712`.
Protokoll: **11/38 manuell abgehakt, 27/38 offen**.
Dieser Bericht ist keine Grid-Messung und keine Stable-Freigabe.

## Einordnung

- `EXP01–EXP08` (8), `LSL01–LSL06` (6) und `NAV01–NAV07` (7):
  **NOT RUN / BLOCKED**, nicht pauschal als reproduzierter Codefehler melden.
- `DB01–DB06` (6): ebenfalls noch kein Live-Abnahmebeleg.
- Die gemeldeten REL01 (Firestorm Zwei-Avatar-Abnahme), REL02
  (Experience-Sicherheit) und REL07 (RC1-Freigabe) sind mit diesen
  nicht durchgefuehrten Tests **inhaltlich widerspruechlich**. Der
  Betreiber muss die Markierungen und konkreten Nachweise neu bewerten.
- Die GitHub-CI fuer `a6778c9` war gruen; dies beweist weder
  die Live-CAPS-Auslieferung noch eine echte Firestorm-Abnahme.

## Konkrete Repository-Befunde

1. `bin/OpenSim.ini` enthielt `[NexExperiencesViewer] Enabled=true`,
   aber keine `FirestormReadCaps`, `FirestormPermissionCaps`,
   `ScriptPendingConsent`, `NativeExperiencePrompt` und
   `ViewerPermissionsApiKey` Eintraege. Der Modulcode behandelt
   fehlende opt-in-Bools als `false`.
2. `bin/OpenSim.ini` enthielt keine `[OGLPathfinding]` und
   `[OGLNpcNavigation]` Sektionen, folglich native Module
   standardmaessig deaktiviert. Die CI-Example-Datei hatte bereits
   `[OGLPathfinding] Enabled=false`.
3. `NexExperienceModule.RegisterFirestormReadCaps` registriert
   `AgentExperiences` **nur GET**. Der unveraenderte SL/Firestorm-
   Viewer kann ueber diesen CAP `POST` zum Anlegen einer Experience
   senden; es fehlt im Server die dafuer autorisierte Bearbeitung.
   `EXP01` ist daher in seiner Formulierung `Experience im Viewer
   anlegen` **noch nicht implementiert**. Anlegen per autorisierter
   World API ist vorhanden, aber kein Ersatz fuer vollen Viewer-
   Editor-Paritaetsnachweis.
4. Das Firestorm-Pathfinding-Menue erwartet `RetrieveNavMeshSrc` mit
   kompatiblem NavMesh. **OGLPathfinding allein entsperrt es nicht**;
   das graue Firestorm-Menue ist bis zum zukuenftigen Viewer-/NavMesh-
   Projekt eine dokumentierte Einschraenkung.

## Read-only-Vorpruefung auf dem Developer-Simulator

Nach Update des Quellcodes, **ohne** Werte echter Schluessel im Chat:

```bash
python3 tools/diagnostics/ogl_voice_pathfinding_preflight.py \
  --sim-config bin/OpenSim.ini \
  --robust-config bin/Robust.HG.ini
```

Ausgaben `action`: fehlende/eingeschraenkte Konfiguration.
`configuration-only`: konfiguriert, aber noch kein Live-Test.
`known-limitation`: derzeit bewusst nicht implementiert.

Der Scanner liest die gewaehlte Datei; er kann **nicht** garantieren,
dass der laufende Prozess exakt dieselbe Datei und dieselben
`[Architecture]`/Nini-Includes benutzt. Bei abweichendem Deployment
die **tatsaechlich geladene** Simulator-Konfiguration uebergeben.
Er testet weder das Netz noch die Credentials und gibt keine Secret-
Werte aus.

## Isolierte Freischaltung zur Abnahme

**Nur OGL Developer Gen1 oder einen gesonderten Test-Simulator.**
Backup und sicheren Restore bestaetigen. Bereits vorhandene Abschnitte
bearbeiten, nicht doppelt anfuegen. Es muessen zwei unterschiedliche,
minimal berechtigte Schluessel in der *laufenden* Simulatorumgebung
korrekt aufgeloest werden.

```ini
[NexExperiencesViewer]
    Enabled = true
    WorldApiBaseUrl = "https://world.stadt-nexverse.de"
    ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
    ViewerPermissionsApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY}"
    FirestormReadCaps = true
    FirestormPermissionCaps = true
    ScriptPendingConsent = true
    NativeExperiencePrompt = true

[OGLPathfinding]
    Enabled = true
    ; Bestaetigte 3D-Oberflaechen nur mit kompatibler Physik aktivieren.
    UseVerifiedLayeredSurfaces = false
    PhysicsRaycastLayeredSurfaces = false
    FirestormNavMeshCaps = false

[OGLNpcNavigation]
    Enabled = true
```

Das ist eine *erste* Terrain-/NPC- und Experience-CAPS-Abnahme, nicht
automatisch die Freigabe fuer `NAV02`/`NAV03`. Deren Bruecken- und
Treppen-Szenarien erfordern einen nachgewiesenen, physikalisch
verifizierten Oberflaechen-/Transitionsprovider, ansonsten **NOT RUN**.
Nie `FirestormNavMeshCaps` zum kosmetischen Entgrauen aktivieren.

Nach Konfigurationspruefung Simulator **kontrolliert neu starten**,
Firestorm aus-/einloggen, die angekuendigten Screenshots aufzeichnen,
anschliessend zuerst `EXP02`, `EXP03`, `EXP04` und die
LSL-Smoke aus `tools/diagnostics/ogl_experience_firestorm_smoke.lsl`
an einer per zentraler API wirklich angelegten/berechtigten Experience
testen. Aktiver Zweitavatar darf kein Admin sein.

Ein bereits beim Einloggen nicht registriertes CAP kann nicht durch
nachtraegliches Aendern einer INI im laufenden Viewer auftauchen.
`EXP01` (Firestorm Create) bleibt bis zum sicheren POST-
`AgentExperiences`-Implementierungs- und E2E-Test offen.

## Naechste technische Gates

- Viewer-Creation/Edit: fehlende Firestorm-Protokolloperationen
  bestimmen, mit Root-Agent-Session- und Owner-ID-Bindung absichern,
  separat regressions- und live testen.
- Firestorm ALLOW/BLOCK/FORGET: beide Avatare, stabile CAPS,
  Timeout und Re-Login nachweisen.
- LSL KV-CAS und Consent-Lebenszyklus mit Scriptreset/Regionwechsel
  testen; Persistenz nach Robust-Restart verifizieren.
- Native NPC-Routen mit C#-Harness und echter Region ausloesen;
  Layered-Physik nur nach Provider-Nachweis.
- DB01–DB06 plus Last-/Restore-Proben, REL01/02/07 neu attestieren;
  erst danach RC1 und Stable diskutieren.

**Ein Host ist derzeit fuer Live-Ausfuehrung nicht verbunden:**
ein hier verfuegbares Remote-Desktop-Commander-Gerät war zum
Pruefzeitpunkt offline. Kein echter Simulator wurde in diesem
Arbeitsschritt umkonfiguriert.
