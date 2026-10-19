using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.PersonalComputers;

public enum CMUNetFont : byte
{
    /// <summary>The OS terminal font.</summary>
    Terminal,
    Mono,
    Sans,
    Display,
    Pixel,
}

public enum CMUNetBackgroundMode : byte
{
    Stretch,
    Tile,
    Center,
}

/// <summary>
/// How an ExtraNet page looks. Unset fields fall back to the site style, then to the OS palette.
/// Sites set it in YAML; a page overrides it with <c>&lt;CMUNetStyle Background="#102030"/&gt;</c>.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUNetPageStyle
{
    [DataField]
    public Color? Background;

    /// <summary>Body text.</summary>
    [DataField]
    public Color? Text;

    [DataField]
    public Color? Heading;

    [DataField]
    public Color? Link;

    [DataField]
    public CMUNetFont? Font;

    /// <summary>Texture drawn behind the page, e.g. <c>/Textures/CMU14/PersonalComputers/Backgrounds/grid.png</c>.</summary>
    [DataField]
    public ResPath? BackgroundImage;

    [DataField]
    public CMUNetBackgroundMode? BackgroundMode;

    /// <summary>Returns a style where this one's set fields win over <paramref name="fallback"/>.</summary>
    public CMUNetPageStyle Over(CMUNetPageStyle? fallback)
    {
        if (fallback == null)
            return this;

        return new CMUNetPageStyle
        {
            Background = Background ?? fallback.Background,
            Text = Text ?? fallback.Text,
            Heading = Heading ?? fallback.Heading,
            Link = Link ?? fallback.Link,
            Font = Font ?? fallback.Font,
            BackgroundImage = BackgroundImage ?? fallback.BackgroundImage,
            BackgroundMode = BackgroundMode ?? fallback.BackgroundMode,
        };
    }
}
