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
assert "OGLSeasons.dll" in build
assert "addon-modules/OGLSeasons/build.sh Release" in prebuild
assert "tools/ci/OglSeasonsRegression/OglSeasonsRegression.csproj" in ci
assert "tools/ci/verify_ogl_seasons.py" in ci
assert "addon-modules/OGLSeasons/build.sh Release" in ci
assert "StoreOnRegion(" not in module, "must not overwrite EEP"
assert "SetTerrain" not in module, "must not edit terrain"
print("OGLSeasons addon opt-in, bounded weather signaling and build contract: OK")
