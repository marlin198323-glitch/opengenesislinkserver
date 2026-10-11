# 0.9.3.10 – Betreiberentscheidung zur direkten Stable-Schließung

**Datum:** 12.10.2026  
**Gilt ausschließlich für:** OpenGenesisLINK v0.9.3.10  
**Freigabeart:** bewusste Betreiber-Risikoübernahme / Ausnahme vom ursprünglich beschlossenen formalen Release-Nachweisverfahren

## Ausgangslage

- Auf dem vorherigen RC1-Kandidaten-PR #179 war die gesamte GitHub-CI
  inklusive .NET-Release-Build, Login-/HG-Runtime-Smokes und NV$-Datenbankmatrix
  erfolgreich: https://github.com/opengenesislink/opengenesislinkserver/actions/runs/38184043731.
- Der Betreiber bestätigte am 12.10.2026 im Projektchat ausdrücklich:
  **„Die 22 Prüfungen sind ok“**.
- Der Betreiber erteilte anschließend ausdrücklich die Anweisung,
  **„Schließe bitte die Version 0.9.3.10 ab … sollte da im laufenden betrieb
  was sein werde ich das mitteilen … finish“**.
- PR #179 wurde nach grüner CI zur RC1-Versionskennung in `main` übernommen.
- NexExperiences ist im Release-Standardprofil deaktiviert. OGL Pathfinding 2.0,
  My Outfits 2.0 und eine mögliche Experiences 2.0 sind **nicht** Gegenstand
  dieser Version.

## Abweichungen, ausdrücklich nicht als PASS buchen

Die vorgesehene `ogl-release-evidence-v2`-Datei mit datierten
Einzelnachweisen und `evidence_ref` pro Testfall wurde im Projektchat
**nicht bereitgestellt**. Eine 24-Stunden-Soak-Messung und ein einzeln
dokumentierter Restore/Rollback-Nachweis sind ebenfalls **nicht separat
verifiziert**. Die Stable-Matrix `STABLE_APPROVAL` steht daher nicht
als formal bestanden zur Verfügung. Es wird **weder** ein Manifest
gefälscht **noch** die 23/23-Formalprüfung als erfolgreich bezeichnet.

Der Betreiber hat die Veröffentlichung **trotz dieser fehlenden
Formalbelege** und nachgelagerte Meldung eventueller Laufzeitfehler
angeordnet. Dies ist eine **einmalige, transparent dokumentierte
Ausnahmeentscheidung für 0.9.3.10** – keine allgemeine Lockerung
der Release-Gates für folgende Versionen.

## Schutzvorkehrungen

1. Vor Veröffentlichung muss eine **grüne GitHub-CI am exakten Stable-SHA**
   existieren; der automatisierte Herausgeber prüft die
   Versionskennung, Release-Notes, diese Ausnahmeentscheidung und
   dass `main` noch auf dem geprüften SHA steht.
2. Der bestehende `v0.9.3.8`-Release und der dortige Publisher bleiben
   unverändert. `v0.9.3.10` wird nicht überschrieben oder forciert.
3. `[NexExperiencesViewer] Enabled=false` und
   `MasterBankEnabled=false` bleiben in den ausgelieferten
   Standardkonfigurationen; kein unautorisiertes Enablement.
4. Deployment des laufenden NexVerse-Grids ist **ein separater
   Betreiberprozess**. Ein GitHub-Release ersetzt weder gesicherte
   Konfigurationen noch eine überprüfte Binärdatei-Auslieferung.
5. Später gemeldete Fehler werden auf dem **nächsten Patch-/Entwicklungszweig**
   behandelt; veröffentlichte Stable-Tags bleiben unveränderlich.

## Ergebnis

0.9.3.10 wird als **Stable mit ausdrücklich protokollierter
Betriebsrisikoübernahme** geschlossen, sobald der SHA-grüne
Release-Publisher den Tag tatsächlich erzeugt hat.
Bis dahin nur Release-Kandidat; keine vorzeitige Erfolgsmeldung.
