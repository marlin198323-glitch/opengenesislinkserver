// OpenGenesisLINK InventoryGuard phase 3m.
// Database-provider-neutral, fail-closed grant persistence.
// Caller supplies a trusted DB connection factory; no public HG route.
// Grant creation is restricted to an authenticated HOME-GRID service.
using System;
using System.Data;
using System.Data.Common;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    public sealed class OglHgRoamingGrantRepository
    {
        private readonly Func<DbConnection> m_openConnection;

        public OglHgRoamingGrantRepository(Func<DbConnection> openConnection)
        {
            m_openConnection = openConnection ??
                throw new ArgumentNullException(nameof(openConnection));
        }

        private static void Add(DbCommand command, string name, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static bool ValidDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination) ||
                destination.Length > 255 ||
                !Uri.TryCreate(destination, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
                return false;
            string authority = uri.GetLeftPart(UriPartial.Authority);
            return destination == authority || destination == authority + "/";
        }

        private DbConnection Open()
        {
            DbConnection connection = m_openConnection();
            if (connection == null)
                throw new InvalidOperationException("Missing grant database connection");
            if (connection.State != ConnectionState.Open)
                connection.Open();
            return connection;
        }

        // Explicit startup/admin migration, never invoked from public HG.
        // Executes provider-specific DDL in separate statements so drivers
        // need not support multi-statement commands.
        public bool TryEnsureSchema(OglHgGrantDatabase database)
        {
            try
            {
                using DbConnection connection = Open();
                string ddl = OglHgRoamingGrantSchema.CreateTable(database);
                foreach (string statement in ddl.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(statement))
                        continue;
                    using DbCommand command = connection.CreateCommand();
                    command.CommandText = statement.Trim();
                    command.ExecuteNonQuery();
                }
                return true;
            }
            catch (DbException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        // Invoke ONLY after an authenticated local travel action. Never
        // accept owner/session/destination solely from a remote HG request.
        public bool TryCreateTrustedGrant(UUID grantId, UUID residentId,
            UUID sessionId, string destination, DateTimeOffset expiry)
        {
            if (grantId == UUID.Zero || residentId == UUID.Zero ||
                sessionId == UUID.Zero || !ValidDestination(destination))
                return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long until = expiry.ToUnixTimeSeconds();
            if (until <= now || until > now + 600)
                return false;
            try
            {
                using DbConnection connection = Open();
                using DbCommand cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO ogl_hg_roaming_grants " +
                    "(grant_id,resident_id,session_id,destination_uri,expires_utc,revoked_utc,created_utc) " +
                    "VALUES (@id,@resident,@session,@destination,@expires,NULL,@now)";
                Add(cmd, "@id", grantId.ToString());
                Add(cmd, "@resident", residentId.ToString());
                Add(cmd, "@session", sessionId.ToString());
                Add(cmd, "@destination", destination);
                Add(cmd, "@expires", until);
                Add(cmd, "@now", now);
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (DbException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        public bool IsActive(UUID residentId, UUID sessionId, string destination)
        {
            if (residentId == UUID.Zero || sessionId == UUID.Zero ||
                !ValidDestination(destination))
                return false;
            try
            {
                using DbConnection connection = Open();
                using DbCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM ogl_hg_roaming_grants WHERE " +
                    OglHgRoamingGrantSchema.ActiveGrantPredicate;
                Add(cmd, "@resident", residentId.ToString());
                Add(cmd, "@session", sessionId.ToString());
                Add(cmd, "@destination", destination);
                Add(cmd, "@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
            catch (DbException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
        }

        public bool TryRevokeSession(UUID residentId, UUID sessionId)
        {
            if (residentId == UUID.Zero || sessionId == UUID.Zero)
                return false;
            try
            {
                using DbConnection connection = Open();
                using DbCommand cmd = connection.CreateCommand();
                cmd.CommandText = OglHgRoamingGrantSchema.RevokeSession;
                Add(cmd, "@resident", residentId.ToString());
                Add(cmd, "@session", sessionId.ToString());
                Add(cmd, "@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
                // Zero rows means the session was already revoked or absent.
                return true;
            }
            catch (DbException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
