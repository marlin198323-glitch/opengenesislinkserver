using System;
using Nini.Config;
using OpenSim.Services.FSAssetService;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Compatibility-first OGL AssetService entry point.
    /// Delegates all asset operations to the existing FSAssetConnector.
    /// No migration, relocation or deletion of existing fsassets occurs.
    /// </summary>
    public sealed class OGLAssetConnector : FSAssetConnector, IAssetService
    {
        private readonly OGLObjectCache cache;
        private readonly OGLDiskCache diskCache;
        private readonly object cacheSync = new object();
        private readonly OGLRedisAssetCoordinator redis;
        private readonly bool redisEnabled;
        private long redisInvalidationFailures;
        public long RedisInvalidationFailures => System.Threading.Interlocked.Read(ref redisInvalidationFailures);
        public long MemoryCacheHits => cache.Hits;
        public long MemoryCacheMisses => cache.Misses;
        public long MemoryCacheUsedBytes => cache.UsedBytes;
        public long DiskCacheHits => diskCache?.Hits ?? 0;
        public long DiskCacheMisses => diskCache?.Misses ?? 0;
        public long DiskCacheEvictions => diskCache?.Evictions ?? 0;
        public OGLAssetConnector(IConfigSource config, string configName)
            : base(config)
        {
            IConfig settings = config.Configs["OGLAssetService"];
            if (settings == null || !settings.GetBoolean("Enabled", false))
                throw new InvalidOperationException(
                    "OGL AssetService is disabled. Set [OGLAssetService] Enabled = true only on a test instance.");

            int cacheMiB = settings.GetInt("MemoryCacheMiB", 64);
            if (cacheMiB < 0 || cacheMiB > 4096)
                throw new ArgumentOutOfRangeException("MemoryCacheMiB", "Must be between 0 and 4096 MiB.");
            cache = new OGLObjectCache((long)cacheMiB * 1024 * 1024);
            if (settings.GetBoolean("DiskCacheEnabled", false))
            {
                int diskMiB = settings.GetInt("DiskCacheMaxMiB", 1024);
                if (diskMiB < 1 || diskMiB > 1048576)
                    throw new ArgumentOutOfRangeException("DiskCacheMaxMiB", "Must be between 1 and 1048576 MiB.");
                diskCache = new OGLDiskCache(
                    settings.GetString("DiskCacheDirectory", ""),
                    (long)diskMiB * 1024 * 1024);
            }

            redisEnabled = settings.GetBoolean("RedisEnabled", false);
            if (redisEnabled)
            {
                string redisConnection = Environment.GetEnvironmentVariable("OGL_ASSET_REDIS_CONNECTION");
                if (String.IsNullOrWhiteSpace(redisConnection))
                    throw new InvalidOperationException("OGL_ASSET_REDIS_CONNECTION must be set when RedisEnabled=true.");
                redis = new OGLRedisAssetCoordinator(redisConnection, settings.GetInt("RedisDatabase", 2));
            }

            string mode = settings.GetString("Mode", "compatibility");
            if (!String.Equals(mode, "compatibility", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "OGL AssetService currently supports only Mode = compatibility. Distributed storage is not implemented.");
        }

        private void TryInvalidateRedis(string id)
        {
            if (!redisEnabled || String.IsNullOrWhiteSpace(id)) return;
            try { redis.InvalidateAsync(id).GetAwaiter().GetResult(); }
            catch (Exception)
            {
                // The authoritative write already succeeded. Never report it as failed.
                // Distributed caches remain disabled until a durable outbox exists.
                System.Threading.Interlocked.Increment(ref redisInvalidationFailures);
            }
        }

        byte[] IAssetService.GetData(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return base.GetData(id);
            lock (cacheSync)
            {
                // Redis connectivity or revision failures must never return stale cached data.
                // In this experimental mode all reads bypass local caches until a
                // distributed writer protocol has been verified end-to-end.
                if (redisEnabled) return base.GetData(id);
                if (cache.TryGet(id, out byte[] result)) return result;
                if (diskCache != null)
                {
                    try
                    {
                        if (diskCache.TryGet(id, out result))
                        {
                            cache.Put(id, result);
                            return result;
                        }
                    }
                    catch (System.IO.IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                result = base.GetData(id);
                if (result != null && !String.IsNullOrEmpty(id))
                {
                    cache.Put(id, result);
                    try { diskCache?.Put(id, result); }
                    catch (System.IO.IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return result;
            }
        }

        bool IAssetService.UpdateContent(string id, byte[] data)
        {
            lock (cacheSync)
            {
                cache.Invalidate(id);
                try { diskCache?.Invalidate(id); }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
                bool updated = base.UpdateContent(id, data);
                if (updated) TryInvalidateRedis(id);
                return updated;
            }
        }

        bool IAssetService.Delete(string id)
        {
            lock (cacheSync)
            {
                cache.Invalidate(id);
                try { diskCache?.Invalidate(id); }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
                bool deleted = base.Delete(id);
                if (deleted) TryInvalidateRedis(id);
                return deleted;
            }
        }

        string IAssetService.Store(AssetBase asset)
        {
            lock (cacheSync)
            {
                string id = base.Store(asset);
                if (!String.IsNullOrEmpty(id))
                {
                    cache.Invalidate(id);
                    try { diskCache?.Invalidate(id); }
                    catch (System.IO.IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                TryInvalidateRedis(id);
                return id;
            }
        }
    }
}
