using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// Bars against a label axis: money in and out per month, earnings split by component, balance per
/// platform. Vertical for time buckets, horizontal when the labels are names.
/// </summary>
/// <remarks>
/// Time buckets are categories, not instants — a month-long bar has no single date — so this chart
/// takes labels rather than dates. That also means a monthly, quarterly or yearly view needs no
/// different chart, only different labels.
/// </remarks>
public sealed partial class CategoryChartViewModel : ChartViewModelBase
{
    [ObservableProperty]
    public partial ISeries[] Series { get; set; }

    [ObservableProperty]
    public partial Axis[] XAxes { get; set; }

    [ObservableProperty]
    public partial Axis[] YAxes { get; set; }

    [ObservableProperty]
    public partial BarLayout Layout { get; set; }

    [ObservableProperty]
    public partial BarOrientation Orientation { get; set; }

    private IReadOnlyList<string> _labels = [];
    private IReadOnlyList<CategorySeries> _data = [];

    public CategoryChartViewModel()
    {
        Series = [];
        XAxes = [new Axis()];
        YAxes = [new Axis()];
        Layout = BarLayout.Grouped;
        Orientation = BarOrientation.Vertical;
    }

    public void SetData(IReadOnlyList<string> labels, IReadOnlyList<CategorySeries> series)
    {
        _labels = labels;
        _data = series;
        Rebuild();
    }

    /// <summary>Convenience for a single series of labelled values.</summary>
    public void SetData(IReadOnlyList<LabeledValue> values, SeriesStyle? style = null, string name = "")
    {
        _labels = [.. values.Select(v => v.Label)];
        _data = [new CategorySeries(name, [.. values.Select(v => v.Value)], style)];
        Rebuild();
    }

    partial void OnLayoutChanged(BarLayout value) => Rebuild();

    partial void OnOrientationChanged(BarOrientation value) => Rebuild();

    protected override void OnCompactChanged() => Rebuild();

    public override void OnThemeChanged()
    {
        base.OnThemeChanged();
        Rebuild();
    }

    private void Rebuild()
    {
        var withData = _data.Where(s => s.HasData).ToList();
        IsEmpty = withData.Count == 0 || _labels.Count == 0;
        ShowLegend = withData.Count > 1;

        if (IsEmpty)
        {
            Series = [];
            XAxes = [BuildLabelAxis()];
            YAxes = [BuildValueAxis()];
            return;
        }

        var built = new List<ISeries>(withData.Count);
        var slot = 0;

        foreach (var series in withData)
        {
            var style = series.Style ?? SeriesStyle.Column();
            var color = style.Color ?? ChartPalette.Slot(slot);

            if (style.Color is null)
                slot++;

            built.Add(BuildSeries(series, style, color));
        }

        Series = [.. built];

        // A horizontal chart swaps the roles of the two axes: labels move to Y, values to X.
        if (Orientation == BarOrientation.Horizontal)
        {
            XAxes = [BuildValueAxis()];
            YAxes = [BuildLabelAxis()];
        }
        else
        {
            XAxes = [BuildLabelAxis()];
            YAxes = [BuildValueAxis()];
        }
    }

    private ISeries BuildSeries(CategorySeries series, SeriesStyle style, string color)
    {
        var values = series.Values.ToList();
        var fill = new SolidColorPaint(ChartPalette.Color(color));

        // 4px rounded data-ends, anchored to the baseline, and a 2px gap between neighbours.
        const double cornerRadius = 4;
        const double padding = 2;

        if (Orientation == BarOrientation.Horizontal)
        {
            var row = new RowSeries<double>
            {
                Name = series.Name,
                Values = values,
                Fill = fill,
                Padding = padding,
                Rx = cornerRadius,
                Ry = cornerRadius,
                XToolTipLabelFormatter = point =>
                    $"{series.Name}: {ValueFormatter.Format(point.Coordinate.PrimaryValue, ValueFormat)}",
            };

            if (style.ColorBySign)
                ColorBySign(row);

            return row;
        }

        if (Layout == BarLayout.Stacked)
        {
            return new StackedColumnSeries<double>
            {
                Name = series.Name,
                Values = values,
                Fill = fill,
                Padding = padding,
                Rx = cornerRadius,
                Ry = cornerRadius,
                YToolTipLabelFormatter = point =>
                    $"{series.Name}: {ValueFormatter.Format(point.Coordinate.PrimaryValue, ValueFormat)}",
            };
        }

        var column = new ColumnSeries<double>
        {
            Name = series.Name,
            Values = values,
            Fill = fill,
            Padding = padding,
            Rx = cornerRadius,
            Ry = cornerRadius,
            YToolTipLabelFormatter = point =>
                $"{series.Name}: {ValueFormatter.Format(point.Coordinate.PrimaryValue, ValueFormat)}",
        };

        if (style.ColorBySign)
            ColorBySign(column);

        return column;
    }

    /// <summary>
    /// Paints each bar by the sign of its value. Direction is the message for a net-flow chart, so
    /// it gets the colour; the series name carries identity in the tooltip.
    /// </summary>
    private static void ColorBySign<TSeries>(TSeries series)
        where TSeries : ISeries
    {
        if (series is ColumnSeries<double> column)
        {
            column.PointMeasured += point =>
            {
                if (point.Visual is not null)
                    point.Visual.Fill = new SolidColorPaint(
                        ChartPalette.Color(ChartPalette.ForSign(point.Coordinate.PrimaryValue))
                    );
            };
        }
        else if (series is RowSeries<double> row)
        {
            row.PointMeasured += point =>
            {
                if (point.Visual is not null)
                    point.Visual.Fill = new SolidColorPaint(
                        ChartPalette.Color(ChartPalette.ForSign(point.Coordinate.SecondaryValue))
                    );
            };
        }
    }

    private Axis BuildLabelAxis()
    {
        // Names down the side of a horizontal chart each get their own row; only labels along the
        // bottom compete for width.
        var alongBottom = Orientation == BarOrientation.Vertical;

        // Many buckets would collide head-on. A wide figure rotates them, which is better than
        // dropping labels; a compact one cannot spare the height a rotated row takes from the plot,
        // so there the axis skips some instead.
        var thin = alongBottom && IsCompact && _labels.Count > 6;

        return new()
        {
            Labels = [.. _labels],
            LabelsPaint = AxisTextPaint(),
            TextSize = 11,
            LabelsRotation = alongBottom && !IsCompact && _labels.Count > 14 ? 45 : 0,
            SeparatorsPaint = null,
            TicksPaint = AxisLinePaint(),
            MinStep = 1,
            ForceStepToMin = !thin && _labels.Count <= 24,
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
