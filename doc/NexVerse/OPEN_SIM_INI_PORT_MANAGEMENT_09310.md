# OpenGenesisLINK 0.9.3.10 – OpenSim.ini und automatische Simulator-Ports

Stand: 11. Oktober 2026.

## Bereinigung der INI-Konfiguration

`bin/OpenSim.ini` (NexVerse Grid/Hypergrid) und
`bin/OpenSim.ini.example` (Standalone-Vorlage) enthalten jetzt
nur aktive Konfigurations-Overrides. Nicht gesetzte Werte kommen weiter aus
`OpenSimDefaults.ini` und den `config-include`-Dateien.

Entfernt wurden auskommentierte historische Muster, der fehlerhaft
zerteilte Einleitungstext, die doppelte Sektion `OGLNpcNavigation`
im Beispiel und acht inaktive Zertifikats-Einzeloptionen fuer derzeit
deaktivierte PEM-Konvertierung und Self-Signed-TLS. Die beiden
Deaktivierungsflags selbst bleiben. Alle uebrigen aktiven Einstellungen
wurden unveraendert beibehalten, ausser den nachfolgend dokumentierten
beiden Ports.

Die experimentellen Pathfinding-/NavMesh-Funktionen bleiben deaktiviert.
Die sechs Experience-Flags bleiben aktiv. Eine fehlende Berechtigung
wird dadurch niemals automatisch als `Allow` gewertet.

## Automatische HTTP-Portwahl – NexVerse Grid/Hypergrid

```ini
[Network]
    http_listener_port = 0

[NexVerseAutoPorts]
    Enabled = true
    HttpPortMin = 9000
    HttpPortMax = 9099
```

Nur die absichtliche Kombination aus `http_listener_port = 0` und
`[NexVerseAutoPorts] Enabled = true` aktiviert Auto-Portwahl.

Beim Start nimmt der Simulator zuerst die zuletzt lokal gespeicherte
Portnummer aus `NexVersePorts/http.port` (relativ zu
`Util.configDir()`). Falls belegt, sucht er im angegebenen Bereich
den naechsten freien TCP-Port. Gegen andere automatische OGL-Prozesse
schuetzt eine pro Port gehaltene Betriebssystem-Dateisperre im
Temp-Verzeichnis. Der gepruefte Port wird als wirksamer
`Network.http_listener_port` gesetzt, bevor der Server lauscht und
bevor die Regionsdaten die HTTP-Portnummer uebernehmen.

Bewusst kein zufaelliger System-Port: Damit sind Firewall-Regeln
und dauerhafte Portzuordnungen planbar. Portpruefung ist gegen
fremde Anwendungen nicht atomar; ein fremder Prozess kann im kleinen
Zeitfenster vor dem HTTP-Bind einen Port belegen. Dann bricht der
Simulator mit Bind-Fehler ab, statt eine unzutreffende Regions-URL
zu bewerben. Nach Neustart wird neu geprueft.

Das Grid muss die im Bereich gewaehlten TCP-Ports von aussen
erreichbar machen (Firewall/Weiterleitung/Ingress). Die Wahl eines
freien lokalen Ports oeffnet KEINE Firewall und provisioniert KEINEN
Reverse-Proxy. Region-UDP-`InternalPort` bleibt unveraendert.

Die Standalone-Vorlage bleibt absichtlich bei
`http_listener_port=9000` und `NexVerseAutoPorts.Enabled=false`:
Standalone-Login-`PublicPort` und externe BaseURL muessen bei
dynamischen Ports zwingend mitangepasst werden. Explizit konfigurierte
feste Ports haben in allen Profilen Vorrang.

Auto-Portmodus in Kombination mit `http_listener_ssl=true` ist
derzeit nicht freigegeben; der Simulator verweigert den Start statt
HTTP/HTTPS-Ports oder TLS-Adressen falsch zu registrieren.

## XML-RPC ohne eigenen Port

```ini
[XMLRPC]
    XmlRpcRouterModule = "XmlRpcRouterModule"
    XmlRpcPort = -1
```

`XmlRpcPort=-1` bedeutet: `llRemoteData` nutzt den bestehenden
HTTP-Listener des Simulators und `MainServer.GetHttpServer` verwendet
dieselbe bereits laufende HTTP-Serverinstanz. Keine zweite
TCP-Bindung, keine kollidierenden Standardports 20800 mehr.

`XmlRpcPort=0` deaktiviert die Funktion unveraendert;
positive feste Ports (beispielsweise 20800) funktionieren weiterhin.
`OpenSimDefaults.ini` behaelt zur Rueckwaertskompatibilitaet 20800.
Externe RemoteData-Clients muessen bei Umstellung von einem
separaten Port ihre Ziel-URL auf den tatsaechlichen Simulator-HTTP-Port
aktualisieren.

## Firestorm Experiences – weiterhin abnahmebeduerftig

Die INIs nutzen unveraendert drei **verschiedene**, nicht im Git
gespeicherte Environment-Credentials mit folgenden Scopes:
`experiences:script`, `experiences:viewer:permissions` und
`experiences:viewer:manage`. Der letzte Scope wird fuer den
Firestorm-Button *Erlebnis erwerben* benoetigt. Schluessel muessen
im autorisierten World-API-Adminbereich erstellt werden; ein
Code-Commit erstellt keinen laufenden Admin-/Service-Key.

Die Experience-CAPS und Kauf-/Editierlogik sind im Quellcode vorhanden.
Ein funktionierender Live-Viewer-Workflow sowie Persistenz,
Allow/Block/Forget und Zweitavatar-Sicherheit muessen fuer
0.9.3.10 Stable noch auf dem Developer-Grid abgenommen werden.

## Deployment / Tests

Die lokalen INIs niemals durch ein ungeprueftes `git pull`
ueberschreiben. Vor einem Upgrade sichern und auf Abweichungen
pruefen. Dieser PR aktualisiert nur GitHub; kein laufender
Simulator und kein Admin-Secret wird veraendert.

GitHub CI fuehrt `verify_opensim_ini_cleanup.py` zusaetzlich zu
den bestehenden Experience-, Environment- und
Runtime-Profil-Checks aus. Bestehende XML-RPC-Regressionstests
pruefen weiterhin das Legacy-Verhalten mit festem Port.
