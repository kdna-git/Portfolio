using Avalonia;
using Avalonia.Styling;
using SkiaSharp;

namespace Portfolio.Charts.Theming;

/// <summary>
/// The colours every chart in the app draws from.
/// </summary>
/// <remarks>
/// Two rules make a dashboard of many charts read as one system:
/// <list type="number">
/// <item>Categorical slots are handed out in a fixed order and never cycled past the eighth — a
/// ninth series folds into "Other" instead of reusing slot 1 and implying a relationship that is
/// not there.</item>
/// <item>A colour follows the entity, not its rank. Ask for a slot by the entity's key (via
/// <see cref="SlotForKey"/>) and filtering the list will not repaint the survivors.</item>
/// </list>
/// Dark mode is a selected set of steps for the dark surface, not an automatic lightening of the
/// light one. Both sets were validated for colour-vision separation and contrast against the actual
/// surfaces this app renders on.
/// </remarks>
public static class ChartPalette
{
    private static readonly string[] LightCategorical =
    [
        "#2a78d6", // blue
        "#eb6834", // orange
        "#1baf7a", // aqua
        "#eda100", // yellow
        "#e87ba4", // magenta
        "#008300", // green
        "#4a3aa7", // violet
        "#e34948", // red
    ];

    private static readonly string[] DarkCategorical =
    [
        "#3987e5",
        "#d95926",
        "#199e70",
        "#c98500",
        "#d55181",
        "#008300",
        "#9085e9",
        "#e66767",
    ];

    /// <summary>How many distinct series a chart may show before folding the rest into "Other".</summary>
    public const int MaxCategoricalSlots = 8;

    /// <summary>Label used for the folded remainder.</summary>
    public const string OtherLabel = "Other";

    /// <summary>True when the app is currently showing its dark theme.</summary>
    public static bool IsDark =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public static IReadOnlyList<string> Categorical => IsDark ? DarkCategorical : LightCategorical;

    /// <summary>Categorical colour for a slot index. Index 8 and beyond reuse the "Other" grey.</summary>
    public static string Slot(int index)
    {
        if (index < 0)
            index = 0;

        return index < MaxCategoricalSlots ? Categorical[index] : Neutral;
    }

    /// <summary>
    /// A stable slot for an entity key, so the same platform keeps the same colour across charts
    /// and across filter changes.
    /// </summary>
    public static string SlotForKey(string? key, IReadOnlyList<string> orderedKeys)
    {
        if (key is null)
            return Neutral;

        var index = -1;

        for (var i = 0; i < orderedKeys.Count; i++)
        {
            if (orderedKeys[i] == key)
            {
                index = i;
                break;
            }
        }

        return index < 0 ? Neutral : Slot(index);
    }

    #region Semantic colours

    /// <summary>Money coming in, value going up.</summary>
    public static string Positive => IsDark ? "#0ca30c" : "#0ca30c";

    /// <summary>Money going out, value going down.</summary>
    public static string Negative => "#d03b3b";

    public static string Warning => "#fab219";

    public static string Serious => "#ec835a";

    /// <summary>Folded remainder and anything deliberately unemphasised.</summary>
    public static string Neutral => IsDark ? "#8a8a84" : "#898781";

    /// <summary>A reference line: what you paid in, a target, a benchmark.</summary>
    public static string Reference => IsDark ? "#c3c2b7" : "#52514e";

    #endregion

    #region Chrome

    public static string Surface => IsDark ? "#16181b" : "#ffffff";

    public static string PrimaryInk => IsDark ? "#ffffff" : "#0b0b0b";

    public static string SecondaryInk => IsDark ? "#c3c2b7" : "#52514e";

    public static string MutedInk => "#898781";

    public static string Gridline => IsDark ? "#2c2c2a" : "#e1e0d9";

    public static string AxisLine => IsDark ? "#383835" : "#c3c2b7";

    #endregion

    public static SKColor Color(string hex) => SKColor.Parse(hex);

    /// <summary>The same colour at a given opacity, for area fills under a line.</summary>
    public static SKColor Fade(string hex, double opacity)
    {
        var color = SKColor.Parse(hex);
        var alpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);
        return new SKColor(color.Red, color.Green, color.Blue, alpha);
    }

    /// <summary>Colour for a signed value: up is good, down is not, zero is neutral.</summary>
    public static string ForSign(double value) =>
        value switch
        {
            > 0 => Positive,
            < 0 => Negative,
            _ => Neutral,
        };
}
