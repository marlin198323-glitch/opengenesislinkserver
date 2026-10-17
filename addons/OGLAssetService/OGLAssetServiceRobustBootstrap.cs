using System;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Explicit host integration for the exact asset instance loaded by ROBUST.
    /// Never constructs another asset connector or binds the public asset port.
    /// </summary>
    public static class OGLAssetServiceRobustBootstrap
    {
        public static OGLAssetServiceAdminListener StartAdmin(
            IConfigSource config, IHttpServer dedicatedAdminServer,
            IAssetService loadedAssetService, string auditPath,
            long maxJournalBytes, int retainedFiles)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            IConfig section = config.Configs[OGLAssetServiceAdminHostConfiguration.Section];
            if (section == null || !section.GetBoolean("Enabled", false))
                return null;

            if (loadedAssetService is not OGLAssetConnector connector)
                throw new InvalidOperationException(
                    "Administrative endpoints require the ROBUST-loaded OGLAssetConnector.");

            // The host must pass its separately configured TLS listener.
            // Startup preflight and journal ownership belong to the listener.
            return OGLAssetServiceAdminListener.Start(config, dedicatedAdminServer,
                connector, auditPath, maxJournalBytes, retainedFiles);
        }
    }
}
