using System;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Validation rules for future authenticated replica transfer endpoints.
    /// This policy does not authenticate a remote node or perform HTTP requests.
    /// </summary>
    public sealed class OGLReplicaTransferPolicy
    {
        public long MaximumPayloadBytes { get; }

        public OGLReplicaTransferPolicy(long maximumPayloadBytes)
        {
            if (maximumPayloadBytes < 1 || maximumPayloadBytes > 1073741824L)
                throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));
            MaximumPayloadBytes = maximumPayloadBytes;
        }

        public void ValidateEndpoint(Uri endpoint)
        {
            if (endpoint == null || !endpoint.IsAbsoluteUri ||
                endpoint.Scheme != Uri.UriSchemeHttps ||
                !String.IsNullOrEmpty(endpoint.UserInfo) ||
                !String.IsNullOrEmpty(endpoint.Fragment))
                throw new ArgumentException("Absolute HTTPS endpoint without credentials or fragment required.", nameof(endpoint));
        }

        public void ValidatePayloadSize(long bytes)
        {
            if (bytes < 0 || bytes > MaximumPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(bytes));
        }
    }
}
