// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace NexVerse.RegionModules.Discovery
{
    [Extension(
        Path = "/OpenSim/RegionModules",
        NodeName = "RegionModule",
        Id = "NexDiscoveryViewerModule")]
    public sealed class NexDiscoveryViewerModule :
        ISharedRegionModule
    {
        private static readonly ILog m_Log =
            LogManager.GetLogger(
                typeof(NexDiscoveryViewerModule));

        private readonly object m_Sync = new object();
        private readonly List<Scene> m_Scenes =
            new List<Scene>();

        private bool m_Enabled;
        private bool m_LegacyFirestormCompatibility;
        private string m_WorldApiBaseUrl = string.Empty;
        // Backend-only group land credential; never exposed to viewers/CAPS.
        private string m_GroupLandApiKey = string.Empty;
        private int m_RequestTimeoutMilliseconds = 3000;
        private HttpClient m_Http;

        public string Name => "NexDiscoveryViewerModule";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource config)
        {
            IConfig section =
                config?.Configs["NexDiscoveryViewer"];

            if (section == null ||
                !section.GetBoolean("Enabled", false))
            {
                m_Enabled = false;
                return;
            }

            m_WorldApiBaseUrl =
                section.GetString(
                        "WorldApiBaseUrl",
                        string.Empty)
                    .Trim()
                    .TrimEnd('/');

            m_GroupLandApiKey = section.GetString(
                "GroupLandApiKey", string.Empty).Trim();

            m_LegacyFirestormCompatibility =
                section.GetBoolean(
                    "LegacyFirestormCompatibility",
                    true);

            m_RequestTimeoutMilliseconds =
                Math.Clamp(
                    section.GetInt(
                        "RequestTimeoutMilliseconds",
                        3000),
                    500,
                    30000);

            if (!Uri.TryCreate(
                    m_WorldApiBaseUrl,
                    UriKind.Absolute,
                    out Uri baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp &&
                 baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "[NEX-DISCOVERY]: WorldApiBaseUrl must be an absolute HTTP(S) URL.");
            }

            m_Http =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromMilliseconds(
                            m_RequestTimeoutMilliseconds)
                };

            m_Enabled = true;

            m_Log.InfoFormat(
                "[NEX-DISCOVERY]: Viewer adapter enabled for {0}; Firestorm legacy bridge={1}.",
                m_WorldApiBaseUrl,
                m_LegacyFirestormCompatibility);
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || scene == null)
                return;

            lock (m_Sync)
                m_Scenes.Add(scene);

            scene.EventManager.OnRegisterCaps +=
                OnRegisterCaps;

            if (m_LegacyFirestormCompatibility)
            {
                scene.EventManager.OnMakeRootAgent +=
                    OnMakeRootAgent;
                scene.EventManager.OnMakeChildAgent +=
                    OnMakeChildAgent;
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene == null)
                return;

            scene.EventManager.OnRegisterCaps -=
                OnRegisterCaps;
            scene.EventManager.OnMakeRootAgent -=
                OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent -=
                OnMakeChildAgent;

            lock (m_Sync)
                m_Scenes.Remove(scene);
        }

        public void Close()
        {
            lock (m_Sync)
            {
                foreach (Scene scene in m_Scenes)
                {
                    scene.EventManager.OnRegisterCaps -=
                        OnRegisterCaps;
                    scene.EventManager.OnMakeRootAgent -=
                        OnMakeRootAgent;
                    scene.EventManager.OnMakeChildAgent -=
                        OnMakeChildAgent;
                }

                m_Scenes.Clear();
            }

            m_Http?.Dispose();
            m_Http = null;
        }

        private void OnMakeRootAgent(ScenePresence presence)
        {
            IClientAPI client = presence?.ControllingClient;
            if (!m_Enabled ||
                !m_LegacyFirestormCompatibility ||
                client == null)
            {
                return;
            }

            // Defensive detach first so a rootification retry cannot duplicate handlers.
            DetachLegacyHandlers(client);
            client.OnDirPlacesQuery += OnDirPlacesQuery;
            client.OnPlacesQuery += OnPlacesQuery;
            client.OnDirLandQuery += OnDirLandQuery;
            client.OnDirClassifiedQuery += OnDirClassifiedQuery;
        }

        private void OnMakeChildAgent(ScenePresence presence)
        {
            IClientAPI client = presence?.ControllingClient;
            if (client != null)
                DetachLegacyHandlers(client);
        }

        private void DetachLegacyHandlers(IClientAPI client)
        {
            client.OnDirPlacesQuery -= OnDirPlacesQuery;
            client.OnPlacesQuery -= OnPlacesQuery;
            client.OnDirLandQuery -= OnDirLandQuery;
            client.OnDirClassifiedQuery -= OnDirClassifiedQuery;
        }

        // Firestorm's Groups > Land & NV$ tab does not use DirPlacesQuery.
        // It uses PlacesQuery, QueryID=group UUID and DFQ_GROUP_OWNED (0x100).
        // Query normal directory searches through the existing separate route.
        // This adapter can enumerate parcels on regions hosted in this
        // simulator process. Grid-wide land would require a separate trusted
        // cross-simulator/central land directory.
        private void OnPlacesQuery(
            UUID queryID, UUID transactionID, string queryText,
            uint queryFlags, byte category, string simName, IClientAPI client)
        {
            const uint GroupOwnedSearch = 1u << 8;
            if ((queryFlags & GroupOwnedSearch) == 0 ||
                queryID.IsZero() || client == null || !client.IsActive ||
                client.Scene is not Scene current)
                return;

            // Firestorm sends the actual group UUID in QueryID. Check
            // membership through the authoritative region groups module,
            // never through name, supplied query flags or cached viewer state.
            IGroupsModule groupModule = current.RequestModuleInterface<IGroupsModule>();
            if (groupModule == null ||
                groupModule.GetMembershipData(queryID, client.AgentId) == null)
            {
                // No disclosure of land to nonmembers.
                client.SendPlacesReply(queryID, transactionID, new[] {
                    new PlacesReplyData { OwnerID = UUID.Zero,
                        Name = string.Empty, Desc = string.Empty,
                        SimName = string.Empty }
                });
                return;
            }

            try
            {
                Scene[] scenes;
                lock (m_Sync)
                    scenes = m_Scenes.ToArray();

                List<PlacesReplyData> rows = new List<PlacesReplyData>();
                int contributed = 0;
                List<GroupMembersData> members =
                    groupModule.GroupMembersRequest(client, queryID);
                if (members != null)
                    foreach (GroupMembersData member in members)
                        contributed = checked(contributed + Math.Max(0, member.Contribution));

                // The first row is the Firestorm "total land contribution"
                // special record. It must be present even for zero parcels.
                rows.Add(new PlacesReplyData {
                    OwnerID = UUID.Zero, Name = string.Empty,
                    Desc = string.Empty, ActualArea = contributed,
                    BillableArea = 0, SimName = string.Empty
                });

                // Prefer the authoritative grid-wide SQL directory, which
                // includes other simulator processes. Only the trusted
                // simulator may use the explicit groups:read machine key;
                // the avatar's membership was checked above.
                if (!string.IsNullOrEmpty(m_GroupLandApiKey))
                {
                    try
                    {
                        using JsonDocument remote = GetGroupLandJson(queryID);
                        if (!remote.RootElement.TryGetProperty(
                                "parcels", out JsonElement list) ||
                            list.ValueKind != JsonValueKind.Array)
                            throw new InvalidOperationException(
                                "Group land response has no parcels list.");

                        foreach (JsonElement parcel in list.EnumerateArray())
                        {
                            rows.Add(new PlacesReplyData {
                                OwnerID = TryUuid(parcel, "owner_id", out UUID owner)
                                    ? owner : UUID.Zero,
                                Name = String(parcel, "name"),
                                Desc = String(parcel, "description"),
                                ActualArea = Int32(parcel, "area"),
                                BillableArea = Int32(parcel, "area"),
                                GlobalX = UInt32(parcel, "global_x"),
                                GlobalY = UInt32(parcel, "global_y"),
                                GlobalZ = UInt32(parcel, "global_z"),
                                SimName = String(parcel, "region_name"),
                                SnapshotID = TryUuid(parcel, "snapshot_id", out UUID snapshot)
                                    ? snapshot : UUID.Zero,
                                Price = Int32(parcel, "price"),
                                Dwell = 0
                            });
                        }
                        client.SendPlacesReply(queryID, transactionID, rows.ToArray());
                        return;
                    }
                    catch (Exception e)
                    {
                        m_Log.WarnFormat(
                            "[NEX-DISCOVERY]: Grid-wide group land lookup failed for {0}; falling back to this simulator only: {1}",
                            queryID, e.Message);
                    }
                }

                foreach (Scene scene in scenes)
                {
                    if (scene?.LandChannel == null)
                        continue;
                    List<ILandObject> parcels = scene.LandChannel.AllParcels();
                    if (parcels == null)
                        continue;
                    foreach (ILandObject parcel in parcels)
                    {
                        LandData land = parcel?.LandData;
                        if (land == null || !land.IsGroupOwned ||
                            land.GroupID != queryID)
                            continue;
                        rows.Add(new PlacesReplyData {
                            OwnerID = land.OwnerID,
                            Name = land.Name ?? string.Empty,
                            Desc = land.Description ?? string.Empty,
                            ActualArea = land.Area,
                            BillableArea = land.Area,
                            GlobalX = scene.RegionInfo.WorldLocX +
                                (uint)Math.Max(0, (int)land.UserLocation.X),
                            GlobalY = scene.RegionInfo.WorldLocY +
                                (uint)Math.Max(0, (int)land.UserLocation.Y),
                            GlobalZ = (uint)Math.Max(0, (int)land.UserLocation.Z),
                            SimName = scene.RegionInfo.RegionName,
                            SnapshotID = land.SnapshotID,
                            Dwell = 0, Price = land.SalePrice
                        });
                    }
                }
                client.SendPlacesReply(queryID, transactionID, rows.ToArray());
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-DISCOVERY]: Firestorm group-owned land query {0} failed: {1}",
                    queryID, e.Message);
                // Always complete the request with the expected IDs.
                client.SendPlacesReply(queryID, transactionID, new[] {
                    new PlacesReplyData { OwnerID = UUID.Zero,
                        Name = string.Empty, Desc = string.Empty,
                        SimName = string.Empty }
                });
            }
        }

        private void OnDirPlacesQuery(
            IClientAPI remoteClient,
            UUID queryId,
            string queryText,
            int queryFlags,
            int category,
            string simName,
            int queryStart)
        {
            try
            {
                string path =
                    "/api/v1/places?q=" +
                    Escape(queryText) +
                    "&offset=" +
                    Math.Max(0, queryStart).ToString(CultureInfo.InvariantCulture) +
                    "&limit=100";

                using JsonDocument document = GetJson(path);
                List<DirPlacesReplyData> replies =
                    new List<DirPlacesReplyData>();

                if (document.RootElement.TryGetProperty(
                        "places",
                        out JsonElement places) &&
                    places.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement place in places.EnumerateArray())
                    {
                        if (!TryUuid(place, "parcel_id", out UUID parcelId))
                            continue;

                        string regionName = String(place, "region_name");
                        if (!string.IsNullOrWhiteSpace(simName) &&
                            !regionName.Contains(
                                simName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        replies.Add(
                            new DirPlacesReplyData
                            {
                                parcelID = parcelId,
                                name = String(place, "name"),
                                forSale = false,
                                auction = false,
                                dwell = Float(place, "traffic"),
                                Status = 0
                            });
                    }
                }

                remoteClient.SendDirPlacesReply(
                    queryId,
                    replies.ToArray());
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-DISCOVERY]: Firestorm Places query failed: {0}",
                    e.Message);
                remoteClient.SendDirPlacesReply(
                    queryId,
                    Array.Empty<DirPlacesReplyData>());
            }
        }

        private void OnDirLandQuery(
            IClientAPI remoteClient,
            UUID queryId,
            uint queryFlags,
            uint searchType,
            int price,
            int area,
            int queryStart)
        {
            try
            {
                string path =
                    "/api/v1/land-portal?type=sale&offset=" +
                    Math.Max(0, queryStart).ToString(CultureInfo.InvariantCulture) +
                    "&limit=100";

                if (price > 0)
                {
                    path +=
                        "&max_price=" +
                        price.ToString(CultureInfo.InvariantCulture);
                }

                if (area > 0)
                {
                    path +=
                        "&min_area=" +
                        area.ToString(CultureInfo.InvariantCulture);
                }

                using JsonDocument document = GetJson(path);
                List<DirLandReplyData> replies =
                    new List<DirLandReplyData>();

                if (document.RootElement.TryGetProperty(
                        "listings",
                        out JsonElement listings) &&
                    listings.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement listing in listings.EnumerateArray())
                    {
                        if (!TryUuid(listing, "parcel_id", out UUID parcelId))
                            continue;

                        replies.Add(
                            new DirLandReplyData
                            {
                                parcelID = parcelId,
                                name = String(listing, "parcel_name"),
                                auction = false,
                                forSale = true,
                                salePrice = Int32(listing, "price"),
                                actualArea = Int32(listing, "area")
                            });
                    }
                }

                remoteClient.SendDirLandReply(
                    queryId,
                    replies.ToArray());
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-DISCOVERY]: Firestorm Land query failed: {0}",
                    e.Message);
                remoteClient.SendDirLandReply(
                    queryId,
                    Array.Empty<DirLandReplyData>());
            }
        }

        private void OnDirClassifiedQuery(
            IClientAPI remoteClient,
            UUID queryId,
            string queryText,
            uint queryFlags,
            uint category,
            int queryStart)
        {
            try
            {
                string path =
                    "/api/v1/classifieds?q=" +
                    Escape(queryText) +
                    "&offset=" +
                    Math.Max(0, queryStart).ToString(CultureInfo.InvariantCulture) +
                    "&limit=100";

                using JsonDocument document = GetJson(path);
                List<DirClassifiedReplyData> replies =
                    new List<DirClassifiedReplyData>();

                if (document.RootElement.TryGetProperty(
                        "classifieds",
                        out JsonElement classifieds) &&
                    classifieds.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement classified in classifieds.EnumerateArray())
                    {
                        if (!TryUuid(
                                classified,
                                "classified_id",
                                out UUID classifiedId))
                        {
                            continue;
                        }

                        replies.Add(
                            new DirClassifiedReplyData
                            {
                                classifiedID = classifiedId,
                                name = String(classified, "name"),
                                classifiedFlags = 0,
                                creationDate = UnixSeconds(classified, "created_at"),
                                expirationDate = UnixSeconds(classified, "updated_at"),
                                price = 0,
                                Status = 0
                            });
                    }
                }

                remoteClient.SendDirClassifiedReply(
                    queryId,
                    replies.ToArray());
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-DISCOVERY]: Firestorm Classified query failed: {0}",
                    e.Message);
                remoteClient.SendDirClassifiedReply(
                    queryId,
                    Array.Empty<DirClassifiedReplyData>());
            }
        }

        private JsonDocument GetGroupLandJson(UUID groupId)
        {
            using HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get,
                m_WorldApiBaseUrl + "/api/v1/groups/" +
                Uri.EscapeDataString(groupId.ToString()) + "/land");
            request.Headers.TryAddWithoutValidation(
                "X-NexVerse-Api-Key", m_GroupLandApiKey);
            using HttpResponseMessage response = m_Http.Send(request);
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(
                response.Content.ReadAsStringAsync()
                    .GetAwaiter().GetResult());
        }

        private static uint UInt32(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetUInt32(out uint result) ? result : 0;
        }

        private JsonDocument GetJson(string path)
        {
            using HttpRequestMessage request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    m_WorldApiBaseUrl + path);

            request.Headers.TryAddWithoutValidation(
                "X-NexVerse-Viewer-Agent",
                "Firestorm/OpenSim legacy directory");

            using HttpResponseMessage response =
                m_Http.Send(request);

            response.EnsureSuccessStatusCode();

            string json =
                response.Content
                    .ReadAsStringAsync()
                    .GetAwaiter()
                    .GetResult();

            return JsonDocument.Parse(json);
        }

        private void OnRegisterCaps(
            UUID agentId,
            Caps caps)
        {
            if (!m_Enabled ||
                caps == null)
            {
                return;
            }

            Register(
                caps,
                "NexSearch",
                "/api/v1/search");

            Register(
                caps,
                "NexPlaces",
                "/api/v1/places");

            Register(
                caps,
                "NexLandPortal",
                "/api/v1/land-portal");

            Register(
                caps,
                "NexDestinationGuide",
                "/api/v1/destinations");
        }

        private void Register(
            Caps caps,
            string capability,
            string worldApiPath)
        {
            caps.RegisterSimpleHandler(
                capability,
                new SimpleStreamHandler(
                    "/" + UUID.Random(),
                    (request, response) =>
                        Proxy(
                            request,
                            response,
                            worldApiPath)));
        }

        private void Proxy(
            IOSHttpRequest request,
            IOSHttpResponse response,
            string worldApiPath)
        {
            if (!string.Equals(
                    request?.HttpMethod,
                    "GET",
                    StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode =
                    (int)HttpStatusCode.MethodNotAllowed;
                response.ContentType =
                    "application/json; charset=utf-8";
                response.RawBuffer =
                    System.Text.Encoding.UTF8.GetBytes(
                        "{\"error\":\"method_not_allowed\",\"message\":\"GET is required.\"}");
                return;
            }

            try
            {
                string query =
                    request?.Url?.Query ??
                    string.Empty;

                using HttpRequestMessage outbound =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        m_WorldApiBaseUrl +
                        worldApiPath +
                        query);

                outbound.Headers.TryAddWithoutValidation(
                    "X-NexVerse-Viewer-Agent",
                    capsSafeAgent(request));

                using HttpResponseMessage upstream =
                    m_Http.Send(outbound);

                response.StatusCode =
                    (int)upstream.StatusCode;

                response.ContentType =
                    upstream.Content.Headers.ContentType
                        ?.ToString() ??
                    "application/json; charset=utf-8";

                response.RawBuffer =
                    upstream.Content
                        .ReadAsByteArrayAsync()
                        .GetAwaiter()
                        .GetResult();
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-DISCOVERY]: Viewer capability proxy {0} failed: {1}",
                    worldApiPath,
                    e.Message);

                response.StatusCode =
                    (int)HttpStatusCode.BadGateway;
                response.ContentType =
                    "application/json; charset=utf-8";
                response.RawBuffer =
                    System.Text.Encoding.UTF8.GetBytes(
                        "{\"error\":\"discovery_upstream_unavailable\",\"message\":\"World API discovery service is unavailable.\"}");
            }
        }

        private static string Escape(string value) =>
            Uri.EscapeDataString(
                (value ?? string.Empty).Trim());

        private static string String(
            JsonElement element,
            string name)
        {
            if (!element.TryGetProperty(
                    name,
                    out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return value.GetString() ?? string.Empty;
        }

        private static int Int32(
            JsonElement element,
            string name)
        {
            if (!element.TryGetProperty(
                    name,
                    out JsonElement value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out int intValue))
            {
                return intValue;
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out long longValue))
            {
                return (int)Math.Clamp(
                    longValue,
                    int.MinValue,
                    int.MaxValue);
            }

            return 0;
        }

        private static float Float(
            JsonElement element,
            string name)
        {
            if (element.TryGetProperty(
                    name,
                    out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetSingle(out float result))
            {
                return result;
            }

            return 0f;
        }

        private static bool TryUuid(
            JsonElement element,
            string name,
            out UUID value)
        {
            value = UUID.Zero;

            if (!element.TryGetProperty(
                    name,
                    out JsonElement raw) ||
                raw.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return UUID.TryParse(
                raw.GetString(),
                out value) &&
                !value.IsZero();
        }

        private static uint UnixSeconds(
            JsonElement element,
            string name)
        {
            string raw = String(element, name);
            if (!DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset value))
            {
                return 0;
            }

            long seconds = value.ToUnixTimeSeconds();
            return seconds <= 0
                ? 0
                : seconds >= uint.MaxValue
                    ? uint.MaxValue
                    : (uint)seconds;
        }

        private static string capsSafeAgent(
            IOSHttpRequest request)
        {
            string agent =
                request?.UserAgent ??
                string.Empty;

            return agent.Length <= 255
                ? agent
                : agent.Substring(0, 255);
        }
    }
}
