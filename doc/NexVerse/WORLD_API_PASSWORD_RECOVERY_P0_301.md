# P0 #301 – World-API Passwortwiederherstellung

Stand: 25.10.2026. World-seitige Implementierung, **keine automatische Produktionsfreigabe**.
Issue: https://github.com/opengenesislink/opengenesislinkserver/issues/301

## Architektur

Der bestehende Endpunkt `POST /api/v1/users/{principalId}/password` bleibt
auf Self-Service und explizite Admin-Aktionen beschränkt.
Ein öffentlicher, nicht angemeldeter Portal-Besucher erhält aus dem neuen
World-API-Protokoll **niemals** ein Passwortmutationsrecht.

World/ROBUST bietet zwei getrennte Backend-Endpunkte:
- `POST /api/v1/auth/password-recovery/challenges` –
  ausschließlich mit `X-NexVerse-Api-Key` und Scope
  `password-recovery:issue`; JSON `{"email":"user@example.invalid"}`.
- `POST /api/v1/auth/password-recovery/complete` –
  ausschließlich mit `X-NexVerse-Api-Key` und Scope
  `password-recovery:complete`; JSON
  `{"token":"<world-challenge>","new_password":"Str0ngPasswordExample"}`.

Beide Endpunkte verlangen TLS oder eine explizit geschützte und
konfigurierte Proxy-/Backend-Verbindung. Die API-Key-Paarung ist
maschinenbezogen; Einwohner-Bearer und `users:register` können keine
Recovery-Challenge ausstellen oder verbrauchen.

Auf gültig angeforderter World-E-Mail-Adresse mit genau **einem**
lokalen, aktiven Konto antwortet die Ausgabe mit einem Token
und `delivery_email` **ausschließlich an das Backend**.
Nicht existierende / mehrdeutige Konten erhalten nur eine generische
Antwort ohne Token. Das Portal darf ausschließlich an `delivery_email`
versenden, niemals an eine unabhängig aus dem öffentlichen Request
übernommene Adresse. Bei maximal drei Ausgaben innerhalb einer Stunde
für denselben Account wird die Ausgabe gedrosselt.

Die World speichert **nur gesalzenen SHA-256-Tokenhash**, UUID-Bindung,
Ablaufzeit, Einmalstatus und Claim-ID in einer eigenen SQLite-Datenbank.
Token: 256-Bit CSPRNG; TTL: **900 Sekunden**, neue Ausgabe invalidiert
alte, Completion ist konkurrierungs- und wiederholungssicher. Vor der
Passwortmutation wird die Account-ID aus dem World-Token aufgelöst.
Nach erfolgreicher Mutation werden zustandsgebundene Einwohner-Tokens
und Refresh-Tokens ungültig. Ein Crash im unklaren Passwort-Write lässt
das Token zur Sicherheit beansprucht und fordert eine neue Mail an.

## Konfiguration im World-/ROBUST-Staging

```ini
[NexVerseWorldApi]
EnablePrivilegedEndpoints = true
PasswordRecoveryEnabled = true
PasswordRecoveryStorePath = "data/ogl-password-recovery.sqlite"
; bereits vorhandene TLS-Terminierung / vertrauenswürdige Proxy-Adressen:
PortalRegistrationTrustedProxyIPs = ""
PortalRegistrationTrustedHttpPeers = ""
```

Default: `PasswordRecoveryEnabled=false`. Das ist **kein** öffentlicher
Browser-Endpunkt. Der SQLite-Pfad muss nur für den World/ROBUST-Dienstbenutzer
les-/schreibbar sein; Datenbank, Journale und Snapshots vor Backup/Restore
bezüglich ihrer Rechte prüfen.

Im World Admin Center zwei **getrennt berechtigte** API-Keys für das Portal
erstellen bzw. einen dedizierten Portal-Servicekey mit **beiden** Recovery-Scopes.
Keine Zugriffsrechte `admin:*` oder `users:register` anheben.
Schlüssel nur in geschützter Portal-Environment-/Secret-Verwaltung speichern.
Für die erste Verifikation ausschließlich eine isolierte Staging-Installation
mit freigegebenem Testkonto verwenden.

## Portal-Migration / R495-Kompatibilität

Das Portal-Repository `marlin198323-glitch/nexverse` verwendet im
bisherigen Reset-Ablauf eigene ausgegebene Token aus seiner
Portal-Reset-Tabelle. **Diese sind nicht identisch mit World-Challenges.**
Sie dürfen nicht mittels `verified:true` oder einer unüberprüften
principal_id zur Änderung der World-Kontodaten verwendet werden.

Der Portal-Backend-Handler muss deshalb die Ausgabe einer World-Challenge
mit dem dedizierten World-Servicekey durchführen, den zurückgegebenen
World-Token ausschließlich über die verifizierte Zieladresse zustellen und
nach Formularabschluss genau diesen World-Token plus neues Passwort via TLS
an `/complete` weiterreichen. Alternativ braucht es einen dedizierten,
beiderseits signierten JTI-Migrationsvertrag mit Replay-Schutz.
Bis die Änderung im Portal gemergt und im Staging geprüft wurde, **World
Recovery nicht auf Produktion aktivieren**.

Die Portal-Diagnose R495 auf dem autorisierten Portal-Host ohne Tokens/Secrets
ausführen. Den Status `NEXVERSE_WORLD_API_KEY=SET` allein nicht als Beweis für
Recovery-Scope oder Erreichbarkeit behandeln; 401/403 getrennt testen.
Nicht die produktive Reset-Token-Tabelle als World-Tokenquelle verwenden.

## HTTP- und Betriebsvertrag

- `201`: World-Challenge an autorisiertes Backend ausgestellt,
  enthält den **nur backendseitig** weiterzugebenden Token.
- `202`: akzeptierte, aber nicht ausstellbare Anfrage (unbekannte,
  mehrdeutige oder gedrosselte Mail); keine Kontodaten.
- `200`: Passwortänderung durchgeführt und Token verbraucht.
- `401`: fehlender oder ungültiger Maschinenkey.
- `403`: Scope oder gesicherter Transport fehlt.
- `410`: Challenge fehlt, ist abgelaufen, rotiert oder bereits verbraucht.
- `400`: Passwort- oder JSON-Validierungsfehler.
- `429`: Backend-/Peer-Rate-Limit.
- `503`: deaktivierte Funktion oder unsicherer Backendzustand.

Fehler- und Auditlogs enthalten nur Fehlerkategorie/Korrelation-ID,
**niemals** Passwort, Klartexttoken, Mailadresse oder Auth-Header.

## Freigabekriterien

1. Eindeutige E-Mail-Kontozuordnung auf der tatsächlich benutzten
   MariaDB-/PostgreSQL-/SQLite-Konfiguration, auch Negativfälle.
2. Maschinenkey mit beiden Scopes akzeptiert; `users:register`,
   Einwohner-Bearer und anonymer Request abgelehnt.
3. Token-Rotation, TTL, Rate-Limit, konkurrierende Completion,
   DB-Neustart und Replay getestet.
4. Verifizierte Reset-Mail aus dem Portal, World/ROBUST-Passwortmutation,
   erfolgreicher **Firestorm**-Login mit neuem Passwort, alter
   Login und Replay abgewiesen, weitere Konten unverändert.
5. Gesicherte Backups, dediziertes Portal-/World-Deployment,
   Rollback und ausdrückliche Betreiberfreigabe dokumentiert.

**P1 separat:** Legacy-OpenSim-MD5-Verifikation durch versionsfähige
Argon2id-Migration mit bestehender Firestorm-/Viewer-Kompatibilität ersetzen.
Die P0-Recovery-Freigabe nicht von einer ungetesteten Hashmigration abhängig
machen. Es gibt hier keine Massenmigration produktiver Kennwörter.
