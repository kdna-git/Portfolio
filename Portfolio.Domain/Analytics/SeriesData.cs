namespace Portfolio.Domain.Analytics;

/// <summary>One dated value in a time series.</summary>
public readonly record struct SeriesPoint(DateOnly Date, decimal Value);

/// <summary>One labelled value: a bar, a pie slice, a table cell.</summary>
public readonly record struct Slice(string Label, decimal Value, string? Key = null);

/// <summary>A named time series, ready to hand to a chart.</summary>
public sealed record NamedSeries(string Name, IReadOnlyList<SeriesPoint> Points)
{
    public static NamedSeries Empty(string name) => new(name, []);

    public bool HasData => Points.Count > 0;

    public decimal Total => Points.Sum(p => p.Value);

    public decimal? Last => Points.Count == 0 ? null : Points[^1].Value;
}

/// <summary>A named set of labelled values, for grouped bars and stacked columns.</summary>
public sealed record NamedSlices(string Name, IReadOnlyList<Slice> Slices);

/// <summary>
/// A node's return over a set of periods: the shape behind the per-platform, per-year table.
/// </summary>
public sealed record ReturnRow(
    string Key,
    string Name,
    PortfolioNodeKindTag Kind,
    IReadOnlyList<PeriodReturn> Periods
)
{
    public double? For(string periodLabel) =>
        Periods.FirstOrDefault(p => p.Label == periodLabel)?.Xirr;
}

/// <summary>Return of one node over one period.</summary>
public sealed record PeriodReturn(string Label, DateRange Range, double? Xirr, decimal NetEarnings);

/// <summary>
/// Mirror of <c>PortfolioNodeKind</c> for rows that are no longer attached to a live node (a table
/// row survives a data reload; the node it came from does not).
/// </summary>
public enum PortfolioNodeKindTag
{
    Portfolio,
    Platform,
    FinancialItem,
    Group,
}
