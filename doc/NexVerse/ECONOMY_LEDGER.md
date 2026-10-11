# NV$ Ledger-Grundlage

OpenGenesisLINK baut die NexVerse-Wirtschaft ab 0.9.3.6 auf einem unveränderlichen Double-Entry-Ledger auf. Diese Grundlage ist bewusst von Viewer-Protokollen, Web-APIs und dem historischen `IMoneyModule` getrennt.

## Währung

- Anzeigename/Symbol: `NV$`
- interner Code: `NVD`
- initiale Untereinheiten: `0`

NV$ ist ausschließlich virtuelle NexVerse-Währung. Der interne Code `NVD` ist kein ISO-4217-Versprechen und darf nicht als reale Fiat-Währung oder echtes Bankguthaben dargestellt werden.

## Double-Entry-Invariante

Jede Buchung besteht aus mindestens zwei Postings auf mindestens zwei Ledger-Konten. Alle Beträge sind positive Ganzzahlen. Die Summe der Debit-Postings muss exakt der Summe der Credit-Postings entsprechen.

Transaktionen und Postings sind nach dem Anhängen unveränderlich. Korrekturen werden später ausschließlich durch neue Gegen-/Korrekturbuchungen vorgenommen; vorhandene Buchungen werden nicht überschrieben.

## Kontoklassen

Der Kern kennt folgende Klassen:

- Resident;
- Group;
- Business;
- Estate;
- ObjectMerchantEndpoint;
- System;
- Escrow.

Jedes Konto besitzt zusätzlich eine explizite Normal-Seite (`Debit` oder `Credit`). Dadurch muss der Ledger keine fehlerhafte Annahme treffen, dass jede System- oder Escrow-Art dieselbe Bilanzlogik hat.

Ein positiver von `GetBalance()` gelieferter Saldo bedeutet: Saldo auf der für dieses konkrete Konto definierten Normal-Seite.

## Idempotenz und Konflikte

Die `TransactionId` ist die unveränderliche Buchungsidentität.

- dieselbe ID mit exakt demselben Inhalt: `Duplicate`, keine zweite Buchung;
- dieselbe ID mit abweichendem Inhalt: Konflikt;
- eine bereits global verwendete `PostingId`: Konflikt.

Diese Regeln sind erforderlich, damit API-, Viewer- oder NexBus-Retries später keine Doppelbuchungen erzeugen.

## Speichergrenze

`INexLedgerStore` ist die Persistenzgrenze. `InMemoryNexLedgerStore` bleibt ausschließlich Referenzimplementierung und Regressionstest-Backend.

Mit `NexLedgerSqlStore` existiert jetzt zusätzlich ein dauerhafter, providerneutraler ADO.NET-Store. Er verwendet ein versioniertes Schema mit getrennten Tabellen für Konten, Transaktionsköpfe und Postings. Das Journal bleibt append-only; eine mutable Balance-Spalte wird absichtlich nicht gespeichert. `GetBalance()` berechnet den Saldo aus den unveränderlichen Postings und der Normal-Seite des Kontos.

Die SQL-Buchung läuft in einer seriellen Datenbanktransaktion. Transaktions-ID, Posting-IDs und Kontoreferenzen werden durch Unique-/Primary-Key-Regeln zusätzlich auf Datenbankebene abgesichert. SQLite aktiviert pro Verbindung Foreign Keys. Der CI-Test legt eine reale SQLite-Datei an, migriert Schema v1, bucht NV$, öffnet den Store erneut und prüft Salden, Historie, Idempotenz und abgewiesene Buchungen.

Die Runtime-Treiber MySQL/MariaDB, PostgreSQL und SQLite werden nicht in den Ledger-Kern eingebaut. Der Store erhält eine `Func<DbConnection>`; dadurch bleibt der Kern providerneutral. `NexLedgerSqlRuntime` löst die vorhandenen OpenSim-`StorageProvider`-Bezeichnungen auf die bereits mitgelieferten ADO.NET-Treiber auf und erzeugt die Verbindung ausschließlich per Reflection. Der SQLite-CI-Test benutzt genau diesen Resolver.

`NexEconomyConnector` bindet diesen Store jetzt als eigenen Robust-Service ein. `[NexEconomy]` ist standardmäßig deaktiviert. Bei Aktivierung erben leere `StorageProvider`-/`ConnectionString`-Werte die autoritative `[DatabaseService]`-Konfiguration; ein expliziter Override ist nur für eine bewusst getrennte Ledger-Datenbank vorgesehen. Null-Storage wird für NV$ abgelehnt. Der Connector registriert keine HTTP-Routen und keine Viewer-Geldschnittstelle. Ein eigener Robust-CI-Smoke aktiviert den Connector mit temporärem SQLite, prüft Schema v2 einschließlich Kontostatus-/Eventtabellen und bestätigt ein leeres Journal.

## Kontolebenszyklus und Policy

Kontostatus wird bewusst außerhalb des unveränderlichen Buchungsjournals geführt. Schema v2 ergänzt `ogl_ledger_account_state` und die append-only Historie `ogl_ledger_account_events`.

Status:

- `Active`: normale Transfers erlaubt;
- `Locked`: normale Transfers gesperrt, administrative Korrektur/Reversal bleibt möglich;
- `Closed`: terminal; Wiedereröffnung ist nicht erlaubt und Schließung erfordert Saldo 0.

`NexEconomyService` ist die verbindliche Policy-Grenze für produktive Geldbewegungen. Der Service verhindert Überziehungen von Credit-normalen Wallets, erzwingt aktive Transferkonten, verlangt bei administrativen Korrekturen ein System-Gegenkonto und erzeugt Reversals ausschließlich als neue, deterministisch idempotente Gegenbuchungen. Vorhandene Journalzeilen werden niemals geändert oder gelöscht.

Die Live-CI-Matrix startet MariaDB und PostgreSQL und prüft neben normalen Commits sowohl explizites Rollback als auch den Verlust einer offenen Verbindung ohne Commit. In beiden Fällen darf kein unvollständiger Transaktionskopf bestehen bleiben.

## World API und Viewer-Adapter

Die privilegierte World API stellt die kontrollierten NV$-Operationen unter `/api/v1/economy` bereit. Balance und Transaktionslesezugriffe verwenden `economy:read`; normale Transfers verwenden `economy:transfer`. Jeder Transfer verlangt einen `Idempotency-Key`, der innerhalb des authentifizierten Principals deterministisch auf eine Ledger-Transaktions-ID abgebildet wird. Kontostatusänderungen und Reversals bleiben `admin:*`.

Der API-Handler besitzt bewusst keinen Zugriff auf `INexLedgerStore.Append`, SQL-Verbindungen oder administrative Geldschöpfung. Er arbeitet ausschließlich über `NexEconomyService`.

Der Simulator verwendet `NexVerseMoneyModule` als `IMoneyModule`-Adapter. Das Modul öffnet keine Ledger-Datenbank. Balance- und Transferoperationen werden über die zentrale World API mit einem eingeschränkten Maschinen-API-Schlüssel ausgeführt. Die Konfiguration `[NexEconomyViewer]` ist standardmäßig deaktiviert; der Schlüssel wird über `NEXVERSE_ECONOMY_API_KEY` aus der Laufzeitumgebung bezogen.

Damit ist Roadmap 10.1 als Ledger-/Policy-/Adapter-Grundlage abgeschlossen. Die folgenden Roadmap-Blöcke ergänzen darauf aufbauend virtuelle Kontonummern, Bankfunktionen, Commerce, Land- und Objektzahlungen.

## Robust-Konsolentest

Der autoritative Robust-Prozess stellt fuer Administratoren zwei lokale Diagnosebefehle bereit. Es wird bewusst kein oeffentlicher Geldschoepfungs-Endpunkt angeboten.

`nv give "<Avatarname|UUID>" <Betrag> [Grund]` erzeugt eine administrative Double-Entry-Gutschrift ueber `NexEconomyService.AdministrativeAdjustment()`. Das feste System-Gegenkonto wird ueber die Policy-Schicht angelegt; die Buchung bleibt mit Actor, Grund, Transaktions-ID und unveraenderlichen Postings nachvollziehbar. Nach Erfolg zeigt die Konsole den neuen Kontostand und versucht zusaetzlich, dem Avatar eine System-IM von `NexVerse Bank` zuzustellen.

Beispiele:

`nv give "Sleimer Akina" 100 Testgutschrift`

`nv balance "Sleimer Akina"`

Die Funktion ist nur fuer Administration und Tests vorgesehen. Normale Nutzerzahlungen laufen weiterhin ueber die authentifizierten Economy-/Commerce-Pfade.

## Nächste Schichten

1. 10.2 virtuelle NexVerse-Kontonummern/IBAN-ähnliche Kennungen;
2. 10.3 erweiterte Bankfunktionen, Statements, Requests, Limits, Gebühren und Reconciliation;
3. 10.4 vollständige Viewer-Commerce-Flows;
4. 10.5+ Land-, Objekt-, Marketplace- und weitere Wirtschaftsabläufe.


## Virtuelle NVBAN-Konten (Roadmap 10.2)

OpenGenesisLINK verwendet fuer virtuelle Kontokennungen das eigene Schema `NVBAN`. Eine Kennung sieht beispielsweise wie `NVBAN-ABCD-EFGH-JKLM-NPQR-STUV` aus. Der Praefix ist absichtlich kein ISO-Laendercode und die Kennung ist keine IBAN. Sie darf weder gegenueber Nutzern noch gegenueber externen Systemen als reales Bankkonto dargestellt werden.

Die Kennung wird deterministisch aus der unveraenderlichen Ledger-Konto-UUID abgeleitet und anschliessend dauerhaft 1:1 in `ogl_ledger_virtual_accounts` gespeichert. Ledger-Schema v3 erzwingt Eindeutigkeit sowohl fuer die Konto-UUID als auch fuer die virtuelle Kennung. Die Kennung ist ein Alias fuer ein bestehendes NV$-Wallet; sie erzeugt kein zweites Saldo- oder Buchungssystem.

`NexEconomyService.EnsureVirtualBankAccount()` bleibt die Policy-Grenze. World API und spaetere Banking-Funktionen greifen nicht direkt auf die SQL-Tabelle zu. `GET /api/v1/economy/virtual-account` liefert die eigene bzw. autorisiert ausgewaehlte NVBAN-Kennung mit `economy:read` und enthaelt einen ausdruecklichen Hinweis, dass es sich nicht um ein reales Bankkonto oder eine IBAN handelt.


## Banking, NexCommerce und Landhandel (Roadmap 10.3–10.6)

Ledger-Schema v4 ergaenzt ausschliesslich Workflow-Zustaende neben dem unveraenderlichen Journal: Transfer-Policies, Zahlungsanforderungen, Commerce-Auftraege, Landangebote und Mietvertraege. Die Tabellen `ogl_ledger_transactions` und `ogl_ledger_postings` bleiben append-only.

`BankingTransfer` erzwingt Kontostatus, Deckung, optionales Transaktionslimit, Tageslimit und eine optionale feste Gebuehr. Statements und Reconciliation werden aus den unveraenderlichen Buchungen abgeleitet. Refunds erzeugen Gegenbuchungen; es gibt weiterhin keinen produktiven Pfad, der historische Journalzeilen editiert.

NexCommerce vereinheitlicht Vendor-Zahlungen, Objektverkaeufe, Marketplace, Landkauf, Eventtickets, Mieten und Merchant-Refunds ueber persistente Orders, die auf ihre Ledger-Transaktionen zeigen. Der Viewer und Simulator behalten keine eigene Geld-Datenbank.

Der Landhandel trennt Katalog und Weltzustand: Listings/Leases sind OpenGenesisLINK-Workflowdaten; die eigentlichen Parcel-Rechte bleiben im nativen LandManagementModule. Beim Viewer-Landkauf wird der Eigentumswechsel erst nach erfolgreichem NV$-Settlement ausgefuehrt.

## Hypergrid-Bewohner als NV$-Teilnehmer (11.10.2026, opt-in)

**Ziel:** Besucher aus anderen OpenSimulator-Hypergrids duerfen im
NexVerse-Grid dieselbe **virtuelle** NV$-Wirtschaft verwenden, ohne
dass Guthaben oder Rechte eines lokalen Avatars vermischt werden.

- **Identitaet:** Die Region liest `HomeURI`, urspruengliche Avatar-UUID
  und Session-ID ausschliesslich aus der serverseitig authentifizierten
  Hypergrid-AgentCircuitData. Die Herkunft wird bei jeder lebenden
  Gast-Session prueft. Ein Name, Objekttext oder Viewer-POST gilt
  **niemals** als Identitaetsnachweis.
- **Wallet:** `NexHypergridWalletIdentity` erzeugt eine stabile,
  gehaertete Account-UUID aus kanonischem Home-Grid-Origin und
  Original-Avatar-UUID. Anderes Grid, gleiche Avatar-UUID → **anderes NV$-Konto**.
  Der Ledger speichert das Konto als Resident-Wallet mit eindeutigem
  `hg:`-Referenzschluessel und dem ueblichen Double-Entry-Journal.
- **Robust:** `POST /api/v1/economy/hypergrid/wallets/ensure`
  ist ausschliesslich mit einem vertrauenswuerdigen Maschinen-Key
  (`economy:transfer`, kein Avatar-Bearer) aufrufbar. Die Ableitung
  erfolgt dort *erneut*. Wallet-Neuanlage ist idempotent und vergibt
  **0 NV$**; NVBAN kann anschliessend als Alias vergeben werden.
  Der Simulator speichert keine Ledgerdatenbank.
- **Viewer & Zahlungen:** Der `NexVerseMoneyModule`-Adapter bildet den
  Live-Agent auf das HG-Wallet ab. Anzeige des NV$-Saldos, normale
  Transfers und Inworld-Kaeufe laufen ueber die existierende zentrale
  Economy-/Commerce-Policy; fehlende oder widerspruechliche Identitaet
  verweigert die Zahlung. Beim Verlassen der Region sind Ausgaben
  von diesem Gast ohne erneut gepruefte Live-Session gesperrt.
- **Aktivierung:** Unter `[NexEconomyViewer]` in der tatsaechlich
  geladenen Simulator-INI `AllowHypergridGuests = true` setzen,
  **erst nach Test** mit mindestens zwei verschiedenen Home-Grids.
  Standard: `false`. Der vorhandene `NEXVERSE_ECONOMY_API_KEY`
  benoetigt weiterhin die bisherigen minimalen `economy:read` und
  `economy:transfer`-Scopes. Kein HG-Economy-Key im Viewer.

**Explizit NICHT enthalten:** Transfer von Fremdgrid-Waehrungen,
Hypergrid-weite Echtgeld-Zahlungen, grenzueberschreitender Wallet-Zugriff
ohne lokale authentifizierte Session, NV$-Wechselkurse und Geldautomaten.
Ein HG-Gast hat **nur ein NexVerse-NV$-Guthaben**, kein
geldliches Guthaben auf seinem Heimatgrid.

### Spaeterer NexVerse-Geldautomat

Einzahlung/Auszahlung soll als **rein virtuelle NV$-Umbuchung**
zwischen bestaetigtem Gast-Wallet und einem getrennten,
autorisierten Bank-/Reservekonto erfolgen. Beide Wege benoetigen:
aktive inworld-Sitzung, HG-Identitaetsbeweis, Rate-Limits,
Betragsgrenzen, Idempotency-Key, genug Deckung, unveraenderliches
Double-Entry-Journal und expliziten Kontoauszug. Eine NVBAN ist
lediglich ein Alias und **kein zweiter Saldo**. Reale Geldzahlungen
oder die Uebertragung auf fremde Hypergrid-Economy-Systeme erfordern
spaeter eine gesonderte, autorisierte Integration; sie duerfen nicht
durch einen normalen LSL-`llGiveMoney`-Trick simuliert werden.

**Release-Status:** Opt-in Entwicklungsumfang, keine Live-Garantie.
Ein Test mit mindestens zwei fremden Home-Grids, identischen
Original-UUIDs auf unterschiedlichen Home-Grids und getrennten
Sim-Prozessen ist fuer produktive Aktivierung Pflicht.

## Zentrale NV$-Masterbank fuer freigegebene LSL-Skripte (11.10.2026)

Die **Masterbank** ist ein vom Betreiber eingerichtetes, dauerhaftes
`Business`-Wallet mit frei waehlbarer UUID. Das Guthaben verbleibt
ausschliesslich im NexVerse-Ledger; die Bank ist **keine Geldschoepfungs-API**.
Sie kann gegenueber lokalen und **angemeldeten HG-Gast-Wallets**
dieselben virtuellen NV$ auszahlen.

**Sicherheitsmodell:** Die Bankkonto-UUID allein darf nie eine Auszahlung
freigeben. Der Simulator prueft vor *jedem* Vorgang:
- `MasterBankEnabled = true` und vorhandene getrennte Bank- und
  Betreiber-UUID; das empfangende Ziel muss aktuell in NexVerse
  angemeldet sein.
- Nur exakt erlaubte Paare aus **Root-Objekt-UUID und
  LSL-Skript-Inventaritem-UUID** erhalten Zugriff. Skriptkopien
  und neu gerezzte Objekte haben normalerweise neue IDs und bleiben gesperrt.
- Das Objekt gehoert dem eingetragenen `MasterBankOperatorId`.
  Der Betreiber hat dem Skript `PERMISSION_DEBIT` erteilt.
- `MasterBankMaxPayout` begrenzt jede einzelne Auszahlung.
  Zusatzeinschraenkungen wie Tageslimits koennen ueber die zentrale
  Banking-Transferpolicy fuer das Bankkonto gesetzt werden.
- Die Umbuchung geht ueber `POST /api/v1/banking/transfers`,
  mit Idempotency-Key, autoritativer Deckungspruefung und
  Double-Entry-Journal. Der Auszahlungscode nutzt ausschliesslich
  die getrennte Bankkonto-UUID als Quelle.
- Keine Schluessel, HTTP-API-Tokens oder privilegierten UUID-Claims
  werden an das LSL-Skript ausgegeben. Das Skript ruft ausschliesslich
  `llNexBankPayout` auf und erhaelt das Ergebnis im
  `transaction_result`-Event. Standardfunktionen wie `llGiveMoney`
  bleiben **avatarfinanziert** und koennen das Bankkonto nicht belasten.

**Konfiguration:** In der geladenen Simulator-`OpenSim.ini`:

```ini
[NexEconomyViewer]
    MasterBankEnabled = false
    MasterBankAccountId = ""
    MasterBankOperatorId = ""
    MasterBankAuthorizedScripts = ""
    MasterBankMaxPayout = 100
```

Die Bank darf erst nach kontrollierter Einrichtung aktiviert werden.
`MasterBankAccountId` ist eine fest gewaehlte **neue, unbenutzte UUID**
und darf nicht der UUID eines vorhandenen Avatars, Gruppenwallets oder
Systemkontos entsprechen. Der Server legt das Konto beim ersten
autorisierten Einsatz als Business-Wallet mit **0 NV$** an.

**Beispiel fuer mehrere Skripte:**
`MasterBankAuthorizedScripts = "OBJEKT-UUID-1/SKRIPT-INVENTAR-UUID-1;OBJEKT-UUID-2/SKRIPT-INVENTAR-UUID-2"`

Die `SKRIPT-INVENTAR-UUID` ist die **Task-Inventory-Item-ID**, nicht
die Skript-Asset-UUID von `llGetInventoryKey()`. Der Administrator
muss das Objekt und sein Task-Inventar ueber vertrauenswuerdige
Simulator-/Inventardiagnose identifizieren. Der Demo-Code liegt unter
[`tools/diagnostics/ogl_master_bank_payout_test.lsl`](../../tools/diagnostics/ogl_master_bank_payout_test.lsl).

**Wichtig:** Das vorhandene `economy:transfer`-Service-Credential ist
allgemein fuer die World-API uebertragungsberechtigt. Die neue
Skriptdelegation haertet die *Inworld-Aufrufer* ab, sie ersetzt
nicht die Absicherung vertrauenswuerdiger Simulatoren und ihrer
Maschinenkeys. Kein ungepruefter LSL-HTTP-Endpunkt darf dieses
Credential nutzen. Konten und Tokens bleiben zentral durch den Betreiber
verwaltet.

HG-Bewohner duerfen NV$ **nur innerhalb NexVerse** verwenden. Sie
koennen dort kaufen, verkaufen und bei spaeteren Bankfunktionen
einzahlen, auszahlen und Zinsen auf virtuelle NV$ erhalten.
Das Guthaben bleibt auch nach dem Ausloggen im zentralen Ledger,
wird aber nicht in Heimatgrid-Waehrung exportiert.
Zinsen/ATM-Buchungen werden spaeter als gedeckte Ledger-Transaktionen
mit eigenem Reservekonto, Freigaben und Limits implementiert.
Noch **keine** automatische Zinszahlung oder ATM-Live-Freigabe.


**Sicherheitsnachtrag:** Bank-Payouts aus LSL verwenden ausschliesslich die
OGL-Funktion `llNexBankPayout(avatar, betrag)` mit
`transaction_result`. Normales `llGiveMoney` und
`llTransferLindenDollars` bleiben **avatarfinanzierte** Funktionen.
Wenn die Masterbank deaktiviert oder das Script nicht berechtigt ist,
meldet `llNexBankPayout` einen Fehler und belastet **niemals** das
private Konto des Bankoperators. Das Demo-Script verwendet nur diesen
eindeutig abgegrenzten Aufruf.
