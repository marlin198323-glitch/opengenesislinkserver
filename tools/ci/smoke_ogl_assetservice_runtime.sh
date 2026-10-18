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
cleanup() {
  if [ -n "$ROBUST_PID" ]; then
    kill "$ROBUST_PID" 2>/dev/null || true
    wait "$ROBUST_PID" 2>/dev/null || true
  fi
  rm -rf -- "$WORK"
}
trap cleanup EXIT

mkdir -p "$WORK/registry" "$WORK/include" "$WORK/fsassets/data" "$WORK/fsassets/tmp"
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
[OGLAssetServiceAdmin]
  Enabled = false
EOF

(cd bin && dotnet Robust.dll -inifile "$WORK/Robust.OGLSmoke.ini") > "$WORK/robust.log" 2>&1 &
ROBUST_PID=$!

# A loaded DLL alone is insufficient. Require the OGL constructor's own log
# message and a reachable HTTP listener after the SQL provider initializes.
ready=0
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

echo "OGL AssetService isolated ROBUST runtime smoke PASS"
