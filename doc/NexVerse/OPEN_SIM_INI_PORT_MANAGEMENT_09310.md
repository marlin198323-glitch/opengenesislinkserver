# OpenGenesisLINK 0.9.3.10 – OpenSim.ini und automatische Ports

11.10.2026: Neu auf aktuellem main nach der Entfernung von OGL Pathfinding.
Der geschlossene alte PR #164 darf nicht unveraendert uebernommen werden.

## Bereinigung

bin/OpenSim.ini (Grid) und bin/OpenSim.ini.example (Standalone) wurden von
historischen Kommentaren und auskommentierten Einstellungen befreit.
Alle bisherigen aktiven Schluessel blieben erhalten, ausser den bewussten
Aenderungen an Grid HTTP und XMLRPC. Leere Sektionen bleiben wegen moeglicher
legacy Abhaengigkeiten bestehen. OpenSimDefaults.ini wird nicht veraendert.

## Grid HTTP automatische Portwahl

    [Network]
        http_listener_port = 0
    [NexVerseAutoPorts]
        Enabled = true
        HttpPortMin = 9000
        HttpPortMax = 9099

Ein expliziter positiver Network.http_listener_port hat immer Vorrang.
0 ohne Enabled=true, ungueltige Bereiche und SSL am Hauptlistener werden
abgewiesen. Der Simulator versucht zuerst den lokal gespeicherten Port unter
bin/data/NexVersePorts/http.port und scannt sonst den Bereich. Eine
Dateisperre im Temp-Verzeichnis verhindert kollidierende OGL-Instanzen,
ein TCP-Bind-Test erkennt auch andere Dienste. Fremde Programme koennen
zwischen dieser Probe und der echten Bindung noch ein Rennen verursachen.

Separat konfiguriertes HTTPS wird bei der Portwahl beruecksichtigt.
Standalone behaelt den festen HTTP-Port 9000.

## XMLRPC und Regionen

Im Grid-Profil verwendet XMLRPC.XmlRpcPort=-1 den ausgewaehlten HTTP-Port.
0 deaktiviert RemoteData; positive feste Ports funktionieren weiterhin.
Standalone und OpenSimDefaults.ini behalten XmlRpcPort=20800.
Fremde XMLRPC-Clients muessen die neue Grid HTTP-Adresse verwenden.

Es werden **keine** UDP-Regionsports, NAT-Regeln, Firewalls oder
Reverse-Proxies automatisch konfiguriert. TCP 9000–9099 sowie die
konfigurierten Region-UDP-Ports muessen extern erreichbar sein.

## Abnahme

CI: python3 tools/ci/verify_opensim_autoports.py sowie regulärer Build.
Live-Test mit parallelen Simulatoren und Neustart steht vor Stable noch aus.
Kein Simulator wird durch einen GitHub-Commit automatisch neugestartet.
Lokale Konfigurationsaenderungen vor dem Pull separat sichern.
