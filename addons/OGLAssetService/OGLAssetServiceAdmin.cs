using System;
using System.Security;
using System.Security.Claims;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Local-only ROBUST administration facade. It does not register a
    /// network route, grant access or mutate the backing asset store.
    /// The hosting application must provide a trusted authenticated principal;
    /// role checks are repeated on every administrative operation.
    /// </summary>
    public sealed class OGLAssetServiceAdmin
    {
        private readonly OGLAssetConnector connector;
        private readonly ClaimsPrincipal principal;

        // Only the connector can create an administration facade.
        internal OGLAssetServiceAdmin(OGLAssetConnector connector, ClaimsPrincipal principal)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
            DemandRole("OGLAssetService.Viewer");
        }

        private void DemandRole(string role)
        {
            if (principal.Identity?.IsAuthenticated != true ||
                (!principal.IsInRole(role) &&
                 !principal.IsInRole("OGLAssetService.Administrator")))
                throw new SecurityException("OGL AssetService administration access denied.");
        }

        /// <summary>
        /// Configuration overview without storage paths, secrets or
        /// connection strings. Flags describe the effective runtime state.
        /// </summary>
        public OGLAssetServiceConfiguration GetConfiguration()
        {
            DemandRole("OGLAssetService.Administrator");
            OGLAssetServiceDiagnostics d = connector.GetDiagnostics();
            return new OGLAssetServiceConfiguration(
                "compatibility", d.AtomicUpdatesEnabled,
                d.LocalCacheEnabled, d.RedisEnabled,
                d.ParallelStoreEnabled, d.ParallelReadPreferred);
        }

        public OGLAssetServiceStatus GetStatus()
        {
            DemandRole("OGLAssetService.Viewer");
            OGLAssetServiceDiagnostics metrics = connector.GetDiagnostics();
            string state;
            string reason;
            if (metrics.AtomicExceptions > 0 || metrics.DiskInvalidationFailures > 0 ||
                metrics.RedisInvalidationFailures > 0 || metrics.ParallelWriteFailures > 0)
            {
                state = "degraded";
                reason = "One or more operation or cache invalidation errors were observed.";
            }
            else if (metrics.RedisEnabled)
            {
                state = "experimental";
                reason = "Redis coordination is enabled; distributed read coherence is not verified.";
            }
            else if (metrics.AtomicUpdatesEnabled)
            {
                state = "experimental";
                reason = "Atomic metadata updates are enabled; end-to-end acceptance is pending.";
            }
            else if (metrics.ParallelStoreEnabled)
            {
                state = "parallel";
                reason = "OGL mirror enabled; FSAssets SQL and payloads remain authoritative.";
            }
            else
            {
                state = "compatibility";
                reason = "The legacy FSAssets compatibility path is active.";
            }

            return new OGLAssetServiceStatus(state, reason, metrics);
        }
    }

    public sealed class OGLAssetServiceConfiguration
    {
        public string Mode { get; }
        public bool ExperimentalAtomicUpdatesEnabled { get; }
        public bool SingleWriterCacheEnabled { get; }
        public bool RedisEnabled { get; }
        public bool ParallelStoreEnabled { get; }
        public bool ParallelReadPreferred { get; }

        public OGLAssetServiceConfiguration(string mode, bool atomicUpdates,
            bool singleWriterCache, bool redisEnabled,
            bool parallelStoreEnabled = false, bool parallelReadPreferred = false)
        {
            Mode = mode;
            ExperimentalAtomicUpdatesEnabled = atomicUpdates;
            SingleWriterCacheEnabled = singleWriterCache;
            RedisEnabled = redisEnabled;
            ParallelStoreEnabled = parallelStoreEnabled;
            ParallelReadPreferred = parallelReadPreferred;
        }
    }

    /// <summary>
    /// Immutable local operational status. Status is an informational
    /// indicator, not a dependency health probe or production readiness gate.
    /// </summary>
    public sealed class OGLAssetServiceStatus
    {
        public string State { get; }
        public string Reason { get; }
        public OGLAssetServiceDiagnostics Diagnostics { get; }

        public OGLAssetServiceStatus(string state, string reason,
            OGLAssetServiceDiagnostics diagnostics)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }
    }
}
