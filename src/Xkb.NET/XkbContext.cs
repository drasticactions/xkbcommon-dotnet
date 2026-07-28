using System.Runtime.InteropServices;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// An xkbcommon library context: the hub that holds include paths, logging
/// state and keymap/compose-table factories. Contexts are cheap; most
/// applications need exactly one.
/// </summary>
public sealed unsafe class XkbContext : IDisposable
{
    private xkb_context* _context;

    private XkbContext(xkb_context* context)
    {
        _context = context;
    }

    /// <summary>Creates a new context.</summary>
    /// <exception cref="XkbException">The context could not be created.</exception>
    public static XkbContext Create(XkbContextFlags flags = XkbContextFlags.None)
    {
        var context = Libxkbcommon.xkb_context_new((xkb_context_flags)flags);
        return context is null
            ? throw new XkbException("Failed to create xkbcommon context")
            : new XkbContext(context);
    }

    /// <summary>The native xkb_context handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the context has been disposed.</summary>
    public bool IsDisposed => _context is null;

    internal xkb_context* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_context is null, this);
            return _context;
        }
    }

    /// <summary>The minimum priority of messages the context logs.</summary>
    public XkbLogLevel LogLevel
    {
        get => (XkbLogLevel)Libxkbcommon.xkb_context_get_log_level(NativePtr);
        set => Libxkbcommon.xkb_context_set_log_level(NativePtr, (xkb_log_level)value);
    }

    /// <summary>
    /// The log verbosity (0..10); messages of Warning and below are only
    /// logged when the verbosity is raised above the default of 0.
    /// </summary>
    public int LogVerbosity
    {
        get => Libxkbcommon.xkb_context_get_log_verbosity(NativePtr);
        set => Libxkbcommon.xkb_context_set_log_verbosity(NativePtr, value);
    }

    /// <summary>A snapshot of the context's current include paths, in order.</summary>
    public IReadOnlyList<string> IncludePaths
    {
        get
        {
            uint count = Libxkbcommon.xkb_context_num_include_paths(NativePtr);
            var paths = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
            {
                var path = Libxkbcommon.xkb_context_include_path_get(NativePtr, i);
                if (path is not null)
                {
                    paths.Add(Marshal.PtrToStringUTF8((IntPtr)path)!);
                }
            }

            return paths;
        }
    }

    /// <summary>Appends a new entry to the include path.</summary>
    /// <exception cref="XkbException">The path is inaccessible.</exception>
    public void AppendIncludePath(string path)
    {
        fixed (byte* pathPtr = Utf8.NullTerminated(path))
        {
            if (Libxkbcommon.xkb_context_include_path_append(NativePtr, (sbyte*)pathPtr) == 0)
            {
                throw new XkbException($"Failed to append include path '{path}'");
            }
        }
    }

    /// <summary>Appends the default include paths (XDG dirs, XKB_CONFIG_ROOT, the system path).</summary>
    /// <exception cref="XkbException">No default path could be appended.</exception>
    public void AppendDefaultIncludePaths()
    {
        if (Libxkbcommon.xkb_context_include_path_append_default(NativePtr) == 0)
        {
            throw new XkbException("Failed to append default include paths");
        }
    }

    /// <summary>Replaces the include paths with the default ones.</summary>
    /// <exception cref="XkbException">No default path could be appended.</exception>
    public void ResetIncludePathsToDefault()
    {
        if (Libxkbcommon.xkb_context_include_path_reset_defaults(NativePtr) == 0)
        {
            throw new XkbException("Failed to reset include paths to defaults");
        }
    }

    /// <summary>Removes all entries from the include path.</summary>
    public void ClearIncludePaths() => Libxkbcommon.xkb_context_include_path_clear(NativePtr);

    /// <summary>
    /// Compiles a keymap from RMLVO names; the default names compile the
    /// system keymap.
    /// </summary>
    /// <exception cref="XkbException">The keymap could not be compiled.</exception>
    public XkbKeymap CreateKeymap(XkbRuleNames names = default)
    {
        fixed (byte* rules = Utf8.NullTerminated(names.Rules))
        fixed (byte* model = Utf8.NullTerminated(names.Model))
        fixed (byte* layout = Utf8.NullTerminated(names.Layout))
        fixed (byte* variant = Utf8.NullTerminated(names.Variant))
        fixed (byte* options = Utf8.NullTerminated(names.Options))
        {
            var ruleNames = new xkb_rule_names
            {
                rules = (sbyte*)rules,
                model = (sbyte*)model,
                layout = (sbyte*)layout,
                variant = (sbyte*)variant,
                options = (sbyte*)options,
            };

            var keymap = Libxkbcommon.xkb_keymap_new_from_names(
                NativePtr, &ruleNames, xkb_keymap_compile_flags.XKB_KEYMAP_COMPILE_NO_FLAGS);
            return keymap is null
                ? throw new XkbException("Failed to compile keymap from RMLVO names")
                : new XkbKeymap(this, keymap);
        }
    }

    /// <summary>Compiles a keymap from a complete keymap string.</summary>
    /// <exception cref="XkbException">The keymap could not be compiled.</exception>
    public XkbKeymap CreateKeymapFromString(string keymap, XkbKeymapFormat format = XkbKeymapFormat.TextV1)
    {
        fixed (byte* keymapPtr = Utf8.NullTerminated(keymap))
        {
            var native = Libxkbcommon.xkb_keymap_new_from_string(
                NativePtr, (sbyte*)keymapPtr, (xkb_keymap_format)format,
                xkb_keymap_compile_flags.XKB_KEYMAP_COMPILE_NO_FLAGS);
            return native is null
                ? throw new XkbException("Failed to compile keymap from string")
                : new XkbKeymap(this, native);
        }
    }

    /// <summary>
    /// Compiles a keymap from a UTF-8 buffer holding a complete keymap
    /// (e.g. one received over the Wayland wl_keyboard.keymap event). The
    /// buffer may, but need not, be null-terminated.
    /// </summary>
    /// <exception cref="XkbException">The keymap could not be compiled.</exception>
    public XkbKeymap CreateKeymapFromBuffer(ReadOnlySpan<byte> buffer, XkbKeymapFormat format = XkbKeymapFormat.TextV1)
    {
        // xkb_keymap_new_from_buffer treats an embedded NUL as end-of-input;
        // trim a trailing terminator so mmap'd Wayland keymaps just work.
        if (!buffer.IsEmpty && buffer[^1] == 0)
        {
            buffer = buffer[..^1];
        }

        fixed (byte* bufferPtr = buffer)
        {
            var native = Libxkbcommon.xkb_keymap_new_from_buffer(
                NativePtr, (sbyte*)bufferPtr, (nuint)buffer.Length, (xkb_keymap_format)format,
                xkb_keymap_compile_flags.XKB_KEYMAP_COMPILE_NO_FLAGS);
            return native is null
                ? throw new XkbException("Failed to compile keymap from buffer")
                : new XkbKeymap(this, native);
        }
    }

    /// <summary>Compiles a keymap from a keymap file.</summary>
    /// <exception cref="XkbException">The keymap could not be compiled.</exception>
    public XkbKeymap CreateKeymapFromFile(string path, XkbKeymapFormat format = XkbKeymapFormat.TextV1)
        => CreateKeymapFromBuffer(File.ReadAllBytes(path), format);

    /// <summary>
    /// Creates a compose table for the given locale (or the current locale
    /// per LC_CTYPE if null), loading the user's or system Compose file.
    /// </summary>
    /// <exception cref="XkbException">No Compose file could be found or parsed.</exception>
    public XkbComposeTable CreateComposeTable(string? locale = null)
    {
        locale ??= GetCurrentLocale();
        fixed (byte* localePtr = Utf8.NullTerminated(locale))
        {
            var table = Libxkbcommon.xkb_compose_table_new_from_locale(
                NativePtr, (sbyte*)localePtr, xkb_compose_compile_flags.XKB_COMPOSE_COMPILE_NO_FLAGS);
            return table is null
                ? throw new XkbException($"Failed to create compose table for locale '{locale}'")
                : new XkbComposeTable(this, table);
        }
    }

    /// <summary>Creates a compose table from a UTF-8 buffer in Compose file format.</summary>
    /// <param name="buffer">The Compose file content.</param>
    /// <param name="locale">The locale used for parsing, e.g. "en_US.UTF-8".</param>
    /// <exception cref="XkbException">The table could not be parsed.</exception>
    public XkbComposeTable CreateComposeTableFromBuffer(ReadOnlySpan<byte> buffer, string locale)
    {
        fixed (byte* bufferPtr = buffer)
        fixed (byte* localePtr = Utf8.NullTerminated(locale))
        {
            var table = Libxkbcommon.xkb_compose_table_new_from_buffer(
                NativePtr, (sbyte*)bufferPtr, (nuint)buffer.Length, (sbyte*)localePtr,
                xkb_compose_format.XKB_COMPOSE_FORMAT_TEXT_V1,
                xkb_compose_compile_flags.XKB_COMPOSE_COMPILE_NO_FLAGS);
            return table is null
                ? throw new XkbException("Failed to create compose table from buffer")
                : new XkbComposeTable(this, table);
        }
    }

    /// <summary>
    /// Unreferences the context. Keymaps, states and compose tables keep
    /// their own reference, so they stay valid (and disposable) afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_context is not null)
        {
            Libxkbcommon.xkb_context_unref(_context);
            _context = null;
        }
    }

    private static string GetCurrentLocale()
    {
        // Mirror the lookup order documented for compose-table creation:
        // LC_ALL beats LC_CTYPE beats LANG; "C" is the portable fallback.
        return Environment.GetEnvironmentVariable("LC_ALL")
            ?? Environment.GetEnvironmentVariable("LC_CTYPE")
            ?? Environment.GetEnvironmentVariable("LANG")
            ?? "C";
    }
}
