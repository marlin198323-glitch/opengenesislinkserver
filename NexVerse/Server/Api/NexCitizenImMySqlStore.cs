// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using NexVerse.Core.Economy;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Consent-gated portal archive in the grid's existing MySQL/MariaDB
    /// database. Separate tables: never alter existing OpenSim IM/offline IM.
    /// Parameterized queries, deduplicated events, transactional opt-out.
    /// </summary>
    internal sealed class NexCitizenImMySqlStore : INexCitizenImStore
    {
        private readonly Func<DbConnection> m_Connect;
        private readonly int m_RetentionDays;
        private const int MaxEntriesPerOwner = 10000;

        internal NexCitizenImMySqlStore(string provider, string connectionString, int retentionDays)
        {
            NexLedgerSqlRuntime runtime = NexLedgerSqlRuntime.Resolve(provider, connectionString);
            if (runtime.Dialect != NexLedgerSqlDialect.MySql)
                throw new NotSupportedException("Portal IM MySql backend requires OpenSim.Data.MySQL.dll (MariaDB/MySQL).");
            m_Connect = runtime.CreateConnection;
            m_RetentionDays = Math.Clamp(retentionDays, 7, 365);

            using DbConnection db = Open();
            Execute(db, null, @"CREATE TABLE IF NOT EXISTS portal_im_settings (
                owner CHAR(36) NOT NULL PRIMARY KEY,
                enabled TINYINT(1) NOT NULL DEFAULT 0,
                updated_at BIGINT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            Execute(db, null, @"CREATE TABLE IF NOT EXISTS portal_im_entries (
                seq BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                event_id CHAR(36) NOT NULL,
                owner CHAR(36) NOT NULL,
                from_agent CHAR(36) NOT NULL,
                to_agent CHAR(36) NOT NULL,
                peer CHAR(36) NOT NULL,
                sender_name VARCHAR(160) NOT NULL,
                body TEXT NOT NULL,
                source VARCHAR(16) NOT NULL,
                accepted_at BIGINT NOT NULL,
                UNIQUE KEY uq_portal_im_owner_event (owner,event_id),
                KEY ix_portal_im_owner_seq (owner,seq),
                KEY ix_portal_im_owner_peer_seq (owner,peer,seq),
                KEY ix_portal_im_expiry (accepted_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }

        private DbConnection Open()
        {
            DbConnection db = m_Connect();
            db.Open();
            return db;
        }

        private static DbCommand Command(DbConnection db, DbTransaction tx,
            string sql, params (string Name, object Value)[] parameters)
        {
            DbCommand cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach ((string name, object value) in parameters)
            {
                DbParameter arg = cmd.CreateParameter();
                arg.ParameterName = name;
                arg.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(arg);
            }
            return cmd;
        }

        private static int Execute(DbConnection db, DbTransaction tx,
            string sql, params (string Name, object Value)[] parameters)
        {
            using DbCommand cmd = Command(db, tx, sql, parameters);
            return cmd.ExecuteNonQuery();
        }

        private static string Id(Guid value) => value.ToString("D");

        public bool IsEnabled(Guid owner)
        {
            using DbConnection db = Open();
            using DbCommand cmd = Command(db, null,
                "SELECT enabled FROM portal_im_settings WHERE owner=@owner",
                ("@owner", Id(owner)));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) == 1;
        }

        public void SetEnabled(Guid owner, bool enabled)
        {
            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            Execute(db, tx, @"INSERT INTO portal_im_settings (owner,enabled,updated_at)
                VALUES (@owner,@enabled,@now)
                ON DUPLICATE KEY UPDATE enabled=VALUES(enabled),updated_at=VALUES(updated_at)",
                ("@owner", Id(owner)), ("@enabled", enabled ? 1 : 0),
                ("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            if (!enabled)
                DeleteOwner(db, tx, owner);
            tx.Commit();
        }

        private static void DeleteOwner(DbConnection db, DbTransaction tx, Guid owner)
            => Execute(db, tx,
                "DELETE FROM portal_im_entries WHERE owner=@owner",
                ("@owner", Id(owner)));

        public void DeleteHistory(Guid owner)
        {
            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            DeleteOwner(db, tx, owner);
            tx.Commit();
        }

        public bool Append(Guid eventId, Guid sender, Guid receiver,
            string senderName, string message, string source)
        {
            if (eventId == Guid.Empty || sender == Guid.Empty ||
                receiver == Guid.Empty || sender == receiver ||
                string.IsNullOrWhiteSpace(message))
                return false;

            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool written = false;
            Guid[] participants = { sender, receiver };
            foreach (Guid owner in participants)
            {
                // The consent check lives in the INSERT itself, not a
                // preceding app-side read, preventing archived non-opt-in IMs.
                written |= Execute(db, tx, @"INSERT IGNORE INTO portal_im_entries
                    (event_id,owner,from_agent,to_agent,peer,sender_name,body,source,accepted_at)
                    SELECT @event,@owner,@from,@to,@peer,@name,@body,@source,@now
                    FROM portal_im_settings
                    WHERE owner=@owner AND enabled=1",
                    ("@event", Id(eventId)), ("@owner", Id(owner)),
                    ("@from", Id(sender)), ("@to", Id(receiver)),
                    ("@peer", Id(owner == sender ? receiver : sender)),
                    ("@name", (senderName ?? string.Empty).Length > 160
                        ? senderName.Substring(0, 160) : senderName ?? string.Empty),
                    ("@body", message), ("@source", source ?? string.Empty),
                    ("@now", now)) > 0;
            }

            Execute(db, tx, "DELETE FROM portal_im_entries WHERE accepted_at < @cutoff",
                ("@cutoff", now - m_RetentionDays * 86400L));

            foreach (Guid owner in participants)
            {
                // Fetch cutoff before deletion. Avoid self-referencing MySQL
                // DELETE subqueries (ERROR 1093) and maintain the 10k cap.
                object cutoff;
                using (DbCommand limit = Command(db, tx,
                    "SELECT seq FROM portal_im_entries WHERE owner=@owner " +
                    "ORDER BY seq DESC LIMIT 1 OFFSET 9999",
                    ("@owner", Id(owner))))
                    cutoff = limit.ExecuteScalar();
                if (cutoff != null && cutoff != DBNull.Value)
                    Execute(db, tx,
                        "DELETE FROM portal_im_entries WHERE owner=@owner AND seq < @seq",
                        ("@owner", Id(owner)), ("@seq", Convert.ToInt64(cutoff)));
            }
            tx.Commit();
            return written;
        }

        public List<NexCitizenImEntry> Read(Guid owner, long after, int limit,
            Guid? peer = null)
        {
            List<NexCitizenImEntry> result = new List<NexCitizenImEntry>();
            using DbConnection db = Open();
            using DbCommand cmd = Command(db, null,
                @"SELECT seq,event_id,from_agent,to_agent,peer,sender_name,
                  body,source,accepted_at FROM portal_im_entries
                  WHERE owner=@owner AND seq>@after" +
                (peer.HasValue ? " AND peer=@peer" : "") +
                " ORDER BY seq ASC LIMIT @limit",
                ("@owner", Id(owner)),
                ("@after", Math.Max(0, after)),
                ("@limit", Math.Clamp(limit, 1, 100)));
            if (peer.HasValue)
            {
                DbParameter par = cmd.CreateParameter();
                par.ParameterName = "@peer";
                par.Value = Id(peer.Value);
                cmd.Parameters.Add(par);
            }
            using DbDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new NexCitizenImEntry
                {
                    seq = Convert.ToInt64(reader.GetValue(0)),
                    event_id = reader.GetString(1),
                    from_agent_id = reader.GetString(2),
                    to_agent_id = reader.GetString(3),
                    peer_id = reader.GetString(4),
                    sender_name = reader.GetString(5),
                    message = reader.GetString(6),
                    source = reader.GetString(7),
                    accepted_at = DateTimeOffset.FromUnixTimeSeconds(
                        Convert.ToInt64(reader.GetValue(8)))
                });
            return result;
        }

        public List<object> Conversations(Guid owner, int limit)
        {
            List<object> result = new List<object>();
            using DbConnection db = Open();
            using DbCommand cmd = Command(db, null,
                @"SELECT e.peer,e.seq,e.accepted_at FROM portal_im_entries e
                  JOIN (SELECT peer,MAX(seq) seq FROM portal_im_entries
                        WHERE owner=@owner GROUP BY peer) latest ON latest.seq=e.seq
                  WHERE e.owner=@owner ORDER BY e.seq DESC LIMIT @limit",
                ("@owner", Id(owner)), ("@limit", Math.Clamp(limit, 1, 100)));
            using DbDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new
                {
                    peer_id = reader.GetString(0),
                    last_seq = Convert.ToInt64(reader.GetValue(1)),
                    last_message_at = DateTimeOffset.FromUnixTimeSeconds(
                        Convert.ToInt64(reader.GetValue(2)))
                });
            return result;
        }
    }
}
