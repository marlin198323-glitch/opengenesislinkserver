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

        public OGLDiskCache(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Cache directory is required.", nameof(directory));
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
                    if (!File.Exists(path))
                        return false;
                    using (FileStream stream = File.OpenRead(path))
                    {
                        if (stream.Length < 32 || stream.Length > Int32.MaxValue)
                            return false;
                        byte[] digest = new byte[32];
                        if (stream.Read(digest, 0, digest.Length) != digest.Length)
                            return false;
                        byte[] payload = new byte[(int)stream.Length - 32];
                        stream.ReadExactly(payload);
                        if (!CryptographicOperations.FixedTimeEquals(digest, SHA256.HashData(payload)))
                        {
                            TryDelete(path);
                            return false;
                        }
                        data = payload;
                        return true;
                    }
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        public void Put(string key, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
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
                }
                finally { TryDelete(temp); }
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
