# ROBUST: Avatar sicher entfernen (delete user)

Stand: 21.10.2026. Gilt fuer den lokalen OpenGenesisLINK-Benutzerkontendienst
auf dem zentralen ROBUST.

## Konsole

```text
R.O.B.U.S.T.# show account Vorname Nachname
R.O.B.U.S.T.# delete user Vorname Nachname <UUID>
R.O.B.U.S.T.# delete user Vorname Nachname <UUID> --confirm
```

Der Befehl **ohne `--confirm` ist eine reine Vorschau**: Name und
UUID muessen genau zusammenpassen. Bei `--confirm` verlangt ROBUST
zusaetzlich die vollstaendige Zeichenfolge `DELETE <UUID>`.
Bei einer fehlerhaften Eingabe wird nichts entfernt.

## Voraussetzungen und Schutz

- Vor einer dauerhaften Loeschung eine getestete Sicherung der ROBUST-DB
  und eine kontrollierte Betriebspause vorsehen. Aktive Avatar-Sessions
  und aktuelle `Presence`-Eintraege blockieren die Loeschung.
- Loeschung des System-/GRID-SERVICES-Kontos und von UserLevel >= 200
  wird verweigert. Fremde Hypergrid-Identitaeten werden nicht entfernt.
- Alle lokalen Kernspeicher muessen denselben Provider und dieselbe
  Verbindungszeichenfolge verwenden. Bei getrennten Datenbanken,
  unueblichen Tabellen-Realms oder fehlenden Tabellen verweigert
  ROBUST die automatische Loeschung.
- Provider: MariaDB/MySQL, PostgreSQL und SQLite. Der Befehl benutzt
  parameterisierte SQL-Anweisungen mit einer Transaktion.
- Der Vorgang entfernt **nur** zentrale Avatar-Daten:
  `tokens`, `auth`, `inventoryitems`, `inventoryfolders`,
  `Avatars`, `GridUser`, `Presence`, die eigenen und
  gegenseitigen `Friends`-Eintraege sowie `UserAccounts`.
  Fremde Benutzerkonten mit derselben E-Mail-Adresse bleiben bestehen.

## Wichtige Grenze: kein vollstaendiger Grid-Cascade-Delete

Dieser Befehl loescht **noch nicht** automatisch von externen Diensten
und Addons verwaltete Daten: Gruppen, NV$-Guthaben und Buchungen, World API
oder Stadtportal, UserProfiles, IM-Archive, Experiences, Marketplace,
Regionsinventar, Land/Parzellen, gerezzte Objekte oder Hypergrid-Caches.
Insbesondere Asset-Dateien duerfen nicht blind geloescht werden, da
sie von anderen Avataren weiterverwendet werden koennen.

**Vor dem irreversiblen Schritt** Eigentuemerwechsel oder manuelle
Bereinigung dieser Komponenten einplanen. Der Dienst meldet nur
die Loeschung der zentralen Avataridentitaet als erfolgreich, nicht
eine vollstaendige End-to-End-Datenbereinigung. Fuer eine
datenschutzrechtlich vollstaendige Loeschung sind die unabhaengigen
Systeme getrennt zu pruefen und zu bereinigen.

Die benutzten Zugangsdaten der Datenbank bleiben in den jeweiligen
INI-/Environment-Einstellungen; keine SQL-Passwoerter in Logdateien
oder im Loeschbefehl angeben.
