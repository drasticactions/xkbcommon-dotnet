using System.Runtime.InteropServices;
using Xkb.Native;

namespace Xkb.Tests;

internal static class TestHelpers
{
    /// <summary>
    /// Whether libxkbregistry can be called. False on the platforms where the package
    /// bundles core and compose only (no library to load, or a wasm pinvoke table without
    /// the symbols) and on desktops without the distro package.
    /// </summary>
    internal static bool RegistryAvailable { get; } = ProbeRegistry();

    /// <summary>
    /// Whether libxkbcommon-x11 is present and loadable. No X server is needed. False on
    /// platforms without dynamic loading; the bundled builds carry no X11 library anyway.
    /// </summary>
    internal static bool X11Available { get; } = ProbeX11();

    private static unsafe bool ProbeRegistry()
    {
        try
        {
            var context = Libxkbregistry.rxkb_context_new(rxkb_context_flags.RXKB_CONTEXT_NO_FLAGS);
            if (context is null)
            {
                return false;
            }

            Libxkbregistry.rxkb_context_unref(context);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool ProbeX11()
    {
        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst() ||
            OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser())
        {
            return false;
        }

        // The same names the assembly's DllImportResolver probes for libxkbcommon-x11.
        ReadOnlySpan<string> candidates = OperatingSystem.IsWindows()
            ? ["xkbcommon-x11.dll", "libxkbcommon-x11-0.dll"]
            : OperatingSystem.IsMacOS()
                ?
                [
                    "libxkbcommon-x11.0.dylib", "libxkbcommon-x11.dylib",
                    "/opt/homebrew/lib/libxkbcommon-x11.0.dylib",
                    "/usr/local/lib/libxkbcommon-x11.0.dylib",
                ]
                : ["libxkbcommon-x11.so.0", "libxkbcommon-x11.so", "libxkbcommon-x11"];

        foreach (var candidate in candidates)
        {
            try
            {
                if (NativeLibrary.TryLoad(candidate, out var handle))
                {
                    NativeLibrary.Free(handle);
                    return true;
                }
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
        }

        return false;
    }
}
