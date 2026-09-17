/*
 * Compiled into every libxkbcommon payload that ships in the package (shared
 * libraries through the cross/native file's c_link_args, static archives with
 * ar).
 *
 * xkb_dotnet_free frees memory returned by libxkbcommon (e.g.
 * xkb_keymap_get_as_string) with the same C runtime that allocated it, so the
 * managed side needs no dynamic lookup of free: browser-wasm has none, and on
 * Windows the CRT that xkbcommon.dll links may differ from the host's.
 * Libc.Free in src/Xkb.NET/Native/Libc.cs calls it and falls back to a dynamic
 * free lookup when the symbol is missing (a distro libxkbcommon).
 */
#include <stdlib.h>

#if defined(_WIN32)
#  define XKB_DOTNET_EXPORT __declspec(dllexport)
#else
#  define XKB_DOTNET_EXPORT __attribute__((visibility("default")))
#endif

XKB_DOTNET_EXPORT void xkb_dotnet_free(void *p)
{
    free(p);
}

#if defined(__EMSCRIPTEN__)
/*
 * Emscripten's unistd.h declares eaccess (so libxkbcommon's configure check
 * sets HAVE_EACCESS) but its libc never defines it, which would surface as an
 * undefined symbol when the consuming app links libxkbcommon.a. There is no
 * real/effective uid distinction in the browser, so access() is equivalent.
 */
#include <unistd.h>

int eaccess(const char *path, int mode)
{
    return access(path, mode);
}
#endif
