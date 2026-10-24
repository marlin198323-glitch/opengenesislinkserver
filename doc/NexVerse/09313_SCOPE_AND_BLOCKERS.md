# OpenGenesisLINK 0.9.3.13 — Implementierungsgrenzen und Freigabeblocker

**Stand: 24.10.2026. Status: technischer Scope-Nachweis, keine Release-Freigabe.**
Siehe [Roadmap](ROADMAP.md), [Goal #294](https://github.com/opengenesislink/opengenesislinkserver/issues/294)
und [Release-Gates](09313_RELEASE_ACCEPTANCE.md).

## Umfang der Version

Die vom Betreiber bestätigten Roadmap-Punkte lauten:

- **17.1 NexAsset / OGL AssetService**
- **17.2 Modern material/content pipeline**

NexMarketplace und NexMedia gehören ausdrücklich nicht mehr dazu. Dieses Dokument
entfernt **keine** weiteren Anforderungen aus der Roadmap. Nicht nachgewiesene
Eigenschaften bleiben bis zur Entscheidung über deren Release-Umfang offen.

## Nachweisbarer Entwicklungsstand (keine Produktionsabnahme)

| Baustein | Code- und CI-Nachweis | Betriebsfreigabe |
| --- | --- | --- |
| FSAssets-kompatibler OGL AssetConnector | PR #237, bestehende Connector-/Build-Integration | Nicht automatisch aktiviert; FSAssets/SQL autoritativ |
| OGL SHA-256-Dual-Store, SQLite-UUID-Index, Snapshot-Migrator | PR #283, Asset-/Migrations-Tests | Nur getrenntes Staging nach Restore/Snapshot-Parität |
| Vollständiger Read-only-Migrations-Preflight und abgesicherte Object-Shards | PR #296, vier grüne Workflows | Test mit isolierter Kopie echter FSAssets-Daten offen |
| Mesh-Fee LLSD-Kompatibilität, ZLib-LOD/Hull-Decoder | PRs #284/#293, Runtime-Regressionsprüfungen | Echter Firestorm-Upload bis Rezzing/Relog offen |
| PBR RenderMaterial-Array und glTF-JSON-Materialoverrides | PR #295, PBR-Regression | Viewer-/Permissions-/Persistenz-Roundtrip offen |
| PBR-CAPS LLSD-Face-Werte, glTF-Bild-/Texturindices und definierte Spaninitialisierung | PR #297, NexVerse CI vollständig grün | Test im unveränderten Firestorm offen |

Diese Tests belegen konkrete Codepfade, **keinen** vollständigen
glTF-Mesh-Importer und keine erfolgreiche produktive AssetService-Umstellung.

## Vor einer Stable-Freigabe nicht als fertig ausgeben

- Konsistenter produktionsnaher, isolierter SQL-/FSAssets-/Spool-Restore
  und vollständige UUID-/SHA-256-Parität.
- Dual-Write, Restart, OGL-first, Störung von OGL-Pfad/SQLite und
  kontrollierter Rückfall auf autoritatives FSAssets mit echten Assets.
- Ein vollständiger Firestorm-Mesh-Upload mit Gebührenantwort, Upload-URL,
  assetseitigem Persistieren, Rezzing, Relog und Physik-/LOD-Prüfung.
- PBR-Materialien mit Metallic/Roughness, Normal/Occlusion, Emissive,
  Assetberechtigungen, Regionwechsel, Relog und Hypergrid.
- Nachweis, welche in 17.1/17.2 aufgeführten Zukunftsfunktionen
  (S3-/CDN-Anbindung, automatische GC, native glTF-Modelle,
  Terrain-/Reflection-/Environment-Materialien) wirklich in 0.9.3.13
  ausgeliefert werden. Ohne belegte Implementierung: **offen**, nicht
  stillschweigend als erfüllt markieren.
- Zwei-ROBUST-Szenario, Leistungsbenchmark, negative Rechte-/Uploadfälle,
  Rollback-Übung, Release Candidate auf exaktem Commit und unterschriebene
  Betreiberfreigabe.
- Relevant gebliebene offene 0.9.3.12-Gates erneut auf dem Kandidaten prüfen.

## Sicherheitsgrenzen und Installation

- Ein Git-Update, Release-Build oder Merge darf keine AssetService-
  Konfiguration aktivieren oder Produktionsbestände migrieren.
- Es dürfen keine produktiven SQL-/FSAssets-/Spool-Quellen für
  migrationsschreibende Tests verwendet werden.
- HTTP-Admin-Funktionen bleiben opt-in und dürfen ohne den geprüften
  Authentifizierungs-, TLS-/Netzwerk- und Auditpfad nicht öffentlich werden.
- Keine bestehende Inventar-, Avatar-, Asset-UUID- oder Hypergrid-Referenz
  wird bei einer Codefreigabe eigenmächtig geändert.

**Freigabeentscheidung:** Erst nach vollständig belegten Gates aus
`09313_RELEASE_ACCEPTANCE.md` und erneuter Prüfung des jeweiligen
Releasekandidaten treffen. Bis dahin bleibt Goal #294 offen.
