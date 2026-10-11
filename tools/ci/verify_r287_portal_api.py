#!/usr/bin/env python3
"""R287 portal coverage gates: authenticated image projection and Discovery CRUD."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
profile = (root / "NexVerse/Server/Api/NexWebProfileV3Api.cs").read_text()
connector = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
discovery = (root / "NexVerse/Server/Api/NexDiscoveryApi.cs").read_text()
groups = (root / "NexVerse/Server/Api/NexGroupsApi.cs").read_text()
api = (root / "NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()

for x in ("GetImage(UserAccount", "profile.PublishProfile",
          "NexScopes.ProfileRead", "creator != account.PrincipalID",
          "OpenJPEG.DecodeToImage", '"image/png"', "profile_image_url",
          "MaxProfileAssetBytes", "private, no-store"):
    assert x in profile, "profile image security or transcode missing: " + x
assert "profilesData,\n                            assets," in connector, "asset service not wired"

for name in ("/api/v1/events/", "/api/v1/classifieds/"):
    assert f'TryResourceId(path, "{name}"' in discovery
for x in ("RequireContentOwner(principal, existing.OrganizerId)",
          "RequireContentOwner(principal, existing.OwnerId)",
          "NexDiscoveryPublicationState.Approved",
          '"discovery.event.deleted"', '"discovery.classified.updated"',
          "RequireResidentActor(principal)",
          "incoming.OrganizerId = actor", "incoming.OwnerId = actor",
          "incoming.State = NexDiscoveryPublicationState.Draft",
          "incoming.State != prior.State",
          "incoming.PlaceId != prior.PlaceId",
          "Unsupported or protected event PATCH field",
          "Unsupported or protected classified PATCH field"):
    assert x in discovery, "Discovery access control missing: " + x

for x in ('path == "/api/v1/groups/mine"',
          "NexScopes.GroupsRead", "resident != account.PrincipalID.Guid",
          "GetAgentGroupMemberships(selfGroupRequester, selfGroupRequester)"):
    assert x in groups, "membership privacy missing: " + x

for path in ("/api/v1/profiles/{principalId}/image",
             "/api/v1/events/{eventId}", "/api/v1/classifieds/{classifiedId}",
             "/api/v1/groups/mine"):
    assert f'["{path}"] =' in api, "missing OpenAPI: " + path
for schema in ("DiscoveryEventRecord", "DiscoveryEventPatch", "DiscoveryEventResponse",
               "DiscoveryEventListResponse", "DiscoveryClassifiedRecord",
               "DiscoveryClassifiedPatch", "DiscoveryClassifiedResponse",
               "DiscoveryClassifiedListResponse", "DiscoveryDeletedResponse",
               "CitizenGroupMembershipRecord", "CitizenGroupMembershipsResponse"):
    assert f'["{schema}"] = new' in api, "missing JSON schema: " + schema
assert '["image/png"] = new' in api and 'format = "binary"' in api
assert "no-store" in profile
print("R287 profile image, Discovery detail/update/delete and own groups contract: OK")
