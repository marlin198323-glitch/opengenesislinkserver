using System;
using System.Net;
using System.Security.Claims;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Adapts ROBUST's existing IServiceAuth authentication to a restricted
    /// service identity. IServiceAuth does not expose user identity or roles.
    /// The operator must assign roles to this dedicated service credential
    /// out-of-band. Do not reuse public grid-to-grid asset credentials.
    /// </summary>
    public sealed class OGLAssetServiceRobustAuth
    {
        private readonly IServiceAuth authentication;
        private readonly string serviceName;
        private readonly bool administrator;

        public OGLAssetServiceRobustAuth(IServiceAuth authentication,
            string serviceName, bool administrator)
        {
            this.authentication = authentication ??
                throw new ArgumentNullException(nameof(authentication));
            if (String.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name required.", nameof(serviceName));
            this.serviceName = serviceName;
            this.administrator = administrator;
        }

        public ClaimsPrincipal Resolve(IOSHttpRequest request)
        {
            if (request == null) return null;
            HttpStatusCode status;
            // Only the ROBUST authenticator decides whether credentials are valid.
            // A successful credential authenticates one preconfigured service role.
            if (!authentication.Authenticate(request.Headers, (k, v) => { }, out status))
                return null;

            string role = administrator
                ? "OGLAssetService.Administrator"
                : "OGLAssetService.Viewer";
            var identity = new ClaimsIdentity("ROBUST-ServiceAuth");
            identity.AddClaim(new Claim(ClaimTypes.Name, serviceName));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            return new ClaimsPrincipal(identity);
        }
    }
}
