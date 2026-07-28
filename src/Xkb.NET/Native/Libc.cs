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
        // Resolve free() from the running process rather than naming a libc
        // soname, so it works on glibc, musl and FreeBSD alike. Strings
        // malloc'd by libxkbcommon (xkb_keymap_get_as_string) must be released
        // by the same allocator.
        _free = (delegate* unmanaged[Cdecl]<void*, void>)NativeLibrary.GetExport(
            NativeLibrary.GetMainProgramHandle(), "free");
    }

    /// <summary>Frees memory allocated by the C library's malloc.</summary>
    internal static void Free(void* ptr) => _free(ptr);
}
