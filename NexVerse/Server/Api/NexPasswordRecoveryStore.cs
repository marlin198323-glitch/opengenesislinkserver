// SPDX-License-Identifier: MPL-2.0
// World-owned one-time recovery challenges. The portal database is never trusted
// as an authentication source. Tokens are never persisted in plaintext.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Mono.Data.Sqlite;

namespace NexVerse.Server.Api
{
    internal sealed class NexPasswordRecoveryStore
    {
        private readonly string m_ConnectionString;
        private const int TtlSeconds = 900;
        private static readonly char[] s_separator = { '.' };

        internal NexPasswordRecoveryStore(string databasePath)
        {
            string full = Path.GetFullPath(databasePath ??
                throw new ArgumentNullException(nameof(databasePath)));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            m_ConnectionString = "Data Source=" + full + ";Version=3;Pooling=False;";
            using var db = Open();
            using var command = db.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_password_recovery (
                challenge_id TEXT PRIMARY KEY,
                principal_id TEXT NOT NULL,
                salt BLOB NOT NULL,
                digest BLOB NOT NULL,
                issued_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                state INTEGER NOT NULL DEFAULT 0,
                claim_id TEXT,
                claimed_at INTEGER
            );
            CREATE INDEX IF NOT EXISTS ix_ogl_recovery_account
            ON ogl_password_recovery(principal_id, issued_at);
            CREATE INDEX IF NOT EXISTS ix_ogl_recovery_expiry
            ON ogl_password_recovery(expires_at);";
            command.ExecuteNonQuery();
        }

        private SqliteConnection Open()
        {
            var db = new SqliteConnection(m_ConnectionString);
            db.Open();
            using var timeout = db.CreateCommand();
            timeout.CommandText = "PRAGMA busy_timeout=5000;";
            timeout.ExecuteNonQuery();
            return db;
        }

        private static byte[] Digest(byte[] salt, string secret)
        {
            byte[] input = Encoding.UTF8.GetBytes(secret);
            byte[] combined = new byte[salt.Length + input.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(input, 0, combined, salt.Length, input.Length);
            byte[] digest = SHA256.HashData(combined);
            CryptographicOperations.ZeroMemory(combined);
            return digest;
        }

        // Null means throttled; do not disclose the reason to a public caller.
        internal string Issue(Guid principal, long now)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            using (var limit = db.CreateCommand())
            {
                limit.Transaction = tx;
                limit.CommandText = @"SELECT COUNT(*) FROM ogl_password_recovery
                    WHERE principal_id=@p AND issued_at>=@window";
                limit.Parameters.AddWithValue("@p", principal.ToString("D"));
                limit.Parameters.AddWithValue("@window", now - 3600);
                if (Convert.ToInt32(limit.ExecuteScalar()) >= 3)
                    return null;
            }

            string challengeId = Guid.NewGuid().ToString("N");
            string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string token = challengeId + "." + secret;
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] digest = Digest(salt, token);
            using (var invalidate = db.CreateCommand())
            {
                invalidate.Transaction = tx;
                invalidate.CommandText = @"UPDATE ogl_password_recovery SET state=2
                    WHERE principal_id=@p AND state=0";
                invalidate.Parameters.AddWithValue("@p", principal.ToString("D"));
                invalidate.ExecuteNonQuery();
            }
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO ogl_password_recovery
                    (challenge_id, principal_id, salt, digest, issued_at, expires_at, state)
                    VALUES (@id, @p, @salt, @digest, @now, @expires, 0)";
                cmd.Parameters.AddWithValue("@id", challengeId);
                cmd.Parameters.AddWithValue("@p", principal.ToString("D"));
                cmd.Parameters.AddWithValue("@salt", salt);
                cmd.Parameters.AddWithValue("@digest", digest);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@expires", now + TtlSeconds);
                cmd.ExecuteNonQuery();
            }
            using (var cleanup = db.CreateCommand())
            {
                cleanup.Transaction = tx;
                cleanup.CommandText = "DELETE FROM ogl_password_recovery WHERE expires_at<@cutoff";
                cleanup.Parameters.AddWithValue("@cutoff", now - 86400);
                cleanup.ExecuteNonQuery();
            }
            tx.Commit();
            return token;
        }

        // Claim is stored transactionally and cannot be acquired by a second
        // concurrent caller/process. A crash leaves a claimed challenge
        // unusable (fail-closed); a new email challenge is then required.
        internal bool TryClaim(string token, long now, out Guid principal, out string claimId)
        {
            principal = Guid.Empty;
            claimId = null;
            if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
                return false;
            string[] parts = token.Split(s_separator, 2);
            if (parts.Length != 2 || parts[0].Length != 32 ||
                !Guid.TryParseExact(parts[0], "N", out _))
                return false;

            using var db = Open();
            using var tx = db.BeginTransaction();
            string userId;
            byte[] salt, expected;
            using (var read = db.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = @"SELECT principal_id,salt,digest FROM ogl_password_recovery
                    WHERE challenge_id=@id AND state=0 AND expires_at>@now";
                read.Parameters.AddWithValue("@id", parts[0]);
                read.Parameters.AddWithValue("@now", now);
                using var row = read.ExecuteReader();
                if (!row.Read()) return false;
                userId = row.GetString(0);
                salt = (byte[])row.GetValue(1);
                expected = (byte[])row.GetValue(2);
            }

            byte[] provided = Digest(salt, token);
            if (expected.Length != provided.Length ||
                !CryptographicOperations.FixedTimeEquals(expected, provided) ||
                !Guid.TryParse(userId, out principal))
            {
                principal = Guid.Empty;
                return false;
            }
            claimId = Guid.NewGuid().ToString("N");
            using (var update = db.CreateCommand())
            {
                update.Transaction = tx;
                update.CommandText = @"UPDATE ogl_password_recovery SET state=1,
                    claim_id=@claim, claimed_at=@now WHERE challenge_id=@id
                    AND state=0 AND expires_at>@now";
                update.Parameters.AddWithValue("@id", parts[0]);
                update.Parameters.AddWithValue("@claim", claimId);
                update.Parameters.AddWithValue("@now", now);
                if (update.ExecuteNonQuery() != 1)
                {
                    claimId = null;
                    principal = Guid.Empty;
                    return false;
                }
            }
            tx.Commit();
            return true;
        }

        // Only the holder of the claim can acknowledge success or return a
        // failed write to the issued state while the challenge is still live.
        internal bool Finish(string token, string claimId, bool passwordWritten, long now)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 34 ||
                string.IsNullOrWhiteSpace(claimId))
                return false;
            string[] parts = token.Split(s_separator, 2);
            if (parts.Length != 2 || parts[0].Length != 32 ||
                !Guid.TryParseExact(parts[0], "N", out _))
                return false;
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE ogl_password_recovery SET
                    state=CASE WHEN @done=1 THEN 2 ELSE 0 END,
                    claim_id=NULL, claimed_at=NULL
                    WHERE challenge_id=@id AND state=1 AND claim_id=@claim
                    AND (@done=1 OR expires_at>@now)";
            cmd.Parameters.AddWithValue("@done", passwordWritten ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", parts[0]);
            cmd.Parameters.AddWithValue("@claim", claimId);
            cmd.Parameters.AddWithValue("@now", now);
            return cmd.ExecuteNonQuery() == 1;
        }
    }
}
