using System;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.ServiceAuth;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Opt-in ROBUST HTTP handler. A trusted host must authenticate the
    /// request and resolve an authorized principal before registering it.
    /// No caller-controlled headers or query strings are treated as roles.
    /// </summary>
    public sealed class OGLAssetServiceAdminHttpHandler : BaseStreamHandler
    {
        public const string StatusPath = "/ogl/admin/assetservice/status";
        public const string ConfigurationPath = "/ogl/admin/assetservice/configuration";

        private readonly OGLAssetConnector connector;
        private readonly Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal;
        private readonly bool configuration;
        private readonly OGLAssetServiceAdminAudit audit;
        private readonly OGLAssetServiceAdminAuditJournal journal;
        private readonly OGLAssetServiceAdminRateLimiter limiter;

        public OGLAssetServiceAdminHttpHandler(OGLAssetConnector connector,
            Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal, bool configuration,
            OGLAssetServiceAdminAudit audit, OGLAssetServiceAdminRateLimiter limiter,
            OGLAssetServiceAdminAuditJournal journal)
            : base("GET", configuration ? ConfigurationPath : StatusPath)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.resolvePrincipal = resolvePrincipal ??
                throw new ArgumentNullException(nameof(resolvePrincipal));
            this.configuration = configuration;
            this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
            this.journal = journal;
            this.limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
        }

        private bool RecordOutcome(string outcome)
        {
            audit.Record(outcome);
            if (journal == null) return true;
            try
            {
                journal.Record(configuration ? "configuration" : "status", outcome);
                return true;
            }
            catch (Exception)
            {
                // Audit failures must not silently authorize a request.
                // The hosting service should monitor and alert on this counter.
                audit.Record("error");
                return false;
            }
        }

        protected override byte[] ProcessRequest(string path, Stream request,
            IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.AddHeader("Cache-Control", "no-store");
            httpResponse.AddHeader("X-Content-Type-Options", "nosniff");
            httpResponse.ContentType = "application/json; charset=utf-8";
            if (!limiter.TryAcquire())
            {
                httpResponse.StatusCode = 429;
                if (!RecordOutcome("throttled"))
                {
                    httpResponse.StatusCode = 503;
                    return Encoding.UTF8.GetBytes("{\"error\":\"audit_unavailable\"}");
                }
                return Encoding.UTF8.GetBytes("{\"error\":\"rate_limited\"}");
            }

            string outcome = "error";
            int status = 500;
            byte[] body = Encoding.UTF8.GetBytes("{\"error\":\"internal_error\"}");
            try
            {
                ClaimsPrincipal principal = resolvePrincipal(httpRequest);
                if (principal?.Identity?.IsAuthenticated != true)
                {
                    outcome = "unauthorized";
                    status = 401;
                    body = Encoding.UTF8.GetBytes("{\"error\":\"unauthorized\"}");
                }
                else
                {
                    OGLAssetServiceAdmin admin = connector.CreateAdmin(principal);
                    object response = configuration
                        ? (object)admin.GetConfiguration()
                        : admin.GetStatus();
                    body = JsonSerializer.SerializeToUtf8Bytes(response);
                    outcome = "allowed";
                    status = 200;
                }
            }
            catch (SecurityException)
            {
                outcome = "forbidden";
                status = 403;
                body = Encoding.UTF8.GetBytes("{\"error\":\"access_denied\"}");
            }
            catch (Exception)
            {
                outcome = "error";
                status = 500;
                body = Encoding.UTF8.GetBytes("{\"error\":\"internal_error\"}");
            }

            if (!RecordOutcome(outcome))
            {
                // Fail closed if a configured persistent audit journal cannot
                // durably record the request, including otherwise valid reads.
                httpResponse.StatusCode = 503;
                return Encoding.UTF8.GetBytes("{\"error\":\"audit_unavailable\"}");
            }
            httpResponse.StatusCode = status;
            return body;
        }
    }

    /// <summary>
    /// Registers read-only handlers on an explicitly chosen ROBUST listener.
    /// Call only after the host has configured its trusted identity resolver.
    /// </summary>
    public static class OGLAssetServiceAdminHttpRegistration
    {
        private static readonly object OwnedLock = new object();
        private static readonly System.Collections.Generic.Dictionary<BaseHttpServer, OGLAssetServiceAdminHttpHandler[]> Owned
            = new System.Collections.Generic.Dictionary<BaseHttpServer, OGLAssetServiceAdminHttpHandler[]>();

        public static void UnregisterOwned(IHttpServer server)
        {
            if (server is not BaseHttpServer host) return;
            lock (OwnedLock)
            {
                if (!Owned.TryGetValue(host, out var handlers)) return;
                foreach (var handler in handlers)
                    host.TryRemoveOwnedStreamHandler(handler);
                Owned.Remove(host);
            }
        }

        /// <summary>
        /// Explicit dedicated ROBUST service-credential integration.
        /// Never supply a shared public asset service credential.
        /// </summary>
        public static void RegisterRobustServiceAccount(IHttpServer server,
            OGLAssetConnector connector, IServiceAuth authentication,
            string serviceName, bool administrator,
            OGLAssetServiceAdminAuditJournal journal)
        {
            var resolver = new OGLAssetServiceRobustAuth(
                authentication, serviceName, administrator);
            Register(server, connector, resolver.Resolve, journal);
        }

        public static void Register(IHttpServer server, OGLAssetConnector connector,
            Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (resolvePrincipal == null)
                throw new ArgumentNullException(nameof(resolvePrincipal));

            Register(server, connector, resolvePrincipal, null);
        }

        public static void Register(IHttpServer server, OGLAssetConnector connector,
            Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal,
            OGLAssetServiceAdminAuditJournal journal)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (resolvePrincipal == null) throw new ArgumentNullException(nameof(resolvePrincipal));
            if (server is not BaseHttpServer host)
                throw new InvalidOperationException("Exclusive admin routes require BaseHttpServer.");
            var audit = new OGLAssetServiceAdminAudit();
            var limiter = new OGLAssetServiceAdminRateLimiter(60);
            var status = new OGLAssetServiceAdminHttpHandler(
                connector, resolvePrincipal, false, audit, limiter, journal);
            var configuration = new OGLAssetServiceAdminHttpHandler(
                connector, resolvePrincipal, true, audit, limiter, journal);
            if (!host.TryAddExclusiveStreamHandler(status))
                throw new InvalidOperationException("Administrative status route is already occupied.");
            try
            {
                if (!host.TryAddExclusiveStreamHandler(configuration))
                    throw new InvalidOperationException("Administrative configuration route is already occupied.");
                lock (OwnedLock)
                {
                    if (Owned.ContainsKey(host))
                        throw new InvalidOperationException("Administrative routes already owned.");
                    Owned.Add(host, new[] { status, configuration });
                }
            }
            catch
            {
                host.TryRemoveOwnedStreamHandler(configuration);
                host.TryRemoveOwnedStreamHandler(status);
                throw;
            }
        }
    }
}
