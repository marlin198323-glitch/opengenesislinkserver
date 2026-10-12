# OpenGenesisLINK – Starter-Avatar-Vorlagen (Phase 1)

## Ziel

Das Stadtportal soll bei der Registrierung einen Starter-Avatar auswählen lassen:

| Portal-ID | Modellavatar |
| --- | --- |
| `ogl:benjamin` | Benjamin Resident |
| `ogl:luna` | Luna Resident |

Es werden **nur die aktuell getragenen Wearables, Bodyparts und Attachments**
(einschließlich getragener Mesh-Objekte) übernommen, nicht das vollständige
Inventar des Modellavatars.

## Implementierter Integrationspunkt

`OpenSim/Services/UserAccountService/UserAccountService.cs` verarbeitet
`CreateUser(..., model)` bereits mit `EstablishAppearance` und
`CopyWearablesAndAttachments`. Die OGL-Erweiterung löst die expliziten
Portal-IDs zu den Modellavataren auf. Der bestehende OpenSim-Mechanismus
kopiert Inventar-Einträge, legt Current-Outfit-Links an und setzt die Appearance.

Die Funktion ist bewusst **standardmäßig ausgeschaltet**.

```ini
[UserAccountService]
CreateDefaultAvatarEntries = true

[OglAvatarTemplates]
Enabled = false
BenjaminModel = "Benjamin Resident"
LunaModel = "Luna Resident"
```

Nach expliziter Freigabe kann `Enabled = true` gesetzt werden.
Die Portal-Registrierung muss den Modellparameter `ogl:benjamin` oder
`ogl:luna` über einen **authentifizierten serverseitigen**
Registrierungs-Endpunkt an `CreateUser` übergeben. Ein öffentlicher
Registrierungs-Endpunkt ist **noch nicht** implementiert.

## Aktuelle Grenzen

1. Die Übernahme nutzt derzeit den **Live-Appearance-Zustand** der
   Modellavatare. Unveränderliche Snapshots und Admin-Publish fehlen noch.
2. Der vorhandene OpenSimulator-Kopierpfad muss vor Freigabe auf
   Transfer-/Copy-Rechte, Objektinhalte und verlinkte Attachments geprüft
   und gegebenenfalls gehärtet werden. Die OGL-Alias-Funktion allein
   gewährleistet diese Prüfung noch nicht.
3. Das Stadtportal ist noch nicht angeschlossen. Das Repository enthält
   nur den serverseitigen Modellparameter-Integrationspunkt.
4. Unbekannte oder deaktivierte `ogl:`-IDs werden nun **vor der Kontoerstellung**
   abgelehnt. Ebenso werden fehlende Modellaccounts, Appearance oder
   benötigte Services abgewiesen. Die Portal-API muss diese Ablehnung
   korrekt als Registrierungsfehler behandeln.
5. GitHub-CI und echte Avatar-Login-/Appearance-Tests stehen aus; ein Merge auf `main` ersetzt diese Tests nicht.

## Zusätzliche Sicherheitsgrenze

Die gemeinsame Inventarauflösung unterstützt direkte Einträge und genau eine
Current-Outfit-Verknüpfung für Wearables **und** Attachments. Verschachtelte
und zyklische Links werden verworfen. Die neue Vorabprüfung kontrolliert nun, ob alle in der Appearance
referenzierten Wearables und Attachments vorhanden sind und die
Current-/Next-Owner-Berechtigungen **Copy + Transfer** besitzen.
Eine erneute Validierung unmittelbar vor dem Kopieren reduziert die
Zeitlücke zwischen Registrierungsvorabprüfung und Übernahme.
Leere Item-UUIDs werden als ungültig behandelt. Sie garantiert
**noch nicht**, dass verschachtelte Inhalte von
Objektinventaren ebenfalls freigegeben sind. Die bestehende
`CopyWearablesAndAttachments`-Routine wird weiterhin verwendet.
Ein vollständiger atomarer Registrierungs-/Inventar-Workflow und
Snapshot-Publishing sind nicht implementiert. Die Vorlage bleibt
bis zur Rechteprüfung und echten Integrationstests deaktiviert.

## Geplante Phase 2

- Tiefenprüfung verschachtelter Attachment-Inhalte, Link-Ketten und
  Race-Conditions zwischen Vorabprüfung und tatsächlichem Kopieren.
- Veröffentlichbare, versionierte Snapshots, getrennt vom Live-Avatar.
- Authentifizierte API für das Stadtportal mit ausschließlich
  freigegebenen Template-IDs.
- Tests für Mesh-Body, Mesh-Head, Kleidung, COF und Erstlogin.

**Integrationsstand:** Änderungen liegen inzwischen auf `main` und können dort getestet werden. Keine Stable-Freigabe und keine Zugriffe auf Entwicklerrechner.
