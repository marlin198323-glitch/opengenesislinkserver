// OGL InventoryGuard phase 3g: bind non-authorizing inspection to a specific
// isolated staged snapshot. A matching fingerprint NEVER grants release rights.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.InventoryService;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// A local diagnostic attestation of precisely which staged bytes and
    /// resource limits were structurally inspected. Not signed, not issued
    /// over HG or HTTP, and not an inventory/asset release credential.
    /// </summary>
    public sealed class OglHgQuarantineInspectionBinding
    {
        public UUID Owner { get; }
        public UUID CaseId { get; }
        public UUID RootItemId { get; }
        public UUID RootAssetId { get; }
        public string SnapshotSha256 { get; }
        public int MaxAssetBytes { get; }
        public int MaxScriptBytes { get; }
        public int MaxAssets { get; }
        public int MaxReferences { get; }
        public int MaxDepth { get; }
        public long MaxTotalBytes { get; }
        public string Status => "structurally_inspected_not_authorized";

        internal OglHgQuarantineInspectionBinding(
            UUID owner, UUID caseId, UUID rootItemId, UUID rootAssetId,
            string snapshotSha256, int maxAssetBytes, int maxScriptBytes,
            int maxAssets, int maxReferences, int maxDepth, long maxTotalBytes)
        {
            Owner = owner;
            CaseId = caseId;
            RootItemId = rootItemId;
            RootAssetId = rootAssetId;
            SnapshotSha256 = snapshotSha256;
            MaxAssetBytes = maxAssetBytes;
            MaxScriptBytes = maxScriptBytes;
            MaxAssets = maxAssets;
            MaxReferences = maxReferences;
            MaxDepth = maxDepth;
            MaxTotalBytes = maxTotalBytes;
        }
    }

    public static class OglHgStagedQuarantineBundleInspector
    {
        // Backward-compatible diagnostic check without a stored fingerprint.
        public static bool TryInspect(OglQuarantineTransferStageStore store,
            UUID verifiedOwner, UUID caseId, UUID rootItemId,
            int maxAssetBytes, int maxScriptBytes, int maxAssets,
            int maxReferences, int maxDepth, long maxTotalBytes,
            out OglHgDependencyGraphReport report, out string reason)
        {
            return TryInspectBound(store, verifiedOwner, caseId, rootItemId,
                maxAssetBytes, maxScriptBytes, maxAssets, maxReferences, maxDepth,
                maxTotalBytes, out _, out report, out reason);
        }

        /// <summary>
        /// Read, inspect and fingerprint all staged assets. After scanning,
        /// read the stage once more to detect modifications during inspection.
        /// Valid fingerprints still MUST be rechecked before any future use.
        /// This method never inserts inventory or FSAssets, or grants approval.
        /// </summary>
        public static bool TryInspectBound(OglQuarantineTransferStageStore store,
            UUID verifiedOwner, UUID caseId, UUID rootItemId,
            int maxAssetBytes, int maxScriptBytes, int maxAssets,
            int maxReferences, int maxDepth, long maxTotalBytes,
            out OglHgQuarantineInspectionBinding binding,
            out OglHgDependencyGraphReport report, out string reason)
        {
            binding = null;
            report = null;
            reason = "bundle_invalid_input";
            if (store == null || verifiedOwner == UUID.Zero ||
                caseId == UUID.Zero || rootItemId == UUID.Zero ||
                maxAssetBytes < 1 || maxScriptBytes < 1 ||
                maxAssets < 1 || maxReferences < 1 || maxDepth < 0 ||
                maxTotalBytes < 1)
                return false;

            if (!TryRead(store, verifiedOwner, caseId, rootItemId,
                maxAssets, maxTotalBytes, out OglQuarantineStagedAsset[] staged,
                out reason))
                return false;

            Dictionary<UUID, AssetBase> assets = new();
            Dictionary<UUID, string> pins = new();
            foreach (OglQuarantineStagedAsset blob in staged)
            {
                if (blob == null || blob.AssetId == UUID.Zero ||
                    blob.AssetType < sbyte.MinValue || blob.AssetType > sbyte.MaxValue ||
                    !pins.TryAdd(blob.AssetId, blob.Sha256))
                {
                    reason = "bundle_invalid_or_duplicate_asset";
                    return false;
                }

                AssetBase asset = new AssetBase(blob.AssetId, "isolated inspection",
                    (sbyte)blob.AssetType, verifiedOwner.ToString());
                asset.Data = (byte[])blob.Data.Clone();
                assets.Add(blob.AssetId, asset);
            }

            UUID rootAssetId = staged[0].AssetId;
            if (!OglHgQuarantineDependencyGraphVerifier.TryInspect(
                rootAssetId, staged[0].AssetType, assets, pins,
                maxAssetBytes, maxScriptBytes, maxAssets, maxReferences,
                maxDepth, maxTotalBytes, out report, out reason))
                return false;

            // A surprising surplus of staged assets fails closed. Nothing may
            // be smuggled as an unexamined unused companion payload.
            if (report.AssetsInspected != staged.Length)
            {
                report = null;
                reason = "bundle_unreferenced_staged_asset";
                return false;
            }

            string snapshotHash = ComputeFingerprint(staged, verifiedOwner, caseId,
                rootItemId, rootAssetId, maxAssetBytes, maxScriptBytes,
                maxAssets, maxReferences, maxDepth, maxTotalBytes);
            if (!TryRead(store, verifiedOwner, caseId, rootItemId,
                maxAssets, maxTotalBytes, out OglQuarantineStagedAsset[] current,
                out reason) ||
                current[0].AssetId != rootAssetId ||
                !EqualFingerprint(snapshotHash, ComputeFingerprint(current,
                    verifiedOwner, caseId, rootItemId, rootAssetId,
                    maxAssetBytes, maxScriptBytes, maxAssets, maxReferences,
                    maxDepth, maxTotalBytes)))
            {
                report = null;
                reason = "bundle_changed_during_inspection";
                return false;
            }

            binding = new OglHgQuarantineInspectionBinding(verifiedOwner, caseId,
                rootItemId, rootAssetId, snapshotHash, maxAssetBytes,
                maxScriptBytes, maxAssets, maxReferences, maxDepth, maxTotalBytes);
            reason = null;
            return true;
        }

        /// <summary>
        /// Detect staleness against the actual disk records (including added
        /// dependencies) after restart. A true result is NOT a release permit.
        /// The caller must separately authenticate the identity / permissions
        /// and run all security policy and final atomic commit checks.
        /// </summary>
        public static bool TryIsBindingCurrent(
            OglQuarantineTransferStageStore store,
            OglHgQuarantineInspectionBinding binding, out string reason)
        {
            reason = "bundle_binding_invalid";
            if (store == null || binding == null ||
                binding.Owner == UUID.Zero || binding.CaseId == UUID.Zero ||
                binding.RootItemId == UUID.Zero || binding.RootAssetId == UUID.Zero ||
                string.IsNullOrEmpty(binding.SnapshotSha256) ||
                binding.SnapshotSha256.Length != 64)
                return false;

            if (!TryRead(store, binding.Owner, binding.CaseId, binding.RootItemId,
                binding.MaxAssets, binding.MaxTotalBytes,
                out OglQuarantineStagedAsset[] current, out reason))
                return false;
            if (current[0].AssetId != binding.RootAssetId ||
                !EqualFingerprint(binding.SnapshotSha256,
                    ComputeFingerprint(current, binding.Owner, binding.CaseId,
                        binding.RootItemId, binding.RootAssetId,
                        binding.MaxAssetBytes, binding.MaxScriptBytes,
                        binding.MaxAssets, binding.MaxReferences,
                        binding.MaxDepth, binding.MaxTotalBytes)))
            {
                reason = "bundle_binding_stale";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryRead(OglQuarantineTransferStageStore store,
            UUID owner, UUID caseId, UUID rootItemId,
            int maxAssets, long maxTotalBytes,
            out OglQuarantineStagedAsset[] blobs, out string reason)
        {
            blobs = Array.Empty<OglQuarantineStagedAsset>();
            if (!store.TryReadVerifiedBundle(owner, caseId, rootItemId,
                maxAssets, maxTotalBytes, out blobs, out string stageReason))
            {
                reason = "bundle_snapshot_" + (stageReason ?? "invalid");
                return false;
            }
            if (blobs.Length == 0)
            {
                reason = "bundle_empty";
                return false;
            }
            reason = null;
            return true;
        }

        private static string ComputeFingerprint(
            OglQuarantineStagedAsset[] blobs, UUID owner, UUID caseId,
            UUID rootItemId, UUID rootAssetId, int maxAssetBytes,
            int maxScriptBytes, int maxAssets, int maxReferences, int maxDepth,
            long maxTotalBytes)
        {
            // Canonicalized with length-prefixed binary fields so strings
            // cannot be ambiguously concatenated. The sorted asset set is
            // independent of directory enumeration or insertion order.
            OglQuarantineStagedAsset[] ordered =
                (OglQuarantineStagedAsset[])blobs.Clone();
            Array.Sort(ordered, (a, b) =>
                string.CompareOrdinal(a.AssetId.ToString(), b.AssetId.ToString()));

            using MemoryStream data = new();
            using (BinaryWriter writer = new(data, Encoding.UTF8, true))
            {
                writer.Write("OGL_QUARANTINE_STRUCTURAL_BINDING_V1");
                writer.Write(owner.ToString());
                writer.Write(caseId.ToString());
                writer.Write(rootItemId.ToString());
                writer.Write(rootAssetId.ToString());
                writer.Write(maxAssetBytes);
                writer.Write(maxScriptBytes);
                writer.Write(maxAssets);
                writer.Write(maxReferences);
                writer.Write(maxDepth);
                writer.Write(maxTotalBytes);
                writer.Write(ordered.Length);
                foreach (OglQuarantineStagedAsset blob in ordered)
                {
                    writer.Write(blob.AssetId.ToString());
                    writer.Write(blob.AssetType);
                    writer.Write(blob.Data.Length);
                    writer.Write(Convert.ToHexString(SHA256.HashData(blob.Data)));
                }
            }
            return Convert.ToHexString(SHA256.HashData(data.ToArray()));
        }

        private static bool EqualFingerprint(string left, string right)
        {
            try
            {
                byte[] expected = Convert.FromHexString(left);
                byte[] actual = Convert.FromHexString(right);
                return expected.Length == 32 &&
                    CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch (FormatException) { return false; }
        }
    }
}
