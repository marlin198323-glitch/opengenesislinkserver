using System;
using System.IO;
using System.Text.Json;
using OpenGenesisLINK.Addons.AssetService;

// Offline dual-store backfill. The source must be a consistent, quiesced
// snapshot, never a live ROBUST spool. No source file or SQL row is changed.
if (args.Length >= 7 && args[0] == "--parallel-snapshot")
{
    long expected = 0;
    bool copy = false;
    bool confirmed = false;
    bool osgrid = false;
    bool invalid = false;
    for (int i = 6; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--expected-rows":
                if (expected != 0 || ++i >= args.Length ||
                    !long.TryParse(args[i], out expected) || expected <= 0)
                    invalid = true;
                break;
            case "--copy":
                if (copy) invalid = true;
                copy = true;
                break;
            case "--offline-snapshot-confirmed":
                if (confirmed) invalid = true;
                confirmed = true;
                break;
            case "--osgrid":
                if (osgrid) invalid = true;
                osgrid = true;
                break;
            default:
                invalid = true;
                break;
        }
    }
    if (invalid || expected <= 0 || (copy && !confirmed))
    {
        Console.Error.WriteLine("Required: --expected-rows N; --copy requires --offline-snapshot-confirmed.");
        return 2;
    }
    try
    {
        var result = new OGLParallelSnapshotMigrator().Run(
            args[1], args[2], args[3], args[4], args[5], expected, copy, osgrid);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Mode = copy ? "copy-verify" : "read-only-dry-run",
            ExpectedRows = expected,
            result.Rows,
            result.Valid,
            result.Imported,
            result.VerifiedExisting,
            result.Bytes,
            result.Failed
        }, new JsonSerializerOptions { WriteIndented = true }));
        return result.Failed == 0 && result.Rows == expected ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Parallel migration failed: " + ex.GetType().Name +
            ": " + ex.Message);
        return 1;
    }
}

if (args.Length >= 3 && args[0] == "--verify-metadata")
{
    long? expectedRows = null;
    bool osgrid = false;
    bool invalidArguments = false;
    for (int i = 3; i < args.Length; i++)
    {
        if (args[i] == "--osgrid" && !osgrid) osgrid = true;
        else if (args[i] == "--expected-rows" && !expectedRows.HasValue &&
            i + 1 < args.Length && long.TryParse(args[++i], out long count) && count > 0)
            expectedRows = count;
        else invalidArguments = true;
    }
    if (invalidArguments || !expectedRows.HasValue)
    {
        Console.Error.WriteLine("Metadata audit requires --expected-rows <positive-count> [--osgrid].");
        return 2;
    }
    try
    {
        var result = OGLFsassetsMetadataAudit.Verify(args[1], File.ReadLines(args[2]), osgrid);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ExpectedRows = expectedRows.Value,
            RowCountMatches = result.Rows == expectedRows.Value,
            result.Rows,
            result.UniqueAssets,
            result.UniqueHashes,
            result.InvalidRows,
            result.DuplicateIds,
            result.ConflictingIds,
            result.ValidFiles,
            result.MissingFiles,
            result.MismatchedFiles
        }, new JsonSerializerOptions { WriteIndented = true }));
        return result.Rows == expectedRows.Value && result.UniqueAssets == expectedRows.Value &&
            result.InvalidRows == 0 && result.DuplicateIds == 0 && result.ConflictingIds == 0 &&
            result.MissingFiles == 0 && result.MismatchedFiles == 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Metadata verification failed: " + ex.GetType().Name + ": " + ex.Message);
        return 1;
    }
}

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
    Console.Error.WriteLine("   or: OGLFsassetsMigration --parallel-snapshot <offline-fsbase> <offline-spool> <uuid-hash.tsv> <ogl-object-directory> <ogl-index.sqlite> --expected-rows N [--osgrid] [--copy --offline-snapshot-confirmed]");
    Console.Error.WriteLine("   or: OGLFsassetsMigration --verify-metadata <offline-fsassets-base> <uuid-hash.tsv> --expected-rows <count> [--osgrid]");
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
