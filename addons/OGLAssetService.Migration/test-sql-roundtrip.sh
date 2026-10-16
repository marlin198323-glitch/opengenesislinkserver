#!/usr/bin/env bash
# Isolated metadata export -> restore -> export comparison, never production.
set -euo pipefail
provider="${1:-}"
if [[ "$provider" != "mysql" && "$provider" != "postgres" ]]; then
  echo "Usage: $0 mysql|postgres" >&2
  exit 2
fi
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
id1='123e4567-e89b-12d3-a456-426614174000'
id2='123e4567-e89b-12d3-a456-426614174001'
hash1="$(printf 'asset-one' | sha256sum | cut -d' ' -f1)"
hash2="$(printf 'asset-two' | sha256sum | cut -d' ' -f1)"
mkdir -p "$work/fsassets"
for item in 1 2; do
  if [[ "$item" == 1 ]]; then hash="$hash1"; data='asset-one'; else hash="$hash2"; data='asset-two'; fi
  folder="$work/fsassets/${hash:0:2}/${hash:2:2}/${hash:4:2}/${hash:6:4}"
  mkdir -p "$folder"
  printf '%s' "$data" > "$folder/$hash"
done
if [[ "$provider" == "mysql" ]]; then
  : "${MYSQL_PWD:?MYSQL_PWD must be set for disposable test database}"
  mysql=(mysql --protocol=tcp --host=127.0.0.1 --port=3306 --user=root --batch --raw --skip-column-names)
  "${mysql[@]}" -e 'CREATE DATABASE IF NOT EXISTS ogl_assetservice_test;'
  trap '"${mysql[@]}" -e "DROP DATABASE IF EXISTS ogl_assetservice_test;" || true; rm -rf "$work"' EXIT
  "${mysql[@]}" ogl_assetservice_test -e 'CREATE TABLE fsassets (id CHAR(36) PRIMARY KEY, hash CHAR(64) NOT NULL);'
  "${mysql[@]}" ogl_assetservice_test -e "INSERT INTO fsassets VALUES ('$id1','$hash1'),('$id2','$hash2');"
  "${mysql[@]}" ogl_assetservice_test < "$root/addons/OGLAssetService.Migration/sql/mysql-mariadb-export.sql" > "$work/before.tsv"
  "${mysql[@]}" ogl_assetservice_test -e 'CREATE TABLE restored LIKE fsassets; INSERT INTO restored SELECT * FROM fsassets; TRUNCATE TABLE fsassets; INSERT INTO fsassets SELECT * FROM restored;'
  "${mysql[@]}" ogl_assetservice_test < "$root/addons/OGLAssetService.Migration/sql/mysql-mariadb-export.sql" > "$work/after.tsv"
else
  : "${PGPASSWORD:?PGPASSWORD must be set for disposable test database}"
  psql=(psql -X -v ON_ERROR_STOP=1 --host=127.0.0.1 --port=5432 --username=postgres --dbname=postgres)
  "${psql[@]}" -c 'DROP TABLE IF EXISTS fsassets; CREATE TABLE fsassets (id UUID PRIMARY KEY, hash VARCHAR(64) NOT NULL);'
  trap '"${psql[@]}" -c "DROP TABLE IF EXISTS fsassets;" || true; rm -rf "$work"' EXIT
  "${psql[@]}" -c "INSERT INTO fsassets VALUES ('$id1','$hash1'),('$id2','$hash2');"
  "${psql[@]}" -A -t -F "$(printf '\t')" -f "$root/addons/OGLAssetService.Migration/sql/postgresql-export.sql" > "$work/before.tsv"
  "${psql[@]}" -c 'BEGIN; CREATE TEMP TABLE restored AS TABLE fsassets; TRUNCATE fsassets; INSERT INTO fsassets SELECT * FROM restored; COMMIT;'
  "${psql[@]}" -A -t -F "$(printf '\t')" -f "$root/addons/OGLAssetService.Migration/sql/postgresql-export.sql" > "$work/after.tsv"
fi
cmp "$work/before.tsv" "$work/after.tsv"
dotnet run --project "$root/addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj" -c Release --no-build -- --verify-metadata "$work/fsassets" "$work/after.tsv" --expected-rows 2
echo "$provider metadata roundtrip passed"
