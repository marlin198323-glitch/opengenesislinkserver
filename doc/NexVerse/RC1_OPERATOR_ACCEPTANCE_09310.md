# OpenGenesisLINK 0.9.3.10 RC1 – Operator-Abnahme (noch OFFEN)

Status: **Entwicklungs-/Testanleitung; kein RC1-Tag und keine Stable-Freigabe.**

**Historisches Betreiberprotokoll 11/38 (Build a6778c9):** [09310_ACCEPTANCE_BLOCKERS.md](09310_ACCEPTANCE_BLOCKERS.md). Seit PR #158 kann `AgentExperiences` bei opt-in `FirestormManageCaps` einen authentifizierten POST für kostenlosen Erwerb verarbeiten; die damalige GET-only-Codeblockade ist behoben, die echte Firestorm-E2E-Abnahme jedoch **nicht** erbracht. `REL01`, `REL02` und `REL07` trotz alter Haken erneut anhand der offenen Experience-Tests bewerten. Für den aktuellen RC-Plan gelten die 48 Fall-IDs aus [09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md). Read-only Preflight: `python3 tools/diagnostics/ogl_voice_pathfinding_preflight.py --sim-config bin/OpenSim.ini --robust-config bin/Robust.HG.ini`.
Aktualisiert: 11.10.2026. Freigabe-Gates: [RELEASE_TRAIN_0.9.3.10.md](RELEASE_TRAIN_0.9.3.10.md).

## Verbindlicher Scope

- **0.9.3.10:** .NET-8-Runtime, NexExperiences fuer Firestorm/LSL mit K/V, Consent und Persistenz; World API R287 und Inventory.
- **Spaeter:** OGL Pathfinding wird erst mit dem eigenen Viewer neu geplant. OGL-Navigation und NAV01–NAV08 sind aus dem Server-Release entfernt.
- **Sicherheit:** Keine gefaelschten NavMesh-CAPS und keine automatischen Experience-Zustimmungen.

## 1. Vorbereitung und beweissicheres Protokoll

1. Notiere Server-/Simulator-Version und den exakten Git-Commit, Firestorm
   Version, `dotnet --info`, eingesetzte Physics Engine, Region-ID/Dimension,
   Datenbanktyp, Robust-/Simulator-Topologie und Datum. Keine Tokens loggen.
2. Sichere die produktive Datenbank, das Inventar, den Experience-Store und
   die Region-INI. Bestaetige **Wiederherstellbarkeit** in einer Kopie.
3. Starte Robust und eine isolierte Developer-Region mit gleichem Build.
   Aktiviere die Testmodule erst nach Kontrollblick auf ihre Config-Defaults.
4. Fuehre den bestehenden Build und alle Tests aus; mindestens:

   ```bash
   python3 tools/ci/verify_stable_release_gate.py
   python3 tools/ci/verify_no_ogl_pathfinding.py
   python3 tools/ci/verify_nexgroups_experiences.py
   ```

   Die vollstaendige GitHub Actions `NexVerse CI` muss am gleichen Commit
   gruen sein. **CI ist notwendig, aber nie hinreichend fuer Stable.**

## 2. Experiences im realen Firestorm

Administrativ drei **verschiedene, minimal berechtigte** API-Keys bereitstellen:
`experiences:script`, `experiences:viewer:permissions` und
`experiences:viewer:manage` (fuer Erwerb und eigenen Profil-Editor).
Diese Schluessel koennen nur per berechtigter Service-Administration
ausgestellt werden und duerfen nie im Viewer, Chat oder Git erscheinen.
In der isolierten Developer-Region die Konfiguration aus
`FIRESTORM_EXPERIENCE_ACQUIRE_09310.md` verwenden:

```ini
[NexExperiencesViewer]
    Enabled = true
    ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
    ViewerPermissionsApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY}"
    ViewerManageApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY}"
    FirestormReadCaps = true
    FirestormPermissionCaps = true
    FirestormManageCaps = true
    ScriptPendingConsent = true
    NativeExperiencePrompt = true
```

Das interaktive LSL-Livetestskript liegt unter
[`tools/diagnostics/ogl_experiences_e2e_test.lsl`](../../tools/diagnostics/ogl_experiences_e2e_test.lsl).
Die schrittweise Anleitung einschliesslich Consent-, Fehlercode-,
K/V-CAS- und Restart-/Persistenztests steht unter
[`EXPERIENCES_LSL_E2E_TEST_09310.md`](EXPERIENCES_LSL_E2E_TEST_09310.md).
Das bisherige `ogl_experience_firestorm_smoke.lsl` bleibt als Minimaltest erhalten.
Eine echte Experience muss dem Script vorher bewusst zugeordnet sein.
Ein zweiter Bewohner darf keine Admin-Rechte haben.

| ID | Firestorm-/Server-Versuch | Erwartetes Ergebnis |
| --- | --- | --- |
| EXP_CREATE | Avatar A klickt im Reiter Eigene auf Erlebnis erwerben, editiert Profil und loggt neu ein | Eigene Experience wird einmalig kostenfrei angelegt und bleibt nur fuer A editierbar; Maximum zehn; keine NV$-Buchung |
| EXP01 | B bittet erstmals per Script um Berechtigung | Native Anfrage bzw. expliziter gesicherter Fallback; **keine** automatische Zustimmung |
| EXP02 | B waehlt Allow im Viewer | Persistentes `ExperiencePreferences` Allow und genau passendes `experience_permissions` |
| EXP03 | B waehlt Block bzw. Deny | Korrekte Fehlermeldung/`experience_permissions_denied`, keine Teilberechtigung |
| EXP04 | B fuehrt Forget aus, meldet sich ab und wieder an | Geloeschtes Recht bleibt geloescht, kein alter Grant |
| EXP05 | Script Reset/Remove im offenen Zustimmungsdialog | Anfrage wird abgebrochen, kein veraltetes LSL-Event |
| EXP06 | B teleportiert auf zweite Region oder loggt aus | Vorherige Experience-CAPS scheitern; keine alten Leases erneut gueltig |
| EXP07 | Zwei gleichzeitige Scripts und zwei Avatare | Deny, Timeout, Allow sind script-/avatar-isoliert |
| EXP08 | Unbefugter Avatar/HG-Gast, gesperrte Parzelle, deaktivierte Experience | Verweigerung trotz alter URLs und Script-Bindung |
| EXP09 | K/V create/read/update-CAS/delete nach Restart von Robust und Simulatoren | Korrekte Werte, Ausfall-/Backup-/Wiederherstellungsnachweis |
| EXP10 | Zwei Robust-Instanzen bzw. produktive zentralisierte Topologie | Auf demselben lokalen Store blockiert die exklusive `.writer.lock` den zweiten Robust; aktive Multi-Host-JSON-Instanzen sind **nicht** unterstützt. Nachweis des einen aktiven Writers plus dokumentierter Recovery-/Wiederanlaufplan, **keine** behauptete SQL-/Active-Active-Freigabe |

EXP01–EXP10 gelten erst als **PASS**, wenn HTTP-/Viewer- und LSL-Ergebnis,
Persistenz nach Re-Login, Berechtigungskontrolle sowie saubere Logs
zusammen dokumentiert sind. Die CI-Tests zu Lease-Revoke, Role-CAPS und
Timeouts ersetzen diese Ende-zu-Ende-Pruefung nicht.

## 3. Weiterhin offene allgemeine Stable-Gates

- **APP01:** Firestorm Appearance `Kein Outfit`/Current Outfit/Links
  mit Bestandsnutzer pruefen, ohne Outfits automatisch zu zerstoeren.
- **INV01:** Akzeptierte bekannte Einschraenkung: Austausch eines
  Outfit-Ordner-Thumbnails wird in Firestorm 7.2.4 mit bestehendem
  Inventarcache teils nicht aktualisiert; Cache-Neuaufbau zeigt das
  gespeicherte neue Asset. **Kein Nachweis einer Fehlerbehebung.**
- **RUNTIME:** Gegen v0.9.3.8 messen: CPU, RAM/Heap, GC, Scheduler-/Region-Ticks,
  Login, Teleport/HG, Inventar, NexBus, NV$, Watchdog und Logwarnungen.
- **RECOVERY:** Backup, Update, Restart, Rollback auf **unveraendertes**
  `v0.9.3.8`, kontrollierte Datenmigration/Restore, Secrets getrennt
  und Rollback bei nicht rueckwaertskompatibler Schemaaenderung.
- **PORTAL:** R287-Bildbytes mit echtem J2K, private Profile, Event-/Classified-Owner-Rechte, eigene Gruppen und Regionendetails ueber Live-API testen. Offen gebliebene R287-Funktionen nicht als fertig kennzeichnen.
- **RELEASE:** Gruener identischer Build-Commit, Maintainer-Review,
  ausgefuellte EXP-/RUNTIME-Resultate, dokumentierte Known Limitations,
  danach erst RC1-Tag. Stable erfordert gesonderte Betreiber-Abnahme.

## 4. Ergebniserfassung

Fuer **jede** Fall-ID: Datum, Region-ID, Server-/Viewer-Build, Testverantwortliche,
`PASS` / `FAIL` / `NOT RUN`, anonymisierter Logausschnitt, beobachtetes
Viewer-/LSL-Verhalten, Reproduktionsschritte, offene Issue/PR-Nummer.
Fehlende Nachweise sind **NOT RUN**, nie pauschal PASS.

**Aktueller Stand bei Erstellung: alle Live-Operator-Fall-IDs NOT RUN.**
Fuer 0.9.3.10 wird weder ein RC1 noch ein Stable-Tag durch diese
Dokumentation automatisch freigegeben.
