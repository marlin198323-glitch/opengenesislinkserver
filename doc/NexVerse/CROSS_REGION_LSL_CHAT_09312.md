# OpenGenesisLINK 0.9.3.12: Chat über Regionsgrenzen für llListen

## Problem

In OpenSimulator ist `WorldCommModule` ein nicht geteiltes Regionsmodul.
Ein Avatar in Region B erzeugt `OnChatFromClient` ausschließlich in B;
Objekte mit `llListen()` in der benachbarten Region A empfangen die
Nachricht deshalb nicht, auch wenn sie nur fünf Meter entfernt sind.
Die Viewer-Chat-Zustellung zu Child-Agents betrifft **nicht** die
LSL-`listen`-Event-Pipeline.

## Fix

1. `ChatModule` (shared module) liefert normalen Avatar-Chat an
   `IWorldComm` anderer lokal gehosteter Regionen. Die Weltposition
   wird in Zielregionskoordinaten übersetzt und der normale
   `WorldCommModule.DeliverMessage`-Filter prüft vollständige
   3D-Entfernung, Kanal, Avatar-ID, Name und Nachricht.
2. Für Regionen auf anderen Simulatorprozessen veröffentlicht der
   aktivierte `NexVerseNodeAgentModule` nur Chat in Grenznähe
   als signiertes `chat.neighbor.requested`-NexBus-Ereignis.
   Der empfangende NodeAgent übergibt diese Nachricht direkt an das
   lokale `IWorldComm` — nicht an den Viewer-Chat.
3. Die Event-Quelle muss ein echter Root-Avatar sein; Child-Agents
   lösen keine weitere Weiterleitung aus. Lokale Source-Regionen
   werden bei empfangenen NexBus-Events ausgeschlossen, sodass keine
   doppelte Script-Zustellung durch dieselbe Simulatorinstanz erfolgt.
   Der bestehende Event-ID-Deduplikator des NexBus bleibt aktiv.
4. Falsche `WorldCommModule.RemoveRegion`-Event-Abmeldung korrigiert.
   Verwaiste Listener in gelöschten Prims blockieren nachfolgende
   aktive Listener nicht mehr.

## Reichweiten

| Chat | Standardreichweite |
| --- | ---: |
| Whisper | 10 m |
| Say | 20 m |
| Shout | 100 m |

Die Werte werden aus der `[Chat]`-Konfiguration gelesen; der
Abstandsfilter arbeitet mit Weltkoordinaten und unterstützt auch
Variable-sized Regions (VarRegions). Private LSL-Chatkanäle werden
ebenfalls beachtet. Regionweite oder direkte Nachrichten werden **nicht**
als normale Nachbar-Chat-Nachricht weitergeleitet.

## Einrichtung und Sicherheit

Der lokale Regions-zu-Regions-Chat benötigt keine neue Option.
Für mehrere Prozesse müssen beide Simulatoren am **gleichen
authentifizierten NexBus-Verbund** teilnehmen.

```ini
[NexVerseNodeAgent]
    Enabled = true
    CrossRegionChatMessaging = true
```

`PeerUrl`, `SharedKey` und `NodeId` sind ebenfalls wie bei NexBus
erforderlich. In den OGL-`OpenSim.ini`-Vorlagen ist die Option
aktiviert; `OpenSimDefaults.ini` bleibt standardmäßig deaktiviert,
damit unbekannte Standard-Installationen kein zusätzliches
Netzwerk-Chat-Verhalten erhalten.

Remote-Nachrichten werden auf 1023 Zeichen begrenzt, anhand des
Avatar-Schlüssels mit den bestehenden NodeAgent-Ratenlimits gedrosselt,
auf gültige Kanal-/Typ-/Positionswerte geprüft und nur angenommen, wenn
ihr signiertes Event höchstens fünf Sekunden alt ist.
Nur vertrauenswürdige NexBus-Knoten dürfen solche Nachrichten
erzeugen; bei fehlendem NexBus funktioniert der lokale Fall weiterhin.

## Validierung

- `dotnet run --project tools/ci/CrossRegionChatRegression` prüft
  Geometrie, Ecken, Entfernung, VarRegions und Kanaltypen.
- `python3 tools/ci/verify_cross_region_chat.py` prüft
  Event-Verkabelung, Positionsumrechnung und Konfiguration.
- Vor einer Stable-Freigabe ist ein Live-Test mit zwei Simulatoren,
  zwei Würfeln mit `llListen`, Grenzabstand 5 m und Out-of-Range-Test
  bei >20 m nötig.

## Automatisierter Zwei-Regionen-Laufzeittest

`tools/ci/CrossRegionListenRuntimeRegression` initialisiert zwei echte
OpenSimulator-`Scene`-Instanzen in einem Prozess, mit einem gemeinsamen
`ChatModule`, unabhängigen `WorldCommModule`-Instanzen, Avatar-
`TestClient` und zwei Script-Listenern auf der Nachbarregion.

Es werden unter anderem die korrekte Weiterleitung von öffentlichem
`Beep` und `/7 Beep`, exakt ein `listen`-Ereignis je Würfel, der
Nachrichtenfilter, die 20-Meter-Grenze und die 10-Meter-`Whisper`-
Reichweite tatsächlich ausgeführt und geprüft.

Der Test läuft **nach** dem CI-Release-Build und verwendet die gerade
kompilierten Server-Assemblies. Veraltete Test-Mocks wurden für das
aktuelle `IClientAPI`, `IXInventoryData` und .NET 8 aktualisiert.
Die beiden anderen, hier nicht benötigten Legacy-Mocks sind aus
diesem isolierten Testprojekt ausgeschlossen.

**Abgrenzung:** Dieser PASS deckt nur benachbarte Regionen im selben
Simulatorprozess ab. NexBus-Transport über zwei reale Simulatorprozesse,
Viewer-Verhalten und Live-Skripte bleiben separate CHAT01/CHAT02-Gates.

## Automatisierter Zweiprozess-NexBus-Test

`dotnet run --configuration Release --project tools/ci/CrossRegionNexBusProcessRegression`
startet **zwei unabhängige .NET-Prozesse** mit je einer echten
OpenSimulator-`Scene` und einer eigenen `NexVerseNodeAgentModule`-
Instanz. Der Sender nutzt reale `TestClient.Chat`-Ereignisse.
Die Übertragung erfolgt mit dem produktiven `SimulatorNexEventTransport`
via HTTP und `X-NexBus-Signature` (HMAC-SHA256) zum zweiten Prozess.
Das Empfangsprogramm prüft die Signatur, deserialisiert das produktive
`NexBusProtocol` und ruft den echten `DistributedNexEventBus.Receive` auf,
der wiederum den produktiven NodeAgent-Handler mit den Script-Listenern
der Nachbarregion verbindet. Die Registrierung der privaten Callback-
Subscription erfolgt ohne `MainServer` per Reflection; der Test
startet daher keine Robust-Control-Endpoints und keine Produktivservices.

Der Test verifiziert `Beep` auf Kanal 0 und /7, `llWhisper` nur innerhalb
von 10 m, genau einmalige Zustellung trotz erneutem Event-ID-Replay,
Ablehnung ungültiger HMAC-Signaturen sowie die Entfernungsprüfung.
Lokale TCP/HTTP-Ports werden dynamisch vergeben und nur über Loopback
erreichbar gemacht; Prozess-PIDs und Exit-Codes werden geprüft.
Beide NodeAgents benutzen absichtlich denselben `NodeId` (typischer
Fall bei mehreren Prozessen auf demselben Host). Der Sender prüft,
dass ein Avatar >20 m von der Regionsgrenze entfernt **kein viertes
NexBus-Ereignis** erzeugt.

Er testet **nicht** den zentralen NexBus-Hub, die vollständigen
Simulator-/Robust-Startprozesse oder echte Firestorm-Viewer-Pakete. Diese
Unterschiede und der separate Live-Abnahmetest bleiben dokumentiert.

## CHAT01/CHAT02: Reproduzierbarer Inworld-Test

Benutze `tools/diagnostics/ogl_cross_region_chat_smoke.lsl`.
Der Test ist bewusst ein einfaches `llListen`-Skript; auf den
Regionen darf keine spezielle OGL-LSL-Funktion erforderlich sein.

1. Zwei Würfel mit dem Skript auf **Region A** aufstellen, unmittelbar
   an einer Grenze zu **Region B**. In beiden Hover-Texten muss
   `Public: 0 | /7: 0` stehen. Positionen notieren.
2. Ein Avatar stellt sich auf **Region A** 5 m vor die Würfel:
   `Beep` und `/7 Beep` in den Chat eingeben. Beide Würfel
   müssen pro Befehl genau einen Treffer zählen (Kontrollfall).
3. Avatar wechselt auf **Region B**, bleibt 5 m in Weltkoordinaten
   von den Würfeln entfernt und wiederholt beide Befehle.
   Beide Würfel müssen rot werden; Zähler dürfen je Befehl
   jeweils nur um eins steigen (**CHAT01**).
4. Avatar bewegt sich weiter als **20 m** von beiden Würfeln
   weg; `Beep` und `/7 Beep` wiederholen.
   Beide Zähler müssen unverändert bleiben (**CHAT02**).
5. Wiederhole die Schritte 1–4 mit den Regionen auf
   **zwei unabhängigen OpenSim-Prozessen** mit gültigem
   `NexVerseNodeAgent`/`NexBus`; prüfe gleichen Commit-SHA
   und `CrossRegionChatMessaging=true` auf beiden Servern.
6. Negativtest mit **NexBus deaktiviert** bei getrennten
   Prozessen: Übergreifende Weiterleitung darf nicht stattfinden.
   Auf einem gemeinsamen Simulatorprozess muss sie weiterhin
   funktionieren.
7. Wiederhole einen positiven Fall an einer Nord-/Südgrenze;
   anschließend bei einer VarRegion-Grenze, sofern verfügbar.

Die Zahlen in den Hover-Texten sind das maßgebliche Signal,
weil `llOwnerSay` je nach Eigentümerposition/Viewer-Zustand
nicht auf beiden Regionen sichtbar sein muss. Bei Fehlern die
exakten Region-Koordinaten, IDs, Prozess-/Build-SHAs, Kanal,
Nachrichtenzeitpunkte, Skriptzustände und Serverlogs sichern.
In Chat-/Log-Screenshots persönliche IDs gegebenenfalls schwärzen.

**Status dieser Anleitung:** Nur Testprozedur. Kein Live-PASS,
solange die obigen Schritte auf dem Ziel-Grid nicht tatsächlich
durchgeführt und dokumentiert sind.
