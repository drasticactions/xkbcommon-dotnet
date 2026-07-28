using System.Text;
using Xunit;

namespace Xkb.Tests;

/// <summary>
/// Compose tests against a self-contained Compose buffer; only the locale
/// test depends on the system's compose files.
/// </summary>
public class ComposeTests
{
    private const string ComposeText = "<dead_acute> <a> : \"á\" aacute\n";

    [Fact]
    public void ComposeTable_FromBuffer_ListsEntries()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var table = context.CreateComposeTableFromBuffer(Encoding.UTF8.GetBytes(ComposeText), "C");

        var entry = Assert.Single(table.GetEntries());
        Assert.Equal([XkbKeysym.FromName("dead_acute"), XkbKeysym.FromName("a")], entry.Sequence);
        Assert.Equal(XkbKeysym.FromName("aacute"), entry.Keysym);
        Assert.Equal("á", entry.Utf8);
    }

    [Fact]
    public void ComposeState_MatchesSequence()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var table = context.CreateComposeTableFromBuffer(Encoding.UTF8.GetBytes(ComposeText), "C");
        using var state = table.CreateState();

        Assert.Equal(XkbComposeStatus.Nothing, state.Status);

        Assert.Equal(XkbComposeFeedResult.Accepted, state.Feed(XkbKeysym.FromName("dead_acute")));
        Assert.Equal(XkbComposeStatus.Composing, state.Status);

        Assert.Equal(XkbComposeFeedResult.Accepted, state.Feed(XkbKeysym.FromName("a")));
        Assert.Equal(XkbComposeStatus.Composed, state.Status);
        Assert.Equal("á", state.GetUtf8());
        Assert.Equal(XkbKeysym.FromName("aacute"), state.GetOneSym());

        state.Reset();
        Assert.Equal(XkbComposeStatus.Nothing, state.Status);
    }

    [Fact]
    public void ComposeState_CancelsOnMismatch()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var table = context.CreateComposeTableFromBuffer(Encoding.UTF8.GetBytes(ComposeText), "C");
        using var state = table.CreateState();

        state.Feed(XkbKeysym.FromName("dead_acute"));
        state.Feed(XkbKeysym.FromName("x"));
        Assert.Equal(XkbComposeStatus.Cancelled, state.Status);
        Assert.Equal(string.Empty, state.GetUtf8());
    }

    [Fact]
    public void ComposeTable_FromLocale()
    {
        using var context = XkbContext.Create();

        XkbComposeTable table;
        try
        {
            table = context.CreateComposeTable("en_US.UTF-8");
        }
        catch (XkbException)
        {
            Assert.Skip("no Compose file available for en_US.UTF-8");
            return;
        }

        using (table)
        {
            Assert.NotEmpty(table.GetEntries());
        }
    }
}
