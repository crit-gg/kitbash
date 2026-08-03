#!/usr/bin/env bash
#
# Publishes Workbench and packs it into a release feed.
#
#   build/release.sh <feed directory> [linux|win]
#
# vpk cross compiles between Linux and Windows, and dotnet publishes for both from
# either, so one machine builds both releases. Building is not testing: a package made
# here for the other OS has not been run anywhere.
#
# Packing straight into the feed means the previous release is already beside the new
# one, so deltas are generated with no download step. vpk upload local does the same job
# against a folder and is the shape an s3 deploy would take later.
#
# vpk comes from `dotnet tool install -g vpk` and wants the .NET 8 SDK or later.

set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
project="$root/src/Workbench/Workbench.csproj"

pack_id="Slopworks.Workbench"
pack_title="Workbench"
pack_authors="Slopworks"

feed="${1:-}"
only="${2:-both}"

if [ -z "$feed" ]; then
  echo "Usage: build/release.sh <feed directory> [linux|win]" >&2
  exit 2
fi

if ! command -v vpk >/dev/null 2>&1; then
  echo "vpk is not on PATH. Install it with: dotnet tool install -g vpk" >&2
  exit 127
fi

mkdir -p "$feed"
feed=$(cd "$feed" && pwd)

version=$(dotnet msbuild "$project" -getProperty:Version -nologo | tr -d '[:space:]')

if [ -z "$version" ]; then
  echo "Could not read Version from $project" >&2
  exit 1
fi

echo "Workbench $version into $feed"

release() {
  local os=$1 runtime=$2 exe=$3 icon=$4 extra=("${@:5}")
  local out="$root/publish/$os"

  rm -rf "$out"

  # Self contained, so nothing on the machine has to have .NET. No trimming and no
  # Native AOT, because Avalonia's reflection roots would have to be protected by hand.
  dotnet publish "$project" \
    --configuration Release \
    --runtime "$runtime" \
    --self-contained \
    --output "$out"

  # The channel is left at its default, which is the OS short name, so this writes
  # releases.linux.json and releases.win.json beside the packages.
  #
  # --mainExe is required rather than optional, because it otherwise defaults to the
  # pack id and the executable is named after the project.
  vpk "[$os]" pack \
    --packId "$pack_id" \
    --packTitle "$pack_title" \
    --packAuthors "$pack_authors" \
    --packVersion "$version" \
    --packDir "$out" \
    --runtime "$runtime" \
    --mainExe "$exe" \
    --icon "$root/$icon" \
    --outputDir "$feed" \
    "${extra[@]}"
}

# --categories is Linux only and names a freedesktop menu section. It defaults to
# Utility, which is not where a person looks for this.
linux_release() { release linux linux-x64 Workbench icons/icon_256x256.png --categories Development; }
win_release() { release win win-x64 Workbench.exe icons/icon.ico; }

case "$only" in
  linux) linux_release ;;
  win)   win_release ;;
  both)
    linux_release
    win_release
    ;;
  *)
    echo "Second argument must be linux, win or both." >&2
    exit 2
    ;;
esac

echo
echo "Done. Point updates.feed at $feed"
ls -1 "$feed"
