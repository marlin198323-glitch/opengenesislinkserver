using System;
using System.IO;
using System.Threading.Tasks;
using NexVerse.Server.Api;

internal static class Program
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("World password recovery regression: " + message);
    }

    private static int Main()
    {
        string path = Path.Combine(Path.GetTempPath(), "ogl-recovery-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            var store = new NexPasswordRecoveryStore(path);
            Guid owner = Guid.NewGuid();
            long now = 1900000000;
            string first = store.Issue(owner, now);
            Require(first != null && first.Length > 58, "no cryptographic token generated");
            Require(File.ReadAllBytes(path).AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(first)) < 0,
                "raw challenge secret appeared in database");
            string replacement = store.Issue(owner, now + 1);
            Require(replacement != first, "issue returned a reused token");
            Require(!store.TryClaim(first, now + 2, out _, out _), "old token survived rotation");
            Require(!store.TryClaim(replacement + "x", now + 2, out _, out _),
                "altered secret was accepted");
            Require(!store.TryClaim(replacement, now + 901, out _, out _),
                "expired token accepted");
            Require(store.TryClaim(replacement, now + 2, out Guid id, out string claim),
                "valid challenge rejected");
            Require(id == owner, "token was mapped to the wrong World account");
            Require(!store.TryClaim(replacement, now + 3, out _, out _),
                "concurrent/replayed claim accepted");
            Require(store.Finish(replacement, claim, false, now + 4),
                "failed password write did not release claim");
            Require(store.TryClaim(replacement, now + 5, out _, out claim),
                "released claim could not be used");
            Require(store.Finish(replacement, claim, true, now + 5),
                "successful write did not consume claim");
            Require(!store.TryClaim(replacement, now + 6, out _, out _),
                "consumed recovery token was reused");
            Require(!store.Finish(replacement, claim, false, now + 6),
                "completed claim was reopened");
            Require(store.Issue(owner, now + 7) != null, "third issuance refused");
            Require(store.Issue(owner, now + 8) == null, "issuance rate limit bypassed");

            var other = new NexPasswordRecoveryStore(path);
            Require(!other.TryClaim(replacement, now + 8, out _, out _),
                "one-time state was not persisted across store restart");
            string concurrent = other.Issue(Guid.NewGuid(), now + 9);
            int wins = 0;
            Parallel.For(0, 6, _ =>
            {
                if (new NexPasswordRecoveryStore(path).TryClaim(concurrent, now + 10,
                    out _, out _))
                    System.Threading.Interlocked.Increment(ref wins);
            });
            Require(wins == 1, "two parallel requests claimed the same challenge");
            Console.WriteLine("World P0 recovery: rotation, hashing, TTL, replay, persistent claims and concurrency OK");
            return 0;
        }
        finally
        {
            foreach (string candidate in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
                try { File.Delete(candidate); } catch (IOException) { }
        }
    }
}
