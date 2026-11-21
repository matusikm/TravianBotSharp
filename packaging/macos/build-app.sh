#!/usr/bin/env bash
# Wraps a `dotnet publish AvaloniaUI -r osx-<arch> --self-contained` output in TravianBotSharp.app
# and zips it as TravianBotSharp-<version>-<rid>.zip. Run it on macOS: it uses sips, iconutil,
# codesign and ditto.
#
# The app is signed ad hoc (Apple Silicon refuses to run unsigned code) but not notarized, so on
# first launch macOS asks the user to allow it in System Settings > Privacy & Security.
# Data goes to ~/Library/Application Support/TravianBotSharp (see MainCore/AppPaths.cs).
#
# Usage: build-app.sh <version> <rid> <publish-dir> <out-dir>
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "usage: $0 <version> <rid> <publish-dir> <out-dir>" >&2
  exit 2
fi

version=$1
rid=$2
publish_dir=$(cd "$3" && pwd)
out_dir=$4
here=$(cd "$(dirname "$0")" && pwd)

if [[ ! -x "$publish_dir/TravianBotSharp" ]]; then
  echo "$publish_dir/TravianBotSharp not found; run dotnet publish first" >&2
  exit 1
fi

mkdir -p "$out_dir"
out_dir=$(cd "$out_dir" && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

app="$work/TravianBotSharp.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -a "$publish_dir/." "$app/Contents/MacOS/"
rm -f "$app/Contents/MacOS/"*.pdb

# Icon: scale the Linux PNG into an .iconset and let iconutil build the .icns.
iconset="$work/TravianBotSharp.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z $size $size "$here/../linux/travianbotsharp.png" --out "$iconset/icon_${size}x${size}.png" > /dev/null
  double=$((size * 2))
  sips -z $double $double "$here/../linux/travianbotsharp.png" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/TravianBotSharp.icns"

sed "s/@VERSION@/$version/g" "$here/Info.plist" > "$app/Contents/Info.plist"

# codesign treats every folder in Contents/MacOS as a nested bundle, so Patchright's driver
# (.playwright) moves to Contents/Resources; Program.cs points PLAYWRIGHT_DRIVER_SEARCH_PATH there.
mv "$app/Contents/MacOS/.playwright" "$app/Contents/Resources/.playwright"

# Every Mach-O file needs a signature on Apple Silicon, and codesign treats every file in
# Contents/MacOS as nested code, so sign each one (for .dll and other data files the signature
# goes into extended attributes, which ditto keeps). The main executable is signed last,
# together with the bundle.
while IFS= read -r -d '' file; do
  [[ $file == "$app/Contents/MacOS/TravianBotSharp" ]] && continue
  if [[ $file == "$app/Contents/MacOS/"* ]] || file -b "$file" | grep -q "Mach-O"; then
    codesign --force --sign - --timestamp=none "$file" 2>&1 | grep -v "replacing existing signature" || true
  fi
done < <(find "$app/Contents/MacOS" "$app/Contents/Resources/.playwright" -type f -print0)
codesign --force --sign - --timestamp=none "$app"
codesign --verify --deep --strict "$app"

zip_file="$out_dir/TravianBotSharp-$version-$rid.zip"
rm -f "$zip_file"
ditto -c -k --sequesterRsrc --keepParent "$app" "$zip_file"
ls -l "$zip_file"
