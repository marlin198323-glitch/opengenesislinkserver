/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using NDesk.Options;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator inventory archives
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "InventoryArchiverModule")]
    public class InventoryArchiverModule : ISharedRegionModule, IInventoryArchiverModule, IInventoryArchiverProgress
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Enable or disable checking whether the iar user is actually logged in
        /// </value>
//        public bool DisablePresenceChecks { get; set; }

        public event InventoryArchiveSaved OnInventoryArchiveSaved;
        public event InventoryArchiveLoaded OnInventoryArchiveLoaded;
        public event Action<UUID, string, int, int> OnInventoryArchiveProgress;

        // Only console jobs are printed; managed/API jobs use the progress event.
        private readonly Dictionary<UUID, string> m_consoleProgressState = new Dictionary<UUID, string>();
        private sealed class ConsoleIarJob
        {
            public string Action;
            public string Archive;
            public string Phase = "start";
            public int Processed;
            public int Total;
            public int Percent;
            public int LastDisplayedPercent = -1;
            public string LastDisplayedPhase = "";
            public DateTime Started = DateTime.UtcNow;
        }

        private readonly Dictionary<UUID, ConsoleIarJob> m_consoleJobs =
            new Dictionary<UUID, ConsoleIarJob>();
        private bool m_liveConsoleProgress = true;


        /// <summary>
        /// The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "user-inventory.iar";

        /// <value>
        /// Pending save and load completions initiated from the console
        /// </value>
        protected List<UUID> m_pendingConsoleTasks = new List<UUID>();

        /// <value>
        /// All scenes that this module knows about
        /// </value>
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        private Scene m_aScene;

        private IUserAccountService m_UserAccountService;
        protected IUserAccountService UserAccountService
        {
            get
            {
                if (m_UserAccountService == null)
                    // What a strange thing to do...
                    foreach (Scene s in m_scenes.Values)
                    {
                        m_UserAccountService = s.RequestModuleInterface<IUserAccountService>();
                        break;
                    }

                return m_UserAccountService;
            }
        }


        public InventoryArchiverModule() {}

//        public InventoryArchiverModule(bool disablePresenceChecks)
//        {
//            DisablePresenceChecks = disablePresenceChecks;
        //        }

        #region ISharedRegionModule

        public void Initialise(IConfigSource source)
        {
            m_liveConsoleProgress =
                source?.Configs["Startup"]?.GetBoolean("OGLIarLiveProgress", true) ?? true;
        }

        public void AddRegion(Scene scene)
        {
            if (scene == null)
                return;

            // This is a shared region module, but consumers resolve the
            // archiver through each Scene's module registry. Register the
            // shared instance on every scene, not only on the first region.
            scene.RegisterModuleInterface<IInventoryArchiverModule>(this);

            if (m_scenes.Count == 0)
            {
                OnInventoryArchiveSaved += SaveInvConsoleCommandCompleted;
                OnInventoryArchiveLoaded += LoadInvConsoleCommandCompleted;
                OnInventoryArchiveProgress += DisplayConsoleArchiveProgress;

                scene.AddCommand(
                    "Archiving", this, "show iar progress",
                    "show iar progress",
                    "Aktiven IAR-Import oder -Export mit Prozenten, Zählern und Laufzeit anzeigen.",
                    "Zeigt den aktuellen Stand laufender IAR-Konsolenoperationen. " +
                    "Die lokale OpenGenesisLINK-Konsole aktualisiert ihren Status zusätzlich live.",
                    ShowConsoleIarProgress);

                scene.AddCommand(
                    "Archiving", this, "load iar",
                    "load iar [-m|--merge] <first> <last> <inventory path> <password> [<IAR path>]",
                    "Load user inventory archive (IAR).",
                    "-m|--merge is an option which merges the loaded IAR with existing inventory folders where possible, rather than always creating new ones"
                    + "<first> is user's first name." + Environment.NewLine
                    + "<last> is user's last name." + Environment.NewLine
                    + "<inventory path> is the path inside the user's inventory where the IAR should be loaded." + Environment.NewLine
                    + "<password> is the user's password." + Environment.NewLine
                    + "<IAR path> is the filesystem path or URI from which to load the IAR."
                    + string.Format("  If this is not given then the filename {0} in the current directory is used", DEFAULT_INV_BACKUP_FILENAME),
                    HandleLoadInvConsoleCommand);

                scene.AddCommand(
                    "Archiving", this, "save iar",
                    "save iar [-h|--home=<url>] [--noassets] <first> <last> <inventory path> <password> [<IAR path>] [-c|--creators] [-e|--exclude=<name/uuid>] [-f|--excludefolder=<foldername/uuid>] [-v|--verbose]",
                    "Save user inventory archive (IAR).",
                    "<first> is the user's first name.\n"
                    + "<last> is the user's last name.\n"
                    + "<inventory path> is the path inside the user's inventory for the folder/item to be saved.\n"
                    + "<IAR path> is the filesystem path at which to save the IAR."
                    + string.Format("  If this is not given then the filename {0} in the current directory is used.\n", DEFAULT_INV_BACKUP_FILENAME)
                    + "-h|--home=<url> adds the url of the profile service to the saved user information.\n"
                    + "-c|--creators preserves information about foreign creators.\n"
                    + "-e|--exclude=<name/uuid> don't save the inventory item in archive" + Environment.NewLine
                    + "-f|--excludefolder=<folder/uuid> don't save contents of the folder in archive" + Environment.NewLine
                    + "-v|--verbose extra debug messages.\n"
                    + "--noassets stops assets being saved to the IAR."
                    + "--perm=<permissions> stops items with insufficient permissions from being saved to the IAR.\n"
                    + "   <permissions> can contain one or more of these characters: \"C\" = Copy, \"T\" = Transfer, \"M\" = Modify.\n",
                    HandleSaveInvConsoleCommand);

                m_aScene = scene;
            }

            m_scenes[scene.RegionInfo.RegionID] = scene;
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene == null)
                return;

            scene.UnregisterModuleInterface<IInventoryArchiverModule>(this);
            m_scenes.Remove(scene.RegionInfo.RegionID);

            if (ReferenceEquals(m_aScene, scene))
            {
                m_aScene = null;
                foreach (Scene remaining in m_scenes.Values)
                {
                    m_aScene = remaining;
                    break;
                }

                m_UserAccountService = null;
            }
        }

        public void Close() {}

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name { get { return "Inventory Archiver Module"; } }

        #endregion

        /// <summary>
        /// Trigger the inventory archive saved event.
        /// </summary>
        protected internal void TriggerInventoryArchiveSaved(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException, int SaveCount, int FilterCount)
        {
            InventoryArchiveSaved handlerInventoryArchiveSaved = OnInventoryArchiveSaved;
            if (handlerInventoryArchiveSaved != null)
                handlerInventoryArchiveSaved(id, succeeded, userInfo, invPath, saveStream, reportedException, SaveCount , FilterCount);
        }

        /// <summary>
        /// Trigger the inventory archive loaded event.
        /// </summary>
        protected internal void TriggerInventoryArchiveLoaded(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream loadStream,
            Exception reportedException, int LoadCount)
        {
            InventoryArchiveLoaded handlerInventoryArchiveLoaded = OnInventoryArchiveLoaded;
            if (handlerInventoryArchiveLoaded != null)
                handlerInventoryArchiveLoaded(id, succeeded, userInfo, invPath, loadStream, reportedException, LoadCount);
        }

        internal void TriggerInventoryArchiveProgress(UUID id, string phase, int processed, int total)
        {
            var handlers = OnInventoryArchiveProgress;
            if (handlers == null)
                return;
            foreach (Action<UUID, string, int, int> handler in handlers.GetInvocationList())
            {
                try { handler(id, phase, processed, total); }
                catch (Exception e) { m_log.Warn("[OGL-IAR]: Fortschritts-Listener fehlgeschlagen.", e); }
            }
        }

        private static string GermanIarPhase(string phase)
        {
            switch (phase)
            {
                case "inventory": return "Inventar erfassen";
                case "inventory-complete": return "Inventar erfasst";
                case "assets": return "Assets sichern";
                case "import": return "Inventar importieren";
                case "finalizing": return "Archiv abschließen";
                default: return "Archiv verarbeiten";
            }
        }

        private static int GetIarPercent(string phase, int processed, int total)
        {
            if (phase == "inventory-complete") return 40;
            if (phase == "assets" && total > 0)
                return 40 + (int)Math.Min(55L, (long)Math.Max(0, processed) * 55L / total);
            if (phase == "import" && total > 0)
                return 5 + (int)Math.Min(90L, (long)Math.Max(0, processed) * 90L / total);
            if (phase == "finalizing") return 98;
            return 5;
        }

        private static string ProgressBar(int percent)
        {
            int blocks = Math.Clamp(percent / 5, 0, 20);
            return "[" + new string('#', blocks) + new string('-', 20 - blocks) + "]";
        }

        private static string LiveIarSummary(ConsoleIarJob job)
        {
            string counters = job.Total > 0
                ? " " + job.Processed + "/" + job.Total + (job.Phase == "assets" ? " Assets" : " Eintr.")
                : " " + job.Processed + " bearbeitet";
            return "IAR " + job.Action + " " + job.Percent + "% " +
                GermanIarPhase(job.Phase) + counters;
        }

        private void RefreshIarPrompt()
        {
            if (!m_liveConsoleProgress || MainConsole.Instance is not LocalConsole local)
                return;
            string status = string.Empty;
            lock (m_pendingConsoleTasks)
            {
                // Multiple parallel archives remain individually queryable.
                if (m_consoleJobs.Count == 1)
                {
                    foreach (ConsoleIarJob job in m_consoleJobs.Values)
                        status = LiveIarSummary(job);
                }
                else if (m_consoleJobs.Count > 1)
                    status = "IAR: " + m_consoleJobs.Count + " Aufträge aktiv (show iar progress)";
            }
            try { local.UpdateOglIarProgress(status); }
            catch (Exception e) { m_log.Debug("[OGL-IAR]: Live-Anzeige nicht verfügbar.", e); }
        }

        private void StartConsoleIarJob(UUID id, string action, string archive)
        {
            lock (m_pendingConsoleTasks)
            {
                m_pendingConsoleTasks.Add(id);
                m_consoleJobs[id] = new ConsoleIarJob
                {
                    Action = action,
                    Archive = archive
                };
            }
            RefreshIarPrompt();
        }

        private void RemoveConsoleIarJob(UUID id)
        {
            lock (m_pendingConsoleTasks)
            {
                m_pendingConsoleTasks.Remove(id);
                m_consoleJobs.Remove(id);
                m_consoleProgressState.Remove(id);
            }
            RefreshIarPrompt();
        }

        private void ShowConsoleIarProgress(string module, string[] cmdparams)
        {
            lock (m_pendingConsoleTasks)
            {
                if (m_consoleJobs.Count == 0)
                {
                    MainConsole.Instance.Output("[OGL-IAR]: Kein Import oder Export aktiv.");
                    return;
                }

                MainConsole.Instance.Output("[OGL-IAR]: Aktuelle Archivvorgänge:");
                foreach (KeyValuePair<UUID, ConsoleIarJob> pair in m_consoleJobs)
                {
                    ConsoleIarJob job = pair.Value;
                    string unit = job.Phase == "assets" ? "Assets" : "Einträge";
                    string counters = job.Total > 0
                        ? job.Processed + "/" + job.Total + " " + unit
                        : job.Processed + " " + unit + " (Gesamtzahl noch unbekannt)";
                    MainConsole.Instance.Output(
                        "[OGL-IAR]: {0} | {1} | {2} {3}% | {4} | Laufzeit {5:n0} s | ID {6}",
                        job.Action, job.Archive, ProgressBar(job.Percent),
                        job.Percent, GermanIarPhase(job.Phase) + ": " + counters,
                        (DateTime.UtcNow - job.Started).TotalSeconds, pair.Key);
                }
            }
        }

        private void DisplayConsoleArchiveProgress(UUID id, string phase, int processed, int total)
        {
            int percent = GetIarPercent(phase, processed, total);
            bool known = total > 0 || phase == "finalizing";
            bool report;
            bool liveUpdate;
            lock (m_pendingConsoleTasks)
            {
                if (!m_pendingConsoleTasks.Contains(id) ||
                    !m_consoleJobs.TryGetValue(id, out ConsoleIarJob job))
                    return;

                job.Phase = phase;
                job.Processed = Math.Max(0, processed);
                job.Total = Math.Max(0, total);
                job.Percent = percent;
                liveUpdate = job.LastDisplayedPercent != percent ||
                    job.LastDisplayedPhase != phase ||
                    (total <= 0 && processed % 100 == 0);
                if (liveUpdate)
                {
                    job.LastDisplayedPercent = percent;
                    job.LastDisplayedPhase = phase;
                }

                int rounded = known ? percent / 5 * 5 : 0;
                string marker = phase + ":" + (known
                    ? rounded.ToString()
                    : processed == 0 ? "start" : (processed / 250).ToString());
                report = !m_consoleProgressState.TryGetValue(id, out string previous)
                    || previous != marker;
                if (report)
                    m_consoleProgressState[id] = marker;
            }

            // The local console shows the newest value between log milestones,
            // while log4net records only 5%-checkpoints as durable lines.
            if (liveUpdate)
                RefreshIarPrompt();
            if (!report)
                return;

            if (phase == "finalizing")
                m_log.Info("[OGL-IAR]: Abschluss: Archiv wird finalisiert und geprüft (98 %).");
            else if (known)
                m_log.InfoFormat("[OGL-IAR]: {0} {1} {2}% ({3}/{4} {5})",
                    GermanIarPhase(phase), ProgressBar(percent), percent,
                    processed, total, phase == "assets" ? "Assets" : "Einträge");
            else
                m_log.InfoFormat("[OGL-IAR]: {0}: {1} Einträge bearbeitet (Gesamtzahl noch unbekannt).",
                    GermanIarPhase(phase), processed);
        }

        private void ForgetConsoleProgress(UUID id)
        {
            lock (m_pendingConsoleTasks)
                m_consoleProgressState.Remove(id);
        }

        public bool ArchiveInventory(
             UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarExport);

            return ArchiveInventory(id, firstName, lastName, invPath, pass, saveStream, new Dictionary<string, object>());
        }

        public bool ArchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream,
            Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarExport);

            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        try
                        {
                            InventoryArchiveWriteRequest iarReq = new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, saveStream);
                            iarReq.Execute(options, UserAccountService);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        public bool ArchiveInventory(
            UUID id, UserAccount userInfo, string invPath, Stream saveStream, Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarExport);

            if (userInfo == null || saveStream == null || m_scenes.Count == 0)
                return false;

            try
            {
                InventoryArchiveWriteRequest iarReq = new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, saveStream);
                iarReq.Execute(options ?? new Dictionary<string, object>(), UserAccountService);
                return true;
            }
            catch (EntryPointNotFoundException e)
            {
                m_log.Error("[INVENTORY ARCHIVER]: Compression library mismatch during server-authorized IAR export.", e);
                return false;
            }
        }

        public bool DearchiveInventory(
            UUID id, UserAccount userInfo, string invPath, Stream loadStream, Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarImport);

            if (userInfo == null || loadStream == null || m_scenes.Count == 0)
                return false;

            bool merge = options != null && options.ContainsKey("merge") && (bool)options["merge"];
            try
            {
                InventoryArchiveReadRequest request = new InventoryArchiveReadRequest(
                    id, this, m_aScene.InventoryService, m_aScene.AssetService, m_aScene.UserAccountService,
                    userInfo, invPath, loadStream, merge);
                request.ExpectedEntries = GetExpectedIarEntries(options);
                UpdateClientWithLoadedNodes(userInfo, request.Execute());
                return true;
            }
            catch (EntryPointNotFoundException e)
            {
                m_log.Error("[INVENTORY ARCHIVER]: Compression library mismatch during server-authorized IAR import.", e);
                return false;
            }
        }

        public bool ArchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, string savePath,
            Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarExport);

//            if (!ConsoleUtil.CheckFileDoesNotExist(MainConsole.Instance, savePath))
//                return false;

            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        try
                        {
                            InventoryArchiveWriteRequest iarReq  = new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, savePath);
                            iarReq.Execute(options, UserAccountService);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        public bool DearchiveInventory(UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarImport);

            return DearchiveInventory(id, firstName, lastName, invPath, pass, loadStream, new Dictionary<string, object>());
        }

        public bool DearchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream,
            Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarImport);

            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        InventoryArchiveReadRequest request;
                        bool merge = (options.ContainsKey("merge") ? (bool)options["merge"] : false);

                        try
                        {
                            request = new InventoryArchiveReadRequest(id, this, m_aScene.InventoryService, m_aScene.AssetService, m_aScene.UserAccountService, userInfo, invPath, loadStream, merge);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        request.ExpectedEntries = GetExpectedIarEntries(options);
                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
                else
                    m_log.ErrorFormat("[INVENTORY ARCHIVER]: User {0} {1} not found",
                            firstName, lastName);
            }

            return false;
        }

        public bool DearchiveInventory(
             UUID id, string firstName, string lastName, string invPath, string pass, string loadPath,
             Dictionary<string, object> options)
        {
            OglArchiveFeatureGate.Require(OglArchiveFeatureGate.IarImport);

            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        InventoryArchiveReadRequest request;
                        bool merge = (options.ContainsKey("merge") ? (bool)options["merge"] : false);

                        try
                        {
                            request = new InventoryArchiveReadRequest(id, this, m_aScene.InventoryService, m_aScene.AssetService, m_aScene.UserAccountService, userInfo, invPath, loadPath, merge);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        request.ExpectedEntries = GetExpectedIarEntries(options);
                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        private static int GetExpectedIarEntries(Dictionary<string, object> options)
        {
            if (options != null && options.TryGetValue("iar_total_entries", out object raw) &&
                raw is int count && count > 0)
                return count;
            return 0;
        }

        /// <summary>
        /// Load inventory from an inventory file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleLoadInvConsoleCommand(string module, string[] cmdparams)
        {
            if (!OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.IarImport))
            {
                m_log.Warn(OglArchiveFeatureGate.DeniedMessage(OglArchiveFeatureGate.IarImport));
                return;
            }

            UUID id = UUID.Random();
            try
            {
                Dictionary<string, object> options = new Dictionary<string, object>();
                OptionSet optionSet = new OptionSet().Add("m|merge", delegate (string v) { options["merge"] = v != null; });

                List<string> mainParams = optionSet.Parse(cmdparams);

                if (mainParams.Count < 6)
                {
                    m_log.Error(
                        "[OGL-IAR]: Syntax: load iar [-m|--merge] <Vorname> <Nachname> <Inventarpfad> <Passwort> [<Archivpfad>]");
                    return;
                }

                string firstName = mainParams[2];
                string lastName = mainParams[3];
                string invPath = mainParams[4];
                string pass = mainParams[5];
                string loadPath = (mainParams.Count > 6 ? mainParams[6] : DEFAULT_INV_BACKUP_FILENAME);

                m_log.InfoFormat(
                    "[OGL-IAR]: Importiere Archiv {0} nach Inventarpfad {1} für {2} {3}.",
                    loadPath, invPath, firstName, lastName);

                // The same bounded validator used by the managed API must run
                // BEFORE the legacy import writes any inventory or assets.
                IInventoryArchiveInspector inspector =
                    m_aScene?.RequestModuleInterface<IInventoryArchiveInspector>();
                if (inspector == null)
                {
                    m_log.Error("[OGL-IAR]: Import abgelehnt: Sichere IAR-Vorprüfung nicht verfügbar.");
                    return;
                }
                m_log.Info("[OGL-IAR]: Prüfe Archivstruktur, Größenlimits und TAR/GZip-Integrität …");
                if (!inspector.TryInspect(loadPath, out int expectedEntries, out string reason))
                {
                    m_log.ErrorFormat("[OGL-IAR]: Import abgelehnt: {0}", reason);
                    return;
                }
                options["iar_total_entries"] = expectedEntries;
                m_log.InfoFormat("[OGL-IAR]: Archivprüfung erfolgreich: {0} Einträge.", expectedEntries);

                StartConsoleIarJob(id, "Import", loadPath);

                if (!DearchiveInventory(id, firstName, lastName, invPath, pass, loadPath, options))
                {
                    RemoveConsoleIarJob(id);
                }
            }
            catch (Exception e)
            {
                RemoveConsoleIarJob(id);
                m_log.Error("[OGL-IAR]: Import fehlgeschlagen.", e);
            }
        }

        /// <summary>
        /// Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvConsoleCommand(string module, string[] cmdparams)
        {
            if (!OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.IarExport))
            {
                m_log.Warn(OglArchiveFeatureGate.DeniedMessage(OglArchiveFeatureGate.IarExport));
                return;
            }

            UUID id = UUID.Random();

            Dictionary<string, object> options = new Dictionary<string, object>();

            OptionSet ops = new OptionSet();
            //ops.Add("v|version=", delegate(string v) { options["version"] = v; });
            ops.Add("h|home=", delegate(string v) { options["home"] = v; });
            ops.Add("v|verbose", delegate(string v) { options["verbose"] = v; });
            ops.Add("c|creators", delegate(string v) { options["creators"] = v; });
            ops.Add("noassets", delegate(string v) { options["noassets"] = v != null; });
            ops.Add("e|exclude=", delegate(string v)
                {
                    if (!options.ContainsKey("exclude"))
                        options["exclude"] = new List<String>();
                    ((List<String>)options["exclude"]).Add(v);
                });
            ops.Add("f|excludefolder=", delegate(string v)
                {
                    if (!options.ContainsKey("excludefolders"))
                        options["excludefolders"] = new List<String>();
                    ((List<String>)options["excludefolders"]).Add(v);
                });
            ops.Add("perm=", delegate(string v) { options["checkPermissions"] = v; });

            List<string> mainParams = ops.Parse(cmdparams);

            try
            {
                if (mainParams.Count < 6)
                {
                    m_log.Error(
                        "[OGL-IAR]: Syntax: save iar [-h|--home=<URL>] [--noassets] <Vorname> <Nachname> <Inventarpfad> <Passwort> [<Archivpfad>] [-c|--creators] [-e|--exclude=<Name/UUID>] [-f|--excludefolder=<Ordner/UUID>] [-v|--verbose]");
                    return;
                }

                if (options.ContainsKey("home"))
                    m_log.Warn("[OGL-IAR]: Achtung: Mit -home erzeugte Archive sind nicht mit OpenSim 0.7.0.2 oder älter kompatibel.");

                string firstName = mainParams[2];
                string lastName = mainParams[3];
                string invPath = mainParams[4];
                string pass = mainParams[5];
                string savePath = (mainParams.Count > 6 ? mainParams[6] : DEFAULT_INV_BACKUP_FILENAME);

                m_log.InfoFormat(
                    "[OGL-IAR]: Exportiere Archiv {0} aus Inventarpfad {1} für {2} {3}.",
                    savePath, invPath, firstName, lastName);

                StartConsoleIarJob(id, "Export", savePath);

                if (!ArchiveInventory(id, firstName, lastName, invPath, pass, savePath, options))
                {
                    RemoveConsoleIarJob(id);
                }
            }
            catch (Exception e)
            {
                RemoveConsoleIarJob(id);
                m_log.Error("[OGL-IAR]: Export fehlgeschlagen.", e);
            }
        }

        private void SaveInvConsoleCommandCompleted(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException, int SaveCount, int FilterCount)
        {
            lock (m_pendingConsoleTasks)
            {
                if (!m_pendingConsoleTasks.Contains(id))
                    return;
            }

            RemoveConsoleIarJob(id);
            if (succeeded)
            {
                m_log.Info("[OGL-IAR]: [####################] 100% – Exportvorgang abgeschlossen (Asset-Warnungen beachten).");
                // Report success and include item count and filter count (Skipped items due to --perm or --exclude switches)
                if(FilterCount == 0)
                    m_log.InfoFormat("[OGL-IAR]: Archiv für {1} {2} erfolgreich gespeichert: {0} Inventareinträge.", SaveCount, userInfo.FirstName, userInfo.LastName);
                else
                    m_log.InfoFormat("[OGL-IAR]: Archiv für {1} {2} gespeichert: {0} Inventareinträge, {3} durch Ausschluss- oder Berechtigungsfilter übersprungen.", SaveCount, userInfo.FirstName, userInfo.LastName, FilterCount);
            }
            else
            {
                m_log.ErrorFormat(
                    "[OGL-IAR]: Archivexport für {0} {1} fehlgeschlagen: {2}",
                    userInfo?.FirstName, userInfo?.LastName, reportedException?.Message ?? "Unbekannter Fehler");
            }
        }

        private void LoadInvConsoleCommandCompleted(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream loadStream,
            Exception reportedException, int LoadCount)
        {
            lock (m_pendingConsoleTasks)
            {
                if (!m_pendingConsoleTasks.Contains(id))
                    return;
            }

            RemoveConsoleIarJob(id);
            if (succeeded)
            {
                m_log.Info("[OGL-IAR]: [####################] 100% – Import erfolgreich abgeschlossen.");
                m_log.InfoFormat("[OGL-IAR]: {0} Inventareinträge aus Archiv nach {1} für {2} {3} importiert.", LoadCount, invPath, userInfo.FirstName, userInfo.LastName);
            }
            else
            {
                m_log.ErrorFormat(
                    "[OGL-IAR]: Archivimport für {0} {1} fehlgeschlagen: {2}",
                    userInfo?.FirstName, userInfo?.LastName, reportedException?.Message ?? "Unbekannter Fehler");
            }
        }

        /// <summary>
        /// Get user information for the given name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="pass">User password</param>
        /// <returns></returns>
        protected UserAccount GetUserInfo(string firstName, string lastName, string pass)
        {
            UserAccount account
                = m_aScene.UserAccountService.GetUserAccount(m_aScene.RegionInfo.ScopeID, firstName, lastName);

            if (null == account)
            {
                m_log.ErrorFormat(
                    "[OGL-IAR]: Benutzerkonto für {0} {1} nicht gefunden.",
                    firstName, lastName);
                return null;
            }

            try
            {
                string encpass = Util.Md5Hash(pass);
                if (m_aScene.AuthenticationService.Authenticate(account.PrincipalID, encpass, 1) != string.Empty)
                {
                    return account;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[OGL-IAR]: Ungültiges Passwort für {0} {1}. Bitte erneut versuchen.",
                        firstName, lastName);
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[OGL-IAR]: Passwortauthentifizierung fehlgeschlagen: {0}", e);
                return null;
            }
        }

        /// <summary>
        /// Notify the client of loaded nodes if they are logged in
        /// </summary>
        /// <param name="loadedNodes">Can be empty.  In which case, nothing happens</param>
        private void UpdateClientWithLoadedNodes(UserAccount userInfo, Dictionary<UUID, InventoryNodeBase> loadedNodes)
        {
            if (loadedNodes.Count == 0)
                return;

            foreach (Scene scene in m_scenes.Values)
            {
                ScenePresence user = scene.GetScenePresence(userInfo.PrincipalID);

                if (user != null && !user.IsChildAgent)
                {
                    foreach (InventoryNodeBase node in loadedNodes.Values)
                    {
//                        m_log.DebugFormat(
//                            "[INVENTORY ARCHIVER]: Notifying {0} of loaded inventory node {1}",
//                            user.Name, node.Name);

                        user.ControllingClient.SendBulkUpdateInventory(node);
                    }

                    break;
                }
            }
        }

//        /// <summary>
//        /// Check if the given user is present in any of the scenes.
//        /// </summary>
//        /// <param name="userId">The user to check</param>
//        /// <returns>true if the user is in any of the scenes, false otherwise</returns>
//        protected bool CheckPresence(UUID userId)
//        {
//            if (DisablePresenceChecks)
//                return true;
//
//            foreach (Scene scene in m_scenes.Values)
//            {
//                ScenePresence p;
//                if ((p = scene.GetScenePresence(userId)) != null)
//                {
//                    p.ControllingClient.SendAgentAlertMessage("Inventory operation has been started", false);
//                    return true;
//                }
//            }
//
//            return false;
//        }
    }
}
