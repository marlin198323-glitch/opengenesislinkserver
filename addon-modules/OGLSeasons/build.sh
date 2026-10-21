#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in Release|Debug) ;; *) echo "Expected Release or Debug" >&2; exit 2;; esac
dotnet build addons/OGLSeasons/OpenGenesisLINK.Addons.OGLSeasons.csproj -c "$CONFIG"
OUT="addons/OGLSeasons/bin/$CONFIG/net8.0"
test -s "$OUT/OpenGenesisLINK.Addons.OGLSeasons.dll" || {
  echo "OGLSeasons addon DLL missing" >&2
  exit 1
}
mkdir -p bin
cp "$OUT/OpenGenesisLINK.Addons.OGLSeasons.dll" bin/
echo "OGLSeasons in bin installiert (standardmaessig DEAKTIVIERT)."
