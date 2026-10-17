using System.Security.Cryptography;
using OpenSim.Services.FSAssetService;

static void Check(bool ok, string reason)
{
    if (!ok) throw new Exception(reason);
}

// A separate OS process exercises the same immutable spool publisher.
if (args.Length == 4 && args[0] == "--publish-worker")
{
    byte[] payload = File.ReadAllBytes(args[3]);
    return AtomicFsAssetPayload.TryPublish(args[1], args[2], payload) ? 0 : 3;
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

    // Real independent OS processes compete for the same digest destination.
    // Unlike Task.Run, this exercises separate runtimes and file handles.
    File.Delete(destination);
    string payloadFile = Path.Combine(root, "payload.bin");
    File.WriteAllBytes(payloadFile, original);
    string assembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
    var workers = new List<System.Diagnostics.Process>();
    try
    {
        for (int i = 0; i < 8; i++)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add("--publish-worker");
            start.ArgumentList.Add(root);
            start.ArgumentList.Add(hash);
            start.ArgumentList.Add(payloadFile);
            workers.Add(System.Diagnostics.Process.Start(start)
                ?? throw new Exception("Unable to start publisher process"));
        }
        foreach (var worker in workers)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await worker.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                worker.Kill(entireProcessTree: true);
                throw new Exception("Concurrent publisher process timed out");
            }
            Check(worker.ExitCode == 0,
                "Independent publisher failed: " + await worker.StandardError.ReadToEndAsync());
        }
    }
    finally
    {
        foreach (var worker in workers) worker.Dispose();
    }
    Check(File.ReadAllBytes(destination).SequenceEqual(original),
        "Cross-process publication produced corrupt content");
    Check(Directory.GetFiles(Path.Combine(root, "spool"), "*.asset").Length == 0,
        "Cross-process publication leaked staging files");

    // Simulate a process crash before rename: an orphaned temporary file
    // must not be mistaken for a published asset or block a later writer.
    File.Delete(destination);
    string orphan = Path.Combine(root, "spool", Guid.NewGuid().ToString("N") + ".asset");
    File.WriteAllBytes(orphan, original.AsSpan(0, 32).ToArray());
    Check(!File.Exists(destination), "Orphan staging file must not publish an asset");
    Check(AtomicFsAssetPayload.TryPublish(root, hash, original),
        "Recovery after interrupted staging must succeed");
    Check(File.ReadAllBytes(destination).SequenceEqual(original),
        "Recovered asset must match authoritative digest");
    Check(File.Exists(orphan), "Unrelated orphan must not be deleted implicitly");
    File.Delete(orphan);

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
return 0;
