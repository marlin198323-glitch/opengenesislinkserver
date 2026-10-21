#!/usr/bin/env bash
set -euo pipefail

# OGLSeasons is an INonSharedRegionModule. A plain server build must never
# install its DLL into the shared bin, because ROBUST does not host the
# /OpenSim/RegionModules Mono.Addins extension point.
if [[ "${1:-}" == "--help" ]]; then
  echo "Usage: $0 [Release|Debug] [--sim-bin /absolute/path/to/simulator/bin]"
  echo "Without --sim-bin, the DLL is only compiled, not installed."
  exit 0
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in
  Release|Debug) ;;
  *) echo "Expected Release or Debug" >&2; exit 2 ;;
esac

if (( $# != 0 && $# != 1 && $# != 3 )); then
  echo "Usage: $0 [Release|Debug] [--sim-bin /absolute/path/to/simulator/bin]" >&2
  exit 2
fi
if (( $# == 3 )) && [[ "$2" != "--sim-bin" ]]; then
  echo "Expected --sim-bin before simulator path" >&2
  exit 2
fi

dotnet build addons/OGLSeasons/OpenGenesisLINK.Addons.OGLSeasons.csproj -c "$CONFIG"
OUT="addons/OGLSeasons/bin/$CONFIG/net8.0"
DLL="$OUT/OpenGenesisLINK.Addons.OGLSeasons.dll"
test -s "$DLL" || {
  echo "OGLSeasons addon DLL missing" >&2
  exit 1
}

if (( $# == 3 )); then
  SIM_BIN="$(realpath -m -- "$3")"
  # Fail closed on the known ROBUST runtime directory. The explicit option
  # and region framework assembly both serve as deployment guardrails.
  if [[ "${SIM_BIN,,}" == */robust/bin ]]; then
    echo "Refusing to install region addon into ROBUST: $SIM_BIN" >&2
    exit 2
  fi
  if [[ "$SIM_BIN" != */bin || ! -f "$SIM_BIN/OpenSim.Region.Framework.dll" ]]; then
    echo "Expected an existing simulator bin with OpenSim.Region.Framework.dll: $SIM_BIN" >&2
    exit 2
  fi
  install -m 0644 "$DLL" "$SIM_BIN/OpenGenesisLINK.Addons.OGLSeasons.dll"
  echo "OGLSeasons installed only in simulator bin: $SIM_BIN"
else
  echo "OGLSeasons built: $DLL"
  echo "Not installed in shared bin. To install explicitly:"
  echo "  bash addon-modules/OGLSeasons/build.sh $CONFIG --sim-bin /opt/developer/bin"
fi
