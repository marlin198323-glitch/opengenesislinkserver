using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Experimental Redis coordinator with content-addressed payload cache primitives.
    /// Revision keys survive subscriber disconnects; pub/sub is only a wakeup hint.
    /// Redis must be private, authenticated, and persistent when revisions are authoritative.
    /// </summary>
    public sealed class OGLRedisAssetCoordinator : IDisposable
    {
        private readonly ConnectionMultiplexer connection;
        private readonly IDatabase database;
        private readonly string prefix;
        private long payloadHits;
        private long payloadMisses;
        private long payloadIntegrityFailures;
        public long PayloadHits => System.Threading.Interlocked.Read(ref payloadHits);
        public long PayloadMisses => System.Threading.Interlocked.Read(ref payloadMisses);
        public long PayloadIntegrityFailures => System.Threading.Interlocked.Read(ref payloadIntegrityFailures);

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
        public async Task<string> PutPayloadAsync(byte[] payload, TimeSpan ttl)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
            // Cap Redis allocations; large meshes remain in authoritative FSAssets.
            if (payload.Length > 8 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(payload), "Redis payload limit is 8 MiB.");
            string hash = OGLObjectCache.ContentHash(payload);
            string key = prefix + "payload:" + hash;
            bool stored = await database.StringSetAsync(key, payload, ttl).ConfigureAwait(false);
            if (!stored) throw new InvalidOperationException("Redis did not store the payload.");
            return hash;
        }

        public async Task<byte[]> GetPayloadAsync(string contentHash)
        {
            if (String.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64 ||
                !System.Text.RegularExpressions.Regex.IsMatch(contentHash, "^[a-f0-9]{64}$"))
                throw new ArgumentException("Expected lowercase SHA-256 hash.", nameof(contentHash));
            RedisValue stored = await database.StringGetAsync(prefix + "payload:" + contentHash)
                .ConfigureAwait(false);
            if (!stored.HasValue)
            {
                System.Threading.Interlocked.Increment(ref payloadMisses);
                return null;
            }
            byte[] bytes = (byte[])stored;
            // Do not return corrupt data even when Redis or its backing store is damaged.
            if (bytes.Length > 8 * 1024 * 1024 ||
                !String.Equals(OGLObjectCache.ContentHash(bytes), contentHash, StringComparison.Ordinal))
            {
                System.Threading.Interlocked.Increment(ref payloadIntegrityFailures);
                System.Threading.Interlocked.Increment(ref payloadMisses);
                return null;
            }
            System.Threading.Interlocked.Increment(ref payloadHits);
            return bytes;
        }

        // The mapping is deliberately revision-scoped. A caller MUST obtain its
        // revision before reading authoritative FSAssets and recheck it after
        // reading, before publishing. This is not a replacement for the durable
        // write/invalidation protocol required for production multi-writer use.
        private string MappingKey(string assetId, long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            return Key(assetId) + ":map:" + revision;
        }

        public async Task<bool> TryPublishMappingAsync(string assetId, long expectedRevision,
            string contentHash, TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
            if (!IsValidHash(contentHash))
                throw new ArgumentException("Expected lowercase SHA-256 hash.", nameof(contentHash));
            // Atomic compare-and-set avoids publishing a stale mapping when an
            // invalidation races with the publication itself.
            const string script = @"local revision = redis.call('GET', KEYS[1])
if not revision then revision = '0' end
if revision ~= ARGV[1] then return 0 end
redis.call('SET', KEYS[2], ARGV[2], 'PX', ARGV[3])
return 1";
            long result = (long)await database.ScriptEvaluateAsync(script,
                new RedisKey[] { Key(assetId), MappingKey(assetId, expectedRevision) },
                new RedisValue[] { expectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    contentHash, (long)Math.Ceiling(ttl.TotalMilliseconds) }).ConfigureAwait(false);
            return result == 1;
        }

        public async Task<string> GetMappedHashAsync(string assetId, long revision)
        {
            RedisValue hash = await database.StringGetAsync(MappingKey(assetId, revision))
                .ConfigureAwait(false);
            string value = hash.HasValue ? hash.ToString() : null;
            return IsValidHash(value) ? value : null;
        }

        // A cache hit is usable only if the revision is stable across the read.
        // A null return means the caller must consult authoritative storage.
        // This is experimental: all writers must reliably invalidate revisions
        // before it can be connected to a production asset read path.
        public async Task<byte[]> TryGetMappedPayloadAsync(string assetId)
        {
            long before = await GetRevisionAsync(assetId).ConfigureAwait(false);
            string hash = await GetMappedHashAsync(assetId, before).ConfigureAwait(false);
            if (hash == null) return null;
            byte[] payload = await GetPayloadAsync(hash).ConfigureAwait(false);
            if (payload == null) return null;
            long after = await GetRevisionAsync(assetId).ConfigureAwait(false);
            return before == after ? payload : null;
        }

        private static bool IsValidHash(string value) =>
            !String.IsNullOrWhiteSpace(value) && value.Length == 64 &&
            System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-f0-9]{64}$");

        public void Dispose() => connection.Dispose();
    }
}
