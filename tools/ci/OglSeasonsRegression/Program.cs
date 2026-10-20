// SPDX-License-Identifier: MPL-2.0
using System;
using OpenGenesisLINK.Addons.OGLSeasons;

static class Program
{
    private static void Require(bool pass, string message)
    {
        if (!pass) throw new Exception("OGLSeasons regression: " + message);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 12, int minute = 0)
        => new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    public static void Main()
    {
        Guid regionA = Guid.Parse("3a111111-1111-4111-8111-111111111111");
        Guid regionB = Guid.Parse("3a222222-2222-4222-8222-222222222222");
        // Europe/Berlin time (including DST boundaries), not UTC month.
        Require(SeasonWeatherModel.ResolveSeason(Utc(2026, 2, 28, 22)) == OglSeason.Winter, "winter end");
        Require(SeasonWeatherModel.ResolveSeason(Utc(2026, 2, 28, 23)) == OglSeason.Fruehling, "spring starts at local midnight");
        Require(SeasonWeatherModel.ResolveSeason(Utc(2026, 5, 31, 22)) == OglSeason.Sommer, "summer starts with CEST");
        Require(SeasonWeatherModel.ResolveSeason(Utc(2026, 8, 31, 22)) == OglSeason.Herbst, "autumn starts with CEST");
        Require(SeasonWeatherModel.ResolveSeason(Utc(2026, 11, 30, 23)) == OglSeason.Winter, "winter starts with CET");
        Require(SeasonWeatherModel.GermanZone.Id != null, "valid German timezone");
        OglWeatherState one = SeasonWeatherModel.Calculate(Utc(2026, 10, 21, 10, 0), regionA);
        OglWeatherState two = SeasonWeatherModel.Calculate(Utc(2026, 10, 21, 10, 9), regionA);
        Require(one.ToProtocol() == two.ToProtocol(), "stable state within 30-minute window");
        Require(one.ToProtocol().Split('|').Length == 9, "LSL protocol fields");
        Require(one.WeatherKey == one.Weather.ToString().ToLowerInvariant(), "weather key");
        bool differs = false;
        for (int h = 0; h < 48; h++)
        {
            DateTimeOffset time = Utc(2026, 10, 21, 0).AddMinutes(h * 30);
            var a = SeasonWeatherModel.Calculate(time, regionA);
            var b = SeasonWeatherModel.Calculate(time, regionB);
            if (a.ToProtocol() != b.ToProtocol()) differs = true;
            Require(a.Intensity >= 0 && a.Intensity <= 100, "intensity bounds");
            Require(a.WindKmh >= 0 && a.HumidityPercent is >= 40 and <= 96, "wind/humidity bounds");
            if (a.Weather is OglWeather.Schneefall or OglWeather.Schneesturm)
                Require(a.TemperatureC <= 2, "snow only when cold");
        }
        Require(differs, "region-specific weather variation");
        try { SeasonWeatherModel.Calculate(Utc(2026, 1, 1), regionA, 17); throw new Exception("expected range exception"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("OGLSeasons: DE-Kalender, Wetterfenster, Regionstreuung, Schnee-/Windgrenzen, Protokoll: OK");
    }
}
