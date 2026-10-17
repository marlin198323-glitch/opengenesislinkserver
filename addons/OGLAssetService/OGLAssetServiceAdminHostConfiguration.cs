using System;
using Nini.Config;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Explicit opt-in binding for a dedicated ROBUST administrative listener.
    /// The caller selects the listener; this class does not open a port.
    /// </summary>
    public static class OGLAssetServiceAdminHostConfiguration
    {
        public const string Section = "OGLAssetServiceAdmin";

        public static bool RegisterIfEnabled(IConfigSource config, IHttpServer server,
            OGLAssetConnector connector, OGLAssetServiceAdminAuditJournal journal)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            IConfig section = config.Configs[Section];
            if (section == null || !section.GetBoolean("Enabled", false))
                return false;

            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (journal == null)
                throw new InvalidOperationException("Persistent audit journal is required.");
            if (server.SSLPort == 0 || !server.UseSSL)
                throw new InvalidOperationException("The administrative listener must use TLS.");
            uint expectedPort = (uint)section.GetInt("ExpectedTlsPort", 0);
            if (expectedPort == 0 || expectedPort != server.SSLPort)
                throw new InvalidOperationException("Administrative TLS port does not match host configuration.");
            if (!section.GetBoolean("DedicatedListenerConfirmed", false))
                throw new InvalidOperationException("A dedicated restricted admin listener must be confirmed.");

            // Do not allow implicit Network-section credential inheritance or
            // a policy-only IServiceAuth that authenticates without credentials.
            if (!String.Equals(section.GetString("AuthType", ""), "BasicHttpAuthentication",
                StringComparison.Ordinal))
                throw new InvalidOperationException("Dedicated BasicHttpAuthentication required.");
            string username = section.GetString("HttpAuthUsername", "");
            string password = section.GetString("HttpAuthPassword", "");
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password) ||
                username.Contains(":") || username.Length < 8 || password.Length < 16)
                throw new InvalidOperationException("Invalid dedicated admin credentials.");

            string role = section.GetString("Role", "");
            if (role != "Viewer" && role != "Administrator")
                throw new InvalidOperationException("Explicit Viewer or Administrator role required.");
            string serviceName = section.GetString("ServiceName", "");
            if (String.IsNullOrWhiteSpace(serviceName))
                throw new InvalidOperationException("Dedicated service identity required.");

            // This constructor reads the credentials from the named section.
            // A dedicated host listener must enforce TLS and network ACLs.
            IServiceAuth auth = new BasicHttpAuthentication(config, Section);
            OGLAssetServiceAdminHttpRegistration.RegisterRobustServiceAccount(
                server, connector, auth, serviceName, role == "Administrator", journal);
            return true;
        }
    }
}
