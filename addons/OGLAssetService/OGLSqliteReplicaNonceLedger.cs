using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Persistent single-use receipt nonces. All coordinators sharing a SQLite
    /// database can atomically reject duplicate receipt submissions.
    /// </summary>
    public sealed class OGLSqliteReplicaNonceLedger
    {
        private readonly string connectionString;

        public OGLSqliteReplicaNonceLedger(string databasePath)
        {
            if (String.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path required.", nameof(databasePath));
            string full = Path.GetFullPath(databasePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Nonce ledger cannot be a symbolic link.");
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = full,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true,
                DefaultTimeout = 30
            }.ToString();
            using (SqliteConnection db = Open())
            using (SqliteCommand cmd = db.CreateCommand())
            {
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_replica_receipt_nonces (
                    node_id TEXT NOT NULL,
                    nonce TEXT NOT NULL,
                    consumed_at INTEGER NOT NULL,
                    PRIMARY KEY(node_id, nonce)
                );";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns false for a previously consumed nonce. Never prune records
        /// until a safe retention policy accounts for the maximum receipt age.
        /// </summary>
        public bool TryConsume(string nodeId, string nonce, DateTimeOffset now)
        {
            if (String.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 ||
                nodeId != nodeId.Trim())
                throw new ArgumentException("Invalid node identity.", nameof(nodeId));
            if (!Guid.TryParseExact(nonce, "N", out Guid parsed) || parsed == Guid.Empty)
                throw new ArgumentException("Invalid receipt nonce.", nameof(nonce));
            using (SqliteConnection db = Open())
            using (SqliteCommand cmd = db.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO ogl_replica_receipt_nonces
                    (node_id, nonce, consumed_at) VALUES ($node, $nonce, $time)
                    ON CONFLICT(node_id, nonce) DO NOTHING";
                cmd.Parameters.AddWithValue("$node", nodeId);
                cmd.Parameters.AddWithValue("$nonce", parsed.ToString("N"));
                cmd.Parameters.AddWithValue("$time", now.ToUnixTimeSeconds());
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        private SqliteConnection Open()
        {
            SqliteConnection db = new SqliteConnection(connectionString);
            db.Open();
            return db;
        }
    }
}
