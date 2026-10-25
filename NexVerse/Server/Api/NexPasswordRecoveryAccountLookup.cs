// SPDX-License-Identifier: MPL-2.0
// Parameterized World-account email resolution. OpenSim's ordinary
// GetUserAccounts(scope, query) searches names, not email addresses.
using System;
using System.Data.Common;
using MySql.Data.MySqlClient;
using Mono.Data.Sqlite;
using Npgsql;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexPasswordRecoveryAccountLookup
    {
        private readonly string m_ConnectionString;
        private readonly string m_Provider;
        private readonly IUserAccountService m_Accounts;

        internal NexPasswordRecoveryAccountLookup(string storageProvider,
            string connectionString, IUserAccountService accounts)
        {
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_ConnectionString = connectionString ?? "";
            string provider = storageProvider ?? "";
            m_Provider = provider.IndexOf("MySQL", StringComparison.OrdinalIgnoreCase) >= 0
                ? "mysql" : provider.IndexOf("PGSQL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                provider.IndexOf("Postgres", StringComparison.OrdinalIgnoreCase) >= 0
                ? "pg" : provider.IndexOf("SQLite", StringComparison.OrdinalIgnoreCase) >= 0
                ? "sqlite" : "";
            if (m_ConnectionString.Length == 0 || m_Provider.Length == 0)
                throw new InvalidOperationException("Unsupported authoritative UserAccount database provider.");
        }

        internal UserAccount FindUniqueLocalAccount(string email)
        {
            if (string.IsNullOrEmpty(email) || email.Length > 64)
                return null;
            string normalized = email.Trim().ToLowerInvariant();
            using DbConnection db = m_Provider switch
            {
                "mysql" => new MySqlConnection(m_ConnectionString),
                "pg" => new NpgsqlConnection(m_ConnectionString),
                _ => new SqliteConnection(m_ConnectionString)
            };
            db.Open();
            using DbCommand cmd = db.CreateCommand();
            string emailField = m_Provider == "mysql" ? "Email" : "\"Email\"";
            string idField = m_Provider == "mysql" ? "PrincipalID" : "\"PrincipalID\"";
            cmd.CommandText = "SELECT " + idField + " FROM UserAccounts " +
                "WHERE LOWER(" + emailField + ") = @email LIMIT 2";
            DbParameter param = cmd.CreateParameter();
            param.ParameterName = "@email";
            param.Value = normalized;
            cmd.Parameters.Add(param);
            using DbDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            if (!Guid.TryParse(Convert.ToString(reader.GetValue(0)), out Guid parsed))
                return null;
            if (reader.Read()) return null; // Ambiguous mailbox -> fail closed.
            UserAccount account = m_Accounts.GetUserAccount(UUID.Zero, new UUID(parsed));
            if (account == null || !account.LoginAllowed ||
                !string.Equals(account.Email?.Trim(), email.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return null;
            return account;
        }
    }
}
