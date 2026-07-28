namespace Xkb.Tests;

/// <summary>
/// A minimal self-contained keymap so keymap/state tests do not depend on an
/// installed xkeyboard-config: one two-level number key and a Shift key.
/// </summary>
internal static class TestKeymap
{
    internal const uint KeycodeAe01 = 10;
    internal const uint KeycodeLeftShift = 50;

    internal const string Text = """
        xkb_keymap {
            xkb_keycodes {
                minimum = 8;
                maximum = 255;
                <AE01> = 10;
                <LFSH> = 50;
            };
            xkb_types {
                type "ONE_LEVEL" {
                    modifiers = none;
                    level_name[Level1] = "Any";
                };
                type "TWO_LEVEL" {
                    modifiers = Shift;
                    map[Shift] = Level2;
                    level_name[Level1] = "Base";
                    level_name[Level2] = "Shift";
                };
            };
            xkb_compat {
                interpret Shift_L {
                    action = SetMods(modifiers = Shift);
                };
            };
            xkb_symbols {
                key <AE01> { type = "TWO_LEVEL", [ 1, exclam ] };
                key <LFSH> { [ Shift_L ] };
                modifier_map Shift { <LFSH> };
            };
        };
        """;
}
