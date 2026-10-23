#!/usr/bin/env python3
"""Guard that runtime ChatGPT Markdown and OpenAPI describe real API routes.

Static checks run before .NET build. The CI Robust smoke test separately
fetches the generated /api/v1/openapi.json and asserts runtime semantics.
"""
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
api = (root / "NexVerse/Server/Api/NexExperiencesApi.cs").read_text(encoding="utf-8")
spec = (root / "NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text(encoding="utf-8")
md = (root / "NexVerse/Server/Api/NexChatGptInstructionDownloads.cs").read_text(encoding="utf-8")
explorer = (root / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text(encoding="utf-8")

# Every explicitly registered Experience route must be in the spec; not just
# the old generic management CRUD endpoints.
actual = set(re.findall(r'if\s*\(path\s*==\s*"(/api/v1/experiences/[^"]+)"\)', api))
actual.update(("/api/v1/experiences", "/api/v1/experiences/{experienceId}"))
specified = set(re.findall(r'\["(/api/v1/experiences[^"]*)"\]\s*=\s*', spec))
assert actual <= specified, "Experience paths absent from OpenAPI: " + str(sorted(actual - specified))
for path in (
    "/api/v1/experiences/script/info",
    "/api/v1/experiences/script/search",
    "/api/v1/experiences/script/resolve",
    "/api/v1/experiences/script/details",
    "/api/v1/experiences/script/permission",
    "/api/v1/experiences/script/location",
    "/api/v1/experiences/script/kv",
    "/api/v1/experiences/viewer/permissions",
    "/api/v1/experiences/viewer/group",
    "/api/v1/experiences/viewer/manage",
    "/api/v1/experiences/viewer/script-binding",
):
    assert path in specified, path

# Ensure typed JSON contracts have schema definitions, not a fall-through
# Chapter11ObjectResponse which prevents API agents from building correct bodies.
contracts = {
    "ExperienceDetailsResponse", "ExperienceSearchResponse",
    "ExperienceApiRecord", "ExperienceProfileUpdateRequest",
    "ExperiencePublicRecord", "ExperiencePublicSearchResponse",
    "ExperiencePublicInfoResponse", "ExperienceScriptResolveResponse",
    "ExperienceScriptPermissionResponse", "ExperienceScriptLocationResponse",
    "ExperienceKvRequest", "ExperienceKvResponse",
    "ExperienceViewerListsResponse", "ExperienceViewerDecisionRequest",
    "ExperienceViewerDecisionResponse", "ExperienceViewerPermissionReadResponse",
    "ExperienceViewerGroupResponse",
    "ExperienceViewerAcquireRequest",
    "ExperienceViewerProfileRequest",
    "ExperienceViewerManageResponse",
    "ExperienceViewerScriptBindingRequest",
    "ExperienceViewerScriptBindingResponse",
}
for name in contracts:
    assert f'["{name}"] = new' in spec, "Missing OpenAPI schema: " + name
    assert name in spec, "Unreferenced schema: " + name

for marker in ("SimulatorBridgeOperations(", "apiKeyAuth",
               'operation["x-nexverse-audience"] = new[] { "service" }',
               "experiences:viewer:permissions", "AddOperationParameters(paths",
               "max. 4000 Zeichen", "ExperienceKvRequest",
               "ExperienceViewerPermissionReadResponse",
               "ExperienceViewerManageResponse", "experiences:viewer:manage"):
    assert marker in spec, "Missing API machine contract/security: " + marker

for marker in ("/script/info", "/script/search", "/script/kv",
               "/experiences/viewer/permissions", "experiences:script",
               "experiences:viewer:permissions", "Viewer-CAPS",
               "OpenAPI", "OpenSim.VersionInfo.Version.Trim()", "OpenGenesisLINK World API",
               "AppendCitizenSection", "AppendAdminSection"):
    assert marker in md, "ChatGPT Markdown generator missing: " + marker

for marker in ("/api/v1/chatgpt-instructions.md",
               "/api/v1/chatgpt-buergerportal.md",
               "/api/v1/chatgpt-admin.md",
               "/api/v1/openapi.json"):
    assert marker in explorer, "Explorer link missing: " + marker

# R2026 capability atlas: every route from the World API's canonical
# OpenAPI dictionary must occur exactly once in the ChatGPT guide index.
# Regressions here mean an endpoint was added, moved or removed without
# updating the AI instructions. Do not silence drift by allowing extras.
registered = re.findall(r'^\\s*\\["(/[^"]+)"\\]\\s*=\\s*', spec, re.MULTILINE)
assert len(registered) == len(set(registered)), "Duplicate World OpenAPI paths"
assert len(registered) >= 151, "Unexpectedly reduced World API surface"
route_index = md.split(
    "private void AppendCompleteRouteIndex(StringBuilder md, string requestedAudience)", 1
)[1].split("private static void AppendEndpointMetadata(StringBuilder md)", 1)[0]
documented = re.findall(r'^\\s*"(/[^"]+)"\\s*[,)]\\s*;?\\s*
, route_index, re.MULTILINE)
assert len(documented) == len(set(documented)), "Duplicate paths in AI instructions"
assert set(registered) == set(documented), (
    "Missing from ChatGPT: " + str(sorted(set(registered) - set(documented))) +
    "; not in OpenAPI: " + str(sorted(set(documented) - set(registered)))
)
assert "AppendCatalogGroup(md, requestedAudience, \\"service\\"" in route_index
assert "string.Equals(requestedAudience, \\"citizen\\"" not in route_index
assert "string.Equals(kind, requestedAudience" in route_index
assert 'string.Equals(requestedAudience, "admin"' in route_index
assert "bool include =" in route_index
assert "private void AppendCompleteRouteIndex" in md, (
    "Route index must use instance base URL, not static fallbacks"
)
assert "m_PublicBaseUrl" in route_index and "m_ApiBaseUrl" in route_index

# Required coverage in ALL and focused citizen/admin manuals. Tests check
# generated method calls, topic coverage and least-privilege rules.
all_section = md.split("private string BuildAll()", 1)[1].split(
    "private string BuildCitizen()", 1
)[0]
citizen_section = md.split("private string BuildCitizen()", 1)[1].split(
    "private string BuildAdmin()", 1
)[0]
admin_section = md.split("private string BuildAdmin()", 1)[1].split(
    "private StringBuilder NewDocument(", 1
)[0]
for name in ("AppendIdentityAndScope2026", "AppendCitizenCoverage2026",
             "AppendAdminCoverage2026", "AppendServiceBoundaries2026",
             "AppendRequestPlaybooks2026", "AppendCompleteRouteIndex"):
    assert name + "(md" in all_section, "Missing ALL manual chapter " + name
for name in ("AppendCitizenCoverage2026", "AppendIdentityAndScope2026",
             "AppendCompleteRouteIndex"):
    assert name + "(md" in citizen_section, "Missing citizen chapter " + name
for name in ("AppendAdminCoverage2026", "AppendIdentityAndScope2026",
             "AppendServiceBoundaries2026", "AppendCompleteRouteIndex"):
    assert name + "(md" in admin_section, "Missing admin chapter " + name
assert 'AppendAdminCoverage2026(md)' not in citizen_section
assert 'AppendCitizenCoverage2026(md)' not in admin_section
assert 'AppendCompleteRouteIndex(md, "all")' in all_section
assert 'AppendCompleteRouteIndex(md, "citizen")' in citizen_section
assert 'AppendCompleteRouteIndex(md, "admin")' in admin_section

for required in (
    "100.000", "default_scopes", "statistics:read", "economy:read",
    "inventory:read", "authorization_persistence_failed",
    "expected_scopes", "admin:*", "Role", "Auth",
    "X-NexVerse-Api-Key", "Idempotency-Key", "x-nexverse-audience",
    "experiences:viewer:permissions", "eindeutige", "Service",
    "Region", "Estate", "Discovery", "Banking", "NexCommerce",
    "Gruppen", "World", "OAuth", "OIDC", "OpenAPI",
):
    assert required in md, "Missing R2026 AI capability/safety chapter: " + required

print(
    "World ChatGPT 2026 atlas: " + str(len(documented)) +
    " OpenAPI paths exactly catalogued; ALL/citizen/admin & scope/security gates OK"
)

print(f"OpenAPI / ChatGPT docs consistency: {len(actual)} explicit Experience paths covered")
