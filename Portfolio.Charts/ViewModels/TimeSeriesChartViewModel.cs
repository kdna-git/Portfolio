using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using SkiaSharp;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// Lines and areas over a date axis: balance against deposits, cumulative earnings, drawdown.
/// </summary>
/// <example>
/// <code>
/// var chart = new TimeSeriesChartViewModel { Title = "Balance" };
/// chart.SetSeries(
/// [
///     new TimeSeries("Value", points, SeriesStyle.Area()),
///     new TimeSeries("Paid in", deposits, SeriesStyle.Reference()),
/// ]);
/// </code>
/// </example>
public sealed partial class TimeSeriesChartViewModel : ChartViewModelBase
{
    [ObservableProperty]
    public partial ISeries[] Series { get; set; }

    [ObservableProperty]
    public partial Axis[] XAxes { get; set; }

    [ObservableProperty]
    public partial Axis[] YAxes { get; set; }

    /// <summary>Draw a line at zero. Worth it for anything that can go negative.</summary>
    [ObservableProperty]
    public partial bool ShowZeroLine { get; set; }

    private IReadOnlyList<TimeSeries> _data = [];

    public TimeSeriesChartViewModel()
    {
        Series = [];
        XAxes = [BuildDateAxis(null, null)];
        YAxes = [BuildValueAxis()];
    }

    public void SetSeries(IReadOnlyList<TimeSeries> series)
    {
        _data = series;
        Rebuild();
    }

    /// <summary>Replaces a single series, keeping the others. Used for live filter changes.</summary>
    public void SetSeries(params TimeSeries[] series) => SetSeries((IReadOnlyList<TimeSeries>)series);

    public override void OnThemeChanged()
    {
        base.OnThemeChanged();
        Rebuild();
    }

    private void Rebuild()
    {
        var withData = _data.Where(s => s.HasData).ToList();
        IsEmpty = withData.Count == 0;
        ShowLegend = withData.Count > 1;

        if (IsEmpty)
        {
            Series = [];
            XAxes = [BuildDateAxis(null, null)];
            YAxes = [BuildValueAxis()];
            return;
        }

        var built = new List<ISeries>(withData.Count);
        var slot = 0;

        foreach (var series in withData)
        {
            var style = series.Style ?? SeriesStyle.Line();
            var color = style.Color ?? ChartPalette.Slot(slot);

            // Only auto-assigned colours consume a palette slot, so a reference line painted in ink
            // does not shift the next series' colour.
            if (style.Color is null)
                slot++;

            built.Add(BuildSeries(series, style, color));
        }

        var from = withData.Min(s => s.Values.Min(v => v.Date));
        var to = withData.Max(s => s.Values.Max(v => v.Date));

        Series = [.. built];
        XAxes = [BuildDateAxis(from, to)];
        YAxes = [BuildValueAxis()];
    }

    private ISeries BuildSeries(TimeSeries series, SeriesStyle style, string color)
    {
        var points = series.Values.Select(v => new DateTimePoint(v.Date, v.Value)).ToList();
        var stroke = new SolidColorPaint(ChartPalette.Color(color), (float)style.StrokeThickness);

        if (style.Dashed)
            stroke.PathEffect = new DashEffect([6, 4]);

        if (style.Shape == SeriesShape.Scatter)
        {
            return new ScatterSeries<DateTimePoint>
            {
                Name = series.Name,
                Values = points,
                Fill = new SolidColorPaint(ChartPalette.Color(color)),
                GeometrySize = 8,
            };
        }

        var line = new LineSeries<DateTimePoint>
        {
            Name = series.Name,
            Values = points,
            Stroke = stroke,
            LineSmoothness = style.Shape == SeriesShape.StepLine ? 0 : 0.3,
            // A dot per point turns a three-year daily series into noise; only short series or
            // explicitly marked ones get geometry.
            GeometrySize = style.ShowPoints ? 8 : 0,
            GeometryFill = style.ShowPoints
                ? new SolidColorPaint(ChartPalette.Color(color))
                : null,
            GeometryStroke = style.ShowPoints
                ? new SolidColorPaint(ChartPalette.Color(ChartPalette.Surface), 2)
                : null,
            Fill = style.Filled
                ? new LinearGradientPaint(
                    [ChartPalette.Fade(color, 0.35), ChartPalette.Fade(color, 0.02)],
                    new SKPoint(0, 0),
                    new SKPoint(0, 1)
                )
                : null,
            XToolTipLabelFormatter = point =>
                point.Model is null
                    ? string.Empty
                    : point.Model.DateTime.ToString("d"),
            YToolTipLabelFormatter = point =>
                $"{series.Name}: {ValueFormatter.Format(point.Coordinate.PrimaryValue, ValueFormat)}",
        };

        return line;
    }

    private Axis BuildDateAxis(DateTime? from, DateTime? to)
    {
        var span = from is not null && to is not null ? to.Value - from.Value : TimeSpan.FromDays(365);

        // Tick granularity follows the span, otherwise a five-year chart prints a label per day and
        // a two-week chart prints none.
        var (unit, format) = span.TotalDays switch
        {
            <= 31 => (TimeSpan.FromDays(1), "d MMM"),
            <= 180 => (TimeSpan.FromDays(7), "d MMM"),
            <= 1_000 => (TimeSpan.FromDays(30), "MMM yy"),
            <= 3_000 => (TimeSpan.FromDays(91), "MMM yy"),
            _ => (TimeSpan.FromDays(365), "yyyy"),
        };

        return new DateTimeAxis(unit, date => date.ToString(format))
        {
            LabelsPaint = AxisTextPaint(),
            TextSize = 11,
            SeparatorsPaint = GridPaint(),
            TicksPaint = AxisLinePaint(),
            ShowSeparatorLines = false,
        };
    }

    private Axis BuildValueAxis() =>
        new()
        {
            Labeler = value =>
                ValueFormatter.Format(
                    value,
                    ValueFormat == ValueFormat.Currency ? ValueFormat.CompactCurrency : ValueFormat
                ),
            LabelsPaint = AxisTextPaint(),
            TextSize = 11,
            SeparatorsPaint = GridPaint(),
            TicksPaint = null,
        };
}
