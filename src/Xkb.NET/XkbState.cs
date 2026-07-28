using System.Text;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// A keyboard state machine: tracks which modifiers, layout and LEDs are
/// active as key events are fed in, and translates keycodes to keysyms and
/// text under that state.
/// </summary>
public sealed unsafe class XkbState : IDisposable
{
    private xkb_state* _state;

    internal XkbState(XkbKeymap keymap, xkb_state* state)
    {
        Keymap = keymap;
        _state = state;
    }

    /// <summary>The keymap the state was created from.</summary>
    public XkbKeymap Keymap { get; }

    /// <summary>The native xkb_state handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the state has been disposed.</summary>
    public bool IsDisposed => _state is null;

    internal xkb_state* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_state is null, this);
            return _state;
        }
    }

    /// <summary>
    /// Updates the state with a key press or release event and returns the
    /// state components that changed. Use this when the application drives
    /// the keyboard directly (e.g. from evdev); under a display server, use
    /// <see cref="UpdateMask"/> with the server-provided values instead.
    /// </summary>
    public XkbStateComponent UpdateKey(uint keycode, XkbKeyDirection direction) =>
        (XkbStateComponent)Libxkbcommon.xkb_state_update_key(NativePtr, keycode, (xkb_key_direction)direction);

    /// <summary>
    /// Updates the state from the modifier and layout values reported by an
    /// external source such as a Wayland compositor (wl_keyboard.modifiers)
    /// and returns the state components that changed.
    /// </summary>
    public XkbStateComponent UpdateMask(
        uint depressedMods, uint latchedMods, uint lockedMods,
        uint depressedLayout, uint latchedLayout, uint lockedLayout) =>
        (XkbStateComponent)Libxkbcommon.xkb_state_update_mask(
            NativePtr, depressedMods, latchedMods, lockedMods, depressedLayout, latchedLayout, lockedLayout);

    /// <summary>The keysyms produced by a key in the current state; empty if it produces none.</summary>
    public XkbKeysym[] GetKeySyms(uint keycode)
    {
        uint* syms;
        int count = Libxkbcommon.xkb_state_key_get_syms(NativePtr, keycode, &syms);
        return XkbKeymap.CopyKeysyms(syms, count);
    }

    /// <summary>
    /// The single keysym produced by a key in the current state, or
    /// <see cref="XkbKeysym.None"/> if it produces none or multiple. This is
    /// the common path and applies the capitalization transformation.
    /// </summary>
    public XkbKeysym GetKeyOneSym(uint keycode) => new(Libxkbcommon.xkb_state_key_get_one_sym(NativePtr, keycode));

    /// <summary>The UTF-8 string produced by a key in the current state, or an empty string.</summary>
    public string GetKeyString(uint keycode)
    {
        var buffer = stackalloc sbyte[64];
        int required = Libxkbcommon.xkb_state_key_get_utf8(NativePtr, keycode, buffer, 64);
        if (required <= 0)
        {
            return string.Empty;
        }

        if (required < 64)
        {
            return Encoding.UTF8.GetString((byte*)buffer, required);
        }

        // Rare: the produced string was truncated; retry with the exact size.
        var bytes = new byte[required + 1];
        fixed (byte* bytesPtr = bytes)
        {
            Libxkbcommon.xkb_state_key_get_utf8(NativePtr, keycode, (sbyte*)bytesPtr, (nuint)bytes.Length);
            return Encoding.UTF8.GetString(bytes, 0, required);
        }
    }

    /// <summary>The Unicode code point produced by a key in the current state, or 0.</summary>
    public uint GetKeyUtf32(uint keycode) => Libxkbcommon.xkb_state_key_get_utf32(NativePtr, keycode);

    /// <summary>The effective layout for a key in the current state, or null if the keycode is invalid.</summary>
    public uint? GetKeyLayout(uint keycode)
    {
        uint layout = Libxkbcommon.xkb_state_key_get_layout(NativePtr, keycode);
        return layout == Libxkbcommon.XKB_LAYOUT_INVALID ? null : layout;
    }

    /// <summary>The shift level for a key in the current state and given layout, or null if invalid.</summary>
    public uint? GetKeyLevel(uint keycode, uint layout)
    {
        uint level = Libxkbcommon.xkb_state_key_get_level(NativePtr, keycode, layout);
        return level == Libxkbcommon.XKB_LEVEL_INVALID ? null : level;
    }

    /// <summary>Serializes the given modifier components to a modifier mask for an external protocol.</summary>
    public uint SerializeMods(XkbStateComponent components) =>
        Libxkbcommon.xkb_state_serialize_mods(NativePtr, (xkb_state_component)components);

    /// <summary>Serializes the given layout components to a layout index for an external protocol.</summary>
    public uint SerializeLayout(XkbStateComponent components) =>
        Libxkbcommon.xkb_state_serialize_layout(NativePtr, (xkb_state_component)components);

    /// <summary>Whether the modifier with the given name is active in the given components.</summary>
    public bool IsModActive(string name, XkbStateComponent components = XkbStateComponent.ModsEffective)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            return Libxkbcommon.xkb_state_mod_name_is_active(
                NativePtr, (sbyte*)namePtr, (xkb_state_component)components) > 0;
        }
    }

    /// <summary>Whether the modifier with the given index is active in the given components.</summary>
    public bool IsModActive(uint index, XkbStateComponent components = XkbStateComponent.ModsEffective) =>
        Libxkbcommon.xkb_state_mod_index_is_active(NativePtr, index, (xkb_state_component)components) > 0;

    /// <summary>Whether the layout with the given name is active in the given components.</summary>
    public bool IsLayoutActive(string name, XkbStateComponent components = XkbStateComponent.LayoutEffective)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            return Libxkbcommon.xkb_state_layout_name_is_active(
                NativePtr, (sbyte*)namePtr, (xkb_state_component)components) > 0;
        }
    }

    /// <summary>Whether the layout with the given index is active in the given components.</summary>
    public bool IsLayoutActive(uint index, XkbStateComponent components = XkbStateComponent.LayoutEffective) =>
        Libxkbcommon.xkb_state_layout_index_is_active(NativePtr, index, (xkb_state_component)components) > 0;

    /// <summary>Whether the LED with the given name is lit.</summary>
    public bool IsLedActive(string name)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            return Libxkbcommon.xkb_state_led_name_is_active(NativePtr, (sbyte*)namePtr) > 0;
        }
    }

    /// <summary>Whether the LED with the given index is lit.</summary>
    public bool IsLedActive(uint index) => Libxkbcommon.xkb_state_led_index_is_active(NativePtr, index) > 0;

    /// <summary>
    /// The mask of modifiers consumed in translating the given key — those a
    /// toolkit should ignore when matching shortcuts (e.g. Shift for "%").
    /// </summary>
    public uint GetConsumedMods(uint keycode, XkbConsumedMode mode = XkbConsumedMode.Xkb) =>
        Libxkbcommon.xkb_state_key_get_consumed_mods2(NativePtr, keycode, (xkb_consumed_mode)mode);

    /// <summary>Whether the modifier with the given index is consumed in translating the given key.</summary>
    public bool IsModConsumed(uint keycode, uint modIndex, XkbConsumedMode mode = XkbConsumedMode.Xkb) =>
        Libxkbcommon.xkb_state_mod_index_is_consumed2(NativePtr, keycode, modIndex, (xkb_consumed_mode)mode) > 0;

    /// <summary>Removes the modifiers consumed by the given key from a modifier mask.</summary>
    public uint RemoveConsumedMods(uint keycode, uint mask) =>
        Libxkbcommon.xkb_state_mod_mask_remove_consumed(NativePtr, keycode, mask);

    /// <summary>Unreferences the state.</summary>
    public void Dispose()
    {
        if (_state is not null)
        {
            Libxkbcommon.xkb_state_unref(_state);
            _state = null;
        }
    }
}
