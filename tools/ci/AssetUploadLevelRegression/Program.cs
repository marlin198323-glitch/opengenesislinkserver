// Upload Level configuration contract shared by UDP and viewer CAPS.
using System;
using Nini.Config;
using OpenSim.Framework;

internal static class Program
{
    private static int checks;

    private static void Check(int expected, IConfigSource source, string description)
    {
        checks++;
        int actual = AssetUploadLevelPolicy.Resolve(source);
        if (expected != actual)
            throw new Exception(description + ": expected " + expected + ", got " + actual);
    }

    public static int Main()
    {
        try
        {
            Check(0, null, "No configuration");
            Check(0, new IniConfigSource(), "No sections");

            IniConfigSource startupOnly = new IniConfigSource();
            startupOnly.AddConfig("Startup").Set("LevelUpload", 150);
            Check(150, startupOnly, "Legacy Startup setting");

            IniConfigSource permissionsOnly = new IniConfigSource();
            permissionsOnly.AddConfig("Permissions").Set("LevelUpload", 200);
            Check(200, permissionsOnly, "Documented Permissions setting");

            IniConfigSource permissionsWin = new IniConfigSource();
            permissionsWin.AddConfig("Startup").Set("LevelUpload", 100);
            permissionsWin.AddConfig("Permissions").Set("LevelUpload", 200);
            Check(200, permissionsWin, "Permissions overrides Startup");

            IniConfigSource explicitZero = new IniConfigSource();
            explicitZero.AddConfig("Startup").Set("LevelUpload", 200);
            explicitZero.AddConfig("Permissions").Set("LevelUpload", 0);
            Check(0, explicitZero, "Explicit Permissions zero must override Startup");

            IniConfigSource missingKey = new IniConfigSource();
            missingKey.AddConfig("Startup").Set("LevelUpload", 50);
            missingKey.AddConfig("Permissions").Set("serverside_object_permissions", true);
            Check(50, missingKey, "Empty Permissions falls back to Startup");

            IniConfigSource mesh = new IniConfigSource();
            mesh.AddConfig("Mesh").Set("LevelUpload", 250);
            Check(0, mesh, "Mesh is not a separate LevelUpload configuration section");

            Console.WriteLine("Asset upload policy: PASS (" + checks + " checks)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Asset upload policy FAIL: " + ex);
            return 1;
        }
    }
}
