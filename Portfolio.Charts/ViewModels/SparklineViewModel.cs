using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using SkiaSharp;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// A chart small enough to sit inside a table row: shape only, no axes, no legend, no tooltip.
/// </summary>
/// <remarks>
/// The point of a sparkline is the trend next to the number, so it is coloured by direction rather
/// than by palette slot, and it deliberately drops every piece of chrome a full chart needs.
/// </remarks>
public sealed partial class SparklineViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ISeries[] Series { get; set; }

    [ObservableProperty]
    public partial Axis[] XAxes { get; set; }

    [ObservableProperty]
    public partial Axis[] YAxes { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Change from the first to the last value, as a fraction. Null when undefined.</summary>
    [ObservableProperty]
    public partial double? Change { get; set; }

    public SparklineViewModel()
    {
        Series = [];
        XAxes = [Hidden()];
        YAxes = [Hidden()];
        IsEmpty = true;
    }

    public SparklineViewModel(IReadOnlyList<TimeValue> values)
        : this() => SetValues(values);

    public void SetValues(IReadOnlyList<TimeValue> values)
    {
        IsEmpty = values.Count < 2;

        if (IsEmpty)
        {
            Series = [];
            Change = null;
            return;
        }

        var first = values[0].Value;
        var last = values[^1].Value;
        Change = first == 0 ? null : (last - first) / Math.Abs(first);

        var color = ChartPalette.ForSign(last - first);

        Series =
        [
            new LineSeries<DateTimePoint>
            {
                Values = [.. values.Select(v => new DateTimePoint(v.Date, v.Value))],
                Stroke = new SolidColorPaint(ChartPalette.Color(color), 1.5f),
                Fill = new LinearGradientPaint(
                    [ChartPalette.Fade(color, 0.28), ChartPalette.Fade(color, 0.0)],
                    new SKPoint(0, 0),
                    new SKPoint(0, 1)
                ),
                GeometrySize = 0,
                LineSmoothness = 0.4,
                IsHoverable = false,
            },
        ];
    }

    private static Axis Hidden() =>
        new()
        {
            IsVisible = false,
            ShowSeparatorLines = false,
        };
}
