# OGL 0.9.3.12 – Feature-Control-Heartbeat-Warnungen und HG-Käufe

## Warum meldet die Konsole fortlaufend „Ungültige, fremde oder abgelaufene Richtlinie“?

Der World-API-/ROBUST-Dienst veröffentlicht pro Heartbeat eine ES256-signierte
Feature-Richtlinie an NexBus. NexBus kann diese Ereignisse an mehrere
Simulator-Nodes verteilen. Bisher versuchte jede Node, **auch fremde
NodeID-Richtlinien** zu prüfen. Der Sicherheitsfilter verweigerte sie
korrekt, erzeugte aber bei jedem Heartbeat Warnungen.

Zusätzlich löste eine bewusst leere `[NexVerseNodeAgent] NodeId = ""`
unterschiedliche Fallbacks aus: NodeAgent verwendete den Rechnernamen,
OGLFeatureControl beim Start hingegen die leere Zeichenfolge.
Damit konnte die *eigene* signierte Richtlinie nicht angenommen werden.

Der Fix:
- Beide Teile verwenden den **identischen** NodeID-Fallback (Rechnername).
- Richtlinien für andere Nodes werden ohne Warnung ignoriert.
- Nur die eigene NodeID wird der ES256-Signaturprüfung zugeführt.
- Eigene gültige Heartbeat-Erneuerungen protokollieren nur DEBUG.
- Eigene ungültige Richtlinien bleiben **abgewiesen**; die Warnung gibt
  NodeID, Revision und Ablaufzeit an, aber niemals Schlüssel oder Signatur,
  und ist auf höchstens eine Meldung in 120 Sekunden begrenzt.
- Alle bisherigen Ablauf-, Revisions-, Flag- und ES256-Grenzen bleiben aktiv.

Trotz Fallback für leere Werte ist eine **explizite, eindeutige**
`[NexVerseNodeAgent] NodeId` je Simulator empfohlen, beispielsweise:

```ini
[NexVerseNodeAgent]
Enabled = true
NodeId = "mainland-01"

[OGLFeatureControl]
Enabled = true
PublicKeyPath = "config-include/ogl-feature-public.pem"
```

ROBUST muss den *zu diesem Public-Key gehörigen* privaten PEM verwenden.
Prüfen mit `openssl pkey -pubout` auf dem Private-Key und dem
öffentlichen PEM, ggf. SHA-256 der normalisierten öffentlichen Schlüssel
vergleichen. Ein ungültiger oder fehlender Schlüssel darf **niemals**
durch Abschalten der Signaturprüfung kompensiert werden.

Die NodeID muss innerhalb eines Grids eindeutig, stabil und für die
Feature-Control-API gültig sein (Buchstaben/Ziffern/`-`/`_`/`.`,
maximal 128 Zeichen, keine Leerzeichen). Es muss eine gültige
Node-Registrierung und eine passende explizite Policy im OGL Control Center
vorliegen. Fehlt die Policy, bleiben OAR/IAR-Rechte gesperrt.

Nach Aktualisierung **und Neustart jedes betroffenen Simulators**
prüfen, ob das Log eigene gültige Richtlinien meldet (ggf. DEBUG)
und ob im Control Center eine Policy für die richtige NodeID hinterlegt
ist. Systemuhren (NTP) von ROBUST und Simulator prüfen.

## Hypergrid-Shopping bleibt ein separater Vorfall

Die in der vorigen Sicherheitskorrektur deaktivierte **öffentliche**
`HGInventoryService`-Schnittstelle war Teil der traditionellen
Hypergrid-Suitcase-Kommunikation und konnte nachweislich ohne
Avatar-Authentifizierung bearbeitet werden. Die Notfallsperre bewirkt,
dass Standard-HG-Übertragungen bei Fremd-Grid-Aufenthalten scheitern
können – auch die Zustellung bezahlter Gegenstände.

**Der vorliegende Feature-Control-Fix stellt diese Inventarschnittstelle
nicht wieder her.** OAR/IAR-Lizenzen sind nicht der normale
Viewer-Kauf-/Inventar-Transfermechanismus.

Nicht:
- `HGInventoryServiceConnector` am PublicPort reaktivieren;
- `/xinventory` aus dem PrivatePort ungefiltert nach außen leiten;
- eine bloße UUID- oder Grid-SharedKey-Prüfung als Avatar-Autorisierung
  behandeln.

Für vollwertiges sicheres Hypergrid-Shopping ist eine getrennte,
kurzlebige, signierte avatar- und ziel-grid-gebundene
Transferberechtigung mit serverseitiger Freigabe, Replay-Schutz,
Suitcase-Eigentumsprüfung, Audit und Interoperabilitätstests nötig.
Fremde Legacy-Grids müssen das neue Verfahren ebenfalls unterstützen.

Bis dahin Käufe bei fremden Grids vermeiden, Transaktionsbelege sichern
und bei bereits bezahlten, nicht gelieferten Objekten Betreiber und
Verkäufer wegen Lieferung/Erstattung kontaktieren. Zahlungsabschluss,
Inventareintrag und Asset-Zustellung sind getrennt zu prüfen.

Die Änderungen aus diesem Fix sind erst nach neuem Release-Build und
Installation auf der Live-Node wirksam. Die bereits veröffentlichte
unveränderliche Version `v0.9.3.12` enthält diesen späteren Fix nicht.
