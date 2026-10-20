// SPDX-License-Identifier: MPL-2.0
using System;
using System.Globalization;

namespace OpenGenesisLINK.Addons.OGLSeasons
{
    public enum OglSeason { Fruehling, Sommer, Herbst, Winter }
    public enum OglWeather
    {
        Heiter, Bewoelkt, Nieselregen, Regen, Starkregen,
        Schneefall, Schneesturm, Nebel, Gewitter, Sturm, Hagel, Hitzewelle
    }

    public sealed class OglWeatherState
    {
        public OglSeason Season { get; }
        public OglWeather Weather { get; }
        public DateTimeOffset ValidFromUtc { get; }
        public int TemperatureC { get; }
        public int WindKmh { get; }
        public int HumidityPercent { get; }
        public int Intensity { get; }
        public string SeasonKey => Season.ToString().ToLowerInvariant();
        public string WeatherKey => Weather.ToString().ToLowerInvariant();

        public OglWeatherState(OglSeason season, OglWeather weather, DateTimeOffset validFromUtc,
            int temperatureC, int windKmh, int humidityPercent, int intensity)
        {
            Season = season;
            Weather = weather;
            ValidFromUtc = validFromUtc;
            TemperatureC = temperatureC;
            WindKmh = windKmh;
            HumidityPercent = humidityPercent;
            Intensity = intensity;
        }

        public string ToProtocol()
        {
            // Stable culture-neutral data contract. No LSL/JSON dependencies.
            return "OGLSEASONS|1|" + ValidFromUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                + "|" + SeasonKey + "|" + WeatherKey + "|"
                + Intensity.ToString(CultureInfo.InvariantCulture) + "|"
                + TemperatureC.ToString(CultureInfo.InvariantCulture) + "|"
                + WindKmh.ToString(CultureInfo.InvariantCulture) + "|"
                + HumidityPercent.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Reproducible seasonal weather simulation for German (Berlin) civil time.
    /// It is NOT live DWD weather data and does not claim climate forecasts.
    /// </summary>
    public static class SeasonWeatherModel
    {
        // Monthly mean anchors for plausible *fictional* lowland German weather,
        // not meteorological observations or climatic reference normals.
        private static readonly int[] s_BaseTemperature = { -1, 0, 5, 9, 14, 18, 20, 19, 15, 10, 5, 1 };
        private static readonly TimeZoneInfo s_Zone = ResolveGermanZone();

        public static TimeZoneInfo GermanZone => s_Zone;

        private static TimeZoneInfo ResolveGermanZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            }
        }

        public static OglSeason ResolveSeason(DateTimeOffset instantUtc)
        {
            int month = TimeZoneInfo.ConvertTime(instantUtc, s_Zone).Month;
            if (month >= 3 && month <= 5) return OglSeason.Fruehling;
            if (month >= 6 && month <= 8) return OglSeason.Sommer;
            if (month >= 9 && month <= 11) return OglSeason.Herbst;
            return OglSeason.Winter;
        }

        public static OglWeatherState Calculate(
            DateTimeOffset instantUtc, Guid regionId, int intervalMinutes = 30, int seed = 2026)
        {
            if (intervalMinutes < 5 || intervalMinutes > 1440 || 1440 % intervalMinutes != 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMinutes),
                    "Interval must be a divisor of 1440 between 5 and 1440 minutes.");

            DateTimeOffset local = TimeZoneInfo.ConvertTime(instantUtc, s_Zone);
            OglSeason season = ResolveSeason(instantUtc);
            int minutes = local.Hour * 60 + local.Minute;
            int slot = minutes / intervalMinutes;
            // Seed by LOCAL day/slot + region; DST repeated hour remains stable
            // but never drifts forward or back with simulator frame rate.
            uint hash = 2166136261u;
            foreach (byte value in regionId.ToByteArray())
                hash = (hash ^ value) * 16777619u;
            hash = Mix(hash, (uint)seed);
            hash = Mix(hash, (uint)local.Year);
            hash = Mix(hash, (uint)local.DayOfYear);
            hash = Mix(hash, (uint)slot);
            Random random = new Random(unchecked((int)(hash & 0x7fffffff)));

            int temperature = s_BaseTemperature[local.Month - 1] + random.Next(-5, 6);
            int wind = Math.Max(0, 4 + random.Next(0, 20));
            int humidity = 40 + random.Next(0, 57);
            int roll = random.Next(1000);
            OglWeather weather;
            switch (season)
            {
                case OglSeason.Winter:
                    weather = roll < 230 ? OglWeather.Bewoelkt
                        : roll < 420 ? OglWeather.Heiter
                        : roll < 585 ? OglWeather.Nebel
                        : roll < 740 ? OglWeather.Schneefall
                        : roll < 795 ? OglWeather.Schneesturm
                        : roll < 940 ? OglWeather.Regen
                        : roll < 980 ? OglWeather.Sturm
                        : OglWeather.Nieselregen;
                    break;
                case OglSeason.Fruehling:
                    weather = roll < 260 ? OglWeather.Heiter
                        : roll < 465 ? OglWeather.Bewoelkt
                        : roll < 565 ? OglWeather.Nieselregen
                        : roll < 800 ? OglWeather.Regen
                        : roll < 870 ? OglWeather.Starkregen
                        : roll < 940 ? OglWeather.Gewitter
                        : roll < 980 ? OglWeather.Nebel
                        : OglWeather.Hagel;
                    break;
                case OglSeason.Sommer:
                    weather = roll < 360 ? OglWeather.Heiter
                        : roll < 550 ? OglWeather.Bewoelkt
                        : roll < 680 ? OglWeather.Hitzewelle
                        : roll < 810 ? OglWeather.Regen
                        : roll < 890 ? OglWeather.Starkregen
                        : roll < 970 ? OglWeather.Gewitter
                        : OglWeather.Hagel;
                    break;
                default: // Autumn
                    weather = roll < 160 ? OglWeather.Heiter
                        : roll < 400 ? OglWeather.Bewoelkt
                        : roll < 590 ? OglWeather.Nebel
                        : roll < 750 ? OglWeather.Regen
                        : roll < 850 ? OglWeather.Nieselregen
                        : roll < 925 ? OglWeather.Starkregen
                        : roll < 975 ? OglWeather.Sturm
                        : OglWeather.Gewitter;
                    break;
            }

            if ((weather == OglWeather.Schneefall || weather == OglWeather.Schneesturm)
                && temperature > 2)
                weather = weather == OglWeather.Schneesturm ? OglWeather.Starkregen : OglWeather.Regen;
            if (weather == OglWeather.Regen && temperature <= -1)
                weather = OglWeather.Schneefall;
            if (weather == OglWeather.Hitzewelle && temperature < 26)
                temperature = 26 + random.Next(0, 9);
            if (weather == OglWeather.Sturm || weather == OglWeather.Schneesturm)
                wind = Math.Max(wind, 65 + random.Next(0, 35));
            else if (weather == OglWeather.Gewitter || weather == OglWeather.Hagel)
                wind = Math.Max(wind, 35 + random.Next(0, 35));
            int intensity = weather switch
            {
                OglWeather.Heiter or OglWeather.Bewoelkt => 0,
                OglWeather.Nebel => 20 + random.Next(55),
                OglWeather.Nieselregen => 12 + random.Next(28),
                OglWeather.Hitzewelle => 30 + random.Next(65),
                OglWeather.Sturm or OglWeather.Schneesturm => 60 + random.Next(41),
                OglWeather.Starkregen or OglWeather.Gewitter or OglWeather.Hagel => 50 + random.Next(51),
                _ => 20 + random.Next(61)
            };
            // Store window boundary as UTC instant; local midnight may differ in DST.
            DateTime localStart = local.Date.AddMinutes(slot * intervalMinutes);
            TimeSpan offset = s_Zone.GetUtcOffset(localStart);
            DateTimeOffset boundary = new DateTimeOffset(localStart, offset).ToUniversalTime();
            return new OglWeatherState(season, weather, boundary, temperature, wind, humidity, intensity);
        }

        private static uint Mix(uint current, uint value)
        {
            unchecked { return (current ^ value) * 16777619u; }
        }
    }

    public interface IOglSeasonsService
    {
        OglWeatherState Current { get; }
        event Action<OglWeatherState> StateChanged;
    }
}
