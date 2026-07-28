using System.Text;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// An XKB keysym: a symbolic identifier for the meaning produced by a key
/// press, e.g. "a", "Return" or "dead_acute". Wraps the raw
/// <c>xkb_keysym_t</c> value; the XKB_KEY_* constants live on
/// <see cref="Libxkbcommon"/>.
/// </summary>
/// <param name="Value">The raw keysym value.</param>
public readonly record struct XkbKeysym(uint Value)
{
    /// <summary>The NoSymbol keysym (0), returned when a key produces no symbol.</summary>
    public static readonly XkbKeysym None = new(0);

    /// <summary>True if this is the NoSymbol keysym.</summary>
    public bool IsNone => Value == 0;

    /// <summary>
    /// The canonical name of the keysym (e.g. "a", "Return", "dead_acute"),
    /// or null if the keysym is invalid.
    /// </summary>
    public unsafe string? Name
    {
        get
        {
            var buffer = stackalloc sbyte[64];
            int length = Libxkbcommon.xkb_keysym_get_name(Value, buffer, 64);
            return length < 0 ? null : Marshal((byte*)buffer, Math.Min(length, 63));
        }
    }

    /// <summary>
    /// The Unicode code point of the keysym, or 0 if it has no Unicode
    /// representation.
    /// </summary>
    public uint Utf32 => Libxkbcommon.xkb_keysym_to_utf32(Value);

    /// <summary>
    /// Looks up a keysym by name (e.g. "a", "Return", "U+1F4A9", "0x1008FF11").
    /// </summary>
    /// <param name="name">The keysym name.</param>
    /// <param name="caseInsensitive">
    /// Match case-insensitively. Ambiguous matches resolve to the lowercase
    /// keysym; prefer exact matching where possible.
    /// </param>
    /// <returns>The keysym, or <see cref="None"/> if the name is not recognized.</returns>
    public static unsafe XkbKeysym FromName(string name, bool caseInsensitive = false)
    {
        fixed (byte* namePtr = Utf8.NullTerminated(name))
        {
            return new(Libxkbcommon.xkb_keysym_from_name(
                (sbyte*)namePtr,
                caseInsensitive ? xkb_keysym_flags.XKB_KEYSYM_CASE_INSENSITIVE : xkb_keysym_flags.XKB_KEYSYM_NO_FLAGS));
        }
    }

    /// <summary>
    /// Gets the keysym corresponding to a Unicode code point (direct encoding
    /// or a matching function keysym), or <see cref="None"/> if there is none.
    /// </summary>
    public static XkbKeysym FromUtf32(uint codePoint) => new(Libxkbcommon.xkb_utf32_to_keysym(codePoint));

    /// <summary>
    /// The UTF-8 string produced by the keysym, or an empty string if it has
    /// no Unicode representation.
    /// </summary>
    public unsafe string ToUtf8String()
    {
        var buffer = stackalloc sbyte[8];
        int written = Libxkbcommon.xkb_keysym_to_utf8(Value, buffer, 8);

        // The result counts the terminating byte; 0 means no representation.
        return written <= 1 ? string.Empty : Marshal((byte*)buffer, written - 1);
    }

    /// <summary>Converts the keysym to its uppercase form, or returns it unchanged.</summary>
    public XkbKeysym ToUpper() => new(Libxkbcommon.xkb_keysym_to_upper(Value));

    /// <summary>Converts the keysym to its lowercase form, or returns it unchanged.</summary>
    public XkbKeysym ToLower() => new(Libxkbcommon.xkb_keysym_to_lower(Value));

    /// <summary>The keysym name, or the hex value if the keysym is invalid.</summary>
    public override string ToString() => Name ?? $"0x{Value:x8}";

    /// <summary>Converts the keysym to its raw <c>xkb_keysym_t</c> value.</summary>
    public static implicit operator uint(XkbKeysym keysym) => keysym.Value;

    /// <summary>Wraps a raw <c>xkb_keysym_t</c> value.</summary>
    public static implicit operator XkbKeysym(uint value) => new(value);

    private static unsafe string Marshal(byte* buffer, int length) =>
        Encoding.UTF8.GetString(buffer, length);
}
