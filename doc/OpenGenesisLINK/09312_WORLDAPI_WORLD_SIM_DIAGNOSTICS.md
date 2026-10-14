# OpenGenesisLINK 0.9.3.12 – World API und Robust-/Simulator-Diagnose

**Stand:** 14. Oktober 2026. Betrifft OpenGenesisLINK (OGL) als Serverprodukt; Stadt-NexVerse ist ausschließlich ein betriebenes Grid. Diese Änderungen modifizieren weder Datenbanken noch FSAssets-Inhalte automatisch.

## 1. OGL World API Control Center

Unter `GET /api/v1/docs` befindet sich die GridUser-Anmeldung (Passwort und UserLevel >= 200). Nach erfolgreicher Anmeldung wird das vollständige Control Center über den geschützten Endpunkt `GET /api/v1/docs/panel` geladen.

Die eigentliche Dashboard-Datei liegt im Repository unter `NexVerse/Server/Api/NexApiDocsPage.cs`; das ist ein historischer technischer Namespace, **kein** Produktname. Die Oberfläche enthält den neuen, deutlich erkennbaren OGL-Kopfbereich, große neonbeleuchtete Hero-Fläche, Live-Systemstatus, neu organisierte Sidebar und futuristische Karten-/Fensterränder. Animationen respektieren `prefers-reduced-motion`. **Robust neu bauen und neu starten**, da das HTML direkt in der kompilierten DLL steckt. Ein reines `git pull` ohne neuen Build aktualisiert die Weboberfläche nicht. Nach Deployment per Browser mit Strg+F5 neu laden.

Die Daten für Serverversion, API-Vertrag, Releaseverlauf und Roadmap stammen weiterhin aus `/api/v1/version` und `/api/v1/openapi.json`. Seit dieser Änderung sind 0.9.3.12 und passende Changelog-Einträge in den strukturierten OpenAPI-Daten enthalten.

### Lizenzverwaltung / Feature Control

`GET /api/v1/feature-control/nodes` ist ein **optionaler** Verwaltungsdienst. Er wird nur registriert, wenn im Robust-Server `[OGLFeatureControl] Enabled = true` gesetzt **und** ein gültiger ES256-P-256-Schlüssel für die signierten Node-Richtlinien vorhanden ist. Ein HTTP-404 aus der Standard-API bedeutet in diesem Fall normalerweise: Dienst ist deaktiviert, nicht dass ein bezahlter Lizenzstatus ungültig wäre.

Die mitgelieferten aktiven OGL-Konfigurationen und Vorlagen setzen seit dieser
Aktivierung `[OGLFeatureControl] Enabled = true`. **Vor dem Upgrade der Live-Instanzen**
müssen der private ES256-Schlüssel auf Robust, der öffentliche Schlüssel auf allen
Nodes sowie die signierten Node-Freigaben vorbereitet werden. Ohne privaten Schlüssel
scheitert die Initialisierung der Robust-World-API; ohne signierte Freigaben sperren
Simulatoren OAR-/IAR-Operationen. Ein Merge oder Pull ersetzt keine individuell
betriebene Konfiguration und verteilt keine Schlüssel.

Die UI zeigt deshalb den deaktivierten Zustand mit Konfigurationshinweis statt `Unknown NexVerse API endpoint`. Die Funktion wird nicht automatisch aktiviert oder mit schwachen Default-Schlüsseln betrieben. Für vorhandene Node-Regeln bleiben die eigenen `simulators:read`-/`simulators:manage`-API-Berechtigungen erforderlich – der Dashboard-Zugang per UserLevel ersetzt **nicht** die Prüfung schreibender API-Operationen.

## 2. JPEG2000 / EOC / World Map Freiburg Sued

Beispielausgabe:

```text
[WORLD MAP]: Kartenbild für Freiburg Sued wird erzeugt
Decoded image with unhandled number of components: 0
[WARNING]: EOC marker not found. Codestream is corrupted.
```

Dies weist auf nicht vollständig oder gar nicht dekodierbare Textur-Bilddaten (JPEG2000 / Codestream) **oder einen inkompatiblen Decoderpfad** hin. Die unmittelbar vorangehende Kartenbildmeldung macht einen Zusammenhang plausibel, beweist die verursachende Asset-UUID aber noch nicht. Eine fehlende EOC-Markierung (End Of Codestream, `FF D9`) kann bei abgebrochenen Bilddaten auftreten; ein `components: 0`-Ergebnis ist kein gültiges RGB-/RGBA-Bild.

OGL hat bereits **LunaTexture / Warp3D** mit pro-Asset-Diagnose und Fallback. Zur sicheren Untersuchung auf dem Simulator zuerst die betroffene Region auswählen und folgende **lesenden** Befehle ausführen:

```text
nex texture status
nex texture inspect <texture-uuid>
```

Die Diagnosen nennen u. a. TextureID, Primname, Position, Klassifizierung und Anzahl. Bei aktivierter Diagnose liegen persistierte JSON-Daten standardmäßig unter `bin/data/lunatexture` bzw. dem für die Simulator-Instanz konfigurierten `LunaTextureDiagnosticStore`; bei anderem Arbeitsverzeichnis Pfad entsprechend prüfen. Die Einträge sind regionsbezogen.

Danach prüfen, ob die gemeldete UUID tatsächlich in der Asset-Datenbank und im dazugehörigen FSAssets-Speicher vorhanden ist und ob der Inhalt als JPEG2000 lesbar ist. Mit der zuvor beobachteten IAR-Warnung über **14 fehlende/beschädigte/ungültige Assets** vergleichen. Nicht vorschnell Datensätze löschen oder beschädigte Assets mehrfach importieren. Eine Reparatur setzt das konkrete fehlerhafte Asset oder eine gültige Sicherung voraus. Bei unerwarteten Dekodierfehlern trotz formal gültiger Codestreams den verwendeten OpenJPEG-/CSJ2K-Fallback und die installierte native Bibliotheksversion vergleichen.

**Noch offen:** Der konkrete JPEG2000-Codestream wurde aus dem Laufzeitlog allein nicht identifiziert oder reproduziert. Es wäre unseriös, ihn als repariert auszugeben.

## 3. Externer XML-RPC-Eingang

```text
[BASE HTTP SERVER]: Fail to decode XMLRPC request 160.250.132.238:53372: Root element is missing.
```

Der Remote-Client hat keine gültige XML-RPC-Anfrage geliefert. Ein einzelner Eintrag reicht nicht, um einen Angriff oder eine fehlerhafte OGL-Installation zu behaupten. Der Parser lehnt malformed XML weiterhin ab; in OGL wird bei einem Deserialisierungsfehler nun explizit **HTTP 400 Bad Request** ausgegeben, statt still einen falschen 404-Endpunktbefund zu erzeugen. Die Warnung bleibt mit Gegenstellen-IP nachvollziehbar.

Bei wiederholter Last sollten Reverse-Proxy-/Firewall-Rate-Limits auf dem öffentlich erreichbaren XML-RPC-Listener geprüft werden. **Keine pauschalen IP-Sperren aus nur einer Logzeile** und keinen XML-RPC-Legacy-Support löschen: HG/LSL-Kompatibilität kann davon abhängen.

## 4. Hypergrid-IM / UserAgent-Lookup

```text
[HG IM SERVICE]: Benutzer ist nicht lokal vorhanden. Standort wird über UserAgentService ermittelt
[HG IM SERVICE]: Benutzer ... konnte nicht lokalisiert werden
```

Ein Remote-/Hypergrid-Benutzer kann bei IM-/Statusbenachrichtigungen zeitweise keine gültige aktuelle Region besitzen, etwa offline, nach Teleport, bei verzögertem HomeAgent-State oder bei nicht erreichbarem Ziel-Grid. Bei unbekanntem Empfänger versucht OGL zunächst die bekannten Location-/Presence-Pfade, danach den UserAgentService.

Die **fehlgeschlagenen LocateUser-Diagnosen** werden pro Ziel-UUID auf höchstens eine konsolidierte Debugmeldung innerhalb von 60 Sekunden begrenzt. **Jede IM-Anfrage läuft unverändert durch die echten Zustellversuche und den bestehenden Offline-IM-Fallback**. Es gibt keine künstliche negative Zustellcache-Sperre und keine Maskierung echter Fehler im UserAgentService.

Zur weiteren Eingrenzung: HomeAgent-URI, Presence-Records, Region-ServerURI, Forwarding-Status und Offline-IM-Konfiguration vergleichen; IM-Zustellung bei Rückkehr des Empfängers auf einen aktiven Simulator testen. Ohne Reproduktionsnachweis kann nicht behauptet werden, dass der fremde Avatar dauerhaft unzustellbar ist.

## 5. API- und ChatGPT-Anweisungen

Die Dateien unter `/api/v1/chatgpt-instructions.md`, `/api/v1/chatgpt-buergerportal.md` und `/api/v1/chatgpt-admin.md` werden bei jedem Aufruf aus der laufenden API generiert. Der Text verwendet nun **OpenGenesisLINK** als Softwareprodukt, nennt die aktuelle `OpenSim.VersionInfo.Version`, verweist auf den wirklich laufenden OpenAPI-Vertrag und behandelt den optionalen Lizenzdienst korrekt. Die URL-Routen und Legacy-Feldnamen (`x-nexverse-*`, `X-NexVerse-Api-Key`) bleiben aus Kompatibilitätsgründen erhalten.

## Betrieb und Abnahme nach eigenständigem Server-Upgrade

1. Aktuellen GitHub-Branch `main` beziehen und neu bauen, Robust **und** die betroffenen Simulator-Prozesse neu starten, ohne produktive `.ini`- und FSAssets-Konfigurationen zu überschreiben.
2. `GET /api/v1/version` auf 0.9.3.12 prüfen; das neue Control Center im Browser mit neuem Cache laden.
3. Aus dem Dashboard die aktuellen ChatGPT-Anweisungen herunterladen und auf OGL-Branding und angezeigte Laufzeitversion kontrollieren.
4. Ist `[OGLFeatureControl]` deaktiviert, muss die UI diesen Zustand anzeigen. Bei aktiviertem Dienst und gültigen Rechten müssen die Node-Daten erscheinen.
5. Den Kartenbildlauf **Freiburg Sued** mit `nex texture status` beobachten und konkrete UUIDs sammeln. Keine produktiven Assets automatisch verändern.
6. Simulierte ungültige XML-RPC-Eingaben mit HTTP 400 beantworten, gültige Legacy-RPC-Aufrufe müssen weiterhin funktionieren.
7. Wiederholte HG-IM-Nachrichten an einen offline/nicht lokalisierten Avatar auslösen; innerhalb von 60 Sekunden nur die Debugmeldung reduzieren, aber erfolgreiche erneute Zustellung nach tatsächlichem Login weiter erlauben.

**Grenze der vorliegenden Prüfung:** Ein erfolgreicher Quellcode-Build oder CI-Lauf ersetzt nicht diese Tests im laufenden Robust-/Simulator-Betrieb.
