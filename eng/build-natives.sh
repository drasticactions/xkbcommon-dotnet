#!/bin/sh
set -eu

usage() {
    echo "usage: eng/build-natives.sh osx" >&2
    echo "  Builds a universal arm64 + x86_64 xkbcommon into runtimes/osx/native." >&2
    echo "  Use eng/build-natives.ps1 for win-x64." >&2
    exit 2
}

[ $# -eq 1 ] || usage
[ "$1" = "osx" ] || usage
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-natives.sh builds the macOS runtime; use build-natives.ps1 for win-x64." >&2
    exit 1
fi

[ -f "$root/external/libxkbcommon/meson.build" ] || {
    echo "external/libxkbcommon is empty: run git submodule update --init." >&2
    exit 1
}

out="$root/runtimes/osx/native"
mkdir -p "$out"
rm -f "$out"/*.dylib

cpu_family() {
    case "$1" in
        arm64) echo aarch64 ;;
        *) echo "$1" ;;
    esac
}

# Both slices are cross builds, including the one for this host's own
# architecture. libxkbcommon probes newlocale by running a test program and
# takes a different answer when it cannot, so a native slice and a cross slice
# would be configured differently inside one universal binary.
write_cross_file() {
    arch=$1
    file=$2
    mkdir -p "$(dirname "$file")"
    cat > "$file" <<EOF
[binaries]
c = ['clang', '-arch', '$arch']
cpp = ['clang++', '-arch', '$arch']
strip = 'strip'

[properties]
needs_exe_wrapper = true

[built-in options]
c_args = ['-arch', '$arch']
c_link_args = ['-arch', '$arch']

[host_machine]
system = 'darwin'
subsystem = 'macos'
kernel = 'xnu'
cpu_family = '$(cpu_family "$arch")'
cpu = '$arch'
endian = 'little'
EOF
}

build_arch() {
    arch=$1
    build="$root/artifacts/natives/osx-$arch"
    cross="$root/artifacts/natives/osx-$arch.ini"
    rm -rf "$build"
    write_cross_file "$arch" "$cross"

    meson setup "$build" "$root/external/libxkbcommon" \
        --cross-file "$cross" \
        --buildtype=release \
        --default-library=shared \
        -Denable-x11=false \
        -Denable-wayland=false \
        -Denable-xkbregistry=false \
        -Denable-tools=false \
        -Denable-docs=false \
        -Denable-bash-completion=false
    ninja -C "$build"

    found=$(find "$build" -name 'libxkbcommon.*dylib' -type f | head -1)
    [ -n "$found" ] || {
        echo "meson built no libxkbcommon dylib for $arch in $build." >&2
        exit 1
    }

    cp "$found" "$out/libxkbcommon.0.$arch.dylib"
}

build_arch arm64
build_arch x86_64

lipo -create "$out/libxkbcommon.0.arm64.dylib" "$out/libxkbcommon.0.x86_64.dylib" \
    -output "$out/libxkbcommon.0.dylib"
rm -f "$out/libxkbcommon.0.arm64.dylib" "$out/libxkbcommon.0.x86_64.dylib"
install_name_tool -id "@rpath/libxkbcommon.0.dylib" "$out/libxkbcommon.0.dylib"

lipo -info "$out/libxkbcommon.0.dylib"
ls -l "$out"