-- FSAssets metadata export from a QUIESCED and SNAPSHOTTED database.
-- Default Realm is fsassets; replace identifier if your AssetService Realm differs.
-- Execute with mysql/mariadb batch mode and --skip-column-names.
-- Do not run while the source asset writer is active.
SELECT LOWER(CAST(id AS CHAR)), LOWER(CAST(hash AS CHAR))
FROM fsassets
ORDER BY id;
