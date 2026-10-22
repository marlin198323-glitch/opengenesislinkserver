# ROBUST: Fehlende lokale NV$-Konten und NVBAN-Kennungen nachtragen

## Anlass

Bei nachträglicher Aktivierung des NV$-Systems besitzen ältere,
bereits registrierte Avatare unter Umständen noch keinen Eintrag in
`ogl_ledger_accounts` oder `ogl_ledger_virtual_accounts`.

Beispiel aus einer SQL-Sicherung vom 22.10.2026: 11 Einträge in
`UserAccounts`, 10 passende `ogl_ledger_accounts` und kein
`ogl_ledger_virtual_accounts`-Eintrag mit einer dieser 11
Benutzer-UUIDs. Die zwei existierenden NVBAN-Einträge dürfen
**nicht** verändert werden. Die Zahlen beschreiben nur diesen
Datenbankexport, nicht den Live-Zustand nach einem späteren Update.

## Kommandos

Im laufenden R.O.B.U.S.T. (NexEconomy aktiviert):

```text
R.O.B.U.S.T.# nv sync accounts
```

Prüft alle lokalen, nicht geschützten Avatare und zeigt die Anzahl
bereits vollständiger Wallets, fehlender Ledger-Konten und fehlender
NVBAN-Zuordnungen. **Keine Daten werden geschrieben.**

Nach Datenbank-Backup:

```text
R.O.B.U.S.T.# nv sync accounts --apply
Zum Anlegen fehlender Ledger- und NVBAN-Konten genau 'SYNC NVBAN' eingeben: SYNC NVBAN
```

Der bestätigte Aufruf legt fehlende NV$-Resident-Konten über
`NexEconomyService.EnsureResidentAccount` und fehlende
NVBAN-Zuordnungen über `EnsureVirtualBankAccount` an.
Das entspricht dem Weg, den bereits die laufende NV$-API nutzt.

## Eigenschaften

- Nur **lokale** Avatare aus `UserAccounts` mit gültiger UUID,
  `LocalToGrid` und `UserLevel < 200`; keine Hypergrid-Fremdidentitäten
  oder geschützten Servicekonten.
- Für existierende Wallets und NVBAN wird nichts geändert. Insbesondere
  bleiben Kennung, aktuelles Guthaben, Konto-Status und Buchungen erhalten.
- Für fehlende Wallets wird ein Bewohnerkonto ohne Startgutschrift angelegt.
  Eine fehlende NVBAN kann auch für ein bereits bestehendes Ledger-Konto
  separat nachgetragen werden.
- Die Kennung folgt dem vorhandenen, zufällig aussehenden Muster
  `NVBAN-XXXX-XXXX-XXXX-XXXX-XXXX`. Im Quellcode wird sie **bewusst
  deterministisch** aus der Avatar-UUID erzeugt. Sie ist stabil und
  vermeidet, dass Wiederholungsläufe andere NVBAN für denselben Account
  erfinden. Bestehende Kennungen bleiben erhalten.
- Bei Kollision mit einem andersartigen Ledger-Konto wird der Eintrag
  als Konflikt ausgegeben und nicht überschrieben.
- Fehler eines einzelnen Accounts werden ausgewiesen, weitere Avatare
  werden weiter geprüft. Der Befehl ist gefahrlos wiederholbar;
  Ledger- und virtuelle Kontoanlage sind jeweils idempotent.
- Das Tool führt **keine** direkte SQL-Einfügung, keine Geldbuchung und
  keine Änderung an `ogl_ledger_postings` oder Transaktionen aus.

## Betrieb und Prüfung

Vor `--apply` die Datenbank sichern und im Wartungsfenster starten.
Nach dem Lauf `nv sync accounts` erneut ausführen und anhand von
`nv balance "Vorname Nachname"` sowie der World API
`GET /api/v1/economy/virtual-account` stichprobenartig prüfen.
Eine Datenbank kann sich zwischen Preview und Apply verändert haben;
die eigentliche Kontoanlage prüft die Eindeutigkeit erneut.

**Hinweis zu `GetUserAccountsWhere`:** Der Konsolenbefehl verwendet
das nur intern fest definierte Suchkriterium `1=1`. Es wird kein
SQL-Ausdruck aus einer Konsoleneingabe übernommen. Die tatsächliche
Lokalität wird an der geladenen Benutzeridentität geprüft.
