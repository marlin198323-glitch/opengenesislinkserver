using System;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Host-owned lifecycle for optional administrative routes.
    /// A dedicated TLS listener is supplied by ROBUST, never created here.
    /// </summary>
    public sealed class OGLAssetServiceAdminListener : IDisposable
    {
        private readonly IHttpServer server;
        private readonly OGLAssetServiceAdminAuditJournal journal;
        private bool registered;
        private bool disposed;

        private OGLAssetServiceAdminListener(IHttpServer server,
            OGLAssetServiceAdminAuditJournal journal)
        {
            this.server = server;
            this.journal = journal;
        }

        public static OGLAssetServiceAdminListener Start(IConfigSource config,
            IHttpServer server, OGLAssetConnector connector, string auditPath,
            long maxJournalBytes, int retainedFiles)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            IConfig settings = config.Configs[OGLAssetServiceAdminHostConfiguration.Section];
            if (settings == null || !settings.GetBoolean("Enabled", false))
                return null;
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (connector == null) throw new ArgumentNullException(nameof(connector));

            var journal = new OGLAssetServiceAdminAuditJournal(
                auditPath, maxJournalBytes, retainedFiles);
            var listener = new OGLAssetServiceAdminListener(server, journal);
            try
            {
                // Registration is opt-in and validates TLS and credentials.
                listener.registered =
                    OGLAssetServiceAdminHostConfiguration.RegisterIfEnabled(
                        config, server, connector, journal);
                return listener;
            }
            catch
            {
                // Registration can fail after the first handler was added.
                // The host must reserve these paths on its dedicated listener.
                server.RemoveStreamHandler("GET", OGLAssetServiceAdminHttpHandler.StatusPath);
                server.RemoveStreamHandler("GET", OGLAssetServiceAdminHttpHandler.ConfigurationPath);
                journal.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (registered)
            {
                server.RemoveStreamHandler("GET", OGLAssetServiceAdminHttpHandler.StatusPath);
                server.RemoveStreamHandler("GET", OGLAssetServiceAdminHttpHandler.ConfigurationPath);
            }
            journal.Dispose();
        }
    }
}
