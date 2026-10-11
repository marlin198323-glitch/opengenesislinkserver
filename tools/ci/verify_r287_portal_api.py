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
# R287 originally named 16 response schemas that used empty opaque
# type=object entries. All 12 Banking/Commerce/Land envelopes now declare
# their real fields; the four polymorphic Discovery/Groups envelopes declare
# a documented subset and are not falsely advertised as complete DTOs.
for name, required in {
    "BankingTransactionsResponse": ("account_id", "transactions", "limit"),
    "BankingStatementResponse": ("statement", "currency", "correlation_id"),
    "BankingReconciliationResponse": ("reconciliation",),
    "PaymentRequestListResponse": ("payment_requests",),
    "PaymentRequestResponse": ("payment_request",),
    "BankingPolicyResponse": ("policy",),
    "CommerceOrderListResponse": ("orders",),
    "CommerceOrderResponse": ("order",),
    "LandListingListResponse": ("listings",),
    "LandListingResponse": ("listing",),
    "LandLeaseListResponse": ("leases",),
    "LandLeaseResponse": ("lease",),
    "DiscoveryObjectResponse": ("place", "correlation_id"),
    "DiscoveryMutationRequest": ("place_id", "name"),
    "Chapter11ObjectResponse": ("group", "members", "correlation_id"),
    "Chapter11MutationRequest": ("agent_id", "role_id"),
}.items():
    start = api.index(f'["{name}"] = new')
    segment = api[start: api.find("\n                },", start) + 20]
    assert "properties = new Dictionary<string, object>" in segment, name
    for prop in required:
        assert f'["{prop}"]' in segment, f"{name} lacks {prop}"

for x in ("RegionDetailResponse", "DiscoveryEventRecord",
          "DiscoveryClassifiedRecord", "CitizenGroupMembershipsResponse"):
    assert f'["{x}"] = new' in api

assert "no-store" in profile
print("R287 portal API owner isolation, 16 previously-empty schema names and live route contract: OK")
