// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace NexVerse.RegionModules.Archives
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OglIarManagementModule")]
    public sealed class OglIarManagementModule : ISharedRegionModule, IInventoryArchiveInspector
    {
        private static readonly ILog m_Log =
            LogManager.GetLogger(typeof(OglIarManagementModule));

        private readonly ConcurrentDictionary<UUID, OglIarOperationManager> m_Managers = new();
        private bool m_Enabled;
        private string m_StorageRoot = "OGLInventoryArchives";
        private long m_MaximumArchiveBytes = 10L * 1024 * 1024 * 1024;

        public string Name => "OGL IAR Management";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["OpenGenesisLINKIAR"];
            m_Enabled = config?.GetBoolean("Enabled", true) ?? true;
            if (!m_Enabled) return;
            m_StorageRoot = config?.GetString("StorageRoot", "OGLInventoryArchives") ?? "OGLInventoryArchives";
            long maxMiB = Math.Max(1, config?.GetLong("MaximumArchiveMiB", 10240) ?? 10240);
            m_MaximumArchiveBytes = checked(maxMiB * 1024L * 1024L);
        }

        // Register inspection even when managed archival jobs are disabled.
        // This protects the legacy "load iar" command independently of the API.
        public void AddRegion(Scene scene)
        {
            scene?.RegisterModuleInterface<IInventoryArchiveInspector>(this);
        }

        public bool TryInspect(string archivePath, out int entryCount, out string rejectionReason)
        {
            OglIarInspection inspection = OglIarInspector.Inspect(archivePath);
            entryCount = inspection.Valid ? inspection.Entries : 0;
            rejectionReason = inspection.Valid ? string.Empty : inspection.Error;
            return inspection.Valid;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled ||
                scene == null ||
                m_Managers.ContainsKey(scene.RegionInfo.RegionID))
            {
                return;
            }

            if (scene.RequestModuleInterface<IInventoryArchiverModule>() == null)
            {
                m_Log.WarnFormat(
                    "[OGL-IAR]: IAR-Verwaltung fuer Region {0} bleibt deaktiviert, weil kein IInventoryArchiverModule verfuegbar ist.",
                    scene.RegionInfo.RegionName);
                return;
            }

            string root =
                System.IO.Path.Combine(
                    m_StorageRoot,
                    scene.RegionInfo.RegionID.ToString());

            OglIarOperationManager manager =
                new(
                    scene,
                    new OglIarStoragePolicy(
                        root,
                        m_MaximumArchiveBytes));

            if (!m_Managers.TryAdd(
                    scene.RegionInfo.RegionID,
                    manager))
            {
                manager.Dispose();
                return;
            }

            scene.RegisterModuleInterface<IOglIarOperations>(
                manager);
        }

        public void RemoveRegion(Scene scene)
        {
            scene?.UnregisterModuleInterface<IInventoryArchiveInspector>(this);
            if (scene != null && m_Managers.TryRemove(scene.RegionInfo.RegionID, out OglIarOperationManager manager))
            {
                scene.UnregisterModuleInterface<IOglIarOperations>(manager);
                manager.Dispose();
            }
        }

        public void PostInitialise() { }
        public void Close()
        {
            foreach (OglIarOperationManager manager in m_Managers.Values) manager.Dispose();
            m_Managers.Clear();
        }
    }
}
