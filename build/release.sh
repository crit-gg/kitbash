#!/usr/bin/env bash
#
# Publishes Kitbash and packs it into a release feed.
#
#   build/release.sh <feed directory> [linux|win|osx|both]
#
# KITBASH_VERSION overrides the version, and is how CI passes the one it worked out from
# git history. Unset, the placeholder in Directory.Build.props is used.
#
# vpk cross compiles between Linux and Windows, and dotnet publishes for both from
# either, so one machine builds both releases. Building is not testing: a package made
# here for the other OS has not been run anywhere.
#
# macOS is the exception and it is not a soft one. vpk registers the [osx] pack command
# only when it is itself running on a Mac, so anywhere else it is an unrecognised command
# rather than a failure that explains itself. So both means linux and win, and osx is
# asked for by name on a Mac.
#
# Packing straight into the feed means the previous release is already beside the new
# one, so deltas are generated with no download step. vpk upload local does the same job
# against a folder and is the shape an s3 deploy would take later.
#
# vpk comes from `dotnet tool install -g vpk` and wants the .NET 8 SDK or later.

set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
project="$root/src/Kitbash/Kitbash.csproj"

pack_id="Kitbash"
pack_title="Kitbash"
pack_authors="Kitbash"

feed="${1:-}"
only="${2:-both}"

if [ -z "$feed" ]; then
  echo "Usage: build/release.sh <feed directory> [linux|win|osx|both]" >&2
  exit 2
fi

if ! command -v vpk >/dev/null 2>&1; then
  echo "vpk is not on PATH. Install it with: dotnet tool install -g vpk" >&2
  exit 127
fi

mkdir -p "$feed"
feed=$(cd "$feed" && pwd)

# CI works the version out from git history and sets it here. A local run has no history
# to read, so it falls back to the placeholder in Directory.Build.props.
version="${KITBASH_VERSION:-}"

if [ -z "$version" ]; then
  version=$(dotnet msbuild "$project" -getProperty:Version -nologo | tr -d '[:space:]')
fi

if [ -z "$version" ]; then
  echo "Could not read Version from $project" >&2
  exit 1
fi

if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+ ]]; then
  echo "Version $version does not start with three numbers." >&2
  exit 1
fi

echo "Kitbash $version into $feed"

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
    --output "$out" \
    -p:Version="$version"

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
linux_release() { release linux linux-x64 Kitbash icons/icon_256x256.png --categories Development; }
win_release() { release win win-x64 Kitbash.exe icons/icon.ico; }

# --icon must be an icns here, which vpk enforces. --bundleId is named rather than left to
# default, since the default is com.{packAuthors}.{packId} and both are Kitbash. --mainExe
# names the program inside the publish output, and vpk builds the .app around it.
osx_release() {
  if [ "$(uname -s)" != "Darwin" ]; then
    echo "vpk registers [osx] pack only on a Mac, so this cannot run on $(uname -s)." >&2
    exit 3
  fi

  release osx osx-arm64 Kitbash icons/icon.icns --bundleId run.kitbash.launcher
}

case "$only" in
  linux) linux_release ;;
  win)   win_release ;;
  osx)   osx_release ;;
  both)
    linux_release
    win_release
    ;;
  *)
    echo "Second argument must be linux, win, osx or both." >&2
    exit 2
    ;;
esac

echo
echo "Done. Point updates.feed at $feed"
ls -1 "$feed"
