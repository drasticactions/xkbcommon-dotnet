# xkbcommon-dotnet

xkbcommon-dotnet are .NET bindings for [libxkbcommon](https://xkbcommon.org).

## Usage

```csharp
using Xkb;

using var context = XkbContext.Create();
using var keymap = context.CreateKeymap(new XkbRuleNames { Layout = "de", Options = "grp:alt_shift_toggle" });
using var state = keymap.CreateState();

// Feed evdev key events (keycode = evdev code + 8) and read the results.
state.UpdateKey(keycode, XkbKeyDirection.Down);
XkbKeysym sym = state.GetKeyOneSym(keycode);
Console.WriteLine($"{sym.Name} -> \"{state.GetKeyString(keycode)}\"");
```

Dead keys via compose:

```csharp
using var compose = context.CreateComposeTable().CreateState();
compose.Feed(XkbKeysym.FromName("dead_acute"));
compose.Feed(XkbKeysym.FromName("a"));
if (compose.Status == XkbComposeStatus.Composed)
    Console.WriteLine(compose.GetUtf8()); // á
```

Enumerating available layouts with the registry:

```csharp
using var registry = XkbRegistry.Create();
foreach (var layout in registry.Layouts)
    Console.WriteLine($"{layout.Name} {layout.Variant}: {layout.Description}");
```

X11 clients can build keymaps from the server with `XkbX11`. The xcb connection is passed as a raw pointer:

```csharp
var info = XkbX11.SetupXkbExtension(xcbConnection);
int deviceId = XkbX11.GetCoreKeyboardDeviceId(xcbConnection);
using var keymap = XkbX11.CreateKeymap(context, xcbConnection, deviceId);
using var state = XkbX11.CreateState(keymap, xcbConnection, deviceId);
```

## Testing

```sh
dotnet test
```

Most tests use a self-contained keymap and compose table and only need `libxkbcommon.so.0`; the RMLVO/registry tests skip when xkeyboard-config is not installed.

## Regenerating the bindings

```sh
git submodule update --init
dotnet tool restore
sh eng/generate.sh
```

Requires a system clang for its builtin-header resource directory, and the libxcb headers for the X11 pass.
