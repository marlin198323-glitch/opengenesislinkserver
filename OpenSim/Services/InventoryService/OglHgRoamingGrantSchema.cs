// OpenGenesisLINK InventoryGuard phase 3l.
// Provider-neutral DDL for an operator-managed, revocable home-grid
// travel delegation table. No migration is executed automatically.
// Grant creation MUST be authorized by local authenticated login/travel.
using System;

namespace OpenSim.Services.InventoryService
{
    public enum OglHgGrantDatabase
    {
        PostgreSql,
        MariaDb,
        Sqlite
    }

    public static class OglHgRoamingGrantSchema
    {
        public static string CreateTable(OglHgGrantDatabase database)
        {
            switch (database)
            {
                case OglHgGrantDatabase.PostgreSql:
                    return @"CREATE TABLE IF NOT EXISTS ogl_hg_roaming_grants (
 grant_id VARCHAR(36) PRIMARY KEY,
 resident_id VARCHAR(36) NOT NULL,
 session_id VARCHAR(36) NOT NULL,
 destination_uri VARCHAR(255) NOT NULL,
 expires_utc BIGINT NOT NULL,
 revoked_utc BIGINT NULL,
 created_utc BIGINT NOT NULL,
 CONSTRAINT ogl_hg_grants_expiry CHECK (expires_utc > created_utc)
);
CREATE INDEX IF NOT EXISTS ogl_hg_grants_session_idx
 ON ogl_hg_roaming_grants (resident_id, session_id, destination_uri);";
                case OglHgGrantDatabase.MariaDb:
                    return @"CREATE TABLE IF NOT EXISTS ogl_hg_roaming_grants (
 grant_id VARCHAR(36) PRIMARY KEY,
 resident_id VARCHAR(36) NOT NULL,
 session_id VARCHAR(36) NOT NULL,
 destination_uri VARCHAR(255) NOT NULL,
 expires_utc BIGINT NOT NULL,
 revoked_utc BIGINT NULL,
 created_utc BIGINT NOT NULL,
 INDEX ogl_hg_grants_session_idx (resident_id, session_id, destination_uri),
 CONSTRAINT ogl_hg_grants_expiry CHECK (expires_utc > created_utc)
) ENGINE=InnoDB;";
                case OglHgGrantDatabase.Sqlite:
                    return @"CREATE TABLE IF NOT EXISTS ogl_hg_roaming_grants (
 grant_id TEXT PRIMARY KEY,
 resident_id TEXT NOT NULL,
 session_id TEXT NOT NULL,
 destination_uri TEXT NOT NULL,
 expires_utc INTEGER NOT NULL,
 revoked_utc INTEGER NULL,
 created_utc INTEGER NOT NULL,
 CHECK (expires_utc > created_utc)
);
CREATE INDEX IF NOT EXISTS ogl_hg_grants_session_idx
 ON ogl_hg_roaming_grants (resident_id, session_id, destination_uri);";
                default:
                    throw new ArgumentOutOfRangeException(nameof(database));
            }
        }

        // The repository adapter must execute this as a parameterized query
        // against its OWN trusted database connection; no HTTP-supplied
        // identity is sufficient to create a grant.
        public const string ActiveGrantPredicate =
            "resident_id = @resident AND session_id = @session " +
            "AND destination_uri = @destination AND revoked_utc IS NULL " +
            "AND created_utc <= @now AND expires_utc > @now";

        // Revocation is monotonic: a revoked grant is never reactivated.
        public const string RevokeSession =
            "UPDATE ogl_hg_roaming_grants SET revoked_utc = @now " +
            "WHERE resident_id = @resident AND session_id = @session " +
            "AND revoked_utc IS NULL";
    }
}
