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
        private readonly bool localCacheEnabled;
        private readonly bool atomicUpdatesEnabled;
        private long redisInvalidationFailures;
        private long atomicUpdateAttempts;
        private long atomicUpdateSucceeded;
        private long atomicUpdateRejected;
        private long atomicUpdateExceptions;
        private long atomicUpdateElapsedTicks;
        private long localInvalidationAttempts;
        private long diskInvalidationFailures;

        // Read-only operational counters; no asset IDs or payloads are exposed.
        public long AtomicUpdateAttempts => System.Threading.Interlocked.Read(ref atomicUpdateAttempts);
        public long AtomicUpdateSucceeded => System.Threading.Interlocked.Read(ref atomicUpdateSucceeded);
        public long AtomicUpdateRejected => System.Threading.Interlocked.Read(ref atomicUpdateRejected);
        public long AtomicUpdateExceptions => System.Threading.Interlocked.Read(ref atomicUpdateExceptions);
        public long AtomicUpdateElapsedTicks => System.Threading.Interlocked.Read(ref atomicUpdateElapsedTicks);
        public long LocalInvalidationAttempts => System.Threading.Interlocked.Read(ref localInvalidationAttempts);
        public long DiskInvalidationFailures => System.Threading.Interlocked.Read(ref diskInvalidationFailures);
        public bool ExperimentalAtomicUpdatesEnabled => atomicUpdatesEnabled;
        public bool LocalCacheEnabled => localCacheEnabled;
        public bool RedisCoordinationEnabled => redisEnabled;
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
            // Process-local cache entries cannot be invalidated by other writers.
            localCacheEnabled = settings.GetBoolean("SingleWriterCacheEnabled", false);
            atomicUpdatesEnabled = settings.GetBoolean("ExperimentalAtomicUpdatesEnabled", false);
            cache = new OGLObjectCache((long)cacheMiB * 1024 * 1024);
            if (settings.GetBoolean("DiskCacheEnabled", false) && !localCacheEnabled)
                throw new InvalidOperationException(
                    "DiskCacheEnabled requires SingleWriterCacheEnabled=true and exclusive asset writers.");
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
            if (redisEnabled && localCacheEnabled)
                throw new InvalidOperationException(
                    "SingleWriterCacheEnabled and RedisEnabled cannot be combined until distributed invalidation is verified.");
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

        /// <summary>
        /// Obtain a point-in-time diagnostics snapshot without exposing
        /// credentials, asset identifiers or payload contents.
        /// </summary>
        public OGLAssetServiceDiagnostics GetDiagnostics()
        {
            return new OGLAssetServiceDiagnostics(
                atomicUpdatesEnabled, localCacheEnabled, redisEnabled,
                AtomicUpdateAttempts, AtomicUpdateSucceeded, AtomicUpdateRejected,
                AtomicUpdateExceptions, AtomicUpdateElapsedTicks,
                MemoryCacheHits, MemoryCacheMisses, MemoryCacheUsedBytes,
                DiskCacheHits, DiskCacheMisses, DiskCacheEvictions,
                LocalInvalidationAttempts, DiskInvalidationFailures,
                RedisInvalidationFailures);
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

        // Keep all virtual read entry points serialized with local writes.
        // Base.Get may populate an asset from fallback storage through Store;
        // Monitor locks are reentrant, so this remains safe on the same thread.
        // This does NOT provide cross-process transactional isolation.
        public override AssetBase Get(string id)
        {
            lock (cacheSync) return base.Get(id);
        }

        public override AssetMetadata GetMetadata(string id)
        {
            lock (cacheSync) return base.GetMetadata(id);
        }

        public override bool[] AssetsExist(string[] ids)
        {
            lock (cacheSync) return base.AssetsExist(ids);
        }

        public override byte[] GetData(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return base.GetData(id);
            lock (cacheSync)
            {
                // Redis connectivity or revision failures must never return stale cached data.
                // In this experimental mode all reads bypass local caches until a
                // distributed writer protocol has been verified end-to-end.
                if (redisEnabled || !localCacheEnabled) return base.GetData(id);
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

        // The legacy base UpdateContent is nonvirtual and remains unchanged.
        // Explicit IAssetService dispatch is the opt-in update entry point.
        bool IAssetService.UpdateContent(string id, byte[] data)
        {
            lock (cacheSync)
            {
                // Keep the legacy compatibility path unchanged. Counters only
                // describe the explicitly enabled atomic update feature.
                long started = 0;
                if (atomicUpdatesEnabled)
                {
                    System.Threading.Interlocked.Increment(ref atomicUpdateAttempts);
                    started = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                try
                {
                    bool updated = atomicUpdatesEnabled
                        ? TryUpdateContentAtomic(id, data)
                        : base.UpdateContent(id, data);
                    if (atomicUpdatesEnabled)
                    {
                        if (updated)
                            System.Threading.Interlocked.Increment(ref atomicUpdateSucceeded);
                        else
                            System.Threading.Interlocked.Increment(ref atomicUpdateRejected);
                    }
                    return updated;
                }
                catch (Exception)
                {
                    if (atomicUpdatesEnabled)
                        System.Threading.Interlocked.Increment(ref atomicUpdateExceptions);
                    throw;
                }
                finally
                {
                    if (atomicUpdatesEnabled)
                        System.Threading.Interlocked.Add(ref atomicUpdateElapsedTicks,
                            System.Diagnostics.Stopwatch.GetTimestamp() - started);
                    // Invalidate even when disk staging, SQL CAS or normalization
                    // throws. The caller must not retain a potentially stale copy.
                    InvalidateLocal(id);
                    // The SQL write may have committed before a subsequent error.
                    // Redis is a best-effort hint; Redis-backed reads are disabled.
                    if (atomicUpdatesEnabled) TryInvalidateRedis(id);
                }
            }
        }

        public override bool Delete(string id)
        {
            lock (cacheSync)
            {
                try
                {
                    return base.Delete(id);
                }
                finally
                {
                    // Legacy Delete reports success even when metadata removal
                    // fails; never use that boolean as cache-coherence evidence.
                    InvalidateLocal(id);
                    TryInvalidateRedis(id);
                }
            }
        }

        public override string Store(AssetBase asset)
        {
            lock (cacheSync)
            {
                string originalId = asset?.ID;
                string id = null;
                try
                {
                    id = base.Store(asset);
                    return id;
                }
                finally
                {
                    // FSAssetConnector can write payload bytes before metadata
                    // commit and can also assign an ID during Store. Even if
                    // it throws, never retain potentially obsolete local data.
                    InvalidateLocal(originalId);
                    InvalidateLocal(asset?.ID);
                    InvalidateLocal(id);
                    // Redis remains a best-effort hint only; distributed cache
                    // reads are disabled until all writers use a durable protocol.
                    if (!String.IsNullOrWhiteSpace(id) &&
                        !String.Equals(id, "00000000-0000-0000-0000-000000000000",
                            StringComparison.OrdinalIgnoreCase))
                        TryInvalidateRedis(id);
                }
            }
        }

        private void InvalidateLocal(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            System.Threading.Interlocked.Increment(ref localInvalidationAttempts);
            cache.Invalidate(id);
            try { diskCache?.Invalidate(id); }
            catch (System.IO.IOException)
            {
                System.Threading.Interlocked.Increment(ref diskInvalidationFailures);
            }
            catch (UnauthorizedAccessException)
            {
                System.Threading.Interlocked.Increment(ref diskInvalidationFailures);
            }
        }

    }
}
