using System.Runtime.InteropServices;

namespace Xkb.Native;

/// <summary>Native libxkbcommon API (core and compose).</summary>
public static unsafe partial class Libxkbcommon
{
    /// <summary>The name of the native xkbcommon library used by the generated bindings.</summary>
#if IOS || TVOS || MACCATALYST
    public const string LibraryName = "__Internal";
#else
    public const string LibraryName = "libxkbcommon";
#endif

    /// <summary>
    /// Frees memory returned by libxkbcommon with the C runtime that allocated it. A one-function
    /// shim (<c>eng/shim/xkbcommon-dotnet-shim.c</c>) compiled into every libxkbcommon payload
    /// that ships in the package; a distro libxkbcommon does not export it.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void xkb_dotnet_free(void* ptr);
}
