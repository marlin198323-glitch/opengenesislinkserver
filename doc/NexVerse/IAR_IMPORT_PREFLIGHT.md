# OpenGenesisLINK – IAR-Import-Vorabprüfung

**Entwicklungsstatus:** neu implementierte obligatorische
Prüfschicht im zentralen `InventoryArchiverModule`,
noch nicht als Stable freigegeben.

`OglIarImportGuard.TryValidate` wird vor
`InventoryArchiveReadRequest.Execute()` aufgerufen: für
den normalen `load iar`-Konsolenbefehl, für API-/NodeAgent-
gesteuerte Importe sowie Stream-basierte Importe.

Dadurch läuft bei einer abgewiesenen Datei **kein**
Inventory- oder Asset-Schreibpfad aus dem Archiv-Importer an.

Die Vorprüfung liest gzip/TAR einmal komplett und setzt den
Streamzeiger für den späteren regulären Import zurück. Sie
erzwingt:

| Grenze | Wert |
|---|---|
| Komprimierte IAR | maximal 2 GiB |
| Entpackte TAR-Daten | maximal 4 GiB |
| Einzelnes Asset | maximal 64 MiB |
| Einzelner Inventory-XML-Eintrag | maximal 4 MiB |
| Insgesamt TAR-Einträge | maximal 100.000 |
| Insgesamt Assets | maximal 25.000 |
| Archivsteuerdatei `archive.xml` | vorhanden, max. 512 KiB, valides XML |
| Inventar | mindestens 1 Eintrag, syntaktisch valides XML |
| Sicherheit | keine Duplikatpfade, Links, Spezialdateien, Traversal, fremde TAR-Verzeichnisse |
| Integrität | TAR-Checksummen, Blocklängen, gzip/TAR-Ende und Kompressionsverhältnis |

Das löst insbesondere **Gzip-Bombs, Pfadtraversal, defekte
TAR-Header, unbegrenzte Eintragszahlen und unkontrollierte
Asset-Größen** abweisend aus, bevor FSAssets/Inventar geschrieben
werden. Für maximale Sicherheitsgarantien müssen auch
Skript-/Objektinhalte und referenzierte Assets semantisch geprüft
und serverseitige Upload-Kontingente ausgebaut werden. Die
Vorprüfung ist **kein Malware- oder Content-Scanner** und
garantiert weder Integrität fremder Assets noch automatisch
vollständige Transaktions-Rollbacks bei Fehlern, die erst
nach dem Preflight während des Imports auftreten.

Importe sollten nur aus einem kontrollierten Verzeichnis
über autorisierte Nutzer mit separaten Gesamt-Storage-/CPU-
Quotas ausgeführt werden. Große Archive benötigen aufgrund der
zweifachen Lektüre mehr Zeit. Bei manipulierten Archiven oder
gesperrten Quoten gibt es eine ausführliche Ablehnung, keine
stillschweigende Teilannahme.
