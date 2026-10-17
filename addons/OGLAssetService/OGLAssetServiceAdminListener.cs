using System;
using System.Collections.Generic;
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
        private static readonly object RegistrationLock = new object();
        private static readonly HashSet<IHttpServer> ActiveServers = new HashSet<IHttpServer>();
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

            OGLAssetServiceAdminPreflight.Check(config, server);
            lock (RegistrationLock)
            {
                if (!ActiveServers.Add(server))
                    throw new InvalidOperationException("Administrative listener is already registered on this server.");
            }
            OGLAssetServiceAdminAuditJournal journal;
            try
            {
                journal = new OGLAssetServiceAdminAuditJournal(
                    auditPath, maxJournalBytes, retainedFiles);
            }
            catch
            {
                lock (RegistrationLock) ActiveServers.Remove(server);
                throw;
            }
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
                OGLAssetServiceAdminHttpRegistration.UnregisterOwned(server);
                journal.Dispose();
                lock (RegistrationLock) ActiveServers.Remove(server);
                throw;
            }
        }

        public void Dispose()
        {
            lock (RegistrationLock)
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    if (registered)
                    {
                        OGLAssetServiceAdminHttpRegistration.UnregisterOwned(server);
                    }
                }
                finally
                {
                    try { journal.Dispose(); }
                    finally { ActiveServers.Remove(server); }
                }
            }
        }
    }
}
