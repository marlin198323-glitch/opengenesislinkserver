// OpenGenesisLINK InventoryGuard - phase 3c: isolated untrusted byte staging.
// NOT callable through the inventory wire API; NOT the main asset service.
// Payloads remain inaccessible to normal inventory readers and script engines.
using System;
using System.Collections.Generic;
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
    // Read-only snapshot data passed directly to the local graph inspector.
    // Its hash is a consistency pin created during trusted local staging;
    // it is NOT an ownership, provenance or authorisation certificate.
    public sealed class OglQuarantineStagedAsset
    {
        public UUID AssetId { get; }
        public int AssetType { get; }
        public string Sha256 { get; }
        public byte[] Data { get; }

        public OglQuarantineStagedAsset(UUID assetId, int assetType,
            string sha256, byte[] data)
        {
            AssetId = assetId;
            AssetType = assetType;
            Sha256 = sha256;
            Data = (byte[])data.Clone();
        }
    }

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

        /// <summary>
        /// Stage a dependent asset ONLY after an authenticated local
        /// StageDependency proof has been consumed by the coordinator.
        /// The parent case MUST already be staged. Never populates FSAssets
        /// or normal inventory. No remote endpoint exposes this method.
        /// </summary>
        public bool TryStageDependency(UUID verifiedOwner, UUID caseId,
            UUID rootItemId, UUID assetId, int assetType, byte[] payload,
            out string reason)
        {
            reason = "dependency_stage_unavailable";
            if (verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                rootItemId == UUID.Zero || assetId == UUID.Zero ||
                payload == null || payload.Length == 0)
            {
                reason = "dependency_invalid_identity_or_payload";
                return false;
            }
            if (payload.Length > m_maxAssetBytes)
            {
                reason = "dependency_asset_size_limit";
                return false;
            }
            if (assetType < sbyte.MinValue || assetType > sbyte.MaxValue)
            {
                reason = "dependency_invalid_asset_type";
                return false;
            }

            string key = StageKey(verifiedOwner, caseId);
            string rootMeta = Path.Combine(m_directory, key + ".json");
            string rootPayload = Path.Combine(m_directory, key + ".payload");
            string suffix = key + ".dep." + assetId;
            string dependencyMeta = Path.Combine(m_directory, suffix + ".json");
            string dependencyPayload = Path.Combine(m_directory, suffix + ".payload");

            bool createdPayload = false;
            bool createdManifest = false;
            try
            {
                using FileStream exclusive = OpenStageLock();
                if (!TryReadVerifiedBlob(rootMeta, rootPayload,
                    verifiedOwner, caseId, rootItemId, null, out OglQuarantineStagedAsset root))
                {
                    reason = "dependency_parent_missing_or_corrupt";
                    return false;
                }
                if (root.AssetId == assetId ||
                    File.Exists(dependencyMeta) || File.Exists(dependencyPayload))
                {
                    reason = "dependency_duplicate_asset";
                    return false;
                }
                if (!FitsGlobalQuota(payload.Length))
                {
                    reason = "dependency_stage_quota";
                    return false;
                }

                try
                {
                    using (FileStream output = CreatePrivate(dependencyPayload))
                    {
                        createdPayload = true;
                        output.Write(payload, 0, payload.Length);
                        output.Flush(true);
                    }

                    // The immutable manifest is the final committed record.
                    byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        status = "staged_uninspected_dependency",
                        created_utc = DateTimeOffset.UtcNow.ToString("O"),
                        owner_id = verifiedOwner.ToString(),
                        case_id = caseId.ToString(),
                        item_id = rootItemId.ToString(),
                        asset_id = assetId.ToString(),
                        asset_type = assetType,
                        byte_length = payload.Length,
                        sha256 = Convert.ToHexString(SHA256.HashData(payload))
                    });
                    using (FileStream output = CreatePrivate(dependencyMeta))
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
                    if (createdManifest)
                        TryDelete(dependencyMeta);
                    if (createdPayload)
                        TryDelete(dependencyPayload);
                    throw;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        /// <summary>
        /// Read one consistent local staged-byte snapshot under the same lock
        /// as stage writes. Fail closed on orphan payloads/manifests, hash
        /// corruption, wrong owner/item/asset, record or total byte quotas.
        /// This returns NO authorization to copy, run or release the data.
        /// </summary>
        public bool TryReadVerifiedBundle(UUID verifiedOwner, UUID caseId,
            UUID rootItemId, int maxAssets, long maxTotalBytes,
            out OglQuarantineStagedAsset[] snapshot, out string reason)
        {
            snapshot = Array.Empty<OglQuarantineStagedAsset>();
            reason = "bundle_unavailable";
            if (verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                rootItemId == UUID.Zero || maxAssets < 1 ||
                maxAssets > m_maxRecords ||
                maxTotalBytes < 1 || maxTotalBytes > m_maxTotalBytes)
                return false;

            string key = StageKey(verifiedOwner, caseId);
            string rootMeta = Path.Combine(m_directory, key + ".json");
            string rootPayload = Path.Combine(m_directory, key + ".payload");
            try
            {
                using FileStream exclusive = OpenStageLock();
                if (!TryReadVerifiedBlob(rootMeta, rootPayload, verifiedOwner,
                    caseId, rootItemId, null, out OglQuarantineStagedAsset root))
                {
                    reason = "bundle_root_missing_or_corrupt";
                    return false;
                }
                List<OglQuarantineStagedAsset> assets = new() { root };
                HashSet<UUID> ids = new() { root.AssetId };
                long totalBytes = root.Data.Length;
                if (totalBytes > maxTotalBytes)
                {
                    reason = "bundle_total_bytes_limit";
                    return false;
                }

                string prefix = key + ".dep.";
                string[] manifests = Directory.GetFiles(m_directory,
                    prefix + "*.json", SearchOption.TopDirectoryOnly);
                string[] payloads = Directory.GetFiles(m_directory,
                    prefix + "*.payload", SearchOption.TopDirectoryOnly);

                // A crash between writes leaves an orphan payload. Never
                // silently ignore it or treat the case as fully inspected.
                if (manifests.Length != payloads.Length ||
                    manifests.Length > maxAssets - 1)
                {
                    reason = "bundle_incomplete_or_too_many_dependencies";
                    return false;
                }

                Array.Sort(manifests, StringComparer.Ordinal);
                foreach (string manifestPath in manifests)
                {
                    string fileName = Path.GetFileName(manifestPath);
                    string middle = fileName.Substring(prefix.Length,
                        fileName.Length - prefix.Length - ".json".Length);
                    if (!UUID.TryParse(middle, out UUID assetId) ||
                        assetId == UUID.Zero || !ids.Add(assetId))
                    {
                        reason = "bundle_invalid_or_duplicate_dependency";
                        return false;
                    }
                    string payloadPath = Path.Combine(m_directory,
                        prefix + middle + ".payload");
                    if (!TryReadVerifiedBlob(manifestPath, payloadPath,
                        verifiedOwner, caseId, rootItemId, assetId,
                        out OglQuarantineStagedAsset dependency))
                    {
                        reason = "bundle_dependency_corrupt";
                        return false;
                    }
                    if (dependency.Data.Length > maxTotalBytes - totalBytes)
                    {
                        reason = "bundle_total_bytes_limit";
                        return false;
                    }
                    totalBytes += dependency.Data.Length;
                    assets.Add(dependency);
                }
                snapshot = assets.ToArray();
                reason = null;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        private static string StageKey(UUID owner, UUID caseId)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                owner.ToString() + "|" + caseId.ToString())));
        }

        private FileStream OpenStageLock()
        {
            return new FileStream(Path.Combine(m_directory, ".stage.lock"),
                new FileStreamOptions
                {
                    Mode = FileMode.OpenOrCreate,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    UnixCreateMode = PrivateFile
                });
        }

        private bool FitsGlobalQuota(int extraBytes)
        {
            int count = 0;
            long usedBytes = 0;
            foreach (string path in Directory.EnumerateFiles(m_directory,
                "*.payload", SearchOption.TopDirectoryOnly))
            {
                if (++count >= m_maxRecords)
                    return false;
                long bytes = new FileInfo(path).Length;
                if (bytes > m_maxTotalBytes - usedBytes)
                    return false;
                usedBytes += bytes;
            }
            return extraBytes <= m_maxTotalBytes - usedBytes;
        }

        private bool TryReadVerifiedBlob(string metaPath, string dataPath,
            UUID owner, UUID caseId, UUID rootItemId, UUID? expectedAssetId,
            out OglQuarantineStagedAsset verified)
        {
            verified = null;
            try
            {
                FileInfo metaInfo = new FileInfo(metaPath);
                if (!metaInfo.Exists || metaInfo.Length < 2 ||
                    metaInfo.Length > 4096 || metaInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;

                using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(metaPath));
                JsonElement entry = document.RootElement;
                string status = entry.GetProperty("status").GetString();
                if (status != "staged_uninspected" &&
                    status != "staged_uninspected_dependency")
                    return false;
                if (entry.GetProperty("owner_id").GetString() != owner.ToString() ||
                    entry.GetProperty("case_id").GetString() != caseId.ToString() ||
                    entry.GetProperty("item_id").GetString() != rootItemId.ToString() ||
                    !UUID.TryParse(entry.GetProperty("asset_id").GetString(), out UUID actualId) ||
                    actualId == UUID.Zero ||
                    (expectedAssetId.HasValue && expectedAssetId.Value != actualId))
                    return false;
                if (expectedAssetId.HasValue && status != "staged_uninspected_dependency")
                    return false;
                if (!expectedAssetId.HasValue && status != "staged_uninspected")
                    return false;

                int type = entry.GetProperty("asset_type").GetInt32();
                int size = entry.GetProperty("byte_length").GetInt32();
                string hash = entry.GetProperty("sha256").GetString();
                if (type < sbyte.MinValue || type > sbyte.MaxValue ||
                    size < 1 || size > m_maxAssetBytes ||
                    hash == null || hash.Length != 64)
                    return false;

                byte[] expectedHash = Convert.FromHexString(hash);
                FileInfo payloadInfo = new FileInfo(dataPath);
                if (!payloadInfo.Exists || payloadInfo.Length != size ||
                    payloadInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;
                using FileStream stream = new FileStream(dataPath,
                    FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] bytes = new byte[size];
                stream.ReadExactly(bytes);
                if (!CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(bytes), expectedHash))
                    return false;
                verified = new OglQuarantineStagedAsset(actualId, type, hash, bytes);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (JsonException) { return false; }
            catch (FormatException) { return false; }
            catch (KeyNotFoundException) { return false; }
            catch (InvalidOperationException) { return false; }
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
