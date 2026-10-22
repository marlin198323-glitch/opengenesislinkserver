# OpenGenesisLINK ↔ NexVerse Stadtportal – Server-Gegenprüfung R409

**Stand:** 23.10.2026 (Quellcode-Abgleich; keine produktiven Mutationen)  
**Auslöser:** NEXVERSE_STADTPORTAL_WORLD_API_ABGLEICH_R409_2026-10-22.md, Stadtportal-R397–R408; **präzisierte Architekturentscheidung 23.10.2026**  
**Zielrepository:** https://github.com/opengenesislink/opengenesislinkserver  
**World-main vor Erstellung dieses Berichts:** 9fd04acaad55d7d7eae2bbdc4c95c9b4c7df19d8 (PR #278, OGLSeasons-Mono.Addins-Fix)  
**Reproduzierbare technische Vergleichsbasis:** 7396897d3d24ab8fac238ded1e2760d7b4544f13 (PR #275, Gruppenland-Key). **Dies ist kein verifiziertes letztes produktives Release.**  
**Stadtportal-Quellstand laut Übergabe:** marlin198323-glitch/nexverse, Branch home-r8-footer-cookie, 6604d9008db28ec441cf40e71dc2cc2fad90b0ba. Der angegebene Portal-Branch ließ sich mit der hier verfügbaren GitHub-Verbindung nicht öffnen; Portalimplementierungen sind deshalb Quellenangaben der Übergabe, keine eigenständigen Codebefunde.

**Verbindliche Scope-Korrektur vom 23.10.2026 (Betreiberentscheidung):** **Bugreports sind vollständig allein Sache des Stadtportals.** Es wird **keinerlei** World-API-/Simulator-Integration hierfür entwickelt oder betrieben. Insbesondere entfallen ein World-REST-Endpunkt, Eventhook, RegionModule, HTTPS-Forwarder, Relay, World-Token/-Secret, World-Datenbank und World-E2E-Test. Die entgegenstehenden R403–R408-Aufträge der Portalübergabe gelten für den World-Server ausdrücklich als **nicht gewünscht**, nicht als offene technische Lücken. Dies ist eine dokumentierte Scope-Änderung, keine Aussage über die Implementierung des Stadtportals.

**Evidenzklassen:** Q = tatsächlicher World-GitHub-Quellcode inklusive Datei/Zeile am obigen World-HEAD; C = GitHub-Commit-Diff/Actions; P = vom Stadtportal am 22.10. gemeldeter Snapshot; L = in dieser Sitzung live geprüft. **Es liegt keine Evidenzklasse L vor**: Abruf von world.stadt-nexverse.de/api/v1/openapi.json, /health und /deutschland war aus der Prüfungsumgebung nicht möglich. Ein Quellcodepfad ist kein Beweis, dass der Endpoint auf dem produktiven ROBUST aktiv oder von außen erreichbar ist. CI-Status ist nicht gleich produktiver E2E-Test.

## A. Prüfdatum, World-Git-HEAD, Baseline-Commit, Live-API-Snapshot und OpenAPI-Fingerprint

- World-HEAD bei Aufnahme: **9fd04acaad55d7d7eae2bbdc4c95c9b4c7df19d8**. Die zugehörige PR #278 lief in Actions **39859081180** mit **2/2 erfolgreich** (Build NexVerse und NV dollar live database matrix).
- Letzter Release-/Deployment-SHA: **NICHT_VERIFIZIERBAR**. Kein im Übergabedokument belegter produktiver World-Deploy-Commit. Vergleiche mit 7396897 sind technisch reproduzierbar, nicht als Release-Differenz auszugeben.
- Vorangegangene nachgewiesene Änderungen: PR #276 (424e4ad3304bc37159b461c3330591ac5336c857, Mesh-LLSD-Kosten, Economy-Key-Gruppenscope); PR #277 (5d65cd73d67d1d89e9242cf991dc2923759c0ff4, NexPortalIM-MariaDB und HMAC-Generator); PR #278 (9fd04aca..., OGLSeasons-Plugin).
- Nur **laut Portal-Snapshot P** vom 22.10.2026: OpenAPI 3.1.0, API-Version v1, **151 Pfade / 203 HTTP-Operationen / 168 Schemas**; SHA-256 der damals geladenen JSON-Bytes: **2dca38f68480d8209d9fe288ad94b8cca49eaf499e2e4d125dd61a24c850da0e**. Nicht in dieser Sitzung repliziert; keine neuen Live-Zahlen oder Hashes erfunden.
- Portal meldet HTTP 200 für /api/v1/health, /version und /deutschland am 22.10.; erneute Live-Statusprüfung aus dieser Umgebung **fehlgeschlagen/nicht möglich**, nicht als aktueller Ausfall zu deuten.
- Nach bestätigtem Deploy: anonymen OpenAPI-Snapshot sicher herunterladen, SHA-256 dokumentieren, nach OpenAPI-Pfaden/Methoden/Schemas mit dem zuvor archivierten **echten** Snapshot vergleichen. Die im Portaltext erwähnten historischen Zähler (R288: 141/193/164) sind **keine maschinenlesbaren Diff-Snapshots**.

## B. Tatsächlich NEU hinzugekommene World-Routen/Operationen/Schemas seit Baseline

**Zwischen Git-Commits 7396897 und 9fd04ac: keine nachgewiesene neue öffentliche World-API-Pfad-/Methodendefinition oder OpenAPI-Schemaänderung.** GitHub-Compare zeigt **3 Commits, 31 betroffene Dateien**, aber **NexVerse/Server/Api/NexVerseWorldApiHandlers.cs ist unverändert**. Im Connector änderten sich NexPortalIM-/Dienstinitialisierung, nicht die hier fraglichen Karten-/Discovery-/Bildrouten.

Neu in dieser technischen Spanne, aber **keine neuen öffentlichen World-Routen**: eigenständige MariaDB-Archivierung für NexPortalIM, HMAC-Erzeugung im vorhandenen Admin-Control-Center, vereinheitlichte Secret-/Konfigurationsführung und Tests; außerdem Mesh-Kosten-Fix und OGLSeasons-Abhängigkeitsfix. Aus dem Portal-Snapshot 141→151 Pfade darf deshalb **nicht** geschlossen werden, diese drei World-Commits hätten zehn Routen hinzugefügt. Wann die zehn hinzugekommen sind, bleibt ohne echten vorherigen Snapshot/Release-SHA offen.

## C. Tatsächlich GEÄNDERTE World-Routen/Operationen/Validierungen/Scopes seit Baseline

- **Keine geänderte deklarierte OpenAPI-Operation/Schema in NexVerseWorldApiHandlers.cs** zwischen der oben gewählten, belegten Baseline und dem World-HEAD.
- **Bestehender Admin-Schlüsselgenerator:** neue Economy-Schlüssel erhalten economy:read + economy:transfer + groups:read (PR #276); Quellcode NexVerse/Server/Api/NexApiDocsPage.cs. Bestehende Schlüssel werden dadurch nicht automatisch rotiert.
- **Bestehender Portal-IM-Dienst:** MariaDB-Archiv und gesicherte Fehlerbehandlung (PR #277), NexVerseWorldApiConnector.cs ca. 1011–1091, NexCitizenImMySqlStore.cs und NexCitizenImIngress.cs. Das ist **nicht** der Portal-Bug-Report-Empfänger und keine Bug-Ticket-DB.
- **OpenSim.ini:** R277 stellte SearchServerURI auf stadt-nexverse.de/service/search und DestinationGuideURI auf stadt-nexverse.de/service/destination. Diese Viewer-Ziele sind nicht identisch mit GET /api/v1/search der World API.
- Für Request-/Response-/Scope-/Audience-/Statuscode-Änderungen zwischen **echten Live-OpenAPI-Snapshots** fehlt eine verifizierte ältere JSON-Datei. Das ist als **NICHT_VERIFIZIERBAR** zu kennzeichnen; bloße Versionszählungen sind nicht ausreichend.

## D. R397–R408 – jede Portalrevision separat klassifiziert

| Portalrevision | Status | World-Befund und erforderliche Entscheidung |
| --- | --- | --- |
| **R397** | VORHANDEN_UNVERAENDERT mit ABWEICHUNG | Öffentliche GET-/api/v1/deutschland-Route plus 16×16-Viewport im World-Handler vorhanden; World-Name-Suche erlaubt q ab **2**, das Portal ab **3** Zeichen. Das ist eine strengere Portalvalidierung, keine defekte World-Suche. 256-Meter-Zellmodell belegt. |
| **R398** | PORTAL_ONLY | Same-origin-Go-JPEG-Proxy, CSP und Karten-Frontend sind Portal-Funktionen; World stellt JPEG über /deutschland/tiles bereit. |
| **R399** | PORTAL_ONLY | Start-/Proxy-Routing des Go-Portals liegt außerhalb der World-API. World-HTTP-MIME/Cache im Tile-Handler vorhanden; externe Proxy-Freigabe nicht live verifiziert. |
| **R400** | PORTAL_ONLY | Zoom/Pan/Touch/Vollbild/Marker im Portal. World verwendet OpenSim-X/Y-Gitterkoordinaten und liefert cells_x/cells_y für VarRegions; konkrete 44/50-Abnahme offen. |
| **R401** | PORTAL_ONLY | Portal darf unabhängig vom World-Health verfügbar bleiben. World /health bleibt getrennte Statusquelle; aktuelle Stabilität/Latenz nicht gemessen. |
| **R402 – Orte** | VORHANDEN_UNVERAENDERT mit ABWEICHUNG | World DELETE /api/v1/places/{placeId} verlangt discovery:manage und führt eine **Kaskadenlöschung** durch. Keine World-seitige Bestätigung des Ortsnamens; dies ist Portal-UI-Logik. Bei unbekannter UUID kann **HTTP 200, deleted:false** statt 404 auftreten. Keine echten Orte gelöscht. |
| **R402 – Kartendaten** | VORHANDEN_UNVERAENDERT mit ABWEICHUNG | Welt-Viewport maximal 16×16, Ergebnis begrenzt durch Take(256); Suche auf 32 begrenzt. **Keine** truncated/reported_regions-Metadaten im aktuellen Handler. 44 Regionen/50 belegte Zellen nicht per echter Laufzeit-Fikstur nachgewiesen. |
| **R403** | PORTAL_ONLY / NICHT_GEWÜNSCHT | Bugreports (Eingang, Persistenz, Administration, Zustellung) werden ausschließlich durch das Stadtportal verantwortet. **Kein World-seitiger Integrationsbedarf.** |
| **R404** | PORTAL_ONLY | CSS-/Kartenhintergrund-Anpassung, keine World-Änderung. |
| **R405** | PORTAL_ONLY / NICHT_GEWÜNSCHT | Portal/LSL-spezifische Bugterminal-Logik ist kein World-API-Arbeitsauftrag. Keine Eventhooks/Avatarverifikation im World-Code hierfür. |
| **R406** | PORTAL_ONLY / NICHT_GEWÜNSCHT | Portal/LSL-JSON und eventueller IM-Fallback bleiben außerhalb des World-API-Projektumfangs. |
| **R407** | PORTAL_ONLY / NICHT_GEWÜNSCHT | Report-ID/Relay sind Portal-Entscheidungen; keine World-seitige Sender-/Idempotenz-Integration. |
| **R408** | PORTAL_ONLY / NICHT_GEWÜNSCHT | Der im Portalquellstand beschriebene CLI-Sender ist **nicht** in OpenGenesisLINK einzubinden; keine World-eigene Übertragung an das Stadtportal. |

**Hinweis zur Klassifikation:** VORHANDEN_UNVERAENDERT bezieht sich auf den nachweisbaren Git-Compare 7396897→9fd04ac, **nicht** auf die unbekannte letzte produktive Release-Basis.

## E. Abweichungen, fehlende World-Funktionen, Prioritäten, Belege

In der folgenden Matrix beziehen sich alle Q-Dateizeilen auf World-HEAD **9fd04aca**; im Rahmen dieses Audits wurden keine produktiven Entitäten verändert.

| ID | Ausgangspunkt | World-Pfad / Modul | Befundstatus | Vorher → Jetzt | Codebeleg Datei:Zeile + SHA | Test/HTTP-Beleg | World-Änderung? | Verantwortlich | Priorität |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **W-01** | R403–R408 | **Kein** World-Bugreport-Endpunkt/Modul | PORTAL_ONLY / NICHT_GEWÜNSCHT | in Portalübergabe vorgeschlagen → durch Betreiber verworfen | Scope-Entscheidung 23.10.2026; World-API-Router NexVerse/Server/Api/NexVerseWorldApiConnector.cs (nur zur Abgrenzung), geprüfter Quellstand 9fd04aca | **World-E2E ausdrücklich nicht vorgesehen** | **Nein. Weder Code noch Tests, Token oder DB.** | Ausschließlich Stadtportal | **entfällt** |
| **W-02** | R397/R402 | GET /api/v1/deutschland; /tiles, /xyz | VORHANDEN_UNVERAENDERT + ABWEICHEND | 16×16/256-limitiert, fehlende Truncation-Signale | NexVerse/Server/Api/NexCitizenWorldMapApi.cs:29,90–198,257–315; 9fd04aca | tools/ci/verify_citizen_world_map.py (Quellcode-Gate); 44/50-Laufzeittest fehlt | P1: Vollständigkeit/Signale + synthetischer Fixture-Test | World-API | **P1** |
| **W-03** | R402 | DELETE /api/v1/places/{placeId} | VORHANDEN_UNVERAENDERT + ABWEICHEND | discovery:manage vorhanden; HTTP 200/deleted:false möglich, kaskadierende Entfernung | NexVerse/Server/Api/NexDiscoveryApi.cs:238–297; NexVerse/Core/Discovery/NexDiscoveryStore.cs:216–230; 9fd04aca | tools/ci/NexDiscoveryRegression/Program.cs; keine produktiven Deletes | Dokumentation und ggf. 404-/Idempotenz-Vertragsabstimmung | World/Portal | **P1** |
| **W-04** | R286/R294/R388 | GET /api/v1/profiles/{principalId}/image | VORHANDEN_UNVERAENDERT; Live NICHT_VERIFIZIERBAR | Route/Owner-/Privatsphäre-/JPEG2000-Transcode vorhanden | NexVerse/Server/Api/NexVerseWorldApiConnector.cs:1118–1136; NexWebProfileV3Api.cs:59–104,178–330; 9fd04aca | tools/ci/verify_r287_portal_api.py; echter autorisierter 200/404-Test offen | Konfiguration/Assetservice/E2E, nicht blind neue Route | World-API/ROBUST-Betreiber | **P1** |
| **W-05** | R311–R385 | GET name-availability / POST register | VORHANDEN_UNVERAENDERT; Live NICHT_VERIFIZIERBAR | maschineller users:register-Key, Resident-Default, Name + Home prüfbar | NexVerse/Server/Api/OglPortalRegistrationApi.cs:125–187,248–335; Connector.cs:642–663; 9fd04aca | tools/ci/verify_portal_registration_iar_preflight.py; keine echten Registrierungen | Staging-E2E, Direct-Login-Policy getrennt klären | ROBUST/Portal | **P1** |
| **W-06** | R337–R395 | /users/me, /users/{principalId}/level | VORHANDEN_UNVERAENDERT mit Rollengrenze | Level 50 allein kein admin:*; explizite Supportrolle getrennt | NexVerse/Core/Security/NexSecurity.cs:220–315; NexVerseUserApi.cs:579–598,1652–1678; 9fd04aca | tools/ci/verify_user_authorization.py | Rollenabbildung Level=50 im Portal live testen | World/Portal | **P1** |
| **W-07** | R308–R390 | /api/v1/inventory/* | VORHANDEN_UNVERAENDERT | inventory:read/write + Ownerprüfung, Admin-Fremdzugriff | NexVerse/Server/Api/NexInventoryApi.cs:35–73,168–225; 9fd04aca | vorhandene Inventory-CI; Fremd-Owner-E2E offen | Nein, außer Vertrags-/E2E-Abweichung | World | P1 |
| **W-08** | R283–R390 | /api/v1/banking/* | VORHANDEN_UNVERAENDERT | getrennte economy:read und economy:transfer, int64-Felder | NexVerse/Server/Api/NexBankingCommerceApi.cs:50–115,~314; NexVerseWorldApiHandlers.cs:376–420,3859–4040; 9fd04aca | NV$ MariaDB-Matrix erfolgreich in Actions #39859081180; kein Echtgeld/Live-Transfer | Nein, nur sichere Lesetests | World/Portal | P1 |
| **W-09** | R320–R391 | /api/v1/land-commerce/listings | VORHANDEN_UNVERAENDERT | GET economy:read; POST derzeit **admin:*** statt normaler Citizen-Listings | NexBankingCommerceApi.cs:314–332; NexVerseWorldApiHandlers.cs:3830–3990; 9fd04aca | keine echten Verkäufe/Mieten ausgeführt | Portal-UX und Schreibberechtigung abstimmen | World/Portal | P1 |
| **W-10** | R267–R308 | /api/v1/groups/* | VORHANDEN_UNVERAENDERT | groups:read/manage + Mitgliedschaftsprüfungen; generisches Chapter11-Envelope teilweise untypisiert | NexGroupsApi.cs:59–98,165–245; NexVerseWorldApiHandlers.cs:294–334,3329–3345; 9fd04aca | NexGroups MariaDB-Matrix Actions #39859081180 | Schemas punktuell härten, keine zweite Gruppen-API | World | P1 |
| **W-11** | R286–R310 | Experiences Script/Viewer | VORHANDEN_UNVERAENDERT | getrennte scopes und Script-Servicekontext | NexVerse/Core/Security/NexSecurity.cs:34–48; NexVerseWorldApiHandlers.cs:336–375,3685–3710; 9fd04aca | Experiences CI in Build NexVerse | Keine pauschalen Service-Scopes für Bürger | World | P1 |
| **W-12** | R368–R401 | GET /api/v1/health | VORHANDEN_UNVERAENDERT; Live NICHT_VERIFIZIERBAR | Portal bleibt unabhängig, Health nur getrennte Diagnose | NexVerseWorldApiHandlers.cs:~80–110,132–145; 9fd04aca | P meldet 200 am 22.10.; in dieser Umgebung kein Liveabruf | Kein Portal-Gate auf Health koppeln | Portal/Betreiber | P2 |
| **W-13** | R397–R402 | Map-Suchquery / Truncation | ABWEICHEND | Portal q=3–80; World q=2–80, bei mehr als 32 Treffern Abschneidung ohne Signal | NexCitizenWorldMapApi.cs:94–121; 9fd04aca | source assertion; kein >32-Fall in CI | P1: Schema/Ergebnis-Metadaten präzisieren | World/Portal | P1 |

## F. Bugreports: verbindliche Systemgrenze (ausschließlich Stadtportal)

**Architekturentscheidung des Betreibers vom 23.10.2026:** Das gesamte Bugreport-System wird allein im **Stadt-NexVerse-Stadtportal** betrieben. Für OpenGenesisLINK bzw. den OpenSim-Simulator ist **keinerlei Verbindung zu diesem System vorgesehen**. R403, R405, R406, R407 und R408 sind für die World-API daher **PORTAL_ONLY / NICHT_GEWÜNSCHT** und keine P0-Mängel.

Nicht implementieren, nicht planen, nicht konfigurieren: World-Bugreport-Routen, LSL-/Touch-/Chat-Eventadapter, Avatarpositionsverifikation speziell für Reports, RegionModule, HTTP-Post-/CLI-Sender, Proxy/Relay, World-spezifische Bearer-Schlüssel, Datenspeicher, Migrationsskripte, Monitoring oder Integrationstests für Portal-Bugreports. Auch der in der Übergabe erwähnte `NEXVERSE_BUG_REPORT_API_TOKEN` ist **kein** einzurichtendes World-Secret.

Die Portal-Route `POST https://stadt-nexverse.de/api/v1/bug-reports` und die Verwaltungsrouten `/api/admin/bug-reports` bleiben ausschließlich Verträge des **Stadtportals**, nicht der World-API. Wie das Stadtportal seine eigenen Meldungen empfängt und verarbeitet, ist Gegenstand seiner eigenen Entwicklung und Prüfungen. Keine vorhandene NexPortalIM-Funktion dafür umwidmen.

**Fazit W-01:** Kein World-Blocker, keine World-Priorität, keine World-Änderung erforderlich. Die frühere Integrationsforderung der R409-Übergabe ist durch die spätere Betreiberentscheidung überholt.

## G. Deutschland-Gridkarte, JPEG-Kacheln, vollständige Regionen und Öffentlichkeit

**Quelle:** NexCitizenWorldMapApi.cs:20–395, Connector.cs:859–879, OpenAPI-Generator NexVerseWorldApiHandlers.cs:218–222, 3108–3130; SHA 9fd04aca.

- World GET /api/v1/deutschland, /view, /tiles/{zoom}/{x}/{y}.jpg und /xyz/{z}/{x}/{y}.jpg sind real als Handler/Handler-Verzweigungen vorhanden. Der Prefix /deutschland ist im Connector registriert; das Modul routet die Unterpfade.
- Server verwendet Rastergröße **256 Meter pro Zelle**, maximal **16×16** in einer Viewport-Anfrage; MaxRegionAxisCells 16 für VarRegions. Koordinaten **X/Ost, Y/Nord** sind Gridwerte, keine echte Geografie. Regionengrößen cells_x/cells_y werden aus RegionSizeX/Y abgeleitet.
- **Tile GET**: zoom 1–8, valide nichtnegative/ausgerichtete Zellkoordinaten; native MapImageService liefert echtes Warp3D-JPEG; bei Erfolg **image/jpeg**, Cache-Control public,max-age=60, max. 2 MiB; 400 ungültig, 404 nicht vorhanden/nicht sichtbar, 503 ohne MapImageService/Providerfehler.
- **Schutz:** Visible() filtert reservierte/offline Regionen, **wenn** RegionFlags verfügbar sind; öffentlich werden Name, UUID, Koordinaten, Größe, Maturity, TeleportURI ausgegeben. Keine Owner-/Simulator-Endpunkt-/Node-ID im Payload. Bei fehlenden RegionFlags wird Sichtbarkeit nicht durch eine zusätzliche zwingende Publish-Policy nachgewiesen; Betreiberpolicy separat prüfen.
- **Vollständigkeitsgrenzen:** bei freier Suche GetRegionsByName(..., 32) + Take(32); bei Viewport GetRegionRange(...) + Take(256); **kein** truncated oder reported_regions und keine tatsächliche Vollzähligkeitsgarantie, falls eine darunterliegende Grid-Suche selbst kappt. Der 44-Regionen-/50-Zellen-Fall ist mit 256 Einträgen vereinbar, aber bislang **kein** über FakeGrid plus MockMapImageService bestätigter Regressionstest mit realer Rasterabdeckung.
- **Schema:** CitizenWorldMapRegion ist für region_id/name/grid_x/grid_y/cells_x/cells_y/size_x/size_y/maturity/teleport_uri typisiert; CitizenWorldMapResponse deklariert mode/cell_size_meters/regions/correlation_id als required; bounds ist nur generisch object, truncated/reported_regions fehlen. Quelle NexVerseWorldApiHandlers.cs:3090–3135.
- **Hinweis zum Default:** Ohne Bounds und ohne DefaultRegion ist HTTP 200, has_default_region:false, leeres regions-Array ausdrücklich gültig. Für **explizite Bounds** setzt der aktuelle Code has_default_region:true unabhängig davon, ob tatsächlich eine DefaultRegion existiert (NexCitizenWorldMapApi.cs:122–195); die Feldsemantik ist uneinheitlich. Bei Suche ist has_default_region nicht immer enthalten. P1-Contract-Fix festhalten, nicht ohne Verbrauchertest still ändern.
- **Abnahmeplan:** Synthetische 16×16-Fenster, 44 Regionen mit 50 Kachelzellen, mindestens eine 2×2-VarRegion, Origin vor West-/Südgrenze, 404 für nicht sichtbare Zellen, JPEG-MIME und Größenlimit; Grenzen 16/17 und negative/out-of-range Koordinaten. Für >32 Suchtreffer und theoretische Abschneidung Truncation-/Count-Felder definieren, Schema + Portal gleichzeitig aktualisieren. Keine echte Grid-Datenbank für Fixtures überschreiben.

## H. Registrierung, Avatarbild, UserLevel 50, Citizen/Admin-Rechte

**Registrierung:** Connector.cs:642–663 registriert Name-GET und Register-POST. OglPortalRegistrationApi.cs:125–154 nimmt nur **X-NexVerse-Api-Key mit users:register**, kontrolliert HTTPS/TrustedProxy; Session-Bearer genügt nicht. GET first_name/last_name, leerer last_name → Resident, Namen 2–31; POST wiederholt Name-/Duplikatprüfung, Modell-Allowlist und home_region/position-Validierung, danach Provisioner. Codebelege 157–189 und 248–335. **Live 201, paralleler Doppelregistrierungs-Race, Login-Umgehung und effektive Home-/LastPosition nicht gegen Produktion getestet.** Portal-eigene PostgreSQL-Namensblacklist ist keine automatische World-Regel.

**Profilbild:** Connector.cs:1121–1136 registriert einen Prefix-Handler; NexWebProfileV3Api.cs:59–105 erkennt /api/v1/profiles/{uuid}/image. Öffentliche Bilder nur bei PublishProfile; ansonsten owner mit profile:read oder admin:*; nur durch das authoritative userprofile referenziertes ImageId. JPEG/PNG-Payload direkt, JPEG2000 über OpenJPEG→PNG; MIME image/jpeg oder image/png; Cache-Control private,no-store (keine verifizierte ETag-Unterstützung). Kann bei nicht vorhandenen Assets, unlesbaren Daten oder falschem Assetservice 404/503 ergeben; optional 415/413. Quelle 178–330. **Route existiert im Code, 200 beim Live-Avatarbild nicht hier bewiesen**.

**Support-Level 50:** NexSecurity.cs:220–257 vergibt admin:* erst ab konfiguriertem AdminMinimumUserLevel (standardmäßig 200). NexRoles.Support erhält begrenzte Lesescopes (UsersRead, RegionsRead, SimulatorsRead, StatisticsRead, EstatesRead; 262–276). **UserLevel=50 ist nicht dasselbe wie explizit zugewiesene Rolle Support.** Keine Beförderung von Level 50 zu vollwertigen World-Adminrechten zulassen. Portal-spezifische Ticketberechtigung bleibt Portal-eigene Policy. World SetUserLevel persistiert UserAccount und invalidiert Cache (NexVerseUserApi.cs:579–598); für wirksamen Tokenwiderruf und kritische Accounts sind gesonderte Tests/Operatorrichtlinien notwendig.

## I. Discovery-DELETE, Land, Banking, Inventory, Groups, Experiences – Vertrag

| Bereich | Tatsächliche Route/Schutzquelle | Schemaabdeckung/Restpunkt |
| --- | --- | --- |
| Discovery Place | GET öffentlich für approved; DELETE /api/v1/places/{placeId} nach discovery:manage. NexDiscoveryApi.cs:184–297 | DeletePlace in NexDiscoveryStore.cs:216–230 entfernt zusätzlich **alle Destinations, Events und Classifieds mit derselben PlaceId**. Bei fehlender PlaceId kann deleted:false als HTTP 200 zurückkommen. Portal muss Kaskadenfolge explizit bestätigen; nicht pauschal behaupten, World biete 404. |
| Discovery Schema | NexVerseWorldApiHandlers.cs:3247–3261 | DiscoveryObjectResponse hat place/destination/places/destinations/deleted/correlation_id, **aber** zusätzliche Properties erlaubt und nested Place-Datensätze dort nur generische objects. Konkrete Events/Classifieds separater typisiert. |
| Gruppen | NexGroupsApi.cs:59–245; NexVerseWorldApiHandlers.cs:294–334 | Gruppenverwaltung schützt groups:read / groups:manage und prüft Mitglieder-/Rollenbezug. Chapter11ObjectResponse (3329–3345) hat bekannte Keys, aber Gruppen-, Mitglieder-, Rollen- und Notices-Elemente sind dort noch generische objects; ExperienceApiRecord teilweise typisiert. Nicht behaupten, vollständig typisiert. |
| Banking | NexBankingCommerceApi.cs:50–115; NexVerseWorldApiHandlers.cs:376–398,3859–4040 | economy:read getrennt von economy:transfer, sensible Buchungen mit integer/int64. BankingTransactionsResponse enthält account_id/transactions/offset/limit; StatementResponse enthält statement/currency; verschachtelte BankingLedgerTransaction mit postings SchemaRef. metadata ist weiterhin frei; Transaktions-/Owner-Runtimeprüfung für Portaldaten separat testen. |
| Land/Commerce | NexBankingCommerceApi.cs:314–390; NexVerseWorldApiHandlers.cs:3822–3860,3956–4100 | GET Listings economy:read; **POST Listings admin:***; kein Self-Service ohne gesonderte Freigabe. LandListingRecord hat listing_id und type (Sale/Rental), integer/int64 price, rental_period_days. Portal muss die **tatsächliche** Antwort nach POST auf listing_id prüfen; keine produktiven Listings als Test. |
| Inventar | NexInventoryApi.cs:35–73,170–225 | owner_id aus authentifiziertem account; fremde Eigentümer nur mit admin:*; UUID-/Folder-Owner-Prüfungen. Mehrere Lese-/Schreiboperationen existieren; konkrete Pagination je Route prüfen, nicht globale Paginierungsannahme. |
| Experiences | NexSecurity.cs:34–48; NexVerseWorldApiHandlers.cs:334–375,3685–3710 | experiences:read/manage von speziellen Simulator-/Viewer-Scopes strikt trennen. Service-only darf nicht über eine normale Resident-Session eskalieren. |
| World Health | NexVerseWorldApiHandlers.cs:~75–110 | /health separat; keine Begründung, das ganze Go-Portal bei 503 dunkel zu schalten. |
| Registrierung | OglPortalRegistrationApi.cs:125–335 | users:register nur Maschinen-Key; keine beliebigen Portalbearer; Resource-/Name-Checks vorhanden, Staging-Creation noch offen. |

## J. Portal-only-Funktionen ohne World-Änderungsbedarf

- Karten-Frontend: CSS, Zoom, Marker, Fullscreen, Touch-Pinch, eigener Go-JPEG-Proxy und CSP (R398–R400, R404).
- Portal-Health-Degradierung / keine globale „Licht aus“-Seite (R401).
- **Sämtliche** Bugreport-Funktionen einschließlich Portal-Eingang, Portal-Speicher, Admin-Queue, LSL-/Terminal-Verhalten und möglichem Portal-Sender (R403–R408). **Keine World-Anbindung, keine World-Ticket-DB und kein World-Secret.**
- Go-Portal-Registrierungsoberfläche, E-Mail-Bestätigung, PostgreSQL-Namensblacklist und UI-Beschriftung; World-Name-/Register-Vertrag nur über bestehende Maschine-Schnittstellen.
- Portal-News-CMS, Banner-/Layout-/Cookie-/Datenschutzseiten (R369–R396).
- Portal-eigene Debounce-/Race-Suppression und Archivierung eigener Suchergebnisse.
- Portal-Regel, dass Support-Level 50 nur dortige Supporttickets sieht: nicht als World-admin:* interpretieren.

## K. Testprotokoll und sicherer E2E-Plan

**Bereits dokumentierte GitHub-CI:** PR #278, Workflow **39859081180**, Build NexVerse = success und NV dollar live database matrix = success. Zusätzlich wurden in früheren PRs #276/#277 Mesh-LLSD- und NexPortalIM-MariaDB-Regressionen grün. Dies beweist den Bau/angesprochenen CI-Vertrag, **nicht** einen realen R409-E2E-Lauf.

| Prüfebene | Test / Quelle | Einordnung |
| --- | --- | --- |
| Map-Routen und Zugriff | tools/ci/verify_citizen_world_map.py | vorhandener Quellcode-/Security-Gate; kein Test mit echten 44/50-Kachelfixtures |
| Discovery | tools/ci/NexDiscoveryRegression/Program.cs; tools/ci/verify_nexdiscovery.py | vorhandene synthetische Persistenz-/Vertragsprüfungen; kein produktives Place-DELETE |
| Profilbild | tools/ci/verify_r287_portal_api.py | Quellenvertrag für Profile und OpenJPEG; Live-MIME/Cache/Owner-200 noch offen |
| Registrierung | tools/ci/verify_portal_registration_iar_preflight.py; tools/ci/NexResidentNameRegression/Program.cs | echte Namens-Normalisierung, Source-Gates; kein echter Avatar angelegt |
| UserLevel/Bearer | tools/ci/verify_user_authorization.py; tools/ci/NexTokenRegression/Program.cs | Rollen-/Revocation-Sicherung im CI, Level=50 Portal-Policy nicht live geprüft |
| Gruppen/Banking | Actions #39859081180; tools/ci/NexGroupsSqlAuthorizationRegression und NexLedgerSqlMatrixRegression | reale **CI-Test-DB** MariaDB/SQL, keine produktiven Finanzbewegungen |
| Bugreports | **Kein World-Test vorgesehen** | Vollständig Portal-only; keine Simulator-/World-API-Integration und kein World-HTTP-E2E-Test. |
| Live OpenAPI / Proxy / HTTP | **NICHT DURCHFÜHRBAR aus dieser Sitzung** | Online-Snapshot/Proxy-Zugriffe müssen in autorisierter Netzumgebung wiederholt werden |

**Neue E2E-Fixtures vor Freigabe (nur Staging/Synthese):**
1. Map: 200 bei zulässiger Bounds-/Search-Abfrage, 400 bei 17×16, negativen Koordinaten und ungültiger Zoomstufe; 404 für inaktive Zellen; 503 bei absichtlich entferntem MapImageService; reales JPEG-MIME, maximal 2 MiB und 44 Regionen/50 Kacheln.
2. Images: öffentlich 200 image/png oder image/jpeg bei veröffentlichtem Profil; 401 bei privatem ohne Token, 404 bei fremdem Token/falscher UUID/nicht gesetztem Bild; 503 bei bewusst fehlendem AssetService; 405 auf POST.
3. Registration: 200 name-availability, 401 falscher Maschinen-Key, 400 Name/Home, 409 eindeutiger Namenskonflikt, 201 synthetisches Staging-Konto; keine realen Avatare registrieren.
4. Place DELETE: 401/403 bei nicht berechtigtem Actor, 200/deleted:false bei nicht existierendem Place wie derzeit implementiert, 200/deleted:true bei synthetischem Place, danach abhängige Events/Destinations/Classifieds entfernt; 429 falls Gateway-Limiter aktiv. **Nie echte Orte löschen.**
5. Banking/Inventory/Groups/Land: 401, 403, 404, 405, 409, 422, 429 entsprechend tatsächlich vorhandenen Handlern; nicht alle Codes sind universelle Fachantworten. Keine Live-Transfers, Inventar- oder Listings-Mutationen.

## L. Erforderliche World-Änderungen (Entscheidung und Risiken)

| ID | Maßnahme und Dateien | Test / OpenAPI / Migration | Priorität | Risiko |
| --- | --- | --- | --- | --- |
| W-01 | **ENTFÄLLT:** Keine World-Bugreport-Integration. Keine RegionModule, Eventhooks, HTTP-Forwarder, Bearer, World-Routen, Datenbank oder Migration. | **Keine World-Tests oder OpenAPI-Änderungen**; allein Stadtportal-Verantwortung. | **entfällt** | unnötige Kopplung zweier Systeme vermeiden |
| W-02 | **PRÜFEN/ggf. ändern:** NexCitizenWorldMapApi.cs: Truncation-/reported_regions für begrenzte Suche/Viewport, einheitliche has_default_region-Semantik; OpenAPI CitizenWorldMapResponse ergänzen, **nach** Portal-Abstimmung. | FakeGrid/MockMapService-Test mit 44 Regionen/50 JPEG-Zellen, >32 Treffern, 16/17-Bounds und VarRegion-Overlaps; kein SQL-Schemawechsel | P1 | API-Rückwärtskompatibilität, verborgen abgeschnittene Regionen |
| W-03 | **ABSTIMMEN:** Discovery Place DELETE: huidige HTTP 200/deleted:false vs Portal-404-Erwartung, dokumentierte kaskadierende Delete-Auswirkung. Nur nach Vertragstest ändern. | NexDiscoveryRegression erweitern, OpenAPI-Statuscodes abgleichen; kein SQL-Schemawechsel | P1 | unbeabsichtigtes Entfernen abhängiger Publikationen |
| W-04 | **E2E/OPERATOR:** Profile AssetService und öffentlicher/privater Bildtransport, HTTP-MIME/Cache. Keine neue Route ohne echten 404-Beleg. | Staging-HTTP-Test; keine Migration | P1 | Datenschutz, falsches AssetService-Routing |
| W-05 | **E2E/OPERATOR:** Registrierung, Resident-Namen, machine key, Home/LastPosition und technische Konto-Sperren. | Fixtures/Staging-Test, keine echten Registrierungen; kein Schemawechsel | P1 | Doppelregistrierung, Gatekeeper-Login |
| W-06 | **DOKU/SCHEMA:** DiscoveryObjectResponse und Chapter11ObjectResponse enger typisieren, Banking-/Land-Nested-Owner-Felder gezielt prüfen; keine pauschale Schema-Neuschreibung. | OpenAPI-Contract-Regression und differenzbasierte Portal-Anpassung | P1/P2 | inkompatible Clients, scheinbar typisierte unbekannte Daten |
| PORTAL_ONLY | Go/CSS/Admin-Bug-Queue/CSP/News/Supportticket-Views bleiben im Portal. | Nur Portal-CI | Keine World-Änderung | unnötige Verdoppelung vermeiden |

**Es gibt keinen World-P0-Bugreport-Auftrag.** W-01 ist ausdrücklich geschlossen als **PORTAL_ONLY / NICHT_GEWÜNSCHT**. Die verbleibenden World-Verbesserungen betreffen ausschließlich bestehende World-API-Funktionen.

## M. World-Commits, Betreiberfreigaben und nächster Integrationsschritt

**Geprüfte bestehende World-Commits:**
- 7396897d3d24ab8fac238ded1e2760d7b4544f13 – dokumentierte technische Ausgangsbasis (PR #275, kein Release-Beweis)
- 424e4ad3304bc37159b461c3330591ac5336c857 – PR #276, Mesh-/Economy-Gruppenhistorie
- 5d65cd73d67d1d89e9242cf991dc2923759c0ff4 – PR #277, NexPortalIM-MariaDB und World-API-Admin-HMAC-Generator
- 9fd04acaad55d7d7eae2bbdc4c95c9b4c7df19d8 – PR #278, korrigierte OGLSeasons-Framework-Abhängigkeit
- **Audit-PR/Commit:** nach Veröffentlichung dieses Dokuments in GitHub anhand des PRs ermitteln; kein vorab erfundener SHA.

**Ausstehende Betreiberfreigaben:** tatsächlicher World-Deployment-SHA; sicherer Abruf von Live-OpenAPI und früherem Snapshot; Proxy/TLS-Regeln für die bestehenden World-Routen; Staging-Freigabe für synthetische World-API-E2E-Tests. **Keine** Freigabe für Bugreport-Integration erforderlich oder erwünscht. Keine produktive Migration oder User-/Finanz-/Orte-Änderungen durchgeführt.

**Konkret an den Stadtportal-Chat zurückgeben:**
1. World-Karte, Profile, Registrierung, Place-DELETE und Fach-APIs **existieren im World-Code**, produktive Einzelaufrufe noch nicht nachgewiesen.
2. **Bugreports ausschließlich Stadtportal:** Für den World-/OpenSim-Server existiert **kein** Integrationsauftrag und **kein** P0-Blocker. Bitte R403–R408 beim Stadtportal belassen.
3. **Place DELETE kaskadiert** über Destinations, Events und Classifieds; mögliche Antwort 200/deleted:false statt 404. Portal bitte explizit anzeigen/bestätigen.
4. World-Karte liefert **16×16**-Bounds, bis 256 Regionen pro Viewport, **32** Suchtreffer, kein truncated/reported_regions; 44/50-VarRegion-Abnahme offen. Portal darf 32/48-Bildkappung nicht neu einführen.
5. Avatarbild mit PublishProfile-/Owner-Prüfung und JPEG2000→PNG vorhanden; Live-404 bei falsch konfiguriertem AssetService möglich.
6. Level 50 darf niemals automatisch admin:* werden. Explizite World-Rolle Support und Portal-Ticketpolicy unterscheiden.
7. Portal-/World-OpenAPI-Live-Diff aus dem **echten vorherigen Snapshot** bleibt offen. Die aus GitHub geprüften PRs #276–#278 führten keine neuen OpenAPI-Operationsdefinitionen ein.
8. **Priorität:** W-02 Karten-Vollständigkeits-Fixtures und W-03 Place-DELETE-Vertrag koordinieren; W-01 ausdrücklich **nicht** bearbeiten.

**Datensicherheit:** Dieser Bericht enthält weder private Benutzer-, Finanz- oder Bugreport-Daten noch Tokens/Passwörter. Alle UUIDs in fachlichen Beispielen der Übergabe waren synthetisch; dieser Report führt keine Live-POST-/DELETE-Aufrufe aus.
