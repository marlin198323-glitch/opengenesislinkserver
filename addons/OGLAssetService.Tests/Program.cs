using OpenGenesisLINK.Addons.AssetService;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// Opt-in Redis integration smoke test: uses a unique asset ID and does not flush Redis.
if (args.Contains("--redis-smoke", StringComparer.OrdinalIgnoreCase))
{
    string connection = Environment.GetEnvironmentVariable("OGL_ASSET_REDIS_CONNECTION")
        ?? throw new InvalidOperationException("OGL_ASSET_REDIS_CONNECTION is required");
    int database = int.TryParse(Environment.GetEnvironmentVariable("OGL_ASSET_REDIS_DATABASE"), out int parsed)
        ? parsed : 2;
    string assetId = "ogl-smoke-" + Guid.NewGuid().ToString("N");
    using var coordinator = new OGLRedisAssetCoordinator(connection, database);
    long initial = await coordinator.GetRevisionAsync(assetId);
    Check(initial == 0, "New smoke-test ID must have revision zero");
    long incremented = await coordinator.InvalidateAsync(assetId);
    Check(incremented == 1, "First revision increment must be one");
    Check(await coordinator.GetRevisionAsync(assetId) == 1, "Revision readback failed");
    // Verify a second independent Redis connection sees the same revision.
    using (var secondCoordinator = new OGLRedisAssetCoordinator(connection, database))
    {
        Check(await secondCoordinator.GetRevisionAsync(assetId) == 1,
            "Revision must be visible across Redis clients");
        Check(await secondCoordinator.InvalidateAsync(assetId) == 2,
            "Second client revision increment failed");
    }
    Check(await coordinator.GetRevisionAsync(assetId) == 2,
        "Original client must observe the second client's invalidation");
    byte[] payloadSample = System.Text.Encoding.UTF8.GetBytes("ogl-redis-payload-" + assetId);
    string contentHash = await coordinator.PutPayloadAsync(payloadSample, TimeSpan.FromMinutes(2));
    Check((await secondPayloadReader()).SequenceEqual(payloadSample),
        "Content-addressed payload must round-trip between connections");
    Check(await coordinator.GetPayloadAsync(new string('a', 64)) == null,
        "Unknown content hash must miss");
    Check(coordinator.PayloadMisses == 1, "Redis payload miss counter");
    Check(await coordinator.GetPayloadAsync(contentHash) is { Length: > 0 },
        "Redis payload hit must return bytes");
    Check(coordinator.PayloadHits == 1, "Redis payload hit counter");
    Check(coordinator.PayloadIntegrityFailures == 0, "No integrity failures expected");
    // A stale publisher must not reintroduce a mapping after invalidation.
    Check(await coordinator.TryPublishMappingAsync(assetId, 2, contentHash,
        TimeSpan.FromMinutes(2)), "Current revision mapping publication");
    Check(await coordinator.GetMappedHashAsync(assetId, 2) == contentHash,
        "Current revision mapping lookup");
    using (var invalidator = new OGLRedisAssetCoordinator(connection, database))
        Check(await invalidator.InvalidateAsync(assetId) == 3,
            "Third revision increment failed");
    Check(!await coordinator.TryPublishMappingAsync(assetId, 2, contentHash,
        TimeSpan.FromMinutes(2)), "Stale mapping publication must be rejected");
    Check(await coordinator.GetMappedHashAsync(assetId, 3) == null,
        "New revision must not resolve stale mapping");
    Check(await coordinator.TryGetMappedPayloadAsync(assetId) == null,
        "Revision change must invalidate mapped payload read");

    Check(await coordinator.TryPublishMappingAsync(assetId, 3, contentHash,
        TimeSpan.FromMinutes(2)), "New revision mapping publication");
    Check((await coordinator.TryGetMappedPayloadAsync(assetId))?.SequenceEqual(payloadSample) == true,
        "Stable revision must return integrity-checked mapped payload");

    string readThroughId = assetId + "-read-through";
    int authoritativeReads = 0;
    byte[] authoritativeBytes = System.Text.Encoding.UTF8.GetBytes("authoritative-" + assetId);
    byte[] ReadAuthoritative()
    {
        authoritativeReads++;
        return (byte[])authoritativeBytes.Clone();
    }
    Check((await coordinator.ReadThroughAsync(readThroughId, ReadAuthoritative,
        TimeSpan.FromMinutes(2))).SequenceEqual(authoritativeBytes),
        "Read-through must return authoritative bytes on first miss");
    Check(authoritativeReads == 1, "First read-through must consult authoritative storage");
    Check((await coordinator.ReadThroughAsync(readThroughId, ReadAuthoritative,
        TimeSpan.FromMinutes(2))).SequenceEqual(authoritativeBytes),
        "Second read-through must return cached bytes");
    Check(authoritativeReads == 1, "Second read-through should hit Redis");
    using (var writer = new OGLRedisAssetCoordinator(connection, database))
        await writer.InvalidateAsync(readThroughId);
    Check((await coordinator.ReadThroughAsync(readThroughId, ReadAuthoritative,
        TimeSpan.FromMinutes(2))).SequenceEqual(authoritativeBytes),
        "Invalidated read-through must return authoritative bytes");
    Check(authoritativeReads == 2, "Invalidated read-through must re-fetch authoritative bytes");
    // Deterministic write during an authoritative read: never publish a
    // mapping for the old revision, even if the read returns bytes.
    string racingId = assetId + "-racing-read";
    byte[] RaceRead()
    {
        using var concurrentWriter = new OGLRedisAssetCoordinator(connection, database);
        concurrentWriter.InvalidateAsync(racingId).GetAwaiter().GetResult();
        return (byte[])authoritativeBytes.Clone();
    }
    Check((await coordinator.ReadThroughAsync(racingId, RaceRead,
        TimeSpan.FromMinutes(2))).SequenceEqual(authoritativeBytes),
        "Racing authoritative read should still return its bytes");
    Check(await coordinator.GetRevisionAsync(racingId) == 1,
        "Concurrent writer must increment revision during authoritative read");
    Check(await coordinator.GetMappedHashAsync(racingId, 0) == null &&
        await coordinator.GetMappedHashAsync(racingId, 1) == null,
        "Concurrent write must prevent publishing an old payload mapping");
    bool badHashRejected = false;
    try { await coordinator.GetPayloadAsync("not-a-sha256"); }
    catch (ArgumentException) { badHashRejected = true; }
    Check(badHashRejected, "Malformed content hashes must be rejected");
    bool oversizedRejected = false;
    try { await coordinator.PutPayloadAsync(new byte[8 * 1024 * 1024 + 1], TimeSpan.FromSeconds(1)); }
    catch (ArgumentOutOfRangeException) { oversizedRejected = true; }
    Check(oversizedRejected, "Oversized Redis payloads must be rejected");
    async Task<byte[]> secondPayloadReader()
    {
        using var reader = new OGLRedisAssetCoordinator(connection, database);
        return await reader.GetPayloadAsync(contentHash);
    }
    bool excessiveTtlRejected = false;
    try { await coordinator.PutPayloadAsync(payloadSample, TimeSpan.FromDays(2)); }
    catch (ArgumentOutOfRangeException) { excessiveTtlRejected = true; }
    Check(excessiveTtlRejected, "Excessive Redis payload TTL must be rejected");
    bool invalidMappingTtlRejected = false;
    try { await coordinator.TryPublishMappingAsync(assetId, 3, contentHash, TimeSpan.Zero); }
    catch (ArgumentOutOfRangeException) { invalidMappingTtlRejected = true; }
    Check(invalidMappingTtlRejected, "Invalid mapping TTL must be rejected");
    // Corrupt only a random smoke-test revision key; never touch production IDs.
    string revisionKey = "ogl:asset:v1:" + Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(assetId)))
        .ToLowerInvariant();
    using (var raw = StackExchange.Redis.ConnectionMultiplexer.Connect(connection))
    {
        await raw.GetDatabase(database).StringSetAsync(revisionKey, "invalid-revision");
        bool invalidRevisionRejected = false;
        try { await coordinator.GetRevisionAsync(assetId); }
        catch (InvalidOperationException) { invalidRevisionRejected = true; }
        Check(invalidRevisionRejected, "Malformed Redis revision must fail closed");
        await raw.GetDatabase(database).KeyDeleteAsync(revisionKey);
    }
    Console.WriteLine("Redis revision smoke test passed (DB " + database + ")");
    Console.WriteLine("Note: smoke-test revision key was deleted; short-lived payload and mapping keys expire by TTL.");
    return;
}

// Opt-in microbenchmark: measures the cache implementation, NOT end-to-end ROBUST/FSAssets.
// Keep the smoke tests as the default execution path.
if (args.Contains("--benchmark", StringComparer.OrdinalIgnoreCase))
{
    const int iterations = 100_000;
    const int payloadBytes = 64 * 1024;
    byte[] sample = new byte[payloadBytes];
    new Random(42).NextBytes(sample);
    var benchmarkCache = new OGLObjectCache(16L * 1024 * 1024);
    benchmarkCache.Put("benchmark-asset", sample);
    // Warm-up JIT and cache before measuring.
    for (int i = 0; i < 1000; i++)
        Check(benchmarkCache.TryGet("benchmark-asset", out _), "Warm-up cache miss");
    var timer = System.Diagnostics.Stopwatch.StartNew();
    long bytesRead = 0;
    for (int i = 0; i < iterations; i++)
    {
        Check(benchmarkCache.TryGet("benchmark-asset", out var bytes), "Unexpected cache miss");
        bytesRead += bytes.Length;
    }
    timer.Stop();
    double elapsedSeconds = Math.Max(timer.Elapsed.TotalSeconds, 0.000001);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        benchmark = "OGLObjectCache warm-hit microbenchmark",
        iterations,
        payloadBytes,
        elapsedMs = timer.Elapsed.TotalMilliseconds,
        operationsPerSecond = iterations / elapsedSeconds,
        throughputMiBPerSecond = bytesRead / 1048576.0 / elapsedSeconds,
        hits = benchmarkCache.Hits,
        misses = benchmarkCache.Misses,
        note = "Not a legacy FSAssetConnector comparison or end-to-end asset latency measurement"
    }));
    return;
}

var memory = new OGLObjectCache(5);
var original = new byte[] { 1, 2, 3 };
memory.Put("first", original);
original[0] = 99;
Check(memory.TryGet("first", out var hit) && hit[0] == 1, "Insertion must clone payload");
hit[0] = 99;
Check(memory.TryGet("first", out hit) && hit[0] == 1, "Read must clone payload");
memory.Put("second", new byte[] { 4, 5, 6 });
Check(!memory.TryGet("first", out _), "LRU must evict first entry");
Check(memory.UsedBytes == 3 && memory.Count == 1, "Memory accounting");
memory.Invalidate("second");
Check(memory.Count == 0, "Invalidation");
// Replacing a cache key must not retain an obsolete snapshot or double-count memory.
memory.Put("replacement", new byte[] { 1, 2 });
memory.Put("replacement", new byte[] { 3, 4, 5 });
Check(memory.Count == 1 && memory.UsedBytes == 3, "Replacement memory accounting");
Check(memory.TryGet("replacement", out var replacement) &&
    replacement.SequenceEqual(new byte[] { 3, 4, 5 }), "Replacement must return new bytes");
memory.Invalidate("replacement");
Check(!memory.TryGet("replacement", out _), "Invalidated replacement must miss");
// Concurrent read/write/invalidate must not corrupt LRU or accounting.
var concurrent = new OGLObjectCache(1024);
System.Threading.Tasks.Parallel.For(0, 2000, i =>
{
    string key = "asset-" + (i % 16);
    if (i % 3 == 0) concurrent.Put(key, new byte[32]);
    else if (i % 3 == 1) concurrent.TryGet(key, out _);
    else concurrent.Invalidate(key);
});
Check(concurrent.UsedBytes >= 0 && concurrent.UsedBytes <= 1024 &&
    concurrent.Count <= 16, "Concurrent cache accounting");
Check(OGLObjectCache.ContentHash(new byte[] { 97, 98, 99 }) ==
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA-256");

string root = Path.Combine(Path.GetTempPath(), "ogl-cache-test-" + Guid.NewGuid().ToString("N"));
try
{
    var disk = new OGLDiskCache(root);
    disk.Put("asset-uuid", new byte[] { 9, 8, 7 });
    Check(disk.TryGet("asset-uuid", out var payload) && payload.SequenceEqual(new byte[] { 9, 8, 7 }), "Disk roundtrip");
    // A fresh ROBUST process must not trust SSD cache files created before
    // it started, even if the payload checksum is still valid.
    var afterRestart = new OGLDiskCache(root);
    Check(!afterRestart.TryGet("asset-uuid", out _),
        "Disk cache must miss on restart rather than serving old UUID contents");
    Check(disk.TryGet("asset-uuid", out payload) && payload.SequenceEqual(new byte[] { 9, 8, 7 }),
        "Original instance must retain its own cache entries");
    var file = Directory.GetFiles(root, "*.cache", SearchOption.AllDirectories).Single();
    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write))
    {
        stream.Position = 32;
        stream.WriteByte(0);
    }
    Check(!disk.TryGet("asset-uuid", out _), "Corrupted cache must be rejected");
    disk.Put("asset-uuid", new byte[] { 3, 2, 1 });
    disk.Invalidate("asset-uuid");
    Check(!disk.TryGet("asset-uuid", out _), "Disk invalidation");
    var bounded = new OGLDiskCache(root, 70);
    bounded.Put("budget-a", new byte[20]);
    System.Threading.Thread.Sleep(1100);
    bounded.Put("budget-b", new byte[20]);
    Check(Directory.GetFiles(root, "*.cache", SearchOption.AllDirectories)
        .Sum(p => new FileInfo(p).Length) <= 70, "Disk quota must be enforced");
    Check(bounded.Evictions >= 1, "Disk quota must evict entries");
    Check(!bounded.TryGet("budget-a", out _), "Oldest entry should be evicted");
    Check(bounded.TryGet("budget-b", out _), "Newest entry should remain");
    Check(bounded.Hits >= 1 && bounded.Misses >= 1, "Disk hit/miss counters");
    bounded.Put("oversize", new byte[100]);
    Check(!bounded.TryGet("oversize", out _), "Oversize payload must be skipped");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
Console.WriteLine("OGL cache smoke tests passed");

string migrationRoot = Path.Combine(Path.GetTempPath(), "ogl-migration-test-" + Guid.NewGuid().ToString("N"));
try
{
    string source = Path.Combine(migrationRoot, "source");
    string target = Path.Combine(migrationRoot, "target");
    Directory.CreateDirectory(Path.Combine(source, "ab"));
    File.WriteAllBytes(Path.Combine(source, "ab", "asset.bin"), new byte[] { 1, 2, 3 });
    var migrator = new OGLFsassetsMigrator();
    var preview = migrator.Run(source, target, true);
    Check(preview.FilesScanned == 1 && !Directory.Exists(target), "Dry run must not write");
    var migrated = migrator.Run(source, target, false);
    Check(migrated.FilesCopied == 1 && migrated.FilesVerified == 1 && migrated.Failures == 0, "Verified copy");
    Check(File.ReadAllBytes(Path.Combine(target, "ab", "asset.bin")).SequenceEqual(new byte[] { 1, 2, 3 }), "Payload preserved");
    var resumed = migrator.Run(source, target, false);
    Check(resumed.FilesCopied == 0 && resumed.FilesVerified == 1, "Resumable verified copy");
    File.WriteAllBytes(Path.Combine(target, "ab", "asset.bin"), new byte[] { 9, 9, 9 });
    var repaired = migrator.Run(source, target, false);
    Check(repaired.FilesCopied == 1 && repaired.FilesVerified == 1, "Corrupt destination repaired");
    Check(File.ReadAllBytes(Path.Combine(source, "ab", "asset.bin")).SequenceEqual(new byte[] { 1, 2, 3 }), "Source remains untouched");
    bool overlapRejected = false;
    try { migrator.Run(source, Path.Combine(source, "nested"), true); }
    catch (InvalidOperationException) { overlapRejected = true; }
    Check(overlapRejected, "Overlapping migration directories rejected");
}
finally { if (Directory.Exists(migrationRoot)) Directory.Delete(migrationRoot, true); }

// Parallel OGL writes are a second immutable copy; the SQL hash remains
// authoritative. Test live write/read, invalid mappings, integrity, restart,
// and offline backfill from regular, gzip and spool snapshot payloads.
string parallelRoot = Path.Combine(Path.GetTempPath(), "ogl-parallel-" + Guid.NewGuid().ToString("N"));
try
{
    string objects = Path.Combine(parallelRoot, "ogl-objects");
    string index = Path.Combine(parallelRoot, "ogl-index", "locations.sqlite");
    var parallel = new OGLParallelAssetStore(objects, index);
    string id = Guid.NewGuid().ToString("D");
    byte[] initialParallelPayload = new byte[] { 17, 19, 23 };
    string originalHash = OGLObjectCache.ContentHash(initialParallelPayload);
    Check(parallel.Mirror(id, originalHash, initialParallelPayload), "OGL parallel mirror");
    Check(parallel.TryRead(id, originalHash)?.SequenceEqual(initialParallelPayload) == true,
        "Parallel read uses immutable OGL copy");
    Check(parallel.TryRead(id, new string('a', 64)) == null,
        "Changed authoritative SQL hash must invalidate old OGL mapping");
    var restarted = new OGLParallelAssetStore(objects, index);
    Check(restarted.TryRead(id, originalHash)?.SequenceEqual(initialParallelPayload) == true,
        "Parallel index and immutable payload survive process restart");
    byte[] replacementParallelPayload = new byte[] { 42, 41, 40 };
    string replacementHash = OGLObjectCache.ContentHash(replacementParallelPayload);
    Check(restarted.Mirror(id, replacementHash, replacementParallelPayload),
        "A changed UUID can point to a new immutable payload");
    Check(restarted.TryRead(id, originalHash) == null &&
        restarted.TryRead(id, replacementHash)?.SequenceEqual(replacementParallelPayload) == true,
        "Mapping update must not expose a stale blob");
    Check(!restarted.Mirror(id, replacementHash, initialParallelPayload),
        "Nonmatching SQL digest must reject a mirror attempt");

    string snapshotFs = Path.Combine(parallelRoot, "snapshot", "fsassets");
    string snapshotSpool = Path.Combine(parallelRoot, "snapshot", "spool");
    Directory.CreateDirectory(snapshotFs);
    Directory.CreateDirectory(snapshotSpool);
    string snapshotIndex = Path.Combine(parallelRoot, "snapshot-import", "locations.sqlite");
    string snapshotObjects = Path.Combine(parallelRoot, "snapshot-import", "objects");
    string tsv = Path.Combine(parallelRoot, "snapshot", "sql-export.tsv");
    var rows = new System.Collections.Generic.List<string>();
    byte[][] assets = { new byte[] { 1, 3, 5 }, new byte[] { 2, 4, 6 }, new byte[] { 7, 8, 9 } };
    var importedIds = new System.Collections.Generic.List<string>();
    for (int i = 0; i < assets.Length; i++)
    {
        string uuid = Guid.NewGuid().ToString("D");
        string digest = OGLObjectCache.ContentHash(assets[i]);
        // Production FSAssets often stores uppercase digest filenames.
        string sourceHash = i == 1 ? digest.ToUpperInvariant() : digest;
        importedIds.Add(uuid);
        rows.Add(uuid + "\t" + sourceHash);
        string shard = Path.Combine(snapshotFs, sourceHash[..2], sourceHash.Substring(2, 2),
            sourceHash.Substring(4, 2), sourceHash.Substring(6, 4));
        if (i < 2)
        {
            Directory.CreateDirectory(shard);
            if (i == 0) File.WriteAllBytes(Path.Combine(shard, sourceHash), assets[i]);
            else
            {
                using var compressedFile = File.Create(Path.Combine(shard, sourceHash + ".gz"));
                using var gzip = new System.IO.Compression.GZipStream(compressedFile,
                    System.IO.Compression.CompressionLevel.Optimal);
                gzip.Write(assets[i]);
            }
        }
        else File.WriteAllBytes(Path.Combine(snapshotSpool, sourceHash + ".asset"), assets[i]);
    }
    File.WriteAllLines(tsv, rows);
    var importer = new OGLParallelSnapshotMigrator();
    var plan = importer.Run(snapshotFs, snapshotSpool, tsv, snapshotObjects,
        snapshotIndex, 3, false);
    Check(plan.Valid == 3 && plan.Failed == 0 && !Directory.Exists(snapshotObjects),
        "Snapshot dry-run must validate all assets without creating OGL files");
    var first = importer.Run(snapshotFs, snapshotSpool, tsv, snapshotObjects,
        snapshotIndex, 3, true);
    Check(first.Imported == 3 && first.Failed == 0, "All snapshot formats migrate");
    var resumed = importer.Run(snapshotFs, snapshotSpool, tsv, snapshotObjects,
        snapshotIndex, 3, true);
    Check(resumed.Imported == 0 && resumed.VerifiedExisting == 3,
        "Migration must resume idempotently without replacing immutable payloads");
    var imported = new OGLParallelAssetStore(snapshotObjects, snapshotIndex);
    for (int i = 0; i < assets.Length; i++)
        Check(imported.TryRead(importedIds[i], OGLObjectCache.ContentHash(assets[i]))
            ?.SequenceEqual(assets[i]) == true, "Migrated UUID and payload parity");
    bool overlapDenied = false;
    try
    {
        importer.Run(snapshotFs, snapshotSpool, tsv,
            Path.Combine(snapshotFs, "dangerous"), snapshotIndex, 3, true);
    }
    catch (InvalidOperationException) { overlapDenied = true; }
    Check(overlapDenied, "Source and destination overlap must be rejected");
    bool countDenied = false;
    try { importer.Run(snapshotFs, snapshotSpool, tsv, snapshotObjects, snapshotIndex, 2, false); }
    catch (InvalidDataException) { countDenied = true; }
    Check(countDenied, "Incomplete or unexpected SQL snapshot row count must fail");

    // A malformed export must be refused before copy creates any OGL object,
    // shard directory or SQLite index, even when the first row was valid.
    string invalidTsv = Path.Combine(parallelRoot, "snapshot", "invalid-export.tsv");
    File.WriteAllLines(invalidTsv, new[] {
        rows[0],
        importedIds[1] + "\t" + new string('0', 64),
        rows[2]
    });
    string blockedObjects = Path.Combine(parallelRoot, "blocked-import", "objects");
    string blockedIndex = Path.Combine(parallelRoot, "blocked-import", "locations.sqlite");
    bool corruptExportRejected = false;
    try
    {
        importer.Run(snapshotFs, snapshotSpool, invalidTsv, blockedObjects,
            blockedIndex, 3, true);
    }
    catch (InvalidDataException) { corruptExportRejected = true; }
    Check(corruptExportRejected && !Directory.Exists(blockedObjects) &&
        !File.Exists(blockedIndex), "Corrupt snapshot created output before preflight");

    string duplicateTsv = Path.Combine(parallelRoot, "snapshot", "duplicate-export.tsv");
    File.WriteAllLines(duplicateTsv, new[] { rows[0], rows[0], rows[2] });
    bool duplicateExportRejected = false;
    try
    {
        importer.Run(snapshotFs, snapshotSpool, duplicateTsv, blockedObjects,
            blockedIndex, 3, true);
    }
    catch (InvalidDataException) { duplicateExportRejected = true; }
    Check(duplicateExportRejected && !Directory.Exists(blockedObjects),
        "Duplicate UUID export was accepted or created output");
}
finally { if (Directory.Exists(parallelRoot)) Directory.Delete(parallelRoot, true); }
Console.WriteLine("OGL parallel write-through and snapshot migration tests passed");

// Regression: a symlink at the first content shard must not redirect reads or
// writes outside the immutable OGL object directory.
string shardSecurityRoot = Path.Combine(Path.GetTempPath(),
    "ogl-shard-security-" + Guid.NewGuid().ToString("N"));
string symlinkPath = null;
try
{
    string objectRoot = Path.Combine(shardSecurityRoot, "objects");
    string outsideRoot = Path.Combine(shardSecurityRoot, "outside");
    var objectStore = new OGLNodeObjectStore(objectRoot);
    byte[] bytes = { 33, 55, 77 };
    string digest = OGLObjectCache.ContentHash(bytes);
    string outsideShard = Path.Combine(outsideRoot, digest.Substring(2, 2));
    Directory.CreateDirectory(outsideShard);
    File.WriteAllBytes(Path.Combine(outsideShard, digest), bytes);
    symlinkPath = Path.Combine(objectRoot, digest[..2]);
    Directory.CreateSymbolicLink(symlinkPath, outsideRoot);
    bool readDenied = false, writeDenied = false;
    try { objectStore.Get(digest); }
    catch (IOException) { readDenied = true; }
    try { objectStore.Put(bytes); }
    catch (IOException) { writeDenied = true; }
    Check(readDenied && writeDenied, "Symlinked first-level content shard accepted");
    Check(File.ReadAllBytes(Path.Combine(outsideShard, digest)).SequenceEqual(bytes),
        "Symlink test changed external payload");
}
finally
{
    if (symlinkPath != null && Directory.Exists(symlinkPath))
        Directory.Delete(symlinkPath);
    if (Directory.Exists(shardSecurityRoot))
        Directory.Delete(shardSecurityRoot, true);
}
Console.WriteLine("OGL storage symlink shard rejection tests passed");

string auditRoot = Path.Combine(Path.GetTempPath(), "ogl-audit-" + Guid.NewGuid().ToString("N"));
try
{
    byte[] content = new byte[] { 11, 22, 33 };
    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
    string folder = Path.Combine(auditRoot, hash[..2], hash.Substring(2, 2), hash.Substring(4, 2), hash.Substring(6, 4));
    Directory.CreateDirectory(folder);
    File.WriteAllBytes(Path.Combine(folder, hash), content);
    var valid = OGLFsassetsHashAudit.Verify(auditRoot, new[] { hash });
    Check(valid.Valid == 1 && valid.Checked == 1, "Audit validates SHA256 payload");
    File.WriteAllBytes(Path.Combine(folder, hash), new byte[] { 0 });
    var corrupt = OGLFsassetsHashAudit.Verify(auditRoot, new[] { hash });
    Check(corrupt.Mismatched == 1, "Audit detects corrupt payload");
    File.Delete(Path.Combine(folder, hash));
    var missing = OGLFsassetsHashAudit.Verify(auditRoot, new[] { hash, "bad" });
    Check(missing.Missing == 1 && missing.Invalid == 1, "Audit detects missing and invalid hashes");
}
finally { if (Directory.Exists(auditRoot)) Directory.Delete(auditRoot, true); }

string metadataRoot = Path.Combine(Path.GetTempPath(), "ogl-metadata-" + Guid.NewGuid().ToString("N"));
try
{
    byte[] payload = new byte[] { 3, 7, 11 };
    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    string directory = Path.Combine(metadataRoot, hash[..2], hash.Substring(2, 2), hash.Substring(4, 2), hash.Substring(6, 4));
    Directory.CreateDirectory(directory);
    File.WriteAllBytes(Path.Combine(directory, hash), payload);
    Guid id = Guid.NewGuid();
    var ok = OGLFsassetsMetadataAudit.Verify(metadataRoot, new[] { id + "\t" + hash });
    Check(ok.UniqueAssets == 1 && ok.ValidFiles == 1, "Metadata UUID-to-hash audit");
    var conflict = OGLFsassetsMetadataAudit.Verify(metadataRoot,
        new[] { id + "\t" + hash, id + "\t" + new string('a', 64), "bad-row" });
    Check(conflict.ConflictingIds == 1 && conflict.DuplicateIds == 1 && conflict.InvalidRows == 1, "Metadata conflicts and invalid rows");
    var repeated = OGLFsassetsMetadataAudit.Verify(metadataRoot, new[] { id + "\t" + hash, id + "\t" + hash });
    Check(repeated.DuplicateIds == 1 && repeated.UniqueAssets == 1, "Duplicate identical UUID must be detected");
    var blank = OGLFsassetsMetadataAudit.Verify(metadataRoot, new[] { "" });
    Check(blank.Rows == 1 && blank.InvalidRows == 1, "Blank export row must be rejected");
}
finally { if (Directory.Exists(metadataRoot)) Directory.Delete(metadataRoot, true); }

string linkAuditRoot = Path.Combine(Path.GetTempPath(), "ogl-link-audit-" + Guid.NewGuid().ToString("N"));
try
{
    byte[] payload = new byte[] { 5, 10, 15 };
    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
    string dir = Path.Combine(linkAuditRoot, hash[..2], hash.Substring(2, 2), hash.Substring(4, 2), hash.Substring(6, 4));
    Directory.CreateDirectory(dir);
    string outside = Path.Combine(linkAuditRoot, "outside-payload");
    File.WriteAllBytes(outside, payload);
    File.CreateSymbolicLink(Path.Combine(dir, hash), outside);
    var linked = OGLFsassetsHashAudit.Verify(linkAuditRoot, new[] { hash });
    Check(linked.Mismatched == 1 && linked.Valid == 0, "Audit must reject symlinked payloads");
}
finally { if (Directory.Exists(linkAuditRoot)) Directory.Delete(linkAuditRoot, true); }
