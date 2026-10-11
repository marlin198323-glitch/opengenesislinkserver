# OpenGenesisLINK 0.9.3.10 – RC1-/Stable-Freigabevorbereitung (Stabilisierung)

**Stand: 12.10.2026 · FREIGABE OFFEN.** Letzte veröffentlichte Stable-Version:
`v0.9.3.8`. **Dieser getrennte Kandidatenbranch** traegt `0.9.3.10 RC1`; der freigegebene `main`-Stand bleibt `Dev` bis zur Betreiberabnahme.
**Kein RC1-/Stable-Tag, keine Betreiberfreigabe.**
Der zuletzt geprüfte Entwicklungsstand vor dieser Scope-Änderung ist
`1b2db582` (PR #177, grüne CI). Das ist **kein** Inworld-/Release-Nachweis.
Den aktuellen RC-Commit immer neu feststellen und vollständig testen.

## 1. Verbindlicher Scope

0.9.3.10 ist ab jetzt ein **Stabilisierungsrelease**: .NET 8,
Simulator-/Robust-Start, lokaler und Hypergrid-Login, Regionen/HTTP-Ports,
bestehende LSL-Kompatibilität, NV$-Ledger und Viewer-Economy,
**GRID-SERVICES-Masterbank in kontrollierter Testumgebung**,
robuste Inventar- und Thumbnail-Daten, World API R287 mit
Berechtigungsgrenzen, Performance-Messungen und Recovery.

**Nicht Teil der RC1-Abnahme:**

- **NexExperiences/Firestorm:** Entwicklung pausiert. Der vorhandene
  opt-in Code bleibt erhalten und durch CI-/Security-Regressionen geschützt,
  wird aber nicht als funktionierend veröffentlicht. `[NexExperiencesViewer]
  Enabled=false` in der tatsächlich geladenen Simulator-Konfiguration.
  Kein nativer Experience-Prompt, keine Experience-CAPS und keine
  automatischen Berechtigungsfreigaben im RC-Deployment.
  OGL Experiences 2.0 ist nur eine mögliche spätere Viewer-Neuentwicklung.
- **OGL Pathfinding 2.0:** nur späterer eigener Viewer plus erforderliche
  autoritative Serverkomponenten. Kein OGL NavMesh/NPC-Pathfinding in 0.9.3.10.
- **OGL My Outfits 2.0:** erst im eigenen Viewer. Die vorhandene
  Inventardatenhaltung, Systemordner, Assets und Thumbnails bleiben
  kompatibel und durch Tests abgesichert; die vollständige
  Firestorm-Outfit-Galerie/COF-Anzeige ist **kein** RC1-Gate.
- **OGLVoice und eigener Renderer:** getrennte spätere Releases.

Das ist eine **Scope-Entscheidung, keine bestandene technische Prüfung**.
Ein ausgesetztes Feature darf im Release nicht als produktionsreif
beworben oder unkontrolliert aktiv sein. Opt-in-Module behalten vorhandene
Auth-/Consent-/Sicherheits-Regressionstests in der GitHub-CI.

## 2. Neue verbindliche Operator-Matrix (v2)

Die historische Matrix enthielt Experience-abhängige `EXP*`,
`LSL01–06`, `DB01–06`, `REL01–07` und das Outfit-`APP01`.
Diese IDs werden nicht mit einer neuen Bedeutung wiederverwendet.
Sie bleiben in alten Berichten historisch, **ihre Ergebnisse gelten
nicht als Nachweise für v2**. Der vorherige Checker hatte zudem
widersprüchliche Angaben zu 40/48 Fällen.

**Jetzt: 22 Pflichtfälle für RC1, 23 für Stable.** Der zusätzliche
`STABLE_APPROVAL`-Fall setzt einen vorhandenen RC1-Tag voraus.
Im initialen JSON beginnen alle **23 Fälle als `NOT_RUN`**.
Ein grüner CI-Lauf vergibt keinen einzigen Operator-`PASS`.

| ID | Verbindlicher Live-Nachweis |
| --- | --- |
| `CORE_BOOT` | Robust und zwei Simulatorinstanzen auf demselben Build starten; keine kritischen Fehler |
| `CORE_LOGIN` | Zwei echte Avatare, Login/Logout/Reconnect, Avatar-/Inventargrundzustand |
| `CORE_HG` | HomeAgent/Gatekeeper, Hypergrid-Besuch und Rückkehr; Rechte/Identitäten erhalten |
| `CORE_REGION` | Regionsstart/-neustart, unterschiedliche HTTP-Ports, Regionwechsel/Teleport |
| `CORE_LSL` | Reguläre LSL-Skripte inkl. Timer/Events, XML-RPC-RemoteData und Skriptneustart; **kein** Experience-Zwang |
| `ECON_LEDGER` | Reales SQL-Double-Entry-Ledger: Buchung, Kontostatus, Idempotenz, Reversal, keine Überziehung |
| `ECON_VIEWER` | NV$-Balance/Transfers im Viewer; positive und negative Rechte-/Guthabenfälle, Reconnect |
| `ECON_MASTERBANK` | Auf isoliertem Testsimulator autorisierter 1-NV$-Payout aus GRID SERVICES, `transaction_result`, Fremdskript/Debit-/Limit-/Deckungs-Verweigerung; kein Avatar-Wallet-Fallback |
| `INV_CORE` | Inventar, Assets, Links, Systemordner, Berechtigungen, Persistenz nach Neustart und Restore |
| `INV_THUMB` | Item-/Ordner-Thumbnails dauerhaft speichern/lesen und nach Cache-Neuaufbau darstellen; bekannte Viewer-Cache-Grenze ausdrücklich vermerken |
| `API_AUTH` | World API/OIDC, Token-Scopes, abgelaufene/entzogene Tokens, keine fremde Avatar-Impersonation |
| `API_IMAGE` | Echtes J2K-Bild und Asset-/Profil-Privatsphäre prüfen |
| `API_DISCOVERY` | Veröffentlichte Events/Classifieds sichtbar, Drafts verborgen, Owner-Rechte auf Mutationen |
| `API_GROUPS` | `/groups/mine` nur für den authentifizierten Bewohner |
| `API_REGIONS` | `/regions/{id}` nur mit `regions:read`; keine ungeprüften Fremddaten |
| `PERF_BASELINE` | Gleiche Konfiguration/Last wie v0.9.3.8; CPU, RAM/Heap, GC, Threads, Tick-/Queue-Latenz vergleichen |
| `PERF_STRESS` | Dokumentierte Login-/HG-/NV$-/Inventar-Bursttests, Watchdog und Stabilität unter Last |
| `RECOVERY_RESTORE` | Backup von SQL/Inventar/Assets/Regionen in **isolierter Kopie** tatsächlich wiederherstellen; Schema-/Rollback-Risiken bewerten |
| `SEC_XP_OFF` | Live-Konfiguration `Enabled=false`, keine Experience-CAPS/Prompts, kein Auto-Allow; XP-Sicherheitsregression bleibt CI-Pflicht |
| `SEC_SECRETS` | Verschiedene minimale Service-Scopes, keine Keys in Viewer, Skript, Git oder Logs; sichere Portal-/Bankrechte |
| `RELEASE_DEPLOY` | Robust/Simulator tatsächlicher identischer Git-/Build-Stand, Env-Includes, Health/Ports, Restart-Nachweis |
| `RELEASE_OPERATOR` | Reproduzierbare Logs/Belege zu allen Fällen, Known Limitations und explizite RC1-Unterschrift |
| `STABLE_APPROVAL` | **Nur Stable:** erfolgreich getaggtes RC1, gesonderte Betreiberfreigabe, Restore und ≥24 h beobachteter Betrieb |

`ECON_MASTERBANK` ist bewusst ein echtes Funktionsgate:
**MasterBankEnabled bleibt im normalen Profil false.**
Erst im isolierten Test mit korrektem Operator, gebundenem
Root-Objekt-/Skriptinventar-Paar, explizitem `PERMISSION_DEBIT`,
finanziertem Wallet und dokumentierten Ablehnungsfällen aktivieren.
Wer das Gate nicht real abnehmen kann, darf die Masterbank nicht als
Release-Funktion ausweisen und erhält noch **keine RC1-Freigabe**.
Eine spätere erneute explizite Scope-Entscheidung wäre nötig.

## 3. Stabilisierungsplan in fünf Phasen

1. **Scope einfrieren:** keine neuen 0.9.3.10-Funktionen; Experiences
   deaktivieren, Voice und Viewer-2.0-Themen vertagen; aktuelle
   Operator-Konfigurationsdateien sicher außerhalb des Git-Worktrees sichern.
2. **Build-/Sicherheitsbasis:** aktuelle CI am identischen SHA,
   Auth/Scope-/NexBus-/LSL-/Inventarregressionen, zwei Simulatoren und
   Robust zusammen starten, Logs auf Startabbrüche prüfen.
3. **Economy/Inventar/API:** Double-Entry-/Viewer-Transfers,
   GRID-SERVICES-Masterbank unter kontrollierten Bedingungen, Inventar
   und J2K/API-Rechte mit positiven wie negativen Szenarien prüfen.
4. **Last-/Recovery-Abnahme:** reproduzierbare Vergleichsmessung gegen
   v0.9.3.8, Stress, tatsächlicher Restore in einer Kopie,
   Downgrade-/Migrationsrisiken dokumentieren.
5. **RC1-Kandidat:** für **alle 22** Fälle reale PASS-Belege,
   abgeschlossene Operatorprüfung, eigener Release-PR mit
   `Flavour.RC1`, neue grüne CI auf diesem Commit und **erst danach**
   RC1-Tag. Keine automatische Release-Publikation.

**Stable** benötigt alle **23/23** Fälle, den verifizierten RC1-Tag
im Commit-Vorfahrenpfad, `Flavour.Release`, mindestens 24 Stunden
dokumentierten Testbetrieb, ausgeführten Restore-/Rollback-Test und
eine gesonderte Freigabe. `v0.9.3.8` bleibt unangetastet.

## 4. Host-Preflight ohne Datenverlust

Der vorherige `git pull`-Konflikt betraf eine lokal angepasste
`bin/OpenSim.ini`. **Kein `git reset --hard`.**
Robust und Simulator müssen tatsächlich auf demselben aktualisierten
Build laufen. Die Shell-Kommandos sind nur Diagnose:

```bash
cd /opt/developer
python3 tools/diagnostics/ogl_09310_operator_preflight.py \
  --robust-dir /opt/robust \
  --sim-dir /opt/developer \
  --json /root/ogl-release-evidence/09310-host-preflight.json
```

Vor dem Update Betreiber-INI/Secrets sicher sichern, ggf. Änderungen
gezielt stashen und nach `git pull --ff-only` **manuell** zusammenführen.
Prüfen, welche Nini-Includes/Umgebungsvariablen tatsächlich geladen
werden. Der Git-SHA einer Datei beweist noch keinen Prozessneustart.

## 5. Fail-closed Evidenzprüfung

```bash
cd /opt/developer
python3 tools/ci/verify_09310_release_readiness.py \
  --init /root/ogl-release-evidence/0.9.3.10-rc1-v2.json

python3 tools/ci/verify_09310_release_readiness.py \
  --evidence /root/ogl-release-evidence/0.9.3.10-rc1-v2.json \
  --stage rc1
```

Die neue Vorlage hat `format=ogl-release-evidence-v2` und 23
`NOT_RUN`-Einträge. Frühere `v1`-/Experience-Nachweise werden
absichtlich abgewiesen. Für **jeden** RC1-Pflichtfall müssen
`status=PASS`, tatsächliches `observed_at`, konkretes
`evidence_ref` und prüfbare `notes` vorliegen.
`ci_commit` muss dem Release-Checkout entsprechen; die verlinkte
GitHub-CI muss extern geprüft werden. Logs anonymisieren, keine
Secrets/CAPS-URLs veröffentlichen. Der Checker prüft die **Struktur**,
nicht die Echtheit eines Betreiberbelegs.

Die hier vorbereitete `VersionInfo.VERSION_FLAVOUR=Flavour.RC1`
ist **nur eine notwendige, keine hinreichende** Freigabebedingung.
Der Checker verweigert RC1 trotz Versionskennung, solange die
22 verifizierten Operator-PASS, exakte SHA/CI, Known-Limitations-
Pruefung und RC1-Signatur fehlen.

## 6. Historische Protokolle und Grenzen

[09310_ACCEPTANCE_BLOCKERS.md](09310_ACCEPTANCE_BLOCKERS.md),
[RC1_OPERATOR_ACCEPTANCE_09310.md](RC1_OPERATOR_ACCEPTANCE_09310.md)
und die archivierten Experience-Testanleitungen dürfen als
historischer Debug-/Implementierungsstand verwendet werden.
Die früheren **11/38** und **40/48** sind **keine**
Freigabezahlen des neuen Stabilisierungstrains.

Die separate Operator-Anleitung
[RC1_OPERATOR_ACCEPTANCE_09310.md](RC1_OPERATOR_ACCEPTANCE_09310.md)
definiert die praktischen Prüfschritte. Der Stabilisierungs-Scope
ändert keine bestehende produktive Konfiguration automatisch.
