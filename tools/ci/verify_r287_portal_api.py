#!/usr/bin/env python3
"""R287 portal coverage gates: authenticated image projection and Discovery CRUD."""
from pathlib import Path
import re

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

# AssetServiceConnector (two-argument module constructor) owns the already
# initialized FSAsset/OGLAsset service. The World API must not construct its
# own separate instance with LoadOptionalService(... one argument).
asset_connector = (root / "OpenSim/Server/Handlers/Asset/AssetServerConnector.cs").read_text()
assert 'new object[] { config, m_ConfigName }' in asset_connector, "ROBUST asset connector contract changed"
assert "public static IAssetService GetLocalService(" in asset_connector
assert 'services[m_ConfigName] = m_AssetService;' in asset_connector
assert 'AssetServiceConnector.GetLocalService(config, "AssetService")' in connector
assert 'IAssetService assets = LoadOptionalService<IAssetService>(config, "AssetService")' not in connector
assert '() => AssetServiceConnector.GetLocalService(config, "AssetService")' in connector
assert "Func<IAssetService> assetResolver = null" in profile
assert "IAssetService assets = m_AssetResolver?.Invoke() ?? m_Assets;" in profile
assert "AssetBase asset = assets.Get(profile.ImageId.ToString());" in profile

# userprofile owns the picture UUID and both publication flags.
# MySQL BINARY(1) flags contain ASCII 0/1 in production exports.
mysql = (root / "OpenSim/Data/MySQL/MySQLUserProfilesData.cs").read_text()
profile_start = mysql.index("public bool GetAvatarProperties(")
profile_end = mysql.index("public bool UpdateAvatarProperties(", profile_start)
profile_reader = mysql[profile_start:profile_end]
profile_writer_end = mysql.index("#endregion Avatar Properties", profile_end)
profile_writer = mysql[profile_end:profile_writer_end]
for column in ('reader["profileAllowPublish"]', 'reader["profileMaturePublish"]'):
    assert column in profile_reader, "publication flag not read: " + column
for parameter in ('profileAllowPublish=?publish', 'profileMaturePublish=?mature',
                  'cmd.Parameters.AddWithValue("?publish"', 'cmd.Parameters.AddWithValue("?mature"'):
    assert parameter in profile_writer, "publication flag not persisted: " + parameter
assert "bytes[0] == (byte)'1'" in mysql, "BINARY(1) publication flag decoding missing"

read_start = mysql.index("public bool GetUserAppData(")
write_start = mysql.index("public bool SetUserAppData(", read_start)
end = mysql.index("#endregion Integration", write_start)
read_body = mysql[read_start:write_start]
write_body = mysql[write_start:end]
assert "using (MySqlDataReader reader" in read_body
assert "INSERT" not in read_body and "ExecuteNonQuery" not in read_body, (
    "GET of privacy preferences must never create database rows"
)
for value in ("ON DUPLICATE KEY UPDATE", "DataVal = VALUES(DataVal)",
              'cmd.Parameters.AddWithValue("?DataVal", (object)props.DataVal'):
    assert value in write_body, "missing MySQL privacy upsert: " + value
assert 'cmd.Parameters.AddWithValue("?DataVal", props.DataKey' not in write_body
assert 'm_Log.Error("[NEX-WEBPROFILE-V3]: Profile image loading or conversion failed.", e);' in profile

# BaseHttpServer dispatches nested paths only when their service prefix is
# registered in the variable-path table. Without True, requests fall through
# to the generic /api user router and return "Unknown NexVerse API endpoint".
for prefix in ("/api/v1/profiles", "/api/v1/relationships", "/api/v1/security"):
    registration = (
        r"server\.AddSimpleStreamHandler\(\s*"
        r"new SimpleStreamHandler\(\s*"
        + re.escape('"' + prefix + '"')
        + r"[^;]*\),\s*true\s*\);"
    )
    assert re.search(registration, connector), "missing variable-path registration: " + prefix

base_http = (root / "OpenSim/Framework/Servers/HttpServer/BaseHttpServer.cs").read_text()
assert "m_simpleStreamVarPath.TryGetValue(uripath[..slash], out handler)" in base_http, (
    "HTTP server must resolve nested variable-prefix routes"
)

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
