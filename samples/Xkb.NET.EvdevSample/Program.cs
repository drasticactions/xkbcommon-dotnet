using System.Runtime.InteropServices;
using Xkb;

const ushort EV_KEY = 0x01;
const int KeyRelease = 0;
const int KeyPress = 1;
const int KeyRepeat = 2;

// Linux evdev keycodes are offset by 8 in XKB keymaps.
const uint EvdevOffset = 8;

string? devicePath = args.Length > 0 ? args[0] : FindKeyboardDevice();
string layout = args.Length > 1 ? args[1] : "us";

if (devicePath is null)
{
    Console.Error.WriteLine("No keyboard-capable /dev/input/event* device found (or accessible).");
    return 1;
}

using var context = XkbContext.Create();
using var keymap = context.CreateKeymap(new XkbRuleNames { Rules = "evdev", Layout = layout });
using var state = keymap.CreateState();

// Compose is optional: fall back gracefully when no Compose file matches.
XkbComposeState? compose = null;
try
{
    compose = context.CreateComposeTable().CreateState();
}
catch (XkbException)
{
}

FileStream stream;
try
{
    stream = new FileStream(devicePath, FileMode.Open, FileAccess.Read);
}
catch (UnauthorizedAccessException)
{
    Console.Error.WriteLine($"No permission to read {devicePath} (join the 'input' group or run as root).");
    return 1;
}

Console.WriteLine($"Reading {devicePath} with layout '{layout}'. Ctrl+C to quit.");

using (stream)
using (compose)
{
    int eventSize = Marshal.SizeOf<InputEvent>();
    var buffer = new byte[eventSize];

    while (true)
    {
        stream.ReadExactly(buffer);
        var ev = MemoryMarshal.Read<InputEvent>(buffer);
        if (ev.Type != EV_KEY || ev.Value == KeyRepeat)
        {
            continue;
        }

        uint keycode = ev.Code + EvdevOffset;

        if (ev.Value == KeyPress)
        {
            var sym = state.GetKeyOneSym(keycode);
            string text = state.GetKeyString(keycode);

            // Feed presses (not releases) to the compose machine.
            if (compose is not null && compose.Feed(sym) == XkbComposeFeedResult.Accepted)
            {
                switch (compose.Status)
                {
                    case XkbComposeStatus.Composing:
                        text = string.Empty;
                        break;
                    case XkbComposeStatus.Composed:
                        sym = compose.GetOneSym();
                        text = compose.GetUtf8();
                        compose.Reset();
                        break;
                    case XkbComposeStatus.Cancelled:
                        compose.Reset();
                        text = string.Empty;
                        break;
                }
            }

            string name = keymap.GetKeyName(keycode) ?? "?";
            Console.WriteLine($"press   {keycode,4} <{name,-5}> keysym {sym,-16} text \"{Printable(text)}\"");
        }

        // Key events drive the modifier/layout state machine.
        state.UpdateKey(keycode, ev.Value == KeyPress ? XkbKeyDirection.Down : XkbKeyDirection.Up);

        if (ev.Value == KeyRelease)
        {
            Console.WriteLine($"release {keycode,4}");
        }
    }
}

static string Printable(string text) =>
    string.Concat(text.Select(c => char.IsControl(c) ? $"\\x{(int)c:x2}" : c.ToString()));

static string? FindKeyboardDevice()
{
    // Prefer devices that declare key capabilities in sysfs; EV_KEY is bit 1
    // of the "ev" capability bitmap, and a real keyboard also reports KEY_A
    // (bit 30) in the "key" bitmap.
    foreach (var eventDir in Directory.GetDirectories("/sys/class/input", "event*").OrderBy(SysfsEventNumber))
    {
        try
        {
            string keyBitmap = File.ReadAllText(Path.Combine(eventDir, "device/capabilities/key")).Trim();
            // The bitmap is space-separated hex words, least-significant last;
            // KEY_A = 30 lives in the last word.
            string lastWord = keyBitmap.Split(' ')[^1];
            if ((Convert.ToUInt64(lastWord, 16) & (1ul << 30)) != 0)
            {
                return $"/dev/input/{Path.GetFileName(eventDir)}";
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    return null;
}

static int SysfsEventNumber(string path) =>
    int.TryParse(Path.GetFileName(path)["event".Length..], out int n) ? n : int.MaxValue;

/// <summary>The Linux input_event struct (64-bit layout: two longs, then type/code/value).</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct InputEvent
{
    public readonly nint TimeSeconds;
    public readonly nint TimeMicroseconds;
    public readonly ushort Type;
    public readonly ushort Code;
    public readonly int Value;
}
