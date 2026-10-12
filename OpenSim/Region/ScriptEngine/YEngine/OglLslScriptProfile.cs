// OpenGenesisLINK-specific operational diagnostics (MPL-2.0).
// Inherited YEngine files and their existing BSD notices remain unchanged.
using System;
using System.Threading;
using OpenMetaverse;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /// <summary>
    /// A read-only, bounded YEngine snapshot, not a promise of equivalent SL
    /// script execution cost. No scripts, chat, permissions or code are exposed.
    /// </summary>
    public sealed class OglLslScriptProfile
    {
        public UUID ScriptItemId { get; }
        public UUID ObjectId { get; }
        public double ElapsedScriptMilliseconds { get; }
        public int EventsStarted { get; }
        public int EventSlicesResumed { get; }
        public int PendingEvents { get; }
        public bool IsRunning { get; }
        public DateTime ObservedAtUtc { get; }

        internal OglLslScriptProfile(
            UUID scriptItemId, UUID objectId, double elapsedScriptMilliseconds,
            int eventsStarted, int eventSlicesResumed, int pendingEvents, bool isRunning)
        {
            ScriptItemId = scriptItemId;
            ObjectId = objectId;
            ElapsedScriptMilliseconds = elapsedScriptMilliseconds;
            EventsStarted = eventsStarted;
            EventSlicesResumed = eventSlicesResumed;
            PendingEvents = pendingEvents;
            IsRunning = isRunning;
            ObservedAtUtc = DateTime.UtcNow;
        }
    }

    public partial class XMRInstance
    {
        /// <summary>
        /// Non-blocking snapshot of counters which YEngine already maintains.
        /// Does not change scheduling or allocation cost for normal scripts.
        /// Caller must possess server-side diagnostic authorization; this API
        /// is not automatically exposed to LSL, residents or HTTP.
        /// </summary>
        public bool TryGetOglLslScriptProfile(out OglLslScriptProfile profile)
        {
            profile = null;
            // Maintain RunOne lock order: m_RunLock before m_QueueLock.
            // Never stall the script thread for a management query.
            if (!Monitor.TryEnter(m_RunLock))
                return false;
            try
            {
                if (m_Item is null || m_IState == XMRInstState.DISPOSED)
                    return false;
                lock (m_QueueLock)
                {
                    profile = new OglLslScriptProfile(
                        m_ItemID, m_PartUUID, m_CPUTime, m_InstEHEvent,
                        m_InstEHSlice, m_EventQueue.Count, m_Running);
                }
                return true;
            }
            finally
            {
                Monitor.Exit(m_RunLock);
            }
        }
    }
}
