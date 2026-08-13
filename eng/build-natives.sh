#!/bin/sh
set -eu

usage() {
    echo "usage: eng/build-natives.sh RID" >&2
    echo "  RID is osx-arm64 or osx-x64, and must match the host architecture." >&2
    exit 2
}

[ $# -eq 1 ] || usage
rid=$1
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
host=$(uname -m)

case "$rid" in
    osx-arm64) want=arm64 ;;
    osx-x64) want=x86_64 ;;
    *) usage ;;
esac

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-natives.sh builds the macOS runtimes; use build-natives.ps1 for win-x64." >&2
    exit 1
fi

if [ "$host" != "$want" ]; then
    echo "$rid wants a $want host, and this one is $host." >&2
    exit 1
fi

[ -f "$root/external/libxkbcommon/meson.build" ] || {
    echo "external/libxkbcommon is empty: run git submodule update --init." >&2
    exit 1
}

build="$root/artifacts/natives/$rid"
out="$root/runtimes/$rid/native"
rm -rf "$build"
meson setup "$build" "$root/external/libxkbcommon" \
    --buildtype=release \
    --default-library=shared \
    -Denable-x11=false \
    -Denable-wayland=false \
    -Denable-xkbregistry=false \
    -Denable-tools=false \
    -Denable-docs=false \
    -Denable-bash-completion=false
ninja -C "$build"

mkdir -p "$out"
rm -f "$out"/*.dylib
found=$(find "$build" -name 'libxkbcommon.*dylib' -type f | head -1)
[ -n "$found" ] || {
    echo "meson built no libxkbcommon dylib in $build." >&2
    exit 1
}

cp "$found" "$out/libxkbcommon.0.dylib"
install_name_tool -id "@rpath/libxkbcommon.0.dylib" "$out/libxkbcommon.0.dylib"
ls -l "$out"
