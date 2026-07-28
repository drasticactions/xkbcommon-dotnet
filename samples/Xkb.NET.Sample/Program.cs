using Xkb;

string layout = args.Length > 0 ? args[0] : "us";
string? variant = args.Length > 1 ? args[1] : null;

using var context = XkbContext.Create();

// --- Keymap: compile from RMLVO names (requires xkeyboard-config). ---
XkbKeymap keymap;
try
{
    keymap = context.CreateKeymap(new XkbRuleNames { Rules = "evdev", Layout = layout, Variant = variant });
}
catch (XkbException e)
{
    Console.Error.WriteLine($"Failed to compile layout '{layout}': {e.Message} (is xkeyboard-config installed?)");
    return 1;
}

using (keymap)
{
    Console.WriteLine($"Compiled keymap: layout={layout} variant={variant ?? "(none)"}");
    Console.WriteLine($"  keycodes {keymap.MinKeycode}..{keymap.MaxKeycode}, " +
                      $"{keymap.ModCount} mods, {keymap.LayoutCount} layouts, {keymap.LedCount} LEDs");
    for (uint i = 0; i < keymap.LayoutCount; i++)
    {
        Console.WriteLine($"  layout {i}: {keymap.GetLayoutName(i)}");
    }

    // --- State: simulate pressing the "AC01".."AC05" row (asdfg on us). ---
    using var state = keymap.CreateState();
    Console.WriteLine();
    Console.WriteLine("Home row without and with Shift:");
    uint? shiftKeycode = keymap.GetKeyByName("LFSH");
    for (int round = 0; round < 2; round++)
    {
        bool shifted = round == 1;
        if (shifted && shiftKeycode is uint sk)
        {
            state.UpdateKey(sk, XkbKeyDirection.Down);
        }

        for (int i = 1; i <= 5; i++)
        {
            if (keymap.GetKeyByName($"AC0{i}") is not uint keycode)
            {
                continue;
            }

            var sym = state.GetKeyOneSym(keycode);
            Console.WriteLine($"  <AC0{i}> (keycode {keycode}){(shifted ? " +Shift" : string.Empty),-7} " +
                              $"-> keysym {sym,-12} text \"{state.GetKeyString(keycode)}\"");
        }

        if (shifted && shiftKeycode is uint sk2)
        {
            state.UpdateKey(sk2, XkbKeyDirection.Up);
        }
    }
}

// --- Compose: dead_acute + a -> á, from an inline Compose table. ---
Console.WriteLine();
using (var table = context.CreateComposeTableFromBuffer("<dead_acute> <a> : \"á\" aacute\n"u8, "C"))
using (var compose = table.CreateState())
{
    compose.Feed(XkbKeysym.FromName("dead_acute"));
    compose.Feed(XkbKeysym.FromName("a"));
    Console.WriteLine($"Compose dead_acute + a: status={compose.Status}, text=\"{compose.GetUtf8()}\", keysym={compose.GetOneSym()}");
}

// --- Registry: what layouts does this system offer? ---
Console.WriteLine();
try
{
    using var registry = XkbRegistry.Create();
    var layouts = registry.Layouts;
    Console.WriteLine($"Registry: {registry.Models.Count} models, {layouts.Count} layout/variant entries, " +
                      $"{registry.OptionGroups.Count} option groups. First five layouts:");
    foreach (var entry in layouts.Take(5))
    {
        Console.WriteLine($"  {entry.Name}{(entry.Variant is null ? string.Empty : $"({entry.Variant})"),-18} {entry.Description}");
    }
}
catch (XkbException e)
{
    Console.WriteLine($"Registry unavailable: {e.Message}");
}

return 0;
