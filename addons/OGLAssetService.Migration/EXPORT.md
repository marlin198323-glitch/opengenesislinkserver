# FSAssets offline metadata export and verification

The MySQL/MariaDB adapter uses the configured `Realm` (default `fsassets`), while the inspected PostgreSQL adapter uses `fsassets`. Both have `id` and `hash` columns. SQLite FSAssets support is **not verified**.

**Safety:** Stop all FSAssets writers, drain the spool, and take matching database and filesystem snapshots. Run the export against the *offline database snapshot*, not a changing production database. Keep all exports private. Neither command below imports data or changes the live service.

## MariaDB / MySQL

Supply credentials via a protected client option file, not Git or shell arguments. Adjust the table name in the SQL file if `Realm` differs from `fsassets`.

```bash
mysql --batch --raw --skip-column-names --database=YOUR_DB \
  < addons/OGLAssetService.Migration/sql/mysql-mariadb-export.sql \
  > /private/snapshot/uuid-hash.tsv
```

## PostgreSQL

```bash
psql -X -v ON_ERROR_STOP=1 -A -t -F "$(printf '\t')" --dbname=YOUR_DB \
  -f addons/OGLAssetService.Migration/sql/postgresql-export.sql \
  > /private/snapshot/uuid-hash.tsv
```

## Verification

First independently obtain the total row count from the **same SQL snapshot** (`SELECT COUNT(*) FROM fsassets;`, or the configured MySQL realm). Then run the audit:

```bash
dotnet run --project addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj -c Release -- \
  --verify-metadata /private/snapshot/fsassets /private/snapshot/uuid-hash.tsv --expected-rows YOUR_COUNT
```

Use `--osgrid` as an additional option only if `UseOsgridFormat=true`. Exit code 0 requires matching expected row count, no duplicate or invalid UUID/hash rows, and all referenced payloads present and checksum-valid. A clean audit does not prove that the SQL backup can be restored, that the spool was drained, or that the running asset service reads back correctly. Database import, service cutover and rollback remain unimplemented.
