# Mesh-Upload in Firestorm: CE/502 und LLSD-Kostenschätzung

## Diagnose (2026-10-22)

Firestorm meldet beim Mesh-Upload wiederholt:

```
POST http://mainland.stadt-nexverse.de:9000/CE/<uuid> 502 Bad Gateway
```

`/CE/<uuid>` ist in OGL der **EventQueueGet**-Long-Poll-Endpunkt, nicht
der Mesh-Upload-Endpunkt. `EventQueueGetModule.NoEvents()` liefert
für eine vorhandene, aktuell leere Queue absichtlich `502 Bad Gateway`;
für eine unbekannte/geschlossene Queue dagegen `404`.
Daher ist **502 allein kein Beweis**, dass der Mesh-Upload oder der
Reverse-Proxy defekt ist. EventQueue-502 darf nicht ohne ein
Protokoll-/Viewer-Kompatibilitätstest zu 200 geändert werden.

Ein eigenständiger Fehler war jedoch in der **Mesh-Gebührenberechnung**
vorhanden: `MeshModelCost()` versuchte, LLSD-`instance_list`-Elemente
als `Hashtable` und `scale` als `ArrayList` zu lesen.
Die Viewer-Daten sind `OSDMap` und `OSDArray`; gültige Uploads
konnten dadurch einen `InvalidCastException` auslösen, anschließend
`state=error` und eine leere `uploader`-URL liefern. Der neue Parser
liest typisierte LLSD-Daten und lehnt ungültige Skalierungen und
Mesh-Indizes ausdrücklich ab. Binäre Mesh-Blöcke werden ebenfalls
typgeprüft.

## Nach Update überprüfen

1. ROBUST und **betroffenen Simulator** mit demselben neuen
   GitHub-`main`-Stand bauen. Bestehende Deployment-Secrets behalten.
2. Den Simulator neu starten. Sicherstellen, dass Port
   `mainland.stadt-nexverse.de:9000` extern wirklich beim **aktiven**
   Simulator-HTTP-Listener ankommt. Bei `[NexVerseAutoPorts] Enabled=true`
   kann der Prozess eine andere Portnummer aus 9000–9099 wählen.
   Reverse-Proxy-Port/Portfreigaben entsprechend abgleichen. HTTP-Port
   nicht mit dem UDP-Regionsport verwechseln.
3. In Firestorm dasselbe Mesh erneut hochladen; zuerst die
   **Kostenberechnung**, dann den separaten **POST an die zurückgelieferte
   `uploader`-URL** beurteilen. Nach erfolgreicher Gebührenanfrage muss
   das LLSD-Feld `state=upload` und eine nichtleere `uploader`-URL
   geliefert werden.
4. Serverlog unmittelbar zum Upload-Zeitpunkt betrachten:

```bash
grep -Ei 'MESH UPLOAD|NewAgentInventory|AssetUploader|BUNCH OF CAPS|EVENTQUEUE|Exception|HTTP' /opt/developer/bin/OpenSim.log | tail -n 120
```

Wenn `[MESH UPLOAD]: Fee validation rejected` erscheint, die konkrete
Fehlermeldung prüfen. `Insufficient funds` bzw.
`Insufficient permissions to upload` sind eigenständige Gründe.
Ein `/CE/`-502 ohne fehlgeschlagenen Upload-/Gebühren-HTTP-Aufruf kann
ein normales EventQueue-Long-Poll-Ende sein.

**Nicht durch CI abgedeckt:** Öffentliche Proxy-Erreichbarkeit,
Firewall-Regeln und der tatsächliche Mesh-Upload von Firestorm
gegen die produktive Region. Dafür sind Simulatorzugriff und ein echter
Viewer-Praxistest nötig.

## Economy-Key und Gruppenhistorie

Der World-API-Control-Center-Economy-Generator gibt für **neue**
Simulator-Economy-Schlüssel die Scopes
`economy:read`, `economy:transfer` und `groups:read` aus.
Damit kann die Firestorm-Gruppenhistorie nach lokaler
`GroupPowers.Accountable`-Prüfung geladen werden. Bereits erzeugte
Schlüssel werden nicht stillschweigend umgestellt. Für Rotation erst
den neuen Schlüssel generieren, Simulatoren umstellen und danach
den alten Schlüssel deaktivieren.
