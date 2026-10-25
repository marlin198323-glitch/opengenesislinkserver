// SPDX-License-Identifier: MPL-2.0
// World-owned password recovery state in the existing ROBUST MariaDB database.
// Only salted SHA-256 token digests are persisted, never plaintext tokens.
// InnoDB transactions and per-account advisory locks protect multi-node use.
using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using MySql.Data.MySqlClient;

namespace NexVerse.Server.Api
{
    internal sealed class NexPasswordRecoveryStore
    {
        private const int TtlSeconds = 900;
        // Reserved marker in the challenge ID for public mailbox-issued tokens.
        // New machine-issued tokens explicitly avoid it. No DB-schema changes.
        private const string PublicIdPrefix = "9a7e4d3c2b1a";
        private readonly string m_ConnectionString;
        private static readonly char[] s_Separator = { '.' };

        internal NexPasswordRecoveryStore(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("The existing ROBUST MariaDB connection string is required.",
                    nameof(connectionString));

            m_ConnectionString = connectionString;
            using MySqlConnection connection = Open();
            using MySqlCommand command = connection.CreateCommand();
            // Dedicated table: do not modify OpenSim users, authentication or
            // asset tables. InnoDB is required for atomic one-time claims.
            command.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_password_recovery (
                challenge_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                principal_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                salt VARBINARY(16) NOT NULL,
                digest BINARY(32) NOT NULL,
                issued_at BIGINT NOT NULL,
                expires_at BIGINT NOT NULL,
                state TINYINT UNSIGNED NOT NULL DEFAULT 0,
                claim_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NULL,
                claimed_at BIGINT NULL,
                INDEX ix_ogl_recovery_account (principal_id, issued_at),
                INDEX ix_ogl_recovery_expiry (expires_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            command.ExecuteNonQuery();
        }

        private MySqlConnection Open()
        {
            var connection = new MySqlConnection(m_ConnectionString);
            connection.Open();
            return connection;
        }

        private static byte[] Digest(byte[] salt, string secret)
        {
            byte[] input = Encoding.UTF8.GetBytes(secret);
            byte[] combined = new byte[salt.Length + input.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(input, 0, combined, salt.Length, input.Length);
            byte[] digest = SHA256.HashData(combined);
            CryptographicOperations.ZeroMemory(combined);
            CryptographicOperations.ZeroMemory(input);
            return digest;
        }

        private static bool TryTokenId(string token, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
                return false;
            string[] parts = token.Split(s_Separator, 2);
            if (parts.Length != 2 || parts[0].Length != 32 || parts[1].Length == 0 ||
                !Guid.TryParseExact(parts[0], "N", out _))
                return false;
            id = parts[0];
            return true;
        }

        private static MySqlCommand Command(MySqlConnection db, MySqlTransaction tx,
            string sql)
        {
            var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = sql;
            return command;
        }

        // MariaDB GET_LOCK is connection-scoped. It serializes token issue for
        // the same account across all ROBUST processes, including rate limiting.
        private static string LockName(Guid principal) =>
            "ogl_pwdrec:" + principal.ToString("N");

        internal string Issue(Guid principal, long now) => IssueCore(principal, now, false);

        internal string IssuePublic(Guid principal, long now) => IssueCore(principal, now, true);

        private string IssueCore(Guid principal, long now, bool publicToken)
        {
            if (principal == Guid.Empty)
                throw new ArgumentException("Missing World principal.", nameof(principal));
            using MySqlConnection db = Open();
            string lockName = LockName(principal);
            using (MySqlCommand acquire = Command(db, null, "SELECT GET_LOCK(@lock, 5)"))
            {
                acquire.Parameters.AddWithValue("@lock", lockName);
                if (Convert.ToInt32(acquire.ExecuteScalar()) != 1)
                    throw new TimeoutException("Recovery issuance lock unavailable.");
            }

            try
            {
                using MySqlTransaction tx = db.BeginTransaction(IsolationLevel.ReadCommitted);
                using (MySqlCommand limit = Command(db, tx,
                    @"SELECT COUNT(*) FROM ogl_password_recovery
                      WHERE principal_id=@p AND issued_at>=@window"))
                {
                    limit.Parameters.AddWithValue("@p", principal.ToString("D"));
                    limit.Parameters.AddWithValue("@window", now - 3600);
                    if (Convert.ToInt32(limit.ExecuteScalar()) >= 3)
                        return null; // Transaction rolls back when disposed.
                }

                string challengeId;
                do { challengeId = Guid.NewGuid().ToString("N"); }
                while (challengeId.StartsWith(PublicIdPrefix, StringComparison.Ordinal));
                if (publicToken)
                    challengeId = PublicIdPrefix + challengeId.Substring(PublicIdPrefix.Length);
                string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                string token = challengeId + "." + secret;
                byte[] salt = RandomNumberGenerator.GetBytes(16);
                byte[] digest = Digest(salt, token);

                using (MySqlCommand invalidate = Command(db, tx,
                    @"UPDATE ogl_password_recovery SET state=2
                      WHERE principal_id=@p AND state=0"))
                {
                    invalidate.Parameters.AddWithValue("@p", principal.ToString("D"));
                    invalidate.ExecuteNonQuery();
                }
                using (MySqlCommand insert = Command(db, tx,
                    @"INSERT INTO ogl_password_recovery
                      (challenge_id, principal_id, salt, digest, issued_at, expires_at, state)
                      VALUES (@id, @p, @salt, @digest, @now, @expires, 0)"))
                {
                    insert.Parameters.AddWithValue("@id", challengeId);
                    insert.Parameters.AddWithValue("@p", principal.ToString("D"));
                    insert.Parameters.AddWithValue("@salt", salt);
                    insert.Parameters.AddWithValue("@digest", digest);
                    insert.Parameters.AddWithValue("@now", now);
                    insert.Parameters.AddWithValue("@expires", now + TtlSeconds);
                    insert.ExecuteNonQuery();
                }
                using (MySqlCommand cleanup = Command(db, tx,
                    "DELETE FROM ogl_password_recovery WHERE expires_at<@cutoff"))
                {
                    cleanup.Parameters.AddWithValue("@cutoff", now - 86400);
                    cleanup.ExecuteNonQuery();
                }
                tx.Commit();
                return token;
            }
            finally
            {
                using MySqlCommand release = Command(db, null, "SELECT RELEASE_LOCK(@lock)");
                release.Parameters.AddWithValue("@lock", lockName);
                release.ExecuteScalar();
            }
        }

        // Lock the record FOR UPDATE before checking the digest, then
        // transactionally claim it; only one concurrent node can succeed.
        // Anonymous proof-of-mailbox completion may ONLY consume challenges
        // issued through World’s own public SMTP path. An issue-only machine
        // key cannot bypass password-recovery:complete by calling /confirm.
        internal bool TryClaimPublic(string token, long now, out Guid principal,
            out string claimId)
        {
            principal = Guid.Empty;
            claimId = null;
            if (!TryTokenId(token, out string id) ||
                !id.StartsWith(PublicIdPrefix, StringComparison.Ordinal))
                return false;
            return TryClaim(token, now, out principal, out claimId);
        }

        internal bool TryClaim(string token, long now, out Guid principal,
            out string claimId)
        {
            principal = Guid.Empty;
            claimId = null;
            if (!TryTokenId(token, out string challengeId))
                return false;

            using MySqlConnection db = Open();
            using MySqlTransaction tx = db.BeginTransaction(IsolationLevel.ReadCommitted);
            string userId;
            byte[] salt, expected;
            using (MySqlCommand read = Command(db, tx,
                @"SELECT principal_id, salt, digest FROM ogl_password_recovery
                  WHERE challenge_id=@id AND state=0 AND expires_at>@now FOR UPDATE"))
            {
                read.Parameters.AddWithValue("@id", challengeId);
                read.Parameters.AddWithValue("@now", now);
                using MySqlDataReader row = read.ExecuteReader();
                if (!row.Read()) return false;
                // MySql.Data returns CHAR(36) UUID values either as a Guid
                // or as a string depending on "Old Guids" in the existing
                // ROBUST connection string. Both are valid representations.
                userId = row.GetValue(0)?.ToString();
                salt = (byte[])row.GetValue(1);
                expected = (byte[])row.GetValue(2);
            }

            byte[] provided = Digest(salt, token);
            if (expected.Length != provided.Length ||
                !CryptographicOperations.FixedTimeEquals(expected, provided) ||
                !Guid.TryParse(userId, out Guid parsedPrincipal))
                return false;

            string nextClaim = Guid.NewGuid().ToString("N");
            using (MySqlCommand update = Command(db, tx,
                @"UPDATE ogl_password_recovery SET state=1, claim_id=@claim,
                  claimed_at=@now WHERE challenge_id=@id AND state=0
                  AND expires_at>@now"))
            {
                update.Parameters.AddWithValue("@id", challengeId);
                update.Parameters.AddWithValue("@claim", nextClaim);
                update.Parameters.AddWithValue("@now", now);
                if (update.ExecuteNonQuery() != 1)
                    return false;
            }
            tx.Commit();
            principal = parsedPrincipal;
            claimId = nextClaim;
            return true;
        }

        // The caller's unique claim ID guards success/failure acknowledgments.
        // On an uncertain password write the token stays claimed (fail closed).
        internal bool Finish(string token, string claimId, bool passwordWritten,
            long now)
        {
            if (!TryTokenId(token, out string challengeId) ||
                !Guid.TryParseExact(claimId, "N", out _))
                return false;
            using MySqlConnection db = Open();
            using MySqlCommand command = Command(db, null,
                @"UPDATE ogl_password_recovery SET
                    state=CASE WHEN @done=1 THEN 2 ELSE 0 END,
                    claim_id=NULL, claimed_at=NULL
                  WHERE challenge_id=@id AND state=1 AND claim_id=@claim
                    AND (@done=1 OR expires_at>@now)");
            command.Parameters.AddWithValue("@done", passwordWritten ? 1 : 0);
            command.Parameters.AddWithValue("@id", challengeId);
            command.Parameters.AddWithValue("@claim", claimId);
            command.Parameters.AddWithValue("@now", now);
            return command.ExecuteNonQuery() == 1;
        }
    }
}
