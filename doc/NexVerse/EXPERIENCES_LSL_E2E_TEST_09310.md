# OpenGenesisLINK 0.9.3.10 – NexExperiences LSL-Livetest

**Testskript:** [`tools/diagnostics/ogl_experiences_e2e_test.lsl`](../../tools/diagnostics/ogl_experiences_e2e_test.lsl)

**Status:** Testwerkzeug, **kein** Nachweis einer erfolgreichen Live-Abnahme. Erst wenn die Tests mit echtem Firestorm, zwei Residents und einem zentralen Robust erfolgreich ausgefuehrt und dokumentiert sind, darf ein Fall als PASS gelten.

## Voraussetzungen

- Beide Simulator und Robust verwenden denselben freigegebenen 0.9.3.10-Commit.
- Isolierte Region `OGL Developer Gen1`; Firestorm-Viewer und **zwei separate Avatar-Konten**: A = Owner, B = normaler Bewohner ohne Admin-Rolle.
- Die Experience wurde von A erfolgreich erworben und der Script-Inventoryeintrag **explizit mit dieser Experience verknuepft**. Die Experience muss aktiviert sein. Nur eine gueltige Parcel-/Estate-Policy darf Requests erlauben.
- `[NexExperiencesViewer]` und alle erforderlichen Firestorm-CAPS/Consent-Optionen sowie die **drei verschiedenen** serverseitigen Schluessel (`experiences:script`, `experiences:viewer:permissions`, `experiences:viewer:manage`) sind korrekt eingerichtet. Geheimwerte duerfen nicht in Script, Chat, Git oder Screenshots auftauchen.
- Ein einzelnes neu gerezztes Test-Prim unter Kontrolle von A, mit genau einer Kopie des Skripts. Script an die Experience binden, erst danach neu starten. `llGetExperienceDetails(NULL_KEY)` muss sechs Werte liefern und eine aktive Experience anzeigen.

## Durchfuehrung

1. LSL-Datei aus GitHub in eine neue Script-Notecard/LSL-Datei uebernehmen und in das Test-Prim laden. In Firestorm **als echtes LSL-Skript** kompilieren und starten; zuerst im Owner-Chat `INFO` / Experience-ID kontrollieren.
2. **EXP01 / EXP02**: Avatar B beruehrt das Prim. B darf nicht automatisch berechtigt werden; ausdruecklich `Allow` im Viewer waehlen. `experience_permissions` und danach `llAgentInExperience(B) == 1` muessen gemeldet werden.
3. **EXP03**: B waehlt `Block` bzw. `Deny`; falls vorher erlaubt, erst die Berechtigung in den Experience-Einstellungen aendern, **dann erneut** beruehren. Bei einer abgewiesenen Anfrage muss `experience_permissions_denied` mit passendem `XP_ERROR` erscheinen; kein aktives Recht bleibt bestehen.
4. **EXP04**: B setzt `Forget` in den Firestorm Experience-Einstellungen, loggt aus und wieder ein, beruehrt das Prim erneut. Ohne neues ausdrueckliches Allow darf kein positives Permission-Event entstehen.
5. **EXP05 / EXP06**: B stellt einen offenen Consent-Dialog her; vor der Antwort Script resetten bzw. B ausloggen/teleportieren. Keine veraltete `experience_permissions`-Bestaetigung darf spaeter ausgeliefert werden. Simulator- und Viewer-Logs mitpruefen.
6. **EXP07 / EXP08**: Parallele Anfragen mit B und einem weiteren Nicht-Owner / einem zweiten Test-Prim, danach Tests auf gesperrten Parzellen, als HG-Gast und mit deaktivierter Experience. Erfolg eines Residents darf kein Grant fuer andere erzeugen.
7. **LSL/KV:** A beruehrt das Prim und oeffnet das Owner-Menue. Folgende Reihenfolge mit protokollierten `dataserver`-Antworten ausfuehren:
   - `KV Anlegen` → `1,ogl09310-test-v1`
   - `KV Lesen` → `1,ogl09310-test-v1`
   - `KV Konflikt` → `0,15` (`XP_ERROR_RETRY_UPDATE`)
   - `KV CAS` → `1,ogl09310-test-v2`
   - `KV Lesen` → `1,ogl09310-test-v2`
   - `KV Groesse`, `KV Anzahl`, `KV Liste` → erfolgreiche Statistiken und Listen
   - `KV Fehlt` → `0,14` (`XP_ERROR_KEY_NOT_FOUND`)
8. **Persistenz / EXP09:** **Vor** dem Loeschen Script resetten und danach Robust und Simulator kontrolliert neu starten; A waehlt `KV Lesen`. Derselbe `v2`-Wert muss erhalten bleiben. Solange der **gleiche** Prim mit seiner Objekt-UUID besteht, wird derselbe isolierte K/V-Testschluessel verwendet.
9. **Aufraeumen:** Nach dokumentiertem Persistenztest `KV Loeschen` → `Ja Loeschen`; anschliessend `KV Fehlt` und `KV Lesen` pruefen. Nur der objektbezogene Testschluessel wird entfernt. Niemals den zentralen Experience-Store loeschen.

## Menu-Funktionen

- Owner-Beruehrung → Dialog mit `Info`, `Anfrage`, `KV Anlegen`, `KV Lesen`, `KV CAS`, `KV Konflikt`, `KV Groesse`, `KV Anzahl`, `KV Liste`, `KV Fehlt`, `KV Loeschen`.
- Nicht-Owner-Beruehrung → Consent-Anfrage nur fuer diesen Avatar; keine automatisch erteilte Zustimmung.
- `dataserver` korreliert asynchrone Request-IDs mit dem Testnamen. Die Ausgabe unterscheidet PASS, FAIL und erwartete Negativfaelle. Das Owner-Menue reagiert nur auf den Objekt-Eigentuemer und beendet seinen Listen-Handler automatisch.
- `KV Anlegen` kann bei einem bereits vorhandenen Schluessel erwartungsgemaess scheitern. Fuer den Persistenztest dann **nicht** neu anlegen, sondern `KV Lesen` benutzen; `KV CAS` erwartet explizit den `v1`-Altwert und darf nach einem erfolgreichen Update nicht mehrfach unkontrolliert wiederholt werden.
- Die `KV Liste` ist auf 100 Schluessel beschraenkt. Bei Experiences mit mehr Daten kann der Testschluessel deshalb ausserhalb der ersten Seite liegen; keine umfassende Enumeration behaupten.

## Abnahme / Grenzen

Fuer jeden getesteten Fall Datum, Simulator-Commit, Firestorm-Version, Avatar-Rollen, Region, anonymisierte Chat- und Serverausgaben sowie beobachtetes Verhalten dokumentieren. Keine Secrets oder vollstaendigen CAP-URLs protokollieren.

Die GitHub-CI kann die **vorhandenen LSL-API-Signaturen und Script-Marker** statisch pruefen. Ein echter Inworld-LSL-Compilerlauf, Firestorm-Consent-Dialoge, mehrere Simulatoren, Restart/Backup und Rechteisolation werden dadurch **nicht** automatisch nachgewiesen. Alle noch nicht ausgefuehrten Operator-Tests bleiben `NOT RUN`.

Siehe [RC1 Operator Acceptance](RC1_OPERATOR_ACCEPTANCE_09310.md) und [Firestorm Experience Setup](FIRESTORM_EXPERIENCE_ACQUIRE_09310.md).
