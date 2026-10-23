// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Security.Cryptography;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Local OGL content-addressed store alongside the authoritative FSAssets.
    /// The SQLite mapping is advisory: callers must compare ContentHash with the
    /// current FSAssets SQL hash before serving an OGL payload. No SQL migration,
    /// remote replication or second FSAssets writer is involved.
    /// </summary>
    public sealed class OGLParallelAssetStore
    {
        private readonly OGLNodeObjectStore objects;
        private readonly OGLSqliteAssetLocationDirectory locations;

        public OGLParallelAssetStore(string objectDirectory, string indexPath)
        {
            if (String.IsNullOrWhiteSpace(objectDirectory) || String.IsNullOrWhiteSpace(indexPath))
                throw new ArgumentException("Parallel object directory and SQLite index path are required.");
            string objectRoot = Path.GetFullPath(objectDirectory);
            string index = Path.GetFullPath(indexPath);
            if (IsInside(index, objectRoot))
                throw new InvalidOperationException("Parallel index must be outside the immutable object directory.");
            objects = new OGLNodeObjectStore(objectRoot);
            locations = new OGLSqliteAssetLocationDirectory(index);
        }

        /// <summary>
        /// Returns null for an unmigrated or stale mapping. Never interpret this
        /// as evidence of an absent authoritative asset.
        /// </summary>
        public byte[] TryRead(string assetId, string authoritativeHash)
        {
            if (String.IsNullOrWhiteSpace(authoritativeHash))
                return null;
            var entry = locations.Find(assetId);
            if (entry == null ||
                !String.Equals(entry.ContentHash, authoritativeHash, StringComparison.OrdinalIgnoreCase))
                return null;
            return objects.Get(entry.ContentHash);
        }

        /// <summary>
        /// Copy an already committed authoritative payload into immutable OGL
        /// storage, then publish the UUID mapping. The bytes MUST match the
        /// current FSAssets SQL digest; otherwise neither the index nor payload
        /// is altered. The caller rechecks SQL before and after this operation.
        /// </summary>
        public bool Mirror(string assetId, string authoritativeHash, byte[] payload)
        {
            if (!Guid.TryParseExact(assetId, "D", out Guid id) || id == Guid.Empty ||
                String.IsNullOrWhiteSpace(authoritativeHash) || payload == null)
                return false;
            string digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            if (!String.Equals(digest, authoritativeHash, StringComparison.OrdinalIgnoreCase))
                return false;

            string stored = objects.Put(payload);
            // SQLite CAS protects two writers racing on one UUID. Other ROBUST
            // processes can still update FSAssets; reads always recheck SQL.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var existing = locations.Find(assetId);
                if (existing != null &&
                    String.Equals(existing.ContentHash, stored, StringComparison.Ordinal))
                    return true;
                var candidate = new OGLAssetLocationDirectory.Entry
                {
                    AssetId = id.ToString("D"),
                    ContentHash = stored,
                    PrimaryNode = "local"
                };
                try
                {
                    locations.Publish(candidate, existing?.Revision ?? 0);
                    return true;
                }
                catch (InvalidOperationException) when (attempt < 2)
                {
                    // Competing publisher; inspect the new mapping and retry.
                }
            }
            return false;
        }

        public static bool IsInside(string candidate, string directory)
        {
            string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            return String.Equals(path, root, StringComparison.Ordinal) ||
                path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }
    }
}
