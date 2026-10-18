using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// HMAC authentication of a replica storage acknowledgement. Each node
    /// needs a separate high-entropy secret provisioned outside source control.
    /// Does not prove fsync, node liveness or possession of the payload.
    /// </summary>
    public sealed class OGLReplicaReceiptAuthenticator
    {
        private readonly byte[] key;
        private readonly string nodeId;
        private readonly TimeSpan maxAge;

        public OGLReplicaReceiptAuthenticator(string nodeId, byte[] secret, TimeSpan maxAge)
        {
            if (String.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 ||
                nodeId != nodeId.Trim() || nodeId.Contains("\n") || nodeId.Contains("\r"))
                throw new ArgumentException("Invalid node identity.", nameof(nodeId));
            if (secret == null || secret.Length < 32)
                throw new ArgumentException("At least 32 bytes of secret key required.", nameof(secret));
            if (maxAge <= TimeSpan.Zero || maxAge > TimeSpan.FromMinutes(15))
                throw new ArgumentOutOfRangeException(nameof(maxAge));
            this.nodeId = nodeId;
            key = (byte[])secret.Clone();
            this.maxAge = maxAge;
        }

        public string Sign(string assetId, string contentHash, long revision,
            long unixSeconds, string nonce)
        {
            string message = Message(assetId, contentHash, revision, unixSeconds, nonce);
            using (HMACSHA256 hmac = new HMACSHA256(key))
                return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        }

        public bool Verify(string assetId, string contentHash, long revision,
            long unixSeconds, string nonce, string signature, DateTimeOffset now)
        {
            if (String.IsNullOrEmpty(signature) || signature.Length != 64) return false;
            byte[] supplied;
            try { supplied = Convert.FromHexString(signature); }
            catch (FormatException) { return false; }
            long age = now.ToUnixTimeSeconds() - unixSeconds;
            if (age < 0 || age > (long)maxAge.TotalSeconds) return false;
            string expected = Sign(assetId, contentHash, revision, unixSeconds, nonce);
            byte[] computed = Convert.FromHexString(expected);
            return CryptographicOperations.FixedTimeEquals(computed, supplied);
        }

        private string Message(string assetId, string hash, long revision,
            long timestamp, string nonce)
        {
            if (!Guid.TryParseExact(assetId, "D", out Guid uuid) || uuid == Guid.Empty)
                throw new ArgumentException("Invalid asset UUID.", nameof(assetId));
            if (hash == null || hash.Length != 64)
                throw new ArgumentException("Invalid content hash.", nameof(hash));
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    throw new ArgumentException("Invalid content hash.", nameof(hash));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Guid.TryParseExact(nonce, "N", out Guid parsedNonce) || parsedNonce == Guid.Empty)
                throw new ArgumentException("Unique nonce required.", nameof(nonce));
            return "ogl-replica-v1\n" + nodeId + "\n" + uuid.ToString("D") + "\n" +
                hash + "\n" + revision.ToString(CultureInfo.InvariantCulture) + "\n" +
                timestamp.ToString(CultureInfo.InvariantCulture) + "\n" + parsedNonce.ToString("N");
        }
    }
}
