// OpenGenesisLINK InventoryGuard phase 3e: hash-pinned, bounded asset dependency graph verification.
// Input must be a trusted immutable snapshot of ISOLATED quarantine bytes, not HGAssetService.
// "Verified" here means structural and byte integrity only, NOT safe to execute/release.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Services.HypergridService
{
    public sealed class OglHgDependencyGraphReport
    {
        public int AssetsInspected { get; internal set; }
        public int ObjectAssetsInspected { get; internal set; }
        public int ScriptSourcesInspected { get; internal set; }
        public int NestedReferences { get; internal set; }
        public long TotalBytes { get; internal set; }
        public string Status => "structurally_inspected_not_authorized";
    }

    /// <summary>
    /// Reads only from a caller-supplied isolated, hash-pinned snapshot.
    /// No HTTP calls, no cache reads, no FSAssets mutations, no inventory writes.
    /// An external authenticator and all transactional/policy checks remain required.
    /// </summary>
    public static class OglHgQuarantineDependencyGraphVerifier
    {
        public const int DefaultMaxAssets = 1024;
        public const int DefaultMaxReferences = 4096;
        public const int DefaultMaxDepth = 12;
        public const long DefaultMaxTotalBytes = 64L * 1024 * 1024;

        private struct Pending
        {
            public UUID Id;
            public int Type;
            public int Depth;
            public bool Leave;

            public Pending(UUID id, int type, int depth, bool leave)
            {
                Id = id;
                Type = type;
                Depth = depth;
                Leave = leave;
            }
        }

        /// <summary>
        /// A returned report MUST NOT be interpreted as an executable/authorizable
        /// asset. Every reachable asset, including root, requires a separate
        /// authenticated, trusted SHA-256 pin. A missing dependency fails closed.
        ///
        /// The caller MUST hold isolatedAssets and expectedSha256Pins immutable
        /// throughout this call, then recheck them before any future release.
        /// </summary>
        public static bool TryInspect(
            UUID rootId, int rootType,
            IReadOnlyDictionary<UUID, AssetBase> isolatedAssets,
            IReadOnlyDictionary<UUID, string> expectedSha256Pins,
            int maxAssetBytes, int maxScriptBytes,
            int maxAssets, int maxReferences, int maxDepth, long maxTotalBytes,
            out OglHgDependencyGraphReport report, out string reason)
        {
            report = null;
            reason = "graph_invalid_input";
            if (rootId == UUID.Zero || rootType < sbyte.MinValue ||
                rootType > sbyte.MaxValue || isolatedAssets == null ||
                expectedSha256Pins == null ||
                maxAssetBytes < 1 || maxAssetBytes > 256 * 1024 * 1024 ||
                maxScriptBytes < 1 || maxScriptBytes > maxAssetBytes ||
                maxAssets < 1 || maxAssets > 100000 ||
                maxReferences < 1 || maxReferences > 100000 ||
                maxDepth < 0 || maxDepth > 64 ||
                maxTotalBytes < 1 || maxTotalBytes > 4L * 1024 * 1024 * 1024)
                return false;

            Dictionary<UUID, int> assignedTypes = new Dictionary<UUID, int>();
            HashSet<UUID> visiting = new HashSet<UUID>();
            HashSet<UUID> finished = new HashSet<UUID>();
            Stack<Pending> stack = new Stack<Pending>();
            var outcome = new OglHgDependencyGraphReport();
            assignedTypes[rootId] = rootType;
            stack.Push(new Pending(rootId, rootType, 0, false));

            while (stack.Count > 0)
            {
                Pending node = stack.Pop();
                if (node.Leave)
                {
                    visiting.Remove(node.Id);
                    finished.Add(node.Id);
                    continue;
                }

                // Enforce the longest encountered path as well, including
                // shared dependencies that were already checked on a short path.
                if (node.Depth > maxDepth)
                {
                    reason = "graph_depth_limit";
                    return false;
                }
                if (visiting.Contains(node.Id))
                {
                    reason = "graph_dependency_cycle";
                    return false;
                }
                if (finished.Contains(node.Id))
                    continue;
                if (outcome.AssetsInspected >= maxAssets)
                {
                    reason = "graph_asset_count_limit";
                    return false;
                }

                if (!isolatedAssets.TryGetValue(node.Id, out AssetBase asset) ||
                    asset == null || asset.Data == null || asset.Data.Length == 0)
                {
                    reason = "graph_dependency_missing";
                    return false;
                }
                if (asset.FullID != node.Id || asset.Type != node.Type)
                {
                    reason = "graph_asset_identity_or_type_mismatch";
                    return false;
                }
                if (!expectedSha256Pins.TryGetValue(node.Id, out string hash) ||
                    hash == null || hash.Length != 64)
                {
                    reason = "graph_missing_or_malformed_hash_pin";
                    return false;
                }

                byte[] pinned;
                try
                {
                    pinned = Convert.FromHexString(hash);
                }
                catch (FormatException)
                {
                    reason = "graph_missing_or_malformed_hash_pin";
                    return false;
                }
                if (asset.Data.Length > maxAssetBytes ||
                    asset.Data.Length > maxTotalBytes - outcome.TotalBytes)
                {
                    reason = "graph_total_or_single_asset_size_limit";
                    return false;
                }

                // This binds each inspected asset to a hash recorded by the
                // trusted staging coordinator, not an untrusted HG payload.
                byte[] actual = SHA256.HashData(asset.Data);
                if (!CryptographicOperations.FixedTimeEquals(actual, pinned))
                {
                    reason = "graph_asset_hash_mismatch";
                    return false;
                }

                if (!OglHgInventoryAssetInspector.TryInspect(
                    asset, maxAssetBytes, maxScriptBytes, out string scanReason))
                {
                    reason = "graph_asset_inspection_" + (scanReason ?? "failed");
                    return false;
                }

                outcome.TotalBytes += asset.Data.Length;
                outcome.AssetsInspected++;
                if (asset.Type == (sbyte)AssetType.LSLText)
                    outcome.ScriptSourcesInspected++;

                if (asset.Type != (sbyte)AssetType.Object)
                {
                    finished.Add(node.Id);
                    continue;
                }

                outcome.ObjectAssetsInspected++;
                if (!OglHgNestedInventoryInspector.TryExtract(asset, maxAssetBytes,
                    out OglHgNestedAssetReference[] references, out string parseReason))
                {
                    reason = "graph_nested_" + (parseReason ?? "failed");
                    return false;
                }
                if (references.Length > maxReferences - outcome.NestedReferences)
                {
                    reason = "graph_reference_count_limit";
                    return false;
                }
                outcome.NestedReferences += references.Length;

                visiting.Add(node.Id);
                stack.Push(new Pending(node.Id, node.Type, node.Depth, true));

                // Traverse in a stable order. Shared dependencies may be
                // inspected once; any back-edge to an active object fails.
                for (int i = references.Length - 1; i >= 0; --i)
                {
                    OglHgNestedAssetReference nested = references[i];
                    if (assignedTypes.TryGetValue(nested.AssetId, out int priorType))
                    {
                        if (priorType != nested.AssetType)
                        {
                            reason = "graph_conflicting_asset_types";
                            return false;
                        }
                    }
                    else
                        assignedTypes[nested.AssetId] = nested.AssetType;

                    if (visiting.Contains(nested.AssetId) ||
                        nested.AssetId == node.Id)
                    {
                        reason = "graph_dependency_cycle";
                        return false;
                    }
                    if (!finished.Contains(nested.AssetId))
                        stack.Push(new Pending(nested.AssetId, nested.AssetType,
                            node.Depth + 1, false));
                }
            }

            // No rights verification, no native-script semantic analysis,
            // no referenced texture/mesh/material decoding. Never auto-release.
            report = outcome;
            reason = null;
            return true;
        }
    }
}
