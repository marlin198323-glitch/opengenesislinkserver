// SPDX-License-Identifier: MPL-2.0
using System;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenGenesisLINK.Addons.OGLSeasons
{
    /// <summary>
    /// Volatile, owner-controlled EEP overlay. NEVER calls StoreOnRegion, never
    /// touches parcel EEP or estate rights, and never changes persisted data.
    /// If an admin replaces the current environment, the addon stops changing it.
    /// </summary>
    internal sealed class OglSeasonalEepOverlay
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OglSeasonalEepOverlay));
        private readonly Scene m_Scene;
        private readonly IEnvironmentModule m_Environment;
        private readonly bool m_AllowCustomEnvironment;
        private ViewerEnvironment m_Previous;
        private ViewerEnvironment m_Template;
        private ViewerEnvironment m_Applied;
        private bool m_Suspended;
        private string m_LastKey;

        internal OglSeasonalEepOverlay(Scene scene, IEnvironmentModule environment,
            bool allowCustomEnvironment)
        {
            m_Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            m_AllowCustomEnvironment = allowCustomEnvironment;
        }

        internal bool Apply(OglWeatherState weather)
        {
            if (weather == null || m_Suspended)
                return false;

            ViewerEnvironment current = m_Scene.RegionEnvironment;
            if (m_Applied == null)
            {
                // An existing EEP daycycle can be estate- or parcel-specific:
                // do not overwrite it without a separate explicit opt-in.
                if (current != null && !m_AllowCustomEnvironment)
                {
                    m_Suspended = true;
                    Log.InfoFormat("[OGL-JAHRESZEITEN][EEP]: Region {0} besitzt eigene EEP-Einstellungen. "
                        + "Automatische Überlagerung übersprungen.", m_Scene.Name);
                    return false;
                }
                m_Previous = current;
                m_Template = (current ?? m_Environment.GetRegionEnvironment())?.Clone();
            }
            else if (!ReferenceEquals(current, m_Applied))
            {
                // CAS-like ownership: a changed region environment belongs to
                // the new administrator/editor, not this module.
                m_Suspended = true;
                Log.WarnFormat("[OGL-JAHRESZEITEN][EEP]: Region {0}: EEP wurde anderweitig verändert; "
                    + "automatische Überlagerung gestoppt.", m_Scene.Name);
                return false;
            }

            string key = weather.SeasonKey + ":" + weather.WeatherKey + ":" + weather.Intensity / 20;
            if (m_Applied != null && key == m_LastKey)
                return false;

            try
            {
                ViewerEnvironment template = m_Template;
                if (template == null)
                    return false;
                ViewerEnvironment overlay = template.Clone();
                if (!TransformSkyFrames(overlay, weather))
                {
                    Log.WarnFormat("[OGL-JAHRESZEITEN][EEP]: Region {0}: "
                        + "keine gültigen Himmel-Frames; Originalumgebung beibehalten.", m_Scene.Name);
                    return false;
                }
                m_Scene.RegionEnvironment = overlay;
                m_Applied = overlay;
                m_LastKey = key;
                m_Environment.WindlightRefresh(10);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("[OGL-JAHRESZEITEN][EEP]: Überlagerung fehlgeschlagen.", e);
                if (m_Applied != null && ReferenceEquals(m_Scene.RegionEnvironment, m_Applied))
                    m_Scene.RegionEnvironment = m_Previous;
                m_Applied = null;
                m_Suspended = true;
                return false;
            }
        }

        internal void Restore()
        {
            if (m_Applied == null)
                return;
            try
            {
                if (ReferenceEquals(m_Scene.RegionEnvironment, m_Applied))
                {
                    m_Scene.RegionEnvironment = m_Previous;
                    m_Environment.WindlightRefresh(10);
                }
            }
            catch (Exception e)
            {
                Log.Warn("[OGL-JAHRESZEITEN][EEP]: Ursprungsumgebung konnte nicht aktualisiert werden.", e);
            }
            finally
            {
                m_Applied = null;
                m_Previous = null;
                m_Template = null;
                m_LastKey = null;
                m_Suspended = true;
            }
        }

        internal static bool TransformSkyFrames(ViewerEnvironment environment, OglWeatherState weather)
        {
            if (environment == null || weather == null || environment.ToOSD() is not OSDMap top ||
                !top.TryGetValue("day_cycle", out OSD rawCycle) || rawCycle is not OSDMap cycle ||
                !cycle.TryGetValue("frames", out OSD rawFrames) || rawFrames is not OSDMap frames)
                return false;

            // Cloud shadow/legacy haze are native EEP sky fields; preserve all
            // sky timings, moon/sun rotation, water and non-sky components.
            bool heavy = weather.Weather == OglWeather.Starkregen
                || weather.Weather == OglWeather.Gewitter
                || weather.Weather == OglWeather.Schneesturm
                || weather.Weather == OglWeather.Sturm;
            bool fog = weather.Weather == OglWeather.Nebel;
            bool precipitation = weather.Weather is OglWeather.Regen or OglWeather.Nieselregen
                or OglWeather.Schneefall or OglWeather.Starkregen or OglWeather.Schneesturm
                or OglWeather.Gewitter or OglWeather.Hagel;
            float coverage = fog ? 0.88f : heavy ? 0.84f
                : precipitation ? 0.66f
                : weather.Weather == OglWeather.Bewoelkt ? 0.52f : 0.16f;
            float haze = fog ? 2.5f : heavy ? 1.45f : precipitation ? 1.2f
                : weather.Season == OglSeason.Winter ? 0.95f : 0.6f;
            float light = weather.Season == OglSeason.Sommer ? 1.05f
                : weather.Season == OglSeason.Winter ? 0.72f : 0.90f;
            if (heavy || fog) light *= 0.72f;

            int transformed = 0;
            foreach (var frame in frames)
            {
                if (frame.Value is not OSDMap sky ||
                    !sky.TryGetValue("type", out OSD type) ||
                    !string.Equals(type.AsString(), "sky", StringComparison.OrdinalIgnoreCase))
                    continue;
                sky["cloud_shadow"] = coverage;
                sky["cloud_variance"] = precipitation ? 0.23f : 0.09f;
                sky["cloud_color"] = fog ? new Vector3(0.74f, 0.77f, 0.79f)
                    : heavy ? new Vector3(0.35f, 0.39f, 0.44f)
                    : new Vector3(0.86f, 0.89f, 0.94f);
                if (sky.TryGetValue("legacy_haze", out OSD oldHaze) && oldHaze is OSDMap legacy)
                {
                    legacy["haze_density"] = haze;
                    legacy["haze_horizon"] = fog ? 0.85f : heavy ? 0.5f : 0.20f;
                    legacy["ambient"] = new Vector3(light, light, light);
                }
                transformed++;
            }
            if (transformed == 0)
                return false;
            environment.FromOSD(top);
            environment.InvalidateCaches();
            return true;
        }
    }
}
