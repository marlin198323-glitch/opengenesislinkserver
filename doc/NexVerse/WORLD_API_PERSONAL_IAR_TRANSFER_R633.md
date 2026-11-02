# R633: Privater IAR-Dateitransfer – abgesicherte Grundlage

Stand: 02.11.2026; **kein produktiver Bürgerexport/Import**.

## Implementierung

`NexVerse/Core/Jobs/OglPersonalIarTransferStore.cs` legt transferierte Bytes ausschließlich unter einer vom Server vorgegebenen privaten Directory-Root ab. Dateinamen bestehen aus einer serverseitigen `Guid` plus festem Suffix; Benutzer können weder Dateinamen noch Verzeichnispfade bestimmen. Jeder Aufruf bindet die Operation an ihren Eigentümer (`Guid ownerId`) und verweigert unbefugtes Lesen, Schreiben, Verifizieren und Löschen.

- Maximal 512 MiB pro Auftrag; max. 1 MiB pro Uploadblock; kein Upload bei falschem Offset, zu großen/duplizierten Blöcken oder nach erfolgreicher SHA-256-Prüfung.
- Manifest wird atomar geschrieben; nach Neustart ist ein konsistenter Upload fortsetzbar. Widerspruch zwischen Manifest und Datendatei blockiert das Fortsetzen, statt fremde Bytes zu überschreiben.
- SHA-256 muss vollständig mit den tatsächlich gespeicherten Bytes übereinstimmen, erst danach wird die `.part`-Datei zum privaten `.iar`-Stagingnamen verschoben. Der Stage darf ausschließlich mit identischer Eigentümer-ID gelesen/gelöscht werden.
- Symlinks als Staging-Root oder Datendatei werden abgewiesen. Es gibt keinen URL- oder HTTP-Endpunkt für diese Dateinamen.

**Wichtig:** Eine gültige SHA-256-Prüfsumme bedeutet nur unveränderte Bytes, nicht ein gültiges IAR-Archiv. Vor jedem Import müssen der Simulator-Archiver und die gesicherte GZip/TAR-Inspektion Größe, Dateistruktur, Links, Traversal und Assets prüfen. Die Datei darf nicht vor einem abgeschlossenen echten Export als herunterladbares IAR angeboten werden.

## Noch nicht integriert

- Authentifizierter privater Übertragungsweg vom Simulator zur World-API, mit sicherer Node-Zuordnung und Replay-Schutz.
- Besitzergebundener Server-Endpunkt `POST /me/export` und privater Download; in R632 bleibt ein bewusstes HTTP 503 als Sicherheitsbarriere bestehen.
- Vollständiger Importfluss einschließlich Quota, Chunk-HTTP-Parser, vollständiger GZip/TAR-Validierung, Restore und Rollback.
- Ablaufzeiten, Cleanup defekter/staler Transfers und verteilte Locks für mehrere World-API-Prozesse.

## Tests

Das isolierte `OglPersonalIarTransferRegression`-Projekt prüft Besitzerisolation, Null-/Übergrößen, leere Blöcke, falsche Offsets, Wiederholungen, Neustart, Teilübertragungen, SHA-256-Fehler und -Erfolg, Lesen und Löschen durch Besitzer sowie Symlink-Verweigerung. Die vollständige .NET-World-CI kompiliert und führt die Regression aus. Kein Live-Bürgerkonto und kein World-Server werden für diese Tests benötigt.