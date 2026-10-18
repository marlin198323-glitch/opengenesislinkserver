using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Single-process local-node staging adapter. Never advertises remote
    /// replicas or replaces the authoritative FSAssetConnector.
    /// </summary>
    public sealed class OGLAssetNodeCoordinator
    {
        private readonly object gate = new object();
        private readonly string nodeId;
        private readonly OGLNodeObjectStore objects;
        private readonly IOGLAssetLocationDirectory locations;

        public OGLAssetNodeCoordinator(string nodeId, OGLNodeObjectStore objects,
            IOGLAssetLocationDirectory locations)
        {
            if (String.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 ||
                nodeId != nodeId.Trim())
                throw new ArgumentException("Node identity required.", nameof(nodeId));
            this.nodeId = nodeId;
            this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
            this.locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        /// <summary>
        /// Writes the immutable payload first and publishes the UUID mapping
        /// afterwards. Orphaned objects are intentionally retained on failure.
        /// </summary>
        public OGLAssetLocationDirectory.Entry Stage(string assetId, byte[] payload,
            long expectedRevision)
        {
            if (!Guid.TryParseExact(assetId, "D", out Guid id) || id == Guid.Empty)
                throw new ArgumentException("Canonical non-empty asset UUID required.", nameof(assetId));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            lock (gate)
            {
                string hash = objects.Put(payload);
                return locations.Publish(new OGLAssetLocationDirectory.Entry
                {
                    AssetId = id.ToString("D"),
                    ContentHash = hash,
                    PrimaryNode = nodeId,
                    Replicas = new List<string>()
                }, expectedRevision);
            }
        }

        /// <summary>
        /// Returns null if this node is not the primary or if the mapping is
        /// absent. Hash verification occurs in the object store on every read.
        /// </summary>
        public byte[] ReadLocal(string assetId)
        {
            lock (gate)
            {
                OGLAssetLocationDirectory.Entry location = locations.Find(assetId);
                if (location == null || !String.Equals(location.PrimaryNode, nodeId,
                    StringComparison.Ordinal)) return null;
                byte[] payload = objects.Get(location.ContentHash);
                if (payload == null)
                    throw new FileNotFoundException("Directory references a missing local payload.");
                return payload;
            }
        }
    }
}
