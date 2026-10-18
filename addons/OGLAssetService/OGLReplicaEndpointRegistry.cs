using System;
using System.Collections.Generic;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Operator-configured HTTPS endpoints for known storage nodes.
    /// Does not perform enrollment, TLS verification or network requests.
    /// </summary>
    public sealed class OGLReplicaEndpointRegistry
    {
        private readonly Dictionary<string, Uri> endpoints =
            new Dictionary<string, Uri>(StringComparer.Ordinal);
        private readonly OGLReplicaTransferPolicy policy;

        public OGLReplicaEndpointRegistry(OGLReplicaTransferPolicy policy)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void Register(string nodeId, Uri endpoint)
        {
            if (String.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 ||
                nodeId != nodeId.Trim())
                throw new ArgumentException("Invalid node identity.", nameof(nodeId));
            policy.ValidateEndpoint(endpoint);
            if (endpoint.Query.Length != 0)
                throw new ArgumentException("Node endpoint must not contain a query.", nameof(endpoint));
            if (endpoints.ContainsKey(nodeId))
                throw new InvalidOperationException("Duplicate node identity.");
            endpoints.Add(nodeId, endpoint);
        }

        public Uri Resolve(string nodeId)
        {
            if (nodeId == null) throw new ArgumentNullException(nameof(nodeId));
            if (!endpoints.TryGetValue(nodeId, out Uri endpoint))
                throw new KeyNotFoundException("Unknown replica node.");
            return endpoint;
        }
    }
}
