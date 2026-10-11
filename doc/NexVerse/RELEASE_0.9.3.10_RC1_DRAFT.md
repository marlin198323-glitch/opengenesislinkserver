# OpenGenesisLINK 0.9.3.10 RC1 – Release Notes (ENTWURF)

**Status: RC1-PROMOTION ALS BRANCH VORBEREITET / NICHT VERÖFFENTLICHT · Scope 12.10.2026.**
Kein RC1-/Stable-Tag. Erst nach den
[22 RC1-Nachweisen](09310_RC1_RELEASE_READINESS.md)
mit `ogl-release-evidence-v2`, aktualisiertem identischen CI-Commit,
`Flavour.RC1` und getrennter Betreiberfreigabe veröffentlichen.
`v0.9.3.8` bleibt die unveränderte Stable-/Rollback-Basis.

## Geplanter Stabilisierungsumfang

- **.NET 8 / Simulatorbetrieb:** veraltete ThreadPool-Grenzen
  entfernt, mehrere Regionsinstanzen und automatische HTTP-Ports;
  Performancegewinn nur nach CPU/RAM/GC-/Tick-A/B-Messung
  gegen `v0.9.3.8` behaupten.
- **Lokale und HG-Kompatibilität:** Login, Regionübertritt,
  XML-RPC-/LSL-Basis und NexBus über Robust und mehrere Simulatoren.
- **NV$ Economy:** Double-Entry-Ledger, Viewer-Zugriff,
  Kontostatus-/Idempotenz-/Policy-Regeln und gesicherte
  **GRID SERVICES**-Masterbank-Auszahlung an registrierte Skripte,
  **vor Veröffentlichung mit positiven und negativen Live-Tests
  zu verifizieren**. Produktivprofile halten die Masterbank aus.
- **Inventar / Assets:** Besitzer- und Linkrechte, dauerhaft
  gespeicherte Inventar-/Ordner-Thumbnaildaten und API-Inventarbasis.
- **World API R287:** OAuth2/OIDC, OpenAPI/ChatGPT-Verträge,
  geschützte J2K-Profilbilder, Events/Classifieds, eigene Gruppen
  und Regionendetails mit Owner-/Scope-Prüfungen.
- **Security und Recovery:** getrennte Credentials,
  tatsächlich deaktiviertes Experience-Modul, reale Restores
  und Rollbackplan sowie Last- und Restart-Abnahme.

## Nicht enthalten – eigene Viewer-Generation

- **NexExperiences / Firestorm:** ausdrücklich PAUSIERT und
  `[NexExperiencesViewer] Enabled=false` im RC-Deployment.
  Experimenteller Code/Daten/CI-Regressionen bleiben erhalten,
  aber keine Experience-UI-/Consent-/K-V-Funktionsfreigabe.
  Eine OGL Experiences 2.0 ist ein mögliches späteres Viewer-Projekt.
- **OGL Pathfinding 2.0:** neue Navigation erst für den eigenen
  Viewer mit serverseitigen Kollisions-/Autorisierungsgrenzen.
- **OGL My Outfits 2.0:** vollständiger Outfitmanager/COF-
  Viewer-Workflow vertagt; bestehende Inventardaten bleiben.
- **OGLVoice/WebRTC und eigener Renderer:** getrennte Zukunftstrains.

Die bekannte Firestorm-7.2.4-Thumbnail-Cache-Einschränkung
bleibt eine dokumentierte Einschränkung, kein bestandener Fix.
Es gibt keine nachgewiesene produktive Experience-Integration,
keinen freigegebenen Multi-Host-Active-Active-Experience-Store
und keine OGL-NavMesh-CAPS.

## Betreiberhinweise vor RC1

1. `v0.9.3.8` und bestehende DB-/Asset-/Regionsbackups erhalten;
   Wiederherstellung in einer Kopie durchführen.
2. Robust und Simulatoren anhand des aktuellen Release-SHA
   synchron bauen und neu starten. Lokale `OpenSim.ini`-
   Änderungen und Geheimnisse sichern, **kein `git reset --hard`**.
3. `NexExperiencesViewer Enabled=false` an der **tatsächlich
   geladenen** Konfiguration kontrollieren. Masterbank nur im
   isolierten Developer-Simulator mit Operator/Skript-Allowlist,
   Debit-Freigabe und finanziertem Wallet testen.
4. Die **22/22 RC1-Pflichtfälle** samt Logs, CPU/RAM-Messungen,
   bekannten Grenzen und Operator-/Maintainer-Signatur
   aus `09310_RC1_RELEASE_READINESS.md` abnehmen.
5. Auf dem getrennten Promotion-Branch ist `Flavour.RC1` jetzt
   bereits vorbereitet. Die CI muss am **exakten Promotions-Commit**
   erneut vollständig erfolgreich sein und alle 22/22 echten Operator-
   PASS erfordern eine gesonderte Freigabe vor dem Merge/Tag.
   Erst danach Release-Kandidat taggen; keine automatische Stable.

**Stable später:** 23/23 Pflichtfälle, getaggtes RC1 in der
Git-Historie, ≥24 h Soak, dokumentierter Restore/Rollback,
`Flavour.Release` und getrennte Freigabe.

## Freigabestatus

RC1-Commit: **NOCH NICHT VERGEBEN**

Operator-Abnahme: **OFFEN**

Stable-Freigabe: **NICHT ERTEILT**
