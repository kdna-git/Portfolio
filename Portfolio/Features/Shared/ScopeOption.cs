using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;

namespace Portfolio.Features.Shared;

/// <summary>
/// One analysable scope offered in the scope picker.
/// </summary>
/// <remarks>
/// The whole point of the rewrite: because statistics work on any <see cref="IPortfolioNode"/>, the
/// picker can offer the portfolio, each platform, each holding and each investment-type grouping, and
/// every screen downstream works unchanged.
/// </remarks>
public sealed record ScopeOption(string Key, string Label, string Group, IPortfolioNode Node)
{
    /// <summary>Indent for holdings listed under their platform.</summary>
    public bool IsChild => Node.Kind == PortfolioNodeKind.FinancialItem;

    public string DisplayLabel => IsChild ? "    " + Label : Label;

    public override string ToString() => DisplayLabel;
}

public static class PortfolioScopes
{
    public const string PortfolioGroup = "Portfolio";
    public const string PlatformGroup = "Platforms";
    public const string ItemGroup = "Holdings";
    public const string TypeGroup = "Investment types";

    /// <summary>
    /// Every scope worth offering, in reading order: the whole portfolio, then each platform with its
    /// holdings beneath it, then the investment-type groupings.
    /// </summary>
    public static IReadOnlyList<ScopeOption> Build(PortfolioRoot root)
    {
        var options = new List<ScopeOption>
        {
            new(root.Key, root.Name, PortfolioGroup, root),
        };

        foreach (var platform in root.Platforms.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            options.Add(new ScopeOption(platform.Key, platform.Name, PlatformGroup, platform));

            foreach (var item in platform.FinancialItems.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                options.Add(new ScopeOption(item.Key, item.Name, ItemGroup, item));
        }

        foreach (var group in root.FinancialItems.GroupBy(i => i.Type).OrderBy(g => g.Key))
        {
            var node = new GroupNode($"type:{group.Key}", group.Key.ToString(), group);
            options.Add(new ScopeOption(node.Key, $"{group.Key} holdings", TypeGroup, node));
        }

        return options;
    }

    /// <summary>
    /// Finds the option matching a key, falling back to the portfolio. Used to keep the selection
    /// after a data reload rebuilt every node.
    /// </summary>
    public static ScopeOption Resolve(IReadOnlyList<ScopeOption> options, string? key) =>
        options.FirstOrDefault(o => o.Key == key) ?? options[0];
}

/// <summary>One period offered in the period picker.</summary>
public sealed record PeriodOption(string Label, DateRange Range)
{
    public override string ToString() => Label;
}

public static class PortfolioPeriods
{
    /// <summary>
    /// The periods people actually ask for: everything, this year so far, the trailing year and
    /// three years, then the last few complete calendar years.
    /// </summary>
    public static IReadOnlyList<PeriodOption> Build(DateOnly? today = null, int pastYears = 4)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);

        var options = new List<PeriodOption>
        {
            new("All time", DateRange.All),
            new("This year", DateRange.YearToDate(now)),
            new("12 months", DateRange.LastMonths(12, now)),
            new("3 years", DateRange.LastYears(3, now)),
        };

        for (var offset = 1; offset <= pastYears; offset++)
        {
            var year = now.Year - offset;
            options.Add(new PeriodOption(year.ToString(), DateRange.Year(year)));
        }

        return options;
    }

    public static PeriodOption Resolve(IReadOnlyList<PeriodOption> options, string? label) =>
        options.FirstOrDefault(o => o.Label == label) ?? options[0];
}
