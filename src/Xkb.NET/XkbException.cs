namespace Xkb;

/// <summary>
/// Thrown when an xkbcommon operation fails. xkbcommon reports failure by
/// returning null (or a false/negative result) without an errno contract;
/// details, when available, go to the context's log.
/// </summary>
public sealed class XkbException : Exception
{
    /// <summary>Creates a new exception with the given message.</summary>
    public XkbException(string message)
        : base(message)
    {
    }
}
