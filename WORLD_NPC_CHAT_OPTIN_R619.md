# R619 – Freiwillige Chatarchivierung eigener NPC-Direktnachrichten

Stand: 31.10.2026 · Repository: opengenesislinkserver · Basis: R617.

## Was aufgezeichnet wird

Ausschließlich direkte Inworld-Instant-Messages zwischen einem NPC und seinem bestätigten Eigentümer. Der Simulator erfasst eingehende Owner-NPC-IMs und ausgehende NPC-Owner-IMs an den bestehenden Ereignissen des NPC-Moduls. Andere Bewohner, Gruppen-IMs und öffentliche/regionale Chats sind ausgeschlossen. Es werden keine alten Bürger-IM-Daten zur künstlichen Erzeugung von NPC-Gesprächen benutzt.

Die Simulatorereignisse werden ausschließlich bei Betreiberfreigabe per HTTPS und HMAC-SHA256 an /internal/nexnpc/chat/v1 übertragen. ROBUST prüft für jedes Ereignis erneut die aktuelle bestätigte Besitzer-UUID aus den signierten Regionensnapshots. Ohne vollständigen Regionsnachweis wird nichts angenommen.

## Einwilligung und Aufbewahrung

Standardmäßig ist die Aufzeichnung für jeden Bürger deaktiviert. Ein Bürger schaltet sie über POST /api/v1/npcs/history/settings mit dem JSON-Body {"enabled":true} explizit ein. Über denselben Endpunkt mit {"enabled":false} wird sie abgeschaltet und alle gespeicherten NPC-Chatnachrichten werden sofort gelöscht. GET /api/v1/npcs/history/settings zeigt den Status und die tatsächliche Aufbewahrungsdauer; DELETE /api/v1/npcs/history löscht sämtliche eigenen gespeicherten NPC-Chats, ohne den Einwilligungsstatus zu ändern.

Die Einstellung und Löschung funktionieren auch ohne verfügbare Simulatoren, nach persönlicher World-Authentifizierung. Alle Schreibzugriffe auf Einstellungen und die Löschfunktion erfordern relationships:write; das Lesen relationships:read. Der Chatverlauf wird nur bei aktiver Einwilligung über GET /api/v1/npcs/{npcId}/messages geliefert, nach Prüfung des aktuellen NPC-Eigentümers.

Gespeichert wird in einer eigenen, dauerhaft nutzbaren SQLite-Datei: Event-ID, Owner-UUID, NPC-UUID, Senderrolle, Nachricht und Aufnahmezeit. Eine Einwilligung ist für jede Archivierung zwingend. Es gibt eine Ereignis-ID-Deduplizierung, eine Obergrenze von 4000 UTF-8-Bytes pro Nachricht, maximal 10000 Nachrichten pro Besitzer und eine maximale Aufbewahrungsdauer von 90 Tagen. Der konfigurierbare Standard ist 30 Tage, das erlaubte Intervall 7–90 Tage. Die Aufbewahrungsfrist beginnt bei Aufnahme.

Ohne eingerichtetes World-Archiv antwortet die API mit 503 npc_chat_archive_unavailable, ohne Einwilligung mit 403 npc_chat_consent_required. Kein Demo-Chat oder irreführend leerer Verlauf.

## Optionale, zunächst deaktivierte Konfiguration

ROBUST:

    [NexNPCPortal]
    Enabled = true
    ExpectedRegionCount = <tatsächlich erwartete Regionenzahl>
    ChatArchiveEnabled = true
    ChatStorePath = data/nexverse-npc-chat.sqlite
    ChatRetentionDays = 30

Simulator:

    [NPC]
    Enabled = true
    NexNPCSnapshotEnabled = true
    NexNPCChatCaptureEnabled = true

Die HMAC-Umgebungsvariablen NEX_NPC_INGEST_URL (HTTPS mit Pfad /internal/nexnpc/snapshot/v1) und NEX_NPC_INGEST_SECRET (mindestens 32 Byte) werden aus R617 verwendet. Die Chatübertragung verwendet den privaten Pfad /internal/nexnpc/chat/v1 mit demselben signierten Verfahren. Interne Endpunkte zusätzlich durch Firewall/Reverse Proxy gegen externen Zugriff absichern. Die SQLite-Datei muss restriktiv geschützt und für den ROBUST-Dienst beschreibbar sein. Keine Schlüssel in Git, Dokumentation oder Chat ausgeben.

## Einschränkungen

Es werden nur Live-NPCs mit gültig signierter Regionenzuordnung und direkte Owner-NPC-IMs erfasst. Öffentlicher Chat, Drittpersonen-Dialoge und ältere Konversationen werden nicht gespeichert oder nachträglich erfunden. Die Simulatorübertragung arbeitet mit begrenzter Warteschlange und maximal drei Zustellversuchen: Bei längeren Netzwerkproblemen können IMs fehlen, der Verlauf ist nicht revisionssicher. Die Aufzeichnung wird erst nach gesonderter World-/Simulator-Bereitstellung, Betreiberkonfiguration und erforderlicher Datenschutzinformation aktiv. Eine Portal-Installation allein genügt nicht.

Der nächste Portal-Schritt ist die Einwilligungs- und Löschbedienung auf /citizen/npc mit dazu passenden Besitzer-API-Routen.

## Regressionstests

- tools/ci/NexNpcChatArchiveRegression: Default-Off, Opt-in, Besitzer-Isolation, Deduplizierung, Sender, Restart-Persistenz, Löschung, Limits und Retention
- tools/ci/verify_npc_chat_optin_r619.py: HMAC, Besitzerprüfung, Dialogfilter, Consent, getrennte Ablage und OpenAPI
- Vollständiger OpenGenesisLINK-Release-Build und bestehende Datenbanktests in GitHub-CI

Produktivstatus nur nach erfolgreich bestandener CI und nach gesondertem World-/Simulator-Rollout bestätigen.
