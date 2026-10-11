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

assert 'FirestormManageCaps = false' in config
assert 'ViewerManageApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY}"' in config
assert "NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY" in runtime
assert 'ViewerManageApiKey' not in api  # Secrets never arrive from viewer HTTP.
assert 'm_ViewerManageApiKey' not in store  # Core persistence has no secrets.
print("Firestorm Experience acquire/edit scope, quota and avatar-bound CAPS: OK")
