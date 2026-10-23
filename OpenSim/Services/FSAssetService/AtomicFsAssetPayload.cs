using System;
using System.IO;
using System.Security.Cryptography;

namespace OpenSim.Services.FSAssetService
{
    /// <summary>
    /// Crash-aware content-addressed spool publication for experimental
    /// FSAssets updates. Never replaces an existing digest file.
    /// </summary>
    public static class AtomicFsAssetPayload
    {
        public static bool TryPublish(string spoolDirectory, string hash, byte[] content)
        {
            if (String.IsNullOrWhiteSpace(spoolDirectory) || content == null ||
                String.IsNullOrWhiteSpace(hash) || hash.Length != 64)
                return false;
            for (int i = 0; i < hash.Length; i++)
                if (!Uri.IsHexDigit(hash[i])) return false;

            string digest = Convert.ToHexString(SHA256.HashData(content));
            if (!String.Equals(digest, hash, StringComparison.OrdinalIgnoreCase))
                return false;

            // Prevent publishing through a symlinked spool root. This check
            // rejects an accidental redirect; deployment must still ensure
            // that the storage root is owned by the service account.
            if (Directory.Exists(spoolDirectory) &&
                (File.GetAttributes(spoolDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("FSAssets spool root must not be a symbolic link.");

            Directory.CreateDirectory(spoolDirectory);
            string finalFile = Path.Combine(spoolDirectory, hash.ToLowerInvariant() + ".asset");
            string staging = Path.Combine(spoolDirectory, "spool");
            if (Directory.Exists(staging) &&
                (File.GetAttributes(staging) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("FSAssets staging directory must not be a symbolic link.");
            Directory.CreateDirectory(staging);

            if (File.Exists(finalFile) &&
                (File.GetAttributes(finalFile) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("FSAssets digest destination must not be a symbolic link.");

            if (!File.Exists(finalFile))
            {
                string tempFile = Path.Combine(staging, Guid.NewGuid().ToString("N") + ".asset");
                try
                {
                    using (FileStream stream = new FileStream(tempFile, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    {
                        stream.Write(content, 0, content.Length);
                        stream.Flush(true);
                    }
                    try
                    {
                        // overwrite:false is essential: a racing writer must
                        // not replace an already published digest file.
                        File.Move(tempFile, finalFile, false);
                    }
                    catch (IOException)
                    {
                        if (!File.Exists(finalFile)) throw;
                    }
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
            }

            // Existing files can be corrupt. Never allow their digest to
            // become authoritative without verifying actual persisted bytes.
            if ((File.GetAttributes(finalFile) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("FSAssets digest destination must not be a symbolic link.");
            byte[] stored = File.ReadAllBytes(finalFile);
            return stored.AsSpan().SequenceEqual(content);
        }
    }
}
