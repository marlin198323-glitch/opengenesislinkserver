# OpenGenesisLINK 0.9.3.10 RC1 – Operator-Abnahme (Stabilisierung)

**Stand: 12.10.2026 · Noch KEINE RC1-/Stable-Freigabe.**
Verbindliche Fall-IDs und Freigabepolitik:
[09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md).
**22 echte RC1-Nachweise / 23 Stable-Nachweise**; historische
Experience-/Outfit-/NAV-Fall-IDs dürfen hierfür nicht wiederverwendet
werden.

## 1. Scope und sichere Ausgangskonfiguration

- **In 0.9.3.10:** .NET 8, lokale/HG-Logins, Region-CAPS/Ports,
  reguläre LSL- und Inventar-Kompatibilität, NV$-Ledger/Viewer-Economy,
  GRID-SERVICES-Masterbank als separat autorisiertes Funktionsgate,
  World API R287, Security, Last, Backup/Recovery.
- **Vertagt:** NexExperiences für Firestorm, OGL Pathfinding 2.0,
  OGL My Outfits 2.0, OGLVoice und eigener Renderer. **Keine**
  entsprechenden Funktions-/E2E-Pflichtgates. Die vorhandenen
  XP-/LSL-/API-Sicherheitsregressionen bleiben Teil der CI.
- **RC-Profil:** `[NexExperiencesViewer] Enabled=false`;
  `[NexEconomyViewer] MasterBankEnabled=false` im normalen
  Produktionsprofil. Masterbank nur im isolierten Test zeitweilig
  mit voll autorisierter Operator-/Skriptbindung aktivieren.

Die aktuell auf dem Host geladenen Nini-Includes/ENV-Werte und
tatsächlich ausgegebenen CAPS müssen geprüft werden. Eine editierte
INI oder ein Git-Checkout allein ändert keinen laufenden Prozess.

## 2. Betreiber-Vorbereitung (nicht überspringen)

1. Den exakten `git rev-parse HEAD` für **Robust und alle Simulatoren**,
   die .NET-Version, Firestorm-Build, Regions-/Estate-/Datenbank-IDs,
   Topologie und Physikengine dokumentieren.
2. Datenbank, Assets, Inventar, Regions-INI und relevante Betreiber-INI
   außerhalb des Git-Worktrees sichern. Restore an einer Kopie üben.
3. `python3 tools/diagnostics/ogl_09310_operator_preflight.py
   --robust-dir /opt/robust --sim-dir /opt/developer` ausführen.
   Bei lokal geänderter `bin/OpenSim.ini` nie `git reset --hard`.
4. Ein separates Developer-Grid bzw. `OGL Developer Gen1` mit
   zwei Simulatoren und zwei Avataren vorsehen. Vor Liveschritten
   unter beiden Prozessen denselben Release-Build kontrollieren.
5. Ganze GitHub-Actions-Pipeline auf demselben SHA grün nachweisen.
   Dazu gehören bestehende Experience-*Sicherheits*-Regressionen,
   aber **keine** manuelle Experience-UI-Feature-Abnahme.

## 3. Pflichtdurchläufe im Testgrid

Die folgenden Blöcke sind eine **Ausführungsreihenfolge**, keine
Ersatzbelege für die einzelnen 22 Fall-IDs. Für jeden Fall:
Datum, Avatar-/Regions-Kontext (anonymisiert), Git-Build,
Testanweisung, beobachtetes Verhalten, Logs und ein konkreter
`evidence_ref`.

### A – System, Login, Hypergrid und LSL

`CORE_BOOT`, `CORE_LOGIN`, `CORE_HG`, `CORE_REGION`,
`CORE_LSL`: Robust und zwei Simulatoren starten, mehrfach
an-/abmelden, lokale und HG-Teleports, Neustarts und eigene
Region-HTTP-Ports prüfen. Mindestens ein normales Timer-/Event-
und ein XML-RPC-RemoteData-Skript samt Restart ausführen.
Den Viewer nicht durch Experience-Funktionen blockieren lassen.

### B – NV$ Ledger, Viewer und GRID-SERVICES-Masterbank

`ECON_LEDGER`, `ECON_VIEWER`, `ECON_MASTERBANK`:
Transaktionen/Idempotenz, negative Salden-/Status-/Scope-Fälle,
Viewer-Balance und Transfers dokumentieren. Für die Masterbank
**nur isoliert** und mit expliziter Autorisierung:

```ini
[NexEconomyViewer]
    MasterBankEnabled = false
    MasterBankAccountId = "6571e388-6218-4574-87db-f9379718315e"
    MasterBankOperatorId = ""
    MasterBankAuthorizedScripts = ""
    MasterBankMaxPayout = 100
```

Nach erfolgreichem sicheren Simulatorstart
`tools/diagnostics/ogl_master_bank_payout_test.lsl` in ein
Operator-eigenes Root-Prim legen; nach bestätigter
`PERMISSION_DEBIT` liefert `llNexBankRegistrationInfo()`
das echte `ROOT-OBJEKT-UUID/SKRIPT-INVENTARITEM-UUID`-Paar.
Operator-UUID und Paar in die **effektiv geladene** Konfiguration
eintragen, Konto im Ledger auf korrekten Business-Typ prüfen und
vor der positiven Auszahlung bewusst finanzieren. Dann
`MasterBankEnabled=true` nur auf dem Testsimulator setzen
und neu starten. Einen 1-NV$-Payout plus `transaction_result`
und negative Fälle (fremdes Objekt/Skript, keine Debit-Zustimmung,
ungenügende Deckung, Limit überschritten, ungeeigneter Empfänger)
belegen. **Kein** Geld aus dem Avatar-Wallet als Bankauszahlung
interpretieren. Danach wieder deaktivieren, sofern nicht ausdrücklich
und gesondert produktiv freigegeben.

### C – Inventar und World API

`INV_CORE`, `INV_THUMB`: bestandsnahe Inventar-/Asset-/Link-/
Rechte-/Persistenz-Prüfung sowie Thumbnail-J2K-Upload und Abruf
nach Cache-Neuaufbau. Die bekannte Firestorm-Thumbnail-
Aktualisierungseinschränkung ist keine bestandene Cache-Fix-Abnahme.
**Nicht** `My Outfits`-Galerie, `Kein Outfit` oder COF
als funktional fertig bestätigen.

`API_AUTH`, `API_IMAGE`, `API_DISCOVERY`, `API_GROUPS`,
`API_REGIONS`: echte laufende R287-Endpunkte testen, mit gültigem
und ungültigem Scope, Privacy-/Owner- und Zugriffsschutz.
Keine API-Keys oder private Bild-/Assetbytes in Logs veröffentlichen.

### D – Performance, Recovery und Security

`PERF_BASELINE`, `PERF_STRESS`: gleiches Regionen-/Avatar-/
Assetprofil unter `v0.9.3.8` und `0.9.3.10`. CPU, RAM/Heap,
GC, Region-Tick, ThreadPool, UDP-Watchdog, HG-/Teleport-/NV$-
Warteschlangen und Fehlerraten dokumentieren. Keine Verbesserung
behaupten, bevor sie gemessen wurde.

`RECOVERY_RESTORE`: SQL, Inventar, Assets und Regionen in
einer **isolierten Kopie** wiederherstellen, Objekt-/Wallet-
Integrität prüfen und Downgrade-Migrationsrisiken dokumentieren.
Produktions-Datenbanken niemals ungeprüft zurückmigrieren.

`SEC_XP_OFF`: Effektiv geladene `[NexExperiencesViewer]
Enabled=false`, keine Get-/Edit-/Permission-/Prompt-CAPS,
keine auto-gewährten XP-Rechte und keine Reaktivierung durch
fehlende Secrets. Alte Experience-Store-Daten bleiben gesichert,
werden jedoch nicht als Bestandteil einer fertigen Viewer-
Integration veröffentlicht.

`SEC_SECRETS`: Scope-/Owner-/HG-Grenzen, HTTPS, API-Key-
Trennung, Geheimnisschutz sowie getrennte Service-/Viewer-
Identitäten überprüfen.

### E – Deployment und Freigabe

`RELEASE_DEPLOY`: gleiche SHA aller relevanten laufenden
Prozesse, aktiver Build und effective INI, Deploy-/Restartbeleg.
`RELEASE_OPERATOR`: Maintainer und Operator prüfen die
22 anonymisierten Belege, bestätigte bekannte Einschränkungen
und erteilen eine explizite **RC1**-Freigabe.

`STABLE_APPROVAL`: wird erst **nach** dem RC1-Tag geprüft.
Mindestens 24 h beobachteter Betrieb, tatsächlich ausgeführter
Restore/Rollback, taggeprüfter RC1-Vorfahre, `Flavour.Release`
und eigene **Stable**-Signatur. Keine Freigabe aus CI allein.

## 4. Evidenzvorlage und Sperre

```bash
cd /opt/developer
python3 tools/ci/verify_09310_release_readiness.py \
  --init /root/ogl-release-evidence/0.9.3.10-rc1-v2.json

python3 tools/ci/verify_09310_release_readiness.py \
  --evidence /root/ogl-release-evidence/0.9.3.10-rc1-v2.json \
  --stage rc1
```

**Alle Fälle beginnen als `NOT_RUN`.** Nur reale
Operatorbeobachtungen dürfen in `PASS` geändert werden.
Die v1-Datei mit alten Experience-Nachweisen kann für v2
nicht wiederverwendet werden. Der Checker selbst ist kein
Release-Automat; solange `Flavour.Dev` steht, bleibt RC1 gesperrt.

## 5. Archivierte Testanleitungen

Alte `EXP_CREATE`, `EXP01–10`, `LSL01–06`,
`DB01–06`, `APP01` und `NAV*` aus
[09310_ACCEPTANCE_BLOCKERS.md](09310_ACCEPTANCE_BLOCKERS.md)
und den Experiences-E2E-Anleitungen sind **historische**
Entwicklungs-/Debugbelege und keine RC1-Freigabeanforderungen.
Keine bestehenden Sicherheits-Regressionstests entfernen.
