using System;
using System.IO;
using System.Text.Json;
using OpenGenesisLINK.Addons.AssetService;

if (args.Length < 2 || args.Length > 3 ||
    (args.Length == 3 && args[2] != "--copy"))
{
    Console.Error.WriteLine("Usage: OGLFsassetsMigration <offline-source> <separate-target> [--copy]");
    Console.Error.WriteLine("Default: read-only dry-run. --copy requires a stopped FSAssets writer and a DB snapshot.");
    return 2;
}

bool copy = args.Length == 3;
try
{
    var result = new OGLFsassetsMigrator().Run(args[0], args[1], !copy);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Mode = copy ? "copy-verify" : "dry-run",
        result.FilesScanned,
        result.FilesCopied,
        result.FilesVerified,
        result.BytesCopied,
        result.Failures
    }, new JsonSerializerOptions { WriteIndented = true }));
    return result.Failures == 0 ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Migration stopped: " + ex.GetType().Name + ": " + ex.Message);
    return 1;
}
