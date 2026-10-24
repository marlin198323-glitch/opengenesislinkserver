#!/usr/bin/env bash
# OpenGenesisLINK node update staging - never modifies a running installation.
set -euo pipefail
export GIT_TERMINAL_PROMPT=0
export GIT_CONFIG_NOSYSTEM=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

if [[ $# -ne 3 ]]; then
  echo "Usage: ogl-node-stage-update.sh <staging-root> <live-bin-dir> <build:0|1>" >&2
  exit 64
fi
root="$(realpath -m -- "$1")"
live="$(realpath -m -- "$2")"
# The simulator's bin directory is inside its deployed checkout. Exclude
# the entire deployed tree, not just bin, from untrusted staging downloads.
deployment="$(realpath -m -- "$live/..")"
build="$3"
if [[ -z "$1" || -z "$2" || "$root" != /* || "$root" == / || "$live" == / ||
      "$root" == "$live" || "$root" == "$live/"* ||
      "$live" == "$root/"* || "$root" == "$deployment" ||
      "$root" == "$deployment/"* || "$deployment" == "$root/"* ||
      ! "$build" =~ ^[01]$ ]]; then
  echo "Unsafe staging root/live directory relationship" >&2
  exit 65
fi
if [[ -L "$root" || -L "$live" ]]; then
  echo "Symlink roots are not supported" >&2
  exit 65
fi

mkdir -p -- "$root"
umask 077
exec 9>"$root/.stage-update.lock"
if ! flock -n 9; then
  echo "Another staged update already runs in this directory" >&2
  exit 66
fi

stage="$(mktemp -d -- "$root/.incoming.XXXXXXXX")"
cleanup() { if [[ -d "$stage" ]]; then rm -rf -- "$stage"; fi; }
trap cleanup EXIT

git -c protocol.file.allow=never clone --depth 1 --single-branch --branch main \
  https://github.com/opengenesislink/opengenesislinkserver.git "$stage/source"
sha="$(git -C "$stage/source" rev-parse --verify HEAD)"
if [[ ! "$sha" =~ ^[[:xdigit:]]{40}$ ]]; then
  echo "Cannot verify downloaded Git commit" >&2
  exit 67
fi

python3 "$stage/source/tools/ops/ogl-config-example-sync.py" \
  --active-bin "$live" --downloaded-bin "$stage/source/bin" \
  --output "$stage/config-review"

if [[ "$build" == 1 ]]; then
  (
    cd "$stage/source"
    bash runprebuild.sh
    dotnet build OpenSim.sln --configuration Release --nologo
  )
fi

printf 'commit=%s\nsource=https://github.com/opengenesislink/opengenesislinkserver.git\nbranch=main\nbuilt=%s\n' \
  "$sha" "$build" > "$stage/manifest.txt"
# Reuse a stage only if its manifest proves the same checked-out
# commit and the requested build state. Otherwise fail closed; never trust
# an incomplete or corrupted existing directory as a verified update.
final="$root/$sha"
if [[ -e "$final" || -L "$final" ]]; then
  if [[ -L "$final" || ! -f "$final/manifest.txt" ]] ||
     ! grep -Fxq "commit=$sha" "$final/manifest.txt" ||
     ! grep -Fxq "source=https://github.com/opengenesislink/opengenesislinkserver.git" "$final/manifest.txt" ||
     { [[ "$build" == "1" ]] && ! grep -Fxq "built=1" "$final/manifest.txt"; }; then
    echo "Existing update stage is unverified or not built: $final" >&2
    exit 68
  fi
  # EXIT cleanup intentionally remains enabled to remove the unused clone.
  echo "Verified commit already staged: $sha"
else
  mv -- "$stage" "$final"
  # The temporary tree has moved to its verified final location.
  stage=""
fi
echo "Staged commit: $sha"
echo "Staged path: $final"
echo "Live service and active .ini files were not touched."
