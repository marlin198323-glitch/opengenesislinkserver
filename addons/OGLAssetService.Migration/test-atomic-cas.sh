#!/usr/bin/env bash
# Disposable SQL compare-and-swap regression for FSAssets metadata.
# Runs only against local CI databases. Never accepts a remote host.
set -euo pipefail
provider="${1:-}"
if [[ "$provider" != mysql && "$provider" != postgres ]]; then
  echo "Usage: $0 mysql|postgres" >&2; exit 2
fi
id="123e4567-e89b-12d3-a456-426614174987"
old="$(printf original | sha256sum | cut -d' ' -f1)"
next="$(printf replacement-a | sha256sum | cut -d' ' -f1)"
other="$(printf replacement-b | sha256sum | cut -d' ' -f1)"
table="ogl_fsassets_cas_test"
if [[ "$provider" == mysql ]]; then
  : "${MYSQL_PWD:?Set MYSQL_PWD for the disposable local CI instance}"
  cli=(mysql --protocol=tcp -h 127.0.0.1 -P 3306 -u root -N -B)
  "${cli[@]}" -e "CREATE DATABASE IF NOT EXISTS ogl_assetservice_cas_test;"
  cli+=(ogl_assetservice_cas_test)
  trap '"${cli[@]}" -e "DROP TABLE IF EXISTS ogl_fsassets_cas_test;" >/dev/null 2>&1 || true' EXIT
  "${cli[@]}" -e "DROP TABLE IF EXISTS $table; CREATE TABLE $table (id CHAR(36) PRIMARY KEY, hash CHAR(64) NOT NULL); INSERT INTO $table VALUES ('$id','$old');"
  cas() { "${cli[@]}" -e "UPDATE $table SET hash='$2' WHERE id='$id' AND hash='$1'; SELECT ROW_COUNT();" | tail -1; }
  current() { "${cli[@]}" -e "SELECT hash FROM $table WHERE id='$id';"; }
  absent() { "${cli[@]}" -e "UPDATE $table SET hash='$next' WHERE id='00000000-0000-0000-0000-000000000000' AND hash='$old'; SELECT ROW_COUNT();" | tail -1; }
else
  : "${PGPASSWORD:?Set PGPASSWORD for the disposable local CI instance}"
  cli=(psql -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U postgres -d postgres -A -t)
  trap '"${cli[@]}" -c "DROP TABLE IF EXISTS ogl_fsassets_cas_test;" >/dev/null 2>&1 || true' EXIT
  "${cli[@]}" -c "DROP TABLE IF EXISTS $table; CREATE TABLE $table (id UUID PRIMARY KEY, hash VARCHAR(64) NOT NULL); INSERT INTO $table VALUES ('$id','$old');" >/dev/null
  cas() { "${cli[@]}" -c "WITH updated AS (UPDATE $table SET hash='$2' WHERE id='$id' AND hash='$1' RETURNING id) SELECT count(*) FROM updated;"; }
  current() { "${cli[@]}" -c "SELECT hash FROM $table WHERE id='$id';"; }
  absent() { "${cli[@]}" -c "WITH updated AS (UPDATE $table SET hash='$next' WHERE id='00000000-0000-0000-0000-000000000000' AND hash='$old' RETURNING id) SELECT count(*) FROM updated;"; }
fi
[[ "$(absent)" == 0 ]] || { echo "Missing ID incorrectly updated" >&2; exit 1; }
[[ "$(cas "$other" "$next")" == 0 ]] || { echo "Stale expected hash incorrectly updated" >&2; exit 1; }
[[ "$(cas "$old" "$next")" == 1 ]] || { echo "Valid CAS did not update one row" >&2; exit 1; }
[[ "$(current)" == "$next" ]] || { echo "Readback differs from published hash" >&2; exit 1; }
[[ "$(cas "$old" "$other")" == 0 ]] || { echo "Stale CAS overwrote new hash" >&2; exit 1; }
# Two concurrent SQL sessions race for the same expected hash.
if [[ "$provider" == mysql ]]; then
  "${cli[@]}" -e "UPDATE $table SET hash='$old' WHERE id='$id';"
else
  "${cli[@]}" -c "UPDATE $table SET hash='$old' WHERE id='$id';" >/dev/null
fi
cas "$old" "$next" > "$TMPDIR_CAS_A" &
pid_a=$!
cas "$old" "$other" > "$TMPDIR_CAS_B" &
pid_b=$!
wait "$pid_a"; wait "$pid_b"
a="$(cat "$TMPDIR_CAS_A")"; b="$(cat "$TMPDIR_CAS_B")"
[[ "$a" =~ ^[01]$ && "$b" =~ ^[01]$ && $((a+b)) -eq 1 ]] || {
  echo "Concurrent CAS expected one winner; got $a and $b" >&2; exit 1;
}
actual="$(current)"
[[ "$actual" == "$next" || "$actual" == "$other" ]] || { echo "Unexpected winning hash" >&2; exit 1; }
echo "$provider atomic CAS SQL regression passed"
