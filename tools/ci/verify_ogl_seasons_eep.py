#!/usr/bin/env python3
"""Safety contract for opt-in nonpersistent seasons EEP and landscape visuals."""
from pathlib import Path
root = Path("addons/OGLSeasons")
overlay = (root/"OglSeasonalEepOverlay.cs").read_text(encoding="utf-8")
module = (root/"OglSeasonsRegionModule.cs").read_text(encoding="utf-8")
landscape = (root/"SeasonalLandscape.lsl").read_text(encoding="utf-8")
config = (root/"OGLSeasons.ini.example").read_text(encoding="utf-8")
ci = Path(".github/workflows/nexjast-ci.yml").read_text(encoding="utf-8")
for marker in ("EnableEepOverlay = false", "AllowCustomRegionEep = false"):
    assert marker in config, marker
for marker in ('settings.GetBoolean("EnableEepOverlay", false)',
               'settings.GetBoolean("AllowCustomRegionEep", false)',
               "m_EepOverlay?.Restore()", "m_EepOverlay.Apply(next)"):
    assert marker in module, marker
for marker in ("RegionEnvironment = overlay", "ReferenceEquals(current, m_Applied)",
               "ReferenceEquals(m_Scene.RegionEnvironment, m_Applied)",
               "GetRegionEnvironment()", "m_Scene.RegionEnvironment = m_Previous",
               "WindlightRefresh(10)", 'sky["cloud_shadow"]',
               'sky["cloud_color"]', 'sky["cloud_variance"]',
               'sky.TryGetValue("legacy_haze"', "TransformSkyFrames("):
    assert marker in overlay, marker
for forbidden in ("StoreOnRegion(", "StoreRegionEnvironmentSettings(", "SetTerrain",
                  "File.WriteAllText(", "ResetEnvironmentSettings("):
    assert forbidden not in overlay, "EEP overlay must not persist data: " + forbidden
for marker in ("llSetLinkAlpha", "llGetOwner()", "id != NULL_KEY",
               "llGetNumberOfPrims()", "CHANGED_LINK", "setSeason(0)"):
    assert marker in landscape, marker
for forbidden in ("llSetTexture(", "llSetLinkTexture(", "llSetPrimitiveParams(",
                  "llGiveMoney(", "llMessageLinked("):
    assert forbidden not in landscape, forbidden
assert "tools/ci/OglSeasonsEepRegression/OglSeasonsEepRegression.csproj" in ci
assert "tools/ci/verify_ogl_seasons_eep.py" in ci
print("OGL Seasons EEP visual overlay: nonpersistent/opt-in and landscape safety: OK")
