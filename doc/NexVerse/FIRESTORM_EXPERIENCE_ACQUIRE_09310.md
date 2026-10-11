# Firestorm Experience erwerben – OpenGenesisLINK 0.9.3.10 Dev

**Aktueller Stand: Implementierung im Quellcode; erst nach kontrolliertem
Developer-Grid-Test freigeben.** Für Firestorm Release x64 7.2.4.80712,
Region `OGL Developer Gen1`. Keine Änderung am Havok-NavMesh.

## Warum der Button bisher grau war

Firestorm deaktiviert im Reiter *Eigene* „Erlebnis erwerben“ zunächst.
Beim GET über `AgentExperiences` aktiviert der Viewer die Schaltfläche
erst, wenn die LLSD-Antwort das Feld `purchase` enthält. Beim Klick
sendet Firestorm einen **leeren POST** an genau dieselbe Capability.
Der bisherige Server unterstützte nur GET und lieferte kein
`purchase`: Deshalb reichte die Experience-INI nicht aus.

Die neue `FirestormManageCaps`-Option registriert:

| Viewer-CAP | Methode | Verhalten |
| --- | --- | --- |
| `AgentExperiences` | GET | `experience_ids` und nur bei freiem Kontingent `purchase: 0` |
| `AgentExperiences` | POST | Kostenfrei genau eine Experience für den aktuellen Avatar anlegen; aktualisierte IDs |
| `IsExperienceAdmin` | GET | `status` nur für selbst besessene Experience |
| `UpdateExperience` | POST | Nur eigener Name, Beschreibung, Altersfreigabe, Aktiviert-Status |
| `GetExperienceInfo` | GET | Profildaten des angelegten Erlebnisses, auch nach Relaunch |
| `ExperiencePreferences` | GET/PUT/DELETE | Separate Berechtigungs-CAPS für Allow/Block/Forget |

Erstellung ist zunächst **kostenfrei**, mit maximal zehn Experiences
je Bewohner. Kein Konto wird belastet und kein NV$-Wallet verändert.
Namen bei Neuanlage lauten `New Experience XXXXXXXX`; beim ersten
Anlegen soll das Firestorm-Profil automatisch zum Bearbeiten öffnen.
Ein `owner_id` aus Viewer-HTTP wird nicht angenommen; der Simulator
bindet die Operation an die aktive Root-Avatar-CAP-Lease. Robust nimmt
neue Inhalte nur über einen separaten, privilegiert angelegten
`experiences:viewer:manage`-Scope an. Das ist **kein** Admin-All-Key.

**Einschränkungen:** Gruppe übertragen, Logo-Uploads, SLURL,
Marketplace-Metadaten und PRIVATE-Property sind hier noch nicht
vollständig als Viewer-Editor-Operationen implementiert. Manipulierte
bzw. nicht unterstützte Properties werden abgelehnt; niemals stille
Rechteausweitung. Ohne echten Zweitavatar-Test bleibt das Release-Gate
offen.

## 1. Installation unterscheiden

Auf dem bisherigen Testsystem gibt es zwei Quellverzeichnisse:
`/opt/robust` (zentraler Dienst) und `/opt/developer` (Simulator).
**Nicht in Robust/bin/OpenSim.ini die Entwicklerregion umschalten.**
Die bisherige Konsolenausgabe des alten Preflight ohne
`[experiences]`-Meldungen zeigt, dass ein alter Git-Stand verwendet wird
oder das Script nicht aktualisiert wurde.

Bei Wartungsfenster und vorliegenden Backups **in beiden Deployments**
den freigegebenen neuen Git-Stand abrufen, korrekt bauen und ihre
separaten Dienste kontrolliert neu starten. Nicht automatisch allein
aufgrund dieser Dokumentation live deployen.

### Kontrollabfrage (nur lesend)

```bash
git -C /opt/robust rev-parse --short HEAD
git -C /opt/developer rev-parse --short HEAD
grep -n EXPERIENCE_VIEWER_MANAGE_CONFIGURED \
  /opt/developer/tools/diagnostics/ogl_voice_pathfinding_preflight.py
```

In der aktuellen Entwicklerversion muss die neue Markierung existieren.

## 2. Drei unterschiedliche Schlüssel

Im World-API-Schlüsselmanagement des Robust **administrativ** jeweils
mit minimalen Scopes anlegen oder bestehende gültige Schlüssel verwenden:

- `NEXVERSE_EXPERIENCES_API_KEY` → nur `experiences:script`
- `NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY` →
  nur `experiences:viewer:permissions`
- **Neu:** `NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY` →
  nur `experiences:viewer:manage`; privilegierte Ausgabe nur durch
  berechtigten Administrator.

Es müssen wirklich **drei verschiedene Werte** sein.
Im Simulator als getrennte Environment-Secrets hinterlegen.
Die Variablen sind in `bin/nexverse.env.example` und
`bin/config-include/GridCommon.ini` vorgemerkt.
Keinesfalls echte Token in Chat, Screenshots oder Git ausgeben.

## 3. Ausschließlich auf OGL Developer Gen1 aktivieren

In der **wirklich geladenen** `/opt/developer/bin/OpenSim.ini`,
nach einem gesicherten Testkonfigurations-Backup:

```ini
[NexExperiencesViewer]
    Enabled = true
    WorldApiBaseUrl = "https://world.stadt-nexverse.de"
    ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
    ViewerPermissionsApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY}"
    ViewerManageApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY}"
    FirestormReadCaps = true
    FirestormPermissionCaps = true
    FirestormManageCaps = true
    ScriptPendingConsent = true
    NativeExperiencePrompt = true
```

Nur einen Abschnitt `[NexExperiencesViewer]` behalten; bestehende
Zeilen ändern, **nicht** einen gleichnamigen zweiten Block ergänzen.
`/opt/robust/bin/Robust.HG.ini` enthält die zentrale
`[NexExperiences]`-Store-Konfiguration und muss nicht als
Simulator-Ini-Ersatz verwendet werden.

```bash
python3 /opt/developer/tools/diagnostics/ogl_voice_pathfinding_preflight.py \
  --sim-config /opt/developer/bin/OpenSim.ini \
  --robust-config /opt/robust/bin/Robust.HG.ini
```

`EXPERIENCE_VIEWER_MANAGE_CONFIGURED` sagt ausschließlich, dass die
Option gesetzt ist; weder Scopes noch World-API-Reichweite, tatsächliche
Viewer-CAPS oder Runtime-Secrets werden geprüft.
Nach kontrolliertem Simulator- und Robust-Neustart ist **neuer Login**
in Firestorm notwendig, da die CAPS bei Login ausgehandelt werden.

## 4. Tatsächliche Abnahme

1. Als Bewohner A anmelden. Erfahrungen → Eigene.
   `Erlebnis erwerben` muss aktiv sein.
2. Einmal klicken: genau eine neue, kostenfreie Experience.
   Der Viewer öffnet das Profil im Editiermodus. Titel und Beschreibung
   setzen, speichern, schließen und erneut öffnen.
3. Firestorm neustarten bzw. ab-/anmelden. Eigenes Erlebnis bleibt
   sichtbar und editierbar. Kein anderer Avatar kann dessen Profil
   ändern.
4. Zehn Experiences besitzen und elften Erwerb versuchen:
   keine unbeschränkte Erzeugung, keine NV$-Belastung.
5. Anschließend Test-LSL-Script der Experience zuweisen,
   zweitem Avatar B mit echten CAPS Allow/Block/Forget anbieten,
   Script-Reset, Timeout, Regionswechsel und Robust-Neustart prüfen.
6. Verbundene Pathfinding-Tests getrennt auf einer geeigneten Physikregion
   durchführen. Die Firestorm NavMesh-UI bleibt absichtlich grau.

**Bei Fehlern zuerst** World-API-Rückgabestatus, Simulator-CAPS-
Registrierungslogs, korrekt geladene Secrets/Scopes und die Versionen
beider laufenden Prozesse prüfen – keine Keys oder vollständigen
Bearer-CAP-URLs in Logs oder Chat weitergeben.

**Bis Abnahme:** 0.9.3.10 Dev, nicht RC1 oder Stable.
