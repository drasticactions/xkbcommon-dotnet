#!/bin/sh
# Runs the shared test suite on a platform where libxkbcommon is bundled with the
# app, using the packed nupkg so the package wiring is what gets tested.
#
#   eng/test-natives.sh ios | tvos | maccatalyst | android | wasm
#
# Each run: packs src/Xkb.NET (skip with XKB_SKIP_PACK=1 if artifacts/
# already holds a fresh nupkg), clears the test apps' package cache, builds
# the host app for the platform, then drives it with XHarness (a local dotnet
# tool, see .config/dotnet-tools.json). Logs land in artifacts/test-results/<platform>.
#
# Prerequisites per platform:
#   ios/tvos      Xcode with a simulator runtime; ios/tvos workloads
#   maccatalyst   maccatalyst workload
#   android       a booted emulator or device visible to adb; android workload
#   wasm          wasm-tools workload; Chrome and a matching chromedriver on PATH
#                 (or XKB_BROWSER_PATH / XKB_CHROMEDRIVER_DIR)
set -eu

usage() {
    echo "usage: eng/test-natives.sh ios|tvos|maccatalyst|android|wasm" >&2
    exit 2
}

[ $# -eq 1 ] || usage
platform=$1
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$root"

device_tests=tests/apps/Xkb.NET.DeviceTests
wasm_tests=tests/apps/Xkb.NET.WasmTests
results="$root/artifacts/test-results/$platform"
config=${XKB_TEST_CONFIGURATION:-Debug}
version=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' Directory.Build.props)

case "$(uname -m)" in
    arm64 | aarch64) host_arch=arm64 ;;
    *) host_arch=x64 ;;
esac

# Each net10.0-ios<version> binds to one Xcode release; pick the newest
# installed SDK pack that the selected Xcode can drive.
apple_platform_version() {
    if [ -n "${XKB_APPLE_PLATFORM_VERSION:-}" ]; then
        echo "$XKB_APPLE_PLATFORM_VERSION"
        return
    fi
    xcode=$(xcodebuild -version | sed -n 's/^Xcode \([0-9]*\.[0-9]*\).*/\1/p')
    packs="${DOTNET_ROOT:-$HOME/.dotnet}/packs"
    best=""
    for pack in "$packs"/Microsoft.iOS.Sdk.net10.0_*; do
        [ -d "$pack" ] || continue
        v=${pack##*_}
        if [ "$(printf '%s\n%s\n' "$v" "$xcode" | sort -V | head -1)" = "$v" ]; then
            best=$v
        fi
    done
    [ -n "$best" ] || { echo "no Microsoft.iOS.Sdk.net10.0_* pack usable with Xcode $xcode under $packs" >&2; exit 1; }
    echo "$best"
}

if [ "${XKB_SKIP_PACK:-}" != "1" ]; then
    rm -f artifacts/xkbcommon-dotnet.*.nupkg artifacts/xkbcommon-dotnet.*.snupkg
    dotnet pack src/Xkb.NET/Xkb.NET.csproj -c Release -o artifacts
fi
[ -f "artifacts/xkbcommon-dotnet.$version.nupkg" ] || {
    echo "artifacts/xkbcommon-dotnet.$version.nupkg is missing." >&2
    exit 1
}

# The nupkg keeps its version between rebuilds, so a cached copy would go stale.
rm -rf artifacts/test-packages
rm -rf "$results"
mkdir -p "$results"
dotnet tool restore >/dev/null

xharness() {
    dotnet xharness "$@"
}

case "$platform" in
    ios | tvos)
        apple_version=$(apple_platform_version)
        tfm="net10.0-${platform}${apple_version}"
        rid="${platform}simulator-$host_arch"
        target="$platform-simulator-64"
        [ "$platform" = tvos ] && target="tvos-simulator"
        dotnet build "$device_tests" -c "$config" -f "$tfm" -r "$rid" -p:XkbApplePlatformVersion="$apple_version"
        app=$(find "$device_tests/bin/$config/$tfm/$rid" -maxdepth 1 -name '*.app' | head -1)
        [ -n "$app" ] || { echo "no .app built under $device_tests/bin/$config/$tfm/$rid" >&2; exit 1; }
        xharness apple run --app "$app" --target "$target" --output-directory "$results" \
            --expected-exit-code 0 --timeout 00:15:00 --launch-timeout 00:05:00 ${XKB_XHARNESS_ARGS:-}
        ;;

    maccatalyst)
        apple_version=$(apple_platform_version)
        tfm="net10.0-maccatalyst${apple_version}"
        rid="maccatalyst-$host_arch"
        dotnet build "$device_tests" -c "$config" -f "$tfm" -r "$rid" -p:XkbApplePlatformVersion="$apple_version"
        app=$(find "$device_tests/bin/$config/$tfm/$rid" -maxdepth 1 -name '*.app' | head -1)
        [ -n "$app" ] || { echo "no .app built under $device_tests/bin/$config/$tfm/$rid" >&2; exit 1; }
        # XHarness launches Catalyst apps through `open`, so it only sees that
        # launcher's exit code. The bundle's executable is run directly instead
        # and its own exit code decides.
        exe=$(find "$app/Contents/MacOS" -maxdepth 1 -type f -perm -u+x | head -1)
        [ -n "$exe" ] || { echo "no executable under $app/Contents/MacOS" >&2; exit 1; }
        rc=0
        "$exe" "${XKB_TEST_FILTER:-}" > "$results/run-maccatalyst.log" 2>&1 || rc=$?
        cat "$results/run-maccatalyst.log"
        [ "$rc" -eq 0 ] || { echo "maccatalyst test run failed with exit code $rc" >&2; exit "$rc"; }
        grep -q 'Failed: 0' "$results/run-maccatalyst.log" || { echo "no passing summary in $results/run-maccatalyst.log" >&2; exit 1; }
        ;;

    android)
        tfm="net10.0-android"
        dotnet build "$device_tests" -c "$config" -f "$tfm" -p:XkbApplePlatformVersion="${XKB_APPLE_PLATFORM_VERSION:-26.5}"
        apk=$(find "$device_tests/bin/$config/$tfm" -name '*-Signed.apk' | head -1)
        [ -n "$apk" ] || { echo "no signed apk built under $device_tests/bin/$config/$tfm" >&2; exit 1; }
        xharness android test --app "$apk" --package-name com.drasticactions.xkbcommon.tests \
            --instrumentation xkbcommon.tests.TestInstrumentation --output-directory "$results" \
            --expected-exit-code 0 --timeout 00:15:00 ${XKB_XHARNESS_ARGS:-}
        ;;

    wasm)
        # publish: build alone leaves the wwwroot page out of the output directory.
        dotnet publish "$wasm_tests" -c "$config"
        bundle="$wasm_tests/bin/$config/net10.0/publish/wwwroot"
        [ -f "$bundle/index.html" ] || { echo "no published wwwroot under $bundle" >&2; exit 1; }
        # Paths with spaces (Chrome for Testing) must survive as single arguments,
        # hence the positional-parameter list instead of a string.
        set --
        [ -n "${XKB_BROWSER_PATH:-}" ] && set -- "$@" --browser-path "$XKB_BROWSER_PATH"
        [ -n "${XKB_CHROMEDRIVER_DIR:-}" ] && PATH="$XKB_CHROMEDRIVER_DIR:$PATH"
        # No chromedriver on PATH: fetch Chrome for Testing plus its matching driver
        # (a pinned pair, unlike Homebrew's dropped chromedriver cask) into artifacts/.
        if ! command -v chromedriver >/dev/null 2>&1; then
            browsers="$root/artifacts/browsers"
            npx -y @puppeteer/browsers install chrome@stable --path "$browsers"
            npx -y @puppeteer/browsers install chromedriver@stable --path "$browsers"
            driver=$(find "$browsers/chromedriver" -type f -name chromedriver | head -1)
            chrome=$(find "$browsers/chrome" -type f -path '*MacOS*' -name 'Google Chrome for Testing' | head -1)
            [ -n "$chrome" ] || chrome=$(find "$browsers/chrome" -type f -name chrome | head -1)
            PATH="$(dirname "$driver"):$PATH"
            [ -n "${XKB_BROWSER_PATH:-}" ] || set -- "$@" --browser-path "$chrome"
        fi
        # shellcheck disable=SC2086
        xharness wasm test-browser --app "$bundle" --browser Chrome "$@" \
            --output-directory "$results" --expected-exit-code 0 --timeout 00:15:00 ${XKB_XHARNESS_ARGS:-}
        ;;

    *) usage ;;
esac
