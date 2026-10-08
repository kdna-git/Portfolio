using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using SkiaSharp;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// What every chart in this library has in common: a title, a formatter, an empty state, and the
/// paints that keep legends and tooltips legible in both themes.
/// </summary>
/// <remarks>
/// Charts are view models rather than controls on purpose. A control that reaches into a portfolio
/// store can only ever draw portfolios; a view model that is handed a list of points can be reused,
/// previewed with fake data, and unit tested.
/// </remarks>
public abstract partial class ChartViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    /// <summary>
    /// True when there is nothing to draw. Views show <see cref="EmptyMessage"/> instead of an
    /// empty set of axes, which otherwise reads as "the value is zero".
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; set; }

    [ObservableProperty]
    public partial ValueFormat ValueFormat { get; set; }

    /// <summary>
    /// A legend is present whenever two or more series share the plot, so identity never rests on
    /// colour alone.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveLegendPosition))]
    public partial bool ShowLegend { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveLegendPosition))]
    public partial LegendPosition LegendPosition { get; set; }

    [ObservableProperty]
    public partial TooltipPosition TooltipPosition { get; set; }

    /// <summary>
    /// Where panning and zooming are allowed. Off by default: on a touch screen a draggable chart
    /// steals the gesture the page needs for scrolling. Hosts that want it opt in.
    /// </summary>
    [ObservableProperty]
    public partial ZoomAndPanMode ZoomMode { get; set; }

    /// <summary>
    /// Drawn on a narrow screen. Each chart decides what to give up for the width — rotated labels,
    /// slice labels — so a phone gets a figure laid out for it rather than a desktop one shrunk to fit.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCompact { get; set; }

    partial void OnIsCompactChanged(bool value) => OnCompactChanged();

    /// <summary>Redraws whatever depends on <see cref="IsCompact"/>.</summary>
    protected virtual void OnCompactChanged() { }

    /// <summary>
    /// What the chart control binds to: the chosen position when a legend is warranted, hidden when
    /// a single series makes it redundant (the title already names it).
    /// </summary>
    public LegendPosition EffectiveLegendPosition =>
        ShowLegend ? LegendPosition : LegendPosition.Hidden;

    protected ChartViewModelBase()
    {
        EmptyMessage = "No data for this selection";
        ValueFormat = ValueFormat.Currency;
        LegendPosition = LegendPosition.Bottom;
        TooltipPosition = TooltipPosition.Top;
        ZoomMode = ZoomAndPanMode.None;
        IsEmpty = true;
    }

    #region Theme-aware paints

    public SolidColorPaint LegendTextPaint => new(ChartPalette.Color(ChartPalette.SecondaryInk));

    public SolidColorPaint LegendBackgroundPaint =>
        new(ChartPalette.Fade(ChartPalette.Surface, 0));

    public SolidColorPaint TooltipTextPaint => new(ChartPalette.Color(ChartPalette.PrimaryInk));

    public SolidColorPaint TooltipBackgroundPaint =>
        new(ChartPalette.Color(ChartPalette.Surface))
        {
            // A plain fill would float; the hairline ring ties the tooltip to the surface.
            StrokeThickness = 1,
        };

    public double LegendTextSize => 12;

    public double TooltipTextSize => 12;

    #endregion

    /// <summary>
    /// Re-reads theme-dependent paints. Called when the app's light/dark variant changes, so charts
    /// follow the theme without being rebuilt.
    /// </summary>
    public virtual void OnThemeChanged()
    {
        OnPropertyChanged(nameof(LegendTextPaint));
        OnPropertyChanged(nameof(LegendBackgroundPaint));
        OnPropertyChanged(nameof(TooltipTextPaint));
        OnPropertyChanged(nameof(TooltipBackgroundPaint));
    }

    /// <summary>A recessive paint for gridlines: present, never competing with the data.</summary>
    protected static SolidColorPaint GridPaint() =>
        new(ChartPalette.Color(ChartPalette.Gridline)) { StrokeThickness = 1 };

    protected static SolidColorPaint AxisTextPaint() =>
        new(ChartPalette.Color(ChartPalette.MutedInk)) { SKTypeface = SKTypeface.Default };

    protected static SolidColorPaint AxisLinePaint() =>
        new(ChartPalette.Color(ChartPalette.AxisLine)) { StrokeThickness = 1 };
}
