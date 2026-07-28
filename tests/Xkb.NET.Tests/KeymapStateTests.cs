using Xkb.Native;
using Xunit;

namespace Xkb.Tests;

/// <summary>
/// Keymap and state tests against the self-contained <see cref="TestKeymap"/>;
/// only the RMLVO test needs an installed xkeyboard-config.
/// </summary>
public class KeymapStateTests
{
    [Fact]
    public void Context_CreateAndConfigure()
    {
        using var context = XkbContext.Create();
        Assert.False(context.IsDisposed);
        Assert.NotEqual(IntPtr.Zero, context.Handle);

        context.LogLevel = XkbLogLevel.Critical;
        Assert.Equal(XkbLogLevel.Critical, context.LogLevel);

        context.LogVerbosity = 3;
        Assert.Equal(3, context.LogVerbosity);
    }

    [Fact]
    public void Context_IncludePaths()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        Assert.Empty(context.IncludePaths);

        var path = Path.GetTempPath();
        context.AppendIncludePath(path);
        Assert.Equal([path.TrimEnd('/') + "/"], [.. context.IncludePaths.Select(p => p.TrimEnd('/') + "/")]);

        context.ClearIncludePaths();
        Assert.Empty(context.IncludePaths);

        Assert.Throws<XkbException>(() => context.AppendIncludePath("/nonexistent/xkb/path"));
    }

    [Fact]
    public void Keymap_CompilesFromString()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var keymap = context.CreateKeymapFromString(TestKeymap.Text);

        // xkbcommon clamps the declared range to the keys actually bound.
        Assert.Equal(TestKeymap.KeycodeAe01, keymap.MinKeycode);
        Assert.Equal(TestKeymap.KeycodeLeftShift, keymap.MaxKeycode);
        Assert.Equal(TestKeymap.KeycodeAe01, keymap.GetKeyByName("AE01"));
        Assert.Equal("AE01", keymap.GetKeyName(TestKeymap.KeycodeAe01));
        Assert.Null(keymap.GetKeyByName("NOPE"));
        // key_for_each visits every keycode in the min..max range.
        var keycodes = keymap.GetKeycodes();
        Assert.Equal(keymap.MinKeycode, keycodes.First());
        Assert.Equal(keymap.MaxKeycode, keycodes.Last());
        Assert.Contains(TestKeymap.KeycodeAe01, keycodes);
        Assert.Contains(TestKeymap.KeycodeLeftShift, keycodes);

        uint? shift = keymap.GetModIndex(XkbNames.ModShift);
        Assert.NotNull(shift);
        Assert.Equal(XkbNames.ModShift, keymap.GetModName(shift.Value));

        Assert.Equal(1u, keymap.LayoutCount);
        Assert.Equal(2u, keymap.GetNumLevelsForKey(TestKeymap.KeycodeAe01, 0));
        Assert.Equal(
            [XkbKeysym.FromName("exclam")],
            keymap.GetKeySymsByLevel(TestKeymap.KeycodeAe01, layout: 0, level: 1));
    }

    [Fact]
    public void Keymap_CompilesFromBuffer_AndSerializes()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var keymap = context.CreateKeymapFromBuffer(System.Text.Encoding.UTF8.GetBytes(TestKeymap.Text));

        var serialized = keymap.AsString();
        Assert.Contains("xkb_keymap", serialized);

        // The serialized form must round trip.
        using var again = context.CreateKeymapFromString(serialized);
        Assert.Equal(TestKeymap.KeycodeAe01, again.GetKeyByName("AE01"));
    }

    [Fact]
    public void State_TracksShiftModifier()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var keymap = context.CreateKeymapFromString(TestKeymap.Text);
        using var state = keymap.CreateState();

        // Unshifted: the number key produces "1".
        Assert.Equal(XkbKeysym.FromName("1"), state.GetKeyOneSym(TestKeymap.KeycodeAe01));
        Assert.Equal("1", state.GetKeyString(TestKeymap.KeycodeAe01));
        Assert.Equal((uint)'1', state.GetKeyUtf32(TestKeymap.KeycodeAe01));
        Assert.False(state.IsModActive(XkbNames.ModShift));

        // Pressing Shift changes the depressed modifiers.
        var changed = state.UpdateKey(TestKeymap.KeycodeLeftShift, XkbKeyDirection.Down);
        Assert.True(changed.HasFlag(XkbStateComponent.ModsDepressed));
        Assert.True(state.IsModActive(XkbNames.ModShift));
        Assert.Equal(XkbKeysym.FromName("exclam"), state.GetKeyOneSym(TestKeymap.KeycodeAe01));
        Assert.Equal("!", state.GetKeyString(TestKeymap.KeycodeAe01));
        Assert.Equal(1u, state.GetKeyLevel(TestKeymap.KeycodeAe01, 0));

        // Shift is consumed when translating the shifted key.
        uint shiftMask = 1u << (int)keymap.GetModIndex(XkbNames.ModShift)!.Value;
        Assert.Equal(shiftMask, state.GetConsumedMods(TestKeymap.KeycodeAe01) & shiftMask);

        // Releasing Shift restores the base level.
        state.UpdateKey(TestKeymap.KeycodeLeftShift, XkbKeyDirection.Up);
        Assert.False(state.IsModActive(XkbNames.ModShift));
        Assert.Equal("1", state.GetKeyString(TestKeymap.KeycodeAe01));
    }

    [Fact]
    public void State_UpdateMask_And_Serialize()
    {
        using var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        using var keymap = context.CreateKeymapFromString(TestKeymap.Text);
        using var state = keymap.CreateState();

        uint shiftMask = 1u << (int)keymap.GetModIndex(XkbNames.ModShift)!.Value;
        state.UpdateMask(shiftMask, 0, 0, 0, 0, 0);

        Assert.True(state.IsModActive(XkbNames.ModShift, XkbStateComponent.ModsDepressed));
        Assert.Equal(shiftMask, state.SerializeMods(XkbStateComponent.ModsDepressed));
        Assert.Equal(0u, state.SerializeLayout(XkbStateComponent.LayoutEffective));
    }

    [Fact]
    public void Keymap_CompilesFromRmlvoNames()
    {
        using var context = XkbContext.Create();

        XkbKeymap keymap;
        try
        {
            keymap = context.CreateKeymap(new XkbRuleNames { Rules = "evdev", Layout = "us" });
        }
        catch (XkbException)
        {
            Assert.Skip("xkeyboard-config is not installed");
            return;
        }

        using (keymap)
        {
            // KEY_A in evdev is keycode 30, +8 offset = 38, named <AC01>.
            using var state = keymap.CreateState();
            Assert.Equal((uint)Libxkbcommon.XKB_KEY_a, state.GetKeyOneSym(38).Value);
            Assert.True(keymap.LayoutCount >= 1);
        }
    }

    [Fact]
    public void DisposalOrder_IsSafe()
    {
        // xkbcommon objects are refcounted: dependents keep their owners
        // alive, so disposing in any order must not crash.
        var context = XkbContext.Create(XkbContextFlags.NoDefaultIncludes);
        var keymap = context.CreateKeymapFromString(TestKeymap.Text);
        var state = keymap.CreateState();

        context.Dispose();
        keymap.Dispose();
        Assert.Equal("1", state.GetKeyString(TestKeymap.KeycodeAe01));
        state.Dispose();

        Assert.Throws<ObjectDisposedException>(() => state.GetKeyOneSym(TestKeymap.KeycodeAe01));
    }
}
