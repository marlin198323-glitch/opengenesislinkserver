#!/usr/bin/env python3
"""Source-contract regression for the public NexVerse citizen world map."""
from pathlib import Path

root=Path("NexVerse/Server/Api")
api=(root/"NexCitizenWorldMapApi.cs").read_text(encoding="utf-8")
page=(root/"NexCitizenWorldMapPage.cs").read_text(encoding="utf-8")
connector=(root/"NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")
openapi=(root/"NexVerseWorldApiHandlers.cs").read_text(encoding="utf-8")
docs=Path("doc/NexVerse/WORLD_API.md").read_text(encoding="utf-8")
ci=Path(".github/workflows/nexjast-ci.yml").read_text(encoding="utf-8")

for item in (
    '"/api/v1/world-map"',
    '"/api/v1/world-map/view"',
    "apiGate.Wrap(citizenWorldMap.Handle)",
    "new NexCitizenWorldMapApi(grid, teleportBaseUri)",
):
    assert item in connector or item in api, item

for item in (
    "MaxWindowAxis = 16",
    "GetRegionRange(UUID.Zero,",
    "GetRegionsByName(UUID.Zero, search, 32)",
    "OpenSim.Framework.RegionFlags.Reservation",
    "OpenSim.Framework.RegionFlags.RegionOnline",
    "Uri.EscapeDataString(name)",
    'root.Scheme != "http"',
    "correlation_id",
    "NexApiRequestContext.Ensure(response)",
):
    assert item in api, item

assert '"/api/v1/world-map/view"' in api
assert "server_uri" not in api
assert "node_id" not in api
assert "owner_id" not in api
assert 'm_Grid.GetRegionRange(UUID.Zero, fromX, toX, fromY, toY)' in api

for item in (
    "CitizenWorldMapRegion",
    "CitizenWorldMapResponse",
    'ApplyJsonContract(paths, "/api/v1/world-map"',
    '["/api/v1/world-map/view"] = GetOperation',
):
    assert item in openapi, item

for item in (
    "Weltkarte",
    "world-map",
    "maplibre-gl@6.15.0",
    "new maplibregl.Map(",
    "'/api/v1/deutschland/xyz/{z}/{x}/{y}.jpg'",
    "minzoom:17,maxzoom:24",
    "fetch(apiRoot+",
    "teleport_uri",
    "encodeURIComponent(q)",
    ".textContent",
    "rel=\"noopener noreferrer\"",
):
    assert item in page, item

# MapLibre movement triggers bounded metadata updates on moveend, not mousemove.
assert "map.on('moveend',viewport)" in page
assert "map.on('mousemove',e=>" in page
assert "innerHTML" not in page, "Region names must never enter HTML unsanitized"
# Public Deutschland tile proxy is strictly bounded and must not disclose
# private region or privileged placement data.
for item in (
    '"/api/v1/deutschland"',
    '"/api/v1/deutschland/view"',
    '"/api/v1/deutschland/tiles/"',
    '"/api/v1/deutschland/xyz/"',
    "HandleXyzTile(path, response)",
    "const long gridShift = 1L << 22",
    "zoom < 1 || zoom > 8",
    "x % span != 0 || y % span != 0",
    "GetMapTile(fileName, UUID.Zero",
    "GetRegionRange(UUID.Zero, searchX, maxX, searchY, maxY)",
    "Overlaps(r, minX, maxX, minY, maxY)",
    "MaxRegionAxisCells = 16",
    "map_image_service_unavailable",
    "map_tile_not_found",
):
    assert item in api, item
assert '"/api/v1/deutschland"' in connector
assert "ConfigureTiles(LoadOptionalService<IMapImageService>" in connector
assert "apiRoot+'?q='" in page
# MapLibre consumes the XYZ tile source directly; the metadata response
# retains tile_url_template for external/non-MapLibre portal clients.
assert "tile_url_template = tileTemplate" in api
assert "'/api/v1/deutschland/xyz/{z}/{x}/{y}.jpg'" in page
for item in ('["/api/v1/deutschland"]', '["/api/v1/deutschland/view"]',
             '["/api/v1/deutschland/tiles/{zoom}/{x}/{y}.jpg"]'):
    assert item in openapi, item
assert 'id="gridShowWarp3d"' in (root/"NexApiDocsPage.cs").read_text(encoding="utf-8")
assert "applyGridImagery" in (root/"NexApiDocsPage.cs").read_text(encoding="utf-8")

assert "GET /api/v1/world-map" in docs
assert "verify_citizen_world_map.py" in ci
print("Citizen world-map API, portal view, security boundaries and CI contract: OK")
