using System.Text;

namespace Xkb;

/// <summary>
/// UTF-8 marshaling helpers for the wrapper layer.
/// </summary>
internal static class Utf8
{
    /// <summary>
    /// Encodes a string as a null-terminated UTF-8 byte array, or null for a
    /// null input — so a <c>fixed</c> statement over the result pins either a
    /// valid C string or a null pointer.
    /// </summary>
    internal static byte[]? NullTerminated(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var bytes = new byte[Encoding.UTF8.GetByteCount(value) + 1];
        Encoding.UTF8.GetBytes(value, bytes);
        return bytes;
    }
}
