// SPDX-License-Identifier: MPL-2.0
using System;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("OpenGenesisLINK.OGLSeasons", Namespace = "OpenGenesisLINK", Version = "0.1.0")]

namespace OpenGenesisLINK.Addons.OGLSeasons
{
    /// <summary>
    /// Nonshared, disabled-by-default region addon. Never edits terrain,
    /// object textures, wind physics, parcel environments or estate rights.
    /// Particle emitter prims are opt-in and read weather broadcasts via LSL.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OGLSeasons")]
    public sealed class OglSeasonsRegionModule : INonSharedRegionModule, IOglSeasonsService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OglSeasonsRegionModule));
        private Scene m_Scene;
        private bool m_Enabled;
        private bool m_Started;
        private int m_IntervalMinutes = 30;
        private int m_Seed = 2026;
        private int m_CheckSeconds = 60;
        private int m_BroadcastSeconds = 180;
        private int m_Channel = -835011;
        private string m_AllowedRegions = "";
        private string m_WeatherZone = "";
        private int m_TemperatureOffsetC;
        private DateTime m_NextCheckUtc = DateTime.MinValue;
        private DateTime m_NextBroadcastUtc = DateTime.MinValue;
        private OglWeatherState m_Current;
        private OglSeasonalEepOverlay m_EepOverlay;
        private bool m_EnableEepOverlay;
        private bool m_AllowCustomEnvironment;

        public string Name => "OGLSeasons";
        public Type ReplaceableInterface => null;
        public OglWeatherState Current => m_Current;
        public event Action<OglWeatherState> StateChanged;

        public void Initialise(IConfigSource config)
        {
            IConfig settings = config?.Configs["OGLSeasons"];
            m_Enabled = settings?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled)
                return;
            m_IntervalMinutes = settings.GetInt("WeatherIntervalMinutes", 30);
            if (m_IntervalMinutes < 5 || m_IntervalMinutes > 1440 ||
                1440 % m_IntervalMinutes != 0)
            {
                Log.Warn("[OGL-JAHRESZEITEN]: Ungültiges Wetterintervall; Modul deaktiviert.");
                m_Enabled = false;
                return;
            }
            m_CheckSeconds = Math.Clamp(settings.GetInt("CheckSeconds", 60), 30, 3600);
            m_BroadcastSeconds = Math.Clamp(settings.GetInt("BroadcastSeconds", 180), 60, 3600);
            m_Seed = settings.GetInt("Seed", 2026);
            m_Channel = settings.GetInt("LSLChannel", -835011);
            m_AllowedRegions = settings.GetString("AllowedRegions", "") ?? "";
            m_EnableEepOverlay = settings.GetBoolean("EnableEepOverlay", false);
            m_AllowCustomEnvironment = settings.GetBoolean("AllowCustomRegionEep", false);
            m_WeatherZone = (settings.GetString("WeatherZone", "") ?? "").Trim();
            m_TemperatureOffsetC = settings.GetInt("TemperatureOffsetC", 0);
            if (m_WeatherZone.Length > 80 ||
                m_TemperatureOffsetC < -15 || m_TemperatureOffsetC > 15)
            {
                Log.Warn("[OGL-JAHRESZEITEN]: Wetterzone oder Temperaturkorrektur ungültig; Modul deaktiviert.");
                m_Enabled = false;
                return;
            }
            if (m_Channel >= 0)
            {
                Log.Warn("[OGL-JAHRESZEITEN]: LSLChannel muss negativ sein; Modul deaktiviert.");
                m_Enabled = false;
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || scene == null)
                return;
            if (!string.IsNullOrWhiteSpace(m_AllowedRegions))
            {
                bool allowed = false;
                foreach (string item in m_AllowedRegions.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (string.Equals(item.Trim(), scene.RegionInfo.RegionName, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }
                if (!allowed)
                    return;
            }
            m_Scene = scene;
            scene.RegisterModuleInterface<IOglSeasonsService>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Scene != scene || m_Started || !m_Enabled)
                return;
            m_Started = true;
            if (m_EnableEepOverlay)
            {
                IEnvironmentModule env = scene.RequestModuleInterface<IEnvironmentModule>();
                if (env == null)
                    Log.WarnFormat("[OGL-JAHRESZEITEN][EEP]: Region {0}: "
                        + "EEP-Modul fehlt; Himmelsüberlagerung bleibt deaktiviert.", scene.Name);
                else
                    m_EepOverlay = new OglSeasonalEepOverlay(scene, env, m_AllowCustomEnvironment);
            }
            m_NextCheckUtc = DateTime.MinValue;
            m_NextBroadcastUtc = DateTime.MinValue;
            scene.EventManager.OnFrame += OnFrame;
            Log.InfoFormat("[OGL-JAHRESZEITEN]: Region {0} aktiviert, deutscher Kalender; " +
                "Wettermodell-Simulation, kein Live-Wetterdienst.", scene.RegionInfo.RegionName);
        }

        private void OnFrame()
        {
            Scene scene = m_Scene;
            if (!m_Started || scene == null)
                return;
            DateTime now = DateTime.UtcNow;
            if (now < m_NextCheckUtc && now < m_NextBroadcastUtc)
                return;
            if (now >= m_NextCheckUtc)
            {
                m_NextCheckUtc = now.AddSeconds(m_CheckSeconds);
                try
                {
                    OglWeatherState next = SeasonWeatherModel.Calculate(
                        new DateTimeOffset(now, TimeSpan.Zero),
                        scene.RegionInfo.RegionID.Guid, m_IntervalMinutes, m_Seed,
                        m_WeatherZone, m_TemperatureOffsetC);
                    OglWeatherState old = m_Current;
                    m_Current = next;
                    if (old == null || old.ValidFromUtc != next.ValidFromUtc ||
                        old.Season != next.Season || old.Weather != next.Weather)
                    {
                        m_NextBroadcastUtc = DateTime.MinValue;
                        Log.InfoFormat("[OGL-JAHRESZEITEN]: Region {0}: {1}, Wetter {2}, {3} °C, " +
                            "Wind {4} km/h, Stärke {5} %.", scene.RegionInfo.RegionName,
                            next.Season, next.Weather, next.TemperatureC,
                            next.WindKmh, next.Intensity);
                        if (m_EepOverlay != null)
                            m_EepOverlay.Apply(next);
                        try { StateChanged?.Invoke(next); }
                        catch (Exception e)
                        {
                            Log.Warn("[OGL-JAHRESZEITEN]: Ein Statusabonnent meldete einen Fehler.", e);
                        }
                    }
                }
                catch (Exception e)
                {
                    // Keep simulator alive; never crash region frame loop.
                    Log.Error("[OGL-JAHRESZEITEN]: Wetterberechnung fehlgeschlagen.", e);
                    return;
                }
            }
            if (now >= m_NextBroadcastUtc && m_Current != null)
            {
                m_NextBroadcastUtc = now.AddSeconds(m_BroadcastSeconds);
                try
                {
                    IWorldComm comm = scene.RequestModuleInterface<IWorldComm>();
                    if (comm != null)
                        comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                            "OGLSeasons", UUID.Zero, m_Current.ToProtocol(),
                            new Vector3(scene.RegionInfo.RegionSizeX / 2f,
                                scene.RegionInfo.RegionSizeY / 2f, 20f));
                }
                catch (Exception e)
                {
                    Log.Warn("[OGL-JAHRESZEITEN]: Wetter-Signal an LSL konnte nicht gesendet werden.", e);
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Scene != scene)
                return;
            if (m_Started)
                scene.EventManager.OnFrame -= OnFrame;
            // Only restore a snapshot still owned by OGLSeasons.
            m_EepOverlay?.Restore();
            m_EepOverlay = null;
            scene.UnregisterModuleInterface<IOglSeasonsService>(this);
            m_Started = false;
            m_Current = null;
            m_Scene = null;
        }

        public void Close()
        {
            if (m_Scene != null)
                RemoveRegion(m_Scene);
        }
    }
}
