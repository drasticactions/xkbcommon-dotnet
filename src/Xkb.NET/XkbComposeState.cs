using System.Text;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// A compose state machine: feed it the keysyms the user types and it tracks
/// dead-key/compose sequences against its <see cref="XkbComposeTable"/>.
/// </summary>
public sealed unsafe class XkbComposeState : IDisposable
{
    private xkb_compose_state* _state;

    internal XkbComposeState(XkbComposeTable table, xkb_compose_state* state)
    {
        Table = table;
        _state = state;
    }

    /// <summary>The compose table the state was created from.</summary>
    public XkbComposeTable Table { get; }

    /// <summary>The native xkb_compose_state handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the state has been disposed.</summary>
    public bool IsDisposed => _state is null;

    internal xkb_compose_state* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_state is null, this);
            return _state;
        }
    }

    /// <summary>The current status of the state machine.</summary>
    public XkbComposeStatus Status => (XkbComposeStatus)Libxkbcommon.xkb_compose_state_get_status(NativePtr);

    /// <summary>
    /// Feeds one keysym (typically from <see cref="XkbState.GetKeyOneSym"/> on
    /// a key press, not a release) to the state machine. Modifier keysyms are
    /// ignored. Check <see cref="Status"/> after every accepted keysym.
    /// </summary>
    public XkbComposeFeedResult Feed(XkbKeysym keysym) =>
        (XkbComposeFeedResult)Libxkbcommon.xkb_compose_state_feed(NativePtr, keysym.Value);

    /// <summary>Resets the state machine to <see cref="XkbComposeStatus.Nothing"/>.</summary>
    public void Reset() => Libxkbcommon.xkb_compose_state_reset(NativePtr);

    /// <summary>
    /// The string the matched sequence produces, or an empty string unless
    /// the status is <see cref="XkbComposeStatus.Composed"/>.
    /// </summary>
    public string GetUtf8()
    {
        var buffer = stackalloc sbyte[64];
        int required = Libxkbcommon.xkb_compose_state_get_utf8(NativePtr, buffer, 64);
        if (required <= 0)
        {
            return string.Empty;
        }

        if (required < 64)
        {
            return Encoding.UTF8.GetString((byte*)buffer, required);
        }

        var bytes = new byte[required + 1];
        fixed (byte* bytesPtr = bytes)
        {
            Libxkbcommon.xkb_compose_state_get_utf8(NativePtr, (sbyte*)bytesPtr, (nuint)bytes.Length);
            return Encoding.UTF8.GetString(bytes, 0, required);
        }
    }

    /// <summary>
    /// The keysym the matched sequence produces, or <see cref="XkbKeysym.None"/>
    /// unless the status is <see cref="XkbComposeStatus.Composed"/>.
    /// </summary>
    public XkbKeysym GetOneSym() => new(Libxkbcommon.xkb_compose_state_get_one_sym(NativePtr));

    /// <summary>Unreferences the state.</summary>
    public void Dispose()
    {
        if (_state is not null)
        {
            Libxkbcommon.xkb_compose_state_unref(_state);
            _state = null;
        }
    }
}
