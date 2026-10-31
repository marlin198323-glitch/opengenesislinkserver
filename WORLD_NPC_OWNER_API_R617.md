# R617 – World-NPC-Eigentümerliste aus echten Simulator-Daten

**Stand:** 31.10.2026 · **Repository:** `opengenesislinkserver` · **Zweck:** Backend für den bereits implementierten Bürgerportal-Menüpunkt `/citizen/npc`.

## Funktionsumfang

R617 führt die erste, bewusst begrenzte World-API-Anbindung für **aktuell aktive, im Simulator bestätigte NPCs** ein.

- `GET /api/v1/npcs/mine` verlangt ein persönliches World-Bearer-Token mit `relationships:read`, ein aktives lokales World-Konto und eine passende Principal-ID. Es werden nur NPCs mit exakt dieser Besitzer-UUID ausgegeben. Die API liefert maximal 500 NPCs pro Seite mit einem an den Snapshot-Stand gebundenen Cursor. Bei geänderten Daten zwischen Seiten wird die Abfrage abgewiesen und nicht stillschweigend unvollständig.
- `GET /api/v1/npcs/{npcId}/messages` bestätigt dieselbe persönliche Berechtigung und den Eigentümer. Weil es noch **keine freigegebene NPC-Chat-Archivquelle** gibt, liefert sie bewusst HTTP 503 `npc_chat_archive_unavailable` statt fiktiver Nachrichten. Andere NPCs liefern 404.
- `POST /internal/nexnpc/snapshot/v1` wird ausschließlich zwischen Simulator und ROBUST benutzt. Die komplette Regionsliste wird mit HMAC-SHA256 über `X-NexNPC-Time + "\n" + JSON-Bytes` signiert. Secrets sind mindestens 32 UTF-8-Bytes; Signaturvergleich erfolgt in konstanter Zeit. Meldungen müssen maximal 180 Sekunden alt sein. Jede Region benötigt monoton steigende Sequenzen; doppelte und ältere Meldungen werden abgewiesen.
- Das Simulator-NPC-Modul übermittelt nur wirklich vorhandene, einem Avatar gehörende NPCs. Es übermittelt **keine Nachrichteninhalte und keine persönlichen World-Tokens**. Nach Änderungen sowie regelmäßig alle 25 Sekunden wird die Liste mit HTTPS gesendet.
- Sind gar keine verifizierten Schnappschüsse vorhanden, einer der erwarteten Regionen veraltet (100-Sekunden-Fenster), oder widersprechen sich die Besitzerangaben verschiedener Regionen, liefert die API HTTP 503 statt unzuverlässiger Ergebnisse.
- Normale NPC-Nutzung läuft weiterhin unverändert, wenn die optionale Schnittstelle deaktiviert ist.

**Wichtige Einschränkung:** R617 ist eine *Live-NPC-Liste*, keine persistente Bestandsverwaltung nicht gespawnter NPCs. Um wirklich alle NPCs unabhängig vom Onlinezustand der Regionen anzuzeigen, wäre zusätzlich eine autoritative persistente NPC-Bestandsquelle nötig. Ebenso ist der Chatverlauf ausdrücklich noch nicht umgesetzt.

## Optionale, getrennte Konfiguration

ROBUST/World API, als vertraulich behandelte Konfiguration:

```ini
[NexNPCPortal]
Enabled = true
# Exakte Zahl der Regionen, deren authentifizierte Snapshots erwartet werden:
ExpectedRegionCount = <tatsächliche Regionenzahl>
```

Auf dem ROBUST- und **jedem** beteiligten Simulatorprozess muss derselbe separat erzeugte starke Schlüssel als Umgebungsvariable `NEX_NPC_INGEST_SECRET` vorhanden sein. Keine Secrets im Git-Repository, in dieser Datei oder im Browser ablegen.

Jeder Simulator benötigt zusätzlich:

```ini
[NPC]
Enabled = true
NexNPCSnapshotEnabled = true
```

Simulator-Prozessumgebung:

```text
NEX_NPC_INGEST_URL=https://<vertrauenswürdige-world-domain>/internal/nexnpc/snapshot/v1
NEX_NPC_INGEST_SECRET=<geheimes-zufällig-erzeugtes-geheimnis-mindestens-32-byte>
```

Ohne gültigen Schlüssel, explizite Regionenzahl und aktive HTTPS-Snapshot-Sender bleibt die World-NPC-Liste aus Sicherheitsgründen nicht verfügbar. Konfiguration und Neustart von ROBUST und Simulatoren erfolgen erst nach erfolgreichem Build und abgesicherter Freigabe. Secrets niemals im Chat oder in Logs ausgeben. Die private Ingest-Route sollte zusätzlich durch Reverse Proxy/Firewall ausschließlich für autorisierte Simulator-Netzwerke erreichbar sein; HMAC ist dennoch zwingend.

## Prüfungen

- `dotnet run --configuration Release --project tools/ci/NexNpcDirectoryRegression/NexNpcDirectoryRegression.csproj`: eigenständige dynamische Unit-Tests für die Eigentümerzuordnung, vollständige Regionsabdeckung, doppelte NPCs, Owner-Änderung, Replay-Erkennung und inkonsistente Regionsdaten.
- `python3 tools/ci/verify_npc_owner_bridge_r617.py`: 12 statische Sicherheitstests für HMAC, begrenzte Signaturgültigkeit, Authentifizierung, Besitzerfilterung, Quellprüfung, sichere Abschaltung und API-Dokumentation.
- `dotnet build --configuration Release OpenSim.sln`: vollständiger World-Server- und Simulator-Build durch GitHub CI; auf Linux Mint liegt derzeit kein .NET-Compiler vor.

## Datenschutz und weitere Arbeit

Die vorhandenen Bürger-IM-Archive `NexCitizenImApi` und `INexCitizenImStore` sind **keine** NPC-Chatarchive. NPC-Gespräche dürfen erst nach geklärter rechtmäßiger Aufzeichnung, Transparenz, Einwilligung (sofern nötig), Aufbewahrungs-/Löschkonzept und Zugriffssperre anderer Nutzer serverseitig erfasst werden. Ohne diesen Teil bleibt die Chatansicht im Bürgerportal ausdrücklich nicht verfügbar. Die World-API schützt den privaten Bereich in jedem Fall durch erneute Eigentümerprüfung.

**Freigabe:** World-Repositorium und Stadtportal sind unterschiedliche Anwendungen. Den Stadtportal-Deploy-Befehl nicht als World-Server-Update ausgeben. World-R617 erst nach erfolgreicher GitHub-CI und gesonderter Betreiberfreigabe für den World-/Simulator-Rollout aktivieren. Projektversion v0.7.0.0 und Portal-Fortschritt 71 % bleiben unverändert.
