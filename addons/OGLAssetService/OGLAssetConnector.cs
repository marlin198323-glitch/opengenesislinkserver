using System;
using Nini.Config;
using OpenSim.Services.FSAssetService;
using OpenSim.Framework;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Compatibility-first OGL AssetService entry point.
    /// Delegates all asset operations to the existing FSAssetConnector.
    /// No migration, relocation or deletion of existing fsassets occurs.
    /// </summary>
    public sealed class OGLAssetConnector : FSAssetConnector
    {
        private readonly OGLObjectCache cache;
        public OGLAssetConnector(IConfigSource config)
            : base(config)
        {
            IConfig settings = config.Configs["OGLAssetService"];
            if (settings == null || !settings.GetBoolean("Enabled", false))
                throw new InvalidOperationException(
                    "OGL AssetService is disabled. Set [OGLAssetService] Enabled = true only on a test instance.");

            int cacheMiB = settings.GetInt("MemoryCacheMiB", 64);
            if (cacheMiB < 0 || cacheMiB > 4096)
                throw new ArgumentOutOfRangeException("MemoryCacheMiB", "Must be between 0 and 4096 MiB.");
            cache = new OGLObjectCache((long)cacheMiB * 1024 * 1024);

            string mode = settings.GetString("Mode", "compatibility");
            if (!String.Equals(mode, "compatibility", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "OGL AssetService currently supports only Mode = compatibility. Distributed storage is not implemented.");
        }
    }
