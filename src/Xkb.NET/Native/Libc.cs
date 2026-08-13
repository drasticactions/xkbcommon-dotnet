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
        var owner = OperatingSystem.IsWindows() && NativeLibrary.TryLoad("ucrtbase", out var ucrt)
            ? ucrt
            : NativeLibrary.GetMainProgramHandle();

        _free = (delegate* unmanaged[Cdecl]<void*, void>)NativeLibrary.GetExport(owner, "free");
    }

    /// <summary>Frees memory allocated by the C library's malloc.</summary>
    internal static void Free(void* ptr) => _free(ptr);
}