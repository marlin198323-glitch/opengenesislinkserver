using System;
using System.IO;
using System.Text.Json;
using OpenGenesisLINK.Addons.AssetService;

if (args.Length >= 3 && args[0] == "--verify-hashes" &&
    (args.Length == 3 || (args.Length == 4 && args[3] == "--osgrid")))
{
    try
    {
        var result = OGLFsassetsHashAudit.Verify(args[1], File.ReadLines(args[2]), args.Length == 4);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result.Missing == 0 && result.Mismatched == 0 && result.Invalid == 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Hash verification failed: " + ex.GetType().Name + ": " + ex.Message);
        return 1;
    }
}

if (args.Length < 2 || args.Length > 3 ||
    (args.Length == 3 && args[2] != "--copy"))
{
    Console.Error.WriteLine("Usage: OGLFsassetsMigration <offline-source> <separate-target> [--copy]");
    Console.Error.WriteLine("   or: OGLFsassetsMigration --verify-hashes <offline-fsassets-base> <hash-list.txt> [--osgrid]");
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
