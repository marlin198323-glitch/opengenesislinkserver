#!/usr/bin/env python3
"""Guard OGL 0.9.3.12 World API UX, release metadata and diagnostics."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8")

ui = read("NexVerse/Server/Api/NexApiDocsPage.cs")
generator = read("NexVerse/Server/Api/NexChatGptInstructionDownloads.cs")
api = read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
feature = read("NexVerse/Server/Api/OglFeatureControlApi.cs")
hgim = read("OpenSim/Services/HypergridService/HGInstantMessageService.cs")
xmlrpc = read("OpenSim/Framework/Servers/HttpServer/BaseHttpServer.cs")

def expect(source, name, *markers):
    for marker in markers:
        assert marker in source, f"{name}: missing {marker}"

expect(ui, "Dashboard visual refresh",
       'id="oglActiveRelease"', 'class="ogl-live-strip"',
       'class="ogl-section-head"', 'class="navsection-label"',
       '@keyframes ogl-orbit', 'prefers-reduced-motion:reduce',
       "version.server_version||'OpenGenesisLINK'",
       "status.textContent='Lizenzmodul nicht aktiviert.",
       "response.status===404", "data.error==='not_found'",
       "'/api/v1/feature-control/nodes'")
expect(generator, "Current ChatGPT instructions",
       "OpenGenesisLINK World API - ChatGPT Gesamtanweisung",
       "opengenesislink-chatgpt-anweisung.md",
       "OpenSim.VersionInfo.Version.Trim()",
       "Stadt-NexVerse bzw. NexVerse ist lediglich ein Betreiber-Grid",
       "X-NexVerse-Api-Key")
assert "0.9.3.10 ist Entwicklung, nicht Stable" not in generator
assert "0.9.3.8 bleibt letzte Stable" not in generator
expect(api, "Current 0.9.3.12 OpenAPI version history",
       'service = "OpenGenesisLINK World API"',
       'server_line = "0.9.3.12"',
       'version = "0.9.3.12"',
       'OpenGenesisLINK 0.9.3.12 Stable',
       'title = "OpenGenesisLINK 0.9.3.12 Stable:',
       'x_nexverse_roadmap =')
expect(feature, "Actual optional feature-control endpoint",
       '"/api/v1/feature-control/nodes"')
expect(hgim, "HG IM diagnostic-only throttle",
       "m_UnlocatedUserLog", "new ExpiringCacheOS<UUID, bool>(60000)",
       "m_UnlocatedUserLog.AddOrUpdate(toAgentID, true, 60)",
       "m_UserAgentService.LocateUser(toAgentID)",
       "return false;", "ForwardIMToGrid(url, im, toAgentID)")
assert "if (m_UnlocatedUserLog.TryGetValue" not in hgim, (
    "Negative location cache must never suppress delivery attempts.")
expect(xmlrpc, "Malformed XMLRPC reply",
       "Ungültige XML-RPC-Anfrage", "HttpStatusCode.BadRequest",
       "XmlRpcRequestDeserializer")
assert read("doc/OpenGenesisLINK/09312_WORLDAPI_WORLD_SIM_DIAGNOSTICS.md")
print("OGL World API UX / 0.9.3.12 metadata / HG IM / XMLRPC guard: OK")
