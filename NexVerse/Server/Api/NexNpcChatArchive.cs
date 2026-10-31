// SPDX-License-Identifier: MPL-2.0
// R619: Separate opt-in NPC owner/agent IM archive. No public chat,
// no messages involving other residents, no reuse of resident IM store.
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcChatRow
    {
        public string message_id {get;set;}
        public string npc_id {get;set;}
        public string sender {get;set;}
        public string text {get;set;}
        public string sent_at {get;set;}
    }

    internal sealed class NexNpcChatArchive
    {
        private readonly object m_Guard=new object();
        private readonly string m_Connection;
        private readonly int m_Retention;
        internal int RetentionDays => m_Retention;
        internal NexNpcChatArchive(string path,int retentionDays)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Dedicated NPC chat archive path required.");
            string full=Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            m_Connection="Data Source="+full+";Version=3;Pooling=False;";
            m_Retention=Math.Clamp(retentionDays,7,90);
            using var db=Open();
            Exec(db,@"CREATE TABLE IF NOT EXISTS npc_chat_consent(
                owner TEXT PRIMARY KEY NOT NULL,
                enabled INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            )");
            Exec(db,@"CREATE TABLE IF NOT EXISTS npc_chat_entries(
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id TEXT UNIQUE NOT NULL,
                owner TEXT NOT NULL,
                npc_id TEXT NOT NULL,
                sender TEXT NOT NULL,
                body TEXT NOT NULL,
                accepted_at INTEGER NOT NULL
            )");
            Exec(db,"CREATE INDEX IF NOT EXISTS ix_npc_chat_owner_npc_seq ON npc_chat_entries(owner,npc_id,seq)");
            Exec(db,"CREATE INDEX IF NOT EXISTS ix_npc_chat_expiry ON npc_chat_entries(accepted_at)");
        }
        private SqliteConnection Open()
        {
            var db=new SqliteConnection(m_Connection);
            db.Open();
            using var pragma=db.CreateCommand();
            pragma.CommandText="PRAGMA busy_timeout=3000";
            pragma.ExecuteNonQuery();
            return db;
        }
        private static void Exec(SqliteConnection db,string sql)
        {
            using var cmd=db.CreateCommand();
            cmd.CommandText=sql;cmd.ExecuteNonQuery();
        }
        private static void Add(SqliteCommand cmd,string name,object value)
            =>cmd.Parameters.AddWithValue(name,value);
        internal bool Enabled(Guid owner)
        {
            lock(m_Guard)
            {
                using var db=Open();
                using var cmd=db.CreateCommand();
                cmd.CommandText="SELECT enabled FROM npc_chat_consent WHERE owner=@owner";
                Add(cmd,"@owner",owner.ToString("D"));
                return Convert.ToInt32(cmd.ExecuteScalar()??0)==1;
            }
        }
        internal void SetEnabled(Guid owner,bool enabled)
        {
            if(owner==Guid.Empty)throw new ArgumentException("Owner required");
            lock(m_Guard)
            {
                using var db=Open();
                using var tx=db.BeginTransaction();
                using(var cmd=db.CreateCommand())
                {
                    cmd.Transaction=tx;
                    cmd.CommandText=@"INSERT INTO npc_chat_consent(owner,enabled,updated_at)
                        VALUES(@owner,@enabled,@now)
                        ON CONFLICT(owner) DO UPDATE SET enabled=excluded.enabled,updated_at=excluded.updated_at";
                    Add(cmd,"@owner",owner.ToString("D"));
                    Add(cmd,"@enabled",enabled?1:0);
                    Add(cmd,"@now",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    cmd.ExecuteNonQuery();
                }
                if(!enabled)Delete(db,tx,owner);
                tx.Commit();
            }
        }
        private static void Delete(SqliteConnection db,SqliteTransaction tx,Guid owner)
        {
            using var cmd=db.CreateCommand();
            cmd.Transaction=tx;
            cmd.CommandText="DELETE FROM npc_chat_entries WHERE owner=@owner";
            Add(cmd,"@owner",owner.ToString("D"));cmd.ExecuteNonQuery();
        }
        internal void Erase(Guid owner)
        {
            lock(m_Guard)
            {
                using var db=Open();
                using var tx=db.BeginTransaction();
                Delete(db,tx,owner);tx.Commit();
            }
        }
        internal bool Append(Guid eventId,Guid owner,Guid npc,string sender,string body)
        {
            if(eventId==Guid.Empty||owner==Guid.Empty||npc==Guid.Empty||
               owner==npc||(sender!="owner"&&sender!="npc")||
               string.IsNullOrWhiteSpace(body)||body.Length>4000)
                return false;
            lock(m_Guard)
            {
                using var db=Open();
                using var tx=db.BeginTransaction();
                long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool inserted;
                using(var cmd=db.CreateCommand())
                {
                    cmd.Transaction=tx;
                    cmd.CommandText=@"INSERT OR IGNORE INTO npc_chat_entries
                      (event_id,owner,npc_id,sender,body,accepted_at)
                      SELECT @event,@owner,@npc,@sender,@body,@now
                      WHERE EXISTS (SELECT 1 FROM npc_chat_consent
                        WHERE owner=@owner AND enabled=1)";
                    Add(cmd,"@event",eventId.ToString("D"));
                    Add(cmd,"@owner",owner.ToString("D"));
                    Add(cmd,"@npc",npc.ToString("D"));
                    Add(cmd,"@sender",sender);
                    Add(cmd,"@body",body);
                    Add(cmd,"@now",now);
                    inserted=cmd.ExecuteNonQuery()!=0;
                }
                using(var expiry=db.CreateCommand())
                {
                    expiry.Transaction=tx;
                    expiry.CommandText="DELETE FROM npc_chat_entries WHERE accepted_at < @cutoff";
                    Add(expiry,"@cutoff",now-m_Retention*86400L);
                    expiry.ExecuteNonQuery();
                }
                using(var cap=db.CreateCommand())
                {
                    cap.Transaction=tx;
                    cap.CommandText=@"DELETE FROM npc_chat_entries WHERE owner=@owner AND
                      seq NOT IN(SELECT seq FROM npc_chat_entries WHERE owner=@owner
                        ORDER BY seq DESC LIMIT 10000)";
                    Add(cap,"@owner",owner.ToString("D"));
                    cap.ExecuteNonQuery();
                }
                tx.Commit();return inserted;
            }
        }
        internal List<NexNpcChatRow> Read(Guid owner,Guid npc)
        {
            var result=new List<NexNpcChatRow>();
            lock(m_Guard)
            {
                using var db=Open();
                using var cmd=db.CreateCommand();
                cmd.CommandText=@"SELECT event_id,npc_id,sender,body,accepted_at FROM
                  npc_chat_entries WHERE owner=@owner AND npc_id=@npc AND
                  accepted_at>=@cutoff AND EXISTS
                    (SELECT 1 FROM npc_chat_consent WHERE owner=@owner AND enabled=1)
                  ORDER BY seq ASC LIMIT 10000";
                Add(cmd,"@owner",owner.ToString("D"));
                Add(cmd,"@npc",npc.ToString("D"));
                Add(cmd,"@cutoff",DateTimeOffset.UtcNow.ToUnixTimeSeconds()-m_Retention*86400L);
                using var reader=cmd.ExecuteReader();
                while(reader.Read())
                {
                    result.Add(new NexNpcChatRow
                    {
                        message_id=reader.GetString(0),
                        npc_id=reader.GetString(1),
                        sender=reader.GetString(2),
                        text=reader.GetString(3),
                        sent_at=DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4))
                            .UtcDateTime.ToString("O")
                    });
                }
                return result;
            }
        }
    }
}
