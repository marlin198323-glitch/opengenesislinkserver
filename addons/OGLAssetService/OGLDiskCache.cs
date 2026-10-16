using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Disposable, content-verified SSD cache. Never a durable asset replica.
    /// Entries are keyed by a SHA-256 digest of the asset identifier and contain
    /// a SHA-256 checksum of the payload. A corrupt entry is treated as a miss.
    /// </summary>
    public sealed class OGLDiskCache
    {
        private readonly string root;
        private readonly object sync = new object();
        private readonly long maxBytes;
        private long hits, misses, evictions;
        public long Hits { get { lock(sync) return hits; } }
        public long Misses { get { lock(sync) return misses; } }
        public long Evictions { get { lock(sync) return evictions; } }

        public OGLDiskCache(string directory, long maxBytes = 0)
        {
            if (String.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Cache directory is required.", nameof(directory));
            if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            this.maxBytes = maxBytes;
            root = Path.GetFullPath(directory);
            Directory.CreateDirectory(root);
        }

        private string EntryPath(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key is required.", nameof(key));
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            string hash = Convert.ToHexString(bytes).ToLowerInvariant();
            return Path.Combine(root, hash.Substring(0, 2), hash + ".cache");
        }

        public bool TryGet(string key, out byte[] data)
        {
            data = null;
            string path = EntryPath(key);
            lock (sync)
            {
                try
                {
                    if (!File.Exists(path)) { misses++; return false; }
                    using (FileStream stream = File.OpenRead(path))
                    {
                        if (stream.Length < 32 || stream.Length > Int32.MaxValue || (maxBytes > 0 && stream.Length > maxBytes)) { misses++; return false; }
                        byte[] digest = new byte[32];
                        if (stream.Read(digest, 0, digest.Length) != digest.Length) { misses++; return false; }
                        byte[] payload = new byte[(int)stream.Length - 32];
                        stream.ReadExactly(payload);
                        if (!CryptographicOperations.FixedTimeEquals(digest, SHA256.HashData(payload)))
                        {
                            misses++;
                            TryDelete(path);
                            return false;
                        }
                        data = payload;
                        hits++;
                        try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                        return true;
                    }
                }
                catch (IOException) { misses++; return false; }
                catch (UnauthorizedAccessException) { misses++; return false; }
            }
        }

        public void Put(string key, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (maxBytes > 0 && data.LongLength > maxBytes - 32) return;
            string path = EntryPath(key);
            lock (sync)
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] digest = SHA256.HashData(data);
                        stream.Write(digest);
                        stream.Write(data);
                        stream.Flush(true);
                    }
                    File.Move(temp, path, true);
                    if (maxBytes > 0) TrimToBudget();
                }
                finally { TryDelete(temp); }
            }
        }

        // Best-effort LRU by last-write timestamp, confined to this cache directory.
        // A dedicated directory and a single writer process are required.
        private void TrimToBudget()
        {
            var files = new System.Collections.Generic.List<FileInfo>();
            long total = 0;
            foreach (string file in Directory.EnumerateFiles(root, "*.cache", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if (!info.Exists) continue;
                files.Add(info);
                total += info.Length;
            }
            if (total <= maxBytes) return;
            files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            foreach (FileInfo info in files)
            {
                if (total <= maxBytes) break;
                try { long length = info.Length; info.Delete(); total -= length; evictions++; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public void Invalidate(string key)
        {
            lock (sync) TryDelete(EntryPath(key));
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
