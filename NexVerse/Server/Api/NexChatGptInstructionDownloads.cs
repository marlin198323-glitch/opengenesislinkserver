// SPDX-License-Identifier: MPL-2.0

using System;
using System.Net;
using System.Text;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexChatGptInstructionDownloads
    {
        private readonly string m_PublicBaseUrl;
        private readonly string m_ApiBaseUrl;

        public NexChatGptInstructionDownloads(string publicBaseUrl)
        {
            m_PublicBaseUrl =
                string.IsNullOrWhiteSpace(publicBaseUrl)
                    ? "https://world.stadt-nexverse.de"
                    : publicBaseUrl.Trim().TrimEnd('/');
            m_ApiBaseUrl = m_PublicBaseUrl + "/api/v1";
        }

        public void HandleAll(IOSHttpRequest request, IOSHttpResponse response) =>
            WriteMarkdown(request, response, "opengenesislink-chatgpt-anweisung.md", BuildAll());

        public void HandleCitizen(IOSHttpRequest request, IOSHttpResponse response) =>
            WriteMarkdown(request, response, "opengenesislink-chatgpt-buergerportal.md", BuildCitizen());

        public void HandleAdmin(IOSHttpRequest request, IOSHttpResponse response) =>
            WriteMarkdown(request, response, "opengenesislink-chatgpt-adminbereich.md", BuildAdmin());

        private static void WriteMarkdown(
            IOSHttpRequest request,
            IOSHttpResponse response,
            string fileName,
            string markdown)
        {
            response.KeepAlive = false;

            if (request == null ||
                !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.ContentType = "text/plain; charset=utf-8";
                response.RawBuffer = Encoding.UTF8.GetBytes("GET ist erforderlich.\n");
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/markdown; charset=utf-8";
            response.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.AddHeader("Referrer-Policy", "no-referrer");
            response.RawBuffer = Encoding.UTF8.GetBytes(markdown ?? string.Empty);
        }

        private string BuildAll()
        {
            StringBuilder md = NewDocument(
                "OpenGenesisLINK World API - ChatGPT Gesamtanweisung",
                "Aktualisierter OGL World-API-Funktionsatlas fuer ChatGPT: Buergerportal, Administration, Betrieb und sichere Service-Integrationen. Alle registrierten OpenAPI-Pfade werden nach Zielgruppe katalogisiert.");

            AppendGlobalRules(md);
            AppendIdentityAndScope2026(md);
            AppendCitizenSection(md);
            AppendCitizenCoverage2026(md);
            AppendAdminSection(md);
            AppendAdminCoverage2026(md);
            AppendServiceSection(md);
            AppendServiceBoundaries2026(md);
            AppendRequestPlaybooks2026(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);
            AppendCompleteRouteIndex(md, "all");

            md.AppendLine();
            md.AppendLine("## Empfohlener Start fuer jede neue Aufgabe");
            md.AppendLine();
            md.AppendLine("1. Zuerst den aktuellen Vertrag von " + m_ApiBaseUrl + "/openapi.json laden.");
            md.AppendLine("2. Den gewuenschten Endpunkt anhand von x-nexverse-audience, x-nexverse-scope, x-nexverse-ai-instruction und x-nexverse-security-constraints klassifizieren.");
            md.AppendLine("3. Entscheiden, ob die Aufgabe zum Buergerportal, Adminbereich oder zu einer Service-Integration gehoert. Interne Simulator-/Viewer-CAPS niemals als Buergerportal-Operation einsetzen.");
            md.AppendLine("4. Nur die minimal benoetigten Berechtigungen verwenden.");
            md.AppendLine("5. Vor schreibenden oder loeschenden Admin-Aktionen sicherstellen, dass der Benutzer diese konkrete Aktion angefordert hat.");
            md.AppendLine("6. Bei Fehlern Statuscode, API-Fehler und X-Correlation-Id auswerten; fehlende Rechte niemals umgehen.");
            md.AppendLine();
            md.AppendLine("Bei Abweichungen ist " + m_ApiBaseUrl + "/openapi.json die technische Quelle der Wahrheit. Der Vertrag ist installations- und versionsbezogen; vor Mutationen zusaetzlich echte Scope-/Feature-Freischaltung pruefen.");
            return md.ToString();
        }

        private string BuildCitizen()
        {
            StringBuilder md = NewDocument(
                "OpenGenesisLINK World API - ChatGPT Anweisung Buergerportal",
                "Vollstaendige Buergerportal-Arbeitsanweisung: Konto, Identitaet, Avatarprofil, Freunde/Chat, Gruppen, Inventar, NV$-Banking, Handel, Land, Weltkarte, Suche und Experiences. Individuelle Rechte und Eigentum beachten.");

            AppendGlobalRules(md);
            AppendIdentityAndScope2026(md);
            AppendCitizenSection(md);
            AppendCitizenCoverage2026(md);
            AppendRequestPlaybooks2026(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);
            AppendCompleteRouteIndex(md, "citizen");

            md.AppendLine();
            md.AppendLine("## Harte Grenze");
            md.AppendLine();
            md.AppendLine("Operationen mit Zielgruppe nur admin oder mit admin:* gehoeren nicht in das Buergerportal. Wenn eine Aufgabe solche Rechte benoetigt, muss sie in den Adminbereich uebergeben werden.");
            return md.ToString();
        }

        private string BuildAdmin()
        {
            StringBuilder md = NewDocument(
                "OpenGenesisLINK World API - ChatGPT Anweisung Adminbereich",
                "Vollstaendige Administrations-Anweisung: Resident-Lifecycle, rollenbasierte Rechte, Sicherheit, API-Schluessel/OAuth, Grid/Regionen/Estates/Nodes, Economy, Banking, Discovery, Gruppen, Experiences und Betriebsdiagnostik.");

            AppendGlobalRules(md);
            AppendIdentityAndScope2026(md);
            AppendAdminSection(md);
            AppendAdminCoverage2026(md);
            AppendServiceSection(md);
            AppendServiceBoundaries2026(md);
            AppendRequestPlaybooks2026(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);
            AppendCompleteRouteIndex(md, "admin");

            md.AppendLine();
            md.AppendLine("## Harte Grenze");
            md.AppendLine();
            md.AppendLine("Administrative Mutationen nur mit ausdruecklicher Benutzerabsicht ausfuehren. Kontosperren, Deaktivierungen, Passwort-Resets, Economy-Reversals, Region-Lifecycle, Estate-Aenderungen und API-Key/OAuth-Verwaltung sind privilegierte Aktionen.");
            return md.ToString();
        }

        private StringBuilder NewDocument(string title, string purpose)
        {
            StringBuilder md = new StringBuilder();
            md.AppendLine("# " + title);
            md.AppendLine();
            md.AppendLine(purpose);
            md.AppendLine();
            md.AppendLine("## Produkt und Grid strikt unterscheiden");
            md.AppendLine();
            md.AppendLine("- OpenGenesisLINK (OGL) ist die Server-Software und World API; Stadt-NexVerse bzw. NexVerse ist lediglich ein Betreiber-Grid.");
            md.AppendLine("- API und Anleitung gelten fuer alle OGL-Grids: niemals Stadt-NexVerse fest als Produkt oder Grid-Adresse voraussetzen.");
            md.AppendLine("- Legacy-Vertraege wie [NexVerseWorldApi], X-NexVerse-Api-Key und x-nexverse-* bleiben fuer Kompatibilitaet unveraendert.");
            md.AppendLine("- Version im aktiven Dienst: " + OpenSim.VersionInfo.Version.Trim() + "; API-Stand und Feature-Aktivierung direkt per Health, Version, Capabilities und OpenAPI verifizieren.");
            md.AppendLine("- Entwicklungsregel: fertiggestellte OpenGenesisLINK-Funktionen im selben Change in den aktiven OGL-Konfigurationen UND .ini.example-Vorlagen aktivieren, durch CI absichern und im Dashboard sichtbar machen. Nicht stillschweigend deaktiviert ausliefern.");
            md.AppendLine("- Vor dem Live-Rollout Schluessel, Abhaengigkeiten und Berechtigungen pruefen. Sicherheitskritische Features niemals durch Default-Secrets, Umgehung von Berechtigungen oder stilles fail-open scheinbar funktionsfaehig machen.");
            md.AppendLine("- OGL Control Center unter /api/v1/docs: Passwort-Login mit lokalem GridUser, UserLevel ab 200 und admin:* im signierten Session-Token. Die automatische Scope-Berechnung bei Login stellt weitere explizite Lese-Scopes bereit; pro Operation gelten dennoch Audience, Eigentum und separate Policies.");
            md.AppendLine("- /api/v1/feature-control/nodes ist nur verfuegbar, wenn [OGLFeatureControl] Enabled und Signierschluessel eingerichtet sind; HTTP 404 bedeutet dann fehlendes optionales Modul, nicht automatisch falsche Lizenz.");
            md.AppendLine("- Hypergrid-IM, Map-Bild-/JPEG2000-Decoding und fremde XML-RPC-Anfragen koennen externe Fehlerquellen sein: erst Daten, Quelle und Korrelation ermitteln; keine permanenten Sicherheitsfilter entfernen.");
            md.AppendLine();
            md.AppendLine("## Laufende OpenGenesisLINK-Endpunkte");
            md.AppendLine();
            md.AppendLine("- API-Basis: " + m_ApiBaseUrl);
            md.AppendLine("- API-Dokumentation: " + m_ApiBaseUrl + "/docs");
            md.AppendLine("- OpenAPI 3.1: " + m_ApiBaseUrl + "/openapi.json");
            md.AppendLine("- Health: " + m_ApiBaseUrl + "/health");
            md.AppendLine("- Version: " + m_ApiBaseUrl + "/version");
            md.AppendLine("- Capabilities: " + m_ApiBaseUrl + "/capabilities");
            md.AppendLine("- Gesamtanweisung: " + m_ApiBaseUrl + "/chatgpt-instructions.md");
            md.AppendLine("- Buergerportal-Anweisung: " + m_ApiBaseUrl + "/chatgpt-buergerportal.md");
            md.AppendLine("- Admin-Anweisung: " + m_ApiBaseUrl + "/chatgpt-admin.md");
            md.AppendLine();
            md.AppendLine("Die allgemeinen World-API-Routen liegen unter " + m_ApiBaseUrl + "/...; OAuth/OIDC liegt teils unter /oauth/... und /.well-known/openid-configuration (separat im OpenAPI-Vertrag). Manche Routen werden nur registriert, wenn [NexVerseWorldApi] EnablePrivilegedEndpoints und die zugehoerigen Services aktiviert sind.");
            md.AppendLine("OpenAPI enthaelt auch vertrauenswuerdige Simulator-Bridges: deren Dokumentation erlaubt KEINEN direkten Zugriff mit normalen Einwohner- oder ChatGPT-Tokens.");
            return md;
        }

        private static void AppendGlobalRules(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## Globale Regeln fuer ChatGPT");
            md.AppendLine();
            md.AppendLine("- Niemals Passwoerter, Bearer-Tokens, API-Schluessel, Client-Secrets oder andere Secrets im Chat, in Logs oder in generierten Dateien offenlegen.");
            md.AppendLine("- Niemals Berechtigungen erfinden, erweitern oder aus der Existenz eines Endpunkts ableiten.");
            md.AppendLine("- Immer den im OpenAPI-Dokument angegebenen Scope und die Zielgruppe beachten.");
            md.AppendLine("- Einwohneraktionen standardmaessig auf den authentifizierten Einwohner beschraenken.");
            md.AppendLine("- Zugriff auf fremde Konten, fremdes Inventar oder administrative Ressourcen nur mit dokumentierter Admin-Autorisierung.");
            md.AppendLine("- Fuer zustandsaendernde Operationen den dokumentierten Idempotency-Key verwenden, wenn der Endpunkt dies verlangt.");
            md.AppendLine("- Request- und Response-Felder aus dem OpenAPI-Schema lesen und nicht erraten. Bei unvollstaendigen Teil-Schemas die Operation nicht blind durch ausgedachte Felder ergaenzen.");
            md.AppendLine("- API-v1, Protokoll- und Serverversion sind getrennte Angaben. Aktuelle OGL-Serverversion: " + OpenSim.VersionInfo.Version.Trim() + ". Immer die tatsaechliche Laufzeitantwort GET /api/v1/version pruefen; daraus allein keinen GitHub-Tag ableiten.");
            md.AppendLine("- Ein Eintrag im OpenAPI-Katalog beweist nicht, dass ein optionales Modul im laufenden Robust freigeschaltet oder eine viewer-spezifische Funktion live abgenommen ist.");
            md.AppendLine("- HTTP 401 bedeutet fehlende oder ungueltige Authentifizierung; HTTP 403 bedeutet fehlende Berechtigung. Keinen privilegierten Alternativpfad zum Umgehen ausprobieren.");
            md.AppendLine("- HTTP 429 respektieren und Rate-Limits beachten.");
            md.AppendLine("- X-Correlation-Id bei Fehlersuche und administrativen Vorgaengen erhalten.");
            md.AppendLine("- Veraltete Operationen nur verwenden, wenn kein aktueller Ersatz verfuegbar ist.");
        }

        private void AppendCitizenSection(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## Buergerportal / Einwohner-Selbstbedienung");
            md.AppendLine();
            md.AppendLine("Das Buergerportal arbeitet im Kontext eines angemeldeten Einwohners. Nur Funktionen anbieten, die das eigene Konto oder explizit gewaehrte OAuth-Scopes erlauben.");
            md.AppendLine();
            md.AppendLine("### Anmeldung und Sitzung");
            md.AppendLine("- Native Sitzung: POST " + m_ApiBaseUrl + "/auth/session");
            md.AppendLine("- Native Portal-Sitzung ist implementiert. OAuth2 Authorization Code mit PKCE S256 ist moeglich, aber NUR nach tatsaechlicher Client-Registrierung, erlaubten redirect_uri/allowed_scopes, Browser-Callback und State-/PKCE-Absicherung; kein stiller Wechsel auf nicht konfigurierte OAuth-Flows.");
            md.AppendLine("- Eigene Sitzungen: POST " + m_ApiBaseUrl + "/auth/sessions/revoke");
            md.AppendLine();
            md.AppendLine("### Eigenes Einwohnerkonto");
            md.AppendLine("- GET /users/me fuer das aktuell authentifizierte Konto (vollstaendige URL: /api/v1/users/me).");
            md.AppendLine("- GET/PATCH /users/{principalId} nur im dokumentierten Self-Kontext.");
            md.AppendLine("- POST /users/{principalId}/password nur fuer den eigenen Account, ausser eine ausdrueckliche Admin-Autorisierung liegt vor.");
            md.AppendLine();
            md.AppendLine("### Freunde, Nachrichten und Avatarprofil");
            md.AppendLine("- GET /relationships/{principalId} liest die eigene Freundesliste und ausstehende Anfragen (relationships:read).");
            md.AppendLine("- POST /relationships/{principalId}/{friendId} fordert Freundschaft an, PUT akzeptiert ausschliesslich eingegangene Anfragen, DELETE lehnt ab oder entfernt Freunde.");
            md.AppendLine("- PATCH /relationships/{principalId}/{friendId} aendert Rechte bestaetigter Freunde. /blocks/{friendId} sperrt bzw. entsperrt.");
            md.AppendLine("- POST /messages sendet nach bestaetigter lokaler Freundschaft eine World-IM; 202 ist keine Lesebestaetigung.");
            md.AppendLine("- GET /messages/settings und PATCH /messages/settings aktivieren opt-in Webchat (standardmaessig aus).");
            md.AppendLine("- GET /messages, GET /messages/events?after=0 und GET /messages/with/{peerId}?after=0: nur eigene Konversationen und neue IMs (5s Polling).");
            md.AppendLine("- DELETE /messages/history loescht die eigene Portal-IM-Historie, opt-out ebenfalls.");
            md.AppendLine("- Fuer Antworten aus Firestorm muss [NexPortalIM] in Robust und Simulator mit demselben HMAC-Secret aktiviert sein.");
            md.AppendLine("- GET /profiles/{principalId} liest das OpenSim-Profil einschliesslich Bildreferenzen. PATCH /profiles/{principalId} (profile:write + Owner/Admin) darf nur die elf editierbaren Spalten der userprofile-Tabelle setzen; useruuid, profileImage, profileFirstImage sind ausnahmslos schreibgeschuetzt (HTTP 403). Ein Webupload ist kein Bestandteil.");
            md.AppendLine("- GET /profiles/{principalId}/image liefert autorisiert tatsaechliche PNG/JPEG-Bilddaten der *aktuell* hinterlegten Profiltextur. Oeffentlich nur bei PublishProfile, sonst ProfileRead + eigener Avatar/Admin; kein Asset-Browser oder beliebige Texture-UUID.");
            md.AppendLine("- profile_image_url ist ein relativer Pfad und kann null sein. Keine Schluessel/Tokens in Querystrings ablegen; Browser-Fetch fuer private Profile braucht den authentifizierten Kontext.");
            md.AppendLine("- Display Name via PATCH /users/{principalId} mit display_name, sieben Tage Cooldown fuer Self-Service.");
            md.AppendLine("- Gruppenmitteilungen: GET/POST /groups/{groupId}/notices; Rechte des Gruppenmitglieds gelten auch mit Gruppen-Scopes.");
            md.AppendLine("- GET /groups/mine liefert die Gruppenmitgliedschaften und aktiven Rollen des angemeldeten Einwohners paginiert (limit max. 100), ohne frei waehbare Fremd-Avatar-ID.");
            md.AppendLine();
            md.AppendLine("### Inventar");
            md.AppendLine("- Lesen ueber /inventory/tree, /inventory/search, /inventory/folders/{folderId}, /inventory/items/{itemId} und /inventory/lost-and-found.");
            md.AppendLine("- Eigene Bearbeitung nur mit inventory:write: Ordner/Items verschieben oder umbenennen, kopieren, Links, Papierkorb und Restore.");
            md.AppendLine("- POST /inventory/items zum Erzeugen eines Items aus einem beliebigen Asset bleibt administrative Funktion.");
            md.AppendLine();
            md.AppendLine("### NV$ / Banking / Commerce");
            md.AppendLine("- Eigener Saldo: GET /economy/balance.");
            md.AppendLine("- Eigene NVBAN-Kennung: GET /economy/virtual-account.");
            md.AppendLine("- Banking-Historie, Kontoauszuege, Transfers und Zahlungsanforderungen nur im autorisierten Kontokontext.");
            md.AppendLine("- Transfers und Zahlungen nur mit economy:transfer und der dokumentierten Idempotenz.");
            md.AppendLine("- Economy-Kontosperren, Reversals und administrative Policies gehoeren in den Adminbereich.");
            md.AppendLine();
            md.AppendLine("### Weltkarte");
            md.AppendLine("- GET /world-map liefert ausschliesslich oeffentliche Regionsdaten mit begrenztem Rasterfenster.");
            md.AppendLine("- GET /world-map/view zeigt eine eigenstaendige, im Stadtportal verlink- oder einbettbare Weltkarte.");
            md.AppendLine("- MapImageService stellt optionale Kartenkacheln unter /map/map-1-{x}-{y}-objects.jpg bereit.");
            md.AppendLine("- Das Stadtportal benoetigt einen eigenen Navigationspunkt Weltkarte; Simulator-Nodes und administrative /grid/layout-Daten nicht oeffentlich machen.");
            md.AppendLine("- R409: /api/v1/deutschland und /deutschland/tiles/{zoom}/{x}/{y}.jpg liefern 256-Meter-Gridzellen (maximal 16x16 im Viewport). 32 Treffer bei Namenssuche bzw. 256 Regionen im Viewport sind Code-Grenzen; die aktuelle World-Antwort hat keine truncation-Metadaten. Keine Garantie fuer Vollstaendigkeit bei abgeschnittenen Ergebnissen behaupten.");
            md.AppendLine();
            md.AppendLine("### Suche, Land und Destinationen");
            md.AppendLine("- Oeffentlich: /search, /places, /events, /classifieds, /land-portal und /destinations.");
            md.AppendLine("- Destination-Einreichungen koennen mit discovery:submit angeboten werden; Moderation bleibt administrativ.");
            md.AppendLine("- Bugreports werden vom Stadtportal unter https://stadt-nexverse.de/api/v1/bug-reports entgegengenommen; keine gleichnamige World-API-Route als vorhanden behaupten. Der vertrauenswuerdige Simulator-Eventadapter ist laut R409 weiterhin offen.");
            md.AppendLine();
            md.AppendLine("### Gruppen und Experiences");
            md.AppendLine("- Nur anzeigen und ausfuehren, wenn der angemeldete Einwohner die vom OpenAPI-Vertrag geforderten Scopes besitzt.");
            md.AppendLine("- Ressourcenrollen und serverseitige Ownership-Pruefungen bleiben verbindlich.");
            md.AppendLine("- experiences:script ist fuer vertrauenswuerdige Script-/Service-Integrationen und nicht fuer normale Browser-Selbstbedienung.");
            md.AppendLine("- /experiences/script/info, /script/search, /script/resolve, /script/details, /script/permission, /script/location und /script/kv sind interne Service-Bridges; keinen experiences:script-Schluessel an Einwohner ausgeben.");
            md.AppendLine("- /experiences/viewer/permissions und /experiences/viewer/group sind NUR fuer den Simulator hinter kurzlebigen avatar-/regiongebundenen Viewer-CAPS. Kein direkter Aufruf mit selbst gewaehltem resident_id.");
            md.AppendLine("- /experiences/viewer/manage (POST Erwerb, PUT Profil) ist ein gesonderter Simulator-Service-Bridge-Endpoint mit experiences:viewer:manage; niemals direkt aus Einwohner-ChatGPT-OAuth aufrufen.");
            md.AppendLine("- Firestorm Experience-Kauf/Editor, Script-Zustimmung und OGL-Pathfinding nur als implementiert/aktiv behaupten, wenn der konkrete installierte Build, seine Feature-Schalter und Live-Tests dies bestaetigen.");
        }

        private static void AppendAdminSection(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## Adminbereich");
            md.AppendLine();
            md.AppendLine("Der Adminbereich darf nur mit ausdruecklich administrativ autorisiertem Principal verwendet werden. admin:* niemals aus einem normalen Einwohner-Token ableiten.");
            md.AppendLine();
            md.AppendLine("### Einwohnerverwaltung");
            md.AppendLine("- /users durchsuchen und neue Einwohner provisionieren.");
            md.AppendLine("- /users/{principalId} administrativ lesen, aktualisieren oder deaktivieren.");
            md.AppendLine("- /users/{principalId}/state, /level, /password und /audit nur nach dokumentierter Admin-Policy.");
            md.AppendLine();
            md.AppendLine("### Grid, Regionen und Simulatoren");
            md.AppendLine("- /grid/layout, /grid/cells/{x}/{y}, /grid/validate-placement.");
            md.AppendLine("- /regions, /regions/{regionId}/placement, /regions/{regionId}/lifecycle und /region-operations/{operationId}.");
            md.AppendLine("- /nodes und /nodes/{nodeId}.");
            md.AppendLine("- Start, Stop, Restart und Placement immer gegen Region-ID, Ziel-Node und aktuelle Grid-Belegung verifizieren.");
            md.AppendLine();
            md.AppendLine("### Estates");
            md.AppendLine("- /estates und /estates/{estateId} fuer Stammdaten.");
            md.AppendLine("- /estates/{estateId}/management fuer Manager-, Access-, Ban- und Gruppenlisten.");
            md.AppendLine("- Regionszuordnung ueber /estates/{estateId}/regions/{regionId}.");
            md.AppendLine();
            md.AppendLine("### Sicherheit, API-Keys und OAuth");
            md.AppendLine("- Maschinen-API-Schluessel: /auth/api-keys.");
            md.AppendLine("- OAuth-/Dienst-Clients: /auth/clients.");
            md.AppendLine("- Audit-Historie: /audit.");
            md.AppendLine("- Neue API-Keys nur mit minimal benoetigten Scopes erzeugen; aktive Schluessel nicht im MD, Chat oder URL ausgeben.");
            md.AppendLine("- NexExperiences besitzt drei strikt getrennte Dienst-Scopes: experiences:script, experiences:viewer:permissions und experiences:viewer:manage; die beiden letzteren sind privilegiert, nur fuer vertrauenswuerdige Simulatoren. Ein Manage-Scope ist nicht automatisch ein Bewohner-Identitaetsnachweis.");
            md.AppendLine("- Der neue POST/PUT /experiences/viewer/manage erfordert eigenes experiences:viewer:manage API-Key-Material; Avatar-UUID und Ownership stammen aus der gueltigen Simulator-CAP, nicht aus frei gewaehltem Nutzerinput.");
            md.AppendLine("- Eigentums- und Actor-Regeln beachten: Service-Clients mit experiences:manage duerfen keine beliebigen Bewohner-IDs impersonieren; administrative Delegation erfordert admin:*.");
            md.AppendLine("- OGLVoice, neue NavMesh-/HydroEngine-Systeme und native Viewer-Funktionen gelten ohne eigene Abnahme nicht als freigegeben, auch wenn Endpunkte dokumentiert sind.");
            md.AppendLine();
            md.AppendLine("### Economy-Administration");
            md.AppendLine("- Reversals: /economy/transactions/{transactionId}/reverse.");
            md.AppendLine("- Kontostatus: /economy/accounts/{accountId}/status.");
            md.AppendLine("- Banking-Policy: /banking/accounts/{accountId}/policy.");
            md.AppendLine("- Service-/Admin-Escrow, Refunds und Wallet-Provisionierung nur mit dokumentierter Service/Admin-Autorisierung.");
            md.AppendLine();
            md.AppendLine("### Discovery und Landverwaltung");
            md.AppendLine("- Place/Event/Classified-Verwaltung mit discovery:manage.");
            md.AppendLine("- Destination-Moderation ueber /destinations/{destinationId}/moderation.");
            md.AppendLine("- Administrative Landangebote unter /land-commerce/listings.");
            md.AppendLine("- R409: DELETE /places/{placeId} mit discovery:manage loescht auch zugeordnete Destinations, Events und Classifieds. Bei unbekannter Place-UUID kann 200 mit deleted:false folgen; nicht pauschal 404 erwarten.");
        }

        private static void AppendServiceSection(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## Dienst-/Serverintegrationen");
            md.AppendLine();
            md.AppendLine("- Maschinenintegrationen verwenden eingeschraenkte API-Schluessel oder registrierte OAuth-Service-Clients.");
            md.AppendLine("- Kein admin:*-Schluessel fuer normale Simulator- oder Portalaufgaben.");
            md.AppendLine("- R409 P0: Der Portal-Bugreport-Empfaenger ist POST https://stadt-nexverse.de/api/v1/bug-reports. Keine Service-Bearer in LSL, Browser oder World-OpenAPI platzieren; erst einen vertrauenswuerdigen Simulator-Eventhook mit serververifizierter Avatar-ID, Region und Position sowie geschuetztem HTTPS-Sender implementieren. Kein World-Ticketsystem und keine World-/api/v1/bug-reports-Route erfinden.");
            md.AppendLine("- Spezielle Experience-Schluessel: experiences:script fuer die LSL-/Metadaten-Bridge, experiences:viewer:permissions nur fuer Allow/Block/Forget/GetExperiences und Gruppenlisten, experiences:viewer:manage nur fuer authentifizierten Erwerb und Eigentuermer-Profiledits.");
            md.AppendLine("- FirestormManageCaps bleibt per Default false; fuer den Developer-Simulator benoetigt es die separaten Voraussetzungen FirestormReadCaps und FirestormPermissionCaps und einen nur fuer experiences:viewer:manage gueltigen Schluessel.");
            md.AppendLine("- Experience-Erwerb ist kostenlos (kein NV$-Debit) mit maximal zehn eigenen Experiences; nicht als unbegrenzte oder kostenpflichtige Marketplace-API darstellen.");
            md.AppendLine("- Die direkte World API /api/v1/experiences/viewer/permissions erfordert einen Service-Principal. resident_id muss aus der aktiven, widerrufbaren Avatar-CAP-Lease stammen, nicht aus Nutzertext.");
            md.AppendLine("- /api/v1/experiences/script/kv unterstuetzt create, read, update (optional check_original/original_value), delete, stats und keys; keine K/V-Autorisierung ohne zugeordnetes Script.");
            md.AppendLine("- World-API-Dienstschluessel bei fehlgeschlagenem Test nicht als HTTP-Query oder Teil einer anonymen Viewer-URL versenden.");
            md.AppendLine("- Ein OpenAPI-Eintrag ist nicht automatisch eine externe ChatGPT-App-Anbindung. Ohne angebundene API-Credentials kann der Agent nicht authentifiziert im Namen des Nutzers handeln.");
            md.AppendLine("- Service-Scopes immer funktionsbezogen und minimal halten.");
            md.AppendLine("- Service-Aktionen muessen Idempotenz-, Audit- und Korrelationsregeln einhalten.");
        }

        private static void AppendIdentityAndScope2026(StringBuilder md)
        {
            md.AppendLine("## Authentifizierung, Rollen und aktuelle Scope-Automatik");
            md.AppendLine("");
            md.AppendLine("Die World API unterscheidet strikt persoenliche Einwohnersitzungen, persoenliche Administratorsitzungen, Maschinen-API-Keys, OAuth2/OIDC-Clients sowie vertrauenswuerdige Simulator-/Viewer-CAPS. Diese Berechtigungsnachweise NIEMALS vermischen.");
            md.AppendLine("");
            md.AppendLine("### Automatische Rechte fuer beliebig viele Einwohner (PR #291)");
            md.AppendLine("- Jede NEU AUSGESTELLTE persoenliche Sitzung berechnet Berechtigungen dynamisch: Resident-Basisscopes + gespeicherte Rollen + berechtigte explizite Benutzer-Ausnahmen + Legacy-UserLevel. Auch bei 100.000 oder mehr Benutzern ist keine Einzelvergabe, SQL-Massenaktualisierung oder per-User-Migrationsschleife erforderlich.");
            md.AppendLine("- Bewohner-Basis umfasst users:read/users:write, profile:read/profile:write, relationships:read/relationships:write, groups:read/groups:manage sowie inventory:read, economy:read, regions:read, experiences:read und discovery:read. Scopes erlauben nur die durch den Endpoint geprueften eigenen/eigentumsgebundenen Operationen, nicht fremde Ressourcen.");
            md.AppendLine("- Support, Moderator und RegionManager erweitern die Basisscopes um ihre eigenen Rollenrechte. Konkrete Werte aus GET /auth/authorization-model beziehen.");
            md.AppendLine("- Administrator-Rolle ODER Legacy-UserLevel ab dem konfigurierten AdminMinimumUserLevel: admin:* und zusaetzliche explizite Fach-Lese-Scopes wie statistics:read, regions:read, simulators:read, estates:read, inventory:read, economy:read, experiences:read und discovery:read. Keine Annahme, dass beliebige Admin-Mutationsscopes separat im Token stehen muessen.");
            md.AppendLine("- Normale Bewohner erhalten automatisch WEDER admin:* NOCH security:manage, economy:transfer, inventory:write, simulators:manage, estates:manage oder andere sensitive Aktionsrechte.");
            md.AppendLine("- Aeltere bereits ausgestellte JWTs werden NICHT erweitert: nach Serverupdate, Rollen- oder Grant-Aenderung neu anmelden. Refresh allein kann nie einen nicht beantragten OAuth-Client-Scope freischalten.");
            md.AppendLine("- Persoenliche Ausnahmen werden ueber GET/PATCH /users/{principalId}/authorization verwaltet. Vor PATCH aktuellen Wert lesen, explizite Scopes/Rollen nicht blind ersetzen. expected_scopes und expected_roles sind optionale Konfliktpruefungen, 409 authorization_changed bedeutet neu laden. Der Server bestaetigt Zuweisungen erst nach Readback aus dem UserAccountService; 503 authorization_persistence_failed bedeutet KEIN gesicherter Erfolg.");
            md.AppendLine("- GET /auth/authorization-model zeigt Rollen, zulaessige Scopes und default_scopes. Control Center zeigt geerbte Scopes als automatisch; Einzelzuweisung nur fuer echte Ausnahmen. Bereits gespeicherte Eintraege sind keine Beweise fuer wirksame Scopes eines alten Tokens.");
            md.AppendLine("");
            md.AppendLine("### Nachweisarten und korrekter Einsatz");
            md.AppendLine("- Einwohner-Bearer: POST /auth/session, fuer eigene Konten, Daten, Freunde, Inventar, Gruppen, Bankdaten; interne Owner-/Group-Power-Pruefungen gelten zusaetzlich.");
            md.AppendLine("- Admin-Bearer: POST /auth/admin/session (lokaler GridUser, UserLevel >= 200, admin:*, ggf. TOTP), fuer Admin-Control-Center und autorisierte Admin-Routen. Das ist KEIN Maschinen-Key.");
            md.AppendLine("- OAuth2/OIDC: /oauth/authorize, /oauth/token, /oauth/revoke, /.well-known/openid-configuration und /auth/clients; pro Client erlaubte Scopes, PKCE S256, Redirect-URI/State, Audiences und Token-Art pruefen. Serviceclient client_credentials ist kein Einwohner.");
            md.AppendLine("- Maschinen-Key: X-NexVerse-Api-Key nur serverseitig in gesichertem Backend/Simulator, nie im Browser, Viewer-Link, LSL, Chat, URL oder Markdown speichern. Bestehende Keys koennen unter /auth/api-keys adminseitig um Scopes ergaenzt werden, ohne Secret-Rotation; die effektive Berechtigung des Bearer-Sitzungstokens aendert das nicht.");
            md.AppendLine("- Nur authentifizierte Nutzer mit dem jeweils erforderlichen Scope und passenden Ressourcenrechten duerfen eine Mutation durchfuehren. Der Wildcard-Scope admin:* ist kein Freibrief fuer Service-only-CAPS oder Cross-Avatar-Identitaeten.");
            md.AppendLine("- Die statistische Zusammenfassung /statistics/summary kann oeffentliche Zahlen liefern; geschuetzte Details wie Herkunftsdaten verlangen die entsprechende Berechtigung und eine tatsaechlich geschuetzte World-Antwort. protected_details bzw. den dokumentierten Feldstatus pruefen.");
            md.AppendLine("");
            md.AppendLine("### Grundsatz fuer API-Agenten");
            md.AppendLine("- Bei neuen Aufgaben zuerst Health, Version, Capabilities und OpenAPI vom Ziel-Grid lesen. Nur vorhandene Pfade UND tatsaechlich gestartete optionale Module verwenden.");
            md.AppendLine("- Immer GET/HEAD lesend beginnen; POST/PATCH/PUT/DELETE nur nach eindeutiger Anforderung, Payload-Pruefung, Objektzuordnung, Berechtigungspruefung und ggf. separater Bestaetigung fuer irreversible Folgen.");
            md.AppendLine("- Antworten mit 401, 403, 409, 422/400, 429, 503 und X-Correlation-Id diagnostizieren. Bei 403 keinen Alternativpfad oder einen privilegierten Maschinen-Key zum Umgehen verwenden.");
            md.AppendLine("- Niemals eine erfolgreiche Mutation, Aktualitaet von Statistiken, Vollstaendigkeit begrenzter Suchergebnisse oder vorhandene Portalfunktion behaupten, bevor der echte API-Response den Erfolg beweist.");
        }

        private static void AppendCitizenCoverage2026(StringBuilder md)
        {
            md.AppendLine("## Vollstaendiger Buergerportal-Funktionsumfang – praxisorientiert");
            md.AppendLine("");
            md.AppendLine("### Identitaet, Konto, Selbstverwaltung");
            md.AppendLine("- Native Anmeldung POST /auth/session; eigenes Konto GET /users/me, GET/PATCH /users/{principalId} mit Self-Pruefung. Account-Security-Stamp und Token-Neuanmeldung bei Rollenaenderungen oder Revocation beachten.");
            md.AppendLine("- Eigene Passwortaenderung POST /users/{principalId}/password nur mit der vom Vertrag geforderten Authentifizierung. Eigenen Account loeschen: POST /users/me/delete mit Passwort-Neupruefung und ausdruecklicher Bestaetigung; NIE automatisieren.");
            md.AppendLine("- Display Name mit dokumentiertem 7-Tage-Cooldown per PATCH /users/{principalId}; andere Benutzerfelder und Sichtbarkeit nach API-Response, keine freie Client-Role- oder UserLevel-Aenderung.");
            md.AppendLine("- Registrierung und Namenspruefung fuer das Stadtportal erfolgt nur ueber das GESCHUETZTE Backend mit users:register-Maschinenkey unter /portal/avatars/register und /portal/avatars/name-availability; ein Browser/ChatGPT-Einwohner darf den Key nicht besitzen.");
            md.AppendLine("");
            md.AppendLine("### Avatarprofil, soziale Funktionen und Instant Messages");
            md.AppendLine("- Avatarprofil GET /profiles/{principalId} (oeffentlich je PublishProfile/Privatsphaere); PATCH /profiles/{principalId} nur fuer den eigenen Avatar bzw. legitimen Admin, exakt die erlaubten editierbaren Felder. useruuid und die aktiven profileImage/ProfileFirstImage-Assetreferenzen sind nicht frei schreibbar; kein generischer Bilderupload ueber World API.");
            md.AppendLine("- GET /profiles/{principalId}/image liefert ausschliesslich das aktuell konfigurierte Profilbild, privat nur mit Owner-/Admin-Berechtigung. Profilbild-URL kann fehlen; Querystrings nie mit Secrets versehen.");
            md.AppendLine("- GET /relationships/{principalId}: eigene Freunde und Anfragen; POST/PUT/PATCH/DELETE /relationships/{principalId}/{targetId}: einladen, bestaetigen, Rechte veraendern, ablehnen oder entfernen. /blocks/{targetId} fuer persoenliches Blockieren/Entsperren.");
            md.AppendLine("- Nachrichten: POST /messages nur an einen bestaetigten lokalen Freund, 202 bedeutet angenommen, nicht gelesen. GET /messages/settings und PATCH /messages/settings fuer Opt-in; GET /messages, /messages/events und /messages/with/{peerId} fuer eigenen Verlauf; DELETE /messages/history nur nach klarer Absicht. Ohne NexPortalIM-Modul ist eine Viewer-Antwort nicht garantiert.");
            md.AppendLine("");
            md.AppendLine("### Gruppen, Rollen und Gruppenwirtschaft");
            md.AppendLine("- Gruppen finden/anlegen: GET/POST /groups; eigene Mitgliedschaften GET /groups/mine (keine frei vorgegebene fremde Avatar-ID). GET/PUT /groups/{groupId}, Mitglieder unter /members und /members/{agentId}, Rollen und Powers unter /roles sowie /roles/{roleId}/members/{agentId}.");
            md.AppendLine("- Gruppeneinladungen: /groups/{groupId}/invites und /invites/{inviteId}; Notices /notices; Ban-Liste /bans und /bans/{agentId}; Gruppenkonto /accounting; technische Gruppen-Faehigkeiten /capabilities.");
            md.AppendLine("- groups:manage als allgemeiner Basis-Scope ersetzt NICHT die erforderlichen gruppenspezifischen Powers, Actor-/Owner-Zuordnung, Mitgliedschafts- und Rollenpruefungen. Keine Verwaltung fremder Gruppen allein aus einer Scope-Liste folgern.");
            md.AppendLine("");
            md.AppendLine("### Inventar, Outfits und Assets");
            md.AppendLine("- Inventarstruktur GET /inventory/tree, /inventory/search, /inventory/folders/{folderId}, /inventory/items/{itemId}, /inventory/lost-and-found und /inventory/outfits/health. Nur eigene Items/Folder anzeigen; Owner-Scope pruefen.");
            md.AppendLine("- Aenderungen mit explizitem inventory:write: /inventory/folders, /folders/{folderId}, /items/{itemId}, /items/copy, /links, /trash/empty und /{folderId|itemId}/restore. Bei definitiver Papierkorbleerung Folgen bestaetigen.");
            md.AppendLine("- /inventory/outfits/ensure-folders kann fehlende Systemordner gezielt herstellen; Gesundheitsdiagnose lesen bevor schreibend repariert wird.");
            md.AppendLine("- POST /inventory/items aus einer frei gewaehlten Asset-ID erfordert admin:* und ist keine beliebige Bewohner-Upload-/Copybot-Funktion. Kein umfassender World-Assetbrowser, Upload von PBR/Mesh/Animesh oder erzwungener Rechteumgehung zugesichert.");
            md.AppendLine("");
            md.AppendLine("### NV$-Economy, Banking, Commerce, Land");
            md.AppendLine("- GET /economy/balance und /economy/virtual-account lesen eigenen Kontostand und NVBAN. GET /economy/transactions/{transactionId} nur fuer autorisierte Beteiligte; fuer andere Konten gilt Eigentum/Admin-Pruefung.");
            md.AppendLine("- Banking /banking/transactions, /statements und /reconciliation mit eigener Kontozuordnung; /banking/payment-requests (GET/POST), /{requestId}/pay oder /cancel; /banking/accounts/{accountId}/policy (lesen fuer das eigene Konto, Verwaltung extra eingeschraenkt).");
            md.AppendLine("- Transfers POST /economy/transfers oder /banking/transfers sind KEINE Standard-Bewohnerberechtigung: economy:transfer, ggf. Limits, Gebuehren, Empfaengerpruefung und Idempotency-Key erforderlich. Keine Geldbewegung aus reiner Leseanfrage ableiten.");
            md.AppendLine("- Escrow /banking/escrow/{escrowId}/fund und /release nur fuer erlaubte Zahler/Dienste/Admins; Release ist nicht automatisch eine frei verfuegbare Einwohneroperation.");
            md.AppendLine("- NexCommerce /commerce/orders und /orders/{orderId}: eigene Kauf-/Bestellhistorie und genehmigte Bestellungen; /refund ist privilegiert. Land-Commerce /land-commerce/listings, /listings/{listingId}/purchase oder /lease, /leases und /leases/{leaseId}/pay: Angebots-/Mietablauf, realer Scope economy:transfer und Eignerpruefung bleiben Pflicht.");
            md.AppendLine("- Landportal-Lesemodul /land-portal zeigt aktive Angebote; /land-portal/owned nur eigene bzw. berechtigt sichtbare Angebote. Kauf, Miete, Zahlung und Storno jeweils getrennt behandeln, Preise und Eigentum aus Live-Daten lesen.");
            md.AppendLine("");
            md.AppendLine("### Welt, Warp3D-Karten, Suche, Events");
            md.AppendLine("- Oeffentliche Karte /world-map und /world-map/view; Grid-spezifische Deutschland/Warp3D-Ansichten /deutschland, /deutschland/view, /deutschland/tiles/{zoom}/{x}/{y}.jpg und /deutschland/xyz/{z}/{x}/{y}.jpg nur wenn beim Betreiber installiert. Region-/VarRegion-Raster korrekt interpretieren; fehlende Karte ist kein freies Land.");
            md.AppendLine("- Volltextsuche /search umfasst je indexierter Quelle Personen, Gruppen, Regionen, Parzellen, Places, Events, Classifieds, Experiences und Destinationen. Pagination/Ergebnislimits beachten: ein begrenztes Resultat ist keine vollstaendige Weltliste.");
            md.AppendLine("- /places, /events und /classifieds sowie jeweilige Detail-GETs: nur veroeffentlichte Inhalte oeffentlich, Entwuerfe und Eigentum geschuetzt. Schreibende Besitzer-/Moderationsoperationen mit discovery:manage erst nach genauer Scope-/Owner- und Feldpruefung.");
            md.AppendLine("- /destinations, /destinations/categories, /destinations/{destinationId} und /destinations/{destinationId}/visit fuer Ziele, Kategorien, Details und Popularitaetszaehlung. Destination-Einreichung discovery:submit; Freigabe/Ablehnung via /moderation administrativ.");
            md.AppendLine("- OGL-World-API bietet laut aktuellem Vertrag NICHT automatisch eine World-Bugreport-/Ticketroute. Eine separate Stadtportal-API kann existieren, ist aber weder OGL-Weltserver noch durch diese Anweisung implementiert.");
            md.AppendLine("");
            md.AppendLine("### Experiences – Benutzerseite und Simulatorgrenzen");
            md.AppendLine("- GET /experiences und /experiences/{experienceId} fuer berechtigte Experience-Sicht; Logs und Verwaltungs-Aktionen nur nach spezifischen Rollen/Scopes (experiences:read/experiences:manage). Besitzer-/Contributor-Pruefung gilt neben dem Scope.");
            md.AppendLine("- Erstellen/Bearbeiten und Rollenzuweisung unter /experiences/{experienceId}, /roles/{role}/{residentId}, /residents/{residentId}, /policies/{kind}/{locationId} und /scripts/{scriptId}/bind nur mit ausdruecklich genehmigter Experience-Verwaltung.");
            md.AppendLine("- Simulator-/Firestorm-Routen /experiences/viewer/* und LSL-/Script-Routen /experiences/script/* sind KEINE Browser-/Bewohner-Aktionen. Simulator leitet die aktuelle Avatar-/Region-/Script-Bindung vertrauenswuerdig her. Eine selbst gewaehlte resident_id ersetzt keine Autorisierung.");
        }

        private static void AppendAdminCoverage2026(StringBuilder md)
        {
            md.AppendLine("## Vollstaendige Administrations-Funktionskarte – Betrieb und Governance");
            md.AppendLine("");
            md.AppendLine("### Admin-Sitzung und Kontoverwaltung");
            md.AppendLine("- Kontrollierter Login POST /auth/admin/session: lokaler GridUser mit UserLevel >= 200 und admin:*, optional TOTP. GET /auth/admin/session zeigt den tatsaechlich aktiven Status; Control Center /docs ist kein alternativer Superuser-Bypass.");
            md.AppendLine("- GET /users (Suche) und POST /users (Provisionierung), GET/PATCH /users/{principalId}, PATCH /users/{principalId}/state fuer Sperren/Bannen/Deaktivieren/Reaktivieren, PATCH /level fuer UserLevel, POST /password zum Reset, GET /audit fuer Account-Historie.");
            md.AppendLine("- Das Loeschen durch einen Einwohner erfolgt ueber /users/me/delete mit Passwort und Bestaetigung; Admin-Aktionen koennen getrennte Policies haben. Keine globale Massenloeschung, keine Passwortausgabe oder Tokens aus Fremdkonten.");
            md.AppendLine("- Lesen oder aendern von Rollen und expliziten Scopes: /auth/authorization-model sowie GET/PATCH /users/{principalId}/authorization mit security:manage; bei Administratorziel zusaetzlich admin:* und aktuelle Rolle beachten.");
            md.AppendLine("- Seit PR #291: Resident-Basis, Support/Moderator/Regionsmanager und Admin-Lesegrants werden fuer alle vorhandenen/neuen Konten automatisch beim NEUEN Login aus der Rollenpolicy berechnet. Nicht 100.000 Benutzer per Hand patchen. Individuelle Ausnahmen bleiben persistiert; der Server prueft den gespeicherten Stand nach PATCH und meldet im Fehlerfall 503 authorization_persistence_failed.");
            md.AppendLine("- Role-Wildcards und Owner-/Audience-Regeln sind kein Ersatz fuer ausdrueckliche Freigabe einer Mutation. Jede Sperre, UserLevel-Erhoehung, Rollen-/Passwortaenderung und Accountentfernung vorher gezielt bestaetigen.");
            md.AppendLine("");
            md.AppendLine("### Maschinen-Keys, OAuth/OIDC, Audit und Schutzniveau");
            md.AppendLine("- GET/POST/PATCH /auth/api-keys: Key-Inventar und erlaubte Maschinen-Scopes, neu erstellen, bestehende Scopes per PATCH mit expected_scopes ohne neue Secret-Ausgabe aktualisieren, aktivieren/deaktivieren. DELETE /auth/api-keys/{keyId} nur fuer vorher deaktivierte Keys. Niemals Admin-Maschinenkeys als normalen Login verwenden.");
            md.AppendLine("- GET/POST/PATCH /auth/clients: OAuth-Clients mit begrenzten Scopes/Typen und genau registrierten Redirect-URIs konfigurieren; OAuth-Flows ueber /oauth/authorize, /oauth/token und /oauth/revoke. Kein ausgedachtes Stadtportal-Callback oder Default-Clientsecret.");
            md.AppendLine("- POST /auth/sessions/revoke widerruft berechtigte User-Sessions; GET /audit liefert persistente administrative Audit-Ereignisse. X-Correlation-Id mit Berichts-/Fehlerdatum verbinden, sensible Rohdaten redigieren.");
            md.AppendLine("- Secret-Pflege nur in geschuetzten Konfigurationen und Secret Stores. Geaenderte Scopes eines Maschinen-API-Keys erscheinen NICHT in persoenlichen Bearer-Sessions. Eigenen persoenlichen Admin-Token nach Rollenupdate neu anmelden.");
            md.AppendLine("");
            md.AppendLine("### Grid-Kapazitaet, Regionen, Simulator-Nodes und Estates");
            md.AppendLine("- GET /grid/layout, /grid/cells/{x}/{y} und /grid/validate-placement: belegte Rasterzellen, Koordinaten, groessere VAR-Footprints und Konflikte pruefen, bevor Regionen erstellt/verschoben werden.");
            md.AppendLine("- GET/POST /regions, GET /regions/{regionId}, PATCH /regions/{regionId}/placement und POST /regions/{regionId}/lifecycle fuer verwaltete Regionen. Asynchronen Status ueber GET /region-operations/{operationId} bis zum endgueltigen Abschluss verfolgen; 202 ist nur angenommen, nicht gestartet/erfolgreich.");
            md.AppendLine("- GET /nodes, /nodes/{nodeId}: NodeAgent-Status und zugeordnete Simulator-/Regionsinformationen. /feature-control/nodes und /nodes/{nodeId} unterhalb dieses Pfades sind optionale, signierte Node-Funktionsfreigaben; fehlendes OGLFeatureControl-Modul nicht mit Lizenzumgehung 'reparieren'.");
            md.AppendLine("- GET/POST /estates, GET/PATCH /estates/{estateId}, GET /estates/{estateId}/management und PUT /estates/{estateId}/regions/{regionId}: Estate-Zuordnung, Manager-/Allow-/Ban-Listen, Gruppen/Owner sowie Richtlinien. Region-/Estate-Mutationen nur mit korrekten Scopes und Objektstatus.");
            md.AppendLine("- Regionen/Simulatoren moeglicherweise auf mehreren Hosts: Live-Zustaende pruefen, zwischen Node-Registrierung, Regionsregion und Gridplatzierung unterscheiden; niemals per blindem PATCH eine belegte Zelle ueberschreiben.");
            md.AppendLine("");
            md.AppendLine("### Betriebsueberwachung, Statistiken und Diagnose");
            md.AppendLine("- GET /health, /version, /capabilities und /openapi.json sind erste Abfragen; /statistics/summary liefert installierte Statistikfelder, geschuetzte Hypergrid-Herkunfts-/Detaildaten nur unter berechtigtem personalem statistics:read bzw. Admin-Kontext.");
            md.AppendLine("- Korrelation, optionales Feature-Control, laufende Node-/Region-Operationen und Domain-/TLS-Proxy-Zustand getrennt ueberpruefen; ein dokumentiertes Modul ist kein Nachweis, dass es auf dieser ROBUST-Instanz aktiv ist.");
            md.AppendLine("- Fuer Fehler keine Workaround-Mutationen ohne Ursache und keine Datenbank-/Asset-Backfills ohne Sicherung, Test, Rollback und Genehmigung vorschlagen. Es gibt nicht automatisch ein World-API-Endpunkt fuer jede Server-Console-Funktion.");
            md.AppendLine("");
            md.AppendLine("### NV$ Ledger, Banken, Marktplatz und Landkauf");
            md.AppendLine("- GET /economy/balance, /economy/transactions/{transactionId}, /banking/transactions, /statements, /reconciliation fuer genehmigte eigene/Fremdkonten je Admin-/Owner-Policy.");
            md.AppendLine("- POST /economy/transactions/{transactionId}/reverse erzeugt Ledger-Gegenbuchung und muss begruendet werden; POST /economy/accounts/{accountId}/status sperrt/freigibt/schliesst NV$-Konten. Keine direkten DB-Ledger-Schreibvorgaenge oder historisches Loeschen.");
            md.AppendLine("- GET/PUT /banking/accounts/{accountId}/policy veraendert Transferlimits und Gebuehren mit dokumentierter Adminberechtigung; /commerce/orders/{orderId}/refund kann Service-/Admin-Rechte verlangen.");
            md.AppendLine("- Service-Wallets unter /economy/accounts/ensure sowie HG-Wallets sind vertrauenswuerdigen Server-/Admin-Flows vorbehalten, nicht einem frei gesetzten Bewohner-Token. Jede Zahlungs-, Miet-, Kauf-, Escrow- und Rueckabwicklungsaktion mit richtigen Rollen, Zielkonto und Idempotenz behandeln.");
            md.AppendLine("- /land-commerce/listings (Angebote anlegen) und /listings/{listingId}/deactivate (deaktivieren) sind Verwaltungsaufgaben; Bewohner kaufen/mieten nur auf den vorgesehenen Workflows. Vor Sale/Lease Preise, Verfuegbarkeit, Provisionierung, Gebuehren und Owner pruefen.");
            md.AppendLine("");
            md.AppendLine("### Discovery, Gruppen, Community und Experiences");
            md.AppendLine("- /places, /events, /classifieds (POST/PUT/PATCH/DELETE): Publikation/Entwurf/Owner und erlaubte Eingabefelder strikt trennen. /places/{placeId} DELETE kann abhaengige Discovery-Eintraege entfernen: vorher Auswirkungen beurteilen.");
            md.AppendLine("- Destinationen /destinations und /destinations/{destinationId}/moderation: Einreichung nicht automatisch veroeffentlichen, Moderation ausdruecklich und auditiert.");
            md.AppendLine("- Gruppenadministration unter /groups, /members, /roles, /invites, /notices, /bans, /accounting und /capabilities: Immer spezifische Gruppen-Powers und Actor-Identitaet pruefen. groups:manage allein garantiert NICHT globale Kontrolle ueber fremde Gruppen.");
            md.AppendLine("- Experiences: /experiences (CRUD), /{experienceId}/logs, /roles/{role}/{residentId}, /residents/{residentId}, /policies/{kind}/{locationId} und /scripts/{scriptId}/bind. Admin-/Owner-/Contributor-/Estate-/Parcel-Policies und Aktivierungsstatus kontrollieren.");
            md.AppendLine("- Firestorm-/Viewer-/Script-Bridge nur ueber vertrauenswuerdige Simulator-/Service-Identitaeten, nicht per admin:* aus dem Browser nachahmen. Ein API-Agent darf keine fremde Avatar-CAP minten oder Script-Rechte ohne Scriptbindung vergeben.");
            md.AppendLine("");
            md.AppendLine("### Automatisierte sichere Admin-Workflows");
            md.AppendLine("- READ: betroffene Ressourcen und Versionen mit den GET-Routen pruefen, dazu Rechte und Audience aus OpenAPI; noetige Owner-/Gruppen-/Estate-IDs gegen aktuelle Live-Objekte abgleichen.");
            md.AppendLine("- PLAN: bevor Zustandswechsel gespeichert werden, exakte Wirkung, Betroffene, Konflikte, Recovery/Rollback und Genehmigung benennen; keine 'Autofixes' fuer echte Geldbuchungen, Ban-Listen oder Kontoloeschungen.");
            md.AppendLine("- WRITE: kleinstmoeglichen dokumentierten PATCH/POST/PUT mit Idempotenz-/Vorbedingungsfeldern senden und X-Correlation-Id sichern; bei 409 neu laden statt blind erneut senden.");
            md.AppendLine("- VERIFY: mutierte Ressource anschliessend mit GET verifizieren, bei Lifecycle-Aktionen /region-operations/{operationId} abfragen; bei Scope-Aenderungen neuen Login pruefen. Keine Aktionen als abgeschlossen deklarieren, solange die World API sie nicht bestaetigt.");
        }

        private static void AppendServiceBoundaries2026(StringBuilder md)
        {
            md.AppendLine("## Strikte Dienst-, Viewer- und Portal-Backend-Grenzen");
            md.AppendLine("");
            md.AppendLine("- Backend-Avatarregistrierung /portal/avatars/register und Namenspruefung /portal/avatars/name-availability erfordern users:register-Maschinenkey; niemals als ungeschuetzte Browser-/LSL-Route exposen.");
            md.AppendLine("- /experiences/script/search, /info, /resolve, /details, /permission, /location und /kv sind experiences:script-Service-Funktionen fuer authentisierte LSL-/Simulatorintegration; Script-Owner und bindende Experience pruefen.");
            md.AppendLine("- /experiences/viewer/permissions, /viewer/group, /viewer/manage und /viewer/script-binding verlangen passende separate Maschinen-Scopes/Simulator-CAP und gebundene Avatar-/Task-Identitaet. Firestorm-CAPS und deren Einstellungen koennen optional deaktiviert sein.");
            md.AppendLine("- /economy/accounts/ensure ist Service-/Admin-Wallet-Provisionierung; keine direkte Einwohnerkonten-Erstellung durch eine fremde Avatar-ID. Hypergrid-Identity- und Assetdienste sind separat.");
            md.AppendLine("- Selbst ein Administrator mit admin:* darf einen service-only Header oder eine kurzlebige Viewer-CAP nicht durch frei erfundene Actor-/resident_id-/region_id-Werte ersetzen.");
            md.AppendLine("- Bei Fehlern einer Service-Bridge den passenden Simulator/Host, Service-Key-Geltungsbereich, IP/TLS, Gate-Status und Correlation-ID pruefen; keine Freischaltung per anonymer Rueckfallroute.");
            md.AppendLine("- OpenGenesisLINK/OGL ist die plattformunabhaengige Server-Software; betreiberspezifische Stadtportal-Webhooks, WhatsApp, externes Ticketsystem, Mietsystem, OGLVoice und andere Addons nur nennen, wenn das Ziel-Grid deren Modul und Route nachweisbar installiert hat.");
        }

        private static void AppendRequestPlaybooks2026(StringBuilder md)
        {
            md.AppendLine("## Praxisablauf fuer jede ChatGPT-Aufgabe");
            md.AppendLine("");
            md.AppendLine("1. Ziel benennen: OGL Grid, API-Basis, Laufzeitversion, Benutzerabsicht, Portalbereich und Authentifizierungsnachweis – nie eine feste Betreiber-Domain erzwingen.");
            md.AppendLine("2. Erst GET /api/v1/health, /version und /capabilities; danach /api/v1/openapi.json als technische Quelle. Moegliche optionale Module und Scope-/Audience-Metadaten der konkret gewaehlten HTTP-Methode bestaetigen.");
            md.AppendLine("3. Oeffentliche GETs ohne Session; private Einwohnerdaten mit persoenlichem Bearer; Adminaenderungen nur mit geeignetem Admin-Bearer; Service-Bridges nur mit getrenntem Maschinen-Key im autorisierten Backend.");
            md.AppendLine("4. Bei Menschenkonten Rollen-/Scope-Automatik respektieren: Resident-/Admin-Leserechte werden beim neuen Login aus Rollen abgeleitet. Ausnahmen nur ueber dokumentierte individuelle Benutzer-Autorisierung verwalten, bestehende Rolle/Scopes bewahren.");
            md.AppendLine("5. Aenderungsoperationen: erst lesend validieren, Objektbesitz/Actor und Rechte checken, exakte Felder aus dem OpenAPI-Schema, dann gezielt ausfuehren. Bei irreversiblem Eingriff vorher ausdrueckliche Bestaetigung.");
            md.AppendLine("6. Wenn Idempotency-Key gefordert ist, ihn fuer denselben fachlichen Vorgang stabil wiederverwenden und fuer andere Vorgaenge neu erzeugen; keine Doppelzahlung/Doppelbuchung durch automatischen Retry.");
            md.AppendLine("7. API-Ergebnisse niemals aus einem HTTP-200 allein ableiten: Response-Koerper, Status, korrelierte Fehler, asynchrone Job-Stati und fehlende/geschuetzte Detailfelder auswerten.");
            md.AppendLine("8. 401/403: Session-/Scope-/Audience-/Ownerproblem; 409: konkurrierende Aenderung, neu lesen; 429: mit Backoff; 503: Modul/Persistenz/Abhaengigkeit pruefen, nicht fail-open. Vor Mutationen/Retry Sicherheitsgrenzen beachten.");
            md.AppendLine("9. Ergebnisse getrennt kennzeichnen: Code/Vertrag vorhanden, am Ziel-Grid aktiviert, echter HTTP-Test bestanden, Mutation ausgefuehrt, persistenter Zustand nachgewiesen, Portal korrekt angezeigt.");
            md.AppendLine("10. Vertrauliche Daten minimieren: Tokens, User-Passwoerter, TOTP, Key-Hashes, Auth-Store-Inhalte, Inventar-/IM-Privatdaten und Kontobewegungen nie in oeffentliche Chatprotokolle oder generierte Reports kopieren.");
            md.AppendLine("");
            md.AppendLine("### Was diese Anleitung NICHT garantiert");
            md.AppendLine("- Keine Generalvollmacht fuer unbekannte Endpunkte, ohne vertrauenswuerdige Credentials oder Berechtigungen.");
            md.AppendLine("- Keine Kopie eines kompletten OpenSim-Viewer- oder ROBUST-Administrationsprotokolls, kein beliebiger Asset-Download und keine Rechteumgehung.");
            md.AppendLine("- Keine betrieberspezifischen Stadtportal- oder Marketplace-Funktionen als Teil des OGL World-API-Vertrags fingieren. Portal-Backend-Route kann von World API abweichen.");
            md.AppendLine("- Keine Live-Freigabe oder Funktionstest ausserhalb der im Ziel-Grid tatsaechlich gestarteten Version. Ein OpenAPI-Pfad kann ohne Feature-Flag/Service-Initialisierung noch nicht nutzbar sein.");
        }

        private void AppendCompleteRouteIndex(StringBuilder md, string requestedAudience)
        {
            md.AppendLine();
            md.AppendLine("## Vollstaendiges Pfadverzeichnis der deklarierten OpenAPI-World-API (Stand dieses Builds)");
            md.AppendLine();
            md.AppendLine("Diese Pfade entsprechen dem dokumentierten Server-OpenAPI-Vertrag. Alle Pfade sind relativ zu " + m_PublicBaseUrl + "; /oauth/* hat KEIN /api/v1-Praefix. Die Zielgruppen-Einordnung ist nur Orientierung und kein Zugangsrecht.");
            md.AppendLine("Fuer JEDE konkrete HTTP-Methode und Ressource den aktuellen OpenAPI-Vertrag " + m_ApiBaseUrl + "/openapi.json pruefen. Gemischt bedeutet: oeffentliche/Einwohner-GETs und privilegierte Schreib- oder Owner-Operationen koennen denselben Pfad teilen.");
            md.AppendLine("Nur im Dokument definierte Methoden verwenden. Optionale Features und betrieberspezifische Webendpunkte sind nicht allein durch eine Pfaddeclaration live.");
            md.AppendLine();
            AppendCatalogGroup(md, requestedAudience, "public", "capabilities",
                "/api/v1/capabilities");
            AppendCatalogGroup(md, requestedAudience, "public", "chatgpt-admin.md",
                "/api/v1/chatgpt-admin.md");
            AppendCatalogGroup(md, requestedAudience, "public", "chatgpt-buergerportal.md",
                "/api/v1/chatgpt-buergerportal.md");
            AppendCatalogGroup(md, requestedAudience, "public", "chatgpt-instructions.md",
                "/api/v1/chatgpt-instructions.md");
            AppendCatalogGroup(md, requestedAudience, "public", "Destination-Guide und Moderation",
                "/api/v1/destinations/categories",
                "/api/v1/destinations/{destinationId}",
                "/api/v1/destinations/{destinationId}/visit");
            AppendCatalogGroup(md, requestedAudience, "public", "Betreiber-Warp3D/MapLibre-Karte",
                "/api/v1/deutschland",
                "/api/v1/deutschland/view",
                "/api/v1/deutschland/tiles/{zoom}/{x}/{y}.jpg",
                "/api/v1/deutschland/xyz/{z}/{x}/{y}.jpg");
            AppendCatalogGroup(md, requestedAudience, "public", "docs",
                "/api/v1/docs");
            AppendCatalogGroup(md, requestedAudience, "public", "health",
                "/api/v1/health");
            AppendCatalogGroup(md, requestedAudience, "public", "Landportal",
                "/api/v1/land-portal");
            AppendCatalogGroup(md, requestedAudience, "public", "openapi.json",
                "/api/v1/openapi.json");
            AppendCatalogGroup(md, requestedAudience, "public", "API-Basis, Health, Version und MD-Anleitungen",
                "/api/v1");
            AppendCatalogGroup(md, requestedAudience, "public", "Indexuebergreifende Suche",
                "/api/v1/search");
            AppendCatalogGroup(md, requestedAudience, "public", "version",
                "/api/v1/version");
            AppendCatalogGroup(md, requestedAudience, "public", "Oeffentliche Weltkarte",
                "/api/v1/world-map",
                "/api/v1/world-map/view");
            AppendCatalogGroup(md, requestedAudience, "citizen", "Login, Token, Maschinenkeys und Scope-/OAuth-Verwaltung",
                "/api/v1/auth/session",
                "/api/v1/auth/sessions/revoke");
            AppendCatalogGroup(md, requestedAudience, "citizen", "Inventar, Outfit, Kopien und Papierkorb",
                "/api/v1/inventory/tree",
                "/api/v1/inventory/search",
                "/api/v1/inventory/outfits/health",
                "/api/v1/inventory/outfits/ensure-folders",
                "/api/v1/inventory/folders",
                "/api/v1/inventory/folders/{folderId}",
                "/api/v1/inventory/items/{itemId}",
                "/api/v1/inventory/items/copy",
                "/api/v1/inventory/folders/{folderId}/restore",
                "/api/v1/inventory/items/{itemId}/restore",
                "/api/v1/inventory/links",
                "/api/v1/inventory/lost-and-found",
                "/api/v1/inventory/trash/empty");
            AppendCatalogGroup(md, requestedAudience, "citizen", "Inworld-IM/Webchat",
                "/api/v1/messages",
                "/api/v1/messages/settings",
                "/api/v1/messages/events",
                "/api/v1/messages/with/{peerId}",
                "/api/v1/messages/history");
            AppendCatalogGroup(md, requestedAudience, "citizen", "OAuth2/OIDC-Flows",
                "/oauth/authorize",
                "/oauth/token",
                "/oauth/revoke");
            AppendCatalogGroup(md, requestedAudience, "citizen", "Freunde und Blockierungen",
                "/api/v1/relationships/{principalId}",
                "/api/v1/relationships/{principalId}/{targetId}",
                "/api/v1/relationships/{principalId}/blocks/{targetId}");
            AppendCatalogGroup(md, requestedAudience, "citizen", "Bewohnerkonten, Rollen und Autorisierung",
                "/api/v1/users/me",
                "/api/v1/users/me/delete");
            AppendCatalogGroup(md, requestedAudience, "shared", "Banking, Zahlungsanforderungen und Escrow",
                "/api/v1/banking/transactions",
                "/api/v1/banking/statements",
                "/api/v1/banking/reconciliation",
                "/api/v1/banking/transfers",
                "/api/v1/banking/payment-requests",
                "/api/v1/banking/payment-requests/{requestId}/pay",
                "/api/v1/banking/payment-requests/{requestId}/cancel",
                "/api/v1/banking/accounts/{accountId}/policy",
                "/api/v1/banking/escrow/{escrowId}/fund",
                "/api/v1/banking/escrow/{escrowId}/release");
            AppendCatalogGroup(md, requestedAudience, "shared", "Kleinanzeigen",
                "/api/v1/classifieds",
                "/api/v1/classifieds/{classifiedId}");
            AppendCatalogGroup(md, requestedAudience, "shared", "NexCommerce-Kaufabwicklung",
                "/api/v1/commerce/orders",
                "/api/v1/commerce/orders/{orderId}",
                "/api/v1/commerce/orders/{orderId}/refund");
            AppendCatalogGroup(md, requestedAudience, "shared", "Destination-Guide und Moderation",
                "/api/v1/destinations");
            AppendCatalogGroup(md, requestedAudience, "shared", "NV$-Ledger, Buchungen und Konten",
                "/api/v1/economy/balance",
                "/api/v1/economy/virtual-account",
                "/api/v1/economy/transfers",
                "/api/v1/economy/transactions/{transactionId}");
            AppendCatalogGroup(md, requestedAudience, "shared", "Veranstaltungen",
                "/api/v1/events",
                "/api/v1/events/{eventId}");
            AppendCatalogGroup(md, requestedAudience, "shared", "Experiences und Script-/Viewer-Bridges",
                "/api/v1/experiences",
                "/api/v1/experiences/{experienceId}",
                "/api/v1/experiences/{experienceId}/logs",
                "/api/v1/experiences/{experienceId}/roles/{role}/{residentId}",
                "/api/v1/experiences/{experienceId}/residents/{residentId}",
                "/api/v1/experiences/{experienceId}/policies/{kind}/{locationId}",
                "/api/v1/experiences/{experienceId}/scripts/{scriptId}/bind");
            AppendCatalogGroup(md, requestedAudience, "shared", "Gruppen, Mitgliedschaften, Rollen, Notices, Einladungen, Bans",
                "/api/v1/groups",
                "/api/v1/groups/mine",
                "/api/v1/groups/{groupId}",
                "/api/v1/groups/{groupId}/members",
                "/api/v1/groups/{groupId}/members/{agentId}",
                "/api/v1/groups/{groupId}/roles",
                "/api/v1/groups/{groupId}/roles/{roleId}",
                "/api/v1/groups/{groupId}/roles/{roleId}/members/{agentId}",
                "/api/v1/groups/{groupId}/invites",
                "/api/v1/groups/{groupId}/invites/{inviteId}",
                "/api/v1/groups/{groupId}/notices",
                "/api/v1/groups/{groupId}/bans",
                "/api/v1/groups/{groupId}/bans/{agentId}",
                "/api/v1/groups/{groupId}/accounting",
                "/api/v1/groups/{groupId}/capabilities");
            AppendCatalogGroup(md, requestedAudience, "shared", "Landangebote, Kaeufe und Mieten",
                "/api/v1/land-commerce/listings",
                "/api/v1/land-commerce/listings/{listingId}/deactivate",
                "/api/v1/land-commerce/listings/{listingId}/purchase",
                "/api/v1/land-commerce/listings/{listingId}/lease",
                "/api/v1/land-commerce/leases",
                "/api/v1/land-commerce/leases/{leaseId}/pay");
            AppendCatalogGroup(md, requestedAudience, "shared", "Landportal",
                "/api/v1/land-portal/owned");
            AppendCatalogGroup(md, requestedAudience, "shared", "Places",
                "/api/v1/places",
                "/api/v1/places/{placeId}");
            AppendCatalogGroup(md, requestedAudience, "shared", "Avatarprofile und Bildbytes",
                "/api/v1/profiles/{principalId}",
                "/api/v1/profiles/{principalId}/image");
            AppendCatalogGroup(md, requestedAudience, "shared", "Regionen",
                "/api/v1/regions",
                "/api/v1/regions/{regionId}");
            AppendCatalogGroup(md, requestedAudience, "shared", "Statistik und geschuetzte Auswertung",
                "/api/v1/statistics/summary");
            AppendCatalogGroup(md, requestedAudience, "shared", "Bewohnerkonten, Rollen und Autorisierung",
                "/api/v1/users/{principalId}",
                "/api/v1/users/{principalId}/password");
            AppendCatalogGroup(md, requestedAudience, "admin", "Audit",
                "/api/v1/audit");
            AppendCatalogGroup(md, requestedAudience, "admin", "Login, Token, Maschinenkeys und Scope-/OAuth-Verwaltung",
                "/api/v1/auth/admin/session",
                "/api/v1/auth/api-keys",
                "/api/v1/auth/api-keys/{keyId}",
                "/api/v1/auth/authorization-model",
                "/api/v1/auth/clients");
            AppendCatalogGroup(md, requestedAudience, "admin", "Destination-Guide und Moderation",
                "/api/v1/destinations/{destinationId}/moderation");
            AppendCatalogGroup(md, requestedAudience, "admin", "NV$-Ledger, Buchungen und Konten",
                "/api/v1/economy/transactions/{transactionId}/reverse",
                "/api/v1/economy/accounts/{accountId}/status");
            AppendCatalogGroup(md, requestedAudience, "admin", "Estate-Verwaltung",
                "/api/v1/estates",
                "/api/v1/estates/{estateId}",
                "/api/v1/estates/{estateId}/management",
                "/api/v1/estates/{estateId}/regions/{regionId}");
            AppendCatalogGroup(md, requestedAudience, "admin", "Optionale signierte Simulator-Funktionsfreigaben",
                "/api/v1/feature-control/nodes",
                "/api/v1/feature-control/nodes/{nodeId}");
            AppendCatalogGroup(md, requestedAudience, "admin", "Grid-Raster und Platzierung",
                "/api/v1/grid/layout",
                "/api/v1/grid/cells/{x}/{y}",
                "/api/v1/grid/validate-placement");
            AppendCatalogGroup(md, requestedAudience, "admin", "Inventar, Outfit, Kopien und Papierkorb",
                "/api/v1/inventory/items");
            AppendCatalogGroup(md, requestedAudience, "admin", "Simulator-Nodes",
                "/api/v1/nodes",
                "/api/v1/nodes/{nodeId}");
            AppendCatalogGroup(md, requestedAudience, "admin", "Asynchrone Regionenoperationen",
                "/api/v1/region-operations/{operationId}");
            AppendCatalogGroup(md, requestedAudience, "admin", "Regionen",
                "/api/v1/regions/{regionId}/placement",
                "/api/v1/regions/{regionId}/lifecycle");
            AppendCatalogGroup(md, requestedAudience, "admin", "Bewohnerkonten, Rollen und Autorisierung",
                "/api/v1/users",
                "/api/v1/users/{principalId}/audit",
                "/api/v1/users/{principalId}/state",
                "/api/v1/users/{principalId}/level",
                "/api/v1/users/{principalId}/authorization");
            AppendCatalogGroup(md, requestedAudience, "service", "NV$-Ledger, Buchungen und Konten",
                "/api/v1/economy/accounts/ensure");
            AppendCatalogGroup(md, requestedAudience, "service", "Experiences und Script-/Viewer-Bridges",
                "/api/v1/experiences/script/search",
                "/api/v1/experiences/script/info",
                "/api/v1/experiences/viewer/permissions",
                "/api/v1/experiences/viewer/group",
                "/api/v1/experiences/viewer/manage",
                "/api/v1/experiences/viewer/script-binding",
                "/api/v1/experiences/script/resolve",
                "/api/v1/experiences/script/details",
                "/api/v1/experiences/script/permission",
                "/api/v1/experiences/script/location",
                "/api/v1/experiences/script/kv");
            AppendCatalogGroup(md, requestedAudience, "service", "Vertrauenswuerdige Portal-Registrierungsbridge",
                "/api/v1/portal/avatars/register",
                "/api/v1/portal/avatars/name-availability");
        }

        private static void AppendCatalogGroup(StringBuilder md, string requestedAudience,
            string kind, string chapter, params string[] paths)
        {
            bool include = string.Equals(requestedAudience, "all", StringComparison.Ordinal) ||
                string.Equals(kind, "public", StringComparison.Ordinal) ||
                string.Equals(kind, requestedAudience, StringComparison.Ordinal) ||
                string.Equals(kind, "shared", StringComparison.Ordinal) ||
                (string.Equals(requestedAudience, "admin", StringComparison.Ordinal) &&
                 string.Equals(kind, "service", StringComparison.Ordinal));
            if (!include)
                return;

            string group = kind == "public" ? "Oeffentlich / Katalog" :
                kind == "citizen" ? "Persoenliche Einwohneroperationen" :
                kind == "admin" ? "Admin-/Betriebsfunktionen" :
                kind == "service" ? "Nur vertrauenswuerdiger Dienst/Simulator" :
                "Gemischte Leserechte, Owner-/Admin-Mutationen";
            md.AppendLine("### " + chapter + " — " + group);
            foreach (string path in paths)
                md.AppendLine("- `" + path + "`");
            md.AppendLine();
        }

        private static void AppendEndpointMetadata(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## OpenAPI-Metadaten fuer die automatische Zuordnung");
            md.AppendLine();
            md.AppendLine("- x-nexverse-audience: citizen, admin und/oder service; Service bedeutet nicht automatisch Browser/OAuth-Einwohnerzugriff.");
            md.AppendLine("- R287: /events/{eventId} und /classifieds/{classifiedId} bieten oeffentliche GETs ausschliesslich fuer freigegebene Inhalte. PATCH und DELETE erfordern discovery:manage plus nachgewiesenes Eigentum oder admin:*.");
            md.AppendLine("- R287-PATCH akzeptiert nur benannte Anzeige-/Zeit-/Tag-Felder; Creator/Owner, UUID, Moderation und Publikationszustand duerfen damit nicht geaendert werden.");
            md.AppendLine("- R287-POST Upsert: Einwohner duerfen ausschliesslich eigene Eintraege als Entwurf anlegen und fremde Inhalte nicht ersetzen. Publikationsstatus, Owner und Place koennen nicht mit normalen discovery:manage-Scopes eskaliert werden.");
            md.AppendLine("- Unveraendert offen: allgemeiner Assetbrowser, Citizen Notifications, Konto-Inhaber-Nachweise, Event RSVP und externe Payment-Credits sind keine zugesicherten Live-Endpunkte.");
            md.AppendLine("- security.apiKeyAuth (Header X-NexVerse-Api-Key) ist bei internen Viewer-Bridges explizit; normalen Bewohner-Bearer dort NICHT verwenden.");
            md.AppendLine("- x-nexverse-scope: benoetigter Berechtigungsumfang.");
            md.AppendLine("- x-nexverse-purpose: Zweck der Operation.");
            md.AppendLine("- x-nexverse-ai-instruction: operation-spezifische Anweisung fuer ChatGPT/API-Agenten.");
            md.AppendLine("- x-nexverse-security-constraints: Sicherheits- und Autorisierungsvorgaben.");
            md.AppendLine();
            md.AppendLine("Diese Metadaten haben Vorrang vor einer nur anhand des URL-Pfads geratenen Zuordnung.");
        }

        private static void AppendExecutionRules(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## HTTP- und Ausfuehrungsregeln");
            md.AppendLine();
            md.AppendLine("- JSON-API: Accept: application/json; bei JSON-Body zusaetzlich Content-Type: application/json.");
            md.AppendLine("- Bearer-Token: Authorization: Bearer <token>.");
            md.AppendLine("- Maschinen-API-Key: X-NexVerse-Api-Key: <key>.");
            md.AppendLine("- Idempotenz: Idempotency-Key mit einem stabilen Key pro fachlichem Vorgang.");
            md.AppendLine("- Niemals denselben Idempotency-Key fuer fachlich unterschiedliche Vorgaenge verwenden.");
            md.AppendLine("- Asynchrone Regionsoperationen ueber /region-operations/{operationId} verfolgen.");
            md.AppendLine("- Bei GET /api/v1/experiences/viewer/permissions bestimmt experience_id die Einzelstatusantwort; ohne experience_id liefert die Bridge mehrere nach Bewohner gefilterte ID-Listen.");
            md.AppendLine("- Bei PUT /api/v1/experiences/viewer/permissions nur status allowed, blocked oder none und die vom Simulator verifizierte avatar-gebundene resident_id verwenden.");
            md.AppendLine("- Interne Viewer-Bridges niemals ueber anonyme oder Bewohner-ChatGPT-Aufgaben direkt aufrufen; kein Cross-Avatar-Test mit fremder UUID als Ersatz fuer Authentifizierung.");
            md.AppendLine("- Keine Endpunkte erfinden. Nicht in OpenAPI vorhandene Funktionen als derzeit nicht ueber World API verfuegbar behandeln.");
        }
    }
}
