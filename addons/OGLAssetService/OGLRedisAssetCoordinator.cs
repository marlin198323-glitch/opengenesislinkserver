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

        public void Dispose() => connection.Dispose();
    }
}
