#!/usr/bin/env bash
#
# Publishes Kitbash and packs it into a release feed.
#
#   build/release.sh <feed directory> [linux|win|osx|both]
#
# KITBASH_VERSION overrides the version, and is how CI passes the one it worked out from
# git history. Unset, the placeholder in Directory.Build.props is used.
#
# KITBASH_SIGN_ENDPOINT, KITBASH_SIGN_ACCOUNT and KITBASH_SIGN_PROFILE turn on Azure
# Artifact Signing for the win release. All three or none. vpk carries its own signtool
# and dlib, so nothing has to be installed, and it authenticates through the Azure CLI,
# so an az login has to have happened first.
#
# vpk cross compiles between Linux and Windows, and dotnet publishes for both from
# either, so one machine builds both releases. Building is not testing: a package made
# here for the other OS has not been run anywhere.
#
# Two things break that and both are the same shape. vpk registers [osx] pack only when
# it is itself on a Mac, and --azureTrustedSignFile only when it is itself on Windows.
# So both means linux and win, osx is asked for by name on a Mac, and a signed win
# release is asked for on Windows.
#
# Packing straight into the feed means the previous release is already beside the new
# one, so deltas are generated with no download step. vpk upload local does the same job
# against a folder and is the shape an s3 deploy would take later.
#
# vpk comes from `dotnet tool install -g vpk` and wants the .NET 8 SDK or later.

set -euo pipefail

# Git Bash hands this script Windows paths such as D:\a\_temp, which bash cannot cd into,
# and it hands vpk paths such as /d/a/_temp, which a Windows program cannot read back.
# cygpath converts both ways and exists only where the problem does.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) windows=true ;;
  *) windows=false ;;
esac

to_posix() {
  if [ "$windows" = true ]; then
    cygpath -u "$1"
  else
    printf '%s' "$1"
  fi
}

to_native() {
  if [ "$windows" = true ]; then
    cygpath -w "$1"
  else
    printf '%s' "$1"
  fi
}

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
project="$root/src/Kitbash/Kitbash.csproj"

pack_id="Kitbash"
pack_title="Kitbash"
pack_authors="Kitbash"

feed_arg="${1:-}"
only="${2:-both}"

if [ -z "$feed_arg" ]; then
  echo "Usage: build/release.sh <feed directory> [linux|win|osx|both]" >&2
  exit 2
fi

if ! command -v vpk >/dev/null 2>&1; then
  echo "vpk is not on PATH. Install it with: dotnet tool install -g vpk" >&2
  exit 127
fi

feed=$(to_posix "$feed_arg")
mkdir -p "$feed"
feed=$(cd "$feed" && pwd)

sign_endpoint="${KITBASH_SIGN_ENDPOINT:-}"
sign_account="${KITBASH_SIGN_ACCOUNT:-}"
sign_profile="${KITBASH_SIGN_PROFILE:-}"
sign_file=""

if [ -n "$sign_endpoint$sign_account$sign_profile" ]; then
  if [ -z "$sign_endpoint" ] || [ -z "$sign_account" ] || [ -z "$sign_profile" ]; then
    echo "Signing needs all three of KITBASH_SIGN_ENDPOINT, KITBASH_SIGN_ACCOUNT and KITBASH_SIGN_PROFILE." >&2
    exit 2
  fi

  if [ "$windows" = false ]; then
    echo "vpk registers --azureTrustedSignFile only on Windows, so signing cannot run on $(uname -s)." >&2
    exit 4
  fi

  # The dlib requires UTF-8 with no byte order mark, which a heredoc writes.
  # ExcludeCredentials leaves AzureCliCredential alone, so DefaultAzureCredential does not
  # spend seconds probing for a managed identity a runner does not have.
  sign_file=$(mktemp)
  trap 'rm -f "$sign_file"' EXIT

  cat > "$sign_file" <<JSON
{
  "Endpoint": "$sign_endpoint",
  "CodeSigningAccountName": "$sign_account",
  "CertificateProfileName": "$sign_profile",
  "ExcludeCredentials": [
    "EnvironmentCredential",
    "ManagedIdentityCredential",
    "WorkloadIdentityCredential",
    "SharedTokenCacheCredential",
    "VisualStudioCredential",
    "VisualStudioCodeCredential",
    "AzurePowerShellCredential",
    "AzureDeveloperCliCredential",
    "InteractiveBrowserCredential"
  ]
}
JSON
fi

# CI works the version out from git history and sets it here. A local run has no history
# to read, so it falls back to the placeholder in Directory.Build.props.
version="${KITBASH_VERSION:-}"

if [ -z "$version" ]; then
  version=$(dotnet msbuild "$(to_native "$project")" -getProperty:Version -nologo | tr -d '[:space:]')
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
  dotnet publish "$(to_native "$project")" \
    --configuration Release \
    --runtime "$runtime" \
    --self-contained \
    --output "$(to_native "$out")" \
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
    --packDir "$(to_native "$out")" \
    --runtime "$runtime" \
    --mainExe "$exe" \
    --icon "$(to_native "$root/$icon")" \
    --outputDir "$(to_native "$feed")" \
    "${extra[@]}"
}

# --categories is Linux only and names a freedesktop menu section. It defaults to
# Utility, which is not where a person looks for this.
linux_release() { release linux linux-x64 Kitbash icons/icon_256x256.png --categories Development; }

# vpk signs its own binaries at points inside the pack, so signing is an argument to it
# rather than a pass over the finished feed. Unsigned is still a supported release.
win_release() {
  if [ -n "$sign_file" ]; then
    release win win-x64 Kitbash.exe icons/icon.ico --azureTrustedSignFile "$(to_native "$sign_file")"
  else
    release win win-x64 Kitbash.exe icons/icon.ico
  fi
}

# --icon must be an icns here, which vpk enforces. --mainExe names the program inside the
# publish output, and vpk builds the .app around it.
#
# The bundle declares the kitbash url scheme, and CFBundleURLTypes is the only way to say
# so, which means handing vpk the whole Info.plist. It copies the file in unchanged, it
# substitutes nothing, and it refuses --plist and --bundleId together, so the identifier
# and both versions are written here. Everything below is what vpk 1.2.0 generates on its
# own, plus the URL types block.
#
# CFBundleShortVersionString carries a fourth part where CFBundleVersion does not, which is
# vpk's own choice. Velopack reads neither at runtime, since it keeps its own sq.version.
osx_release() {
  if [ "$(uname -s)" != "Darwin" ]; then
    echo "vpk registers [osx] pack only on a Mac, so this cannot run on $(uname -s)." >&2
    exit 3
  fi

  local short="$version"

  if [[ "$version" =~ ^([0-9]+\.[0-9]+\.[0-9]+) ]]; then
    short="${BASH_REMATCH[1]}.0"
  fi

  # A template rather than a bare mktemp, which BSD mktemp refuses and which is the only
  # one this function ever meets.
  local plist
  plist=$(mktemp "${TMPDIR:-/tmp}/kitbash-plist.XXXXXX")
  trap 'rm -f "$plist"' RETURN

  cat > "$plist" <<PLIST
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleName</key>
    <string>$pack_title</string>
    <key>CFBundleIdentifier</key>
    <string>run.kitbash.launcher</string>
    <key>CFBundleVersion</key>
    <string>$version</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>Kitbash</string>
    <key>CFBundleIconFile</key>
    <string>icon.icns</string>
    <key>CFBundleShortVersionString</key>
    <string>$short</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSHighResolutionCapable</key>
    <true />
    <key>CFBundleURLTypes</key>
    <array>
      <dict>
        <key>CFBundleURLName</key>
        <string>run.kitbash.launcher</string>
        <key>CFBundleURLSchemes</key>
        <array>
          <string>kitbash</string>
        </array>
      </dict>
    </array>
  </dict>
</plist>
PLIST

  release osx osx-arm64 Kitbash icons/icon.icns --plist "$(to_native "$plist")"
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
