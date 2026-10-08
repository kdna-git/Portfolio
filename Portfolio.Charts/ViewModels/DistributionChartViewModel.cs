using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// A donut showing how a total splits across a handful of things, with the total in the middle.
/// </summary>
/// <remarks>
/// Two deliberate limits. Slices past the eighth fold into a single "Other" slice rather than taking
/// a ninth colour, because a reader cannot hold more than a handful of hues apart. And negative
/// values are not drawn at all — a slice cannot be negative without lying about the proportions — so
/// they are reported in <see cref="ExcludedNote"/> and listed in <see cref="Rows"/> instead.
/// </remarks>
public sealed partial class DistributionChartViewModel : ChartViewModelBase
{
    [ObservableProperty]
    public partial ISeries[] Series { get; set; }

    /// <summary>
    /// The same numbers as a list: the accessible path out of a colour-coded chart, and the only
    /// honest home for negative or zero entries.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<DistributionRow> Rows { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; set; }

    [ObservableProperty]
    public partial string? ExcludedNote { get; set; }

    /// <summary>Largest share, as a fraction. A one-glance read on concentration.</summary>
    [ObservableProperty]
    public partial double TopShare { get; set; }

    private IReadOnlyList<LabeledValue> _data = [];

    public DistributionChartViewModel()
    {
        Series = [];
        Rows = [];
        TotalText = string.Empty;
    }

    public void SetData(IReadOnlyList<LabeledValue> values)
    {
        _data = values;
        Rebuild();
    }

    public override void OnThemeChanged()
    {
        base.OnThemeChanged();
        Rebuild();
    }

    protected override void OnCompactChanged() => Rebuild();

    /// <summary>
    /// The height to give this chart so the donut itself gets <paramref name="donutHeight"/> and the
    /// list still fits under it.
    /// </summary>
    /// <remarks>
    /// The list has a row per entry, so a fixed height hands the donut whatever the list leaves over:
    /// the more platforms, the smaller the donut, until past a dozen rows the list spills out of its
    /// card. Rows are over- rather than under-estimated, since spare height goes to the donut.
    /// </remarks>
    public double HeightFor(double donutHeight)
    {
        const double listMargin = 8;
        const double rowHeight = 22;
        const double noteHeight = 36;

        return donutHeight
            + listMargin
            + (Rows.Count * rowHeight)
            + (ExcludedNote is null ? 0 : noteHeight);
    }

    private void Rebuild()
    {
        var ordered = _data.OrderByDescending(v => v.Value).ToList();
        var drawable = ordered.Where(v => v.Value > 0).ToList();
        var excluded = ordered.Count - drawable.Count;

        IsEmpty = drawable.Count == 0;
        ShowLegend = drawable.Count > 1;
        ExcludedNote =
            excluded == 0
                ? null
                : excluded == 1
                    ? "1 entry is zero or negative and is listed below only"
                    : $"{excluded} entries are zero or negative and are listed below only";

        if (IsEmpty)
        {
            Series = [];
            Rows = [.. ordered.Select(v => new DistributionRow(v.Label, v.Value, 0, ChartPalette.Neutral))];
            TotalText = ValueFormatter.Format(ordered.Sum(v => v.Value), ValueFormat);
            TopShare = 0;
            return;
        }

        // Fold the tail so the chart never needs a ninth colour.
        var slices = new List<LabeledValue>();

        if (drawable.Count > ChartPalette.MaxCategoricalSlots)
        {
            slices.AddRange(drawable.Take(ChartPalette.MaxCategoricalSlots - 1));
            slices.Add(
                new LabeledValue(
                    ChartPalette.OtherLabel,
                    drawable.Skip(ChartPalette.MaxCategoricalSlots - 1).Sum(v => v.Value),
                    "other"
                )
            );
        }
        else
        {
            slices.AddRange(drawable);
        }

        var total = slices.Sum(s => s.Value);
        var built = new List<ISeries>(slices.Count);

        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var color = slice.Key == "other" ? ChartPalette.Neutral : ChartPalette.Slot(i);
            var share = total == 0 ? 0 : slice.Value / total;

            built.Add(
                new PieSeries<ObservableValue>
                {
                    Name = slice.Label,
                    Values = [new ObservableValue(slice.Value)],
                    Fill = new SolidColorPaint(ChartPalette.Color(color)),
                    InnerRadius = 72,
                    // A 2px surface gap keeps adjacent slices from bleeding into one another.
                    Pushout = 0,
                    OuterRadiusOffset = 0,
                    Stroke = new SolidColorPaint(ChartPalette.Color(ChartPalette.Surface), 2),
                    // Label the slices that have room; the rest are covered by the legend and list.
                    // Not at all on a compact figure: labels outside the ring take width from the
                    // ring itself, and on a phone the list below already says the same thing.
                    DataLabelsSize = !IsCompact && share >= 0.08 ? 11 : 0,
                    DataLabelsPosition = PolarLabelsPosition.Outer,
                    DataLabelsPaint = new SolidColorPaint(
                        ChartPalette.Color(ChartPalette.SecondaryInk)
                    ),
                    DataLabelsFormatter = _ => $"{slice.Label} {share:P0}",
                    ToolTipLabelFormatter = point =>
                        $"{slice.Label}: {ValueFormatter.Format(point.Coordinate.PrimaryValue, ValueFormat)} ({share:P1})",
                }
            );
        }

        Series = [.. built];
        TotalText = ValueFormatter.Format(total, ValueFormat);
        TopShare = total == 0 ? 0 : slices.Max(s => s.Value) / total;

        Rows =
        [
            .. ordered.Select(v => new DistributionRow(
                v.Label,
                v.Value,
                total == 0 ? 0 : v.Value / total,
                ColorFor(v, slices)
            )),
        ];
    }

    private static string ColorFor(LabeledValue value, List<LabeledValue> slices)
    {
        var index = slices.FindIndex(s => s.Label == value.Label);

        return index < 0 ? ChartPalette.Neutral : ChartPalette.Slot(index);
    }
}

/// <summary>One row of the list that accompanies a donut.</summary>
public sealed record DistributionRow(string Label, double Value, double Share, string Color)
{
    public string ValueText => ValueFormatter.Format(Value, ValueFormat.Currency);

    public string ShareText => Share.ToString("P1");
}
