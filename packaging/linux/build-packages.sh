#!/usr/bin/env bash
# Packs a `dotnet publish AvaloniaUI -r linux-x64 --self-contained` output into
#   TravianBotSharp-<version>-linux-x64.tar.gz  (portable: data stays in the extracted folder)
#   travianbotsharp_<version>_amd64.deb         (installs to /opt, data goes to ~/.local/share/TravianBotSharp)
#
# Usage: build-packages.sh <version> <publish-dir> <out-dir>
# Optional env: DEB_MAINTAINER ("Name <email>"), HOMEPAGE (URL).
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <version> <publish-dir> <out-dir>" >&2
  exit 2
fi

version=$1
publish_dir=$(realpath "$2")
out_dir=$3
here=$(dirname "$(realpath "$0")")
maintainer=${DEB_MAINTAINER:-"TravianBotSharp <noreply@github.com>"}
homepage=${HOMEPAGE:-"https://github.com/vinaghost/TravianBotSharp"}

if [[ ! -x "$publish_dir/TravianBotSharp" ]]; then
  echo "$publish_dir/TravianBotSharp not found; run dotnet publish first" >&2
  exit 1
fi

mkdir -p "$out_dir"
out_dir=$(realpath "$out_dir")
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Copies the app with predictable permissions: 644 for files, 755 for folders and
# for programs (ELF executables that are not libraries, plus shell scripts).
copy_app() {
  local dest=$1
  mkdir -p "$dest"
  cp -a "$publish_dir/." "$dest/"
  find "$dest" -type d -exec chmod 755 {} +
  find "$dest" -type f -exec chmod 644 {} +
  while IFS= read -r -d '' file; do
    if [[ $file == *.sh ]] || { [[ $file != *.so && $file != *.so.* ]] && [[ $(head -c 4 "$file" | od -An -tx1 | tr -d ' \n') == 7f454c46 ]]; }; then
      chmod 755 "$file"
    fi
  done < <(find "$dest" -type f -print0)
}

# --- tarball -----------------------------------------------------------------
tar_root="$work/tar"
copy_app "$tar_root/TravianBotSharp"
tarball="$out_dir/TravianBotSharp-$version-linux-x64.tar.gz"
tar --sort=name --owner=0 --group=0 --numeric-owner -C "$tar_root" -czf "$tarball" TravianBotSharp

# --- .deb --------------------------------------------------------------------
pkg="$work/deb"
copy_app "$pkg/opt/travianbotsharp"

# The app host resolves its own real path, so running through this link still finds
# its files in /opt/travianbotsharp. /opt is read-only for users, so the app keeps
# TBS.db, logs/ and Data/ in ~/.local/share/TravianBotSharp (see MainCore/AppPaths.cs).
mkdir -p "$pkg/usr/bin"
ln -s /opt/travianbotsharp/TravianBotSharp "$pkg/usr/bin/travianbotsharp"

install -Dm644 "$here/travianbotsharp.desktop" "$pkg/usr/share/applications/travianbotsharp.desktop"
install -Dm644 "$here/travianbotsharp.png" "$pkg/usr/share/icons/hicolor/96x96/apps/travianbotsharp.png"

installed_size=$(du -sk --exclude=DEBIAN "$pkg" | cut -f1)
mkdir -p "$pkg/DEBIAN"
# google-chrome-stable comes from Google's .deb (which also adds Google's apt repo);
# the bot drives /opt/google/chrome/chrome, so snap Chromium does not satisfy it.
# libicu78 is Ubuntu 26.04, libicu76 is 25.04/25.10, libicu74 is 24.04.
cat > "$pkg/DEBIAN/control" <<EOF
Package: travianbotsharp
Version: $version
Architecture: amd64
Maintainer: $maintainer
Installed-Size: $installed_size
Depends: google-chrome-stable, libc6, libgcc-s1, libstdc++6, zlib1g, ca-certificates, libssl3t64 | libssl3, libicu78 | libicu76 | libicu74, libfontconfig1, libx11-6, libice6, libsm6
Section: games
Priority: optional
Homepage: $homepage
Description: Travian bot with a desktop UI
 TravianBotSharp (TBS) plays Travian accounts through Google Chrome: auto
 builder, adventures, farm lists, daily quests, NPC and troop training.
 .
 It needs Google Chrome from Google's .deb (google-chrome-stable); Chromium
 from snap does not work. Your database, logs and Chrome profiles are kept in
 ~/.local/share/TravianBotSharp.
EOF

deb="$out_dir/travianbotsharp_${version}_amd64.deb"
dpkg-deb --root-owner-group -Zxz --build "$pkg" "$deb"

(cd "$out_dir" && sha256sum "$(basename "$tarball")" "$(basename "$deb")" > SHA256SUMS)
ls -l "$out_dir"
