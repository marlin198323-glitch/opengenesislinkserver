using System;
using System.IO;
using System.Security.Cryptography;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Local, content-addressed payload storage primitive for a future federated
    /// node. It is not an asset directory, replication service or remote endpoint.
    /// </summary>
    public sealed class OGLNodeObjectStore
    {
        private readonly string root;

        public OGLNodeObjectStore(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Storage directory required.", nameof(directory));
            root = Path.GetFullPath(directory);
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Storage root must not be a symbolic link.");
        }

        public string Put(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            string target = Resolve(hash);
            string parent = Path.GetDirectoryName(target);
            Directory.CreateDirectory(parent);
            if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Storage shard must not be a symbolic link.");
            if (File.Exists(target))
            {
                if (Get(hash) == null)
                    throw new InvalidDataException("Existing object failed integrity validation.");
                return hash;
            }
            string temporary = Path.Combine(parent, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    output.Write(payload, 0, payload.Length);
                    output.Flush(true);
                }
                try { File.Move(temporary, target, false); }
                catch (IOException) when (File.Exists(target))
                {
                    if (Get(hash) == null)
                        throw new InvalidDataException("Concurrent object failed integrity validation.");
                }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return hash;
        }

        public byte[] Get(string hash)
        {
            string path = Resolve(hash);
            if (!File.Exists(path)) return null;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Object path must not be a symbolic link.");
            byte[] bytes = File.ReadAllBytes(path);
            string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!String.Equals(actual, hash, StringComparison.Ordinal))
                throw new InvalidDataException("Object SHA-256 mismatch.");
            return bytes;
        }

        private string Resolve(string hash)
        {
            if (hash == null || hash.Length != 64)
                throw new ArgumentException("Invalid SHA-256 object identifier.", nameof(hash));
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Object identifier must be lowercase hexadecimal.", nameof(hash));
            return Path.Combine(root, hash.Substring(0, 2), hash.Substring(2, 2), hash);
        }
    }
}
