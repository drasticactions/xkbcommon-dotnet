using Xkb.Native;
using Xunit;

namespace Xkb.Tests;

/// <summary>
/// Tests of the generated bindings that need no keymap data, only an
/// installed libxkbcommon.so.0.
/// </summary>
public class NativeBindingTests
{
    [Fact]
    public void GeneratedConstants_HaveExpectedValues()
    {
        Assert.Equal(0x61u, (uint)Libxkbcommon.XKB_KEY_a);
        Assert.Equal(0xff0du, (uint)Libxkbcommon.XKB_KEY_Return);
        Assert.Equal(0x1fffffff, Libxkbcommon.XKB_KEYSYM_MAX);
        Assert.Equal(0xffffffffu, Libxkbcommon.XKB_MOD_INVALID);
        Assert.True("Shift"u8.SequenceEqual(Libxkbcommon.XKB_MOD_NAME_SHIFT));

        // The fixup applied by eng/generate.sh.
        Assert.Equal(unchecked((xkb_keymap_format)(-1)), Libxkbcommon.XKB_KEYMAP_USE_ORIGINAL_FORMAT);
    }

    [Fact]
    public void Libxkbcommon_ResolvesNativeLibrary()
    {
        // Any call proves the DllImportResolver found libxkbcommon.so.0.
        Assert.Equal((uint)'a', Libxkbcommon.xkb_keysym_to_utf32((uint)Libxkbcommon.XKB_KEY_a));
    }

    [Fact]
    public unsafe void Libxkbcommon_KeysymNameRoundTrips()
    {
        var buffer = stackalloc sbyte[64];
        int length = Libxkbcommon.xkb_keysym_get_name((uint)Libxkbcommon.XKB_KEY_Return, buffer, 64);
        Assert.True(length > 0);
        Assert.Equal("Return", System.Text.Encoding.UTF8.GetString((byte*)buffer, length));
    }

    [Fact]
    public unsafe void Libxkbregistry_ResolvesNativeLibrary()
    {
        var context = Libxkbregistry.rxkb_context_new(rxkb_context_flags.RXKB_CONTEXT_NO_FLAGS);
        Assert.True(context is not null);
        Libxkbregistry.rxkb_context_unref(context);
    }

    [Fact]
    public void LibxkbcommonX11_LibraryIsLoadable()
    {
        // No X server in the test environment; just prove the soname the
        // resolver probes for is present and loadable.
        Assert.SkipWhen(
            !System.Runtime.InteropServices.NativeLibrary.TryLoad("libxkbcommon-x11.so.0", out var handle),
            "libxkbcommon-x11.so.0 is not installed");
        System.Runtime.InteropServices.NativeLibrary.Free(handle);
    }
}
