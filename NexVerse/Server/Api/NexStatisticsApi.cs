// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using log4net;
using NexVerse.Core;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexStatisticsApi
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(NexStatisticsApi));
        private static readonly JsonSerializerOptions s_Json =
            new JsonSerializerOptions { WriteIndented = true };

        private readonly IUserAccountService m_UserAccounts;
        private readonly IGridUserData m_GridUsers;
        private readonly IPresenceService m_Presence;
        private readonly IGridService m_Grid;
        private readonly IRegionData m_RegionData;
        private readonly IAssetService m_Assets;
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly bool m_AllowPublicAggregates;

        public NexStatisticsApi(
            IUserAccountService userAccounts,
            IGridUserData gridUsers,
            IPresenceService presence,
            IGridService grid,
            NexApiAuthenticator authenticator,
            bool allowPublicAggregates,
            IRegionData regionData = null,
            IAssetService assets = null)
        {
            m_UserAccounts = userAccounts;
            m_GridUsers = gridUsers;
            m_Presence = presence;
            m_Grid = grid;
            m_RegionData = regionData;
            m_Assets = assets;
            m_Authenticator = authenticator;
            m_AllowPublicAggregates = allowPublicAggregates;
        }

        public void Summary(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (request == null ||
                !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                WriteError(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed", "GET is required.");
                return;
            }

            bool hasCredentials = HasCredentials(request);
            bool includeProtectedDetails = false;

            if (hasCredentials)
            {
                if (m_Authenticator == null)
                {
                    WriteError(
                        response,
                        HttpStatusCode.ServiceUnavailable,
                        "statistics_authentication_unavailable",
                        "Protected statistics details are unavailable because privileged World API authentication is disabled.");
                    return;
                }

                if (!m_Authenticator.TryAuthenticate(
                        request,
                        NexScopes.StatisticsRead,
                        out NexPrincipal _,
                        out UserAccount _,
                        out int statusCode,
                        out string authError))
                {
                    response.AddHeader("WWW-Authenticate", "Bearer");
                    WriteError(
                        response,
                        (HttpStatusCode)statusCode,
                        authError,
                        "Authentication or statistics:read authorization is required for protected details.");
                    return;
                }

                includeProtectedDetails = true;
            }
            else if (!m_AllowPublicAggregates)
            {
                response.AddHeader("WWW-Authenticate", "Bearer");
                WriteError(
                    response,
                    HttpStatusCode.Unauthorized,
                    "authentication_required",
                    "Authentication or statistics:read authorization is required.");
                return;
            }

            if (m_UserAccounts == null)
            {
                WriteError(
                    response,
                    HttpStatusCode.ServiceUnavailable,
                    "statistics_data_unavailable",
                    "UserAccount statistics storage is not available.");
                return;
            }

            // A GridUser storage issue must not take down the entire public
            // statistics endpoint when authoritative resident account totals
            // can still be read. Mark activity/visitor metrics as unknown.
            // If account enumeration ALSO fails, return 503 instead of
            // inventing a grid with zero registered users.

            try
            {
                BuildSummary(response, includeProtectedDetails);
            }
            catch (Exception e)
            {
                m_Log.Error("[NEX-STATS]: Failed to build statistics summary.", e);
                WriteError(
                    response,
                    HttpStatusCode.ServiceUnavailable,
                    "statistics_unavailable",
                    "Statistics could not be calculated from the current service state.");
            }
        }

        private void BuildSummary(
            IOSHttpResponse response,
            bool includeProtectedDetails)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset dayCutoff = now.AddHours(-24);
            DateTimeOffset weekCutoff = now.AddDays(-7);
            DateTimeOffset monthCutoff = now.AddDays(-30);

            bool gridUserAvailable = m_GridUsers != null;
            GridUserData[] rawRows = Array.Empty<GridUserData>();
            if (gridUserAvailable)
            {
                try
                {
                    rawRows = m_GridUsers.GetAll(string.Empty) ?? Array.Empty<GridUserData>();
                }
                catch (Exception e)
                {
                    m_Log.Warn("[NEX-STATS]: GridUser statistics unavailable; serving partial account aggregates.", e);
                    gridUserAvailable = false;
                }
            }

            List<ActivityRecord> parsed = rawRows.Select(ParseActivity).Where(x => x != null).ToList();

            Dictionary<string, ActivityRecord> localActivity =
                parsed.Where(x => !x.IsHypergrid)
                    .GroupBy(x => x.PrincipalId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => MostRecent(x), StringComparer.OrdinalIgnoreCase);

            List<ActivityRecord> knownHypergrid =
                parsed.Where(x => x.IsHypergrid)
                    .GroupBy(x => x.VisitorKey, StringComparer.OrdinalIgnoreCase)
                    .Select(MostRecent)
                    .ToList();

            Dictionary<string, ActivityRecord> latestHypergridByPrincipal =
                knownHypergrid
                    .GroupBy(x => x.PrincipalId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => MostRecent(x), StringComparer.OrdinalIgnoreCase);

            List<UserAccount> accounts = LoadAccounts(localActivity, out bool completeAccountEnumeration);
            if (!gridUserAvailable && !completeAccountEnumeration)
                throw new InvalidOperationException(
                    "Both GridUser history and complete UserAccount enumeration are unavailable.");

            Dictionary<string, UserAccount> accountById =
                accounts.GroupBy(x => x.PrincipalID.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            string[] presenceIds =
                accountById.Keys
                    .Concat(latestHypergridByPrincipal.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            Dictionary<string, UUID> livePresence = ReadPresence(presenceIds, out bool presenceAvailable);
            List<OnlineRecord> residentsOnline = new List<OnlineRecord>();

            foreach (UserAccount account in accounts)
            {
                string id = account.PrincipalID.ToString();
                localActivity.TryGetValue(id, out ActivityRecord activity);

                if (!IsOnline(id, activity, livePresence, presenceAvailable, now))
                    continue;

                UUID regionId = ResolvePresenceRegion(id, activity, livePresence, presenceAvailable);
                residentsOnline.Add(new OnlineRecord
                {
                    Type = "resident",
                    PrincipalId = id,
                    Name = account.Name,
                    HomeGrid = string.Empty,
                    RegionId = regionId,
                    RegionName = ResolveRegionName(regionId),
                    LoginAt = activity?.LoginAt
                });
            }

            List<OnlineRecord> hypergridOnline = new List<OnlineRecord>();
            foreach (ActivityRecord activity in latestHypergridByPrincipal.Values)
            {
                if (!IsOnline(activity.PrincipalId, activity, livePresence, presenceAvailable, now))
                    continue;

                UUID regionId =
                    ResolvePresenceRegion(activity.PrincipalId, activity, livePresence, presenceAvailable);

                hypergridOnline.Add(new OnlineRecord
                {
                    Type = "hypergrid",
                    PrincipalId = activity.PrincipalId,
                    Name = activity.DisplayName,
                    HomeGrid = activity.HomeGrid,
                    RegionId = regionId,
                    RegionName = ResolveRegionName(regionId),
                    LoginAt = activity.LoginAt
                });
            }

            List<OnlineRecord> allOnline =
                residentsOnline.Concat(hypergridOnline)
                    .OrderBy(x => x.Type, StringComparer.Ordinal)
                    .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            object[] regionBreakdown =
                allOnline.GroupBy(x => x.RegionId)
                    .Select(group => new
                    {
                        region_id = group.Key.IsZero() ? string.Empty : group.Key.ToString(),
                        region_name = group.Select(x => x.RegionName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                        online_total = group.Count(),
                        residents = group.Count(x => x.Type == "resident"),
                        hypergrid = group.Count(x => x.Type == "hypergrid")
                    })
                    .OrderByDescending(x => x.online_total)
                    .ThenBy(x => x.region_name, StringComparer.OrdinalIgnoreCase)
                    .Cast<object>()
                    .ToArray();

            HashSet<string> onlineHypergridKeys =
                new HashSet<string>(
                    hypergridOnline.Select(x => MakeVisitorKey(x.PrincipalId, x.HomeGrid)),
                    StringComparer.OrdinalIgnoreCase);

            object[] homeGridBreakdown =
                knownHypergrid
                    .Where(x => !string.IsNullOrWhiteSpace(x.HomeGrid))
                    .GroupBy(x => x.HomeGrid, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new
                    {
                        home_grid = group.Key,
                        known_visitors = group.Count(),
                        online_now = group.Count(x => onlineHypergridKeys.Contains(x.VisitorKey)),
                        last_7_days = group.Count(x => IsRecent(x.LastActivityAt, weekCutoff)),
                        last_30_days = group.Count(x => IsRecent(x.LastActivityAt, monthCutoff))
                    })
                    .OrderByDescending(x => x.online_now)
                    .ThenByDescending(x => x.last_30_days)
                    .ThenBy(x => x.home_grid, StringComparer.OrdinalIgnoreCase)
                    .Cast<object>()
                    .ToArray();

            // A successful GridUser query does not guarantee that its HG
            // identities include a HomeURI. Never claim origin coverage when
            // only bare UUIDs (or malformed historical UUI rows) are present.
            int missingHomeGridIdentities = knownHypergrid.Count(
                x => string.IsNullOrWhiteSpace(x.HomeGrid));
            string hypergridOriginStatus = !gridUserAvailable
                ? "unavailable"
                : knownHypergrid.Count == 0
                    ? "no_recorded_visitors"
                    : missingHomeGridIdentities > 0
                        ? "incomplete_home_uri"
                        : "available";

            Dictionary<string, int> accountStates =
                accounts.GroupBy(
                        x => string.IsNullOrWhiteSpace(x.NexVerseState)
                            ? "active"
                            : x.NexVerseState.Trim().ToLowerInvariant(),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

            int? neverLoggedIn = gridUserAvailable
                ? accounts.Count(account =>
                  {
                      string id = account.PrincipalID.ToString();
                      return !localActivity.TryGetValue(id, out ActivityRecord record) || !record.LoginAt.HasValue;
                  })
                : null;

            // A missing GridUser history alone does not imply that everybody
            // is offline. If the PresenceService is authoritative, resident
            // online counts remain meaningful, otherwise report unknown.
            bool residentOnlineKnown = gridUserAvailable || presenceAvailable;

            RegionSnapshot regions = ReadRegions();
            string assetStatus = ReadAssetServiceStatus();

            string correlationId = Correlation(response);

            WriteJson(response, new
            {
                generated_at = now,
                residents = new
                {
                    registered_total = accounts.Count,
                    active_accounts = accounts.Count(x => x.LoginAllowed),
                    restricted_accounts = accounts.Count(x => !x.LoginAllowed),
                    registrations_last_7_days =
                        accounts.Count(x => IsRecent(FromUnix(x.Created), weekCutoff)),
                    registrations_last_30_days =
                        accounts.Count(x => IsRecent(FromUnix(x.Created), monthCutoff)),
                    online_now = residentOnlineKnown ? residentsOnline.Count : (int?)null,
                    active_last_24_hours = gridUserAvailable
                        ? accounts.Count(x => HasRecentActivity(x, localActivity, dayCutoff))
                        : (int?)null,
                    active_last_7_days = gridUserAvailable
                        ? accounts.Count(x => HasRecentActivity(x, localActivity, weekCutoff))
                        : (int?)null,
                    active_last_30_days = gridUserAvailable
                        ? accounts.Count(x => HasRecentActivity(x, localActivity, monthCutoff))
                        : (int?)null,
                    never_logged_in = neverLoggedIn
                },
                hypergrid = new
                {
                    online_now = gridUserAvailable ? hypergridOnline.Count : (int?)null,
                    known_visitors_total = gridUserAvailable ? knownHypergrid.Count : (int?)null,
                    visitors_last_7_days = gridUserAvailable
                        ? knownHypergrid.Count(x => IsRecent(x.LastActivityAt, weekCutoff))
                        : (int?)null,
                    visitors_last_30_days = gridUserAvailable
                        ? knownHypergrid.Count(x => IsRecent(x.LastActivityAt, monthCutoff))
                        : (int?)null,
                    known_home_grids = gridUserAvailable
                        ? knownHypergrid.Select(x => x.HomeGrid)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count()
                        : (int?)null,
                    home_grids_online_now = gridUserAvailable
                        ? hypergridOnline.Select(x => x.HomeGrid)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count()
                        : (int?)null
                },
                detail_level = !gridUserAvailable || !regions.Available || assetStatus != "ok" ||
                    missingHomeGridIdentities > 0
                    ? "partial"
                    : includeProtectedDetails ? "authenticated" : "aggregate",
                protected_details = includeProtectedDetails,
                online = new
                {
                    total = gridUserAvailable ? allOnline.Count : (int?)null,
                    users = includeProtectedDetails
                        ? allOnline.Select(OnlinePayload).ToArray()
                        : Array.Empty<object>()
                },
                account_states = accountStates,
                // Grid region registration is public map metadata, not an
                // online-avatar breakdown. Include offline and reserved local
                // regions; never expose simulator internal endpoints or secrets.
                regions = regions.Rows,
                region_summary = new
                {
                    registered_total = regions.Registered,
                    online_total = regions.Online,
                    offline_total = regions.Offline,
                    reserved_total = regions.Reserved,
                    hypergrid_links = regions.HypergridLinks,
                    status_source = "GridStore RegionOnline flag (registration state, not an active network probe)"
                },
                services = new
                {
                    grid = new
                    {
                        status = m_Grid == null
                            ? "unavailable"
                            : regions.Available ? "ok" : "degraded",
                        data_available = regions.Available
                    },
                    asset = new
                    {
                        status = assetStatus,
                        probe = "read_only_missing_asset_existence_query"
                    }
                },
                region_online_users = includeProtectedDetails
                    ? regionBreakdown
                    : Array.Empty<object>(),
                hypergrid_home_grids = includeProtectedDetails
                    ? homeGridBreakdown
                    : Array.Empty<object>(),
                data_quality = new
                {
                    complete = gridUserAvailable && completeAccountEnumeration &&
                        regions.Available && assetStatus == "ok" && missingHomeGridIdentities == 0,
                    griduser_source = gridUserAvailable ? "available" : "unavailable",
                    hypergrid_origin_status = hypergridOriginStatus,
                    hypergrid_origin_rows = includeProtectedDetails
                        ? homeGridBreakdown.Length : (int?)null,
                    hypergrid_identities_without_home_uri = includeProtectedDetails
                        ? missingHomeGridIdentities : (int?)null,
                    region_source = regions.Available ? "GridStore" : "unavailable",
                    asset_service_probe = assetStatus,
                    missing_metrics = (gridUserAvailable
                        ? Array.Empty<string>()
                        : new[] { "activity_history", "hypergrid_visitors", "hypergrid_online", "total_online" })
                        .Concat(regions.Available ? Array.Empty<string>() : new[] { "registered_regions", "online_regions" })
                        .Concat(assetStatus == "ok" ? Array.Empty<string>() : new[] { "asset_service_health" })
                        .Concat(missingHomeGridIdentities == 0
                            ? Array.Empty<string>()
                            : new[] { "hypergrid_home_grid_identity" })
                        .ToArray(),
                    registered_source =
                        completeAccountEnumeration
                            ? "UserAccountService aggregate"
                            : "GridUser/UserAccount fallback",
                    online_source = presenceAvailable
                        ? "PresenceService"
                        : gridUserAvailable
                            ? "GridUser Online flag with five-day stale guard"
                            : "unavailable",
                    historical_activity = gridUserAvailable
                        ? "Unique identities use the latest GridUser Login/Logout timestamps."
                        : "unavailable",
                    hypergrid_history = gridUserAvailable
                        ? "Known Hypergrid counts are distinct visitor/home-grid identities, not cumulative visit sessions."
                        : "unavailable",
                    exact_visit_sessions_available = false
                },
                correlation_id = correlationId
            });
        }


        // Use the authoritative GridStore instead of deriving regions from
        // avatar presence: empty grids still have registered/offline regions.
        private RegionSnapshot ReadRegions()
        {
            if (m_RegionData == null)
                return new RegionSnapshot();

            try
            {
                List<RegionData> rows = m_RegionData.Get("%", UUID.Zero);
                if (rows == null)
                    return new RegionSnapshot();

                RegionData[] distinct = rows
                    .Where(x => x != null && !x.RegionID.IsZero())
                    .GroupBy(x => x.RegionID)
                    .Select(group => group.First())
                    .OrderBy(x => x.RegionName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                int online = 0, reserved = 0, links = 0;
                List<object> visible = new List<object>();
                foreach (RegionData region in distinct)
                {
                    int flags = 0;
                    if (region.Data != null &&
                        region.Data.TryGetValue("flags", out object raw))
                        int.TryParse(Convert.ToString(raw), out flags);

                    bool isLink =
                        (flags & (int)OpenSim.Framework.RegionFlags.Hyperlink) != 0;
                    if (isLink)
                    {
                        links++;
                        continue;
                    }

                    bool isReserved =
                        (flags & (int)OpenSim.Framework.RegionFlags.Reservation) != 0;
                    bool isOnline = !isReserved &&
                        (flags & (int)OpenSim.Framework.RegionFlags.RegionOnline) != 0;
                    if (isReserved)
                        reserved++;
                    if (isOnline)
                        online++;

                    visible.Add(new
                    {
                        region_id = region.RegionID.ToString(),
                        region_name = region.RegionName ?? string.Empty,
                        grid_x = region.posX / 256,
                        grid_y = region.posY / 256,
                        size_x = region.sizeX,
                        size_y = region.sizeY,
                        online = isOnline,
                        status = isReserved ? "reserved" : isOnline ? "online" : "offline"
                    });
                }

                return new RegionSnapshot
                {
                    Available = true,
                    Registered = visible.Count,
                    Online = online,
                    Offline = visible.Count - online - reserved,
                    Reserved = reserved,
                    HypergridLinks = links,
                    Rows = visible
                        .ToArray()
                };
            }
            catch (Exception e)
            {
                m_Log.Warn("[NEX-STATS]: GridStore region inventory unavailable; other statistics remain accessible.", e);
                return new RegionSnapshot();
            }
        }

        // A read-only query for a known missing UUID tests the actual asset
        // datastore. A false existence result is SUCCESS; no asset is changed.
        private string ReadAssetServiceStatus()
        {
            if (m_Assets == null)
                return "unavailable";
            try
            {
                bool[] found = m_Assets.AssetsExist(
                    new[] { UUID.Zero.ToString() });
                return found != null && found.Length == 1 ? "ok" : "degraded";
            }
            catch (Exception e)
            {
                m_Log.Warn("[NEX-STATS]: AssetService read-only health probe failed.", e);
                return "degraded";
            }
        }

        private sealed class RegionSnapshot
        {
            public bool Available;
            public int? Registered;
            public int? Online;
            public int? Offline;
            public int? Reserved;
            public int? HypergridLinks;
            public object[] Rows = Array.Empty<object>();
        }

        private List<UserAccount> LoadAccounts(
            Dictionary<string, ActivityRecord> localActivity,
            out bool completeEnumeration)
        {
            completeEnumeration = true;
            List<UserAccount> accounts;

            try
            {
                accounts = m_UserAccounts.GetUserAccountsWhere(UUID.Zero, "1=1");
            }
            catch
            {
                accounts = null;
            }

            if (accounts != null && (accounts.Count > 0 || localActivity.Count == 0))
            {
                return accounts
                    .Where(x => x != null && x.LocalToGrid)
                    .GroupBy(x => x.PrincipalID)
                    .Select(x => x.First())
                    .ToList();
            }

            completeEnumeration = false;
            List<UserAccount> fallback = new List<UserAccount>();

            foreach (string principalId in localActivity.Keys)
            {
                if (!UUID.TryParse(principalId, out UUID id))
                    continue;

                UserAccount account = m_UserAccounts.GetUserAccount(UUID.Zero, id);
                if (account != null && account.LocalToGrid)
                    fallback.Add(account);
            }

            return fallback.GroupBy(x => x.PrincipalID).Select(x => x.First()).ToList();
        }

        private Dictionary<string, UUID> ReadPresence(string[] userIds, out bool available)
        {
            Dictionary<string, UUID> result =
                new Dictionary<string, UUID>(StringComparer.OrdinalIgnoreCase);

            available = m_Presence != null;
            if (!available || userIds.Length == 0)
                return result;

            try
            {
                const int batchSize = 250;
                for (int offset = 0; offset < userIds.Length; offset += batchSize)
                {
                    string[] batch = userIds.Skip(offset).Take(batchSize).ToArray();
                    PresenceInfo[] found = m_Presence.GetAgents(batch) ?? Array.Empty<PresenceInfo>();

                    foreach (PresenceInfo presence in found)
                    {
                        if (presence == null || string.IsNullOrWhiteSpace(presence.UserID))
                            continue;

                        result[presence.UserID] = presence.RegionID;
                    }
                }
            }
            catch (Exception e)
            {
                m_Log.Warn(
                    "[NEX-STATS]: PresenceService lookup failed; falling back to GridUser status.",
                    e);
                result.Clear();
                available = false;
            }

            return result;
        }

        private string ResolveRegionName(UUID regionId)
        {
            if (regionId.IsZero() || m_Grid == null)
                return string.Empty;

            try
            {
                OpenSim.Services.Interfaces.GridRegion region = m_Grid.GetRegionByUUID(UUID.Zero, regionId);
                return region?.RegionName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static UUID ResolvePresenceRegion(
            string principalId,
            ActivityRecord activity,
            Dictionary<string, UUID> livePresence,
            bool presenceAvailable)
        {
            if (presenceAvailable && livePresence.TryGetValue(principalId, out UUID regionId))
                return regionId;

            return activity?.LastRegionId ?? UUID.Zero;
        }

        private static bool IsOnline(
            string principalId,
            ActivityRecord activity,
            Dictionary<string, UUID> livePresence,
            bool presenceAvailable,
            DateTimeOffset now)
        {
            if (presenceAvailable)
                return livePresence.ContainsKey(principalId);

            return activity != null &&
                   activity.Online &&
                   activity.LoginAt.HasValue &&
                   activity.LoginAt.Value >= now.AddDays(-5);
        }

        private static bool HasRecentActivity(
            UserAccount account,
            Dictionary<string, ActivityRecord> activity,
            DateTimeOffset cutoff)
        {
            return activity.TryGetValue(account.PrincipalID.ToString(), out ActivityRecord record) &&
                   IsRecent(record.LastActivityAt, cutoff);
        }

        private static bool IsRecent(DateTimeOffset? timestamp, DateTimeOffset cutoff)
        {
            return timestamp.HasValue && timestamp.Value >= cutoff;
        }

        private static ActivityRecord ParseActivity(GridUserData row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.UserID))
                return null;

            string raw = row.UserID.Trim();
            bool hypergrid = raw.IndexOf(';') >= 0;
            string principalId = raw;
            string homeGrid = string.Empty;
            string displayName = string.Empty;

            if (hypergrid)
            {
                string[] parts = raw.Split(new[] { ';' }, 3, StringSplitOptions.None);
                principalId = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                homeGrid = parts.Length > 1 ? parts[1].Trim().TrimEnd('/') : string.Empty;
                displayName = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            }

            if (!UUID.TryParse(principalId, out UUID parsedId))
                return null;

            DateTimeOffset? login = ReadUnix(row.Data, "Login");
            DateTimeOffset? logout = ReadUnix(row.Data, "Logout");

            bool online = false;
            if (row.Data != null && row.Data.TryGetValue("Online", out string onlineRaw))
                bool.TryParse(onlineRaw, out online);

            UUID lastRegionId = UUID.Zero;
            if (row.Data != null && row.Data.TryGetValue("LastRegionID", out string regionRaw))
                UUID.TryParse(regionRaw, out lastRegionId);

            string normalizedId = parsedId.ToString();

            return new ActivityRecord
            {
                PrincipalId = normalizedId,
                IsHypergrid = hypergrid,
                HomeGrid = homeGrid,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName,
                LoginAt = login,
                LogoutAt = logout,
                Online = online,
                LastRegionId = lastRegionId,
                VisitorKey = MakeVisitorKey(normalizedId, homeGrid)
            };
        }

        private static ActivityRecord MostRecent(IEnumerable<ActivityRecord> records)
        {
            return records.OrderByDescending(x => x.LastActivityAt ?? DateTimeOffset.UnixEpoch).First();
        }

        private static DateTimeOffset? ReadUnix(Dictionary<string, string> data, string key)
        {
            if (data == null ||
                !data.TryGetValue(key, out string raw) ||
                !long.TryParse(raw, out long value) ||
                value <= 0)
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(value);
            }
            catch
            {
                return null;
            }
        }

        private static DateTimeOffset? FromUnix(int value)
        {
            if (value <= 0)
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(value);
            }
            catch
            {
                return null;
            }
        }

        private static string MakeVisitorKey(string principalId, string homeGrid)
        {
            return (principalId ?? string.Empty) + "|" +
                   (homeGrid ?? string.Empty).Trim().TrimEnd('/');
        }

        private static bool HasCredentials(IOSHttpRequest request)
        {
            if (request?.Headers == null)
                return false;

            string bearer = request.Headers["Authorization"];
            string apiKey = request.Headers["X-NexVerse-Api-Key"];

            return !string.IsNullOrWhiteSpace(bearer) ||
                   !string.IsNullOrWhiteSpace(apiKey);
        }

        private static object OnlinePayload(OnlineRecord record)
        {
            return new
            {
                type = record.Type,
                principal_id = record.PrincipalId,
                name = record.Name,
                home_grid = string.IsNullOrWhiteSpace(record.HomeGrid) ? null : record.HomeGrid,
                region_id = record.RegionId.IsZero() ? null : record.RegionId.ToString(),
                region_name = record.RegionName,
                login_at = record.LoginAt
            };
        }

        private static string Correlation(IOSHttpResponse response)
        {
            string id = Guid.NewGuid().ToString("N");
            response.AddHeader("X-Correlation-Id", id);
            response.AddHeader("X-NexVerse-Api-Version", NexVersePlatform.ApiVersion);
            return id;
        }

        private static void WriteError(
            IOSHttpResponse response,
            HttpStatusCode status,
            string error,
            string message)
        {
            string correlationId = Correlation(response);
            WriteJson(response, new { error, message, correlation_id = correlationId }, status);
        }

        private static void WriteJson(
            IOSHttpResponse response,
            object payload,
            HttpStatusCode status = HttpStatusCode.OK)
        {
            response.KeepAlive = false;
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.RawBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, s_Json));
        }

        private sealed class ActivityRecord
        {
            public string PrincipalId;
            public bool IsHypergrid;
            public string HomeGrid;
            public string DisplayName;
            public DateTimeOffset? LoginAt;
            public DateTimeOffset? LogoutAt;
            public bool Online;
            public UUID LastRegionId;
            public string VisitorKey;

            public DateTimeOffset? LastActivityAt
            {
                get
                {
                    if (LoginAt.HasValue && LogoutAt.HasValue)
                        return LoginAt.Value >= LogoutAt.Value ? LoginAt : LogoutAt;

                    return LoginAt ?? LogoutAt;
                }
            }
        }

        private sealed class OnlineRecord
        {
            public string Type;
            public string PrincipalId;
            public string Name;
            public string HomeGrid;
            public UUID RegionId;
            public string RegionName;
            public DateTimeOffset? LoginAt;
        }
    }
}
