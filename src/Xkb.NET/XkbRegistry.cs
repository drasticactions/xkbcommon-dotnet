using System.Runtime.InteropServices;
using Xkb.Native;

namespace Xkb;

/// <summary>
/// The XKB registry (libxkbregistry): enumerates the models, layouts and
/// options available from the installed xkeyboard-config rulesets, for
/// presenting keyboard-configuration choices to users.
/// </summary>
public sealed unsafe class XkbRegistry : IDisposable
{
    private rxkb_context* _context;

    private XkbRegistry(rxkb_context* context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a registry and parses a ruleset (the default ruleset, usually
    /// "evdev", when <paramref name="ruleset"/> is null).
    /// </summary>
    /// <exception cref="XkbException">The registry could not be created or the ruleset not parsed.</exception>
    public static XkbRegistry Create(XkbRegistryFlags flags = XkbRegistryFlags.None, string? ruleset = null)
    {
        var context = Libxkbregistry.rxkb_context_new((rxkb_context_flags)flags);
        if (context is null)
        {
            throw new XkbException("Failed to create registry context");
        }

        byte parsed;
        if (ruleset is null)
        {
            parsed = Libxkbregistry.rxkb_context_parse_default_ruleset(context);
        }
        else
        {
            fixed (byte* rulesetPtr = Utf8.NullTerminated(ruleset))
            {
                parsed = Libxkbregistry.rxkb_context_parse(context, (sbyte*)rulesetPtr);
            }
        }

        if (parsed == 0)
        {
            Libxkbregistry.rxkb_context_unref(context);
            throw new XkbException($"Failed to parse ruleset '{ruleset ?? "(default)"}'");
        }

        return new XkbRegistry(context);
    }

    /// <summary>The native rxkb_context handle, for use with the raw API.</summary>
    public IntPtr Handle => (IntPtr)NativePtr;

    /// <summary>True once the registry has been disposed.</summary>
    public bool IsDisposed => _context is null;

    internal rxkb_context* NativePtr
    {
        get
        {
            ObjectDisposedException.ThrowIf(_context is null, this);
            return _context;
        }
    }

    /// <summary>A snapshot of the keyboard models in the ruleset.</summary>
    public IReadOnlyList<XkbRegistryModel> Models
    {
        get
        {
            var models = new List<XkbRegistryModel>();
            for (var model = Libxkbregistry.rxkb_model_first(NativePtr);
                 model is not null;
                 model = Libxkbregistry.rxkb_model_next(model))
            {
                models.Add(new XkbRegistryModel(
                    ToString(Libxkbregistry.rxkb_model_get_name(model))!,
                    ToString(Libxkbregistry.rxkb_model_get_vendor(model)),
                    ToString(Libxkbregistry.rxkb_model_get_description(model)),
                    (XkbPopularity)Libxkbregistry.rxkb_model_get_popularity(model)));
            }

            return models;
        }
    }

    /// <summary>
    /// A snapshot of the layout/variant combinations in the ruleset. Each
    /// variant is a separate entry; a null <see cref="XkbRegistryLayout.Variant"/>
    /// is the layout's base entry.
    /// </summary>
    public IReadOnlyList<XkbRegistryLayout> Layouts
    {
        get
        {
            var layouts = new List<XkbRegistryLayout>();
            for (var layout = Libxkbregistry.rxkb_layout_first(NativePtr);
                 layout is not null;
                 layout = Libxkbregistry.rxkb_layout_next(layout))
            {
                var iso639 = new List<string>();
                for (var code = Libxkbregistry.rxkb_layout_get_iso639_first(layout);
                     code is not null;
                     code = Libxkbregistry.rxkb_iso639_code_next(code))
                {
                    iso639.Add(ToString(Libxkbregistry.rxkb_iso639_code_get_code(code))!);
                }

                var iso3166 = new List<string>();
                for (var code = Libxkbregistry.rxkb_layout_get_iso3166_first(layout);
                     code is not null;
                     code = Libxkbregistry.rxkb_iso3166_code_next(code))
                {
                    iso3166.Add(ToString(Libxkbregistry.rxkb_iso3166_code_get_code(code))!);
                }

                layouts.Add(new XkbRegistryLayout(
                    ToString(Libxkbregistry.rxkb_layout_get_name(layout))!,
                    ToString(Libxkbregistry.rxkb_layout_get_variant(layout)),
                    ToString(Libxkbregistry.rxkb_layout_get_brief(layout)),
                    ToString(Libxkbregistry.rxkb_layout_get_description(layout)),
                    (XkbPopularity)Libxkbregistry.rxkb_layout_get_popularity(layout),
                    iso639,
                    iso3166));
            }

            return layouts;
        }
    }

    /// <summary>A snapshot of the option groups (and their options) in the ruleset.</summary>
    public IReadOnlyList<XkbRegistryOptionGroup> OptionGroups
    {
        get
        {
            var groups = new List<XkbRegistryOptionGroup>();
            for (var group = Libxkbregistry.rxkb_option_group_first(NativePtr);
                 group is not null;
                 group = Libxkbregistry.rxkb_option_group_next(group))
            {
                var options = new List<XkbRegistryOption>();
                for (var option = Libxkbregistry.rxkb_option_first(group);
                     option is not null;
                     option = Libxkbregistry.rxkb_option_next(option))
                {
                    options.Add(new XkbRegistryOption(
                        ToString(Libxkbregistry.rxkb_option_get_name(option))!,
                        ToString(Libxkbregistry.rxkb_option_get_brief(option)),
                        ToString(Libxkbregistry.rxkb_option_get_description(option)),
                        (XkbPopularity)Libxkbregistry.rxkb_option_get_popularity(option),
                        Libxkbregistry.rxkb_option_is_layout_specific(option) != 0));
                }

                groups.Add(new XkbRegistryOptionGroup(
                    ToString(Libxkbregistry.rxkb_option_group_get_name(group))!,
                    ToString(Libxkbregistry.rxkb_option_group_get_description(group)),
                    Libxkbregistry.rxkb_option_group_allows_multiple(group) != 0,
                    (XkbPopularity)Libxkbregistry.rxkb_option_group_get_popularity(group),
                    options));
            }

            return groups;
        }
    }

    /// <summary>Unreferences the registry context.</summary>
    public void Dispose()
    {
        if (_context is not null)
        {
            Libxkbregistry.rxkb_context_unref(_context);
            _context = null;
        }
    }

    private static string? ToString(sbyte* value) =>
        value is null ? null : Marshal.PtrToStringUTF8((IntPtr)value);
}

/// <summary>A keyboard model from the registry.</summary>
/// <param name="Name">The model name, e.g. "pc105".</param>
/// <param name="Vendor">The vendor name, if any.</param>
/// <param name="Description">The human-readable description, if any.</param>
/// <param name="Popularity">Whether the model is standard or exotic.</param>
public sealed record XkbRegistryModel(string Name, string? Vendor, string? Description, XkbPopularity Popularity);

/// <summary>A layout/variant combination from the registry.</summary>
/// <param name="Name">The layout name, e.g. "de".</param>
/// <param name="Variant">The variant, e.g. "nodeadkeys", or null for the base layout.</param>
/// <param name="Brief">The brief name, e.g. "de", if any.</param>
/// <param name="Description">The human-readable description, if any.</param>
/// <param name="Popularity">Whether the layout is standard or exotic.</param>
/// <param name="Iso639Codes">ISO 639-3 language codes for the layout.</param>
/// <param name="Iso3166Codes">ISO 3166 country codes for the layout.</param>
public sealed record XkbRegistryLayout(
    string Name,
    string? Variant,
    string? Brief,
    string? Description,
    XkbPopularity Popularity,
    IReadOnlyList<string> Iso639Codes,
    IReadOnlyList<string> Iso3166Codes);

/// <summary>An option from the registry.</summary>
/// <param name="Name">The option name including its group prefix, e.g. "grp:alt_shift_toggle".</param>
/// <param name="Brief">The brief name, if any.</param>
/// <param name="Description">The human-readable description, if any.</param>
/// <param name="Popularity">Whether the option is standard or exotic.</param>
/// <param name="IsLayoutSpecific">Whether the option applies per layout.</param>
public sealed record XkbRegistryOption(
    string Name, string? Brief, string? Description, XkbPopularity Popularity, bool IsLayoutSpecific);

/// <summary>An option group from the registry.</summary>
/// <param name="Name">The group name, e.g. "grp".</param>
/// <param name="Description">The human-readable description, if any.</param>
/// <param name="AllowsMultiple">Whether several options in the group may be active at once.</param>
/// <param name="Popularity">Whether the group is standard or exotic.</param>
/// <param name="Options">The options in the group.</param>
public sealed record XkbRegistryOptionGroup(
    string Name,
    string? Description,
    bool AllowsMultiple,
    XkbPopularity Popularity,
    IReadOnlyList<XkbRegistryOption> Options);
