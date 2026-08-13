using System.Runtime.InteropServices;

namespace Xkb.Native;

/// <summary>
/// Minimal libc access.
/// </summary>
internal static unsafe class Libc
{
    private static readonly delegate* unmanaged[Cdecl]<void*, void> _free;

    static Libc()
    {
        nint address = 0;
        if (OperatingSystem.IsWindows())
        {
            foreach (var candidate in new[] { "ucrtbase", "api-ms-win-crt-heap-l1-1-0", "msvcrt", "xkbcommon" })
            {
                if (NativeLibrary.TryLoad(candidate, out var module) &&
                    NativeLibrary.TryGetExport(module, "free", out address))
                {
                    break;
                }

                address = 0;
            }
        }

        if (address == 0)
        {
            address = NativeLibrary.GetExport(NativeLibrary.GetMainProgramHandle(), "free");
        }

        _free = (delegate* unmanaged[Cdecl]<void*, void>)address;
    }

    /// <summary>Frees memory allocated by the C library's malloc.</summary>
    internal static void Free(void* ptr) => _free(ptr);
}