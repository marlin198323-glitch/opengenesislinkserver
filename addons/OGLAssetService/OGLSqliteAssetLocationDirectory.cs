using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// SQLite-backed, transactional asset location directory for a single shared
    /// SQLite database file. Not a distributed lease or node-authentication layer.
    /// </summary>
    public sealed class OGLSqliteAssetLocationDirectory
    {
        private readonly string connectionString;

        public OGLSqliteAssetLocationDirectory(string databasePath)
        {
            if (String.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path required.", nameof(databasePath));
            string full = Path.GetFullPath(databasePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Database file cannot be a symbolic link.");
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
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_asset_locations (
                    asset_id TEXT PRIMARY KEY NOT NULL,
                    content_hash TEXT NOT NULL,
                    primary_node TEXT NOT NULL,
                    replicas_json TEXT NOT NULL,
                    revision INTEGER NOT NULL CHECK (revision > 0)
                );";
                cmd.ExecuteNonQuery();
            }
        }

        private SqliteConnection Open()
        {
            SqliteConnection db = new SqliteConnection(connectionString);
            db.Open();
            using (SqliteCommand pragma = db.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=30000;";
                pragma.ExecuteNonQuery();
            }
            return db;
        }

        public OGLAssetLocationDirectory.Entry Find(string assetId)
        {
            string id = CanonicalId(assetId);
            using (SqliteConnection db = Open())
            using (SqliteCommand cmd = db.CreateCommand())
            {
                cmd.CommandText = @"SELECT content_hash, primary_node, replicas_json, revision
                    FROM ogl_asset_locations WHERE asset_id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new OGLAssetLocationDirectory.Entry
                    {
                        AssetId = id,
                        ContentHash = reader.GetString(0),
                        PrimaryNode = reader.GetString(1),
                        Replicas = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)),
                        Revision = reader.GetInt64(3)
                    };
                }
            }
        }

        public OGLAssetLocationDirectory.Entry Publish(
            OGLAssetLocationDirectory.Entry candidate, long expectedRevision)
        {
            OGLAssetLocationDirectory.Entry next = ValidateCopy(candidate);
            if (expectedRevision < 0 || expectedRevision == long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            using (SqliteConnection db = Open())
            using (SqliteTransaction tx = db.BeginTransaction())
            using (SqliteCommand cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                next.Revision = expectedRevision + 1;
                if (expectedRevision == 0)
                {
                    cmd.CommandText = @"INSERT INTO ogl_asset_locations
                        (asset_id, content_hash, primary_node, replicas_json, revision)
                        VALUES ($id, $hash, $node, $replicas, 1)
                        ON CONFLICT(asset_id) DO NOTHING";
                }
                else
                {
                    cmd.CommandText = @"UPDATE ogl_asset_locations SET
                        content_hash = $hash, primary_node = $node,
                        replicas_json = $replicas, revision = $next
                        WHERE asset_id = $id AND revision = $expected";
                    cmd.Parameters.AddWithValue("$expected", expectedRevision);
                    cmd.Parameters.AddWithValue("$next", next.Revision);
                }
                cmd.Parameters.AddWithValue("$id", next.AssetId);
                cmd.Parameters.AddWithValue("$hash", next.ContentHash);
                cmd.Parameters.AddWithValue("$node", next.PrimaryNode);
                cmd.Parameters.AddWithValue("$replicas", JsonSerializer.Serialize(next.Replicas));
                if (cmd.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Asset location revision conflict.");
                tx.Commit();
                return next;
            }
        }

        private static string CanonicalId(string assetId)
        {
            if (!Guid.TryParseExact(assetId, "D", out Guid id) || id == Guid.Empty)
                throw new ArgumentException("Canonical non-empty UUID required.", nameof(assetId));
            return id.ToString("D");
        }

        private static OGLAssetLocationDirectory.Entry ValidateCopy(
            OGLAssetLocationDirectory.Entry input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            string id = CanonicalId(input.AssetId);
            if (input.ContentHash == null || input.ContentHash.Length != 64)
                throw new ArgumentException("SHA-256 hash required.");
            foreach (char c in input.ContentHash)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    throw new ArgumentException("Lowercase SHA-256 required.");
            if (String.IsNullOrWhiteSpace(input.PrimaryNode) ||
                input.PrimaryNode.Length > 128 || input.PrimaryNode != input.PrimaryNode.Trim())
                throw new ArgumentException("Invalid primary node.");
            if (input.Replicas == null || input.Replicas.Count > 32)
                throw new ArgumentException("Invalid replica list.");
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { input.PrimaryNode };
            foreach (string node in input.Replicas)
                if (String.IsNullOrWhiteSpace(node) || node.Length > 128 ||
                    node != node.Trim() || !seen.Add(node))
                    throw new ArgumentException("Invalid replica node.");
            return new OGLAssetLocationDirectory.Entry
            {
                AssetId = id,
                ContentHash = input.ContentHash,
                PrimaryNode = input.PrimaryNode,
                Replicas = new List<string>(input.Replicas)
            };
        }
    }
}
