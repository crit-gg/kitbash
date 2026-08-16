#!/usr/bin/env bash
#
# Builds icons/icon.icns from icons/icon.svg, which is what vpk wants for a macOS bundle.
#
#   tools/icon_builder/icns.sh [icons/icon.svg] [icons/icon.icns]
#
# sips and iconutil are both part of macOS, so this needs nothing installed, and it only
# runs here. The result is committed the way icon.ico is, so a release never depends on it.
#
# Every size is rasterised from the vector rather than scaled down from one bitmap, so the
# small ones stay crisp.

set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)

source_svg="${1:-$root/icons/icon.svg}"
target_icns="${2:-$root/icons/icon.icns}"

if [ "$(uname -s)" != "Darwin" ]; then
  echo "icns.sh needs sips and iconutil, which are macOS only." >&2
  exit 3
fi

if [ ! -f "$source_svg" ]; then
  echo "No such file: $source_svg" >&2
  exit 2
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

set -- 16 32 128 256 512

mkdir -p "$work/icon.iconset"

for size in "$@"; do
  # The plain size and its retina twin, which is the next size up under the same name.
  sips -s format png -Z "$size" "$source_svg" \
    --out "$work/icon.iconset/icon_${size}x${size}.png" >/dev/null
  sips -s format png -Z "$((size * 2))" "$source_svg" \
    --out "$work/icon.iconset/icon_${size}x${size}@2x.png" >/dev/null
done

iconutil -c icns "$work/icon.iconset" -o "$target_icns"

echo "Wrote $target_icns"
