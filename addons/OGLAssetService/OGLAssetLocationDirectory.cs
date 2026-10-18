using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Prototype durable UUID-to-node directory. Single-process writer only:
    /// not a substitute for transactional SQL or a multi-node consensus protocol.
    /// </summary>
    public sealed class OGLAssetLocationDirectory
    {
        private readonly object gate = new object();
        private readonly string path;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public sealed class Entry
        {
            public string AssetId { get; set; }
            public string ContentHash { get; set; }
            public string PrimaryNode { get; set; }
            public List<string> Replicas { get; set; } = new List<string>();
            public long Revision { get; set; }
        }

        public OGLAssetLocationDirectory(string filePath)
        {
            if (String.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Directory file path required.", nameof(filePath));
            path = Path.GetFullPath(filePath);
            if (File.Exists(path))
            {
                foreach (Entry entry in JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path))
                    ?? new List<Entry>())
                {
                    Validate(entry);
                    if (!entries.TryAdd(entry.AssetId, Copy(entry)))
                        throw new InvalidDataException("Duplicate asset UUID in location directory.");
                }
            }
        }

        public Entry Find(string assetId)
        {
            string id = NormalizeAssetId(assetId);
            lock (gate) return entries.TryGetValue(id, out Entry entry) ? Copy(entry) : null;
        }

        /// <summary>
        /// Expected revision 0 creates a new entry; existing entries must match
        /// their current revision. The directory is a prototype, not a lease.
        /// </summary>
        public Entry Publish(Entry candidate, long expectedRevision)
        {
            Validate(candidate);
            if (expectedRevision < 0 || expectedRevision == long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            lock (gate)
            {
                bool exists = entries.TryGetValue(candidate.AssetId, out Entry current);
                if ((!exists && expectedRevision != 0) ||
                    (exists && current.Revision != expectedRevision))
                    throw new InvalidOperationException("Asset directory revision conflict.");
                Entry next = Copy(candidate);
                next.Revision = expectedRevision + 1;
                Dictionary<string, Entry> snapshot = new Dictionary<string, Entry>(entries, StringComparer.Ordinal);
                snapshot[next.AssetId] = next;
                Persist(snapshot);
                entries[next.AssetId] = next;
                return Copy(next);
            }
        }

        private void Persist(Dictionary<string, Entry> snapshot)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    JsonSerializer.Serialize(stream, snapshot.Values);
                    stream.Flush(true);
                }
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static string NormalizeAssetId(string id)
        {
            if (!Guid.TryParseExact(id, "D", out Guid uuid) || uuid == Guid.Empty)
                throw new ArgumentException("Non-empty canonical asset UUID required.");
            return uuid.ToString("D");
        }

        private static void Validate(Entry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entry.AssetId = NormalizeAssetId(entry.AssetId);
            if (entry.ContentHash == null || entry.ContentHash.Length != 64)
                throw new ArgumentException("SHA-256 content hash required.");
            foreach (char c in entry.ContentHash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Lowercase SHA-256 required.");
            if (String.IsNullOrWhiteSpace(entry.PrimaryNode) || entry.PrimaryNode.Length > 128)
                throw new ArgumentException("Primary node identity required.");
            if (entry.Replicas == null || entry.Replicas.Count > 32)
                throw new ArgumentException("Invalid replica set.");
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            seen.Add(entry.PrimaryNode);
            foreach (string node in entry.Replicas)
                if (String.IsNullOrWhiteSpace(node) || node.Length > 128 || !seen.Add(node))
                    throw new ArgumentException("Invalid or duplicate replica node.");
            if (entry.Revision < 0)
                throw new ArgumentException("Invalid revision.");
        }

        private static Entry Copy(Entry e) => new Entry
        {
            AssetId = e.AssetId,
            ContentHash = e.ContentHash,
            PrimaryNode = e.PrimaryNode,
            Replicas = new List<string>(e.Replicas),
            Revision = e.Revision
        };
    }
}
