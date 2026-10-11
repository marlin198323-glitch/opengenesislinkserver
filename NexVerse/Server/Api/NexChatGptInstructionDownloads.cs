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
            WriteMarkdown(request, response, "nexverse-chatgpt-anweisung.md", BuildAll());

        public void HandleCitizen(IOSHttpRequest request, IOSHttpResponse response) =>
            WriteMarkdown(request, response, "nexverse-chatgpt-buergerportal.md", BuildCitizen());

        public void HandleAdmin(IOSHttpRequest request, IOSHttpResponse response) =>
            WriteMarkdown(request, response, "nexverse-chatgpt-adminbereich.md", BuildAdmin());

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
                "NexVerse World API - ChatGPT Gesamtanweisung",
                "Zentrale Arbeitsanweisung fuer ChatGPT/API-Agenten. Einwohner-Selbstbedienung, administrative Funktionen und Dienstintegrationen muessen strikt getrennt bleiben.");

            AppendGlobalRules(md);
            AppendCitizenSection(md);
            AppendAdminSection(md);
            AppendServiceSection(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);

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
                "NexVerse World API - ChatGPT Anweisung Buergerportal",
                "Diese Anweisung gilt fuer Einwohner-Selbstbedienung. ChatGPT darf keine administrativen Rechte annehmen oder aus Einwohnerrechten ableiten.");

            AppendGlobalRules(md);
            AppendCitizenSection(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);

            md.AppendLine();
            md.AppendLine("## Harte Grenze");
            md.AppendLine();
            md.AppendLine("Operationen mit Zielgruppe nur admin oder mit admin:* gehoeren nicht in das Buergerportal. Wenn eine Aufgabe solche Rechte benoetigt, muss sie in den Adminbereich uebergeben werden.");
            return md.ToString();
        }

        private string BuildAdmin()
        {
            StringBuilder md = NewDocument(
                "NexVerse World API - ChatGPT Anweisung Adminbereich",
                "Diese Anweisung gilt fuer ausdruecklich autorisierte administrative Arbeiten. Adminrechte duerfen niemals aus einer normalen Einwohneranmeldung abgeleitet werden.");

            AppendGlobalRules(md);
            AppendAdminSection(md);
            AppendServiceSection(md);
            AppendEndpointMetadata(md);
            AppendExecutionRules(md);

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
            md.AppendLine("## Laufende NexVerse-Endpunkte");
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
            md.AppendLine("- API-v1 und Protokollversion sind nicht identisch mit der Serverrelease-Version. OpenGenesisLINK 0.9.3.10 ist Entwicklung, nicht Stable; 0.9.3.8 bleibt letzte Stable.");
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
            md.AppendLine("- Browser- und App-Integration bevorzugt OAuth 2.0 Authorization Code mit PKCE S256.");
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
            md.AppendLine("- GET/PATCH /profiles/{principalId} liest/bearbeitet das OpenSim-Avatarprofil einschliesslich profile_image als existierende Asset-UUID. Ein Webupload ist noch kein Bestandteil.");
            md.AppendLine("- Display Name via PATCH /users/{principalId} mit display_name, sieben Tage Cooldown fuer Self-Service.");
            md.AppendLine("- Gruppenmitteilungen: GET/POST /groups/{groupId}/notices; Rechte des Gruppenmitglieds gelten auch mit Gruppen-Scopes.");
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
            md.AppendLine();
            md.AppendLine("### Suche, Land und Destinationen");
            md.AppendLine("- Oeffentlich: /search, /places, /events, /classifieds, /land-portal und /destinations.");
            md.AppendLine("- Destination-Einreichungen koennen mit discovery:submit angeboten werden; Moderation bleibt administrativ.");
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
            md.AppendLine("- OGLVoice und Havok-kompatibles Firestorm-NavMesh sind in 0.9.3.10 kein als Stable freigegebenes Feature.");
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
        }

        private static void AppendServiceSection(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## Dienst-/Serverintegrationen");
            md.AppendLine();
            md.AppendLine("- Maschinenintegrationen verwenden eingeschraenkte API-Schluessel oder registrierte OAuth-Service-Clients.");
            md.AppendLine("- Kein admin:*-Schluessel fuer normale Simulator- oder Portalaufgaben.");
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

        private static void AppendEndpointMetadata(StringBuilder md)
        {
            md.AppendLine();
            md.AppendLine("## OpenAPI-Metadaten fuer die automatische Zuordnung");
            md.AppendLine();
            md.AppendLine("- x-nexverse-audience: citizen, admin und/oder service; Service bedeutet nicht automatisch Browser/OAuth-Einwohnerzugriff.");
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
