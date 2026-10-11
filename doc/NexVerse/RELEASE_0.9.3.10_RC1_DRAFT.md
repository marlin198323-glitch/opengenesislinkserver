# OpenGenesisLINK 0.9.3.10 RC1 – Release Notes (ENTWURF)

**Status: DRAFT / NICHT VEROEFFENTLICHT.** Diese Datei ist eine
Vorbereitung, kein RC1- oder Stable-Tag. Eine Veroeffentlichung erfordert
die realen Operator-Nachweise nach
[09310_RC1_RELEASE_READINESS.md](09310_RC1_RELEASE_READINESS.md).

## Inhalt der geplanten Vorabversion

- Runtime-Bereinigung und .NET-8-ThreadPool-Anpassungen. Keine
  pauschale Aussage ueber Performanceverbesserungen ohne
  Vergleichsmessungen gegen `v0.9.3.8`.
- Native OGL-Navigation mit begrenztem A*-Kern, optionalen
  physikverifizierten 3D-Flaechen und NPC-Wegpunkten sowie der
  tatsaechlich implementierten statischen LSL-Wegsuche.
  Standardmaessig deaktiviert; keine Havok-Kompatibilitaetszusage.
- NexExperiences: eigene sichere Firestorm-CAPS fuer Listen,
  kostenlosen Erwerb (maximal zehn je Avatar), grundlegenden
  Besitzer-Profil-Editor und ausdrueckliche Allow/Block/Forget-Rechte;
  LSL Script-/K/V-Funktionen und ein zentraler
  `SingleWriterJson`-Store. Alle produktiven Experience-Funktionen
  muessen Ende-zu-Ende mit zwei Avataren abgenommen werden.
- Verbesserungen am Firestorm-Inventar, `My Outfits`,
  Systemordnern, Thumbnails und deren Persistenz.
- Erweiterte World API/OpenAPI-3.1-Vertraege und dynamische
  ChatGPT-MD-Dokumentation. R287: profilgebundenes Bild als PNG/JPEG,
  eigene Gruppen, Regionendetails sowie geschuetzte
  Event-/Classified-Detailoperationen.
- Regressionstests fuer Login, Hypergrid, NexBus, NV$,
  Nutzer-/Rollenrechte, Inventory und World API.

## Bekannte und akzeptierte Grenzen

- Kein `RetrieveNavMeshSrc` fuer das graue Standard-Firestorm-
  Pathfinding-Menue; ein spaeterer eigener Viewer ist dafuer geplant.
- OGLVoice/WebRTC bewusst auf einen anderen Release-Train verschoben.
- Keine vollstaendige SL-Character-LSL-API und kein
  automatisches Navigieren auf ungeprueften Physikoberflaechen.
- Firestorm 7.2.4: ein geaendertes Outfit-Snapshot-Thumbnail
  benoetigt unter Umstaenden einen Inventar-Cache-Neuaufbau.
- Experience-`SingleWriterJson` ist **kein**
  Multi-Host-Active-Active-Store.
- R287: Kein offener, beliebiger Asset-Browser, keine zentrale
  Notification-Inbox, keine Bank-Owner-Personenauflösung,
  kein RSVP-System und keine externen Payment-Credits.
- Die profilgebundene Asset-Creator-Pruefung ist konservativ;
  Bilder anderer Ersteller koennen mangels Assetlizenzbeweis
  abgelehnt werden.

## Betreiberhinweise vor dem Rollout

1. `v0.9.3.8`-Stand und alle Datenbank-, Inventar- und
   Regionsbackups unveraendert verfuegbar halten.
2. `/opt/robust` und `/opt/developer` getrennt aktualisieren,
   ihre lokalen `OpenSim.ini`-Anpassungen sichern und sorgfaeltig
   mergen; neue Config-Optionen nicht ungeprueft global aktivieren.
3. Nur isolierte Developer-Sim testen, drei separate
   Experience-Service-Schluessel verwenden.
4. Build-SHA, CI-URL, Release-Candidate-Testprotokoll, Firestorm-
   Logeinsicht, Restore und Maintainer-Freigabe dokumentieren.
5. Erst nach allen zwingenden Gates `Flavour.RC1` auf
   einem gesonderten, CI-geprueften Release-Commit setzen.
   **Keine automatische Stable-Veroeffentlichung.**

## Freigabe

RC1-Commit: **noch nicht vergeben**

Operatorabnahme: **OFFEN**

Stable-Freigabe: **NICHT ERTEILT**
