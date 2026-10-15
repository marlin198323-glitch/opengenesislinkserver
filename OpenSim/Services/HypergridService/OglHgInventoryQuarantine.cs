// OpenGenesisLINK HG InventoryGuard: isolated evidence storage for rejected HG assets.
// Rejected assets are NEVER inserted into the production asset service from here.
// Evidence storage is NOT a pending-transfer workflow or a safe automatic release API.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OpenSim.Framework;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Private, bounded evidence records for assets rejected by the HG ingress inspector.
    /// This only handles rejected payloads; it cannot retroactively remove inventory
    /// references written by legacy HG, and it does not authorize remote callers.
    /// Linux-only for this first version so restrictive local file modes are enforced.
    /// </summary>
    public sealed class OglHgInventoryQuarantine
    {
        public const int DefaultMaxCaptureBytes = 256 * 1024;
        public const int DefaultMaxRecords = 5000;
        private const int MaximumHashBytes = 32 * 1024 * 1024;
        private const UnixFileMode PrivateDirectoryMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        private const UnixFileMode PrivateFileMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;

        private readonly string m_directory;
        private readonly int m_maxCaptureBytes;
        private readonly int m_maxRecords;
        private readonly object m_sync = new object();

        public OglHgInventoryQuarantine(string directory, int maxCaptureBytes, int maxRecords)
        {
            // Refuse a fallback into bin/, web roots or relative paths.
            if (OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "HG quarantine requires a verified private filesystem ACL; Windows support is pending");

            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
                throw new ArgumentException("QuarantineDirectory must be an absolute private path");

            if (maxCaptureBytes < 0 || maxCaptureBytes > 1024 * 1024 ||
                maxRecords < 1 || maxRecords > 100000)
                throw new ArgumentOutOfRangeException(nameof(maxCaptureBytes),
                    "QuarantineMaxCaptureBytes must be 0..1048576 and MaxRecords 1..100000");

            m_directory = Path.GetFullPath(directory);
            // Never tighten permissions on a filesystem root or a generic system directory.
            string root = Path.GetPathRoot(m_directory);
            if (string.Equals(m_directory, root, StringComparison.Ordinal) ||
                string.Equals(m_directory, "/tmp", StringComparison.Ordinal) ||
                string.Equals(m_directory, "/var", StringComparison.Ordinal) ||
                string.Equals(m_directory, "/var/lib", StringComparison.Ordinal) ||
                string.Equals(m_directory, "/opt", StringComparison.Ordinal) ||
                string.Equals(m_directory, "/home", StringComparison.Ordinal) ||
                string.Equals(m_directory, "/root", StringComparison.Ordinal))
                throw new ArgumentException("QuarantineDirectory must be a dedicated private subdirectory");

            m_maxCaptureBytes = maxCaptureBytes;
            m_maxRecords = maxRecords;

            Directory.CreateDirectory(m_directory);
            if ((File.GetAttributes(m_directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Quarantine root cannot be a symbolic link");
            File.SetUnixFileMode(m_directory, PrivateDirectoryMode);
        }

        /// <summary>
        /// Capture bounded evidence of an already rejected asset. Never throws to HG request
        /// callers. Returns false when disk/quota/mode operations fail; import stays rejected.
        /// No untrusted names or paths are used in filenames.
        /// </summary>
        public bool TryCapture(AssetBase asset, string reason, out string recordId)
        {
            recordId = null;
            if (asset == null || asset.Data == null ||
                string.IsNullOrWhiteSpace(reason))
                return false;

            lock (m_sync)
            {
                try
                {
                    // Fixed quota: require operator retention/cleanup on exhaustion.
                    // Count only committed metadata records, not half-written payloads.
                    int records = 0;
                    foreach (string unused in Directory.EnumerateFiles(
                        m_directory, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        if (++records >= m_maxRecords)
                            return false;
                    }

                    string id = Guid.NewGuid().ToString("N");
                    string dataPath = Path.Combine(m_directory, id + ".payload");
                    string metaPath = Path.Combine(m_directory, id + ".json");
                    int captureLength = Math.Min(m_maxCaptureBytes, asset.Data.Length);
                    try
                    {
                        if (captureLength > 0)
                        {
                            using (FileStream file = CreatePrivateFile(dataPath))
                            {
                                file.Write(asset.Data, 0, captureLength);
                                file.Flush(true);
                            }
                        }

                        string sha256 = asset.Data.Length <= MaximumHashBytes ?
                            Convert.ToHexString(SHA256.HashData(asset.Data)) : null;

                        // No original asset Name, CreatorID or script content in metadata.
                        // Payloads can be sensitive; access is strictly local/operator-only.
                        var evidence = new
                        {
                            id,
                            utc = DateTimeOffset.UtcNow.ToString("O"),
                            status = "rejected_evidence_only",
                            asset_id = asset.FullID.ToString(),
                            asset_type = (int)asset.Type,
                            reason,
                            input_bytes = asset.Data.Length,
                            captured_bytes = captureLength,
                            truncated = captureLength != asset.Data.Length,
                            sha256_full = sha256
                        };

                        byte[] json = JsonSerializer.SerializeToUtf8Bytes(evidence);
                        using (FileStream file = CreatePrivateFile(metaPath))
                        {
                            file.Write(json, 0, json.Length);
                            file.Flush(true);
                        }

                        recordId = id;
                        return true;
                    }
                    catch
                    {
                        // Do not leave partial or orphaned evidence files when committing
                        // the metadata fails. Best-effort cleanup; import still rejected.
                        try { if (File.Exists(metaPath)) File.Delete(metaPath); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                        try { if (File.Exists(dataPath)) File.Delete(dataPath); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                        throw;
                    }
                }
                catch (Exception e) when (
                    e is IOException || e is UnauthorizedAccessException ||
                    e is ArgumentException || e is System.Security.SecurityException)
                {
                    // Caller's rejection still takes effect when evidence cannot be written.
                    return false;
                }
            }
        }

        private static FileStream CreatePrivateFile(string path)
        {
            // Parent directory was set to 0700 before creating any files.
            FileStream output = new FileStream(path,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.WriteThrough,
                    UnixCreateMode = PrivateFileMode
                });
            return output;
        }
    }
}
