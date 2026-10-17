using System;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Local-only ROBUST administration facade. It does not register a
    /// network route, grant access or mutate the backing asset store.
    /// The hosting application must authenticate and authorize operators
    /// before calling this API.
    /// </summary>
    public sealed class OGLAssetServiceAdmin
    {
        private readonly OGLAssetConnector connector;

        public OGLAssetServiceAdmin(OGLAssetConnector connector)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        public OGLAssetServiceStatus GetStatus()
        {
            OGLAssetServiceDiagnostics metrics = connector.GetDiagnostics();
            string state;
            string reason;
            if (metrics.AtomicExceptions > 0 || metrics.DiskInvalidationFailures > 0 ||
                metrics.RedisInvalidationFailures > 0)
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
            else
            {
                state = "compatibility";
                reason = "The legacy FSAssets compatibility path is active.";
            }

            return new OGLAssetServiceStatus(state, reason, metrics);
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
