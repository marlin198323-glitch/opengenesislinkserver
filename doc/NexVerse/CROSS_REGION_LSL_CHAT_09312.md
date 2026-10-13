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
