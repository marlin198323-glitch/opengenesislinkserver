// OpenGenesisLINK InventoryGuard - phase 3c: isolated untrusted byte staging.
// NOT callable through the inventory wire API; NOT the main asset service.
// Payloads remain inaccessible to normal inventory readers and script engines.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    /// <summary>
    /// Stores an authenticated local transfer's raw bytes outside inventory/FSAssets.
    /// No acceptance/restore method is exposed. Use only AFTER a short-lived
    /// OglQuarantineTransferAction.Stage grant has been successfully consumed.
    /// </summary>
    public sealed class OglQuarantineTransferStageStore
    {
        public const int DefaultMaxAssetBytes = 8 * 1024 * 1024;
        public const int DefaultMaxRecords = 2000;
        public const long DefaultMaxTotalBytes = 1024L * 1024 * 1024;

        private const UnixFileMode PrivateDir =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        private const UnixFileMode PrivateFile =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;
        private readonly string m_directory;
        private readonly int m_maxAssetBytes;
        private readonly int m_maxRecords;
        private readonly long m_maxTotalBytes;

        public OglQuarantineTransferStageStore(string directory, int maxAssetBytes,
            int maxRecords, long maxTotalBytes)
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException(
                    "Staging requires verified private Linux filesystem permissions");
            if (string.IsNullOrWhiteSpace(directory) ||
                !Path.IsPathFullyQualified(directory))
                throw new ArgumentException("Staging directory must be an absolute private path");
            if (maxAssetBytes < 1 || maxAssetBytes > 32 * 1024 * 1024 ||
                maxRecords < 1 || maxRecords > 100000 ||
                maxTotalBytes < maxAssetBytes || maxTotalBytes > 32L * 1024 * 1024 * 1024)
                throw new ArgumentException("Invalid size, record or total staging quota");

            m_directory = Path.GetFullPath(directory);
            string root = Path.GetPathRoot(m_directory);
            if (m_directory == root || m_directory == "/tmp" ||
                m_directory == "/var" || m_directory == "/var/lib" ||
                m_directory == "/opt" || m_directory == "/home" ||
                m_directory == "/root")
                throw new ArgumentException("Staging requires a dedicated subdirectory");
            Directory.CreateDirectory(m_directory);
            if ((File.GetAttributes(m_directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Staging root must not be a symbolic link");
            File.SetUnixFileMode(m_directory, PrivateDir);
            m_maxAssetBytes = maxAssetBytes;
            m_maxRecords = maxRecords;
            m_maxTotalBytes = maxTotalBytes;
        }

        /// <summary>
        /// Never stores a partial asset as a successful record. A crash may leave
        /// a reserved .payload with no manifest; it is not treated as accepted
        /// and requires explicit operator reconciliation.
        /// </summary>
        public bool TryStage(UUID verifiedOwner, UUID caseId, UUID itemId,
            UUID assetId, int assetType, byte[] payload, out string reason)
        {
            reason = "stage_unavailable";
            if (verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                itemId == UUID.Zero || assetId == UUID.Zero ||
                payload == null || payload.Length == 0)
            {
                reason = "invalid_stage_identity_or_payload";
                return false;
            }
            if (payload.Length > m_maxAssetBytes)
            {
                reason = "stage_asset_size_limit";
                return false;
            }
            if (assetType < sbyte.MinValue || assetType > sbyte.MaxValue)
            {
                reason = "invalid_asset_type";
                return false;
            }

            // Stable, opaque case key; no foreign name/UUID is a literal path.
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                verifiedOwner.ToString() + "|" + caseId.ToString())));
            string dataPath = Path.Combine(m_directory, key + ".payload");
            string metaPath = Path.Combine(m_directory, key + ".json");
            string lockPath = Path.Combine(m_directory, ".stage.lock");
            bool createdPayload = false;
            bool createdManifest = false;
            try
            {
                // Cross-process exclusive access serializes quota checks and
                // stage commits on a shared local POSIX filesystem.
                using (FileStream exclusive = new FileStream(lockPath,
                    new FileStreamOptions {
                        Mode = FileMode.OpenOrCreate,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        UnixCreateMode = PrivateFile
                    }))
                {
                    int records = 0;
                    long usedBytes = 0;
                    foreach (string path in Directory.EnumerateFiles(
                        m_directory, "*.payload", SearchOption.TopDirectoryOnly))
                    {
                        if (++records >= m_maxRecords)
                        {
                            reason = "stage_record_quota";
                            return false;
                        }
                        long length = new FileInfo(path).Length;
                        if (length > m_maxTotalBytes - usedBytes)
                        {
                            reason = "stage_storage_quota";
                            return false;
                        }
                        usedBytes += length;
                    }
                    if (payload.Length > m_maxTotalBytes - usedBytes)
                    {
                        reason = "stage_storage_quota";
                        return false;
                    }
                    if (File.Exists(dataPath) || File.Exists(metaPath))
                    {
                        reason = "stage_case_already_exists";
                        return false;
                    }

                    try
                    {
                        using (FileStream output = CreatePrivate(dataPath))
                        {
                            createdPayload = true;
                            output.Write(payload, 0, payload.Length);
                            output.Flush(true);
                        }
                        // Evidence manifest is committed *last*, after durable
                        // payload write. Never load data into live FSAssets.
                        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
                        {
                            status = "staged_uninspected",
                            created_utc = DateTimeOffset.UtcNow.ToString("O"),
                            owner_id = verifiedOwner.ToString(),
                            case_id = caseId.ToString(),
                            item_id = itemId.ToString(),
                            asset_id = assetId.ToString(),
                            asset_type = assetType,
                            byte_length = payload.Length,
                            sha256 = Convert.ToHexString(SHA256.HashData(payload))
                        });
                        using (FileStream output = CreatePrivate(metaPath))
                        {
                            createdManifest = true;
                            output.Write(manifest, 0, manifest.Length);
                            output.Flush(true);
                        }
                        reason = null;
                        return true;
                    }
                    catch
                    {
                        // Staged contents are never automatically promoted.
                        // A failed commit cleans both paths if possible.
                        if (createdManifest)
                            TryDelete(metaPath);
                        if (createdPayload)
                            TryDelete(dataPath);
                        throw;
                    }
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        /// <summary>
        /// Read-only integrity verification for internal reconciliation.
        /// No asset bytes are returned. Records can only be inspected by the
        /// operator/trusted local component, never by public inventory APIs.
        /// </summary>
        public bool TryVerify(UUID verifiedOwner, UUID caseId, UUID itemId, UUID assetId)
        {
            if (verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                itemId == UUID.Zero || assetId == UUID.Zero)
                return false;

            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                verifiedOwner.ToString() + "|" + caseId.ToString())));
            string dataPath = Path.Combine(m_directory, key + ".payload");
            string metaPath = Path.Combine(m_directory, key + ".json");
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                JsonElement root = doc.RootElement;
                if (root.GetProperty("status").GetString() != "staged_uninspected" ||
                    root.GetProperty("owner_id").GetString() != verifiedOwner.ToString() ||
                    root.GetProperty("case_id").GetString() != caseId.ToString() ||
                    root.GetProperty("item_id").GetString() != itemId.ToString() ||
                    root.GetProperty("asset_id").GetString() != assetId.ToString())
                    return false;

                int len = root.GetProperty("byte_length").GetInt32();
                if (len <= 0 || len > m_maxAssetBytes)
                    return false;

                using FileStream file = new FileStream(dataPath, FileMode.Open,
                    FileAccess.Read, FileShare.Read);
                if (file.Length != len)
                    return false;
                byte[] hash = SHA256.HashData(file);
                return string.Equals(root.GetProperty("sha256").GetString(),
                    Convert.ToHexString(hash), StringComparison.Ordinal);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (JsonException) { return false; }
            catch (FormatException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (KeyNotFoundException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        private static FileStream CreatePrivate(string path)
        {
            return new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
                UnixCreateMode = PrivateFile
            });
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
