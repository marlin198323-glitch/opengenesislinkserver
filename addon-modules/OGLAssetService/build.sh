#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in
  Release|Debug) ;;
  *) echo "Expected Release or Debug" >&2; exit 2 ;;
esac

# The addon uses OutputPath=../../bin in its csproj because ROBUST loads
# plugins directly from the shared OpenGenesisLINK bin/ directory.
# Do not expect the old addons/OGLAssetService/bin/<config>/net8.0 path
# or copy assemblies over those already built into shared bin/.
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c "$CONFIG"

OUT="$ROOT/bin"
for dll in \
  OpenGenesisLINK.Addons.OGLAssetService.dll \
  OpenSim.Services.FSAssetService.dll \
  StackExchange.Redis.dll \
  Pipelines.Sockets.Unofficial.dll \
  Microsoft.Data.Sqlite.dll \
  Microsoft.Extensions.Logging.Abstractions.dll; do
  if [ ! -s "$OUT/$dll" ]; then
    echo "Missing required OGL AssetService runtime assembly: $OUT/$dll" >&2
    exit 1
  fi
done

echo "OGL AssetService assemblies verified in bin/ (ROBUST remains on its configured asset provider)."
