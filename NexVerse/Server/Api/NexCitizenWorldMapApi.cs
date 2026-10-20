// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Public, read-only map projection for the NexVerse citizen portal.
    /// Unlike /grid/layout this never exposes simulator endpoints, node IDs,
    /// reservation records, owners or administrative placement metadata.
    /// Coordinates are expressed in 256-meter grid cells, not meters.
    /// </summary>
    internal sealed class NexCitizenWorldMapApi
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(NexCitizenWorldMapApi));
        private const int CellMeters = 256;
        private const int MaxWindowAxis = 16;
        private const int MaxRegionAxisCells = 16; // up to 4096m VarRegions
        private const int MaxCellCoordinate = (int.MaxValue - (CellMeters - 1)) / CellMeters;
        private readonly IGridService m_Grid;
        private IMapImageService m_MapImages;
        private readonly string m_TeleportBaseUri;

        public NexCitizenWorldMapApi(IGridService grid, string teleportBaseUri)
        {
            m_Grid = grid;
            m_TeleportBaseUri = (teleportBaseUri ?? string.Empty).TrimEnd('/');
        }

        // MapImageService publishes Warp3D imagery already uploaded by simulators.
        // A missing optional map service must not break the existing World API.
        internal void ConfigureTiles(IMapImageService mapImages) => m_MapImages = mapImages;

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (!string.Equals(request?.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "GET");
                Json(response, HttpStatusCode.MethodNotAllowed, new { error = "method_not_allowed" });
                return;
            }

            string path = (request.UriPath ?? string.Empty).TrimEnd('/');
            bool deutschland = path.StartsWith("/api/v1/deutschland", StringComparison.OrdinalIgnoreCase);
            if (deutschland &&
                path.StartsWith("/api/v1/deutschland/tiles/", StringComparison.OrdinalIgnoreCase))
            {
                HandleTile(path, response);
                return;
            }

            if (path.Equals("/api/v1/world-map/view", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/v1/deutschland/view", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/html; charset=utf-8";
                response.AddHeader("Cache-Control", "no-store");
                response.AddHeader("X-Content-Type-Options", "nosniff");
                response.RawBuffer = Encoding.UTF8.GetBytes(NexCitizenWorldMapPage.Html);
                return;
            }

            if (!path.Equals("/api/v1/world-map", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/deutschland", StringComparison.OrdinalIgnoreCase))
            {
                Json(response, HttpStatusCode.NotFound, new { error = "not_found" });
                return;
            }
            string tileTemplate = deutschland
                ? "/api/v1/deutschland/tiles/1/{x}/{y}.jpg"
                : "/map/map-1-{x}-{y}-objects.jpg";

            if (m_Grid == null)
            {
                Json(response, HttpStatusCode.ServiceUnavailable, new { error = "grid_service_unavailable" });
                return;
            }

            try
            {
                string search = (request.QueryString?["q"] ?? string.Empty).Trim();
                if (search.Length > 80)
                {
                    Json(response, HttpStatusCode.BadRequest, new { error = "invalid_query", message = "q must be at most 80 characters." });
                    return;
                }

                if (!string.IsNullOrEmpty(search))
                {
                    if (search.Length < 2)
                    {
                        Json(response, HttpStatusCode.BadRequest, new { error = "invalid_query", message = "q must contain at least two characters." });
                        return;
                    }
                    GridRegion[] matches = (m_Grid.GetRegionsByName(UUID.Zero, search, 32) ?? new List<GridRegion>())
                        .Where(Visible).Take(32).ToArray();
                    Json(response, HttpStatusCode.OK, new
                    {
                        cell_size_meters = CellMeters,
                        mode = "search",
                        query = search,
                        count = matches.Length,
                        tile_url_template = tileTemplate,
                        regions = matches.Select(PublicRegion).ToArray(),
                        correlation_id = NexApiRequestContext.Ensure(response)
                    });
                    return;
                }

                bool anyBounds = new[] { "min_x", "max_x", "min_y", "max_y" }
                    .Any(key => request.QueryString?[key] != null);
                int minX, maxX, minY, maxY;
                if (anyBounds)
                {
                    if (!Cell(request, "min_x", out minX) || !Cell(request, "max_x", out maxX) ||
                        !Cell(request, "min_y", out minY) || !Cell(request, "max_y", out maxY) ||
                        minX > maxX || minY > maxY || maxX - minX + 1 > MaxWindowAxis ||
                        maxY - minY + 1 > MaxWindowAxis)
                    {
                        Json(response, HttpStatusCode.BadRequest, new
                        {
                            error = "invalid_map_bounds",
                            message = "Provide all four non-negative grid-cell bounds, each axis spanning at most 16 cells."
                        });
                        return;
                    }
                }
                else
                {
                    GridRegion initial = (m_Grid.GetDefaultRegions(UUID.Zero) ?? new List<GridRegion>())
                        .FirstOrDefault(Visible);
                    if (initial == null)
                    {
                        Json(response, HttpStatusCode.OK, new
                        {
                            cell_size_meters = CellMeters,
                            mode = "viewport",
                            has_default_region = false,
                            tile_url_template = tileTemplate,
                            max_window_axis = MaxWindowAxis,
                            zoom_levels = 8,
                            regions = Array.Empty<object>(),
                            correlation_id = NexApiRequestContext.Ensure(response)
                        });
                        return;
                    }
                    int centerX = initial.RegionLocX / CellMeters;
                    int centerY = initial.RegionLocY / CellMeters;
                    minX = Math.Max(0, centerX - 5);
                    minY = Math.Max(0, centerY - 5);
                    maxX = Math.Min(MaxCellCoordinate, minX + 11);
                    maxY = Math.Min(MaxCellCoordinate, minY + 11);
                }

                long cellMinX = (long)minX * CellMeters;
                long cellMinY = (long)minY * CellMeters;
                long cellMaxX = (long)maxX * CellMeters + CellMeters - 1;
                long cellMaxY = (long)maxY * CellMeters + CellMeters - 1;
                // The underlying GridService searches region ORIGINS, not complete
                // VarRegion footprints. Query a bounded south/west margin and
                // intersect the actual 256..4096m footprint with the viewport.
                int fromX = checked(Math.Max(0, minX - MaxRegionAxisCells + 1) * CellMeters);
                int fromY = checked(Math.Max(0, minY - MaxRegionAxisCells + 1) * CellMeters);
                int toX = checked(maxX * CellMeters + CellMeters - 1);
                int toY = checked(maxY * CellMeters + CellMeters - 1);
                GridRegion[] regions = (m_Grid.GetRegionRange(UUID.Zero, fromX, toX, fromY, toY) ??
                    new List<GridRegion>()).Where(r => Visible(r) &&
                        Overlaps(r, cellMinX, cellMaxX, cellMinY, cellMaxY))
                    .GroupBy(r => r.RegionID).Select(g => g.First())
                    .Take(256).ToArray();

                Json(response, HttpStatusCode.OK, new
                {
                    cell_size_meters = CellMeters,
                    mode = "viewport",
                    has_default_region = true,
                    bounds = new { min_x = minX, max_x = maxX, min_y = minY, max_y = maxY },
                    tile_url_template = tileTemplate,
                    max_window_axis = MaxWindowAxis,
                    zoom_levels = 8,
                    regions = regions.Select(PublicRegion).ToArray(),
                    count = regions.Length,
                    correlation_id = NexApiRequestContext.Ensure(response)
                });
            }
            catch (Exception e)
            {
                m_Log.Error("[NEX-WORLD-MAP]: Failed to resolve public map viewport.", e);
                Json(response, HttpStatusCode.ServiceUnavailable, new { error = "world_map_unavailable" });
            }
        }

        // Zoom=1 is one 256m grid cell; levels 2..8 are the native
        // OpenSimulator multi-resolution files (2,4,8,...,128 cells per tile).
        // Never interpret the coordinates as geographic longitude/latitude.
        private void HandleTile(string path, IOSHttpResponse response)
        {
            const string prefix = "/api/v1/deutschland/tiles/";
            string[] parts = path.Substring(prefix.Length).Split('/');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int zoom) || zoom < 1 || zoom > 8 ||
                !int.TryParse(parts[1], out int x) ||
                !parts[2].EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(parts[2].Substring(0, parts[2].Length - 4), out int y))
            {
                Json(response, HttpStatusCode.BadRequest, new { error = "invalid_map_tile" });
                return;
            }
            int span = 1 << (zoom - 1);
            if (x < 0 || y < 0 || x > MaxCellCoordinate - span + 1 ||
                y > MaxCellCoordinate - span + 1 || x % span != 0 || y % span != 0)
            {
                Json(response, HttpStatusCode.BadRequest, new { error = "invalid_map_tile_bounds" });
                return;
            }
            if (m_Grid == null || m_MapImages == null)
            {
                Json(response, HttpStatusCode.ServiceUnavailable, new { error = "map_image_service_unavailable" });
                return;
            }
            try
            {
                // Expose only already-public active grid regions, never arbitrary
                // filesystem paths, unknown scopes or private/reserved grid cells.
                int minX = checked(x * CellMeters);
                int minY = checked(y * CellMeters);
                int maxX = checked((x + span) * CellMeters - 1);
                int maxY = checked((y + span) * CellMeters - 1);
                // Preserve tiles in the non-origin cells of VarRegions.
                int searchX = Math.Max(0, (x - MaxRegionAxisCells + 1) * CellMeters);
                int searchY = Math.Max(0, (y - MaxRegionAxisCells + 1) * CellMeters);
                bool available = (m_Grid.GetRegionRange(UUID.Zero, searchX, maxX, searchY, maxY) ??
                    new List<GridRegion>()).Any(r => Visible(r) &&
                        Overlaps(r, minX, maxX, minY, maxY));
                if (!available)
                {
                    Json(response, HttpStatusCode.NotFound, new { error = "map_tile_not_found" });
                    return;
                }

                string fileName = "map-" + zoom + "-" + x + "-" + y + "-objects.jpg";
                byte[] data = m_MapImages.GetMapTile(fileName, UUID.Zero, out string format);
                if (data == null || data.Length == 0 || data.Length > 2 * 1024 * 1024 ||
                    !string.Equals(format, ".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    Json(response, HttpStatusCode.NotFound, new { error = "map_tile_not_found" });
                    return;
                }
                response.KeepAlive = false;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "image/jpeg";
                response.AddHeader("Cache-Control", "public, max-age=60");
                response.AddHeader("X-Content-Type-Options", "nosniff");
                response.RawBuffer = data;
            }
            catch (Exception e)
            {
                m_Log.Error("[NEX-DEUTSCHLAND-MAP]: Tile retrieval failed.", e);
                Json(response, HttpStatusCode.ServiceUnavailable, new { error = "map_tile_unavailable" });
            }
        }

        private static bool Overlaps(GridRegion region,
            long minX, long maxX, long minY, long maxY)
        {
            long left = region.RegionLocX;
            long bottom = region.RegionLocY;
            long right = left + Math.Max(CellMeters, region.RegionSizeX) - 1L;
            long top = bottom + Math.Max(CellMeters, region.RegionSizeY) - 1L;
            return left <= maxX && right >= minX && bottom <= maxY && top >= minY;
        }

        private static bool Cell(IOSHttpRequest request, string name, out int value)
        {
            value = 0;
            return int.TryParse(request.QueryString?[name], out value) &&
                   value >= 0 && value <= MaxCellCoordinate;
        }

        private static bool Visible(GridRegion region)
        {
            if (region == null || region.RegionID.IsZero() ||
                string.IsNullOrWhiteSpace(region.RegionName))
                return false;

            if (region.RegionFlags.HasValue)
            {
                int flags = (int)region.RegionFlags.Value;
                if ((flags & (int)OpenSim.Framework.RegionFlags.Reservation) != 0 ||
                    (flags & (int)OpenSim.Framework.RegionFlags.RegionOnline) == 0)
                    return false;
            }
            return true;
        }

        private object PublicRegion(GridRegion region)
        {
            int sizeX = region.RegionSizeX > 0 ? region.RegionSizeX : CellMeters;
            int sizeY = region.RegionSizeY > 0 ? region.RegionSizeY : CellMeters;
            return new
            {
                region_id = region.RegionID.ToString(),
                name = region.RegionName,
                grid_x = region.RegionLocX / CellMeters,
                grid_y = region.RegionLocY / CellMeters,
                cells_x = Math.Max(1, (sizeX + CellMeters - 1) / CellMeters),
                cells_y = Math.Max(1, (sizeY + CellMeters - 1) / CellMeters),
                size_x = sizeX,
                size_y = sizeY,
                maturity = Math.Max(0, Math.Min(2, region.Maturity)),
                teleport_uri = TeleportUri(region.RegionName)
            };
        }

        private string TeleportUri(string name)
        {
            if (string.IsNullOrWhiteSpace(m_TeleportBaseUri) ||
                !Uri.TryCreate(m_TeleportBaseUri, UriKind.Absolute, out Uri root) ||
                (root.Scheme != "http" && root.Scheme != "https" &&
                 root.Scheme != "hop" && root.Scheme != "secondlife"))
                return string.Empty;

            return m_TeleportBaseUri + "/" + Uri.EscapeDataString(name) + "/128/128/25";
        }

        private static void Json(IOSHttpResponse response, HttpStatusCode status, object payload)
        {
            response.KeepAlive = false;
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "public, max-age=20");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }
    }
}
