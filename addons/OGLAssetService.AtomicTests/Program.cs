using System.Security.Cryptography;
using OpenSim.Services.FSAssetService;

static void Check(bool ok, string reason)
{
    if (!ok) throw new Exception(reason);
}

string root = Path.Combine(Path.GetTempPath(), "ogl-atomic-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    byte[] original = new byte[128 * 1024];
    RandomNumberGenerator.Fill(original);
    string hash = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
    Check(AtomicFsAssetPayload.TryPublish(root, hash, original), "Initial digest publication failed");
    string destination = Path.Combine(root, hash + ".asset");
    Check(File.ReadAllBytes(destination).SequenceEqual(original), "Published bytes differ");
    Check(AtomicFsAssetPayload.TryPublish(root, hash, original), "Idempotent digest publication failed");
    Check(!AtomicFsAssetPayload.TryPublish(root, new string('a', 64), original),
        "Digest mismatch must fail closed");
    Check(!AtomicFsAssetPayload.TryPublish(root, "../escape", original),
        "Unsafe digest path must be rejected");
    Check(!AtomicFsAssetPayload.TryPublish(root, hash, null),
        "Null content must be rejected");
    File.WriteAllBytes(destination, new byte[] { 1, 2, 3 });
    Check(!AtomicFsAssetPayload.TryPublish(root, hash, original),
        "Existing corrupt destination must never be accepted");
    Check(File.ReadAllBytes(destination).SequenceEqual(new byte[] { 1, 2, 3 }),
        "Publisher must never overwrite a preexisting digest");
    File.Delete(destination);

    // Multiple simultaneous publishers of identical content must converge
    // on one immutable destination without sharing temporary filenames.
    var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        AtomicFsAssetPayload.TryPublish(root, hash, original))).ToArray();
    await Task.WhenAll(tasks);
    Check(tasks.All(t => t.Result), "Concurrent publication must converge");
    Check(File.ReadAllBytes(destination).SequenceEqual(original),
        "Concurrent publication changed destination");
    Check(Directory.GetFiles(Path.Combine(root, "spool"), "*.asset").Length == 0,
        "Staging files must be cleaned after concurrent publication");

    // Failed staging (spool is a regular file) cannot publish an SQL-safe payload.
    string blocked = Path.Combine(root, "blocked");
    Directory.CreateDirectory(blocked);
    File.WriteAllText(Path.Combine(blocked, "spool"), "not-a-directory");
    bool failed = false;
    try { failed = !AtomicFsAssetPayload.TryPublish(blocked, hash, original); }
    catch (IOException) { failed = true; }
    Check(failed && !File.Exists(Path.Combine(blocked, hash + ".asset")),
        "Failed staging must not publish a digest");
    Console.WriteLine("Atomic FSAssets spool regression passed");
}
finally
{
    Directory.Delete(root, recursive: true);
}
