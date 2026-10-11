# OpenGenesisLINK 0.9.3.10 – Runtime- und Stabilisierungs-Release-Train

**Aktive Entwicklung:** `OpenGenesisLINK 0.9.3.10 Dev`  
**Letzte veröffentlichte Stable-Version:** `OpenGenesisLINK 0.9.3.8` (Tag `v0.9.3.8`)  
**Freigabeziel:** `OpenGenesisLINK 0.9.3.10 Stable`; **keine separate Stable-Version 0.9.3.9**.  
**Status:** Quellcode und CI fuer aktuelle Dev-Features vorhanden, RC1-Vorbereitung gestartet; KEIN RC-Tag und KEINE Stable-Freigabe. Ein gruener Build ersetzt keine Live-Abnahme. Verbindlicher Ablauf: [09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md).

**Aktuelle Scope-Entscheidung (11.10.2026):** OGL Pathfinding ist aus dieser Version **und dem aktuellen Server-Code** komplett entfernt: keine A*-Kerne, keine Terrain-/Layered-Navigation, keine NPC-Follower, keine OGLNavGraph- oder Firestorm-NavMesh-CAPS. Der eigene OpenGenesisLINK-Viewer kann spaeter ein unabhaengiges Navigationsvorhaben erhalten. Dessen Protokolle und serverseitige Sicherheitsgrenzen werden erst dann neu geplant.

**Scope-Entscheidung 12.10.2026:** 0.9.3.10 ist ein **Stabilisierungsrelease** fuer .NET 8, Simulator-/Hypergrid-Betrieb, die bestehende LSL- und Inventar-/Asset-Grundlage, NV$ Ledger/Viewer/GRID-SERVICES-Masterbank, R287 World API sowie Security, CPU/RAM/GC-Messungen und Recovery. **NexExperiences/Firestorm ist pausiert** und muss im RC-Profil deaktiviert sein; der bestehende Code und die Sicherheitsregressionen bleiben bestehen, sind aber keine Funktionsfreigabe. OGL Pathfinding 2.0, OGL My Outfits 2.0 und eine moegliche OGL Experiences 2.0 werden erst im eigenen Viewer-/Server-Nachfolgeprojekt geplant. OGLVoice und eigener Renderer bleiben separat vertagt. Verbindliche **22 RC1-/23 Stable-Pflichtfaelle** und das neue Evidenzformat v2 stehen in [09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md). Alte EXP-/APP-/NAV-Matrizen sind historisch und nicht als PASS verwendbar.

## A. Ausgangslage und erledigte Vorarbeiten

- 0.9.3.8 Stable veröffentlicht und unverändert als Downgrade-/Rollback-Basis erhalten.
- Pull Request #114: UDP-Watchdog-Leerlauf-Korrektur in `main`.
- Pull Request #115 und spaetere OGLVoice-Branches: vorhandene Architektur-, Discovery- und Media-Gateway-Bausteine bleiben fuer den spaeteren Voice-Train erhalten; kein produktiver Voice-Livetest abgenommen.
- Erste 0.9.3.10-Entwicklungsänderungen: geerbte Mono-/ThreadPool-Maximalbegrenzung aus Simulator-Startup entfernt; .NET-8-Poolwerte werden protokolliert. Produktive Vergleichs-/Lastmessungen offen.
- Die experimentellen Pathfinding-Bausteine sind entfernt und nur noch in der Git-Historie nachvollziehbar.

## A.1 Firestorm Viewer – Inventar-Thumbnail und Outfit-Diagnose

- [x] **InventoryThumbnailUpload Dev-CAP:** Zweistufige LLSD-Upload-URL und JPEG2000-Binaerupload nach Firestorm-Vertrag, mit Owner-/Root-Agent-Pruefung, einmaligen 60-s-Uploadern, 1-MiB-Begrenzung und persistenter Textur-Asset-Speicherung. Unter `[ClientStack.LindenCaps] Cap_InventoryThumbnailUpload = "localhost"` automatisch registriert. Schließt den fehlenden CAP-Namen im Firestorm-Seed.
- [x] **INV01 – offizielle Dev-Implementierung abgeschlossen; bekannte Firestorm-Kompatibilitaetseinschraenkung akzeptiert (10.10.2026):** `InventoryThumbnailUpload`, Asset-Speicherung, persistente Item-/Ordner-Thumbnail-UUIDs und `FetchInventory2`/`FetchInventoryDescendents2` sind serverseitig implementiert. Firestorm 7.2.4.80712 kann das Thumbnail in der getesteten OpenSim/OpenGenesisLINK-Konfiguration hochladen und nach Inventar-Cache-Neuaufbau anzeigen. **Bekannte Einschraenkung:** Beim **Ersetzen** eines bestehenden Outfit-Ordner-Snapshots aktualisiert Firestorm die Vorschau nach normalem Neustart mit bestehendem Inventarcache nicht zuverlaessig (Standardordnersymbol). Ein einmaliges Loeschen des **Inventarcaches** behebt die Anzeige fuer das gespeicherte neue Bild. Kein DB-Datenverlust. Die genaue Verursachung von Server-Versionsabgleich gegenueber Viewer-Cache ist nicht abschliessend bewiesen; die Einschraenkung gilt **nur fuer diese getestete Kombination**, nicht pauschal fuer alle Firestorm-Versionen oder OpenSim-Grids. INV01 ist **als Known Limitation geschlossen, nicht als Cache-Fix bestanden**. #127/#128 sind gemergt. Diese akzeptierte Einschraenkung ist kein eigener Stable-Blocker mehr; andere Release-Gates bleiben unveraendert.
- [x] **Dauerhafte Inventar-/Outfit-Thumbnail-Verknuepfungen im Code:** Neues `ThumbnailID` in `InventoryItemBase` und `InventoryFolderBase`, XInventory-Konverter und Robust-Simulator-HTTP-Connectoren (auch bei getrennt gehostetem Inventardienst), Besitzer-gebundene Speicherung nach erfolgreichem Bild-Upload, additive MySQL/MariaDB-, PostgreSQL- und SQLite-Migration sowie Firestorm-konforme `thumbnail.asset_id`-LLSD-Serialisierung in Item- und Folder-Fetch. **MariaDB-Persistenz und Abruf nach Cache-Neuaufbau live nachgewiesen; vollstaendige Datenbank-Rollout-/Backup-Pruefung fuer Stable separat offen.**
- [x] **Idempotenz-Fix Inventar-Systemordner:** Das bisherige `GetSystemFolders`-Filter `type > 0` schloss den gueltigen `Texture`-Typ 0 aus und erstellte bei erneutem `CreateUserInventory` einen zusaetzlichen `Textures`-Ordner. Filter korrigiert und durch den SQLite-Laufzeittest abgesichert. Bereits vorhandene Duplikate werden **nicht** automatisch geloescht.
- [x] **Firestorm Outfit-Gallery Server-Fix (Dev):** `My Outfits` wird fuer neue Residenten (inkl. HG-Suitcase-Inventar) idempotent als eigener Systemordner provisioniert. Fuer bestehende Residenten: `GET /api/v1/inventory/outfits/health` (`inventory:read`) mit COF-/Saved-Outfit-/defekten Link-Checks ohne Datenveraenderung, `POST /api/v1/inventory/outfits/ensure-folders` (`inventory:write`) legt ausschliesslich den fehlenden `My Outfits`-Systemordner an. Systemordner vor Portal-Loeschen/Verschieben geschuetzt; Thumbnail-UUIDs im Portal-Inventar sichtbar. SQLite-Laufzeitregression unter CI. **Gespeicherter Outfit-Eintrag in der Firestorm-Galerie nach Inventar-Cache-Neuaufbau live sichtbar; COF/`Kein Outfit` bleibt separater offener Abnahmepunkt.**
- [x] **Scope-Entscheidung – My Outfits 2.0 vertagt:** Firestorm Appearance/COF/`Kein Outfit` sind **keine** 0.9.3.10-RC1-Gates mehr. Die Inventargrundfunktionen (Systemordner, Links, Berechtigungen, Persistenz) bleiben als `INV_CORE` und `INV_THUMB` verbindlich. Keine automatische Loeschung oder Reparatur bestehender Outfits; OGL My Outfits 2.0 erst im eigenen Viewer.
- [x] **Viewer-Thumbnail-E2E mit dokumentierter Einschraenkung abgeschlossen:** Neuer Snapshot, MariaDB-Persistenz, Versionsanstieg, Anzeige nach Cache-Neuaufbau und fehlerhafte Aktualisierung nach normalem Relog reproduziert. **Kein** bestandener automatischer Cache-Refresh-Test; Fehler als Firestorm-7.2.4-OpenSim-Kompatibilitaetslimit akzeptiert. `Kein Outfit`/COF ist davon **nicht** abgenommen.

## A.2 Firestorm-Log: historische Voice-/NavMesh-Hinweise (10.10.2026)

- [x] **Analyse und Debugbarkeit:** Firestorm 7.2.4.80712 meldet
  `RetrieveNavMeshSrc` als fehlend und faellt beim Voice-Account-Setup
  auf Legacy-Vivox mit leerer POST-URL zurueck.
  Eigene Servermeldungen fuer ausgeschaltete/noch nicht bereite
  OGLVoice-Provider und nativen A*-Status sowie ein schluesselfreies
  read-only-Konfigurations-Preflight plus CI-Tests dokumentiert.
  Details: `doc/NexVerse/FIRESTORM_VOICE_PATHFINDING_LOG_20261010.md`.
- [x] **Kein OGL Pathfinding in 0.9.3.10:** Der Firestorm-NavMesh-Menuepunkt bleibt inaktiv; es gibt weder eine OGL-Fallback-CAP noch eine Abnahmepflicht.
- [x] **Voice bewusst verschoben:** Der beobachtete Firestorm-Vivox-
  Fallback bleibt als bekannter Zustand dokumentiert. Die noch
  fehlende produktive OGLVoice-/Pion-/LiveKit-Integration ist
  **kein Blocker fuer 0.9.3.10**, weil die Funktion aus dem Scope
  entfernt wurde. Kein fingierter Vivox- oder WebRTC-Endpunkt.

## B. OGLVoice – zurueckgestelltes Arbeitspaket (nicht Teil von 0.9.3.10)

**Status: PAUSIERT, Stand 10.10.2026.** Diese Checkliste ist als technische Projektuebergabe archiviert und wird erst in einem spaeteren, eigenstaendig zu definierenden Release-Train weiterbearbeitet. Keiner der offenen Voice-Punkte unten ist eine Freigabebedingung fuer 0.9.3.10. Kein Gateway-Rollout, kein Provider-/WebRTC-Enable und keine erzwungene Vivox-Ersatzkonfiguration. Bereits implementierte Module, Tests und Dokumentation bleiben erhalten; bestehende Sicherheitsstandards und CI-Regressionen werden nicht abgeschaltet.

- [ ] Bestehenden NexVoice-/LiveKit-Quellcode einschließlich Credentials/Signalisierung sicher prüfen; keine Produktivschlüssel kopieren.
- [ ] OGLVoice Central Control Plane: Tenant-Registry, Auth, Räume, Tokens, Nutzungszählung, Revoke und Audit.
- [x] **Sicherer JWT-Kern und Robust-Token-Authority:** serverseitiges LiveKit-HS256-Signing, kurzlebige raumgebundene Tokens, Explicit-Node-ACL und signierte Simulator-Anfragen; standardmaessig deaktiviert. **Noch kein automatischer Viewer-CAPS-/Region-Join, kein LiveKit-Medien-Gateway und keine Quoten-/Revocation-Control-Plane.**
- [x] **Region-seitige lokale Pruefung:** aktuelle Root-Agent-/Circuit-Sitzung, Estate-/Parcel-Voice, Guest-Herkunft und NPC-Ausschluss; noch nicht an Firestorm/LiveKit angeschlossen.
- [ ] NexVerse als nicht selbst zuweisbaren First-Party-`unlimited`-Tenant, keine künstlichen User-/Sim-/Region-/Minutenlimits.
- [ ] Dritt-Grid-Isolation und konfigurierbare, atomar durchgesetzte Plan-Limits (Abrechnung später).
- [x] **Discovery v1:** C#-Robust-Authority und signierte/authentifizierte Simulator-Provider-Discovery mit bestehendem NexBus-SharedKey, Standalone-Modus und Replay-Abwehr; **nur Provider-Metadaten**, keine LiveKit-/Viewer-Sessions.
- [ ] **Produktivabnahme der Discovery:** gesonderter Zwei-Simulator-/TLS-/Proxy-Test im echten Grid ohne lokale Voice-Schluessel; keine Admin-Secrets im Region-INI.
- [ ] Standalone-Modus über `OpenSim.ini` und getrennte Simulator-Service-Credentials.
- [ ] HG-Gast-Präsenz und Home-Grid-Identität, Rechte des besuchten Grids, Session-TTL und Leave/Teleport-Revoke.
- [x] HG-Identitaetskern: kanonische Heim-Grid-Origin + Original-Avatar-UUID (SHA-256), C#-Regressionstests. **Noch keine authentifizierte HG-Session-/Voice-Anbindung.**
- [ ] Firestorm-WebRTC-CAPS-/Signalisierungs-/Media-Gateway mit funktionierendem Audio, nicht nur Browser-Demo.
- [x] **Erster realer Go Media-Gateway-Daemon:** Pion v4 terminiert SDP/ICE/DTLS/SRTP/SCTP, LiveKit Go-SDK publiziert Firestorm-Opus-RTP und leitet den ersten empfangenen fremden Opus-Track zum Viewer; echte LiveKit-Roster-/Speaking-Datenkanael-Meldungen, Node-Tenant-HMAC/Replay, Quoten, Session-Reaper. Eigenes Go-CI mit lokaler Pion-WebRTC-SDP-Verhandlung. **Noch kein simultaner Multi-Speaker-/Spatial-Audio-Mixer, kein echter Zwei-Firestorm-Plus-HG-Livetest, keine Stable-Freigabe.**
- [x] **Multi-Speaker-Opus-Spatial-Mixer (Dev):** eigene libopus-Decoder je LiveKit-Track, 20-ms-Stereo-Mix/Opus-Encoder je Firestorm-Zuhoerer, 40-m-Hoergrenze, Distanzdaempfung/Panning/Clipping, begrenzte Jitter-Puffer/Quellen, signierte Positions-/Heading-Updates aus `ScenePresence` und Eventweitergabe ueber LiveKit. Vollstaendige Race-/PCM-/RTP-/Audio-CI erforderlich; kein Nachweis im produktiven Grid.
- [ ] Operative LiveKit-/STUN-/TURN-Konfiguration, echtes Firestorm-Audio im Grid mit mehreren Sprechern, HRTF/Occlusion und Multi-Region-Audio, durchgaengige HG-Guest-Voice-Anzeige, sichere Revoke-Pipeline sowie Quota- und Belastungsabnahme.
- [x] **Firestorm-LLSD-CAPS/SDP/ICE-Adapter:** `ProvisionVoiceAccountRequest`, `VoiceSignalingRequest`, `VoiceServerType=webrtc` bei signierter und ausdruecklich freigeschalteter MediaGatewayUrl; signierte, beschraenkte JSON-Media-Exchange und .NET-Protokoll-Regressionen. Default `EnableFirestormGateway=false`. **Eine tatsaechliche kompatible SDP/DTLS/SRTP/SCTP-/LiveKit-Media-Bridge existiert dadurch noch NICHT.**
- [x] **Remote-Speaker-Wire-Format:** Firestorm-konforme JSON-Nachrichten `j`/ `p` / `v` / `l` nach wahrer Avatar-UUID samt Tests. **Noch keine LiveKit-Mediaquelle, die diese Nachrichten in echte WebRTC-Datenkanaele sendet.**
- [ ] Spatial-/Parcel-Voice mit Autorität des Simulators, Echtzeitpositionen und Distanz-/Richtungsmodell.
- [ ] Eigene **graue Voice-Kugel über allen hör- und sichtbar verbundenen Avataren**; Remote-Speaking-Wellen auf dem tatsächlichen Sprecher, inklusive HG-Gästen; Viewerpräferenz respektieren.
- [ ] Group Voice, Direktanruf, Push-to-Talk, Mute, Moderation, Parcel-/Regionwechsel.
- [ ] Multi-Simulator-/HG-/Firestorm-Tests, Token-Tamper-/Cross-Tenant-Tests, Last- und Ausfalltests.

Details: [OGLVOICE_ARCHITECTURE.md](OGLVOICE_ARCHITECTURE.md).

## C. Arbeitspaket Runtime (aus 0.9.3.9)

- [x] Historische `MONO_THREADS_PER_CPU`-Abfrage und ihre irreführende Startmeldung entfernt.
- [x] Feste, aus Mono-/Framework-Zeiten geerbte `ThreadPool.SetMaxThreads`-Begrenzung entfernt, .NET-8-Runtime verwaltet Worker-/IOCP-Grenzen selbst.
- [x] Betroffene Startmeldungen mit OGL-Präfix und deutschen Konsolentexten versehen.
- [ ] Vergleichsmessung vor/nach unter identischer Multi-Region-Last: CPU, Heap, GC-Pausen, Pool-Threads, Warteschlange, Paket-Queue, Tick-Latenz und Watchdog-Warnungen.
- [ ] Startup, Burst-Last, Wiederverbindung, Asset- und HG-Teleport-Stress mit simulierten und echten Clients abnehmen.

## D. OGL Pathfinding — aus 0.9.3.10 vollstaendig gestrichen

Die bisherigen experimentellen Module, Konfigurationen, Testprojekte und NAV-Release-Gates wurden entfernt. Die Neuentwicklung ist spaeter dem eigenen Viewer vorbehalten. Historische Konzepte koennen ueber Git eingesehen werden.

## D.1 NexExperiences – PAUSIERT, nur historischer Dev-Stand

**Entscheidung 12.10.2026:** Die folgenden abgehakten und offenen Dev-Punkte
dokumentieren ausschliesslich die bisherige Implementierung. Die volle
Firestorm-/LSL-/Consent-/K-V-Featureabnahme ist **nicht mehr** Teil von
0.9.3.10. `[NexExperiencesViewer] Enabled=false` ist ein neues
Release-Gate (`SEC_XP_OFF`). Existierender Code, gespeicherte Daten
und die CI-Sicherheitsregressionen bleiben erhalten; kein Auto-Allow,
keine behauptete Produktionsreife. Eine moegliche OGL Experiences 2.0
ist erst spaeter fuer den eigenen Viewer zu planen.


- [x] **Bestehende Grundlage aus 0.9.3.7:** Verwaltungs-/Berechtigungs-/Script-Binding-/K/V-Autoritaet, authentifizierte World API, Region-Adapter und wesentliche LSL-Funktionen inklusive YEngine-Events. Diese Checkliste betrifft die **zusaetzliche vollstaendige Viewer-/Live-Paritaet**, nicht eine pauschale Ruecknahme bereits implementierten Codes.
- [x] **Sicherheits-Hardening (Dev):** Deaktivierte Experiences verweigern auch bereits gebundenen Scripts jede K/V-Operation; Service-API-Schluessel mit nur `experiences:manage` duerfen keine frei waehlbaren Resident-Owner/-Akteure imitieren. Runtime-Regression und CI-Guards vorgesehen.
- [ ] **Firestorm-CAPS und UI – E2E offen:** Bereits implementiert: `GetExperienceInfo`, `FindExperienceByName`, `GetExperiences` und `ExperiencePreferences` mit avatar-gebundenem Allow/Block/Forget (opt-in); lesende `GetAdminExperiences`, `GetCreatorExperiences`, `GroupExperiences`. Seit PR #158 auch `AgentExperiences` GET mit `purchase` sowie kostenfreier POST-Erwerb, `IsExperienceAdmin` und eng begrenzter `UpdateExperience`-Owner-Editor; eigene Verwaltungs-Credential `experiences:viewer:manage` und maximal zehn Experiences je Resident. Ausgelieferte `OpenSim.ini` und `.example` setzen `FirestormReadCaps=true`, `FirestormPermissionCaps=true` und `FirestormManageCaps=true`. Fehlen die separat ausgestellten Schluessel, deaktiviert der Simulator abhaengige CAPS mit Warnung (fail-closed); die eigentliche Live-Abnahme bleibt offen. **Offen:** echter Firestorm-7.2.4-Create/Edit-/Consent-/Forget-Livetest, gruppenbezogene Rechtepruefung weitergehender Schreibfunktionen und nicht implementierte SL-Viewer-Verwaltungscapabilities. **Kein Stable-Nachweis**.
- [x] **Bewohner-Selbstverwaltung als opt-in Dev-Backend:** Isolierter scope `experiences:viewer:permissions` (nur admin-vergebener Service-Key), zentrale `SetOwnResidentPermission`-Operation mit Audit und Persistenz, `GetExperiences` + `ExperiencePreferences` fuer Firestorm mit Allow/Block/Forget und nur zum aktiven Avatar gebundenen CAPS. Der INI-Standard aktiviert `FirestormPermissionCaps=true`, aber fehlende Permissions-Service-Schluessel schalten Berechtigungs-CAPS sowie abhaengige Einwilligungsfunktionen ohne automatische Freigabe ab; Live-Verifikation und Script-Ausloesung stehen aus.
- [x] **Teilfunktion: explizit freizugebende, wartende LSL-Einwilligung:** Im ausgelieferten INI-Standard `ScriptPendingConsent=true` (nur mit gueltigem Permissions-Service-Schluessel funktional); Script-Anfragen mit Berechtigungsstatus `none` werden nur bei aktivem Agent und passender Parcel-/Scriptbindung zeitlich begrenzt zwischengespeichert. Nach ausdruecklichem Allow/Block ueber die bereits vorhandene Firestorm-`ExperiencePreferences`-CAP erfolgt zentrale Neupruefung und das passende LSL-Ereignis. Timeout, Quoten, Region-Shutdown und getrennte Bewohner sind abgesichert. Bei `NativeExperiencePrompt=true` kann der Server mittlerweile den echten optionalen Firestorm-`ScriptQuestionExperience`-Prompt senden; **ein Viewer-`ScriptAnswerYes` ohne authentifizierte `ExperiencePreferences`-Persistierung ist niemals ein Allow**. Ohne native Transport-Unterstuetzung bleibt der Hinweis rein informativ. Echte Firestorm-Tests sind offen.
- [ ] **Echte Einwilligung (Live-Abnahme offen):** Die native opt-in `ScriptQuestionExperience`-Transportschicht, avatar-gebundene `ExperiencePreferences`-Persistierung, `experience_permissions(_denied)`, CAP-Lease-Revoke und Script-Reset-/Relog-Cleanup sind im Dev-Code implementiert und CI-getestet. **Noch kein bestaetigter durchgehender Firestorm-Allow/Block/Forget-Ablauf mit zwei realen Avataren**; keine automatische Berechtigung unbekannter Residents.
- [ ] **Vollstaendige LSL-Paritaet:** Experience-KV-`dataserver`-Events, `llGetExperienceDetails`, `llAgentInExperience`, XP_ERROR-Codes, Ratenlimits, Scriptidentitaet/-besitzer, deaktivierte Experiences, Fehler/Timeouts und Landrechte im echten YEngine-/Viewer-Test.
- [ ] **Persistenz/Betrieb – Teilsicherung implementiert, Live-Gate offen:** Der bestehende JSON-Store wird nun lokal durch einen exklusiven Writer-Lease und Flush-then-Rename-Commits gegen gleichzeitige Robust-Writer abgesichert; beschädigte oder unvollständige Snapshots werden fail-closed abgelehnt. Siehe `EXPERIENCES_SINGLE_WRITER_09310.md`. **Das ist kein SQL-/transaktionaler Active-Active-Store.** Zwei-Robust-/Multi-Host-Strategie, Migrations-/Recovery- und echte Backup-/Restart-/K/V-CAS-Failover-Abnahme sind weiterhin offen; kein Stable-Nachweis.
- [ ] **E2E-Abnahme:** Zwei Residents, eine Besitzer-Experience, ein fremdes Objekt, Allow/Block/Forget, disabled/revoked, Parcel-/Estate-Limits, gruppenbasierte Rollen, HG-Gast, Viewer-Floater, Script-Dataserver und Neustarts auf mehreren Simulatoren.
- Operator-Abnahmematrix (alle Live-Tests anfangs NOT RUN): `doc/NexVerse/RC1_OPERATOR_ACCEPTANCE_09310.md`.

## D.2 World API / R287 – integrierte Portal-Erweiterung

- [x] **Code/CI:** PR #157 dokumentiert die World API und ChatGPT-Anweisungen; PR #159 ergaenzt echte profilgebundene Bildbytes, Event-/Classified-Detail-GET/PATCH/DELETE, eigene Gruppen-Mitgliedschaften, Regionsdetail und konkrete Banking-/Commerce-/Land-Antwortschemata. Die CI auf Commit `b473158` war gruen.
- [ ] **Live-Abnahme:** J2K-Profilbilder, private Avatar- und Assetrechte, Discovery-Owner-/Draft-/Moderationssicherheit, eigene Gruppen/Region und aktualisierte OpenAPI auf laufendem Robust pruefen.
- **Nicht versprochen:** sieben R287-Folgewünsche wie allgemeine Asset-Metadaten, Notifications, verifizierte Bank-Owner-Aufloesung, RSVP und Payment-Credits. Details: [R287_WORLD_API_DELIVERY.md](R287_WORLD_API_DELIVERY.md).

## E. Freigabe-Gates – ohne OGL Pathfinding

- [ ] **Gate 1 – Funktional:** Zwei Simulatoren, lokale/HG-Logins, Region-/HTTP-Portwechsel, reguläre LSL-Kompatibilitaet, NV$-Ledger/Viewer/isolierte Masterbank und Inventar-/Assetgrundfunktionen real nachweisen.
- [ ] **Gate 2 – CI:** Aktueller identischer Release-Build, Login/HG, NexBus, NV$, Inventory, World API sowie vorhandene Experience-**Sicherheits**-Regressionen gruen; CI verhindert Wiedereinfuehrung gestrichener Navigation.
- [ ] **Gate 3 – Security:** Auth-/API-Scopes, Bank-/Script-Payout-Autorisierung, Asset-/HG-Rechte, Limits/Secrets sowie tatsaechlich deaktivierte Experience-CAPS (`SEC_XP_OFF`) pruefen.
- [ ] **Gate 4 – Performance:** CPU/RAM/Heap/GC, Netzwerk, Ticks, Teleport und Watchdog gegen v0.9.3.8 messen.
- [ ] **Gate 5 – Operator:** Neue **23-Fall-v2-Matrix** (22 RC1/23 Stable), zwei reale Avatare, World API, Economy, Inventargrundfunktionen, Lastmessung, Restore und Rollback. Keine EXP/APP-UI-Abnahme.
- [ ] **Gate 6 – Release:** Nach Freigaben RC1 und anschliessend Stable; keine vorgezogene Stable-Kennzeichnung.

**Keine Experience-Feature-, My-Outfits-2.0-, NAV- oder Voice-Release-Gates.** Stattdessen sind die 22/23 stabilitaets- und sicherheitsbezogenen v2-Nachweise verbindlich. Fehlende Live-Betriebsnachweise bleiben Blocker.

## F. Kompatibilität und Releasepolitik

- Die stabile Referenz `v0.9.3.8` bleibt verfügbar; kein Verschieben eines Tags.
- Die aktive Produktversion bleibt `OpenGenesisLINK 0.9.3.10 Dev` fuer Runtime und Stabilisierung; keine oeffentliche Stable-Verwechslung. NexExperiences sind nur archivierter opt-in Dev-Code, keine freigegebene Funktionszusage.
- Keine separate Veröffentlichung `0.9.3.9 Stable`.
- OGLVoice bleibt standardmaessig inaktiv und ist ausdruecklich auf einen spaeteren Entwicklungsabschnitt vertagt. Vorhandenen Code, Tests und Sicherheitsgrenzen beibehalten; keine produktive Gateway-Aktivierung zur Erfuellung dieses Release-Trains.
- Kein OGL Pathfinding oder NavMesh-CAPS in der 0.9.3.10-Version behaupten; eigener Viewer erst in einem spaeteren Projekt.
- Alle zusätzlichen Features nach 0.9.3.10 gehören zu späteren Release-Trains.
