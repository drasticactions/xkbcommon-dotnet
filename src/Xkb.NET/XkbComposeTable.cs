using System.Runtime.InteropServices;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// A compiled, immutable compose table: the set of compose/dead-key sequences
/// (usually from a Compose file) that an <see cref="XkbComposeState"/> matches
/// against.
/// </summary>
public sealed unsafe class XkbComposeTable : IDisposable
{
    private xkb_compose_table* _table;

    internal XkbComposeTable(XkbContext context, xkb_compose_table* table)
    {
        Context = context;
        _table = table;
    }

    /// <summary>The context the table was created with.</summary>
    public XkbContext Context { get; }

    /// <summary>The native xkb_compose_table handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the table has been disposed.</summary>
    public bool IsDisposed => _table is null;

    internal xkb_compose_table* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_table is null, this);
            return _table;
        }
    }

    /// <summary>
    /// A snapshot of every compose sequence in the table, in lexicographic
    /// order of the left-hand side.
    /// </summary>
    public IReadOnlyList<XkbComposeEntry> GetEntries()
    {
        var iterator = Libxkbcommon.xkb_compose_table_iterator_new(NativePtr);
        if (iterator is null)
        {
            throw new XkbException("Failed to create compose table iterator");
        }

        try
        {
            var entries = new List<XkbComposeEntry>();
            xkb_compose_table_entry* entry;
            while ((entry = Libxkbcommon.xkb_compose_table_iterator_next(iterator)) is not null)
            {
                nuint sequenceLength;
                uint* sequence = Libxkbcommon.xkb_compose_table_entry_sequence(entry, &sequenceLength);
                var utf8 = Libxkbcommon.xkb_compose_table_entry_utf8(entry);
                entries.Add(new XkbComposeEntry(
                    XkbKeymap.CopyKeysyms(sequence, (int)sequenceLength),
                    new XkbKeysym(Libxkbcommon.xkb_compose_table_entry_keysym(entry)),
                    utf8 is null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)utf8)!));
            }

            return entries;
        }
        finally
        {
            Libxkbcommon.xkb_compose_table_iterator_free(iterator);
        }
    }

    /// <summary>Creates a new compose state machine for this table.</summary>
    /// <exception cref="XkbException">The state could not be created.</exception>
    public XkbComposeState CreateState()
    {
        var state = Libxkbcommon.xkb_compose_state_new(NativePtr, xkb_compose_state_flags.XKB_COMPOSE_STATE_NO_FLAGS);
        return state is null
            ? throw new XkbException("Failed to create compose state")
            : new XkbComposeState(this, state);
    }

    /// <summary>
    /// Unreferences the table. States created from it keep their own
    /// reference, so they stay valid (and disposable) afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_table is not null)
        {
            Libxkbcommon.xkb_compose_table_unref(_table);
            _table = null;
        }
    }
}

/// <summary>One compose sequence: a left-hand side of keysyms and the result it produces.</summary>
/// <param name="Sequence">The keysyms that make up the sequence.</param>
/// <param name="Keysym">The right-hand-side keysym, or <see cref="XkbKeysym.None"/>.</param>
/// <param name="Utf8">The right-hand-side string, or an empty string.</param>
public sealed record XkbComposeEntry(IReadOnlyList<XkbKeysym> Sequence, XkbKeysym Keysym, string Utf8);
