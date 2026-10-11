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
               "OpenAPI", "0.9.3.10", "0.9.3.8",
               "AppendCitizenSection", "AppendAdminSection"):
    assert marker in md, "ChatGPT Markdown generator missing: " + marker

for marker in ("/api/v1/chatgpt-instructions.md",
               "/api/v1/chatgpt-buergerportal.md",
               "/api/v1/chatgpt-admin.md",
               "/api/v1/openapi.json"):
    assert marker in explorer, "Explorer link missing: " + marker

print(f"OpenAPI / ChatGPT docs consistency: {len(actual)} explicit Experience paths covered")
