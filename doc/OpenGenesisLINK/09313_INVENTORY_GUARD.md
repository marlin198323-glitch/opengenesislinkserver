# OpenGenesisLINK 0.9.3.13 - OGL HG InventoryGuard (Phase 1-3c, Entwicklung)

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

## Phase 2a: implementierte HG Asset-Eingangspruefung (keine Quarantaene)

- `[OGLInventoryGuard] ScanInboundAssets = true` aktiviert bei ebenfalls
  gesetztem `Enabled = true` die Pruefung am
  `HGAssetService.Store(AssetBase)`-Pfad **vor** `m_assetService.Store`.
  Der Code-Default fuer die Einzeloption ist `false`.
- `MaxInboundAssetBytes = 33554432` (32 MiB) und
  `MaxInboundScriptBytes = 1048576` (1 MiB) begrenzen Payloads und LSL-Text.
  Gueltige kleinere Assets koennen weiter importiert werden.
- Null-/leere Assets oder fehlende Asset-UUID werden abgewiesen.
- `Object`-Assets muessen syntaktisch gueltiges XML sein; XML-Reader ist
  streaming, mit `DtdProcessing.Prohibit`, `XmlResolver=null`,
  einer Dokumentgroessen-, Tiefen- und Knotenzahlgrenze.
- `LSLText` braucht gueltiges UTF-8, darf kein NUL enthalten.
  `LSLBytecode` als eingehendes fremdes Ausfuehrungsformat wird
  vorsorglich abgewiesen. Das ist kein Beweis fuer boesartige Scripts.
- `HGAssetService.Store` lehnt bei Guard-Fehlern mit leerem Ergebnis ab.
  Reject-Grund wird im Serverlog ohne Assetinhalt protokolliert.
- Ein Inventar-Item kann ueber andere Wege geschrieben werden oder bevor
  die referenzierten Assetbytes uebertragen sind. Phase 2a blockiert nur
  die Payload am HGAssetService-Eingang, **nicht** jeden potentiellen
  Inventareintrag und nicht die internen Asset-Dienste.
- Der Objekt-XML-Parser prueft nur die Struktursicherheit des Containers,
  nicht alle referenzierten Assets, enthaltenen Scripts/Objekte oder die
  beabsichtigte Laufzeitwirkung. Texturen/Meshes erhalten bisher nur
  allgemeine Groessen-/Nichtleer-Pruefungen, keine volle Formatvalidierung.

## Phase 2b: persistente Quarantaene fuer abgelehnte HG Asset-Payloads

Bei aktiviertem `[OGLInventoryGuard] Enabled=true` und
`ScanInboundAssets=true` kann `QuarantineRejectedAssets=true` zusaetzlich
einen **Beweisdatensatz fuer bereits abgelehnte** HG-Assets ablegen.
Code-Default und ausgelieferte Profile stehen auf `false`.
Der normale HG-Import wird auch dann abgelehnt, wenn diese Evidence-
Speicherung wegen Vollbelegung, Rechten oder IO-Fehlern fehlschlaegt.

```ini
[OGLInventoryGuard]
Enabled = true
ScanInboundAssets = true
MaxInboundAssetBytes = 33554432
MaxInboundScriptBytes = 1048576
QuarantineRejectedAssets = false
QuarantineDirectory = "/var/lib/opengenesislink/hg-rejected-assets"
QuarantineMaxCaptureBytes = 262144
QuarantineMaxRecords = 5000
```

- Nur dediziertes, absolutes, nicht oeffentlich ausgeliefertes
  Linux-Verzeichnis verwenden. Der Dienstbenutzer muss dort schreiben koennen.
  Ein fehlendes Verzeichnis wird mit Modus 0700 angelegt, die Records
  erhalten 0600. Symlinks direkt auf den Zielordner werden verweigert.
  Windows wird fuer diese Funktion absichtlich nicht aktiviert, bis ein
  entsprechendes Dateisystem-ACL-Modell implementiert wurde.
- Je Ablehnung generiert der Server eine **eigene zufaellige**
  Evidence-ID, eine `<id>.json`-Datei und bei positiver
  Capture-Grenze eine `<id>.payload`-Stichprobe. Dateinamen enthalten
  keine vom HG-Sender gelieferten Werte.
- JSON enthaelt Zeit, Ablehnungsgrund, Asset-ID/-Typ, Originalgroesse,
  Samplegroesse, Truncation und nach Moeglichkeit den SHA-256-Hash
  des vollstaendigen Assets. Um riesige Uploads nicht zusaetzlich
  auszurechnen, ist der Vollhash auf Payloads bis 32 MiB begrenzt.
  Weder Scripttext noch Creator-Felder werden in JSON aufgenommen.
- Pro Record werden maximal 1 MiB Payload kopiert (Standard 256 KiB);
  `0` speichert nur Metadaten. Groessere Dateien werden **nicht**
  unlimitiert auf Platte abgelegt. Nach `QuarantineMaxRecords`
  werden keine weiteren Records angenommen; die Ablehnung im HG-Service
  bleibt unabhaengig davon bestehen.
- Aufbewahrung, geloeschte Records, Datenschutz, Rechte und freier
  Speicher sind Betreiberaufgaben. **Keine** Freigabe-/Restore-API
  implementiert. Forensik darf untrusted Payloads nicht ausfuehren.
- Die Quarantaene schreibt keine `fsassets`-Daten und veraendert
  keine bestehenden Itemreferenzen. Sie ist kein Staging vor
  `InventoryService.AddItem`, kein Zahlungsrollback und keine
  Verifikation eines remote Avatars.

**Betriebsrisiko:** Bei aktivierter Quarantaene liegen fremde Dateien
dauerhaft auf dem Host. Verzeichnis mit restriktiven Rechten, getrennt
von HTTP-/FSAssets-/Backup-Freigaben halten; regelmaessig externe
Speicherauslastung ueberwachen und Datensaetze nach einer definierten
Aufbewahrungsfrist manuell archivieren/loeschen.

## Phase 2c: geschuetzte, sichtbare Inventar-Systemordner

Ein weiterer Teil von PR #229 ist die optionale, serverseitig verwaltete
Quarantaene-Ordnerhierarchie fuer **lokale** Bewohner:

```text
My Inventory
  Quarantaene
    In Pruefung
    Abgelehnt
```

In den HG-Entwicklungskonfigurationen steht:

```ini
[OGLInventoryGuard]
Enabled = true
CreateInventorySystemFolder = true
```

`XInventoryService.CreateUserInventory` erzeugt die Hierarchie bei neuen
Konten. `XInventoryService.GetInventorySkeleton` legt sie auch fuer
Bestandskonten beim naechsten Inventarskelett-Abruf (typisch Login)
an. Jede der drei UUIDs wird deterministisch aus Account-UUID und Rolle
abgeleitet: dieselben Ordner werden bei einem Neustart nicht dupliziert.
Eine neu erstellte DB bekommt erst beim Konto-/Skelettzugriff neue Ordner.

Viewerkompatibilitaet: Diese Ordner haben `FolderType.None`, also
eine **gewoehnliche OpenSim-Ordnerdarstellung**. OGL verwaltet sie
serverseitig als Systemordner, Firestorm muss dafuer keine
unbekannte FolderType-Enum unterstuetzen. Eine eigene Schutzgrafik
oder Badge im OGL Viewer ist **noch nicht** implementiert.

Im lokalen `XInventoryService` wird bei aktivem Schalter die
Liste geschuetzter UUIDs geprueft:
`AddFolder`, `UpdateFolder`, `MoveFolder`, `DeleteFolders`,
`PurgeFolder`, `AddItem`, `UpdateItem`, `MoveItems`,
`DeleteItems` und normale Inventar-Leseoperationen sperren
Manipulation bzw. Auslieferung aus diesen Ordnern.
Die IDs sind Marker, keine Authentisierung. Die Sperren greifen
im Inventarservice, nicht nur in der Viewer-Darstellung.

**Wichtig: Die Ordner enthalten keine Assets oder Inventargegenstaende.**
Es koennen nur harmlose, leere Vorgangs-Unterordner als
Statusmarker angezeigt werden (siehe Phase 3a). Die separate
serverseitige Evidence-Quarantaene fuer abgelehnte
HG-Asset-Uploads ist **nicht** in die Avatareingaben verlinkt. Ein
abgelehntes externes Script/Objekt wird nicht automatisch zu einem
inventarisierbaren Item oder in `Abgelehnt` verschoben. Ein
ausstehender, sicher autorisierter `PendingInventoryTransfer`
fehlt weiterhin. Die Viewer-Ordner zeigen keine administrativen
Quarantaene-Payloads an und bieten keinerlei Freigabefunktion.

**Hinweise fuer Administratoren:** Nur Codepfade, die den
`XInventoryService` verwenden, werden durch diese Sperren
abgedeckt. Andere Inventory-Implementierungen, SQL-Mutationen,
direkte Script-Engine-Verwendungen und Asset-Service-Zugriffe
sind nicht automatisch geschuetzt. Die Sicherheitsgarantie des
Gesamtsystems benoetigt weiterhin Phase 3 mit Transaktionsstatus,
signierter Autorisierung und einheitlicher Inventar-/Asset-Gate.

SQLite-Regression mit echtem Inventardatenbank-Backend:

```bash
python3 tools/ci/verify_ogl_quarantine_system_folders.py
dotnet run -c Release --project tools/ci/OglQuarantineFolderRegression/OglQuarantineFolderRegression.csproj
```

## Phase 3a: sichere STATUS-Anzeige je Vorgang (lokale API)

Die `XInventoryService`-Implementierung unterstuetzt jetzt **nur fuer
vertrauenswuerdige lokale Serverkomponenten** zwei neue Aufrufe:

- `TryBeginTrustedQuarantineCase(verifiedOwner, caseId)`: legt
  einen leeren, nicht ausfuehrbaren `Vorgang XXXXXXXX`-Unterordner
  in `Quarantaene / In Pruefung` an.
- `TryRejectTrustedQuarantineCase(verifiedOwner, caseId)`: verschiebt
  ausschliesslich einen bestehenden solchen Vorgang nach
  `Quarantaene / Abgelehnt`. Das ist ein dauerhaft ablehnender
  Status, **keine Freigabe**. Wiederholtes Ablehnen ist idempotent.

Vorgangsordner-IDs werden stabil aus verifiziertem Bewohnerkonto und
Transaktions-UUID abgeleitet. Die Vorgangsordner enthalten
**keinerlei Inventarobjekt, Script, Asset oder herunterladbare Datei**.
Ein untrusted HG-Name/Asset-Inhalt wird nicht in einen Ordnernamen
uebernommen. Versionszaehler der Elternordner werden bei Aenderungen
aktualisiert, damit Viewer den neuen Status nachladen koennen.

Die Hierarchie-Sperre gilt jetzt auch fuer alle Vorgangsordner und
darunterliegende Elemente. Die regulären `AddFolder`-/`MoveFolder`-/
`DeleteFolders`-/`AddItem`-/`MoveItems`- und Leseoperationen
koennen diese Bereiche nicht normal veraendern oder dort liegende
Gegenstaende ausliefern.

**Verbindliche Sicherheitsgrenze:** `verifiedOwner` muss vom
AUFRUFENDEN, vertrauenswuerdigen Transferkoordinator authentifiziert
werden. Diese Methoden gehoeren absichtlich **nicht** zur
`IInventoryService`-Schnittstelle und erscheinen nicht im
`/xinventory`-Remoteprotokoll. Das ist eine serverinterne
Projektions-API, **noch keine** implementierte Authentifizierungs-
oder Hypergrid-Eingangslogik. Weder rohe HG-Item-Metadaten noch
ein Dateiname der Evidence-Quarantaene koennen damit bereits
automatisch auf ein Bewohnerkonto abgebildet werden.

**Aktueller Automatik-Stand:** Der Code *kann* sichere Statusmarker
anzeigen, sobald ein spaeterer verifizierter Transferkoordinator
die APIs aufruft. Kein fremdes Grid kann mit den normalen
Remoteinventarmethoden selbst solche Marker anlegen. Die
automatische Verknuepfung der HG-Transfers und eine erfolgreiche
Freigabe/Zustellung bleiben als Phase 3b offen.

SQLite-Regression: `tools/ci/OglQuarantineFolderRegression`
prueft zwei unabhängige Vorgänge, doppelte Registrierung,
erlaubte Pending->Rejected-Transition, verbotenen Ruecksprung,
Nichtmanipulierbarkeit, Versionszaehler und Persistenz nach
einem neuen Serviceprozess.

## Phase 3b / Sicherheitsnachweis fuer lokale Statusuebergaenge

`OglQuarantineTransferGrantAuthority` und
`OglQuarantineTransferCoordinator` sind in
`OpenSim.Services.InventoryService` implementiert.

- Der **vertraute lokale** Aussteller erzeugt ein signiertes
  HMAC-SHA-256-Ticket fuer eine exakt angegebene Bewohner-UUID,
  Vorgangs-UUID, Inventory-Item-UUID, Asset-UUID, Ziel-Grid-Audience
  und Operation `Begin` oder `Reject`. Die Gültigkeit liegt zwischen
  einer Sekunde und maximal fuenf Minuten.
- Validator prueft MAC in konstanter Zeit, Audience, Operation,
  Objekt-/Asset-/Owner-IDs, Zeitgrenzen und Signatur, **bevor** der
  Quarantaene-Vorgangsstatus veraendert werden darf. Ein Begin-Ticket
  kann keine Ablehnung ausloesen, ein Ticket fuer Avatar A nicht
  an Avatar B eingesetzt werden.
- Jedes gueltige Ticket wird beim Verbrauch im isolierten Linux-Verzeichnis
  durch `FileMode.CreateNew` (atomar auf einem lokalen Dateisystem)
  als `SHA256(ticket).spent` vermerkt. Die JSON-Auditdatei
  enthaelt bewusst **nur** die verifizierte Grid-Audience,
  Operation, Vorgangs-/Bewohner-/Item-/Asset-UUID sowie
  Ausstellungs-/Ablauf-/Verbrauchszeit; sie speichert **kein**
  Ticket, keine Schluessel und keine fremden Assetbytes.
  Kein Ticket darf erneut verwendet werden, auch nicht nach
  einem Dienstneustart. Dateimodus 0600, Verzeichnis 0700;
  ein unbeschreibbares Verzeichnis fuehrt zum **Abbruch**.
  Kann nach erfolgreichem Verbrauch der Viewer-Status
  nicht geschrieben werden, bleibt das Ticket bewusst verbraucht;
  Wiederherstellung erfordert einen neuen, separat autorisierten
  Vorgang, nicht einen unsicheren Replay.
- Das Ticket muss aus einem **bereits authentifizierten lokalen
  Kontext** stammen; der Konstruktor erwartet einen mindestens
  32 Bytes langen zufaelligen Schluessel und eine explizite
  Grid-Audience. **Kein** Default-Schluessel und **keine**
  Ticket-Ausgabe ueber einen oeffentlichen Connector!
- Das Replay-Verzeichnis muss bei Multi-Prozess-Betrieb von **allen**
  kooperierenden Komponenten ueber **ein gemeinsames Dateisystem
  mit garantierter atomarer CreateNew-Semantik** verwendet werden.
  Ein je-Knoten-lokales Verzeichnis bietet keinen Cluster-weiten
  Replay-Schutz. Langfristig ist eine transaktionale Datenbank-
  Ledger-Tabelle in PostgreSQL/MariaDB/SQLite vorgesehen.
- Die Zeitpruefung verwendet die lokale Server-UTC; NTP-Synchronisierung
  und Key-Rotation sind Betreiberpflicht. Keine automatisierte
  Bereinigung verbrauchter Tickets implementiert. Keine
  Signierschluessel ins Repository oder die Clientanwendung kopieren.

**Extrem wichtig:** Das ist eine kryptographische Eintrittsbedingung
fuer **lokale Statuswechsel**, **keine HG-Avatar-Authentifizierung**.
Die Ausstellerseite besitzt noch keine bewiesene Zuordnung
`fremdes Grid -> Avatar -> Zugriffsrecht -> bezahlter Transfer`.
Daher werden aus rohen HG-/xinventory-Nachrichten derzeit weder
Tickets ausgegeben noch der Coordinator aus der Public-API
aufgerufen. Die oeffentliche Legacy-Schnittstelle bleibt in den
Entwicklungsprofilen teilweise aktiv und ist weiter unsicher.
Ticketpruefung ist auch noch **keine** Prüfung von rekursiven
Scripten/Meshes oder atomare Inventarzustellung.
Produktiver Einsatz dieser Teilfunktion erst nach integriertem
authentifiziertem Transferprotokoll und gemeinsamem Ledger.

Regressionstest:
`dotnet run -c Release --project tools/ci/OglQuarantineFolderRegression/OglQuarantineFolderRegression.csproj`
prueft gueltige und gefaelschte Tickets, falsche Audience/
Avatar-/Item-/Asset-ID, Operationstrennung, Replay nach Neustart,
maximale Gültigkeitsdauer, idempotente Quarantaene-Statuswechsel und
konkurrierende Replay-Versuche von zwei unabhängigen Service-Instanzen.

## Phase 3c: isoliertes, persistentes Staging fuer lokale, verifizierte Transfers

Neu: `OglQuarantineTransferStageStore` speichert ungepruefte Payloadbytes
als **separate, nicht ausfuehrbare Dateien** auf dem Server, bewusst
**nicht in FSAssets oder der Inventardatenbank**.
`OglQuarantineTransferCoordinator.TryStage(...)` verlangt zuvor einen
gueltigen, nur einmal nutzbaren und zielgebundenen
`OglQuarantineTransferAction.Stage`-Nachweis. Erst nach
erfolgreicher Ablage und einer Integritaetskontrolle kann ein
leerer Statusordner unter `In Pruefung` erscheinen. Der Nachweis
ist eine **lokale** Eintrittsbedingung; er beweist weiterhin nicht
die HG-Identitaet eines externen Anrufers.

Die API wird **nicht ueber HTTP oder /xinventory angeboten**.
Eine spaetere vertrauenswuerdige HG-Transferkomponente muss die
identitaetsgebundene Ticket-Ausstellung und das Staging ausdruecklich
aufrufen. Es gibt **noch keinen automatischen Assetimport**.

Sicherheitsgrenzen und Eigenschaften:

- Separater **absoluter, dedizierter Linux-Pfad**, Verzeichnisrechte
  0700, Payload und JSON-Manifest 0600. Die direkte Verzeichnis-
  Symlink-Nutzung wird verweigert. Keinen oeffentlichen HTTP-Pfad,
  kein `fsassets`-Verzeichnis und keine freigegebenen Backups verwenden.
- Opaque Dateikennung `SHA256(owner|case)` statt fremder Dateinamen.
  Rohdatei: `<kennung>.payload`, JSON-Manifest:
  `<kennung>.json`. Metadata: `staged_uninspected`, verifizierte
  Bewohner-, Vorgangs-, Item- und Asset-UUID, Assettyp,
  Laenge, voller SHA-256 und Erfassungszeit.
- Bytes werden **unveraendert, aber ungeprueft** aufbewahrt.
  Das ist ein Quarantaenespeicher und kein Malware-/Script-Scanner.
  Standardwerte fuer die Store-Klasse sind maximal
  **8 MiB je Asset, 2.000 Records und 1 GiB gesamt**.
  Die harten Konstruktor-Obergrenzen sind 32 MiB je Asset und
  32 GiB insgesamt. Abweichende Limits muessen bewusst vom
  vertrauten Aufrufer uebergeben werden.
- Eine `FileShare.None`-Dateisperre begrenzt konkurrierende
  Schreibvorgaenge innerhalb eines gemeinsam genutzten
  POSIX-Dateisystems. In Multi-Host-Umgebungen ist eine
  geeignete atomare Ledger-/Quota-Datenbank erforderlich;
  ein je-Node-lokales Dateiverzeichnis garantiert keine
  clusterweite Deduplikation.
- Manifest wird zuletzt geschrieben. Bricht der Dienst zwischen
  Payload- und Manifestablage ab, bleiben moeglicherweise
  unvollstaendige Artefakte. Diese werden **niemals automatisch
  als geprueft angesehen** und erfordern manuelle Reconciliation.
  Kann ein erfolgreiches Staging nicht in den sichtbaren
  Pending-Ordner projiziert werden, bleiben die Bytes sicher
  isoliert, waehrend der Aufruf fehlschlaegt.
- `TryVerify(owner,case,item,asset)` vergleicht persistente
  Identifier, Laenge und vollen SHA-256 nach Neustart, liefert
  aber **keine** Rohbytes an Viewer oder Skripte.
  Ein manipuliertes Asset kann damit nicht stillschweigend in
  den Bestand gelangen.
- Kein `TryRelease`, kein `m_assetService.Store` und kein
  `m_Database.StoreItem` im Staging-Pfad. **Auch gueltig
  signierte Stage-Nachweise koennen nichts ins reguläre Inventar
  freigeben.** Eine solche Freigabe verlangt einen gesonderten,
  erst noch zu implementierenden autorisierten und atomaren
  Prozess. Operator-Retention/-Loeschung und sichere Behandlung
  ausgefallener/manipulierter Records sind noch offen.

Der separate, in Phase 2b eingefuehrte Speicher fuer **bereits
abgelehnte Upload-Evidenz** ist davon unabhaengig und wird
nicht automatisch in das neue Staging ueberfuehrt.

Regression: `tools/ci/OglQuarantineFolderRegression` prueft
signierten `Stage`-Vorgang, Avatar-/Asset-Bindung, Replay,
Dateiberechtigungen, Manifest, Persistenz nach Neustart,
Groessenlimit, Record-Quota, mutierte Bytes und das Verbot
eines normalen Inventareintrags.

## Nicht implementiert - keine Sicherheitsversprechen

1. **Keine Autorisierung:** Der Legacy-Handler ermittelt aus einem
   vom fremden Grid gelieferten `Owner`/`Principal`-Wert keine
   kryptographisch nachgewiesene, avatar-spezifische Berechtigung.
   Ein Angreifer kann Besitzer-IDs faelschen. Diese Phase verhindert
   insbesondere weder alle fremden Schreiboperationen noch alle
   Lesoperationen.
2. **Keine vollstaendige rekursive Content-Inspektion:** Die `AssetID`
   referenziert separate Daten, die ein `AddItem` nicht sieht. Die
   HGAssetService-Eingangskontrolle validiert Objekt-XML nur strukturell,
   prueft aber weder eingebettete Script-Faehigkeiten noch alle
   referenzierten Assets, Mesh-/Texturdateiformate und Unterinventare.
   Ohne isolierten Transferstatus existiert keine atomare
   "vollstaendig geprueft vor Item-Anlage"-Garantie.
3. **Kein kompletter HG-Transfer-Workflow:** Es existieren
   serverseitige forensische Evidenz, gesperrte Bewohnerordner,
   lokale signierte Statusnachweise und isoliertes persistentes
   Byte-Staging. Die **fremdgrid-authentifizierte** Zuordnung,
   automatische Assetzustellung, rekursive Pruefung und atomare
   Freigabe sind weiterhin nicht implementiert.
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

Statische Verkabelungspruefung (Metadaten- und Asset-Eingangspruefung):

```bash
python3 tools/ci/verify_ogl_hg_inventory_guard_09313.py
python3 tools/ci/verify_hg_inventory_security_09312.py
```

Ausfuehrbare Metadaten-/Asset-/Quarantaene-Regression (benoetigt den vollstaendig gebauten
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
