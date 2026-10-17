using System;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Host-owned lifecycle boundary for the existing ROBUST asset connector.
    /// The caller owns both HTTP servers; this object never starts a listener.
    /// </summary>
    public sealed class OGLAssetServiceRobustLifecycle : IDisposable
    {
        private readonly object gate = new object();
        private OGLAssetServiceAdminListener administration;
        private bool disposed;

        public bool IsActive
        {
            get { lock (gate) return administration != null && !disposed; }
        }

        public void Start(IConfigSource config, IHttpServer publicAssetServer,
            IHttpServer dedicatedAdminServer, IAssetService loadedAssetService,
            string auditPath, long maxJournalBytes, int retainedFiles)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            IConfig section = config.Configs[OGLAssetServiceAdminHostConfiguration.Section];
            if (section == null || !section.GetBoolean("Enabled", false)) return;
            if (publicAssetServer == null) throw new ArgumentNullException(nameof(publicAssetServer));
            if (dedicatedAdminServer == null) throw new ArgumentNullException(nameof(dedicatedAdminServer));
            if (ReferenceEquals(publicAssetServer, dedicatedAdminServer) ||
                publicAssetServer.Port == dedicatedAdminServer.Port)
                throw new InvalidOperationException("Admin and public asset listeners must be distinct.");
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(OGLAssetServiceRobustLifecycle));
                if (administration != null)
                    throw new InvalidOperationException("Admin lifecycle already started.");
                administration = OGLAssetServiceRobustBootstrap.StartAdmin(
                    config, dedicatedAdminServer, loadedAssetService,
                    auditPath, maxJournalBytes, retainedFiles);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                try { administration?.Dispose(); }
                finally { administration = null; }
            }
        }
    }
}
