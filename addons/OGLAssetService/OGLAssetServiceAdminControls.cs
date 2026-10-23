using System;
using System.Threading;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Process-local fixed-window request limiter, shared by admin routes.
    /// Not a substitute for host-level distributed or per-client controls.
    /// </summary>
    public sealed class OGLAssetServiceAdminRateLimiter
    {
        private readonly object sync = new object();
        private readonly int requestsPerMinute;
        private long windowStart;
        private int count;

        public OGLAssetServiceAdminRateLimiter(int requestsPerMinute)
        {
            if (requestsPerMinute < 1 || requestsPerMinute > 10000)
                throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
            this.requestsPerMinute = requestsPerMinute;
            windowStart = Environment.TickCount64;
        }

        public bool TryAcquire()
        {
            lock (sync)
            {
                long now = Environment.TickCount64;
                if (now - windowStart >= 60000 || now < windowStart)
                {
                    windowStart = now;
                    count = 0;
                }
                if (count >= requestsPerMinute) return false;
                count++;
                return true;
            }
        }
    }

    /// <summary>
    /// Privacy-preserving process-local outcome counters. No request body,
    /// remote address, identity, token or asset identifier is stored.
    /// </summary>
    public sealed class OGLAssetServiceAdminAudit
    {
        private long allowed;
        private long unauthorized;
        private long forbidden;
        private long throttled;
        private long errors;

        public long Allowed => Interlocked.Read(ref allowed);
        public long Unauthorized => Interlocked.Read(ref unauthorized);
        public long Forbidden => Interlocked.Read(ref forbidden);
        public long Throttled => Interlocked.Read(ref throttled);
        public long Errors => Interlocked.Read(ref errors);

        public void Record(string outcome)
        {
            switch (outcome)
            {
                case "allowed": Interlocked.Increment(ref allowed); break;
                case "unauthorized": Interlocked.Increment(ref unauthorized); break;
                case "forbidden": Interlocked.Increment(ref forbidden); break;
                case "throttled": Interlocked.Increment(ref throttled); break;
                default: Interlocked.Increment(ref errors); break;
            }
        }
    }
}
