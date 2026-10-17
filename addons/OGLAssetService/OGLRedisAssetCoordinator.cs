using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Optional Redis coordination building block. Does not store asset payloads.
    /// Revision keys survive subscriber disconnects; pub/sub is only a wakeup hint.
    /// Redis must be private, authenticated, and persistent when revisions are authoritative.
    /// </summary>
    public sealed class OGLRedisAssetCoordinator : IDisposable
    {
        private readonly ConnectionMultiplexer connection;
        private readonly IDatabase database;
        private readonly string prefix;

        public OGLRedisAssetCoordinator(string connectionString, int databaseIndex = 2,
            string keyPrefix = "ogl:asset:v1:")
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Redis connection is required.", nameof(connectionString));
            if (String.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("Redis key prefix is required.", nameof(keyPrefix));
            prefix = keyPrefix;
            connection = ConnectionMultiplexer.Connect(connectionString);
            database = connection.GetDatabase(databaseIndex);
        }

        private string Key(string assetId)
        {
            if (String.IsNullOrWhiteSpace(assetId))
                throw new ArgumentException("Asset ID required.", nameof(assetId));
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assetId)));
            return prefix + hash.ToLowerInvariant();
        }

        public async Task<long> GetRevisionAsync(string assetId)
        {
            RedisValue value = await database.StringGetAsync(Key(assetId)).ConfigureAwait(false);
            return value.HasValue && long.TryParse(value.ToString(), out long revision) ? revision : 0;
        }

        public Task<long> InvalidateAsync(string assetId) =>
            database.StringIncrementAsync(Key(assetId));

        // Experimental payload primitives. Not used by OGLAssetConnector until a
        // coherent multi-writer invalidation protocol is implemented.
        // A content-addressed key avoids overwriting one payload with another.
        private string PayloadKey(byte[] payload)
        {
            return prefix + "payload:" + OGLObjectCache.ContentHash(payload);
        }

        public async Task<string> PutPayloadAsync(byte[] payload, TimeSpan ttl)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
            // Cap Redis allocations; large meshes remain in authoritative FSAssets.
            if (payload.Length > 8 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(payload), "Redis payload limit is 8 MiB.");
            string key = PayloadKey(payload);
            bool stored = await database.StringSetAsync(key, payload, ttl).ConfigureAwait(false);
            if (!stored) throw new InvalidOperationException("Redis did not store the payload.");
            return OGLObjectCache.ContentHash(payload);
        }

        public async Task<byte[]> GetPayloadAsync(string contentHash)
        {
            if (String.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64 ||
                !System.Text.RegularExpressions.Regex.IsMatch(contentHash, "^[a-f0-9]{64}$"))
                throw new ArgumentException("Expected lowercase SHA-256 hash.", nameof(contentHash));
            RedisValue stored = await database.StringGetAsync(prefix + "payload:" + contentHash)
                .ConfigureAwait(false);
            if (!stored.HasValue) return null;
            byte[] bytes = (byte[])stored;
            // Do not return corrupt data even when Redis or its backing store is damaged.
            return bytes.Length <= 8 * 1024 * 1024 &&
                String.Equals(OGLObjectCache.ContentHash(bytes), contentHash, StringComparison.Ordinal)
                ? bytes : null;
        }

        public void Dispose() => connection.Dispose();
    }
}
