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
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
Console.WriteLine("OGL cache smoke tests passed");
