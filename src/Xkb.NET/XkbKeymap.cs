using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// A compiled, immutable keymap: the description of how keycodes translate to
/// keysyms under the various modifier and layout states.
/// </summary>
public sealed unsafe class XkbKeymap : IDisposable
{
    private xkb_keymap* _keymap;

    internal XkbKeymap(XkbContext context, xkb_keymap* keymap)
    {
        Context = context;
        _keymap = keymap;
    }

    /// <summary>The context the keymap was compiled with.</summary>
    public XkbContext Context { get; }

    /// <summary>The native xkb_keymap handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the keymap has been disposed.</summary>
    public bool IsDisposed => _keymap is null;

    internal xkb_keymap* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_keymap is null, this);
            return _keymap;
        }
    }

    /// <summary>The lowest keycode in the keymap.</summary>
    public uint MinKeycode => Libxkbcommon.xkb_keymap_min_keycode(NativePtr);

    /// <summary>The highest keycode in the keymap.</summary>
    public uint MaxKeycode => Libxkbcommon.xkb_keymap_max_keycode(NativePtr);

    /// <summary>The number of modifiers in the keymap.</summary>
    public uint ModCount => Libxkbcommon.xkb_keymap_num_mods(NativePtr);

    /// <summary>The number of layouts in the keymap.</summary>
    public uint LayoutCount => Libxkbcommon.xkb_keymap_num_layouts(NativePtr);

    /// <summary>The number of LEDs in the keymap.</summary>
    public uint LedCount => Libxkbcommon.xkb_keymap_num_leds(NativePtr);

    /// <summary>Serializes the keymap back to a keymap string.</summary>
    /// <exception cref="XkbException">The keymap could not be serialized.</exception>
    public string AsString(XkbKeymapFormat format = XkbKeymapFormat.UseOriginalFormat)
    {
        var text = Libxkbcommon.xkb_keymap_get_as_string(NativePtr, (xkb_keymap_format)format);
        if (text is null)
        {
            throw new XkbException("Failed to serialize keymap");
        }

        try
        {
            return Marshal.PtrToStringUTF8((IntPtr)text)!;
        }
        finally
        {
            Libc.Free(text);
        }
    }

    /// <summary>Every keycode that exists in the keymap, in ascending order.</summary>
    public uint[] GetKeycodes()
    {
        var keycodes = new List<uint>();
        var handle = GCHandle.Alloc(keycodes);
        try
        {
            Libxkbcommon.xkb_keymap_key_for_each(NativePtr, &CollectKeycode, (void*)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return [.. keycodes];
    }

    /// <summary>The XKB name of a key (e.g. "AC01"), or null if the keycode is invalid.</summary>
    public string? GetKeyName(uint keycode)
    {
        var name = Libxkbcommon.xkb_keymap_key_get_name(NativePtr, keycode);
        return name is null ? null : Marshal.PtrToStringUTF8((IntPtr)name);
    }

    /// <summary>The keycode with the given XKB name (or alias), or null if there is none.</summary>
    public uint? GetKeyByName(string name)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            uint keycode = Libxkbcommon.xkb_keymap_key_by_name(NativePtr, (sbyte*)namePtr);
            return keycode == Libxkbcommon.XKB_KEYCODE_INVALID ? null : keycode;
        }
    }

    /// <summary>The name of the modifier with the given index, or null if the index is invalid.</summary>
    public string? GetModName(uint index)
    {
        var name = Libxkbcommon.xkb_keymap_mod_get_name(NativePtr, index);
        return name is null ? null : Marshal.PtrToStringUTF8((IntPtr)name);
    }

    /// <summary>The index of the modifier with the given name, or null if there is none.</summary>
    public uint? GetModIndex(string name)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            uint index = Libxkbcommon.xkb_keymap_mod_get_index(NativePtr, (sbyte*)namePtr);
            return index == Libxkbcommon.XKB_MOD_INVALID ? null : index;
        }
    }

    /// <summary>The name of the layout with the given index, or null if the index is invalid or the layout is unnamed.</summary>
    public string? GetLayoutName(uint index)
    {
        var name = Libxkbcommon.xkb_keymap_layout_get_name(NativePtr, index);
        return name is null ? null : Marshal.PtrToStringUTF8((IntPtr)name);
    }

    /// <summary>The index of the (first) layout with the given name, or null if there is none.</summary>
    public uint? GetLayoutIndex(string name)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            uint index = Libxkbcommon.xkb_keymap_layout_get_index(NativePtr, (sbyte*)namePtr);
            return index == Libxkbcommon.XKB_LAYOUT_INVALID ? null : index;
        }
    }

    /// <summary>The name of the LED with the given index, or null if the index is invalid.</summary>
    public string? GetLedName(uint index)
    {
        var name = Libxkbcommon.xkb_keymap_led_get_name(NativePtr, index);
        return name is null ? null : Marshal.PtrToStringUTF8((IntPtr)name);
    }

    /// <summary>The index of the LED with the given name, or null if there is none.</summary>
    public uint? GetLedIndex(string name)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            uint index = Libxkbcommon.xkb_keymap_led_get_index(NativePtr, (sbyte*)namePtr);
            return index == Libxkbcommon.XKB_LED_INVALID ? null : index;
        }
    }

    /// <summary>The number of layouts for a specific key (may differ from <see cref="LayoutCount"/>).</summary>
    public uint GetNumLayoutsForKey(uint keycode) => Libxkbcommon.xkb_keymap_num_layouts_for_key(NativePtr, keycode);

    /// <summary>The number of shift levels for a specific key and layout.</summary>
    public uint GetNumLevelsForKey(uint keycode, uint layout) => Libxkbcommon.xkb_keymap_num_levels_for_key(NativePtr, keycode, layout);

    /// <summary>
    /// The keysyms obtained from pressing a key at a given layout and shift
    /// level, independent of any state. Empty if the key produces nothing there.
    /// </summary>
    public XkbKeysym[] GetKeySymsByLevel(uint keycode, uint layout, uint level)
    {
        uint* syms;
        int count = Libxkbcommon.xkb_keymap_key_get_syms_by_level(NativePtr, keycode, layout, level, &syms);
        return CopyKeysyms(syms, count);
    }

    /// <summary>
    /// The modifier masks that produce a given shift level for a key and
    /// layout. Empty if the level is not reachable.
    /// </summary>
    public uint[] GetModsForLevel(uint keycode, uint layout, uint level)
    {
        // Grow until the fixed-size output buffer holds every mask.
        Span<uint> masks = stackalloc uint[16];
        fixed (uint* masksPtr = masks)
        {
            nuint count = Libxkbcommon.xkb_keymap_key_get_mods_for_level(
                NativePtr, keycode, layout, level, masksPtr, (nuint)masks.Length);
            return masks[..(int)count].ToArray();
        }
    }

    /// <summary>Whether the given key should repeat while held down.</summary>
    public bool KeyRepeats(uint keycode) => Libxkbcommon.xkb_keymap_key_repeats(NativePtr, keycode) != 0;

    /// <summary>Creates a new keyboard state object for this keymap.</summary>
    /// <exception cref="XkbException">The state could not be created.</exception>
    public XkbState CreateState()
    {
        var state = Libxkbcommon.xkb_state_new(NativePtr);
        return state is null
            ? throw new XkbException("Failed to create keyboard state")
            : new XkbState(this, state);
    }

    /// <summary>
    /// Unreferences the keymap. States created from it keep their own
    /// reference, so they stay valid (and disposable) afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_keymap is not null)
        {
            Libxkbcommon.xkb_keymap_unref(_keymap);
            _keymap = null;
        }
    }

    internal static XkbKeysym[] CopyKeysyms(uint* syms, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var result = new XkbKeysym[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new XkbKeysym(syms[i]);
        }

        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CollectKeycode(xkb_keymap* keymap, uint keycode, void* data)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)data);
        ((List<uint>)handle.Target!).Add(keycode);
    }
}
