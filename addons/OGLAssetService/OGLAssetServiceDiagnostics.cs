using System;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Immutable point-in-time operational snapshot. No asset identifiers,
    /// connection strings, credentials or payload bytes are included.
    /// </summary>
    public sealed class OGLAssetServiceDiagnostics
    {
        public DateTimeOffset CapturedAtUtc { get; }
        public bool AtomicUpdatesEnabled { get; }
        public bool LocalCacheEnabled { get; }
        public bool RedisEnabled { get; }
        public long AtomicAttempts { get; }
        public long AtomicSucceeded { get; }
        public long AtomicRejected { get; }
        public long AtomicExceptions { get; }
        public double AtomicTotalMilliseconds { get; }
        public long MemoryCacheHits { get; }
        public long MemoryCacheMisses { get; }
        public long MemoryCacheUsedBytes { get; }
        public long DiskCacheHits { get; }
        public long DiskCacheMisses { get; }
        public long DiskCacheEvictions { get; }
        public long LocalInvalidations { get; }
        public long DiskInvalidationFailures { get; }
        public long RedisInvalidationFailures { get; }

        public OGLAssetServiceDiagnostics(
            bool atomicUpdatesEnabled, bool localCacheEnabled, bool redisEnabled,
            long attempts, long succeeded, long rejected, long exceptions,
            long elapsedTicks, long memoryHits, long memoryMisses, long memoryBytes,
            long diskHits, long diskMisses, long diskEvictions, long localInvalidations,
            long diskInvalidationFailures, long redisInvalidationFailures)
        {
            CapturedAtUtc = DateTimeOffset.UtcNow;
            AtomicUpdatesEnabled = atomicUpdatesEnabled;
            LocalCacheEnabled = localCacheEnabled;
            RedisEnabled = redisEnabled;
            AtomicAttempts = attempts;
            AtomicSucceeded = succeeded;
            AtomicRejected = rejected;
            AtomicExceptions = exceptions;
            AtomicTotalMilliseconds = elapsedTicks * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;
            MemoryCacheHits = memoryHits;
            MemoryCacheMisses = memoryMisses;
            MemoryCacheUsedBytes = memoryBytes;
            DiskCacheHits = diskHits;
            DiskCacheMisses = diskMisses;
            DiskCacheEvictions = diskEvictions;
            LocalInvalidations = localInvalidations;
            DiskInvalidationFailures = diskInvalidationFailures;
            RedisInvalidationFailures = redisInvalidationFailures;
        }
    }
}
