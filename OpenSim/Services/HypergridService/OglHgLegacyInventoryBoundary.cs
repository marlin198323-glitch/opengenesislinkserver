// OpenGenesisLINK HG InventoryGuard - explicit policy for unauthenticated
// legacy public /xinventory. This does NOT authenticate a remote avatar.
// It only makes risky operations unavailable when the operator opts in.
using System;

namespace OpenSim.Services.HypergridService
{
    public enum OglHgLegacyInventoryMode
    {
        LegacyCompatible = 0,
        ReadOnly = 1,
        BlockAll = 2
    }

    public static class OglHgLegacyInventoryBoundary
    {
        /// <summary>
        /// Invalid values fail closed. LegacyCompatible is the historical
        /// default only so existing HG grids aren't silently disconnected.
        /// </summary>
        public static OglHgLegacyInventoryMode Parse(string configured)
        {
            if (string.Equals(configured, "LegacyCompatible",
                StringComparison.OrdinalIgnoreCase))
                return OglHgLegacyInventoryMode.LegacyCompatible;
            if (string.Equals(configured, "ReadOnly",
                StringComparison.OrdinalIgnoreCase))
                return OglHgLegacyInventoryMode.ReadOnly;
            if (string.Equals(configured, "BlockAll",
                StringComparison.OrdinalIgnoreCase))
                return OglHgLegacyInventoryMode.BlockAll;
            return OglHgLegacyInventoryMode.BlockAll;
        }

        public static bool AllowRead(OglHgLegacyInventoryMode mode)
        {
            return mode != OglHgLegacyInventoryMode.BlockAll;
        }

        public static bool AllowWrite(OglHgLegacyInventoryMode mode)
        {
            return mode == OglHgLegacyInventoryMode.LegacyCompatible;
        }
    }
}
