// SPDX-License-Identifier: MPL-2.0
// R617: pure thread-safe NPC source index. No HTTP or simulator dependency.
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcOwnerEntry
    {
        public Guid npc_id { get; set; }
        public Guid owner_principal_id { get; set; }
        public string name { get; set; }
        public string region_name { get; set; }
    }

    internal sealed class NexNpcSnapshotIndex
    {
        private sealed class Region
        {
            internal long Sequence;
            internal DateTimeOffset LastSeen;
            internal List<NexNpcOwnerEntry> Npcs;
        }

        private readonly object m_Guard = new object();
        private readonly Dictionary<Guid, Region> m_Regions =
            new Dictionary<Guid, Region>();
        private long m_Version;
        private readonly int m_RequiredRegions;
        private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(100);

        internal NexNpcSnapshotIndex(int requiredRegions)
        {
            if (requiredRegions <= 0 || requiredRegions > 1024)
                throw new ArgumentOutOfRangeException(nameof(requiredRegions));
            m_RequiredRegions = requiredRegions;
        }

        // The presence of an expired snapshot never implies "zero NPCs".
        // Restart starts unready until a live simulator proves its own state.
        internal bool TryOwned(Guid owner, out List<NexNpcOwnerEntry> owned,
            out long revision)
        {
            owned = new List<NexNpcOwnerEntry>();
            lock (m_Guard)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var fresh = m_Regions.Values.Where(x => now - x.LastSeen <= FreshFor).ToList();
                revision = m_Version;
                if (fresh.Count < m_RequiredRegions || fresh.Count != m_Regions.Count)
                    return false;
                // A conflicting owner claim from different reporting regions
                // invalidates the directory rather than exposing an NPC to
                // both accounts.
                var owners = new Dictionary<Guid,Guid>();
                foreach (Region region in fresh)
                    foreach (NexNpcOwnerEntry npc in region.Npcs)
                    {
                        if (owners.TryGetValue(npc.npc_id,out Guid previous)
                            && previous != npc.owner_principal_id)
                            return false;
                        owners[npc.npc_id] = npc.owner_principal_id;
                    }
                var seen = new HashSet<Guid>();
                foreach (Region region in fresh)
                    foreach (NexNpcOwnerEntry npc in region.Npcs)
                        if (npc.owner_principal_id == owner && seen.Add(npc.npc_id))
                            owned.Add(npc);
                owned.Sort((a,b) => a.npc_id.CompareTo(b.npc_id));
                return true;
            }
        }

        // An authorized *complete* snapshot replaces one simulator region;
        // monotonic timestamps reject duplicated/replayed state changes.
        internal bool Replace(Guid regionID, long sequence,
            List<NexNpcOwnerEntry> entries)
        {
            if (regionID == Guid.Empty || sequence <= 0 || entries == null ||
                entries.Count > 1000 ||
                entries.Any(n => n == null || n.npc_id == Guid.Empty ||
                    n.owner_principal_id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(n.name) || n.name.Length > 120 ||
                    (n.region_name ?? "").Length > 120) ||
                entries.GroupBy(n => n.npc_id).Any(g => g.Count() != 1))
                return false;

            lock (m_Guard)
            {
                if (m_Regions.TryGetValue(regionID, out Region last))
                {
                    if (sequence <= last.Sequence) return false;
                }
                else if (m_Regions.Count >= 1024) return false;
                m_Regions[regionID] = new Region
                {
                    Sequence = sequence,
                    LastSeen = DateTimeOffset.UtcNow,
                    Npcs = entries.ToList()
                };
                ++m_Version;
            }
            return true;
        }
    }

 }
