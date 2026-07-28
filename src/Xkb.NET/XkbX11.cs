using Xkb.Native;

namespace Xkb;

/// <summary>
/// Helpers for building keymaps and states from an X server via the XKB X11
/// extension (libxkbcommon-x11). The xcb connection is passed as the raw
/// <c>xcb_connection_t*</c> pointer, since no xcb binding is involved.
/// </summary>
public static unsafe class XkbX11
{
    /// <summary>
    /// Sets up the XKB X11 extension for the given connection. Must be called
    /// once per connection before the other functions; it also enables the
    /// detectable-autorepeat X feature.
    /// </summary>
    /// <param name="connection">A native <c>xcb_connection_t*</c>.</param>
    /// <returns>The negotiated extension version and event/error base.</returns>
    /// <exception cref="XkbException">The server does not support a compatible XKB version.</exception>
    public static XkbX11ExtensionInfo SetupXkbExtension(IntPtr connection)
    {
        ushort major, minor;
        byte baseEvent, baseError;
        int ok = LibxkbcommonX11.xkb_x11_setup_xkb_extension(
            (xcb_connection_t*)connection,
            (ushort)LibxkbcommonX11.XKB_X11_MIN_MAJOR_XKB_VERSION,
            (ushort)LibxkbcommonX11.XKB_X11_MIN_MINOR_XKB_VERSION,
            xkb_x11_setup_xkb_extension_flags.XKB_X11_SETUP_XKB_EXTENSION_NO_FLAGS,
            &major, &minor, &baseEvent, &baseError);
        return ok == 0
            ? throw new XkbException("Failed to set up the XKB X11 extension")
            : new XkbX11ExtensionInfo(major, minor, baseEvent, baseError);
    }

    /// <summary>The XInput device ID of the core keyboard.</summary>
    /// <param name="connection">A native <c>xcb_connection_t*</c>.</param>
    /// <exception cref="XkbException">The device ID could not be queried.</exception>
    public static int GetCoreKeyboardDeviceId(IntPtr connection)
    {
        int deviceId = LibxkbcommonX11.xkb_x11_get_core_keyboard_device_id((xcb_connection_t*)connection);
        return deviceId == -1
            ? throw new XkbException("Failed to query the core keyboard device ID")
            : deviceId;
    }

    /// <summary>Creates a keymap from an X11 keyboard device.</summary>
    /// <param name="context">The context to associate the keymap with.</param>
    /// <param name="connection">A native <c>xcb_connection_t*</c>.</param>
    /// <param name="deviceId">An XInput device ID, e.g. from <see cref="GetCoreKeyboardDeviceId"/>.</param>
    /// <exception cref="XkbException">The keymap could not be created.</exception>
    public static XkbKeymap CreateKeymap(XkbContext context, IntPtr connection, int deviceId)
    {
        var keymap = LibxkbcommonX11.xkb_x11_keymap_new_from_device(
            context.NativePtr, (xcb_connection_t*)connection, deviceId,
            xkb_keymap_compile_flags.XKB_KEYMAP_COMPILE_NO_FLAGS);
        return keymap is null
            ? throw new XkbException($"Failed to create keymap from X11 device {deviceId}")
            : new XkbKeymap(context, keymap);
    }

    /// <summary>
    /// Creates a state initialized from the current state of an X11 keyboard
    /// device. Unlike <see cref="XkbKeymap.CreateState"/>, the returned state
    /// is intended to be updated with <see cref="XkbState.UpdateMask"/> from
    /// XKB state-notify events, not with key events.
    /// </summary>
    /// <param name="keymap">A keymap created with <see cref="CreateKeymap"/> for the same device.</param>
    /// <param name="connection">A native <c>xcb_connection_t*</c>.</param>
    /// <param name="deviceId">The XInput device ID the keymap was created from.</param>
    /// <exception cref="XkbException">The state could not be created.</exception>
    public static XkbState CreateState(XkbKeymap keymap, IntPtr connection, int deviceId)
    {
        var state = LibxkbcommonX11.xkb_x11_state_new_from_device(
            keymap.NativePtr, (xcb_connection_t*)connection, deviceId);
        return state is null
            ? throw new XkbException($"Failed to create state from X11 device {deviceId}")
            : new XkbState(keymap, state);
    }
}

/// <summary>The result of <see cref="XkbX11.SetupXkbExtension"/>.</summary>
/// <param name="MajorVersion">The negotiated major XKB version.</param>
/// <param name="MinorVersion">The negotiated minor XKB version.</param>
/// <param name="BaseEvent">The base event code of the XKB extension.</param>
/// <param name="BaseError">The base error code of the XKB extension.</param>
public sealed record XkbX11ExtensionInfo(ushort MajorVersion, ushort MinorVersion, byte BaseEvent, byte BaseError);
