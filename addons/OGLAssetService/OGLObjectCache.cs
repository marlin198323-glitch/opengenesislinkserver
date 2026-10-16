using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Bounded, thread-safe in-memory cache of immutable payload snapshots.
    /// Never evicts durable storage. Payloads are copied on insertion and retrieval.
    /// </summary>
    public sealed class OGLObjectCache
    {
        private sealed class Entry
        {
            public string Key;
            public byte[] Payload;
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, LinkedListNode<Entry>> entries =
            new Dictionary<string, LinkedListNode<Entry>>(StringComparer.Ordinal);
        private readonly LinkedList<Entry> lru = new LinkedList<Entry>();
        private readonly long capacityBytes;
        private long usedBytes;
        private long hits;
        private long misses;

        public OGLObjectCache(long capacityBytes)
        {
            if (capacityBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityBytes));
            this.capacityBytes = capacityBytes;
        }

        public long UsedBytes { get { lock (sync) return usedBytes; } }
        public long Hits { get { lock (sync) return hits; } }
        public long Misses { get { lock (sync) return misses; } }
        public int Count { get { lock (sync) return entries.Count; } }

        public bool TryGet(string key, out byte[] payload)
        {
            payload = null;
            if (String.IsNullOrEmpty(key))
                return false;
            lock (sync)
            {
                LinkedListNode<Entry> node;
                if (!entries.TryGetValue(key, out node))
                {
                    misses++;
                    return false;
                }
                lru.Remove(node);
                lru.AddFirst(node);
                hits++;
                payload = (byte[])node.Value.Payload.Clone();
                return true;
            }
        }

        public void Put(string key, byte[] payload)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentException("An object key is required.", nameof(key));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            lock (sync)
            {
                RemoveCore(key);
                if (capacityBytes == 0 || payload.LongLength > capacityBytes)
                    return;
                while (usedBytes > capacityBytes - payload.LongLength)
                    RemoveCore(lru.Last.Value.Key);
                byte[] snapshot = (byte[])payload.Clone();
                LinkedListNode<Entry> node = lru.AddFirst(new Entry { Key = key, Payload = snapshot });
                entries.Add(key, node);
                usedBytes += snapshot.LongLength;
            }
        }

        public void Invalidate(string key)
        {
            lock (sync) RemoveCore(key);
        }

        private void RemoveCore(string key)
        {
            LinkedListNode<Entry> node;
            if (entries.TryGetValue(key, out node))
            {
                usedBytes -= node.Value.Payload.LongLength;
                lru.Remove(node);
                entries.Remove(key);
            }
        }

        public static string ContentHash(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            using (SHA256 sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(payload)).ToLowerInvariant();
        }
    }
}
