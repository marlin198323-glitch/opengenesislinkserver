// OpenGenesisLINK InventoryGuard phase 3f: connect staged multi-asset bundles to
// the bounded isolated dependency graph verifier, without ANY release path.
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.InventoryService;

namespace OpenSim.Services.HypergridService
{
    public static class OglHgStagedQuarantineBundleInspector
    {
        /// <summary>
        /// Read every staged blob from a verified local case snapshot, then
        /// check all reachable task-inventory dependencies and require there
        /// are NO unreferenced staged blobs. Never reads the live asset service.
        ///
        /// This is diagnostic structural inspection only; no HG auth, rights,
        /// external texture/mesh graph or asset/inventory commit is performed.
        /// </summary>
        public static bool TryInspect(OglQuarantineTransferStageStore store,
            UUID verifiedOwner, UUID caseId, UUID rootItemId,
            int maxAssetBytes, int maxScriptBytes, int maxAssets,
            int maxReferences, int maxDepth, long maxTotalBytes,
            out OglHgDependencyGraphReport report, out string reason)
        {
            report = null;
            reason = "bundle_invalid_input";
            if (store == null || verifiedOwner == UUID.Zero ||
                caseId == UUID.Zero || rootItemId == UUID.Zero ||
                maxAssets < 1 || maxTotalBytes < 1)
                return false;

            if (!store.TryReadVerifiedBundle(verifiedOwner, caseId, rootItemId,
                maxAssets, maxTotalBytes,
                out OglQuarantineStagedAsset[] staged, out string readReason))
            {
                reason = "bundle_snapshot_" + (readReason ?? "invalid");
                return false;
            }

            if (staged.Length == 0)
            {
                reason = "bundle_empty";
                return false;
            }

            // Fresh AssetBase objects reference only the verified isolated
            // copies supplied by the stage store, never a live asset cache.
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
                asset.Data = blob.Data;
                assets.Add(blob.AssetId, asset);
            }

            OglQuarantineStagedAsset root = staged[0];
            if (!OglHgQuarantineDependencyGraphVerifier.TryInspect(
                root.AssetId, root.AssetType, assets, pins,
                maxAssetBytes, maxScriptBytes, maxAssets, maxReferences,
                maxDepth, maxTotalBytes, out report, out reason))
                return false;

            // An extra unexpected staged blob is not proof of an allowed
            // dependency; reject the entire case rather than ignoring it.
            if (report.AssetsInspected != staged.Length)
            {
                report = null;
                reason = "bundle_unreferenced_staged_asset";
                return false;
            }

            // NEVER return an "authorized", "approved" or "released" state.
            return true;
        }
    }
}
