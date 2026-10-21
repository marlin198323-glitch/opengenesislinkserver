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
        // The repeated local hour at the autumn DST rollback must not
        // produce a bogus UTC future window boundary.
        var fallFirst = Utc(2026, 10, 25, 0, 15);
        var fallSecond = Utc(2026, 10, 25, 1, 15);
        var firstHour = SeasonWeatherModel.Calculate(fallFirst, regionA);
        var secondHour = SeasonWeatherModel.Calculate(fallSecond, regionA);
        Require(firstHour.ValidFromUtc == Utc(2026, 10, 25, 0), "DST fallback first 02:xx valid-from");
        Require(secondHour.ValidFromUtc == Utc(2026, 10, 25, 1), "DST fallback second 02:xx valid-from");
        Require(firstHour.ValidFromUtc <= fallFirst && secondHour.ValidFromUtc <= fallSecond,
            "no future weather period at DST rollback");
        Require(SeasonWeatherModel.Calculate(Utc(2026, 3, 29, 1, 3), regionA).ValidFromUtc
                <= Utc(2026, 3, 29, 1, 3), "spring DST forward valid-from");
        // Same weather zone across independent region IDs and simulators
        // guarantees a shared local weather pattern.
        var alignedA = SeasonWeatherModel.Calculate(Utc(2026, 10, 21), regionA,
            weatherZone: "Freiburg");
        var alignedB = SeasonWeatherModel.Calculate(Utc(2026, 10, 21), regionB,
            weatherZone: " freiburg ");
        Require(alignedA.ToProtocol() == alignedB.ToProtocol(),
            "shared zone weather must match across region IDs");
        var cold = SeasonWeatherModel.Calculate(Utc(2026, 10, 21), regionA,
            weatherZone: "Freiburg", temperatureOffsetC: -4);
        Require(cold.TemperatureC == alignedA.TemperatureC - 4 ||
                (alignedA.Weather == OglWeather.Hitzewelle && cold.Weather == OglWeather.Hitzewelle),
            "negative microclimate temperature offset");
        try { SeasonWeatherModel.Calculate(Utc(2026, 1, 1), regionA, temperatureOffsetC: 20);
            throw new Exception("expected climate offset range exception"); }
        catch (ArgumentOutOfRangeException) { }
        try { SeasonWeatherModel.Calculate(Utc(2026, 1, 1), regionA,
            weatherZone: new string('Z', 81));
            throw new Exception("expected zone name length exception"); }
        catch (ArgumentOutOfRangeException) { }

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
