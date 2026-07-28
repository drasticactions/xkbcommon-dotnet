#!/bin/sh
# Regenerates src/Xkb.NET/Native/Generated from the external/libxkbcommon
# submodule headers via ClangSharpPInvokeGenerator (a local dotnet tool, see
# .config/dotnet-tools.json). Run from the repository root:
#
#   dotnet tool restore
#   sh eng/generate.sh
#
# The x11 pass needs the system libxcb headers (/usr/include/xcb/xcb.h).
set -eu
cd "$(dirname "$0")/.."

rm -rf src/Xkb.NET/Native/Generated
mkdir -p src/Xkb.NET/Native/Generated

# The bundled libclang needs clang's builtin headers (stddef.h etc.); use the
# system clang's resource directory.
RESOURCE_DIR="$(clang -print-resource-dir)"

# The generator's exit code is its diagnostic (warning) count, so a successful
# run with warnings is nonzero; presence of the output files is checked instead.
dotnet tool run ClangSharpPInvokeGenerator -- --resource-directory "$RESOURCE_DIR" "@eng/xkbcommon.rsp" || true
dotnet tool run ClangSharpPInvokeGenerator -- --resource-directory "$RESOURCE_DIR" "@eng/x11.rsp" || true
dotnet tool run ClangSharpPInvokeGenerator -- --resource-directory "$RESOURCE_DIR" "@eng/registry.rsp" || true

test -f src/Xkb.NET/Native/Generated/xkbcommon/Libxkbcommon.cs
test -f src/Xkb.NET/Native/Generated/x11/LibxkbcommonX11.cs
test -f src/Xkb.NET/Native/Generated/registry/Libxkbregistry.cs

# Fixups for constructs ClangSharp emits but C# cannot compile:
# - XKB_KEYMAP_USE_ORIGINAL_FORMAT is ((enum xkb_keymap_format) -1); casting -1
#   to an enum in a constant initializer requires unchecked().
sed -i 's/public const xkb_keymap_format XKB_KEYMAP_USE_ORIGINAL_FORMAT = ((xkb_keymap_format)(-1));/public const xkb_keymap_format XKB_KEYMAP_USE_ORIGINAL_FORMAT = unchecked((xkb_keymap_format)(-1));/' \
  src/Xkb.NET/Native/Generated/xkbcommon/Libxkbcommon.cs

# The foreign types FILE (_IO_FILE), va_list (__va_list_tag) and libxcb's
# xcb_connection_t are referenced by signatures but deliberately not traversed;
# opaque stand-in structs live in the hand-written Native/OpaqueTypes.cs.
