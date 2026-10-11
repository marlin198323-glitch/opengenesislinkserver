#!/usr/bin/env python3
"""No privileged viewer purchase/edit may bypass a live CAP or separate scope."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
security = (root/"NexVerse/Core/Security/NexSecurity.cs").read_text()
store = (root/"NexVerse/Core/Experiences/NexExperienceStore.cs").read_text()
api = (root/"NexVerse/Server/Api/NexExperiencesApi.cs").read_text()
viewer = (root/"NexVerse/RegionModules/Experiences/NexExperienceModule.cs").read_text()
config = (root/"bin/OpenSim.ini.example").read_text()
runtime = (root/"bin/config-include/GridCommon.ini").read_text()

for token in ('ExperiencesViewerManage = "experiences:viewer:manage"',
              "NexScopes.ExperiencesViewerManage",
              "privileged_scope_requires_admin"):
    assert token in security, "missing privileged viewer manage scope: " + token

for token in ("MaxViewerExperiencesPerResident = 10",
              "CreateViewerExperience(", "UpdateViewerExperience(",
              "existing.OwnerId != ownerId", "EnsureActive();"):
    assert token in store, "missing atomic owner-scoped acquisition: " + token

for token in ('"/api/v1/experiences/viewer/manage"',
              "NexScopes.ExperiencesViewerManage",
              'BodyGuid(viewerBody, "owner_id")',
              "viewerBody.EnumerateObject().Count() != 1",
              "m_Store.CreateViewerExperience(",
              "m_Store.UpdateViewerExperience(",
              'Audit(principal, "experience.viewer.acquired"',
              'Audit(principal, "experience.viewer.updated"'):
    assert token in api, "missing trusted World API viewer bridge: " + token

for token in ("FirestormManageCaps", "ViewerManageApiKey",
              "CreateViewerManageRequest(", "HandleViewerOwned(", "HandleViewerUpdate(",
              'result["purchase"] = OSD.FromInteger(0)',
              "NexExperienceStore.MaxViewerExperiencesPerResident",
              'if (!IsCurrentViewer(scene, avatar, lease))',
              'owner_id = avatar.ToString()', '"experience_keys"',
              "properties & ~(1 << 6)",
              '"UpdateExperience"'):
    assert token in viewer, "missing Firestorm CAP safety: " + token

assert 'FirestormManageCaps = true' in config
for flag in ("FirestormReadCaps", "FirestormPermissionCaps", "ScriptPendingConsent", "NativeExperiencePrompt"):
    assert f"{flag} = true" in config, flag
for guard in (
    "m_FirestormPermissionCaps = false;",
    "m_FirestormManageCaps = false;",
    "m_ScriptPendingConsent = false;",
    "m_NativeExperiencePrompt = false;",
    "m_ViewerManageApiKey = string.Empty;",
    "m_ViewerPermissionsApiKey = string.Empty;",
    "Central Experience adapter disabled: experiences:script API key is not provisioned.",
    "No automatic consent will be granted."
):
    assert guard in viewer, "missing fail-closed default guard: " + guard
assert 'ViewerManageApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY}"' in config
assert "NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY" in runtime
assert 'ViewerManageApiKey' not in api  # Secrets never arrive from viewer HTTP.
assert 'm_ViewerManageApiKey' not in store  # Core persistence has no secrets.
print("Firestorm Experience acquire/edit scope, quota and avatar-bound CAPS: OK")

# GetExperienceInfo must parse the Firestorm /id/uuid CAP suffix and
# reject foreign/ambiguous URL forms (covered by NexExperiencesRegression).
info_parser = (root/"NexVerse/Core/Experiences/NexExperienceInfoRequestIds.cs").read_text()
for token in ("TryParse(string requestPath, string capPath,",
              'path.StartsWith(root + "/", StringComparison.Ordinal)',
              'suffix.StartsWith("id/", StringComparison.OrdinalIgnoreCase)',
              "Guid.TryParseExact", "queryIds.Length > 64"):
    assert token in info_parser, "ExperienceInfo CAP URL guard missing: " + token
assert "NexExperienceInfoRequestIds.TryParse(" in viewer
assert "request.Url?.AbsolutePath, infoCapPath" in viewer
