// SPDX-License-Identifier: MPL-2.0
// Real MariaDB integration: tests persistent World recovery challenge storage,
// competing ROBUST processes, TTL, replay and failure-path rollback.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using NexVerse.Server.Api;

internal static class Program
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("MariaDB World password recovery: " + message);
    }

    private static int Main()
    {
        string connection = Environment.GetEnvironmentVariable("NEX_PASSWORD_RECOVERY_MARIADB");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                "NEX_PASSWORD_RECOVERY_MARIADB is required for real SQL regression.");

        // Isolate tests by random identities. Do not delete real World accounts.
        Guid owner = Guid.NewGuid(), parallelOwner = Guid.NewGuid(), legacyOwner = Guid.NewGuid();
        long now = 1900000000;
        var store = new NexPasswordRecoveryStore(connection);

        try
        {
            string first = store.Issue(owner, now);
            Require(first != null && first.Length > 58, "no random challenge generated");

            // Check the real DB row, not a locally created SQLite file.
            using (var db = new MySqlConnection(connection))
            {
                db.Open();
                using MySqlCommand command = db.CreateCommand();
                command.CommandText = @"SELECT salt, digest FROM ogl_password_recovery
                    WHERE principal_id=@p AND state=0";
                command.Parameters.AddWithValue("@p", owner.ToString("D"));
                using MySqlDataReader rows = command.ExecuteReader();
                Require(rows.Read(), "challenge not saved to existing MariaDB");
                byte[] salt = (byte[])rows.GetValue(0);
                byte[] digest = (byte[])rows.GetValue(1);
                Require(salt.Length == 16 && digest.Length == 32,
                    "the challenge does not store a salted SHA-256 digest");
                Require(!rows.Read(), "more than one unconsumed World challenge issued");
            }

            string replacement = store.Issue(owner, now + 1);
            Require(replacement != first, "new issuance reused old secret");
            Require(!store.TryClaim(first, now + 2, out _, out _), "rotated token accepted");
            Require(!store.TryClaim(replacement + "x", now + 2, out _, out _),
                "tampered token accepted");
            Require(!store.TryClaim(replacement, now + 901, out _, out _),
                "expired token accepted");
            Require(store.TryClaim(replacement, now + 2, out Guid id, out string claim),
                "valid token rejected");
            Require(id == owner, "token not bound to correct World principal");
            Require(!store.TryClaim(replacement, now + 3, out _, out _),
                "claimed token was reused");
            Require(store.Finish(replacement, claim, false, now + 4),
                "failed password write did not release challenge");
            Require(store.TryClaim(replacement, now + 5, out _, out claim),
                "failed write did not permit re-claim");
            Require(store.Finish(replacement, claim, true, now + 5),
                "successful password update did not consume challenge");
            Require(!store.TryClaim(replacement, now + 6, out _, out _),
                "consumed token accepted again");
            Require(!store.Finish(replacement, claim, false, now + 6),
                "consumed token became pending again");
            Require(store.Issue(owner, now + 7) != null, "third issuance refused");
            Require(store.Issue(owner, now + 8) == null,
                "hourly per-account throttle bypassed");

            var restarted = new NexPasswordRecoveryStore(connection);
            Require(!restarted.TryClaim(replacement, now + 8, out _, out _),
                "claimed/consumed state did not persist across store restart");
            string contested = restarted.Issue(parallelOwner, now + 9);
            int winners = 0;
            Parallel.For(0, 8, worker =>
            {
                // Independent store instances and SQL connections, like
                // multiple ROBUST nodes sharing the same MariaDB.
                var other = new NexPasswordRecoveryStore(connection);
                if (other.TryClaim(contested, now + 10, out _, out _))
                    Interlocked.Increment(ref winners);
            });
            Require(winners == 1, "two processes claimed the same World token");

            // Account-level issuance lock must not leak when throttled.
            Require(store.Issue(owner, now + 9) == null, "rate limit bypass on retry");

            // The production ROBUST config commonly uses Old Guids=true.
            // Exercise that mode as well as the driver's Guid-return mode.
            string legacyConnection = connection.EndsWith(";", StringComparison.Ordinal)
                ? connection + "Old Guids=true;"
                : connection + ";Old Guids=true;";
            var legacy = new NexPasswordRecoveryStore(legacyConnection);
            string legacyToken = legacy.Issue(legacyOwner, now + 11);
            Require(legacy.TryClaim(legacyToken, now + 12, out Guid legacyPrincipal,
                out string legacyClaim) && legacyPrincipal == legacyOwner,
                "legacy Old Guids=true account lookup failed");
            Require(legacy.Finish(legacyToken, legacyClaim, true, now + 12),
                "legacy GUID form could not consume the token");
            Console.WriteLine("World P0 MariaDB: hashed tokens, InnoDB claim, TTL, replay, " +
                "reissue, throttle, multi-connection concurrency and restart: OK");
            return 0;
        }
        finally
        {
            using var db = new MySqlConnection(connection);
            db.Open();
            using MySqlCommand cleanup = db.CreateCommand();
            cleanup.CommandText = @"DELETE FROM ogl_password_recovery
                WHERE principal_id=@first OR principal_id=@second OR principal_id=@third";
            cleanup.Parameters.AddWithValue("@first", owner.ToString("D"));
            cleanup.Parameters.AddWithValue("@second", parallelOwner.ToString("D"));
            cleanup.Parameters.AddWithValue("@third", legacyOwner.ToString("D"));
            cleanup.ExecuteNonQuery();
        }
    }
}
