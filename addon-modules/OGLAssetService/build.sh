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

# ROBUST loads the addon dynamically from shared bin/ and does not always
# include its NuGet runtime native assets in Robust.deps.json. SQLitePCL
# needs the correct native e_sqlite3 beside Robust.dll at process startup.
# Install only the package-restored binary for this host RID.
case "$(uname -s)" in
  Linux) sqlite_os="linux"; sqlite_lib="libe_sqlite3.so" ;;
  Darwin) sqlite_os="osx"; sqlite_lib="libe_sqlite3.dylib" ;;
  MINGW*|MSYS*|CYGWIN*) sqlite_os="win"; sqlite_lib="e_sqlite3.dll" ;;
  *) echo "Unsupported SQLite runtime host: $(uname -s)" >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64) sqlite_arch="x64" ;;
  aarch64|arm64) sqlite_arch="arm64" ;;
  *) echo "Unsupported SQLite runtime architecture: $(uname -m)" >&2; exit 1 ;;
esac
sqlite_rid="${sqlite_os}-${sqlite_arch}"
nuget_root="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: //p' | head -n 1)"
sqlite_pkg="${nuget_root%/}/sqlitepclraw.lib.e_sqlite3"
if [ ! -d "$sqlite_pkg" ]; then
  echo "Restored SQLitePCLRaw.lib.e_sqlite3 native package not found: $sqlite_pkg" >&2
  exit 1
fi
sqlite_native="$(find "$sqlite_pkg" -type f -path "*/runtimes/$sqlite_rid/native/$sqlite_lib" -print | sort -V | tail -n 1)"
if [ -z "$sqlite_native" ] || [ ! -s "$sqlite_native" ]; then
  echo "Missing native $sqlite_lib in SQLitePCLRaw for $sqlite_rid" >&2
  exit 1
fi
cp -f -- "$sqlite_native" "$OUT/$sqlite_lib"
chmod 644 "$OUT/$sqlite_lib"
echo "Installed SQLite native runtime for $sqlite_rid into shared ROBUST bin/."

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
