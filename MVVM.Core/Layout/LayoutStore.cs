using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MVVM.Core.Layout;

/// <summary>
/// Single source of truth for "how much room do we have". The shell pushes the viewport size in
/// here whenever it changes; view models read <see cref="Breakpoint"/> and the column helpers so
/// the same view adapts on desktop resize, browser resize and device rotation alike.
/// </summary>
public sealed partial class LayoutStore : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Breakpoint))]
    [NotifyPropertyChangedFor(nameof(IsCompact))]
    [NotifyPropertyChangedFor(nameof(IsMedium))]
    [NotifyPropertyChangedFor(nameof(IsExpanded))]
    [NotifyPropertyChangedFor(nameof(Columns))]
    [NotifyPropertyChangedFor(nameof(WideColumns))]
    [NotifyPropertyChangedFor(nameof(TileColumns))]
    [NotifyPropertyChangedFor(nameof(ChartHeight))]
    [NotifyPropertyChangedFor(nameof(UsesSideNavigation))]
    public partial double Width { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChartHeight))]
    public partial double Height { get; set; }

    /// <summary>
    /// Insets reported by the platform (Android display cutouts and navigation bar, iOS notch).
    /// Zero everywhere else.
    /// </summary>
    [ObservableProperty]
    public partial Thickness SafeAreaPadding { get; set; }

    /// <summary>
    /// True when touch is the primary pointer, so hit targets grow and hover-only affordances
    /// get a tap equivalent.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTouchPrimary { get; set; }

    public LayoutStore()
    {
        // Start wide so the first layout pass on desktop does not flash the compact shell; the
        // shell corrects this as soon as it measures.
        Width = Breakpoints.ExpandedMinWidth;
        Height = 800;
        IsTouchPrimary = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
    }

    public Breakpoint Breakpoint => Breakpoints.FromWidth(Width);

    public bool IsCompact => Breakpoint == Breakpoint.Compact;

    public bool IsMedium => Breakpoint == Breakpoint.Medium;

    public bool IsExpanded => Breakpoint == Breakpoint.Expanded;

    /// <summary>Columns for a grid of medium-sized cards (forms, item cards).</summary>
    public int Columns =>
        Breakpoint switch
        {
            Breakpoint.Compact => 1,
            Breakpoint.Medium => 2,
            _ => 3,
        };

    /// <summary>Columns for wide content such as a dashboard section row.</summary>
    public int WideColumns =>
        Breakpoint switch
        {
            Breakpoint.Compact => 1,
            Breakpoint.Medium => 2,
            _ => 3,
        };

    /// <summary>
    /// Columns for small single-number tiles, <i>within a group</i>. Groups themselves sit several to
    /// a row on a wide screen, so this stays at two until there is genuinely room for more — four
    /// tiles across a third of the window leaves no space for the number.
    /// </summary>
    public int TileColumns => Width >= 1700 ? 3 : 2;

    /// <summary>
    /// A chart tall enough to read but never taller than the viewport, so a phone does not need
    /// two swipes to pass a single chart.
    /// </summary>
    /// <remarks>
    /// The height covers the legend and axis labels as well as the plot, which on a phone is where
    /// most of it goes — hence a compact figure that is not much shorter than a medium one.
    /// </remarks>
    public double ChartHeight
    {
        get
        {
            var ideal = Breakpoint switch
            {
                Breakpoint.Compact => 300.0,
                Breakpoint.Medium => 320.0,
                _ => 400.0,
            };

            return Height > 0 ? Math.Min(ideal, Math.Max(180, Height * 0.62)) : ideal;
        }
    }

    /// <summary>Side rail on wide layouts, bottom bar on compact ones.</summary>
    public bool UsesSideNavigation => Breakpoint != Breakpoint.Compact;

    public void Update(Size size)
    {
        // Height first: a width change can cross a breakpoint, and screens rebuild their figures
        // there, reading ChartHeight — which has to be computed from this size, not the last one.
        if (size.Height > 0)
            Height = size.Height;

        if (size.Width > 0)
            Width = size.Width;
    }
}
