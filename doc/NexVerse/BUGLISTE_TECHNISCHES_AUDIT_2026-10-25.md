# OpenGenesisLINK – Bugliste aus dem technischen Audit vom 25.10.2026

**Auditbasis:** statische, risikoorientierte Prüfung ausgewählter Pfade von `main` nach PR #300 (`9b4461a5`).  
**Umfang:** 7 × P1, 14 × P2 und 3 gesonderte Abnahme-/Dokumentationspunkte.  
**Status:** **Offen / noch nicht einzeln reproduziert oder behoben.** Ein Codebefund ist kein Nachweis einer tatsächlich ausnutzbaren Produktionslücke. Kein Pentest, keine Live-SQL-/FSAssets-Prüfung, kein vollständiger Zeile-für-Zeile-Audit.

Diese Liste ist Bestandteil der [Roadmap](ROADMAP.md). IDs sind stabil und sollen bei einzelnen Fix-PRs, GitHub-Issues und Tests übernommen werden. Bereits existierende Release-Meilensteine werden dadurch nicht umnummeriert.

## P1 – Sicherheit und Integrität

### OGL-001 · Node-Control ohne ROBUST-spezifische Signatur

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund / bedingtes Risiko
- **Fundstelle:** [`NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs#L1969-L2026)
- **Befund:** maintenance_on/off, drain und resume prüfen nur Bus-Nachricht und Ziel-Node; Teilnehmer mit Schreibzugriff auf den Bus könnten Managementaktionen nachbilden.
- **Maßnahme:** Separates signiertes Node-Control-Envelope mit Aktion, Ziel, Zeitfenster und Replay-Schutz; Mehr-Node-Negativtests.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-002 · OAR-Verwaltung ohne separate Betreiberfreigabe

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund / bedingtes Risiko
- **Fundstelle:** [`NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs#L1758-L1810)
- **Befund:** OAR-Aufträge prüfen Identifier und Dateinamen, jedoch keine originäre ROBUST-Adminsignatur; die Wirkung ist von weiteren OAR-Berechtigungen abhängig.
- **Maßnahme:** Verbindliche Node-seitige Signatur-, Aktions- und Dateipfadprüfung; Archiv-/Restore-Berechtigungstests.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-003 · IAR-Verwaltung ohne separate Betreiberfreigabe

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund / bedingtes Risiko
- **Fundstelle:** [`NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs#L1677-L1735)
- **Befund:** IAR-Aufträge prüfen Ziel, Region, Benutzer und Pfade, nicht aber die privilegierte Herkunft des Ereignisses.
- **Maßnahme:** Signierte Betreiberfreigabe, Nutzer-/Inventarberechtigungen, Pfad-/Rate-Limits und Audit-Logs.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-004 · KeyframeMotion deserialisiert mit BinaryFormatter

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/Framework/Scenes/KeyframeMotion.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/Framework/Scenes/KeyframeMotion.cs#L308-L323)
- **Befund:** Persistierte Bewegungszustände werden mit BinaryFormatter.Deserialize eingelesen; Erreichbarkeit über konkrete Datenpfade separat prüfen.
- **Maßnahme:** Versioniertes typisiertes Format und Offline-Konverter für vorhandene DB-/OAR-Daten; Restore-/Roundtrip-Tests.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-005 · YEngine verarbeitet BinaryFormatter-Objektgraphen

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/ScriptEngine/YEngine/XMRInstAbstract.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/ScriptEngine/YEngine/XMRInstAbstract.cs#L1978-L2008)
- **Befund:** Ser.SYSERIAL und Ser.THROWNEX deserialisieren Objektgraphen, mit unzureichend begrenzter rawLength.
- **Maßnahme:** Typliste, Größenlimits und sichere Zustandsmigration; Draft #220 nur als nicht freigegebene Vorarbeit.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-006 · Server-Basispfad ignoriert TLS-Zertifikatsfehler standardmäßig

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Konfigurationsrisiko
- **Fundstelle:** [`OpenSim/Framework/Servers/BaseOpenSimServer.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Framework/Servers/BaseOpenSimServer.cs#L87-L121)
- **Befund:** NoVerifyCertChain/Hostname starten mit true und beeinflussen globale HTTPS-Zertifikatsprüfungen. ROBUST-ServerMain besitzt separate Defaults.
- **Maßnahme:** Sichere Defaults, explizite Ausnahmen und HTTPS-Tests gegen falsche, abgelaufene oder nicht vertrauenswürdige Zertifikate.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-007 · PBR-Materialaktualisierung nicht atomar

- **Status:** offen · **Priorität:** P1 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs#L947-L994)
- **Befund:** asset_id kann bereits geändert sein, bevor ein glTF-Override vollständig validiert ist; ein ungültiges Override kann Teilzustände hinterlassen.
- **Maßnahme:** Vollständige Vorvalidierung, temporäre Datenstruktur, einzelner Commit und Negativ-CAPS-Roundtrip.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

## P2 – Betrieb, Renderer, Datenqualität und Konfiguration

### OGL-008 · FSAssets-Hashprüfung meldet Uppercase-Pfade unter Linux als fehlend

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`addons/OGLAssetService/OGLFsassetsHashAudit.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/addons/OGLAssetService/OGLFsassetsHashAudit.cs#L27-L52)
- **Befund:** Das Audit normalisiert SHA-Dateinamen nach lowercase, während Legacy-FSAssets teils Uppercase-Dateinamen nutzen.
- **Maßnahme:** Originale SQL-Hashschreibweise für Dateipfade beibehalten; osgrid- und Mixedcase-Regression.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-009 · 222-Asset-Audit ignoriert Spool und Legacy-Dateinamencasing

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`tools/ops/ogl-empty-assets-audit.py`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/tools/ops/ogl-empty-assets-audit.py#L44-L106)
- **Befund:** Das Python-Audit normalisiert Hashes und prüft nicht zuerst .asset-Dateien im Spool; daher mögliche False Positives.
- **Maßnahme:** Offline-Spool-Snapshot unterstützen; gleiche Suchreihenfolge wie FSAssets (Spool > gzip > raw).
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-010 · Warp3D räumt Ressourcen bei Renderfehlern nicht immer auf

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs#L260-L354)
- **Befund:** Renderer/Kamera und Caches sind gemeinsam genutzte Felder, die bei Ausnahme vor normalem destroy/reset weiter bestehen können.
- **Maßnahme:** Joblokaler Zustand, try/finally und Recovery-Tests.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-011 · Warp3D erzwingt zwei vollständige Garbage-Collections

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund / Performance
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs#L335-L352)
- **Befund:** Pro Kartenlauf werden LOH-Compaction, GC.Collect, Finalizer-Wait und weiterer GC.Collect ausgeführt; unter Last drohen Latenzspitzen.
- **Maßnahme:** Gezielte Grafikressourcenfreigabe und belastbarer p95/p99-CPU-/RAM-Benchmark vor Änderung.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-012 · Warp3D kann bei konkurrierenden Renderaufrufen gemeinsamen Zustand überschreiben

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Risikobefund
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs#L255-L320)
- **Befund:** CreateMapTile und CreateViewImage ändern dieselben Kamera-/Cachefelder ohne gemeinsamen Modul-Lock.
- **Maßnahme:** Lokale Renderparameter oder modulweite Serialisierung und Nebenläufigkeitstests.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-013 · Warp3D-Warnungssammlungen wachsen ohne Grenze

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs#L75-L103)
- **Befund:** Warnungs-HashSets für Asset-UUIDs haben keine Kapazitäts- oder TTL-Grenze; erneute Warnungen bleiben langfristig verborgen.
- **Maßnahme:** Begrenztes LRU/TTL sowie aggregierte Fehlermetriken.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-014 · Warp3D schreibt stets MAP-UUID.png

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs#L278-L283)
- **Befund:** Jeder Kartenlauf speichert im Arbeitsverzeichnis; Schreibfehler können den eigentlichen Render scheitern lassen.
- **Maßnahme:** Optionaler Debugdump (standardmäßig aus), Schreibfehler getrennt behandeln.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-015 · FastMap wartet beim Shutdown auf kompletten Render

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Betriebsrisiko
- **Fundstelle:** [`OpenSim/Region/Framework/Scenes/Scene.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/Framework/Scenes/Scene.cs#L1497-L1507)
- **Befund:** Close sperrt denselben Lock wie der Renderer; lange Kartenläufe können den Stop lange blockieren.
- **Maßnahme:** Sichere Abbruchpunkte, überprüfte Ressourcenlebensdauer und Shutdown-Benchmark.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-016 · glTF-Parser indiziert zu kurze Arrays

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Codebefund
- **Fundstelle:** [`OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs#L1095-L1123)
- **Befund:** KHR_texture_transform.offset/scale und baseColorFactor werden ohne ausreichende Mindestlängenprüfung verwendet.
- **Maßnahme:** Längen-, Typ- und Grenzwertprüfung vor jeglicher Mutation; Negative-CAPS-Tests zusammen mit OGL-007.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-017 · Remote-Console-Standardkennungen bei Aktivierung unsicher

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Konfigurationsrisiko
- **Fundstelle:** [`bin/OpenSimDefaults.ini`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/bin/OpenSimDefaults.ini#L725-L731)
- **Befund:** ConsoleUser Test und ConsolePass secret sind als Default konfiguriert; console_port ist standardmäßig 0.
- **Maßnahme:** Keine funktionsfähigen Standard-Credentials, zwingende sichere Secret-Konfiguration beim Aktivieren.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-018 · Updater baut ungeprüft jeweils aktuelles main

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Supply-Chain-/Betriebsrisiko
- **Fundstelle:** [`tools/ops/ogl-node-stage-update.sh`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/tools/ops/ogl-node-stage-update.sh#L38-L62)
- **Befund:** Staging lädt main und führt Prebuild/Build-Code aus, ohne durch ROBUST vorab erlaubte Commit-SHA/Release-Signatur. Kein automatischer Produktivdeploy.
- **Maßnahme:** Signiertes Manifest, explizit freigegebene SHA, CI-Provenienz, isolierter unprivilegierter Builder.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-019 · INI-Abgleich erkennt nur Namen, nicht Wertkonflikte

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Funktionslücke
- **Fundstelle:** [`tools/ops/ogl-config-example-sync.py`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/tools/ops/ogl-config-example-sync.py#L8-L95)
- **Befund:** Verglichen werden (Section, Key)-Mengen, nicht geänderte Defaults, Includes, doppelte Sektionen oder effektive Override-Reihenfolge.
- **Maßnahme:** Drei-Wege-Diff (alter/neuer Upstream/aktive Config), aufgelöste Include-Prioritäten, Geheimwerte nicht ausgeben.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-020 · Warp3D-Regressionen ohne vollständigen Mesh-Render-E2E-Test

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Abdeckungslücke
- **Fundstelle:** [`tools/ci/OglWarpMeshRegression/Program.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/tools/ci/OglWarpMeshRegression/Program.cs)
- **Befund:** LLSD/zlib-Prüfungen und Warteschlangen-Tests ersetzen keine real gerenderte Region mit Mesh-LOD, Texturen, konkurrierenden Jobs und Viewerkarte.
- **Maßnahme:** Reproduzierbare echte Mesh-/Regionfixtures, Benchmark und Ausgabe-/Shutdown-Vergleich.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

### OGL-021 · LOD-Fallback nicht dauerhaft im Asset-Audit vermerkt

- **Status:** offen · **Priorität:** P2 · **Kategorie:** Diagnoselücke
- **Fundstelle:** [`OpenSim/Region/CoreModules/World/Warp3DMap/WarpMeshAssetPreflight.cs`](https://github.com/opengenesislink/opengenesislinkserver/blob/main/OpenSim/Region/CoreModules/World/Warp3DMap/WarpMeshAssetPreflight.cs#L56-L77)
- **Befund:** Ein defektes Wunsch-LOD wird teils auf gültiges LOD umgeschaltet, der Fehler aber nur als Debug-Log festgehalten.
- **Maßnahme:** Fallback-Zähler, UUID und LOD-Details zentral protokollieren, Asset-Audit exportieren.
- **Abnahmekriterium:** reproduzierbarer Regressionstest (inkl. Negativfällen) und dokumentierte Prüfung im geeigneten Staging; kein unkontrollierter Produktiv-Cutover.

## Gesonderte Abnahme-/Dokumentationspunkte (keine zusätzlichen Codefehler)

### OGL-G01 · 0.9.3.13-Stable-Abnahme offen
- **Status:** offen · **Typ:** Abnahmelücke.
- **Beleg:** [Goal #294](https://github.com/opengenesislink/opengenesislinkserver/issues/294), [Scope und Blocker](09313_SCOPE_AND_BLOCKERS.md).
- **Nachweis:** SQL-/FSAssets-/Spool-Snapshot; vollständiger UUID-/Hash-Abgleich; idempotenter Import; Firestorm-Mesh-Upload/PBR-Roundtrip; HG und mehrere ROBUST-Prozesse; Restart, Fallback, Restore, Operatorentscheid.

### OGL-G02 · Releasestatus in der Roadmap möglicherweise veraltet
- **Status:** zu verifizieren · **Typ:** Dokumentationsabweichung (P3).
- **Befund:** Roadmap nennt v0.9.3.10 als Stable und 0.9.3.12-Gates. Den tatsächlich veröffentlichten neuesten Stable-Release **erst separat prüfen**, anschließend Angaben korrigieren; nicht allein aus Merges schließen.

### OGL-G03 · 222 gemeldete Asset-UUIDs benötigen einen echten Snapshot-Abgleich
- **Status:** offen · **Typ:** Datenintegritäts-/Abnahmelücke.
- **Beleg:** [schreibgeschütztes Audit](../../tools/ops/ogl-empty-assets-audit.py).
- **Nachweis:** identischer SQL-/FSAssets-/Spool-Snapshot plus Referenzen; sichere Wiederherstellung zunächst im isolierten Staging. **Keine automatische Löschung** aufgrund von Warnungen.

## Bearbeitungsreihenfolge

1. **Block A – Sicherheitsgrenzen:** OGL-001 bis OGL-006. `BinaryFormatter`-Migrationsrisiken berücksichtigen, den unfertigen .NET-10-Entwurf #220 nicht direkt deployen.
2. **Block B – Material-/Assetintegrität:** OGL-007–009 sowie OGL-016. Keine Bestandsänderung ohne getesteten Restore und negative Rechteprüfungen.
3. **Block C – Renderer und Startzeit:** OGL-010–015, OGL-020–021; `generate map` mit realen Meshregionen gegen Baseline messen.
4. **Block D – Node-Updater und Konfiguration:** OGL-017–019; getrennte Signatur, SHA-Freigabe, Konfigurationskonflikte.
5. **Block E – Betreiberabnahme:** OGL-G01–G03. Keine implizite Stable-Freigabe.

**Triage-Regel:** Beim Fix jeden Eintrag gegen den dann aktuellen `main` erneut prüfen, den tatsächlich bestätigten reproduzierbaren Fehler benennen und nur mit bestandenen Tests als erledigt markieren. Gesonderte Abnahmelücken bleiben von Codefixes unabhängig.
