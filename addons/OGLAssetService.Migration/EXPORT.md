# FSAssets metadata export (offline only)

The checked FSAssets adapters are `OpenSim/Data/MySQL/MySQLFSAssetData.cs` (configurable Realm, default `fsassets`) and `OpenSim/Data/PGSQL/PGSQLFSAssetData.cs` (hard-coded `fsassets` table). Both read `id` and `hash`. The SQLite FSAssets provider has not been confirmed in this repository: SQLite migration is unsupported until verified.

**Before export:** stop ROBUST/FSAssets writes and wait for the spool to drain; take a consistent SQL backup and a matching FSAssets directory snapshot. Export from the database snapshot, not a moving production database. Keep backup and TSV private.

MariaDB/MySQL, with connection arguments and credentials supplied securely through your local client configuration:

```bash
mysql --batch --raw --skip-column-names --database=YOUR_DB \
  < addons/OGLAssetService.Migration/sql/mysql-mariadb-export.sql \
  > /private/snapshot/uuid-hash.tsv
```

PostgreSQL:

```bash
psql -X -A -t -F $'\\t' --dbname=YOUR_DB \
  -f addons/OGLAssetService.Migration/sql/postgresql-export.sql \
  > /private/snapshot/uuid-hash.tsv
```

Confirm the SQL table name and provider schema before running; adjust the MySQL table identifier for a nondefault Realm. Never place credentials in Git or command arguments. On PostgreSQL set `ON_ERROR_STOP=1` in the psql command so SQL errors cannot be mistaken for an empty export.

Verify the snapshot:

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- --verify-metadata /private/snapshot/fsassets /private/snapshot/uuid-hash.tsv
```

Add `--osgrid` only when `UseOsgridFormat=true`. The verifier is read-only; it does not migrate SQL metadata, prove that the export is complete, or perform cutover/rollback. A successful audit alone does not authorize production switchover.
