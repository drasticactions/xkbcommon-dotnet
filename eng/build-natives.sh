#!/bin/sh
# Builds the libxkbcommon payloads that ship in the package. Windows is covered
# by eng/build-natives.ps1. Outputs land in runtimes/<rid>/native (shared
# libraries and the browser-wasm archive) and xcframeworks/ (Apple static
# slices); both directories are gitignored and merged into the nupkg by pack.
# Linux is not bundled: the package relies on the distro's libxkbcommon there.
set -eu

usage() {
    cat >&2 <<'EOF'
usage: eng/build-natives.sh <target>

  osx                  universal arm64 + x86_64 dylib          -> runtimes/osx/native
  android [<arch>]     arm64, x64 or both (default)             -> runtimes/android-{arm64,x64}/native
  apple                iOS, tvOS and Mac Catalyst static slices -> xcframeworks/xkbcommon.xcframework
  wasm                 browser-wasm static archive              -> runtimes/browser-wasm/native/libxkbcommon.a

Requirements: meson >= 1.3, ninja and bison >= 3.6 for every target (macOS
ships bison 2.3: brew install bison and put it first on PATH); Xcode for osx
and apple; an Android NDK (ANDROID_NDK_HOME, ANDROID_NDK_ROOT or
ANDROID_HOME/ndk) for android; Emscripten $XKB_WASM_EMSCRIPTEN for wasm
(EMSDK, PATH, or the .NET wasm-tools workload pack under DOTNET_ROOT).
EOF
    exit 2
}

# The Emscripten release the .NET 10 wasm-tools workload links apps with
# (Microsoft.NET.Runtime.Emscripten.<ver>.* in the workload's manifest). The
# archive must be produced by the same release or the final link can fail.
XKB_WASM_EMSCRIPTEN=3.1.56

# Deployment targets, matching the .NET 10 minimums for each Apple platform.
IOS_MIN=15.0
TVOS_MIN=15.0
CATALYST_MIN=15.0

[ $# -ge 1 ] || usage
target=$1
shift
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
src="$root/external/libxkbcommon"
shim="$root/eng/shim/xkbcommon-dotnet-shim.c"

[ -f "$src/meson.build" ] || {
    echo "external/libxkbcommon is empty: run git submodule update --init." >&2
    exit 1
}

# bison >= 3.6 is a hard requirement on every build host, cross builds
# included: src/xkbcomp/parser.y is generated at build time.
if [ "$(uname -s)" = "Darwin" ] && ! bison --version 2>/dev/null | grep -qE ' 3\.[6-9]| [4-9]\.'; then
    for prefix in /opt/homebrew/opt/bison /usr/local/opt/bison; do
        [ -x "$prefix/bin/bison" ] && PATH="$prefix/bin:$PATH" && break
    done
fi
bison --version 2>/dev/null | grep -qE ' 3\.[6-9]| [4-9]\.' || {
    echo "libxkbcommon needs bison >= 3.6 on PATH (macOS: brew install bison)." >&2
    exit 1
}

# Core + compose only. The config roots are pinned so the baked-in default
# include path never depends on what pkg-config finds on the build host;
# XKB_CONFIG_ROOT / XLOCALEDIR override them at runtime.
common_args="--buildtype=release -Denable-x11=false -Denable-wayland=false -Denable-xkbregistry=false -Denable-tools=false -Denable-docs=false -Denable-bash-completion=false -Dxkb-config-root=/usr/share/X11/xkb -Dx-locale-root=/usr/share/X11/locale"

meson_setup() {
    # meson_setup <builddir> <shared|static> [extra meson args...]
    # Only the library target is built: libxkbcommon's tests and benches need
    # fork/execv (unavailable on tvOS) and other host facilities.
    build=$1
    kind=$2
    shift 2
    rm -rf "$build"
    # shellcheck disable=SC2086
    meson setup "$build" "$src" --default-library="$kind" $common_args "$@"
    meson compile -C "$build" "xkbcommon:${kind}_library"
}

find_built() {
    # find_built <builddir> <glob> : the real (non-symlink) library file
    found=$(find "$1" -name "$2" -type f | head -1)
    [ -n "$found" ] || {
        echo "meson built no $2 in $1." >&2
        exit 1
    }
    echo "$found"
}

cpu_family() {
    case "$1" in
        arm64 | aarch64) echo aarch64 ;;
        x86_64 | x64) echo x86_64 ;;
        *) echo "$1" ;;
    esac
}

write_cross_file() {
    # write_cross_file <file> <system> <subsystem> <cpu_family> <cpu> <c-array> <ar> <strip> <c_args-array> [link_args-array] [kernel]
    file=$1
    mkdir -p "$(dirname "$file")"
    cat > "$file" <<EOF
[binaries]
c = $6
ar = '$7'
strip = '$8'

[properties]
needs_exe_wrapper = true

[built-in options]
c_args = $9
c_link_args = ${10:-$9}

[host_machine]
system = '$2'
subsystem = '$3'
kernel = '${11:-linux}'
cpu_family = '$4'
cpu = '$5'
endian = 'little'
EOF
}

# The xkb_dotnet_free shim rides along in every payload: shared libraries get
# the object through c_link_args (so the library link pulls it in; test
# executables also receive it, harmlessly), static archives through ar.
compile_shim() {
    # compile_shim <output.o> <compiler...>
    obj=$1
    shift
    mkdir -p "$(dirname "$obj")"
    "$@" -O2 -c "$shim" -o "$obj"
}

assert_exported() {
    # assert_exported <library> <nm command...> : fails when xkb_dotnet_free was dropped.
    lib=$1
    shift
    "$@" "$lib" | grep -q ' T _\{0,1\}xkb_dotnet_free' || {
        echo "$lib does not export xkb_dotnet_free; the shim object was dropped from the link." >&2
        "$@" "$lib" | grep -i dotnet || true
        exit 1
    }
}

# ---------------------------------------------------------------------------
# macOS: two cross builds lipo'd together. Both slices are cross builds,
# including the host's own architecture: libxkbcommon probes newlocale by
# running a test program and takes a different answer when it cannot, so a
# native slice and a cross slice would be configured differently.
build_osx() {
    [ "$(uname -s)" = "Darwin" ] || { echo "osx builds need macOS." >&2; exit 1; }
    out="$root/runtimes/osx/native"
    mkdir -p "$out"
    rm -f "$out"/*.dylib

    for arch in arm64 x86_64; do
        build="$root/artifacts/natives/osx-$arch"
        cross="$build.ini"
        shim_obj="$build-shim.o"
        compile_shim "$shim_obj" clang -arch "$arch"
        write_cross_file "$cross" darwin macos "$(cpu_family "$arch")" "$arch" \
            "['clang', '-arch', '$arch']" ar strip "['-arch', '$arch']" "['-arch', '$arch', '$shim_obj']" xnu
        meson_setup "$build" shared --cross-file "$cross"
        cp "$(find_built "$build" 'libxkbcommon.*dylib')" "$out/libxkbcommon.0.$arch.dylib"
        assert_exported "$out/libxkbcommon.0.$arch.dylib" nm -gU
    done

    lipo -create "$out/libxkbcommon.0.arm64.dylib" "$out/libxkbcommon.0.x86_64.dylib" \
        -output "$out/libxkbcommon.0.dylib"
    rm -f "$out/libxkbcommon.0.arm64.dylib" "$out/libxkbcommon.0.x86_64.dylib"
    install_name_tool -id "@rpath/libxkbcommon.0.dylib" "$out/libxkbcommon.0.dylib"
    lipo -info "$out/libxkbcommon.0.dylib"
    ls -l "$out"
}

# ---------------------------------------------------------------------------
# Android: NDK clang per ABI, API level 21. meson emits an unversioned soname
# for Android, which is the name the resolver probes.
find_ndk() {
    for candidate in "${ANDROID_NDK_HOME:-}" "${ANDROID_NDK_ROOT:-}" "${ANDROID_NDK:-}"; do
        [ -n "$candidate" ] && [ -d "$candidate/toolchains/llvm" ] && { echo "$candidate"; return; }
    done
    for sdk in "${ANDROID_HOME:-}" "${ANDROID_SDK_ROOT:-}" "$HOME/Library/Android/sdk" "$HOME/Android/Sdk"; do
        [ -n "$sdk" ] && [ -d "$sdk/ndk" ] || continue
        latest=$(ls "$sdk/ndk" | sort -V | tail -1)
        [ -n "$latest" ] && { echo "$sdk/ndk/$latest"; return; }
    done
    echo "no Android NDK found: set ANDROID_NDK_HOME." >&2
    exit 1
}

build_android_arch() {
    arch=$1
    ndk=$2
    api=21
    case "$arch" in
        arm64) rid=android-arm64; triple=aarch64-linux-android ;;
        x64) rid=android-x64; triple=x86_64-linux-android ;;
        *) echo "android needs arm64 or x64." >&2; usage ;;
    esac
    host=$(ls "$ndk/toolchains/llvm/prebuilt" | head -1)
    bin="$ndk/toolchains/llvm/prebuilt/$host/bin"
    [ -x "$bin/$triple$api-clang" ] || { echo "$bin/$triple$api-clang not found." >&2; exit 1; }

    out="$root/runtimes/$rid/native"
    build="$root/artifacts/natives/$rid"
    cross="$build.ini"
    shim_obj="$build-shim.o"
    shim_map="$build-shim.map"
    mkdir -p "$out"
    rm -f "$out"/*.so

    compile_shim "$shim_obj" "$bin/$triple$api-clang"
    # libxkbcommon links with a version script whose "local: *" would hide the
    # shim; lld merges every --version-script it is given, so a second one
    # exports xkb_dotnet_free under its own node.
    printf 'XKB_DOTNET_1 {\nglobal:\n    xkb_dotnet_free;\n};\n' > "$shim_map"
    # 16 KB page alignment is mandatory for Android 15+ on arm64.
    write_cross_file "$cross" android android "$(cpu_family "$arch")" "$(cpu_family "$arch")" \
        "['$bin/$triple$api-clang']" "$bin/llvm-ar" "$bin/llvm-strip" "[]" \
        "['-Wl,-z,max-page-size=16384', '-Wl,--version-script=$shim_map', '$shim_obj']"
    meson_setup "$build" shared --cross-file "$cross"
    cp "$(find_built "$build" 'libxkbcommon.so*')" "$out/libxkbcommon.so"
    "$bin/llvm-strip" --strip-unneeded "$out/libxkbcommon.so"
    assert_exported "$out/libxkbcommon.so" "$bin/llvm-nm" -D
    "$bin/llvm-readelf" -d "$out/libxkbcommon.so" | grep -E 'SONAME|NEEDED' || true
    ls -l "$out"
}

build_android() {
    ndk=$(find_ndk)
    echo "Using NDK $ndk"
    if [ $# -eq 0 ]; then
        build_android_arch arm64 "$ndk"
        build_android_arch x64 "$ndk"
    else
        build_android_arch "$1" "$ndk"
    fi
}

# ---------------------------------------------------------------------------
# Apple: static slices per (platform, arch), fat per platform, then one
# xcframework. Consumers link it through the buildTransitive targets.
build_apple_slice() {
    # build_apple_slice <slice> <arch> <sdk> <subsystem> <target-flag...>
    slice=$1
    arch=$2
    sdk=$3
    subsystem=$4
    shift 4
    sysroot=$(xcrun --sdk "$sdk" --show-sdk-path)
    build="$root/artifacts/natives/apple/$slice-$arch"
    cross="$build.ini"

    flags="'-arch', '$arch', '-isysroot', '$sysroot'"
    for f in "$@"; do flags="$flags, '$f'"; done
    write_cross_file "$cross" darwin "$subsystem" "$(cpu_family "$arch")" "$arch" \
        "['clang', $flags]" ar strip "[$flags]" "[$flags]" xnu
    meson_setup "$build" static --cross-file "$cross"
    cp "$(find_built "$build" 'libxkbcommon.a')" "$build.a"
    compile_shim "$build-shim.o" clang -arch "$arch" -isysroot "$sysroot" "$@"
    ar r "$build.a" "$build-shim.o"
    assert_exported "$build.a" nm -gU
}

build_apple_platform() {
    # build_apple_platform <slice> <sdk> <subsystem> <archs> <target-flag...>
    slice=$1
    sdk=$2
    subsystem=$3
    archs=$4
    shift 4
    libs=""
    for arch in $archs; do
        build_apple_slice "$slice" "$arch" "$sdk" "$subsystem" "$@"
        libs="$libs $root/artifacts/natives/apple/$slice-$arch.a"
    done
    mkdir -p "$root/artifacts/natives/apple/$slice"
    # shellcheck disable=SC2086
    lipo -create $libs -output "$root/artifacts/natives/apple/$slice/libxkbcommon.a"
    lipo -info "$root/artifacts/natives/apple/$slice/libxkbcommon.a"
}

build_apple() {
    [ "$(uname -s)" = "Darwin" ] || { echo "apple builds need macOS." >&2; exit 1; }
    stage="$root/artifacts/natives/apple"
    rm -rf "$stage"
    mkdir -p "$stage"

    build_apple_platform ios iphoneos ios "arm64" "-miphoneos-version-min=$IOS_MIN"
    build_apple_platform iossimulator iphonesimulator ios-simulator "arm64 x86_64" "-mios-simulator-version-min=$IOS_MIN"
    build_apple_platform tvos appletvos tvos "arm64" "-mtvos-version-min=$TVOS_MIN"
    build_apple_platform tvossimulator appletvsimulator tvos-simulator "arm64 x86_64" "-mtvos-simulator-version-min=$TVOS_MIN"
    # Catalyst is the macOS SDK with an iOS-macabi target; the arch goes in the target triple.
    build_apple_slice maccatalyst arm64 macosx macos "-target" "arm64-apple-ios$CATALYST_MIN-macabi"
    build_apple_slice maccatalyst x86_64 macosx macos "-target" "x86_64-apple-ios$CATALYST_MIN-macabi"
    mkdir -p "$stage/maccatalyst"
    lipo -create "$stage/maccatalyst-arm64.a" "$stage/maccatalyst-x86_64.a" -output "$stage/maccatalyst/libxkbcommon.a"
    lipo -info "$stage/maccatalyst/libxkbcommon.a"

    # Headers are informational (the bindings need none): the public API headers.
    headers="$stage/include"
    mkdir -p "$headers"
    cp "$src"/include/xkbcommon/*.h "$headers/"

    out="$root/xcframeworks/xkbcommon.xcframework"
    rm -rf "$out"
    mkdir -p "$root/xcframeworks"
    xcodebuild -create-xcframework \
        -library "$stage/ios/libxkbcommon.a" -headers "$headers" \
        -library "$stage/iossimulator/libxkbcommon.a" -headers "$headers" \
        -library "$stage/tvos/libxkbcommon.a" -headers "$headers" \
        -library "$stage/tvossimulator/libxkbcommon.a" -headers "$headers" \
        -library "$stage/maccatalyst/libxkbcommon.a" -headers "$headers" \
        -output "$out"
    ls "$out"
}

# ---------------------------------------------------------------------------
# Browser WebAssembly: a static archive the wasm-tools workload links into the
# app. The file name doubles as the pinvoke module name, hence libxkbcommon.a.
find_emcc() {
    # Sets emdir (and, for the workload packs, the environment emcc needs).
    if [ -n "${EMSDK:-}" ] && [ -x "$EMSDK/upstream/emscripten/emcc" ]; then
        emdir="$EMSDK/upstream/emscripten"
        return
    fi
    if command -v emcc >/dev/null 2>&1; then
        emdir=$(dirname "$(command -v emcc)")
        return
    fi
    # The wasm-tools workload ships Emscripten as packs: Sdk (emcc + llvm),
    # Node and Cache. Their .emscripten config reads these DOTNET_EMSCRIPTEN_*
    # variables instead of hard-coding paths. Stable pack versions win over previews.
    for dotnet_root in "${DOTNET_ROOT:-}" "$HOME/.dotnet" /usr/share/dotnet /usr/local/share/dotnet /usr/lib/dotnet; do
        [ -n "$dotnet_root" ] && [ -d "$dotnet_root/packs" ] || continue
        for pack in "$dotnet_root/packs/Microsoft.NET.Runtime.Emscripten.$XKB_WASM_EMSCRIPTEN.Sdk."*; do
            [ -d "$pack" ] || continue
            ver=$(ls "$pack" | grep -v -- '-' | sort -V | tail -1)
            [ -n "$ver" ] || ver=$(ls "$pack" | sort -V | tail -1)
            tools="$pack/$ver/tools"
            [ -x "$tools/emscripten/emcc" ] || continue
            host_rid=${pack##*.Sdk.}
            node_pack="$dotnet_root/packs/Microsoft.NET.Runtime.Emscripten.$XKB_WASM_EMSCRIPTEN.Node.$host_rid/$ver"
            cache_pack="$dotnet_root/packs/Microsoft.NET.Runtime.Emscripten.$XKB_WASM_EMSCRIPTEN.Cache.$host_rid/$ver"
            export EM_CONFIG="$tools/emscripten/.emscripten"
            export DOTNET_EMSCRIPTEN_LLVM_ROOT="$tools/bin"
            export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$tools"
            export DOTNET_EMSCRIPTEN_NODE_JS="$node_pack/tools/bin/node"
            [ -d "$cache_pack/tools/emscripten/cache" ] && export EM_CACHE="$cache_pack/tools/emscripten/cache" EM_FROZEN_CACHE=1
            emdir="$tools/emscripten"
            return
        done
    done
    echo "no emcc found: install emsdk $XKB_WASM_EMSCRIPTEN, or the .NET wasm-tools workload." >&2
    exit 1
}

build_wasm() {
    find_emcc
    version=$("$emdir/emcc" --version | head -1 | sed -E 's/.* ([0-9]+\.[0-9]+\.[0-9]+).*/\1/')
    echo "Using emcc $version from $emdir"
    if [ "$version" != "$XKB_WASM_EMSCRIPTEN" ] && [ "${XKB_ALLOW_EMSCRIPTEN_MISMATCH:-}" != "1" ]; then
        echo "emcc $version does not match the pinned $XKB_WASM_EMSCRIPTEN; set XKB_ALLOW_EMSCRIPTEN_MISMATCH=1 to build anyway." >&2
        exit 1
    fi

    out="$root/runtimes/browser-wasm/native"
    build="$root/artifacts/natives/browser-wasm"
    cross="$build.ini"
    mkdir -p "$out"
    rm -f "$out"/*.a

    write_cross_file "$cross" emscripten emscripten wasm32 wasm32 \
        "['$emdir/emcc']" "$emdir/emar" "$emdir/emstrip" "[]" "[]" emscripten
    meson_setup "$build" static --cross-file "$cross"
    cp "$(find_built "$build" 'libxkbcommon.a')" "$out/libxkbcommon.a"
    compile_shim "$build-shim.o" "$emdir/emcc"
    "$emdir/emar" r "$out/libxkbcommon.a" "$build-shim.o"
    assert_exported "$out/libxkbcommon.a" "$emdir/emnm"
    # Every object must link against Emscripten's libc: a symbol its headers
    # declare but its libc lacks would only surface in the consuming app's
    # publish. --whole-archive pulls all of them in.
    printf 'int main(void) { return 0; }\n' > "$build-linkcheck.c"
    "$emdir/emcc" -O1 -o "$build-linkcheck.js" "$build-linkcheck.c" \
        -Wl,--whole-archive "$out/libxkbcommon.a" -Wl,--no-whole-archive \
        -sERROR_ON_UNDEFINED_SYMBOLS=1 || {
        echo "libxkbcommon.a has undefined symbols against Emscripten's libc." >&2
        exit 1
    }
    ls -l "$out"
}

case "$target" in
    osx) build_osx ;;
    android) build_android "$@" ;;
    apple) build_apple ;;
    wasm) build_wasm ;;
    *) usage ;;
esac
