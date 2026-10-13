# OpenGenesisLINK 0.9.3.12 – RC1-/Stable-Freigabematrix

**Stand: 13. Oktober 2026. Status: DEVELOPMENT, nicht RC1, nicht Stable.**
Aktuelle veröffentlichte Stable: `v0.9.3.10`. Das aktuelle `main`
enthält bereits 0.9.3.12-Entwicklungsarbeit, trägt in
`OpenSim/Framework/VersionInfo.cs` aber noch `0.9.3.10 Release`.
**Grüne CI allein ist keine Freigabe** und löst keine Versionsumstellung aus.

Diese Matrix ergänzt `LSL_PARITY_09311_09312.md`, die IAR- und
Regionschat-Protokolle und die historischen 0.9.3.10-Freigabedokumente.
Die einmalige Betreiber-Ausnahme von 0.9.3.10 darf nicht stillschweigend auf
0.9.3.12 übertragen werden.

## Festgelegter Release-Scope

0.9.3.12 umfasst LSL Core/Advanced-Parität innerhalb eines dokumentierten,
realistisch abgenommenen Umfangs sowie die bereits in `main` eingebrachten
Kompatibilitäts-/Security-Fixes: Touch-Hold, Chat über Regionsgrenzen,
IAR-Fortschritt und Archiv-Vorprüfung, Inventar-Integrität und bestehende
Robust/Simulator/Hypergrid-Services.

**Nicht als 0.9.3.12 Stable bewerben:** OGLVoice, OGL Viewer
Native-Login ohne separaten E2E-Test, Experiences 2.0,
Pathfinding 2.0, My Outfits 2.0, dynamische Wasser-/Renderer-Engine.
Opt-in-Features bleiben im Produktivprofil deaktiviert, solange ihre
eigene Abnahme nicht vorliegt.

## Releaseblocker (offen bis echte Evidence vorliegt)

| Fall-ID | Live-/Security-Test | Minimaler PASS-Beleg |
|---|---|---|
| BOOT01 | Robust und mindestens zwei Simulatorprozesse auf identischem Build, Login- und HTTP-Portzuordnung | Prozess-/Build-SHAs, Start-/Health-Logs, wirksame Konfiguration |
| VIEW01 | Firestorm: Login, Inventar, Teleport, HG-Rückkehr, Avatar, Gruppen, LSL | Viewer-/Region-Log mit Erfolg und Negativfall |
| LSL01 | Kernfunktionen und neue Advanced-LSL-Funktionen, Signatur, Delay, Rechte | Testmatrix mit echten `PASS`-Logs; ungeprüfte Funktionen bleiben PARTIAL |
| LSL02 | Ein statischer Mouse-Hold >2 s ohne Mausbewegung, danach kurzer Klick und Bewegung | `tools/diagnostics/ogl_lsl_touch_hold_smoke.lsl` mit PASS; keine Events nach release |
| LSL03 | Zwei Avatare und Child-Prim-/PassTouches-Fälle; Delete, Teleport, Logout während Hold | Getrennte Ereignisprotokolle, keine Restschleife |
| CHAT01 | Zwei Regionen im gleichen Prozess und zwei Simulatorprozesse, `llListen` über Grenze | Würfel auf A und Avatar auf B innerhalb 5 m hören Chat |
| CHAT02 | Out-of-range bei >20 m, private Kanäle, Kanten, VarRegions, NexBus deaktiviert | Kein fremder, doppelter oder unautorisierter Chat |
| INV01 | Firestorm Item- und Ordner-Thumbnails, Update/Rename, Reconnect und Logout/Relog | Asset-UUID bleibt in SQL und nach frischem Viewer-Login sichtbar; bekannte Einschränkungen dokumentiert |
| INV02 | OGL-Starterinventar, Outfit-/Systemordner, Rechte, SQLite/MariaDB und Inventar-Konsistenz | Positive und negative Inventarfälle ohne verlorene/vertauschte Objekte |
| IAR01 | Große legitime `save iar`/`load iar`, Stream/API und Konsole | Fortlaufender Konsolenfortschritt; 100 % nur bei Erfolg; importiertes Inventar prüfbar |
| IAR02 | Defekte TAR-/GZip-/XML-IAR, Path traversal, Duplikate, Oversize, ZIP-Bomb-Limits | Import abgelehnt, keine Inventory-/FSAssets-Änderungen |
| IAR03 | Alle direkten Import-Einstiegspunkte, konkurrierende Importe, Dateiwechsel nach Preflight | Kein ungeprüfter Mutationspfad; TOCTOU- und Quota-Tests |
| PORTAL01 | Avatarregistrierung mit Benjamin/Luna im isolierten Test, Validierung/Duplikate, E-Mail | TLS, Berechtigungsgrenzen, sauberes Rollback bei Fehlern |
| PORTAL02 | Deaktivierte Portal-/Viewer-Opt-ins im Produktivprofil | Fehlende öffentliche Endpunkte bzw. fail-closed, keine Secret-Leaks |
| PROF01 | YEngine-Zeit-/Warteschlangenprofile bei vielen Skripten | Belastungsnachweis ohne Blockieren der Script-Threads; keine unautorisierte Offenlegung |
| ECON01 | NV$-Buchungen und Masterbank/Rechtefälle, keine Überziehung/Replay | Reale Ledger-Konsistenz, negative Rechtefälle |
| PERF01 | Regions-FPS, Skript-Zeit, CPU/Heap/GC, Login/Teleport unter Last | Messung gegen 0.9.3.10 mit identischer Workload |
| REC01 | SQL + FSAssets + Regionsdaten sichern und in einer isolierten Kopie wiederherstellen | Wiederhergestellte Avatare, Inventory, Objekt-/Script-Zustand; Restore-Logs |
| RC01 | Eindeutiger Candidate-Commit, passender VersionInfo-Dev→RC1-Wechsel und saubere CI | grüner Release-Build + Runtime-/Datenbanktests auf exakt demselben SHA |
| RC02 | Betreiber prüft alle Fälle und bekannte Abweichungen, genehmigt RC1 | explizite, datierte Freigabe samt Belegreferenzen |
| STABLE01 | 24 h beobachteter Testbetrieb, gesonderter Rollback-Test, Freigabe | Soak-/Recovery-Logs, benannter Betreiber und zweiter Freigabevermerk |
| STABLE02 | RC1-Tag im Vorfahrenpfad; erst danach Release-Flavour/Stable-Tag | SHA-Identität, Release-Workflow, unveränderlicher `v0.9.3.12`-Tag |

Die Tabelle enthält **22 Fälle**. Alle beginnen als **NOT_RUN**, unabhängig
vom Status bestehender Unit- oder CI-Tests. Im RC1-Schritt sind STABLE01 und
STABLE02 noch nicht nötig; sie werden erst für Stable ausgeführt.

## Prüfkommandos (read-only bzw. Build)

Im Arbeitsverzeichnis des tatsächlich verwendeten OpenGenesisLINK-Quellstands:

```bash
git rev-parse HEAD
dotnet build --configuration Release OpenSim.sln
python3 tools/ci/verify_iar_progress.py
python3 tools/ci/verify_portal_registration_iar_preflight.py
dotnet run --configuration Release --project tools/ci/OglIarPreflightRegression/OglIarPreflightRegression.csproj
dotnet run --configuration Release --project tools/ci/CrossRegionChatRegression
dotnet run --configuration Release --project tools/ci/LslConformanceRegression/LslConformanceRegression.csproj
python3 tools/ci/verify_inventory_thumbnail_upload.py
```

Nicht vorhandene Dateien/Befehle oder Fehler sind **FAIL**, nicht „überspringen
= PASS“. Für Live-Tests Simulator und Robust auf demselben SHA neu starten.
Vor Updates lokale INI-/Secret-Dateien sichern und mit `git pull --ff-only`
arbeiten; kein automatisches `reset --hard` und kein Überschreiben von
Produktionsdaten. Tests für fehlerhafte IARs ausschließlich in isolierten
Testkonten ausführen.

## Operator-Evidence (nicht ins Git-Repository mit Geheimnissen)

Pro Fall eine datierte Datei/Logreferenz mit:
- Fall-ID und Ergebnis `NOT_RUN`, `PASS` oder `FAIL`
- exaktem Build-SHA (Robust und alle Simulatoren)
- Versions-/Flavour-Wert, Testregion, DB-Backend, Viewer-Version
- Zeitstempel, ausführender Person, Testschritten, beobachtetem Verhalten
- anonymisierter Log- oder Screenshot-Referenz ohne Token, Passwörter,
  API-Schlüssel oder personenbezogene Daten
- dokumentierter Abweichung und ggf. erneutem Test nach einem Fix.

Kein bloßes Häkchen aus GitHub CI darf einem Live-Test zugeordnet werden.
Ein Bugfix nach RC1 erfordert erneute CI und betroffene Live-Tests auf
dem **neuen** Candidate-SHA. Bei FAIL oder NOT_RUN gibt es **kein Stable**.

## Sofortige Reihenfolge

1. Nur die sicherheits- und kompatibilitätsrelevanten, eng begrenzten
   offenen PRs prüfen und mit grüner CI integrieren. Alte konfliktbehaftete
   PRs weder blind mergen noch als erledigt deklarieren.
2. IAR03, INV01 und LSL02/03 sowie CHAT01/02 unter echten Bedingungen
   schließen. Vor allem sind Stream-/TOCTOU-IAR-Schutz und
   Avatar-/Viewer-Liveabnahme noch keine PASS-Belege.
3. 0.9.3.11-Core- und 0.9.3.12-Advanced-Parität ausdrücklich begrenzen;
   YEngine-Profiler ohne Auth-/Lastprüfung nicht öffentlich schalten.
4. Release-Candidate-Branch mit `AssemblyVersionNumber = "0.9.3.12"`
   und `Flavour.RC1` vorbereiten **erst nach** Scope-Freeze/Verifikation.
5. Betreibergestützte RC1-Abnahme, Soak, Restore und gesonderte
   Stable-Freigabe. `v0.9.3.10` bleibt unverändert als Rollback-Basis.

**Kein Datum und keine „Prozent fertig“-Schätzung ersetzen diese Gates.**
