#!/usr/bin/env python3
"""Regression guard: statistics must keep safe public aggregates during GridUser failure."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
stats = (root / "NexVerse/Server/Api/NexStatisticsApi.cs").read_text(encoding="utf-8")
ui = (root / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text(encoding="utf-8")
routes = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")

for marker in (
    '"statistics_data_unavailable"',
    'if (m_UserAccounts == null)',
    'bool gridUserAvailable = m_GridUsers != null;',
    'm_GridUsers.GetAll(string.Empty)',
    'catch (Exception e)',
    'gridUserAvailable = false;',
    'if (!gridUserAvailable && !completeAccountEnumeration)',
    '"Both GridUser history and complete UserAccount enumeration are unavailable."',
    'active_last_7_days = gridUserAvailable',
    'online_now = gridUserAvailable ? hypergridOnline.Count : (int?)null',
    'total = gridUserAvailable ? allOnline.Count : (int?)null',
    'missing_metrics = (gridUserAvailable',
    'griduser_source = gridUserAvailable ? "available" : "unavailable"',
    'complete = gridUserAvailable && completeAccountEnumeration &&',
):
    assert marker in stats, "Statistics fallback contract missing: " + marker

assert "m_UserAccounts == null || m_GridUsers == null" not in stats

# Full OpenSim HG UUIs must be looked up exactly. Prefixing all lookups
# with the bare 36-character UUID merges different HG homes and can erase
# the GridUser origin rows expected by statistics:read.
griduser = (root / "OpenSim/Services/UserAccountService/GridUserService.cs").read_text(encoding="utf-8")
for marker in (
    "private static string GridUserCacheKey(string userID)",
    "userID.Length > 36 && userID[36] == ';'",
    "GridUserData exact = m_Database.Get(userID);",
    "CacheGridUser(userID, exact);",
    "GridUserData local = m_Database.Get(bareId);",
    "CacheGridUser(userID, d);",
):
    assert marker in griduser, "HG UUI provenance regression: " + marker
assert 'cache.Add(userID.Substring(0, 36), d, 300000)' not in griduser
assert 'userID = userID.Substring(0, 36);' not in griduser

# Protected HG origin rows are published only with statistics:read. The
# data-quality envelope differentiates missing source, zero known HG users,
# and legacy records with no recoverable home URI.
for marker in (
    "NexScopes.StatisticsRead",
    "hypergrid_home_grids = includeProtectedDetails",
    "hypergrid_origin_status = hypergridOriginStatus",
    "hypergrid_origin_rows = includeProtectedDetails",
    "hypergrid_identities_without_home_uri = includeProtectedDetails",
    '"no_recorded_visitors"',
    '"incomplete_home_uri"',
    '"hypergrid_home_grid_identity"',
    "missingHomeGridIdentities == 0",
):
    assert marker in stats, "Protected HG origin diagnostics missing: " + marker

assert stats.index('if (m_UserAccounts == null)') < stats.index('BuildSummary(response, includeProtectedDetails);')
assert '"/api/v1/statistics/summary"' in routes
assert routes.count('"/api/v1/statistics/summary"') == 1, "One early statistics route must serve both auth modes"
assert routes.index('"/api/v1/statistics/summary"') < routes.index('bool privilegedEndpoints ='), "Statistics must register before privileged service initialization"
assert 'statisticsRoute.AttachPrivileged(statisticsApi);' in routes, "Protected statistics must preserve scope-enforcing authenticator"
assert 'System.Threading.Volatile.Read(ref m_PrivilegedApi)' in routes
assert 'LoadOptionalService<IUserAccountService>(m_Config, "UserAccountService")' in routes
assert 'new NexStatisticsApi(null, null, null, null, null, true)' in routes, "Provider errors must result in JSON 503, not HTML 404"

start = ui.split("async function loadStatistics(){", 1)[1].split("\nfunction ", 1)[0]
assert "const bearer=$('statsBearer').value.trim()||adminAuthToken();" in start
assert "const key=$('statsApiKey').value.trim();" in start
assert "$('bearer').value.trim()" not in start, "Unrelated explorer bearer must never break public stats"
assert "$('apiKey').value.trim()" not in start, "Unrelated explorer API key must never break public stats"
assert "Teilweise (Datenquelle fehlt)" in start
assert "feh" in ui

# The public region inventory must enumerate GridStore rows even with no online
# avatars; do not fall back to the avatar-derived region breakdown.
for marker in (
    "IRegionData m_RegionData",
    "IAssetService m_Assets",
    'm_RegionData.Get("%", UUID.Zero)',
    "GroupBy(x => x.RegionID)",
    "RegionFlags.Hyperlink",
    "RegionFlags.Reservation",
    "RegionFlags.RegionOnline",
    "region_summary = new",
    "registered_total = regions.Registered",
    "online_total = regions.Online",
    "offline_total = regions.Offline",
    "regions = regions.Rows",
    "services = new",
    "ReadAssetServiceStatus()",
    "m_Assets.AssetsExist(",
    "region_source = regions.Available",
    "asset_service_probe = assetStatus",
    "region_online_users = includeProtectedDetails",
):
    assert marker in stats, "World regions and service health contract missing: " + marker

for marker in (
    "LoadRegionData(m_Config)",
    "LoadRegionData(config)",
    'LoadOptionalService<IAssetService>(m_Config, "AssetService")',
    "return ServerUtils.LoadPlugin<IRegionData>(",
    'grid?.GetString("Realm", "regions")',
):
    assert marker in routes, "GridStore/AssetService load contract missing: " + marker

# Verify public region detail does not accidentally publish simulator internal
# endpoints, database connection strings, owner data, tokens or secrets.
public_regions = stats.split("private RegionSnapshot ReadRegions()", 1)[1].split("private string ReadAssetServiceStatus()", 1)[0]
for disallowed in ("ServerURI", "ExternalHostName", "RegionSecret", ".Token", "connectionString", "EstateOwner"):
    assert disallowed not in public_regions, "Sensitive region field exposed: " + disallowed


print("OGL statistics partial-data and public aggregate routing: PASS")
