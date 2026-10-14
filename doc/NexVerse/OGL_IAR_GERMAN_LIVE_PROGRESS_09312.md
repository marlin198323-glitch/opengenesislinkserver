# OGL 0.9.3.12 – IAR-Konsole: deutsche Meldungen und Live-Fortschritt

## Funktionsumfang

Die klassische OpenGenesisLINK-Simulatorkonsole zeigt beim IAR-Export und
-Import die Fortschrittsphasen auf Deutsch. Beispiele:

```text
[OGL-IAR]: Exportiere Archiv backup.iar aus Inventarpfad / für Muster Resident.
[OGL-IAR]: Inventar erfassen: 474 Einträge bearbeitet (Gesamtzahl noch unbekannt).
[OGL-IAR]: Inventar erfasst [########------------] 40% (474/474 Einträge)
[OGL-IAR]: Assets sichern [#############-------] 65% (1288/2832 Assets)
[OGL-IAR]: Abschluss: Archiv wird finalisiert und geprüft (98 %).
[OGL-IAR]: [####################] 100% – Exportvorgang abgeschlossen (Asset-Warnungen beachten).
[OGL-IAR]: Archiv für Muster Resident erfolgreich gespeichert: 474 Inventareinträge.
```

**Live-Anzeige:** Beim aktiven IAR-Vorgang wird der Status in der
interaktiven **lokalen Simulator-Konsole** direkt neben dem Prompt
aktualisiert. Das geschieht etwa bei jeder Prozentänderung, ohne tausende
neue Logzeilen zu erzeugen. Der eingegebene Befehl und der Cursor bleiben
erhalten. In sehr schmalen Terminalfenstern wird die Anzeige gekürzt oder
ausgeblendet. Die Logzeilen bleiben unabhängig davon alle fünf Prozent
als dauerhaftes Protokoll erhalten.

Die Anzeige lässt sich in `[Startup]` konfigurieren:

```ini
[Startup]
OGLIarLiveProgress = true
```

Mit `false` wird ausschließlich die Live-Anzeige im lokalen Prompt
deaktiviert; die Protokoll-Fortschrittsmeldungen und Archivvorgänge
bleiben erhalten.

**Jederzeit abfragen (auch in Remote-Konsolen):**

```text
show iar progress
```

Der Befehl zeigt laufende IAR-Konsolenjobs mit Archivdatei,
Verarbeitungsphase, Prozentanzeige, Eintragszähler, Laufzeit und
interner Auftrags-ID. Bei mehreren parallelen Jobs werden alle aufgelistet;
die lokale Statusanzeige verweist dann auf den Abfragebefehl.

## Warnungen

Die Ausgabe des gemeinsamen `AssetsArchiver` im Abstand von 100 Assets
wurde von INFO auf DEBUG reduziert und übersetzt. Dadurch konkurrieren
die Meldungen nicht mehr mit dem OGL-IAR-Fortschritt.

Fehlende, beschädigte oder nicht erkannte Assets bleiben **sichtbar**:
Die WARN-Zeile nennt deren Anzahl. Ein IAR kann technisch erfolgreich
gespeichert sein, obwohl das Archiv wegen solcher Referenzen inhaltlich
unvollständig ist. Das wird ausdrücklich vermerkt und darf nicht als
vollständiger Backup-Restore-Nachweis gewertet werden.

Die **100-%-Meldung** wird ausschließlich aus dem Abschlussereignis
ausgegeben, nicht allein weil die Asset-Verarbeitung 95 % erreicht hat.
Sie bedeutet ein technisch abgeschlossenes Schreiben des Archivs;
Integritäts- und Restore-Tests bleiben separat erforderlich.

## Abnahme

```bash
python3 tools/ci/verify_iar_progress.py
dotnet build OpenSim.sln -c Release
```

Live-Test auf isolierter Entwicklungsregion mit `save iar` und `load iar`:

1. Beobachte Fortschrittsprompt und 5-%-Logcheckpoints.
2. Während eines laufenden Exports `show iar progress` eingeben.
3. Sicherstellen, dass der Status nach Abschluss wieder verschwindet.
4. Beschädigtes Asset oder defekte IAR in einem isolierten Testkonto
   überprüfen; WARN beziehungsweise sichere Importablehnung erwarten.
5. Normale und Remote-Konsole sowie schmale Terminalfenster gegenprüfen.

Diese Codeänderungen ersetzen **nicht** den manuellen IAR01–03-RC1-Test
unter einem realen OpenGenesisLINK-Simulator.
