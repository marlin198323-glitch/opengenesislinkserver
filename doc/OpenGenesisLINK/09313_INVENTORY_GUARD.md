# OpenGenesisLINK 0.9.3.13 - OGL HG InventoryGuard (Phase 1, Entwicklung)

Status: **Entwicklungszweig / noch nicht fuer Stable freigegeben.**
Diese Aenderung betrifft ausschliesslich den per `HGSuitcaseInventoryService`
abgewickelten Legacy-Hypergrid-Inventarpfad. Sie ergaenzt die vorhandenen
Suitcase-Grenzen, **behebt aber NICHT die fehlende Avatar-Autorisierung** von
oeffentlichem `/xinventory`. Das explizite
`AllowLegacyPublicHGInventory = true` bleibt unsicher.

## Phase 1: tatsaechlich implementiert

- `[OGLInventoryGuard] Enabled = true` aktiviert die Pruefung in der
  `HGSuitcaseInventoryService`-Instanz (Default des Codes: `false`).
- `AddItem` lehnt Null-Items, Null-UUIDs fuer Item/Owner/Folder,
  ungültige signed-byte Asset-/Inventartypen, leere/ueberlange
  Namen, Steuerzeichen in Namen und sehr grosse
  Beschreibungs-/Creator-Metadaten ab.
- `AddItem` lehnt eine schon vorhandene Item-ID ab, statt das bestehende
  Item per Datenbank-`StoreItem` zu ueberschreiben.
  **Grenze:** Die Pruefung ist noch nicht atomar mit dem Schreiben verbunden;
  ein konkurrierender Schreibvorgang bleibt theoretisch moeglich.
- `UpdateItem` vergleicht mit dem gespeicherten Eintrag und verweigert
  fehlende Originale, Owner-/Ordner-/Typ-/Creator-Identitaetswechsel
  sowie das Austauschen der Asset-ID. Die Legacy-Basisklasse korrigiert
  unzulaessige Immutable-Aenderungen bislang teils stillschweigend;
  mit Guard werden solche Anfragen abgewiesen.
- `MoveItems` weist nicht gefundene Original-Items zurueck, statt beim
  Zugriff auf `originalItem.Owner` abzustuerzen. Der historisch
  nicht verlaessliche `principalID`-Parameter bleibt kompatibel.
- `GetItem` verweigert bei aktivem Guard Items, deren tatsaechlicher
  gespeicherter Owner vom angefragten `principalID` abweicht.

Die Beispielprofile `bin/Robust.HG.ini`,
`bin/Robust.HG.ini.example` und
`bin/config-include/StandaloneHypergrid.ini` aktivieren Phase 1
explizit. Eigene Live-Konfigurationen werden durch Git nicht zwingend
aktualisiert. ROBUST/Standalone muessen **mit einer neu gebauten Binary**
gestartet werden; ein Git-Commit allein aktiviert nichts.

## Nicht implementiert - keine Sicherheitsversprechen

1. **Keine Autorisierung:** Der Legacy-Handler ermittelt aus einem
   vom fremden Grid gelieferten `Owner`/`Principal`-Wert keine
   kryptographisch nachgewiesene, avatar-spezifische Berechtigung.
   Ein Angreifer kann Besitzer-IDs faelschen. Diese Phase verhindert
   insbesondere weder alle fremden Schreiboperationen noch alle
   Lesoperationen.
2. **Kein Asset-Scanner:** Die `AssetID` referenziert separate Daten;
   `AddItem` sieht keine vollstaendigen Objektbytes. Meshes,
   Texturen, XML-Objekte, Scripts und verschachtelte Inventare
   werden derzeit NICHT geprueft.
3. **Keine Quarantaene:** Es gibt noch keinen persistenten
   Zwischenzustand mit Freigabe-/Ablehnungsworkflow.
4. **Keine Schadcode-Erkennung:** Statische Suche nach LSL-/OSSL-
   Funktionsnamen waere keine verlaessliche Aussage ueber
   Boesartigkeit. Restriktionen der Script-Engine sind getrennt
   erforderlich.
5. **Keine atomare Insert-only-Datenbankoperation:** Eine race-freie
   ID-Kollisionssperre erfordert eine bedingte INSERT-Operation in
   SQLite, PostgreSQL und MySQL/MariaDB.
6. **Keine weltweite HG-Abnahme:** Tests mit fremden Legacy-Grids,
   gekauften Objekten, Attachments, Wearables, Script-Aktualisierungen
   und gesonderter Asset-Zustellung stehen aus.

## Tests

Statische Verkabelungspruefung:

```bash
python3 tools/ci/verify_ogl_hg_inventory_guard_09313.py
python3 tools/ci/verify_hg_inventory_security_09312.py
```

Ausfuehrbare Metadaten-Regression (benoetigt den vollstaendig gebauten
OpenGenesisLINK-/OpenSim-Abhaengigkeitsbaum und .NET 8):

```bash
dotnet run -c Release --project tools/ci/OglHgInventoryGuardRegression/OglHgInventoryGuardRegression.csproj
```

Mindestens ein legitimer HG-Testkauf, ein legitimer Suitcase-Transfer,
ein wiederholter (idempotenter) Transfer, ein fuer das falsche
Avatar-UUID gesendetes Item, eine Duplikat-Item-ID, ein Update
mit anderer Asset-ID und ein absichtlich defektes Item testen. Fuer
bezahlte Transfers Zahlung, Inventareintrag und Assetzustellung
getrennt protokollieren. Aktueller Stand der Tests: **nicht auf
einem produktionsnahen Grid ausgefuehrt**.

## Phase 2: verbindliche technische Vorarbeit

- Authentifizierter Transferkontext statt frei uebermittelter Owner-ID:
  kryptographisch signiert, kurzlebig, ziel-/avatar-/operation-spezifisch,
  serverseitig gebundene Session, Replay-Schutz, Revocation,
  einzeln auditiertes Read/Write; Legacy-Grids brauchen
  einen Interoperabilitaets- oder sicheren Fallbackvertrag.
- Neue `PendingInventoryTransfer`-Entitaet mit eindeutiger Transfer-ID:
  `received -> staging -> inspected -> accepted | quarantined | rejected`.
  Bytes/Assets erst in einem isolierten, ressourcenlimitierten
  Staging-Bereich annehmen, nicht unter produktiven Item-IDs.
  Freigabe plus DB-Referenz als atomaren Schritt umsetzen.
- Auf Assettyp begrenztes Parsing mit DTD-Verbot,
  Maximalgroesse, Maximalverschachtelung, Scriptinventar-
  Rekursion, Ressourcenkostenlimit, Hash/Provenienz und
  laufzeitlichen Script-Capabilities. Kein Ausfuehren
  untrusted Scripts bei der Inspektion.
- Fremd-Asset-Verfuegbarkeit: HG legt Item-Verweise teils vor
  dem Asset-Transfer an. Fehlende Daten nicht pauschal
  als unbedenklich akzeptieren und auch nicht ohne
  Transfer-Protokoll als endgueltig schadhaft einstufen.
  Status `pending` plus sichere Wiederholung implementieren.
- Quarantaene mit Betreiber-/Residenten-Audit und optionaler
  manueller Freigabe; niemals Autostart von Scripts.
- Datenbank-`INSERT IF NOT EXISTS`/Transaktionsschutz auf
  allen drei Datenbank-Backends. Negative und parallele Tests.
- Fehlersignalisierung an Kaeufer/Verkaeufer fuer bezahlte,
  nicht zugestellte Transfers inkl. Beleg/Erstattungsprozess.

## Rollout / Rollback

Nur nach Backups von Inventardatenbank, FSAssets, Regiondaten
und Konfiguration. Testgrid vor Live-Grid. Bei Fehlern
`[OGLInventoryGuard] Enabled = false` und den ROBUST- bzw.
Standalone-Dienst neu starten. Das ist **nur** das Abschalten der
neuen Metadatenpruefung; es ist KEIN Fix fuer die
Legacy-XInventory-Sicherheitsluecke. Als bevorzugte sichere
Notfallmassnahme oeffentlichen HG-Inventardienst
deaktivieren (`AllowLegacyPublicHGInventory = false`)
und den Public-Connector entfernen.

Siehe `09312_HG_INVENTORY_SECURITY.md` und
`09313_TEMPORARY_LEGACY_HG_INVENTORY.md`.
