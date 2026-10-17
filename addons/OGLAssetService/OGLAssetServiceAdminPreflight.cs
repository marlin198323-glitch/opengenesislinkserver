using System;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Validates host prerequisites before the audit journal is opened.
    /// This does not register routes or claim that network ACLs are enforced.
    /// </summary>
    public static class OGLAssetServiceAdminPreflight
    {
        public static bool Check(IConfigSource config, IHttpServer listener)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            IConfig section = config.Configs["OGLAssetServiceAdmin"];
            if (section == null || !section.GetBoolean("Enabled", false))
                return false;
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (!listener.UseSSL || listener.SSLPort == 0)
                throw new InvalidOperationException("Dedicated TLS listener required.");
            int configuredPort = section.GetInt("ExpectedTlsPort", 0);
            if (configuredPort <= 0 || (uint)configuredPort != listener.SSLPort)
                throw new InvalidOperationException("Administrative TLS listener port mismatch.");
            if (!section.GetBoolean("DedicatedListenerConfirmed", false))
                throw new InvalidOperationException("Dedicated listener must be explicitly confirmed.");
            if (section.GetString("AuthType", "") != "BasicHttpAuthentication")
                throw new InvalidOperationException("Explicit Basic authentication required.");
            string user = section.GetString("HttpAuthUsername", "");
            string password = section.GetString("HttpAuthPassword", "");
            if (String.IsNullOrWhiteSpace(user) || user.Length < 8 ||
                user.Contains(":") || String.IsNullOrWhiteSpace(password) ||
                password.Length < 16 ||
                user.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
                password.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid dedicated credentials.");
            string role = section.GetString("Role", "");
            if (role != "Viewer" && role != "Administrator")
                throw new InvalidOperationException("Explicit administrative role required.");
            if (String.IsNullOrWhiteSpace(section.GetString("ServiceName", "")))
                throw new InvalidOperationException("Service identity required.");
            return true;
        }
    }
}
