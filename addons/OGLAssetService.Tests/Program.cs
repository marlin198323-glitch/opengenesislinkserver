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
    async Task<byte[]> secondPayloadReader()
    {
        using var reader = new OGLRedisAssetCoordinator(connection, database);
        return await reader.GetPayloadAsync(contentHash);
    }
    Console.WriteLine("Redis revision smoke test passed (DB " + database + ")");
    Console.WriteLine("Note: one uniquely named revision key remains in Redis with value 2; no production asset IDs touched.");
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
