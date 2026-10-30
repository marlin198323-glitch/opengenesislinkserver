# R579 – Freundschaftseinladungen aus dem Bürgerportal im Viewer anzeigen

Stand 30.10.2026. Befund: Die World-API POST /api/v1/relationships/{principalId}/{targetId} speicherte die beiden ausstehenden FriendInfo-Einträge und lieferte HTTP 202, löste bei einem bereits im Simulator eingeloggten Empfänger aber kein InstantMessageDialog.FriendshipOffered aus.

## Änderung auf World-Seite

- Die persönliche World-Authentifizierung mit relationships:write, Besitzerprüfung, Zielvalidierung und gespiegelten FriendInfo-Zeilen bleiben erhalten.
- Nur nach erfolgreicher Speicherung beider Seiten wird für einen aktuell online befindlichen Empfänger anhand bestätigter World-Presence der vorhandene HGInstantMessageService verwendet.
- An den Simulator-IM-Dienst geht genau ein GridInstantMessage vom Typ FriendshipOffered. Absendername stammt von World; imSessionID entspricht dem Absender, wie FriendsModule erwartet.
- Offline-Empfänger bekommen keine doppelte Offline-IM: der Simulator liefert beim nächsten Login die bereits ausstehenden FriendInfo-Einladungen. Der vorhandene Mechanismus bleibt unverändert.
- Die optionale IM-Bridge kann fehlen oder ablehnen. Die dauerhafte Anfrage wird deswegen nicht zurückgerollt. Die 202-Antwort ergänzt viewer_notification (accepted, offline_pending, presence_unavailable, delivery_unavailable, not_delivered, sender_unavailable). accepted heißt vom Nachrichtendienst übernommen, nicht Popup empfangen.
- Keine doppelte Einladung bei demselben Beziehungsstatus, kein Umgehen der Authentifizierung, keine fremden Bearer und kein spekulatives Nachsenden bei unklarer Zustellung.

## Freigabe

1. World mit .NET SDK 8 in der GitHub-CI bauen. Auf Mint ist derzeit kein dotnet installiert; lokal wurde nur der statische Vertrag geprüft.
2. ROBUST/API auf dem World-Server aktualisieren. HGInstantMessageService muss wie für die bestehende Portalchat-Funktion konfiguriert sein.
3. Zwei echte lokale Avatare A und B prüfen: B ist online, A sendet im Bürgerportal, B bekommt im Viewer ein Freundschaftsangebot. B nimmt an und World/Portal zeigen die Freundschaft.
4. B ist offline: A sendet im Bürgerportal; beim nächsten B-Login zeigt FriendsModule die gespeicherte offene Einladung. Keine doppelte Benachrichtigung.
5. Bei fehlendem IM-Dienst bleibt die Anfrage in World bestehen; viewer_notification zeigt die nicht bestätigte Zustellung.

Die World- und Stadtportal-Repositories benötigen **getrennte Deployments**. Ohne World-Release ist die Viewer-Benachrichtigung noch nicht aktiv.
