#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in Release|Debug) ;; *) echo "Expected Release or Debug" >&2; exit 2;; esac
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c "$CONFIG"
OUT="addons/OGLAssetService/bin/$CONFIG/net8.0"
test -s "$OUT/OpenGenesisLINK.Addons.OGLAssetService.dll" || { echo "AssetService DLL missing" >&2; exit 1; }
mkdir -p bin
cp "$OUT/OpenGenesisLINK.Addons.OGLAssetService.dll" bin/
# Copy only the addon's NuGet runtime dependencies; do not replace OpenSim binaries.
for dll in StackExchange.Redis.dll Pipelines.Sockets.Unofficial.dll Microsoft.Extensions.Logging.Abstractions.dll; do
  if [ ! -s "$OUT/$dll" ]; then echo "Missing required OGL AssetService runtime dependency: $OUT/$dll" >&2; exit 1; fi
  cp "$OUT/$dll" bin/
done
echo "OGL AssetService installed in bin/ (not activated in ROBUST)."
