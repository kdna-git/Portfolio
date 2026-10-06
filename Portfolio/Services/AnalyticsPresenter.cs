using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using Portfolio.Charts.ViewModels;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;

namespace Portfolio.Services;

/// <summary>
/// Turns domain analytics into chart and tile view models.
/// </summary>
/// <remarks>
/// The one place that knows about both halves. The chart library stays ignorant of portfolios and the
/// domain stays ignorant of charts, which is what lets either be reused on its own; this class is the
/// seam, and it is deliberately thin — a method per figure, no state.
/// </remarks>
public static class AnalyticsPresenter
{
    #region Conversions

    public static IReadOnlyList<TimeValue> ToTimeValues(NamedSeries series) =>
        [.. series.Points.Select(p => new TimeValue(p.Date.ToDateTime(TimeOnly.MinValue), (double)p.Value))];

    public static IReadOnlyList<LabeledValue> ToLabeledValues(IReadOnlyList<Slice> slices) =>
        [.. slices.Select(s => new LabeledValue(s.Label, (double)s.Value, s.Key))];

    public static IReadOnlyList<string> BucketLabels(NamedSeries series, Granularity granularity) =>
        [.. series.Points.Select(p => granularity.Label(p.Date))];

    #endregion

    #region Tiles

    /// <summary>What the scope is worth right now and how it is split between cash and holdings.</summary>
    public static MetricTileGroup BalanceTiles(NodeStatistics stats, int columns, SparklineViewModel? trend = null)
    {
        var hero = new MetricTile(
            "Total balance",
            (double)stats.TotalBalance,
            description: "Money paid in plus everything it has earned, after costs and write-offs.",
            emphasis: MetricEmphasis.Hero
        )
        {
            Sparkline = trend,
        };

        var tiles = new List<MetricTile>
        {
            new("Cash", (double)stats.Cash, description: "Balance not committed to a holding."),
            new("Invested", (double)stats.Invested, description: "Capital committed to holdings, at cost."),
            new(
                "Estimated value",
                (double)stats.EstimatedValue,
                description: "Cash plus the latest reported value of the holdings."
            ),
            new(
                "Unrealised gain",
                (double)stats.UnrealisedGain,
                isSigned: true,
                description: "Reported value minus what the holdings cost. Zero when nothing reports a value.",
                hasValue: stats.MarketValue != 0 || stats.Invested != 0
            ),
            new(
                "XIRR",
                stats.Xirr ?? 0,
                ValueFormat.Percent,
                isSigned: true,
                hasValue: stats.Xirr is not null,
                description: "Annual return on the money you put in, weighted by when you put it in."
            ),
            new(
                "Holdings",
                stats.ActiveItemCount,
                ValueFormat.Integer,
                description: $"{stats.ActiveItemCount} of {stats.ItemCount} holdings still carry a balance."
            ),
        };

        return new MetricTileGroup("Balance", hero, tiles) { Columns = columns };
    }

    /// <summary>Where the money came from and how much of the balance you funded yourself.</summary>
    public static MetricTileGroup FundingTiles(NodeStatistics stats, int columns)
    {
        var hero = new MetricTile(
            "Net deposits",
            (double)stats.NetDeposits,
            isSigned: true,
            description: "Money in minus money out over the selected period.",
            emphasis: MetricEmphasis.Hero
        );

        var tiles = new List<MetricTile>
        {
            new("Deposits", (double)stats.Deposits),
            new("Withdrawals", (double)stats.Withdrawals),
            new(
                "Capital freedom",
                (double)stats.CapitalFreedom,
                ValueFormat.Percent,
                isSigned: true,
                description: "Share of the balance that earnings paid for rather than you."
            ),
            new(
                "Yield on deposits",
                (double)stats.YieldOnDeposits,
                ValueFormat.Percent,
                isSigned: true,
                description: "Earnings in this period against every euro you have paid in."
            ),
        };

        return new MetricTileGroup("Funding", hero, tiles) { Columns = columns };
    }

    /// <summary>What the scope earned, and what that cost.</summary>
    public static MetricTileGroup EarningsTiles(NodeStatistics stats, int columns)
    {
        var hero = new MetricTile(
            "Net earnings",
            (double)stats.NetEarnings,
            isSigned: true,
            description: "Income minus costs minus write-offs, over the selected period.",
            emphasis: MetricEmphasis.Hero
        );

        var tiles = new List<MetricTile>
        {
            new("Interest", (double)stats.Interest),
            new("Bonuses", (double)stats.Bonuses),
            new("Taxes", (double)stats.Taxes),
            new("Fees", (double)stats.Fees),
            new(
                "Write-offs",
                (double)stats.Losses,
                description: "Capital lost to defaults. Reduces both the balance and the invested total."
            ),
            new(
                "Cost ratio",
                (double)stats.CostRatio,
                ValueFormat.Percent,
                description: "Taxes and fees as a share of gross income."
            ),
        };

        return new MetricTileGroup("Earnings", hero, tiles) { Columns = columns };
    }

    /// <summary>How the scope has behaved: steadiness, worst case, concentration.</summary>
    public static MetricTileGroup BehaviourTiles(
        NodeStatistics stats,
        decimal concentration,
        decimal topShare,
        int columns
    )
    {
        var tiles = new List<MetricTile>
        {
            new(
                "Max drawdown",
                -(double)stats.MaxDrawdown,
                ValueFormat.Percent,
                isSigned: true,
                description: "Deepest fall from a previous high in the selected period."
            ),
            new(
                "Simple return",
                (double)stats.SimpleReturn,
                ValueFormat.Percent,
                isSigned: true,
                description: "Earnings over the average capital employed. Defined even when XIRR is not."
            ),
            new(
                "Average month",
                (double)stats.AverageMonthlyEarnings,
                isSigned: true,
                description: $"Mean net earnings across {stats.MonthsTracked} months."
            ),
            new(
                "Best month",
                (double)stats.BestMonthEarnings,
                isSigned: true,
                description: stats.BestMonth is null ? null : $"{stats.BestMonth:MMMM yyyy}",
                hasValue: stats.BestMonth is not null
            ),
            new(
                "Worst month",
                (double)stats.WorstMonthEarnings,
                isSigned: true,
                description: stats.WorstMonth is null ? null : $"{stats.WorstMonth:MMMM yyyy}",
                hasValue: stats.WorstMonth is not null
            ),
            new(
                "Positive streak",
                stats.PositiveMonthStreak,
                ValueFormat.Integer,
                description: "Consecutive recent months that ended in the black."
            ),
            new(
                "Concentration",
                (double)concentration,
                ValueFormat.Percent,
                description: "1 means everything sits in one place; 1/n means it is spread evenly over n."
            ),
            new(
                "Largest share",
                (double)topShare,
                ValueFormat.Percent,
                description: "Share of the balance held by the single biggest child."
            ),
        };

        return new MetricTileGroup("Behaviour", null, tiles) { Columns = columns };
    }

    #endregion

    #region Charts

    /// <summary>
    /// What the scope is worth against what you paid in. The gap between the two lines is the whole
    /// story of a portfolio, which is why they belong on one axis rather than two.
    /// </summary>
    public static TimeSeriesChartViewModel ValueAgainstDeposits(
        IPortfolioNode node,
        DateRange range,
        DateOnly today
    )
    {
        var granularity = node.SuggestGranularity(range, today);

        var chart = new TimeSeriesChartViewModel
        {
            Title = "Value and money paid in",
            Subtitle = "The gap between the lines is what the portfolio has earned",
        };

        chart.SetSeries(
            [
                new TimeSeries(
                    "Estimated value",
                    ToTimeValues(node.EstimatedValueSeries(range, granularity, today)),
                    SeriesStyle.Area(ChartPalette.Slot(0))
                ),
                new TimeSeries(
                    "Balance",
                    ToTimeValues(node.BalanceSeries(range, granularity, today)),
                    SeriesStyle.Line(ChartPalette.Slot(2))
                ),
                new TimeSeries(
                    "Paid in",
                    ToTimeValues(node.NetDepositsSeries(range, granularity, today)),
                    SeriesStyle.Reference(ChartPalette.Reference)
                ),
            ]
        );

        return chart;
    }

    /// <summary>Earnings adding up over time: the shape of compounding, or the lack of it.</summary>
    public static TimeSeriesChartViewModel CumulativeEarnings(
        IPortfolioNode node,
        DateRange range,
        DateOnly today
    )
    {
        var granularity = node.SuggestGranularity(range, today);

        var chart = new TimeSeriesChartViewModel { Title = "Cumulative net earnings" };

        chart.SetSeries(
            [
                new TimeSeries(
                    "Net earnings",
                    ToTimeValues(node.CumulativeEarningsSeries(range, granularity, today)),
                    SeriesStyle.Area(ChartPalette.Slot(5))
                ),
            ]
        );

        return chart;
    }

    /// <summary>How far below its own high-water mark the scope has been.</summary>
    public static TimeSeriesChartViewModel Drawdown(
        IPortfolioNode node,
        DateRange range,
        DateOnly today
    )
    {
        var granularity = node.SuggestGranularity(range, today);

        var chart = new TimeSeriesChartViewModel
        {
            Title = "Drawdown",
            Subtitle = "Distance below the previous peak",
            ValueFormat = ValueFormat.Percent,
        };

        chart.SetSeries(
            [
                new TimeSeries(
                    "Drawdown",
                    ToTimeValues(node.DrawdownSeries(range, granularity, today)),
                    SeriesStyle.Area(ChartPalette.Negative)
                ),
            ]
        );

        return chart;
    }

    /// <summary>Money in or out, and earnings, per period. Bars because a month is a span, not a point.</summary>
    public static CategoryChartViewModel PeriodActivity(
        IPortfolioNode node,
        DateRange range,
        DateOnly today
    )
    {
        var granularity = PreferBuckets(node, range, today);
        var flows = node.PeriodNetFlowSeries(range, granularity, today);
        var earnings = node.PeriodEarningsSeries(range, granularity, today);

        var chart = new CategoryChartViewModel
        {
            Title = "Activity per period",
            Subtitle = "Money you moved against what the portfolio generated",
        };

        chart.SetData(
            BucketLabels(flows, granularity),
            [
                new CategorySeries(
                    "Net invested",
                    [.. flows.Points.Select(p => (double)p.Value)],
                    SeriesStyle.Column(ChartPalette.Slot(0))
                ),
                new CategorySeries(
                    "Generated",
                    [.. earnings.Points.Select(p => (double)p.Value)],
                    SeriesStyle.Column(ChartPalette.Slot(5))
                ),
            ]
        );

        return chart;
    }

    /// <summary>Where earnings come from and what eats them, stacked per period.</summary>
    public static CategoryChartViewModel EarningsBreakdown(
        IPortfolioNode node,
        DateRange range,
        DateOnly today
    )
    {
        var granularity = PreferBuckets(node, range, today);
        var parts = node.EarningsBreakdownSeries(range, granularity, today);

        var chart = new CategoryChartViewModel
        {
            Title = "Income and costs",
            Subtitle = "Costs and write-offs are drawn below the line",
            Layout = BarLayout.Stacked,
        };

        // Fixed colours rather than palette slots: these five components mean the same thing in every
        // chart, so they keep the same colour everywhere.
        var styles = new[]
        {
            SeriesStyle.Column(ChartPalette.Slot(5)),
            SeriesStyle.Column(ChartPalette.Slot(2)),
            SeriesStyle.Column(ChartPalette.Slot(3)),
            SeriesStyle.Column(ChartPalette.Slot(1)),
            SeriesStyle.Column(ChartPalette.Negative),
        };

        chart.SetData(
            BucketLabels(parts[0], granularity),
            [
                .. parts.Select(
                    (series, index) =>
                        new CategorySeries(
                            series.Name,
                            [.. series.Points.Select(p => (double)p.Value)],
                            styles[index]
                        )
                ),
            ]
        );

        return chart;
    }

    /// <summary>How a metric splits across the scope's children.</summary>
    public static DistributionChartViewModel Distribution(
        IPortfolioNode node,
        PortfolioMetric metric,
        DateRange range,
        DateOnly today
    )
    {
        var slices = node.DistributionByChild(metric, range, today);

        var chart = new DistributionChartViewModel
        {
            Title = $"{metric.DisplayName()} by {ChildNoun(node)}",
            Subtitle = $"Each {ChildNoun(node)}'s share of the total",
            ValueFormat = metric.Format() == MetricFormat.Percent ? ValueFormat.Percent : ValueFormat.Currency,
        };

        chart.SetData(ToLabeledValues(slices));
        return chart;
    }

    /// <summary>The scope's children ranked by a metric. Horizontal, because the labels are names.</summary>
    public static CategoryChartViewModel ChildComparison(
        IPortfolioNode node,
        PortfolioMetric metric,
        DateRange range,
        DateOnly today
    )
    {
        var slices = node.DistributionByChild(metric, range, today);

        var chart = new CategoryChartViewModel
        {
            // Not the same title as the distribution: the two sit together and answer different
            // questions — what share each part has, and how the parts rank.
            Title = $"{metric.DisplayName()} by {ChildNoun(node)}, ranked",
            Subtitle = "Largest at the top",
            Orientation = BarOrientation.Horizontal,
            ValueFormat = metric.Format() == MetricFormat.Percent ? ValueFormat.Percent : ValueFormat.Currency,
        };

        // Horizontal bars read bottom-up, so reverse to put the largest at the top.
        var ordered = slices.Reverse().ToList();

        chart.SetData(
            [.. ordered.Select(s => s.Label)],
            [
                new CategorySeries(
                    metric.DisplayName(),
                    [.. ordered.Select(s => (double)s.Value)],
                    metric.IsSigned() ? SeriesStyle.SignedColumn() : SeriesStyle.Column(ChartPalette.Slot(0))
                ),
            ]
        );

        return chart;
    }

    /// <summary>Return per child per period: the table that makes a dashboard worth opening.</summary>
    public static DataTableViewModel ReturnMatrix(
        IPortfolioNode node,
        IReadOnlyList<(string Label, DateRange Range)> periods,
        DateOnly today
    )
    {
        var rows = node.ReturnMatrix(periods, today);
        var keys = rows.Where(r => !r.Name.Equals("Total", StringComparison.Ordinal)).Select(r => r.Key).ToList();

        var table = new DataTableViewModel { Title = "Return by period" };

        table.SetData(
            [
                new TableColumn("name", ChildNoun(node, plural: false), IsNumeric: false),
                .. periods.Select(p => new TableColumn(
                    p.Label,
                    p.Label,
                    Format: ValueFormat.Percent,
                    Description: p.Range.ToString()
                )),
            ],
            [
                .. rows.Select(row => new TableRow(
                    row.Key,
                    row.Name,
                    [.. row.Periods.Select(p => new TableCell(p.Xirr, IsSigned: true))],
                    IsPinned: row.Name.Equals("Total", StringComparison.Ordinal),
                    Color: row.Name.Equals("Total", StringComparison.Ordinal)
                        ? null
                        : ChartPalette.SlotForKey(row.Key, keys)
                )),
            ]
        );

        return table;
    }

    /// <summary>A side-by-side table of the scope's children across several metrics.</summary>
    public static DataTableViewModel ChildBreakdown(
        IPortfolioNode node,
        IReadOnlyList<PortfolioMetric> metrics,
        DateRange range,
        DateOnly today
    )
    {
        var children = node.Children;
        var keys = children.Select(c => c.Key).ToList();

        var table = new DataTableViewModel { Title = "Breakdown" };

        table.SetData(
            [
                new TableColumn("name", ChildNoun(node, plural: false), IsNumeric: false),
                .. metrics.Select(m => new TableColumn(
                    m.ToString(),
                    m.DisplayName(),
                    Format: m.Format() == MetricFormat.Percent
                        ? ValueFormat.Percent
                        : m.Format() == MetricFormat.Count
                            ? ValueFormat.Integer
                            : ValueFormat.Currency
                )),
            ],
            [
                .. children.Select(child => new TableRow(
                    child.Key,
                    child.Name,
                    [
                        .. metrics.Select(m => new TableCell(
                            m == PortfolioMetric.Xirr
                                ? child.XirrFor(range, today)
                                : (double)child.ValueOf(m, range, today),
                            IsSigned: m.IsSigned()
                        )),
                    ],
                    Color: ChartPalette.SlotForKey(child.Key, keys),
                    Sublabel: child.Kind == PortfolioNodeKind.FinancialItem ? null : $"{child.Children.Count} holdings"
                )),
            ]
        );

        return table;
    }

    /// <summary>A trend small enough for a tile or a list row.</summary>
    public static SparklineViewModel Sparkline(IPortfolioNode node, DateRange range, DateOnly today)
    {
        var granularity = node.SuggestGranularity(range, today);
        return new SparklineViewModel(ToTimeValues(node.EstimatedValueSeries(range, granularity, today)));
    }

    #endregion

    /// <summary>
    /// Bucket size for bar charts. Bars need fewer, wider buckets than lines: forty columns are
    /// unreadable where forty line points are fine.
    /// </summary>
    private static Granularity PreferBuckets(IPortfolioNode node, DateRange range, DateOnly today)
    {
        var suggested = node.SuggestGranularity(range, today);

        var coarser = suggested switch
        {
            Granularity.Daily => Granularity.Weekly,
            Granularity.Weekly => Granularity.Monthly,
            _ => suggested,
        };

        // Keep stepping up until the bars fit. Around thirty columns is the point past which a bar
        // chart stops being readable and starts being a texture.
        const int maxBars = 30;

        while (coarser != Granularity.Yearly && BucketCount(node, range, coarser, today) > maxBars)
        {
            coarser = coarser switch
            {
                Granularity.Daily => Granularity.Weekly,
                Granularity.Weekly => Granularity.Monthly,
                Granularity.Monthly => Granularity.Quarterly,
                _ => Granularity.Yearly,
            };
        }

        return coarser;
    }

    private static int BucketCount(
        IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly today
    ) => node.PeriodNetFlowSeries(range, granularity, today).Points.Count;

    private static string ChildNoun(IPortfolioNode node, bool plural = true) =>
        node.Kind switch
        {
            PortfolioNodeKind.Portfolio => plural ? "platform" : "Platform",
            PortfolioNodeKind.Platform => plural ? "holding" : "Holding",
            PortfolioNodeKind.Group => plural ? "holding" : "Holding",
            _ => plural ? "entry" : "Entry",
        };
}
