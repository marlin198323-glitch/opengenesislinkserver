// SPDX-License-Identifier: MPL-2.0
// Parameterized World-account email resolution. OpenSim's ordinary
// GetUserAccounts(scope, query) searches names, not email addresses.
using System;
using MySql.Data.MySqlClient;
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
            m_Provider =
                provider.IndexOf("MySQL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                provider.IndexOf("MariaDB", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "mysql" : "";
            if (m_ConnectionString.Length == 0 || m_Provider.Length == 0)
                throw new InvalidOperationException(
                    "World password recovery requires the existing MariaDB UserAccount database.");
        }

        internal UserAccount FindUniqueLocalAccount(string email)
        {
            if (string.IsNullOrEmpty(email) || email.Length > 64)
                return null;
            string normalized = email.Trim().ToLowerInvariant();
            using MySqlConnection db = new MySqlConnection(m_ConnectionString);
            db.Open();
            using MySqlCommand cmd = db.CreateCommand();
            cmd.CommandText = "SELECT PrincipalID FROM UserAccounts " +
                "WHERE LOWER(Email) = @email LIMIT 2";
            cmd.Parameters.AddWithValue("@email", normalized);
            using MySqlDataReader reader = cmd.ExecuteReader();
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
