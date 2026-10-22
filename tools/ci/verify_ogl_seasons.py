#!/usr/bin/env python3
"""Feature boundary and build wiring for opt-in OGL seasons addon."""
from pathlib import Path
model = Path("addons/OGLSeasons/SeasonWeatherModel.cs").read_text(encoding="utf-8")
module = Path("addons/OGLSeasons/OglSeasonsRegionModule.cs").read_text(encoding="utf-8")
config = Path("addons/OGLSeasons/OGLSeasons.ini.example").read_text(encoding="utf-8")
emitter = Path("addons/OGLSeasons/WeatherEmitter.lsl").read_text(encoding="utf-8")
build = Path("addon-modules/OGLSeasons/build.sh").read_text(encoding="utf-8")
prebuild = Path("runprebuild.sh").read_text(encoding="utf-8")
ci = Path(".github/workflows/nexjast-ci.yml").read_text(encoding="utf-8")
for marker in ("ResolveSeason(", "Europe/Berlin", "WeatherIntervalMinutes",
               "OglWeather.Schneefall", "OglWeather.Sturm", "OglWeather.Gewitter",
               "humidity", "OGLSEASONS|1|"):
    assert marker.lower() in (model+module).lower(), marker
assert 'settings?.GetBoolean("Enabled", false)' in module, "must be disabled by default"
for marker in ("INonSharedRegionModule", "IWorldComm", "OnFrame += OnFrame",
               "OnFrame -= OnFrame", "RegisterModuleInterface<IOglSeasonsService>",
               "UnregisterModuleInterface<IOglSeasonsService>", "DeliverMessage("):
    assert marker in module, marker
assert "Enabled = false" in config
assert "AllowedRegions" in config
assert 'WeatherZone = ""' in config and "TemperatureOffsetC = 0" in config
assert 'weatherZone.Trim().Normalize(NormalizationForm.FormKC)' in model
assert 'new DateTimeOffset(localStart, local.Offset)' in model
assert 'settings.GetString("WeatherZone", "")' in module
assert 'settings.GetInt("TemperatureOffsetC", 0)' in module
assert "llParticleSystem([])" in emitter
assert "llListen(CHANNEL, SOURCE_NAME" in emitter
assert "OGLSEASONS" in emitter
assert "id != NULL_KEY" in emitter
assert "if (count > 5) count = 5;" in emitter
assert 'm_weather == "sturm"' in emitter
# Region modules must explicitly depend on the add-in root defining
# /OpenSim/RegionModules. Otherwise Mono.Addins silently skips discovery.
assert '[assembly: Addin("OGLSeasons", Namespace = "OpenGenesisLINK", Version = "0.1.1")]' in module
assert '[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]' in module, "framework dependency must be globally qualified, not OpenGenesisLINK.OpenSim.Region.Framework"
assert 'AddinDependency("OpenSim.Region.Framework"' not in module, "relative dependency breaks Mono.Addins region module startup"
assert 'Addin("OpenGenesisLINK.OGLSeasons", Namespace = "OpenGenesisLINK"' not in module
assert "OGLSeasons.dll" in build
# Plain ROBUST/CI builds must not load a simulator-only Mono.Addins plugin.
assert "--sim-bin" in build, "installation must explicitly target a simulator"
assert 'install -m 0644 "$DLL" "$SIM_BIN/OpenGenesisLINK.Addons.OGLSeasons.dll"' in build
assert 'SIM_BIN' in build and '*/robust/bin' in build
assert 'OpenSim.Region.Framework.dll' in build
assert 'cp "$OUT/OpenGenesisLINK.Addons.OGLSeasons.dll" bin/' not in build
assert "mkdir -p bin" not in build

assert "addon-modules/OGLSeasons/build.sh Release" in prebuild
assert "tools/ci/OglSeasonsRegression/OglSeasonsRegression.csproj" in ci
assert "tools/ci/verify_ogl_seasons.py" in ci
assert "addon-modules/OGLSeasons/build.sh Release" in ci
assert "StoreOnRegion(" not in module, "must not overwrite EEP"
assert "SetTerrain" not in module, "must not edit terrain"
print("OGLSeasons addon opt-in, bounded weather signaling and build contract: OK")
