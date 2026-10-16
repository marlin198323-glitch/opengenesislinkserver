using OpenGenesisLINK.Addons.AssetService;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
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
