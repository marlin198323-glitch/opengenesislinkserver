-- FSAssets metadata export from a QUIESCED and SNAPSHOTTED database.
-- PGSQLFSAssetData currently uses the hard-coded fsassets table.
-- Use psql -A -t -F E'\\t' and redirect output to a private snapshot file.
SELECT LOWER(id::text), LOWER(hash::text)
FROM fsassets
ORDER BY id;
