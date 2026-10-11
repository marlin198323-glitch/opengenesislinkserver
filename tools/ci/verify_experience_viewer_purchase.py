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
              "NexExperienceViewerFlags.TryDecode(properties,",
              "received={properties}",
              "rejected=0x{rejectedFlags:X8}",
              "only PRIVATE (0x20) and DISABLED (0x40) are editable.",
              'is_private = isPrivate',
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

# GetExperienceInfo must parse the *actual* Firestorm /id/?public_id=UUID
# batched CAP URL as well as /id/uuid, without widening CAPS access.
# Tests cover malformed/foreign/mixed paths (NexExperiencesRegression).
info_parser = (root/"NexVerse/Core/Experiences/NexExperienceInfoRequestIds.cs").read_text()
for token in ("TryParse(string requestPath, string capPath,",
              'path.StartsWith(root + "/", StringComparison.Ordinal)',
              'suffix.StartsWith("id/", StringComparison.OrdinalIgnoreCase)',
              'string.Equals(path, root + "/id", StringComparison.Ordinal)',
              "Guid.TryParseExact", "queryIds.Length > 64"):
    assert token in info_parser, "ExperienceInfo CAP URL guard missing: " + token
assert "NexExperienceInfoRequestIds.TryParse(" in viewer
assert "request.Url?.AbsolutePath, infoCapPath" in viewer

# Privacy must round-trip through the authorized World API and the durable store.
assert 'bool isPrivate = BodyBool(viewerBody, "is_private", false);' in api
assert 'viewerBody.EnumerateObject().Count() != 7' in api
assert 'is_private = e.IsPrivate' in api
assert "NexExperienceViewerFlags.Encode(enabled, isPrivate)" in viewer
storage = (root/"NexVerse/Core/Experiences/NexExperienceStore.cs").read_text()
assert "public bool IsPrivate { get; set; }" in storage
assert "x.Enabled && !x.IsPrivate" in storage
assert "IsPrivate = source.IsPrivate" in storage

flagContract = (root/"NexVerse/Core/Experiences/NexExperienceInfoRequestIds.cs").read_text()
assert "public const int EditableMask = Private | Disabled;" in flagContract
assert "rejectedFlags = flags & ~EditableMask;" in flagContract
assert "return rejectedFlags == 0;" in flagContract

# Firestorm uses GetCreatorExperiences to enable its script-editor checkbox.
# An owner is automatically an admin, but NOT automatically a contributor.
assert 'lease, "creator_ids"' in viewer
assert '"admin_ids", "contributor_ids", "creator_ids"' in viewer
assert 'creator_ids = m_Store.GetViewerCreatorExperienceIds(resident)' in api
assert "GetViewerCreatorExperienceIds(Guid residentId)" in store
