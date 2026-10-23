#!/usr/bin/env bash
# CI-only runtime smoke for the OGLAssetConnector. Isolated SQL/database and
# disposable asset directories: NEVER supply a production connection string.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
test -s bin/OpenGenesisLINK.Addons.OGLAssetService.dll
test -s bin/Robust.dll

WORK="$(mktemp -d "${RUNNER_TEMP:-/tmp}/ogl-robust-smoke.XXXXXXXX")"
ROBUST_PID=""
STAGED_INI="$ROOT/bin/Robust.OGLSmoke.Tests.ini"
if [ -e "$STAGED_INI" ]; then
  echo "Refusing to overwrite existing staging smoke configuration: $STAGED_INI" >&2
  exit 1
fi
cleanup() {
  if [ -n "$ROBUST_PID" ]; then
    kill "$ROBUST_PID" 2>/dev/null || true
    wait "$ROBUST_PID" 2>/dev/null || true
  fi
  rm -f -- "$STAGED_INI"
  rm -rf -- "$WORK"
}
trap cleanup EXIT

mkdir -p "$WORK/registry" "$WORK/include" "$WORK/fsassets/data" "$WORK/fsassets/tmp" "$WORK/ogl"
cat > "$WORK/Robust.OGLSmoke.ini" <<EOF
[Startup]
  RegistryLocation = "$WORK/registry"
  ConfigDirectory = "$WORK/include"
[ServiceList]
  AssetServiceConnector = "19495/OpenSim.Server.Handlers.dll:AssetServiceConnector"
[Network]
  port = 19495
[DatabaseService]
  StorageProvider = "OpenSim.Data.MySQL.dll"
  ConnectionString = "Data Source=127.0.0.1;Port=3306;Database=ogl_assetservice_smoke;User ID=root;Password=ogl-test-only;Old Guids=true;SslMode=None;"
[AssetService]
  LocalServiceModule = "OpenGenesisLINK.Addons.OGLAssetService.dll:OGLAssetConnector"
  BaseDirectory = "$WORK/fsassets/data"
  SpoolDirectory = "$WORK/fsassets/tmp"
  ShowConsoleStats = false
  DefaultAssetLoader = ""
[OGLAssetService]
  Enabled = true
  Mode = "compatibility"
  MemoryCacheMiB = 0
  SingleWriterCacheEnabled = false
  DiskCacheEnabled = false
  RedisEnabled = false
  ExperimentalAtomicUpdatesEnabled = false
  ParallelStoreEnabled = true
  ParallelReadPreferred = true
  ParallelObjectDirectory = "$WORK/ogl/objects"
  ParallelIndexPath = "$WORK/ogl/locations.sqlite"
[OGLAssetServiceAdmin]
  Enabled = false
EOF

cp -- "$WORK/Robust.OGLSmoke.ini" "$STAGED_INI"

start_robust() {
  # exec makes the background PID the actual ROBUST process; cleanup cannot
  # accidentally leave a detached process holding the CI port.
  (cd bin && exec dotnet Robust.dll -inifile Robust.OGLSmoke.Tests.ini) > "$WORK/robust.log" 2>&1 &
  ROBUST_PID=$!
}

assert_ready() {
  local ready=0
  # A loaded DLL alone is insufficient. Require the OGL constructor's own
  # log message and a reachable HTTP listener after SQL initializes.
  for _ in $(seq 1 120); do
    if grep -Fq '[OGL-ASSETS]: OGL AssetService geladen;' "$WORK/robust.log"; then
      if python3 - <<'PY'
import socket
try:
    with socket.create_connection(("127.0.0.1", 19495), timeout=1):
        pass
except OSError:
    raise SystemExit(1)
PY
      then
        ready=1
        break
      fi
    fi
    if ! kill -0 "$ROBUST_PID" 2>/dev/null; then
      echo "::error::ROBUST exited before OGL AssetService activated" >&2
      tail -n 100 "$WORK/robust.log" >&2
      exit 1
    fi
    sleep 0.5
  done

  if [ "$ready" -ne 1 ]; then
    echo "::error::OGL connector was not activated or HTTP listener was unreachable" >&2
    tail -n 100 "$WORK/robust.log" >&2
    exit 1
  fi
  if ! grep -Fq 'authoritative=FSAssets; mode=compatibility; local_cache=False; redis_coordination=False; atomic_updates=False' "$WORK/robust.log"; then
    echo "::error::Unexpected OGL AssetService runtime safety flags" >&2
    tail -n 100 "$WORK/robust.log" >&2
    exit 1
  fi
}

start_robust
assert_ready

# Exercise the actual public asset HTTP handlers and the asynchronous FSAssets
# spool writer against throwaway MariaDB, not just a TCP health check.
python3 tools/ci/verify_ogl_assetservice_http_persistence.py \
  write "$WORK/fixtures.json" "$WORK/fsassets/data" \
  --ogl-objects "$WORK/ogl/objects" --ogl-index "$WORK/ogl/locations.sqlite"

# Force the next read through a NEW OGL connector/ROBUST process so that
# memory-only state cannot mask a failed SQL/object-store persistence.
kill "$ROBUST_PID"
wait "$ROBUST_PID" 2>/dev/null || true
ROBUST_PID=""
: > "$WORK/robust.log"
start_robust
assert_ready
python3 tools/ci/verify_ogl_assetservice_http_persistence.py \
  read "$WORK/fixtures.json" "$WORK/fsassets/data" \
  --ogl-objects "$WORK/ogl/objects" --ogl-index "$WORK/ogl/locations.sqlite"

# Strict proof that the new read path actually uses the OGL copy:
# hide ONLY disposable CI FSAssets folders, start a new ROBUST instance,
# and confirm that the same HTTP asset requests still work via OGL.
kill "$ROBUST_PID"
wait "$ROBUST_PID" 2>/dev/null || true
ROBUST_PID=""
mv "$WORK/fsassets/data" "$WORK/fsassets/data-hidden"
mv "$WORK/fsassets/tmp" "$WORK/fsassets/tmp-hidden"
mkdir -p "$WORK/fsassets/data" "$WORK/fsassets/tmp/spool"
: > "$WORK/robust.log"
start_robust
assert_ready
python3 tools/ci/verify_ogl_assetservice_http_persistence.py \
  mirror-read "$WORK/fixtures.json" "$WORK/fsassets/data" \
  --ogl-objects "$WORK/ogl/objects" --ogl-index "$WORK/ogl/locations.sqlite"

echo "OGL AssetService isolated ROBUST HTTP dual-store + OGL-first read smoke PASS"
