using System.Runtime.InteropServices;

namespace Xkb.Native;

/// <summary>
/// Frees memory that libxkbcommon hands over to the caller (e.g. <c>xkb_keymap_get_as_string</c>).
/// </summary>
internal static unsafe class Libc
{
    private static volatile bool _useFallback;
    private static delegate* unmanaged[Cdecl]<void*, void> _fallbackFree;

    /// <summary>
    /// True once <see cref="Free"/> has switched to the dynamic <c>free</c> lookup because the
    /// loaded libxkbcommon does not export <see cref="Libxkbcommon.xkb_dotnet_free"/>. Bundled
    /// builds always carry the shim, so this only turns true against a system library.
    /// </summary>
    internal static bool UsedFallback => _useFallback;

    /// <summary>Frees memory allocated by libxkbcommon.</summary>
    internal static void Free(void* ptr)
    {
        if (!_useFallback)
        {
            try
            {
                Libxkbcommon.xkb_dotnet_free(ptr);
                return;
            }
            catch (EntryPointNotFoundException)
            {
                // A libxkbcommon without the package's shim (distro Linux, Homebrew, ...).
                _useFallback = true;
            }
        }

        FallbackFree()(ptr);
    }

    private static delegate* unmanaged[Cdecl]<void*, void> FallbackFree()
    {
        if (_fallbackFree is null)
        {
            _fallbackFree = (delegate* unmanaged[Cdecl]<void*, void>)FindFree();
        }

        return _fallbackFree;
    }

    // Resolved lazily so the statically linked platforms (Apple mobile, browser-wasm), where
    // NativeLibrary.GetMainProgramHandle is unavailable, never reach this code.
    private static nint FindFree()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var candidate in new[] { "ucrtbase", "api-ms-win-crt-heap-l1-1-0", "msvcrt", "xkbcommon" })
            {
                if (NativeLibrary.TryLoad(candidate, out var module) &&
                    NativeLibrary.TryGetExport(module, "free", out var address))
                {
                    return address;
                }
            }
        }

        return NativeLibrary.GetExport(NativeLibrary.GetMainProgramHandle(), "free");
    }
}
