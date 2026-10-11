# R287: Portal-World-API – tatsächliche Umsetzung und Abnahme

**Release-Zweig:** 0.9.3.10 Dev · **Quelle:** WORLD_API_FEHLENDE_ENDPUNKTE_R287.md
· keine Stable-Freigabe. Das R287-Dokument beschreibt Vorschläge, nicht
automatisch 16 bestehende Routen oder eine bereits erfolgte Betriebsabnahme.
Die dortigen Zählungen 135/182/139 sind Momentaufnahmen des Go-Portals,
nicht der aktuelle API-Gesamtvertrag.

## Implementiert (dieser Entwicklungs-Pull-Request)

| R287-Punkt | World API | Sicherheit |
| --- | --- | --- |
| P0: Avatarbild | GET `/api/v1/profiles/{principalId}/image` | Bild nur aus demselben Profil; öffentlich nur bei `PublishProfile`, sonst ProfileRead und Avatar-Owner/Admin. Vorläufig nur vom Avatar selbst erstellte Assettexturen; fremde Creator ohne nachgewiesene Assetrechte nicht ausliefern |
| P1: Event-Details | GET `/api/v1/events/{eventId}` | Öffentlich nur `Approved`, fremde Draft-UUID ergibt 404 |
| P1: Event-Korrektur | PATCH/DELETE `/api/v1/events/{eventId}` | `discovery:manage` plus Owner oder `admin:*`, Audit |
| P1: Classified-Details | GET `/api/v1/classifieds/{classifiedId}` | Öffentlich nur `Approved` |
| P1: Classified-Korrektur | PATCH/DELETE `/api/v1/classifieds/{classifiedId}` | `discovery:manage` plus Owner oder `admin:*`, Audit |
| P2: Eigene Gruppen | GET `/api/v1/groups/mine` | Authentifizierter Einwohner, `groups:read`, keine `agent_id`-Fremdabfrage, begrenzte Pagination |

Die bestehenden Event-/Classified-POST-Upsert-Routen sind zusätzlich gegen
Fremd-Owner, Eintragsübernahme und Status-Eskalation geschützt: normale
Einwohner erzeugen eigene Entwürfe; eine beliebig mitgesendete UUID,
`organizer_id`/`owner_id` oder `state=approved` ist keine Autorisierung.
Administrative Freigaben benötigen ausdrücklich `admin:*`.

PATCH ist absichtlich eng begrenzt: Name, Beschreibung, Bild, Tags, Eventzeiten
und Veranstaltungs-Eintrittspreis bzw. Classified-Kategorie. Änderungen an
Owner, Place-ID, UUID, Veröffentlichungsstatus und Moderationsdaten werden
nicht stillschweigend angenommen. Existierende POST-Upsert-Routen erzwingen ebenfalls Owner-/Statusschutz.

### Avatar-Bild-Bytes und Einschränkungen

Profilbilder im OpenSim-Assetstore sind häufig JPEG2000 (J2K), nicht PNG/JPEG.
Der neue Profil-Endpunkt liest die Referenz ausschließlich aus den aktuellen
`IProfilesData`-Daten, lädt die Bytes aus dem vorhandenen `IAssetService`,
prüft Typ/Creator und Größe, transkodiert J2K bei vorhandener OpenJPEG-
Runtime in PNG, setzt `X-Content-Type-Options: nosniff` sowie
`Cache-Control: private, no-store`. Bei fehlendem Codec/Assetservice
werden Fehler statt einer fremden Textur ausgeliefert.

Die Überprüfung der Creator-ID ist strenger als übliche OpenSim-Assetnutzung:
Ein von einem Fotografen hochgeladenes Porträt bleibt **vorerst** gesperrt,
bis ein expliziter Asset-Lizenz-/Public-Berechtigungsbeweis implementiert ist.
Dies ist ein bewusster Datenschutzvorrang, kein Beleg vollständiger Viewer-
Asset-Permissions-Parität. Ein Upload oder ein generischer Asset-Browser
gehört nicht zu diesem Endpunkt.

Profile-JSON enthält weiter `profile_image` als UUID und neu optional
`profile_image_url` als *relativen* Pfad. Die URL garantiert weder eine
Bilddatei noch Berechtigung. Ein privates Profil benötigt weiterhin Auth.
Keine API-Keys in Bild-URLs und keine fremden Texture-UUIDs ausprobieren.

## Noch NICHT als fertig freigegeben

- Notifications: keine zentrale Inbox/Producer-, Persistenz- und Owner-
  Read-Architektur in der geprüften World-API; nicht als leeres Fake-API
  bereitstellen.
- Allgemeines GET `/assets/{assetId}/metadata`: würde Asset-/Inventory-
  Eigentumsgrenzen betreffen; nicht ohne Autorisierung via Assetdienst öffnen.
- Einzelregion `GET /regions/{regionId}` und persönliche Bank-Account-
  Owner-Auflösung: sichere Provider und Berechtigungsmodelle prüfen.
- Banking/Commerce/Land-Commerce: die zwölf bisher komplett leeren
  Banking-/Payment-/Commerce-/Land-Response-Schemas sind anhand der
  C#-Payloads jetzt konkret mit Ledger-Postings, Kontoauszügen, Salden,
  Payment-Requests, Orders, Listings und Leases typisiert. Zehn
  wiederverwendbare Record-Schemas wurden ergänzt. Bei Sammel-Operationen
  bleiben weitere Pagination-/Owner-Erweiterungen gesondert zu prüfen.
- Die vier Legacy-Sammel-Schemas für Discovery und Chapter11 deklarieren
  nun ihre gemeinsamen möglichen Felder; wegen polymorpher Antworttypen
  sind sie ausdrücklich keine vollständigen per-Route-DTOs.
- Optionales RSVP und Payment-/Credits-POST: **keine API ohne ausdrückliche
  Produkt-/Zahlungs- und Ledger-Idempotenzentscheidung**.
- Die Live-Abnahme, JPEG2000-Runtime-Konvertierung und echte private
  Zweitavatar-Tests sind auf dem Betreibergrid noch nicht erbracht.

## Operator-Akzeptanz

1. Backup von Robust, Profile/Assetservice und `NexDiscoveryStore`; Pull,
   Build und geordneten Start zuerst auf isolierter Testinstanz.
2. Viewer mit öffentlichem Besitzerprofil, privatem Fremdprofil, eigenem
   privaten Profil und fremder Asset-Creator-ID testen. Verifizieren:
   öffentlich erlaubtes Bild → PNG/JPEG-Bytes; private UUID → 404/401;
   Besitzer-Token → richtige Bytes; falsche UUID → 404.
3. Mindestens eine echte J2K-Avatartextur mit Linux-Laufzeit prüfen;
   keine Annahme, dass jedes libgdiplus/OpenJPEG-System gleich ist.
4. Event und Classified: öffentlich freigegeben GET 200; Entwurf GET 404;
   fremder PATCH/DELETE 403; Owner-Änderung/Audit; nur erlaubte Felder.
5. Gruppenausgabe: eigenes `groups/mine`; Service-Principal, fremde
   Avatar-UUID und abgelaufener Einwohner-Token dürfen nicht fremde
   Gruppendaten offenlegen.
6. `GET /api/v1/openapi.json`, alle drei ChatGPT-MD-Downloads und
   `tools/ci/verify_r287_portal_api.py` gegen denselben Commit prüfen.

**Keine produktiven NV$-Transfers, administrativen Schlüsselrotationen oder
Echtgeld-Transaktionen für diese Tests auslösen.**
