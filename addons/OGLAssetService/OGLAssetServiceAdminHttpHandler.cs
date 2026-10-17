using System;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using OpenSim.Framework.Servers.HttpServer;

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
        private readonly OGLAssetServiceAdminRateLimiter limiter;

        public OGLAssetServiceAdminHttpHandler(OGLAssetConnector connector,
            Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal, bool configuration,
            OGLAssetServiceAdminAudit audit, OGLAssetServiceAdminRateLimiter limiter)
            : base("GET", configuration ? ConfigurationPath : StatusPath)
        {
            this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
            this.resolvePrincipal = resolvePrincipal ??
                throw new ArgumentNullException(nameof(resolvePrincipal));
            this.configuration = configuration;
            this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
            this.limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
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
                audit.Record("throttled");
                return Encoding.UTF8.GetBytes("{}");
            }
            try
            {
                // The resolver MUST validate a host-controlled authenticated
                // session; it must not trust unverified HTTP identity headers.
                ClaimsPrincipal principal = resolvePrincipal(httpRequest);
                if (principal?.Identity?.IsAuthenticated != true)
                {
                    httpResponse.StatusCode = 401;
                    audit.Record("unauthorized");
                    return Encoding.UTF8.GetBytes("{}");
                }
                OGLAssetServiceAdmin admin = connector.CreateAdmin(principal);
                object response = configuration
                    ? (object)admin.GetConfiguration()
                    : admin.GetStatus();
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                audit.Record("allowed");
                return JsonSerializer.SerializeToUtf8Bytes(response);
            }
            catch (SecurityException)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                audit.Record("forbidden");
                return Encoding.UTF8.GetBytes("{\"error\":\"access_denied\"}");
            }
            catch (ArgumentNullException)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                audit.Record("unauthorized");
                return Encoding.UTF8.GetBytes("{\"error\":\"unauthorized\"}");
            }
            catch (Exception)
            {
                // Never leak exception messages, paths or connection strings.
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                audit.Record("error");
                return Encoding.UTF8.GetBytes("{\"error\":\"internal_error\"}");
            }
        }
    }

    /// <summary>
    /// Registers read-only handlers on an explicitly chosen ROBUST listener.
    /// Call only after the host has configured its trusted identity resolver.
    /// </summary>
    public static class OGLAssetServiceAdminHttpRegistration
    {
        public static void Register(IHttpServer server, OGLAssetConnector connector,
            Func<IOSHttpRequest, ClaimsPrincipal> resolvePrincipal)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (resolvePrincipal == null)
                throw new ArgumentNullException(nameof(resolvePrincipal));

            var audit = new OGLAssetServiceAdminAudit();
            var limiter = new OGLAssetServiceAdminRateLimiter(60);
            server.AddStreamHandler(new OGLAssetServiceAdminHttpHandler(
                connector, resolvePrincipal, false, audit, limiter));
            server.AddStreamHandler(new OGLAssetServiceAdminHttpHandler(
                connector, resolvePrincipal, true, audit, limiter));
        }
    }
}
