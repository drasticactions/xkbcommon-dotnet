using Xkb.Native;
using Xunit;

namespace Xkb.Tests;

/// <summary>Tests of the <see cref="XkbKeysym"/> wrapper.</summary>
public class KeysymTests
{
    [Fact]
    public void FromName_FindsKeysym()
    {
        Assert.Equal((uint)Libxkbcommon.XKB_KEY_Return, XkbKeysym.FromName("Return").Value);
        Assert.Equal((uint)Libxkbcommon.XKB_KEY_a, XkbKeysym.FromName("a").Value);
        Assert.True(XkbKeysym.FromName("NoSuchKeysymName").IsNone);
    }

    [Fact]
    public void FromName_CaseInsensitive()
    {
        Assert.Equal((uint)Libxkbcommon.XKB_KEY_Return, XkbKeysym.FromName("return", caseInsensitive: true).Value);
    }

    [Fact]
    public void Name_RoundTrips()
    {
        Assert.Equal("Return", new XkbKeysym((uint)Libxkbcommon.XKB_KEY_Return).Name);
        Assert.Equal("dead_acute", XkbKeysym.FromName("dead_acute").Name);
    }

    [Fact]
    public void Utf_Conversions()
    {
        var a = new XkbKeysym((uint)Libxkbcommon.XKB_KEY_a);
        Assert.Equal((uint)'a', a.Utf32);
        Assert.Equal("a", a.ToUtf8String());
        Assert.Equal(a, XkbKeysym.FromUtf32('a'));

        // Return has no printable representation but a control representation.
        Assert.Equal("\r", new XkbKeysym((uint)Libxkbcommon.XKB_KEY_Return).ToUtf8String());
    }

    [Fact]
    public void CaseMapping_Works()
    {
        var a = new XkbKeysym((uint)Libxkbcommon.XKB_KEY_a);
        var upperA = new XkbKeysym((uint)Libxkbcommon.XKB_KEY_A);
        Assert.Equal(upperA, a.ToUpper());
        Assert.Equal(a, upperA.ToLower());
    }

    [Fact]
    public void ToString_UsesNameOrHex()
    {
        Assert.Equal("a", new XkbKeysym((uint)Libxkbcommon.XKB_KEY_a).ToString());
    }
}
