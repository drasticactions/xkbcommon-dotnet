using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xkb.Native;

/// <summary>
/// Resolves the DllImport names used by the generated bindings against the
/// versioned sonames installed by the system xkbcommon packages.
/// </summary>
internal static class NativeResolver
{
#pragma warning disable CA2255 // ModuleInitializer in library: needed to resolve the sonames.
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Only one DllImportResolver may be registered per assembly, so a
        // single resolver handles every native library this package binds.
        NativeLibrary.SetDllImportResolver(typeof(NativeResolver).Assembly, static (name, assembly, searchPath) =>
        {
            // Probe explicitly, since the DllImport name alone does not
            // resolve against the versioned soname.
            ReadOnlySpan<string> candidates = OperatingSystem.IsWindows()
                ? name switch
                {
                    Libxkbcommon.LibraryName => ["xkbcommon.dll", "libxkbcommon-0.dll", "libxkbcommon.dll"],
                    LibxkbcommonX11.LibraryName => ["xkbcommon-x11.dll", "libxkbcommon-x11-0.dll"],
                    Libxkbregistry.LibraryName => ["xkbregistry.dll", "libxkbregistry-0.dll"],
                    _ => [],
                }
                : OperatingSystem.IsMacOS()
                    ? name switch
                    {
                        Libxkbcommon.LibraryName =>
                        [
                            "libxkbcommon.0.dylib", "libxkbcommon.dylib",
                            "/opt/homebrew/lib/libxkbcommon.0.dylib",
                            "/usr/local/lib/libxkbcommon.0.dylib",
                            "/opt/local/lib/libxkbcommon.0.dylib",
                        ],
                        LibxkbcommonX11.LibraryName =>
                        [
                            "libxkbcommon-x11.0.dylib", "libxkbcommon-x11.dylib",
                            "/opt/homebrew/lib/libxkbcommon-x11.0.dylib",
                            "/usr/local/lib/libxkbcommon-x11.0.dylib",
                        ],
                        Libxkbregistry.LibraryName =>
                        [
                            "libxkbregistry.0.dylib", "libxkbregistry.dylib",
                            "/opt/homebrew/lib/libxkbregistry.0.dylib",
                            "/usr/local/lib/libxkbregistry.0.dylib",
                        ],
                        _ => [],
                    }
                    : name switch
                    {
                        Libxkbcommon.LibraryName => ["libxkbcommon.so.0", "libxkbcommon.so", "libxkbcommon"],
                        LibxkbcommonX11.LibraryName => ["libxkbcommon-x11.so.0", "libxkbcommon-x11.so", "libxkbcommon-x11"],
                        Libxkbregistry.LibraryName => ["libxkbregistry.so.0", "libxkbregistry.so", "libxkbregistry"],
                        _ => [],
                    };

            foreach (var candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        });
    }
#pragma warning restore CA2255
}
