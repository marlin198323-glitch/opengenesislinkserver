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
            // One authoritative security preflight for direct registration and
            // host-managed listener startup. Never bypass placeholder checks.
            OGLAssetServiceAdminPreflight.Check(config, server);

            string username = section.GetString("HttpAuthUsername", "");
            string password = section.GetString("HttpAuthPassword", "");
            string role = section.GetString("Role", "");
            string serviceName = section.GetString("ServiceName", "");

            // Credentials are read only from this dedicated section.
            // A dedicated host listener must enforce TLS and network ACLs.
            IServiceAuth auth = new OGLAssetServiceDedicatedAuth(username, password);
            OGLAssetServiceAdminHttpRegistration.RegisterRobustServiceAccount(
                server, connector, auth, serviceName, role == "Administrator", journal);
            return true;
        }
    }
}
