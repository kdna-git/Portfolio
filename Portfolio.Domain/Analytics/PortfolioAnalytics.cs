using Portfolio.Domain.Models;

namespace Portfolio.Domain.Analytics;

/// <summary>
/// Statistics and series for any <see cref="IPortfolioNode"/>.
/// </summary>
/// <remarks>
/// Everything here takes a node and a <see cref="DateRange"/>, which is what makes per-platform and
/// per-holding analysis free: the dashboard, a platform page and a single loan all call the same
/// methods with a different node.
/// </remarks>
public static class PortfolioAnalytics
{
    #region Point-in-time values

    /// <summary>Book balance on a date: deposits plus earnings, minus withdrawals and costs.</summary>
    public static decimal BalanceAt(this IPortfolioNode node, DateOnly date) =>
        node.Entries.UpTo(date).TotalBalance();

    /// <summary>Capital at cost committed on a date.</summary>
    public static decimal InvestedAt(this IPortfolioNode node, DateOnly date) =>
        node.Entries.UpTo(date).InvestedBook();

    /// <summary>Uncommitted balance on a date.</summary>
    public static decimal CashAt(this IPortfolioNode node, DateOnly date) =>
        node.BalanceAt(date) - node.InvestedAt(date);

    /// <summary>
    /// Cash plus reported market value on a date. The figure a return calculation should use,
    /// because it reflects what the holdings are actually worth rather than what they cost.
    /// </summary>
    public static decimal ValueAt(this IPortfolioNode node, DateOnly date) =>
        node.CashAt(date) + node.MarketValueAt(date);

    #endregion

    #region Returns

    /// <summary>
    /// Money-weighted return of a node over a range.
    /// </summary>
    /// <remarks>
    /// The flows are the ones that cross the portfolio boundary — deposits out of your pocket and
    /// withdrawals back into it — bracketed by the opening value (as an outflow) and the closing
    /// value (as an inflow). Interest and fees are deliberately absent: they are already inside the
    /// closing value, and counting them twice is what makes a naive XIRR come out wildly wrong.
    /// </remarks>
    public static double? XirrFor(this IPortfolioNode node, DateRange range, DateOnly? today = null)
    {
        var entries = node.Entries;
        if (entries.Count == 0)
            return null;

        var asOf = range.EffectiveTo(today ?? DateOnly.FromDateTime(DateTime.Today));
        var start = range.From;

        var flows = new List<CashFlow>();

        if (start is not null)
        {
            var opening = node.ValueAt(start.Value.AddDays(-1));
            if (opening != 0)
                flows.Add(new CashFlow(-opening, start.Value));
        }

        foreach (var entry in entries.Within(start, asOf))
        {
            if (entry.ExternalCashFlow != 0)
                flows.Add(new CashFlow(entry.ExternalCashFlow, entry.Date));
        }

        var closing = node.ValueAt(asOf);
        if (closing != 0)
            flows.Add(new CashFlow(closing, asOf));

        return Xirr.Calculate(flows);
    }

    /// <summary>
    /// Simple return: net earnings over the average capital employed. A sanity check next to XIRR,
    /// and defined in cases where XIRR is not.
    /// </summary>
    public static decimal SimpleReturn(this IPortfolioNode node, DateRange range, DateOnly asOf)
    {
        var entries = node.Entries;
        if (entries.Count == 0)
            return 0;

        var from = range.From ?? entries[0].Date;
        var opening = node.ValueAt(from.AddDays(-1));
        var closing = node.ValueAt(asOf);
        var average = (opening + closing) / 2;

        if (average == 0)
            return 0;

        return entries.Within(range.From, asOf).NetEarnings() / Math.Abs(average);
    }

    #endregion

    #region Statistics snapshot

    /// <summary>
    /// Every headline figure for a node over a range, in one pass.
    /// </summary>
    /// <param name="parentBalance">
    /// Balance of the enclosing scope, used for <see cref="NodeStatistics.ShareOfParent"/>. Pass
    /// null for a root.
    /// </param>
    public static NodeStatistics Describe(
        this IPortfolioNode node,
        DateRange range,
        DateOnly? today = null,
        decimal? parentBalance = null
    )
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        var asOf = range.EffectiveTo(now);
        var all = node.Entries;
        var inRange = all.Within(range.From, asOf).ToList();
        var upToAsOf = all.UpTo(asOf).ToList();

        var totalBalance = upToAsOf.TotalBalance();
        var invested = upToAsOf.InvestedBook();
        var cash = totalBalance - invested;
        var marketValue = node.MarketValueAt(asOf);
        var lifetimeNetDeposits = upToAsOf.NetDeposits();
        var grossIncome = inRange.GrossIncome();
        var totalCosts = inRange.TotalCosts();
        var losses = inRange.TotalLosses();
        var netEarnings = inRange.NetEarnings();

        var monthly = node.MonthlyEarnings(range, asOf);
        SeriesPoint? best = monthly.Count == 0 ? null : monthly.MaxBy(p => p.Value);
        SeriesPoint? worst = monthly.Count == 0 ? null : monthly.MinBy(p => p.Value);

        var leaves = node.Leaves();

        return new NodeStatistics
        {
            Key = node.Key,
            Name = node.Name,
            Kind = node.Kind,
            Range = range,
            AsOf = asOf,

            TotalBalance = totalBalance,
            Cash = cash,
            Invested = invested,
            MarketValue = marketValue,
            EstimatedValue = cash + marketValue,
            UnrealisedGain = node.HasValuations() ? marketValue - invested : 0,

            Deposits = inRange.TotalDeposits(),
            Withdrawals = inRange.TotalWithdrawals(),
            NetDeposits = inRange.NetDeposits(),
            LifetimeNetDeposits = lifetimeNetDeposits,

            Interest = inRange.TotalInterest(),
            Bonuses = inRange.TotalBonuses(),
            GrossIncome = grossIncome,
            Taxes = inRange.TotalTaxes(),
            Fees = inRange.TotalFees(),
            TotalCosts = totalCosts,
            Losses = losses,
            NetEarnings = netEarnings,

            Xirr = node.XirrFor(range, now),
            CapitalFreedom = EntryMath.CapitalFreedom(totalBalance, lifetimeNetDeposits),
            CostRatio = EntryMath.CostRatio(grossIncome, totalCosts),
            SimpleReturn = node.SimpleReturn(range, asOf),
            YieldOnDeposits =
                lifetimeNetDeposits == 0 ? 0 : netEarnings / Math.Abs(lifetimeNetDeposits),
            ShareOfParent =
                parentBalance is null or 0 ? 1 : totalBalance / parentBalance.Value,

            EntryCount = inRange.Count,
            ItemCount = leaves.Count,
            ActiveItemCount = leaves.Count(i => i.BalanceAt(asOf) != 0),
            FirstActivity = all.Count == 0 ? null : all[0].Date,
            LastActivity = all.Count == 0 ? null : all[^1].Date,
            MonthsTracked = monthly.Count,
            AverageMonthlyEarnings =
                monthly.Count == 0 ? 0 : monthly.Sum(p => p.Value) / monthly.Count,
            BestMonthEarnings = best?.Value ?? 0,
            BestMonth = best?.Date,
            WorstMonthEarnings = worst?.Value ?? 0,
            WorstMonth = worst?.Date,
            MaxDrawdown = node.MaxDrawdown(range, asOf),
            PositiveMonthStreak = PositiveStreak(monthly),
        };
    }

    private static int PositiveStreak(IReadOnlyList<SeriesPoint> monthly)
    {
        var streak = 0;

        for (var i = monthly.Count - 1; i >= 0; i--)
        {
            if (monthly[i].Value <= 0)
                break;

            streak++;
        }

        return streak;
    }

    #endregion

    #region Time series

    /// <summary>Book balance at the end of each bucket.</summary>
    public static NamedSeries BalanceSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Balance"
    ) => node.RunningSeries(range, granularity, today, name, e => e.BalanceChange);

    /// <summary>Money paid in, net of withdrawals, at the end of each bucket.</summary>
    public static NamedSeries NetDepositsSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Net deposits"
    ) => node.RunningSeries(range, granularity, today, name, e => e.Deposit - e.Withdrawal);

    /// <summary>Cumulative net earnings at the end of each bucket.</summary>
    public static NamedSeries CumulativeEarningsSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Cumulative earnings"
    ) => node.RunningSeries(range, granularity, today, name, e => e.NetEarnings);

    /// <summary>
    /// Cash plus market value at the end of each bucket: the line to compare against net deposits.
    /// </summary>
    public static NamedSeries EstimatedValueSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Estimated value"
    )
    {
        if (!node.TryGetSpan(range, today, out var from, out var to))
            return NamedSeries.Empty(name);

        var points = new List<SeriesPoint>();

        foreach (var bucket in granularity.Buckets(from, to))
        {
            var end = Min(granularity.BucketEnd(bucket), to);
            points.Add(new SeriesPoint(end, node.ValueAt(end)));
        }

        return new NamedSeries(name, points);
    }

    /// <summary>Net money in or out during each bucket.</summary>
    public static NamedSeries PeriodNetFlowSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Net invested"
    ) => node.BucketSeries(range, granularity, today, name, e => e.Deposit - e.Withdrawal);

    /// <summary>Net earnings during each bucket.</summary>
    public static NamedSeries PeriodEarningsSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Generated"
    ) => node.BucketSeries(range, granularity, today, name, e => e.NetEarnings);

    /// <summary>
    /// Income and cost components per bucket, ready for a stacked column chart: the shape of where
    /// earnings actually come from and what eats them.
    /// </summary>
    public static IReadOnlyList<NamedSeries> EarningsBreakdownSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null
    ) =>
        [
            node.BucketSeries(range, granularity, today, "Interest", e => e.Interest),
            node.BucketSeries(range, granularity, today, "Bonuses", e => e.Bonus),
            node.BucketSeries(range, granularity, today, "Taxes", e => -e.Tax),
            node.BucketSeries(range, granularity, today, "Fees", e => -e.Fee),
            node.BucketSeries(range, granularity, today, "Losses", e => -e.DefaultLoss),
        ];

    /// <summary>Net earnings per calendar month. The input for best/worst month and streaks.</summary>
    public static IReadOnlyList<SeriesPoint> MonthlyEarnings(
        this IPortfolioNode node,
        DateRange range,
        DateOnly asOf
    ) => node.BucketSeries(range, Granularity.Monthly, asOf, "Monthly", e => e.NetEarnings).Points;

    /// <summary>
    /// Fall from the running peak of the estimated value, as a negative fraction. Zero while at a
    /// new high.
    /// </summary>
    public static NamedSeries DrawdownSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today = null,
        string name = "Drawdown"
    )
    {
        var values = node.EstimatedValueSeries(range, granularity, today);
        if (!values.HasData)
            return NamedSeries.Empty(name);

        var points = new List<SeriesPoint>(values.Points.Count);
        decimal peak = 0;

        foreach (var point in values.Points)
        {
            if (point.Value > peak)
                peak = point.Value;

            var drawdown = peak <= 0 ? 0 : (point.Value - peak) / peak;
            points.Add(new SeriesPoint(point.Date, drawdown));
        }

        return new NamedSeries(name, points);
    }

    /// <summary>Deepest drawdown within the range, as a positive fraction.</summary>
    public static decimal MaxDrawdown(this IPortfolioNode node, DateRange range, DateOnly asOf)
    {
        var granularity = node.SuggestGranularity(range, asOf);
        var series = node.DrawdownSeries(range, granularity, asOf);

        return series.HasData ? -series.Points.Min(p => p.Value) : 0;
    }

    private static NamedSeries RunningSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today,
        string name,
        Func<Entry, decimal> selector
    )
    {
        if (!node.TryGetSpan(range, today, out var from, out var to))
            return NamedSeries.Empty(name);

        // Everything before the window still counts towards the running total, otherwise a series
        // starting mid-life would look like the holding began at zero.
        var running = node.Entries.Where(e => e.Date < from).Sum(selector);
        var byBucket = node
            .Entries.Within(from, to)
            .GroupBy(e => granularity.BucketStart(e.Date))
            .ToDictionary(g => g.Key, g => g.Sum(selector));

        var points = new List<SeriesPoint>();

        foreach (var bucket in granularity.Buckets(from, to))
        {
            if (byBucket.TryGetValue(bucket, out var delta))
                running += delta;

            points.Add(new SeriesPoint(Min(granularity.BucketEnd(bucket), to), running));
        }

        return new NamedSeries(name, points);
    }

    private static NamedSeries BucketSeries(
        this IPortfolioNode node,
        DateRange range,
        Granularity granularity,
        DateOnly? today,
        string name,
        Func<Entry, decimal> selector
    )
    {
        if (!node.TryGetSpan(range, today, out var from, out var to))
            return NamedSeries.Empty(name);

        var byBucket = node
            .Entries.Within(from, to)
            .GroupBy(e => granularity.BucketStart(e.Date))
            .ToDictionary(g => g.Key, g => g.Sum(selector));

        var points = new List<SeriesPoint>();

        foreach (var bucket in granularity.Buckets(from, to))
        {
            byBucket.TryGetValue(bucket, out var value);
            points.Add(new SeriesPoint(bucket, value));
        }

        return new NamedSeries(name, points);
    }

    /// <summary>A bucket size that keeps the node's own span readable.</summary>
    public static Granularity SuggestGranularity(
        this IPortfolioNode node,
        DateRange range,
        DateOnly? today = null
    ) =>
        node.TryGetSpan(range, today, out var from, out var to)
            ? GranularityExtensions.SuggestFor(from, to)
            : Granularity.Monthly;

    private static bool TryGetSpan(
        this IPortfolioNode node,
        DateRange range,
        DateOnly? today,
        out DateOnly from,
        out DateOnly to
    )
    {
        from = default;
        to = default;

        var entries = node.Entries;
        if (entries.Count == 0)
            return false;

        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        from = range.EffectiveFrom(entries[0].Date);
        to = range.EffectiveTo(Max(now, entries[^1].Date));

        if (range.To is not null)
            to = Min(to, range.To.Value);

        return to >= from;
    }

    #endregion

    #region Distribution and ranking

    /// <summary>
    /// A metric split across the node's direct children: balance per platform, earnings per
    /// holding, cost per platform. Zero and negative slices are kept, because hiding them would
    /// make the parts stop adding up to the whole.
    /// </summary>
    public static IReadOnlyList<Slice> DistributionByChild(
        this IPortfolioNode node,
        PortfolioMetric metric,
        DateRange? range = null,
        DateOnly? today = null
    ) =>
        [
            .. node
                .Children.Select(child => new Slice(
                    child.Name,
                    child.ValueOf(metric, range ?? DateRange.All, today),
                    child.Key
                ))
                .OrderByDescending(s => s.Value),
        ];

    /// <summary>The same metric split by investment type rather than by platform.</summary>
    public static IReadOnlyList<Slice> DistributionByInvestmentType(
        this PortfolioRoot root,
        PortfolioMetric metric,
        DateRange? range = null,
        DateOnly? today = null
    ) =>
        [
            .. root
                .FinancialItems.GroupBy(i => i.Type)
                .Select(g => new Slice(
                    g.Key.ToString(),
                    new GroupNode($"type:{g.Key}", g.Key.ToString(), g).ValueOf(
                        metric,
                        range ?? DateRange.All,
                        today
                    ),
                    $"type:{g.Key}"
                ))
                .OrderByDescending(s => s.Value),
        ];

    /// <summary>Best or worst holdings by a metric.</summary>
    public static IReadOnlyList<Slice> Ranking(
        this IPortfolioNode node,
        PortfolioMetric metric,
        int count = 5,
        bool ascending = false,
        DateRange? range = null,
        DateOnly? today = null
    )
    {
        var slices = node
            .Leaves()
            .Select(leaf => new Slice(
                leaf.Name,
                leaf.ValueOf(metric, range ?? DateRange.All, today),
                leaf.Key
            ));

        return
        [
            .. (ascending ? slices.OrderBy(s => s.Value) : slices.OrderByDescending(s => s.Value))
                .Take(count),
        ];
    }

    /// <summary>
    /// Herfindahl–Hirschman index of a split: 1 means everything sits in one place, 1/n means it is
    /// spread evenly over n. A plain-language read on concentration risk.
    /// </summary>
    public static decimal HerfindahlIndex(IReadOnlyList<Slice> slices)
    {
        var total = slices.Sum(s => Math.Max(0, s.Value));
        if (total <= 0)
            return 0;

        decimal sum = 0;

        foreach (var slice in slices)
        {
            var share = Math.Max(0, slice.Value) / total;
            sum += share * share;
        }

        return sum;
    }

    /// <summary>Share of the total held by the largest <paramref name="count"/> slices.</summary>
    public static decimal TopShare(IReadOnlyList<Slice> slices, int count = 1)
    {
        var total = slices.Sum(s => Math.Max(0, s.Value));
        if (total <= 0)
            return 0;

        return slices
                .Select(s => Math.Max(0, s.Value))
                .OrderByDescending(v => v)
                .Take(count)
                .Sum() / total;
    }

    /// <summary>One metric of one node, used by distributions, rankings and sortable tables.</summary>
    public static decimal ValueOf(
        this IPortfolioNode node,
        PortfolioMetric metric,
        DateRange range,
        DateOnly? today = null
    )
    {
        var asOf = range.EffectiveTo(today ?? DateOnly.FromDateTime(DateTime.Today));
        var inRange = node.Entries.Within(range.From, asOf);

        return metric switch
        {
            PortfolioMetric.TotalBalance => node.BalanceAt(asOf),
            PortfolioMetric.EstimatedValue => node.ValueAt(asOf),
            PortfolioMetric.Invested => node.InvestedAt(asOf),
            PortfolioMetric.Cash => node.CashAt(asOf),
            PortfolioMetric.MarketValue => node.MarketValueAt(asOf),
            PortfolioMetric.NetDeposits => inRange.NetDeposits(),
            PortfolioMetric.Deposits => inRange.TotalDeposits(),
            PortfolioMetric.Withdrawals => inRange.TotalWithdrawals(),
            PortfolioMetric.GrossIncome => inRange.GrossIncome(),
            PortfolioMetric.Interest => inRange.TotalInterest(),
            PortfolioMetric.Bonuses => inRange.TotalBonuses(),
            PortfolioMetric.TotalCosts => inRange.TotalCosts(),
            PortfolioMetric.Taxes => inRange.TotalTaxes(),
            PortfolioMetric.Fees => inRange.TotalFees(),
            PortfolioMetric.Losses => inRange.TotalLosses(),
            PortfolioMetric.NetEarnings => inRange.NetEarnings(),
            PortfolioMetric.UnrealisedGain =>
                node.HasValuations() ? node.MarketValueAt(asOf) - node.InvestedAt(asOf) : 0,
            PortfolioMetric.Xirr => (decimal)(node.XirrFor(range, today) ?? 0),
            _ => inRange.Count(),
        };
    }

    #endregion

    #region Return matrix

    /// <summary>
    /// A node and its children scored over several periods — the per-platform, per-year return
    /// table. Works for any node, so drilling into a platform gives the same table for its holdings.
    /// </summary>
    public static IReadOnlyList<ReturnRow> ReturnMatrix(
        this IPortfolioNode node,
        IReadOnlyList<(string Label, DateRange Range)> periods,
        DateOnly? today = null,
        bool includeTotalRow = true
    )
    {
        var rows = new List<ReturnRow>();

        if (includeTotalRow)
            rows.Add(node.ToReturnRow(periods, today, "Total"));

        foreach (var child in node.Children.OrderByDescending(c => c.BalanceAt(
            (today ?? DateOnly.FromDateTime(DateTime.Today))
        )))
        {
            rows.Add(child.ToReturnRow(periods, today));
        }

        return rows;
    }

    public static ReturnRow ToReturnRow(
        this IPortfolioNode node,
        IReadOnlyList<(string Label, DateRange Range)> periods,
        DateOnly? today = null,
        string? nameOverride = null
    )
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);

        return new ReturnRow(
            node.Key,
            nameOverride ?? node.Name,
            node.Kind switch
            {
                PortfolioNodeKind.Portfolio => PortfolioNodeKindTag.Portfolio,
                PortfolioNodeKind.Platform => PortfolioNodeKindTag.Platform,
                PortfolioNodeKind.FinancialItem => PortfolioNodeKindTag.FinancialItem,
                _ => PortfolioNodeKindTag.Group,
            },
            [
                .. periods.Select(p => new PeriodReturn(
                    p.Label,
                    p.Range,
                    node.XirrFor(p.Range, now),
                    node.Entries.Within(p.Range.From, p.Range.EffectiveTo(now)).NetEarnings()
                )),
            ]
        );
    }

    /// <summary>
    /// The default period set: all time, this year, and the three preceding calendar years.
    /// </summary>
    public static IReadOnlyList<(string Label, DateRange Range)> DefaultPeriods(
        DateOnly? today = null,
        int pastYears = 3
    )
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        var periods = new List<(string, DateRange)> { ("All time", DateRange.All) };

        for (var offset = 0; offset <= pastYears; offset++)
        {
            var year = now.Year - offset;
            periods.Add((year.ToString(), DateRange.Year(year)));
        }

        return periods;
    }

    #endregion

    #region Tree helpers

    /// <summary>Every leaf under a node, or the node itself when it is already a leaf.</summary>
    public static IReadOnlyList<IPortfolioNode> Leaves(this IPortfolioNode node)
    {
        if (node.Children.Count == 0)
            return [node];

        var leaves = new List<IPortfolioNode>();
        Collect(node, leaves);
        return leaves;

        static void Collect(IPortfolioNode current, List<IPortfolioNode> into)
        {
            foreach (var child in current.Children)
            {
                if (child.Children.Count == 0)
                    into.Add(child);
                else
                    Collect(child, into);
            }
        }
    }

    /// <summary>The node itself and every descendant, depth first.</summary>
    public static IEnumerable<IPortfolioNode> Flatten(this IPortfolioNode node)
    {
        yield return node;

        foreach (var child in node.Children)
        foreach (var descendant in child.Flatten())
            yield return descendant;
    }

    public static IPortfolioNode? FindByKey(this IPortfolioNode node, string key) =>
        node.Flatten().FirstOrDefault(n => n.Key == key);

    public static bool HasValuations(this IPortfolioNode node) =>
        node.Entries.Any(e => e.MarketValue is not null);

    #endregion

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
