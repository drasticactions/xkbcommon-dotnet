namespace Xkb;

/// <summary>
/// Well-known modifier, virtual modifier and LED names from
/// xkbcommon-names.h, for use with the name-based lookups on
/// <see cref="XkbKeymap"/> and <see cref="XkbState"/>.
/// </summary>
public static class XkbNames
{
    /// <summary>The real Shift modifier.</summary>
    public const string ModShift = "Shift";

    /// <summary>The real Lock (caps lock) modifier.</summary>
    public const string ModCaps = "Lock";

    /// <summary>The real Control modifier.</summary>
    public const string ModCtrl = "Control";

    /// <summary>The real Mod1 modifier (usually mapped to Alt).</summary>
    public const string ModMod1 = "Mod1";

    /// <summary>The real Mod2 modifier (usually mapped to NumLock).</summary>
    public const string ModMod2 = "Mod2";

    /// <summary>The real Mod3 modifier.</summary>
    public const string ModMod3 = "Mod3";

    /// <summary>The real Mod4 modifier (usually mapped to Super/logo).</summary>
    public const string ModMod4 = "Mod4";

    /// <summary>The real Mod5 modifier (usually mapped to AltGr).</summary>
    public const string ModMod5 = "Mod5";

    /// <summary>The virtual Alt modifier.</summary>
    public const string VModAlt = "Alt";

    /// <summary>The virtual Hyper modifier.</summary>
    public const string VModHyper = "Hyper";

    /// <summary>The virtual LevelThree (AltGr) modifier.</summary>
    public const string VModLevel3 = "LevelThree";

    /// <summary>The virtual LevelFive modifier.</summary>
    public const string VModLevel5 = "LevelFive";

    /// <summary>The virtual Meta modifier.</summary>
    public const string VModMeta = "Meta";

    /// <summary>The virtual NumLock modifier.</summary>
    public const string VModNum = "NumLock";

    /// <summary>The virtual ScrollLock modifier.</summary>
    public const string VModScroll = "ScrollLock";

    /// <summary>The virtual Super (logo) modifier.</summary>
    public const string VModSuper = "Super";

    /// <summary>The Num Lock LED.</summary>
    public const string LedNum = "Num Lock";

    /// <summary>The Caps Lock LED.</summary>
    public const string LedCaps = "Caps Lock";

    /// <summary>The Scroll Lock LED.</summary>
    public const string LedScroll = "Scroll Lock";

    /// <summary>The Compose LED.</summary>
    public const string LedCompose = "Compose";

    /// <summary>The Kana LED.</summary>
    public const string LedKana = "Kana";
}
