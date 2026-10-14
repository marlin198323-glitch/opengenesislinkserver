/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the original license notices
 * are retained. See CONTRIBUTORS.TXT.
 */

using Nini.Config;

namespace OpenSim.Framework
{
    /// <summary>
    /// Reads the simulator's minimum user level for permanent asset uploads.
    /// [Permissions] is the documented owner of LevelUpload. Keep [Startup]
    /// as a backwards-compatible fallback for installations using older INIs.
    /// An explicit zero in [Permissions] MUST override an older nonzero value.
    /// </summary>
    public static class AssetUploadLevelPolicy
    {
        public static int Resolve(IConfigSource config)
        {
            if (config == null)
                return 0;

            IConfig permissions = config.Configs["Permissions"];
            if (permissions != null && permissions.Contains("LevelUpload"))
                return permissions.GetInt("LevelUpload", 0);

            IConfig startup = config.Configs["Startup"];
            return startup != null ? startup.GetInt("LevelUpload", 0) : 0;
        }
    }
}
