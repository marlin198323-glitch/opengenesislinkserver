// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace NexVerse.RegionModules.ViewerAdvisor
{
    /// <summary>
    /// Opt-in, private chat recommendation for non-OGL viewers.
    /// The cooldown is per avatar and per simulator process. Region crossings
    /// on the same simulator do not reset it.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OGLViewerAdvisorModule")]
    public sealed class OGLViewerAdvisorModule : ISharedRegionModule
    {
        private static readonly ILog m_Log =
            LogManager.GetLogger(typeof(OGLViewerAdvisorModule));

        private readonly object m_Sync = new object();
        private readonly List<Scene> m_Scenes = new List<Scene>();
        private readonly Dictionary<UUID, DateTime> m_LastSentUtc =
            new Dictionary<UUID, DateTime>();

        private volatile bool m_Enabled;
        private TimeSpan m_Interval = TimeSpan.FromMinutes(60);
        private string[] m_OfficialPrefixes = { "OpenGenesisLINK", "OGL Viewer" };
        private string m_Message =
            "Um das volle Erlebnis von OpenGenesisLINK geniessen zu koennen, " +
            "steigen Sie auf den offiziellen OGL Viewer um: https://opengenesislink.de";
        private Timer m_Timer;
        private int m_CheckRunning;

        public string Name => "OGL Viewer Advisor";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource config)
        {
            IConfig section = config?.Configs["OGLViewerAdvisor"];
            // Explicit opt-in: absent sections and bare installations remain silent.
            m_Enabled = section?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled)
                return;

            int minutes = Math.Clamp(section.GetInt("IntervalMinutes", 60), 1, 1440);
            m_Interval = TimeSpan.FromMinutes(minutes);
            m_OfficialPrefixes = ParsePrefixes(
                section.GetString("OfficialViewerPrefixes", "OpenGenesisLINK;OGL Viewer"));

            string message = section.GetString("Message", m_Message)?.Trim();
            if (!string.IsNullOrEmpty(message))
                m_Message = message.Length > 900 ? message.Substring(0, 900) : message;

            m_Log.InfoFormat(
                "[OGL-VIEWER-ADVISOR]: Enabled; interval={0} minute(s); known official viewer prefixes={1}.",
                minutes, m_OfficialPrefixes.Length);
        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;

            // A periodic sweep handles users who remain connected for hours.
            // One timer is shared by all regions in this simulator process.
            m_Timer = new Timer(CheckOnlineAvatars, null,
                TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || scene == null)
                return;

            lock (m_Sync)
            {
                if (m_Scenes.Contains(scene))
                    return;
                m_Scenes.Add(scene);
            }

            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
        }

        public void RegionLoaded(Scene scene) { }

        public void RemoveRegion(Scene scene)
        {
            if (scene == null)
                return;

            scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
            lock (m_Sync)
                m_Scenes.Remove(scene);
        }

        public void Close()
        {
            m_Enabled = false;
            m_Timer?.Dispose();
            m_Timer = null;

            lock (m_Sync)
            {
                foreach (Scene scene in m_Scenes)
                    scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;

                m_Scenes.Clear();
                m_LastSentUtc.Clear();
            }
        }

        private static string[] ParsePrefixes(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<string>();

            string[] parts = input.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in parts)
            {
                string cleaned = part.Trim();
                if (cleaned.Length != 0)
                    unique.Add(cleaned);
            }

            var result = new string[unique.Count];
            unique.CopyTo(result);
            return result;
        }

        internal static bool MatchesOfficialViewer(string value, string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string candidate = value.Trim();
            foreach (string prefix in prefixes)
            {
                if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (candidate.Length == prefix.Length)
                    return true;

                // Require a separator after the reported product name.
                // Viewer-supplied names are not authenticated identities.
                char delimiter = candidate[prefix.Length];
                if (char.IsWhiteSpace(delimiter) || delimiter == '-' ||
                    delimiter == '_' || delimiter == '/' || delimiter == '(' ||
                    delimiter == ':' || delimiter == '.')
                    return true;
            }

            return false;
        }

        private void OnMakeRootAgent(ScenePresence presence)
        {
            CheckAndNotify(presence);
        }

        private void CheckOnlineAvatars(object state)
        {
            if (!m_Enabled ||
                Interlocked.CompareExchange(ref m_CheckRunning, 1, 0) != 0)
                return;

            try
            {
                Scene[] scenes;
                lock (m_Sync)
                {
                    scenes = m_Scenes.ToArray();
                    DateTime staleBefore = DateTime.UtcNow - m_Interval - m_Interval;
                    var stale = new List<UUID>();
                    foreach (KeyValuePair<UUID, DateTime> entry in m_LastSentUtc)
                    {
                        if (entry.Value < staleBefore)
                            stale.Add(entry.Key);
                    }
                    foreach (UUID id in stale)
                        m_LastSentUtc.Remove(id);
                }

                foreach (Scene scene in scenes)
                {
                    try
                    {
                        scene.ForEachRootScenePresence(CheckAndNotify);
                    }
                    catch (Exception error)
                    {
                        m_Log.Warn("[OGL-VIEWER-ADVISOR]: Periodic avatar scan failed.", error);
                    }
                }
            }
            finally
            {
                Volatile.Write(ref m_CheckRunning, 0);
            }
        }

        private void CheckAndNotify(ScenePresence presence)
        {
            if (!m_Enabled || presence == null || presence.IsDeleted ||
                presence.IsNPC || presence.IsChildAgent)
                return;

            IClientAPI client = presence.ControllingClient;
            if (client == null)
                return;

            AgentCircuitData circuit;
            try
            {
                circuit = presence.Scene.AuthenticateHandler.GetAgentCircuitData(
                    client.CircuitCode);
            }
            catch (Exception error)
            {
                m_Log.Debug("[OGL-VIEWER-ADVISOR]: Could not inspect viewer circuit.", error);
                return;
            }

            // A missing circuit is not a reason to send an unsolicited advert.
            if (circuit == null ||
                MatchesOfficialViewer(circuit.Channel, m_OfficialPrefixes) ||
                MatchesOfficialViewer(circuit.Viewer, m_OfficialPrefixes))
                return;

            DateTime now = DateTime.UtcNow;
            lock (m_Sync)
            {
                if (m_LastSentUtc.TryGetValue(presence.UUID, out DateTime last) &&
                    now - last < m_Interval)
                    return;

                // Reserve the slot before sending to avoid timer/login races.
                m_LastSentUtc[presence.UUID] = now;
            }

            try
            {
                // Owner-only chat: never broadcast the recommendation to a region.
                client.SendChatMessage(
                    m_Message,
                    (byte)ChatTypeEnum.Owner,
                    presence.AbsolutePosition,
                    "OpenGenesisLINK System",
                    UUID.Zero,
                    UUID.Zero,
                    (byte)ChatSourceType.Object,
                    (byte)ChatAudibleLevel.Fully);
            }
            catch (Exception error)
            {
                lock (m_Sync)
                {
                    if (m_LastSentUtc.TryGetValue(presence.UUID, out DateTime last) &&
                        last == now)
                        m_LastSentUtc.Remove(presence.UUID);
                }

                m_Log.Warn("[OGL-VIEWER-ADVISOR]: Sending viewer recommendation failed.", error);
            }
        }
    }
}
