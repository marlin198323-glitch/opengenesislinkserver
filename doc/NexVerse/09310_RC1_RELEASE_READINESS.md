# OpenGenesisLINK 0.9.3.10 – RC1-/Stable-Freigabevorbereitung

**Status: FREIGABE OFFEN.** Der letzte erfolgreich getestete Entwicklungsstand
war `b473158` aus PR #159. Diese Vorbereitung selbst erzeugt weder ein
Release noch einen RC1- oder Stable-Tag. Die letzte veröffentlichte Stable
bleibt das unveränderte `v0.9.3.8`.

## 1. Verbindliche Release-Entscheidung

0.9.3.10 bündelt .NET-8-/ThreadPool-Verbesserungen, OpenGenesisLINKs
optionale native Navigation inklusive der **tatsächlich implementierten**
NPC-/statischen-LSL-Teilfunktionen sowie NexExperiences mit Script-, K/V-,
Consent- und Firestorm-CAPS. R287 erweitert die World API um Profilbild,
Event-/Classified-Detailverwaltung, eigene Gruppen und Regionen-Details.
Der Code dafür ist integriert, aber das Betreibergrid wurde mit den neuen
Endpunkten noch nicht durchgehend geprüft.

**Nicht Teil dieser Stable-Freigabe:** OGLVoice/WebRTC, ein Havok-kompatibles
`RetrieveNavMeshSrc`, der eigene Viewer/Renderer, dynamische
SL-Character-Funktionen, neue Echtgeld-Zahlungswebhooks und die sieben
noch offenen R287-Anforderungen. Sie werden nicht kosmetisch aktiviert.
Bei nicht zertifizierter Physik bleibt die native 3D-Navigation
abgeschaltet und es werden keine falschen Wege ausgegeben.

## 2. Jetzt bereits technisch nachgewiesen

- PRs #156 (Diagnose/Opt-in), #157 (World API OpenAPI/ChatGPT), #158
  (Firestorm Experience-Erwerb/Editor) und #159 (R287-Portal-Abdeckung)
  sind in `main` integriert.
- CI für `b4731582cfbaaaa1a1e5ab07fa41c0759c84399a` vollständig
  grün: <https://github.com/opengenesislink/opengenesislinkserver/actions/runs/38147751531>.
- Version im Quellcode: `0.9.3.10 Dev`; die Schutz-CI hält die alte
  `v0.9.3.8`-Veröffentlichung unverändert.
- In `OpenSim/Framework/VersionInfo.cs` steht noch
  `VERSION_FLAVOUR = Flavour.Dev`. **Nicht ohne Freigabe auf RC1 oder
  Release umstellen.**

Das ist **CI-Nachweis**, kein erfolgreicher Firestorm-Livetest.

## 3. Operator-Abnahme auf exakt demselben Commit

Voraussetzung sind ein funktionierender, gesicherter Robust und mindestens
zwei getrennte Simulatorregionen, darunter `OGL Developer Gen1`, sowie
zwei reale Firestorm-Avatare (Owner A und nichtadministrativer B).
Die Region muss das neue Build laufen lassen; Git-Checkout allein ist
kein Deployment. Für bestehende `bin/OpenSim.ini`-Anpassungen vorher
eine externe Sicherheitskopie anlegen, temporär staschen und nach Update
**manuell mergen**; niemals `git reset --hard`.

**Testmatrix:** Für RC1 sind **47 von 48** Fällen vorab nachzuweisen.
`REL07` enthält die separate Bestätigung der *bereits erfolgten* RC1-
und Stable-Freigabe und wird deshalb erst für **Stable** verpflichtend.
Sie kann logisch nicht vor der RC1-Veröffentlichung PASS sein.
Für Stable sind alle **48/48** Fälle und die gesonderte Betreiberabnahme
erforderlich; eine fehlende Liveprüfung wird niemals pauschal erlassen.

**Pflichtfälle (48):** Alle 38 aus dem Betreiberprotokoll
(`SYS01–04`, `EXP01–08`, `LSL01–06`, `DB01–06`, `NAV01–07`,
`REL01–07`) plus `EXP_CREATE`, `NAV08`, `APP01`,
`PORTAL_IMAGE`, `PORTAL_DISCOVERY`, `PORTAL_GROUPS`,
`PORTAL_REGION`, `PORTAL_AUTH`, `RUNTIME_STRESS` und
`RECOVERY_RESTORE`. **NOT RUN ist nicht PASS.**

- `EXP_CREATE`: mit Firestorm 7.2.4.80712 tatsächlichen
  `AgentExperiences`-Erwerb, Profiländerung, zehn Einträge als Grenze,
  Logout/Login, neue UUID und fremden Editor-Zugriff prüfen.
  Getrennte `experiences:script`, `experiences:viewer:permissions` und
  `experiences:viewer:manage`-Schlüssel sind erforderlich.
- `EXP01–08`, `LSL01–06`, `DB01–06`: echter Prompt und
  Allow/Block/Forget, Script-ID-/Avatar-Leases, LSL-Rückmeldungen, KV-CAS,
  exklusiver Store-Writer, Restart/Backup und mehrere Simulatoren.
  Kein Auto-Allow, keine aktiven JSON-Writers auf zwei Hosts.
- `NAV01–08`: echte Bewegung auf Terrain, Brücke über Boden,
  validierte Treppen-/Rampen-Kanten, Hindernisse, Rechte, Terraforming
  und 4er-VAR-Last. Ohne verifizierte Physik gilt der betreffende
  Test als NOT RUN, nicht als PASS.
- `APP01`: Firestorm `My Outfits`/COF mit Bestandsavatar.
  Die dokumentierte Thumbnail-Cache-Einschränkung bleibt bekannt.
- `PORTAL_IMAGE`: echte OpenSim-J2K-Textur wird als Bild gerendert;
  privates Fremdprofil verweigert, falscher Creator/Asset nicht auslesbar.
- `PORTAL_DISCOVERY`: veröffentlichte Events/Classifieds lesbar,
  Entwürfe unsichtbar, owner-only PATCH/DELETE, POST ohne Fremd-Owner-
  oder State-Eskalation.
- `PORTAL_GROUPS`: `/groups/mine` nur für den eigenen Resident.
- `PORTAL_REGION`: `/regions/{id}` nur mit gültigem `regions:read`.
- `PORTAL_AUTH`: alte Tokens, fremde Principal-ID, defekte UUIDs,
  API-Key-Trennung und fehlende/ungültige Scopes werden abgewiesen.
- `RUNTIME_STRESS`: CPU/RAM/GC, Region-Ticks, Watchdog,
  Login-/Hypergrid-/NV$-Last gegen `v0.9.3.8` dokumentieren.
- `RECOVERY_RESTORE`: PostgreSQL/MariaDB/SQLite (je nach Produktion),
  Inventar, Regionen und Experiences in einer **Kopie** restaurieren;
  Datenmigrationsrisiken vor echtem Downgrade auf v0.9.3.8 prüfen.

## 4. Lokalen Serverstand schreibgeschützt prüfen

Der vorherige Betreiberfehler beim `git pull` wurde durch lokal
veränderte `bin/OpenSim.ini` ausgelöst. Bei beiden Deployments
niemals `git reset --hard` einsetzen. Vor dem Build zuerst:

```bash
cd /opt/developer
python3 tools/diagnostics/ogl_09310_operator_preflight.py \
  --robust-dir /opt/robust \
  --sim-dir /opt/developer \
  --json /root/ogl-release-evidence/09310-host-preflight.json
```

Das Tool ist **read-only**, außer beim optionalen Anlegen des JSON-
Diagnoseberichts. Der Bericht enthält nur Git-SHAs, Branch-Namen,
Konfigurations-**Schaltzustände** und Hinweise auf lokale Änderungen;
weder API-Keys noch Nini-Secretwerte werden ausgegeben. Fehlende
Konfiguration, Git-Divergenz und fehlende Schlüsseltrennung sind
Hinweise auf Handlungsbedarf. Ein identischer Git-Commit beweist aber
**nicht**, dass der .NET-Prozess neu gestartet wurde oder mit diesem
Build läuft. Nini Includes/Environment-Expansion, World-API-Scopes
und echte Firestorm-CAPS müssen getrennt verifiziert werden.

Falls `OpenSim.ini` verändert ist: außerhalb des Repositories sichern,
gezielt `git stash push -- bin/OpenSim.ini`, Git-Update nur mit
`--ff-only`, anschließend `git stash apply` und eventuelle Konflikte
sorgfältig auflösen. Vorhandene Betreiber-Konfiguration nie durch eine
Vorlage überschreiben.

## 5. Nachweise lokal erfassen – automatische Sperre

Es existiert jetzt ein konservativer Offline-Checker für
Freigabe-Evidenz. **Er kann keine echten Viewer-Ereignisse erzeugen,
keine Dokumente verifizieren und keine Freigabe unterschreiben.**
Betreiber und Maintainer kontrollieren die verlinkten Logs unabhängig.

```bash
cd /opt/developer
python3 tools/ci/verify_09310_release_readiness.py \
  --init /root/ogl-release-evidence/0.9.3.10-rc1.json
```

Die Datei beginnt mit `NOT_RUN` für alle 48 Fälle. Der RC1-Checker
fordert davon 47 echte Nachweise; `REL07` bleibt bis zur späteren
Stable-Genehmigung als `NOT_RUN` stehen. Bei jeder
Testausführung `status`, `observed_at`, `evidence_ref` und
`notes` nach echten Beobachtungen ausfüllen. CI-URL und `ci_commit`
müssen **denselben Git-SHA** dokumentieren wie das finale
RC1-Checkout. Keine Secrets, Auth-CAP-URLs oder personenbezogenen
Testlogins im Beleg speichern.

```bash
python3 tools/ci/verify_09310_release_readiness.py \
  --evidence /root/ogl-release-evidence/0.9.3.10-rc1.json \
  --stage rc1
```

Solange die Laufzeitversion noch `Flavour.Dev` trägt, meldet der
Checker **absichtlich nicht freigabefähig**. Wenn die echten
Betreibertests abgeschlossen sind, folgt eine gesonderte kleine
Release-PR mit `Flavour.RC1`, neuer grüner CI am Release-Commit und
erneuter finaler Signatur der Evidenz.

Für eine spätere Stable-Freigabe sind zusätzlich ein verifizierter
RC1-Tag `v0.9.3.10-rc1` im Commit-Vorfahrenpfad, `Flavour.Release`,
mindestens 24 Stunden dokumentierter Testbetrieb, tatsächlich
durchgeführte Wiederherstellung und eine **gesonderte**
Stable-Freigabe erforderlich. Deshalb werden RC1 und Stable niemals
automatisch aus einem grünen `main`-CI-Lauf getaggt.

## 6. Blocker und bekannte Grenzen

Siehe [09310_ACCEPTANCE_BLOCKERS.md](09310_ACCEPTANCE_BLOCKERS.md),
[RC1_OPERATOR_ACCEPTANCE_09310.md](RC1_OPERATOR_ACCEPTANCE_09310.md),
[FIRESTORM_EXPERIENCE_ACQUIRE_09310.md](FIRESTORM_EXPERIENCE_ACQUIRE_09310.md)
und [R287_WORLD_API_DELIVERY.md](R287_WORLD_API_DELIVERY.md).

Die zuletzt manuell berichteten **11/38** beziehen sich auf den alten
Build `a6778c9`. Das ist weder eine Aussage über den jetzt gemergten
Build noch eine echte vollständige Abnahme. Ohne frische Messungen
bleiben die 48 release-relevanten Nachweise **nicht attestiert**.
