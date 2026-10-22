#!/usr/bin/env python3
"""R409: source-level integration boundaries; not a production HTTP/E2E test."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
doc = (root / "doc/NexVerse/OPEN_GENESISLINK_WORLD_API_GEGENPRUEFUNG_R409.md").read_text()
routes = (root / "NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()
connector = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
worldmap = (root / "NexVerse/Server/Api/NexCitizenWorldMapApi.cs").read_text()
discovery = (root / "NexVerse/Server/Api/NexDiscoveryApi.cs").read_text()
discovery_store = (root / "NexVerse/Core/Discovery/NexDiscoveryStore.cs").read_text()
profile = (root / "NexVerse/Server/Api/NexWebProfileV3Api.cs").read_text()
registration = (root / "NexVerse/Server/Api/OglPortalRegistrationApi.cs").read_text()
security = (root / "NexVerse/Core/Security/NexSecurity.cs").read_text()
instructions = (root / "NexVerse/Server/Api/NexChatGptInstructionDownloads.cs").read_text()

for letter in "ABCDEFGHIJKLM":
    assert f"## {letter}." in doc, f"required R409 section {letter}"
for rev in ("R397", "R398", "R399", "R400", "R401", "R402", "R403", "R404",
            "R405", "R406", "R407", "R408"):
    assert rev in doc, f"missing Portal revision {rev}"
assert "NICHT_VERIFIZIERBAR" in doc and "nicht in dieser sitzung repliziert" in doc.lower()
for path in ('"/api/v1/deutschland"', '"/api/v1/deutschland/view"',
             '"/api/v1/deutschland/tiles/{zoom}/{x}/{y}.jpg"',
             '"/api/v1/deutschland/xyz/{z}/{x}/{y}.jpg"',
             '"/api/v1/places/{placeId}"',
             '"/api/v1/profiles/{principalId}/image"',
             '"/api/v1/portal/avatars/register"',
             '"/api/v1/portal/avatars/name-availability"'):
    assert path in routes, f"World OpenAPI operation absent: {path}"
for prefix in ('"/api/v1/deutschland"', '"/api/v1/places"',
               '"/api/v1/profiles"'):
    assert prefix in connector, f"World route prefix not registered: {prefix}"
assert '"/api/v1/bug-reports"' not in routes
assert '"/api/v1/bug-reports"' not in connector
assert "GetRegionsByName(UUID.Zero, search, 32)" in worldmap
assert "MaxWindowAxis = 16" in worldmap
assert ".Take(256).ToArray()" in worldmap
assert "response.ContentType = \"image/jpeg\"" in worldmap
assert "m_State.Destinations.RemoveAll(x => x.PlaceId == id)" in discovery_store
assert "m_State.Events.RemoveAll(x => x.PlaceId == id)" in discovery_store
assert "m_State.Classifieds.RemoveAll(x => x.PlaceId == id)" in discovery_store
assert "m_Store.DeletePlace(placeId)" in discovery
assert "HttpStatusCode.NotFound" in profile and "profile.PublishProfile" in profile
assert "OpenJPEG.DecodeToImage" in profile
assert 'last = "Resident";' in registration
assert "NexScopes.UsersRegister" in registration
assert "userLevel >= adminMinimumLevel" in security
assert "NexScopes.AdminAll" in security
assert "R409 P0:" in instructions
assert "keine truncation-Metadaten" in instructions
assert "Portal-Bugreport-Empfaenger" in instructions
assert "R409" in (root / "README.md").read_text()
assert "OPEN_GENESISLINK_WORLD_API_GEGENPRUEFUNG_R409.md" in (root / "doc/NexVerse/WORLD_API.md").read_text()
print("R409 static World/Portal route and security boundary audit: OK (NOT E2E)")
