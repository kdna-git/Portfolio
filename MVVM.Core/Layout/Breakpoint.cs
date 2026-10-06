namespace MVVM.Core.Layout;

/// <summary>
/// Width classes the UI adapts to. Named after the sizes rather than the devices, because the
/// same device can be in any of them (a rotated phone, a resized browser, a split desktop window).
/// </summary>
public enum Breakpoint
{
    /// <summary>Phone portrait, narrow browser window: single column, bottom navigation.</summary>
    Compact,

    /// <summary>Phone landscape, tablet, half-screen desktop: two columns.</summary>
    Medium,

    /// <summary>Tablet landscape, desktop: three or more columns, side navigation.</summary>
    Expanded,
}

public static class Breakpoints
{
    /// <summary>Widths below this are <see cref="Breakpoint.Compact"/>.</summary>
    public const double MediumMinWidth = 720;

    /// <summary>Widths at or above this are <see cref="Breakpoint.Expanded"/>.</summary>
    public const double ExpandedMinWidth = 1180;

    public static Breakpoint FromWidth(double width) =>
        width switch
        {
            < MediumMinWidth => Breakpoint.Compact,
            < ExpandedMinWidth => Breakpoint.Medium,
            _ => Breakpoint.Expanded,
        };

    /// <summary>The style class name matching a breakpoint, for use in XAML selectors.</summary>
    public static string ClassName(this Breakpoint breakpoint) =>
        breakpoint switch
        {
            Breakpoint.Compact => "compact",
            Breakpoint.Medium => "medium",
            _ => "expanded",
        };
}
