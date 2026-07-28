namespace Xkb;

/// <summary>
/// The RMLVO (rules, model, layouts, variants, options) names used to compile
/// a keymap. Null members fall back to xkbcommon's defaults (which consult
/// the XKB_DEFAULT_* environment variables unless the context was created
/// with <see cref="XkbContextFlags.NoEnvironmentNames"/>).
/// </summary>
public readonly record struct XkbRuleNames
{
    /// <summary>The rules file to use, e.g. "evdev".</summary>
    public string? Rules { get; init; }

    /// <summary>The keyboard model, e.g. "pc105".</summary>
    public string? Model { get; init; }

    /// <summary>A comma-separated list of layouts, e.g. "us,de".</summary>
    public string? Layout { get; init; }

    /// <summary>A comma-separated list of variants, one per layout, e.g. ",nodeadkeys".</summary>
    public string? Variant { get; init; }

    /// <summary>A comma-separated list of options, e.g. "grp:alt_shift_toggle".</summary>
    public string? Options { get; init; }
}
