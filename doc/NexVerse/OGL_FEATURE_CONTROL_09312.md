# OpenGenesisLINK 0.9.3.12 – Deutsche Konsole und OGL Feature Control

## Überblick

Der Simulator zeigt standardmäßig die deutsche OGL-Hilfe: `help`, `help all`,
`help Archiving`, `help Regionen`, `help suche oar`.
Befehlsnamen selbst (z. B. `load oar`) bleiben wegen Skript-/OpenSim-
Kompatibilität unverändert. Die Übersichten und Kurzbeschreibungen werden
deutsch generiert; nicht katalogisierte Befehle erhalten eine allgemeine
deutsche Bereichsbeschreibung. Es handelt sich **nicht** um eine Übersetzung
jeder einzelnen historischen OpenSimulator-Ausgabe außerhalb der Hilfe.

In `[Startup]`:

```ini
OGLConsoleGermanHelp = true
OGLConsoleExpertHelp = false
```

Im Normalmodus werden Debug, Tree, yeng, Friends und Comms **nur in
der Hilfe ausgeblendet**, nicht aus dem Server entfernt. Im Expertenmodus
(`true`) werden diese Kategorien wieder angezeigt. Die Einstellung ist
keine Berechtigungsgrenze.

## Archiv-Freigaben zentral durch Robust

Vier einzeln freischaltbare Flags:

| Wert | Operation |
| --- | --- |
| 1 | OAR importieren |
| 2 | OAR exportieren |
| 4 | IAR importieren |
| 8 | IAR exportieren |

**Konfiguration für Robust (World API):**

```ini
[OGLFeatureControl]
Enabled = true
StorePath = "data/ogl-feature-control.json"
```

**Konfiguration je OpenSim-Simulator:**

```ini
[OGLFeatureControl]
Enabled = true

[NexVerseNodeAgent]
Enabled = true
NodeId = "mainland-01"
; weitere bestehende PeerUrl/SharedKey/NexBus-Konfiguration
```

Erzeuge einen P-256-ES256-Schlüssel, zum Beispiel mit OpenSSL:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out ogl-feature-ec.pem
openssl pkcs8 -topk8 -nocrypt -in ogl-feature-ec.pem -out ogl-feature-private.pem
openssl pkey -in ogl-feature-private.pem -pubout -out ogl-feature-public.pem
chmod 600 ogl-feature-private.pem
```

Der **private** PEM-Schlüssel wird ausschließlich auf Robust unter
`PrivateKeyPath` bereitgestellt (oder `OGL_FEATURE_PRIVATE_KEY_PEM`).
Simulator-Nodes erhalten **nur** den öffentlichen PEM-Schlüssel unter
`PublicKeyPath` (oder `OGL_FEATURE_PUBLIC_KEY_PEM`).
Der private Schlüssel darf niemals in den Simulator-Deployments oder
Git-Repositories liegen. Die NexBus-SharedKeys bleiben davon unabhängig. Lass die Funktion
bei noch nicht eingerichteten Grids auf `Enabled=false`.

**Ablauf:** Robust erkennt Simulatoren aus der NexBus-NodeRegistry,
speichert Freigaben atomar als JSON pro NodeID und publiziert signierte
`feature.policy.updated`-Ereignisse (ECDSA P-256 / SHA-256).
Simulatoren beziehen die Richtlinie automatisch beim Node-Start sowie
bei Heartbeats erneut, binden sie an ihre NodeID und akzeptieren nur
gültige Signaturen, monoton steigende Revisionsstände und höchstens
600 Sekunden weit in der Zukunft liegende Ablaufzeiten.

Die von Robust ausgestellten Richtlinien haben eine **120-Sekunden-TTL**.
Ein Widerruf wird über NexBus unmittelbar veröffentlicht. Ist Robust/
NexBus unerreichbar, sperren die Simulatoren beim TTL-Ablauf automatisch
alle vier Archivoperationen (fail-closed). Ein Netzwerkzustellfehler kann
die Sperre während der verbleibenden TTL verzögern; ein augenblicklicher
Widerruf ist ohne bestätigte Node-Quittierung **nicht garantiert**.

**Administrationsoberfläche:** World API `/api/v1/docs` →
`Simulator-Nodes` → `OGL Feature Control`. Geschützte Endpunkte:

- `GET /api/v1/feature-control/nodes` – für `simulators:read` und UserLevel ≥ 200
- `PATCH /api/v1/feature-control/nodes/{nodeId}` – für `simulators:manage` und UserLevel ≥ 200

PATCH-Beispiel:

```json
{"oar_import": false, "oar_export": true,
 "iar_import": false, "iar_export": true}
```

Optionale `license_key`-Kennungen dürfen 16–128 Zeichen haben.
Robust speichert davon ausschließlich einen SHA-256-Fingerabdruck.
Die eigentliche Durchsetzung erfolgt nicht durch diese sichtbare
Kennung, sondern durch die separat signierten kurzlebigen Node-Policies.
NodeIDs müssen eindeutig sein; die aktuelle NexBus-NodeID selbst ist
kein kryptografischer Besitznachweis.

**Durchsetzung im Server**:

- Legacy-`load/save oar` an der Hauptkonsole und im Regions-Archiver
- Alle öffentlichen OAR-`ArchiveRegion/DearchiveRegion`-Aufrufe
- Legacy-`load/save iar` und öffentliche IAR-`ArchiveInventory/DearchiveInventory`-Aufrufe
- Managed OAR-/IAR-OperationManager
- Robust World API `/api/v1/oar/*` und `/api/v1/iar/*` vor Job-Erstellung

Fehlende/ungültige Freigabe ergibt `OGL-LIC-403` und
`info@stadt-nexverse.de`. Unmodifizierte alte OpenSimulator-Versionen,
andere externe Importpfade und Betreiber mit vollständiger Kontrolle
über modifizierten Quellcode sind damit **nicht** zwangsweise geschützt.

## Archiv-Sicherheit bleibt zusätzlich nötig

Eine gültige Freigabe macht ein OAR oder IAR **nicht sicher**. Die
bestehenden OGL-Archivvalidatoren und Beschränkungen bleiben erforderlich.
Weitere Sicherung (Quoten, Isolation und Import-Audit) ist unabhängig vom
Lizenzstatus vor produktiver Aktivierung zu prüfen.

## Test / Freigabegates

```bash
dotnet run -c Release --project tools/ci/OglFeatureControlRegression/OglFeatureControlRegression.csproj
```

Testet 22 signierte Richtlinien-, Ablauf-, Widerrufs- und Hilfefälle;
ist im GitHub-Build eingebunden. Für die **produktive Abnahme** müssen
darüber hinaus Live-NexBus-Policy-Updates, Robust-Neustart, Offline-Fall,
IAR/OAR-Import/Export, Rollback und echte Rechteentziehung auf einem
isolierten Grid getestet werden.

Version 0.9.3.12 bleibt DEVELOPMENT bis zu den normalen RC/STABLE-Gates.
