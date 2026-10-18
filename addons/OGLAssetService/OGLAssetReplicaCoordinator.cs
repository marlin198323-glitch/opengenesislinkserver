using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Trusted in-process replica promotion. Network authentication and transfer
    /// are deliberately out of scope; never expose this directly as an endpoint.
    /// </summary>
    public sealed class OGLAssetReplicaCoordinator
    {
        private readonly IOGLAssetLocationDirectory directory;

        public OGLAssetReplicaCoordinator(IOGLAssetLocationDirectory directory)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        /// <summary>
        /// A caller must independently authenticate the target node and obtain
        /// a durable receipt for the exact hash before calling this method.
        /// CAS prevents promotion of a replica for a superseded asset revision.
        /// </summary>
        public OGLAssetLocationDirectory.Entry ConfirmReplica(
            string assetId, string replicaNode, string acknowledgedHash,
            long expectedRevision)
        {
            if (String.IsNullOrWhiteSpace(replicaNode) ||
                replicaNode.Length > 128 || replicaNode != replicaNode.Trim())
                throw new ArgumentException("Invalid replica node identity.", nameof(replicaNode));
            OGLAssetLocationDirectory.Entry current = directory.Find(assetId);
            if (current == null || current.Revision != expectedRevision)
                throw new InvalidOperationException("Asset revision changed before replica confirmation.");
            if (!String.Equals(current.ContentHash, acknowledgedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Replica receipt hash does not match current asset.");
            if (String.Equals(current.PrimaryNode, replicaNode, StringComparison.Ordinal))
                throw new ArgumentException("Primary node cannot also be a replica.", nameof(replicaNode));
            if (current.Replicas.Contains(replicaNode))
                return current;
            if (current.Replicas.Count >= 32)
                throw new InvalidOperationException("Maximum replica count reached.");
            List<string> replicas = new List<string>(current.Replicas);
            replicas.Add(replicaNode);
            current.Replicas = replicas;
            return directory.Publish(current, expectedRevision);
        }
    }
}
