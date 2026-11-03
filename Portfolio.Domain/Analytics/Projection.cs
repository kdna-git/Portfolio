using Portfolio.Domain.Models;

namespace Portfolio.Domain.Analytics;

/// <summary>
/// What a node's history says about its future: the inputs a projection starts from before anyone
/// overrides them.
/// </summary>
/// <param name="Months">Complete months the return figures were measured over.</param>
/// <param name="MonthlyReturn">Typical (geometric mean) monthly return, as a fraction.</param>
/// <param name="MonthlyVolatility">Standard deviation of the monthly log returns.</param>
/// <param name="MonthlyContribution">Average net deposits per month over the last year.</param>
/// <param name="StartValue">Estimated value today, where every projected line begins.</param>
/// <param name="ValuedHoldings">Holdings with at least one reported market value.</param>
/// <param name="Holdings">Holdings under the node.</param>
public sealed record ProjectionBasis(
    int Months,
    double MonthlyReturn,
    double MonthlyVolatility,
    decimal MonthlyContribution,
    decimal StartValue,
    int ValuedHoldings,
    int Holdings
)
{
    /// <summary>Below a year of history the measured return and volatility are mostly noise.</summary>
    public const int ReliableMonths = 12;

    public bool IsReliable => Months >= ReliableMonths;

    public double AnnualReturn => Projection.ToAnnualReturn(MonthlyReturn);

    public double AnnualVolatility => Projection.ToAnnualVolatility(MonthlyVolatility);
}

/// <summary>The figures a projection runs on, whether measured or typed in.</summary>
/// <param name="MonthlyReturn">Median monthly return, as a fraction.</param>
/// <param name="MonthlyVolatility">Standard deviation of the monthly log returns.</param>
/// <param name="MonthlyContribution">Net money added each month. Negative for a drawdown plan.</param>
/// <param name="Months">How far ahead to project.</param>
/// <param name="BandWidth">
/// How many standard deviations the bad and good cases sit from the median: 1.2816 for P10–P90,
/// 1.6449 for P5–P95.
/// </param>
public sealed record ProjectionAssumptions(
    double MonthlyReturn,
    double MonthlyVolatility,
    decimal MonthlyContribution,
    int Months,
    double BandWidth = Projection.P10P90
);

/// <summary>One month of a projection: the bad case, the median and the good case.</summary>
public readonly record struct ProjectionPoint(DateOnly Date, decimal Low, decimal Median, decimal High);

/// <summary>
/// Projects a value forward from its own history.
/// </summary>
/// <remarks>
/// Growth is modelled as log-normal: monthly log returns with mean <c>ln(1 + r)</c> and standard
/// deviation σ. Over <c>n</c> months the uncertainty of the compounded return grows with √n, not n,
/// which is what makes the band fan out instead of widening in a straight line. Each scenario is
/// the constant monthly rate that compounds to that percentile over the horizon, applied to the
/// starting value and to the contributions as an annuity.
/// <para>
/// That last step is an approximation — a contribution made late in the horizon is exposed to less
/// uncertainty than the starting value — but it errs towards a slightly wider band, and in exchange
/// the result is a formula: the same inputs always draw the same chart, and it can be tested
/// exactly, which a Monte Carlo run cannot.
/// </para>
/// </remarks>
public static class Projection
{
    /// <summary>Standard deviations for an 80 % band, P10 to P90.</summary>
    public const double P10P90 = 1.2816;

    /// <summary>Standard deviations for a 90 % band, P5 to P95.</summary>
    public const double P5P95 = 1.6449;

    /// <summary>How far back the return and volatility are measured. Recent behaviour matters most.</summary>
    public const int LookbackMonths = 36;

    /// <summary>How far ahead a goal is searched for before giving up.</summary>
    public const int GoalSearchMonths = 100 * 12;

    #region Basis

    /// <summary>
    /// Measures a node's monthly returns and contributions, ready to project from.
    /// </summary>
    /// <remarks>
    /// Each complete month's return is a Modified Dietz figure: the change in value not caused by
    /// deposits or withdrawals, over the capital employed during the month, with each flow weighted
    /// by how much of the month it was there for. That strips your own contributions out, so paying
    /// in does not read as performance.
    /// </remarks>
    public static ProjectionBasis Basis(this IPortfolioNode node, DateOnly today)
    {
        var leaves = node.Leaves().Where(l => l.Entries.Count > 0).ToList();
        var startValue = node.ValueAt(today);

        if (node.Entries.Count == 0)
            return new ProjectionBasis(0, 0, 0, 0, startValue, 0, 0);

        var returns = node.MonthlyReturns(today, LookbackMonths);
        var logs = returns.Select(r => Math.Log(1 + Math.Max(r, -0.99))).ToList();

        var meanLog = logs.Count == 0 ? 0 : logs.Average();
        var volatility = logs.Count < 2
            ? 0
            : Math.Sqrt(logs.Sum(l => (l - meanLog) * (l - meanLog)) / (logs.Count - 1));

        return new ProjectionBasis(
            logs.Count,
            Math.Exp(meanLog) - 1,
            volatility,
            node.AverageMonthlyContribution(today),
            startValue,
            leaves.Count(l => l.HasValuations()),
            leaves.Count
        );
    }

    /// <summary>
    /// Modified Dietz return of each of the last <paramref name="lookback"/> complete months, oldest
    /// first. Months with no capital at work are skipped: they have no return.
    /// </summary>
    public static IReadOnlyList<double> MonthlyReturns(this IPortfolioNode node, DateOnly today, int lookback)
    {
        var entries = node.Entries;
        if (entries.Count == 0)
            return [];

        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var lastComplete = thisMonth.AddDays(-1);
        var firstMonth = new DateOnly(entries[0].Date.Year, entries[0].Date.Month, 1);
        var windowStart = thisMonth.AddMonths(-lookback);
        var month = firstMonth > windowStart ? firstMonth : windowStart;
        var returns = new List<double>();

        for (; month <= lastComplete; month = month.AddMonths(1))
        {
            var end = month.AddMonths(1).AddDays(-1);
            var days = (double)end.Day;

            var opening = (double)node.ValueAt(month.AddDays(-1));
            var closing = (double)node.ValueAt(end);

            double flows = 0;
            double weighted = 0;

            foreach (var entry in entries.Within(month, end))
            {
                var flow = (double)(entry.Deposit - entry.Withdrawal);
                if (flow == 0)
                    continue;

                flows += flow;

                // Money that arrives on the last day was at work for none of the month.
                weighted += flow * (days - entry.Date.Day) / days;
            }

            var employed = opening + weighted;
            if (employed <= 0)
                continue;

            returns.Add((closing - opening - flows) / employed);
        }

        return returns;
    }

    /// <summary>
    /// Net deposits per month over the last twelve complete months, or over the node's whole life
    /// when it is younger than that.
    /// </summary>
    public static decimal AverageMonthlyContribution(this IPortfolioNode node, DateOnly today)
    {
        var entries = node.Entries;
        if (entries.Count == 0)
            return 0;

        var to = new DateOnly(today.Year, today.Month, 1).AddDays(-1);
        var firstMonth = new DateOnly(entries[0].Date.Year, entries[0].Date.Month, 1);
        var from = to.AddDays(1).AddMonths(-12);

        if (from < firstMonth)
            from = firstMonth;

        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
        if (months <= 0)
            return 0;

        return entries.Within(from, to).NetDeposits() / months;
    }

    #endregion

    #region Projection

    /// <summary>
    /// One point per month from <paramref name="start"/> (the starting value itself) to the end of
    /// the horizon.
    /// </summary>
    public static IReadOnlyList<ProjectionPoint> Project(
        decimal startValue,
        DateOnly start,
        ProjectionAssumptions assumptions
    )
    {
        var points = new List<ProjectionPoint>(assumptions.Months + 1);

        for (var n = 0; n <= assumptions.Months; n++)
        {
            points.Add(
                new ProjectionPoint(
                    start.AddMonths(n),
                    ValueAfter(startValue, assumptions, n, -assumptions.BandWidth),
                    ValueAfter(startValue, assumptions, n, 0),
                    ValueAfter(startValue, assumptions, n, assumptions.BandWidth)
                )
            );
        }

        return points;
    }

    /// <summary>
    /// Value after <paramref name="months"/> at the percentile <paramref name="z"/> standard
    /// deviations from the median. Never below zero: a plan that withdraws more than it holds runs
    /// out, it does not go into debt.
    /// </summary>
    public static decimal ValueAfter(decimal startValue, ProjectionAssumptions assumptions, int months, double z)
    {
        if (months <= 0)
            return startValue;

        var rate = ScenarioRate(assumptions.MonthlyReturn, assumptions.MonthlyVolatility, months, z);
        var growth = Math.Pow(1 + rate, months);
        var contribution = (double)assumptions.MonthlyContribution;

        var fromContributions = Math.Abs(rate) < 1e-12
            ? contribution * months
            : contribution * (growth - 1) / rate;

        var value = ((double)startValue * growth) + fromContributions;

        return value <= 0 ? 0 : (decimal)Math.Min(value, (double)decimal.MaxValue / 2);
    }

    /// <summary>
    /// The constant monthly rate that compounds to percentile <paramref name="z"/> over
    /// <paramref name="months"/>: the median log return plus z·σ/√n per month.
    /// </summary>
    public static double ScenarioRate(double monthlyReturn, double volatility, int months, double z) =>
        Math.Exp(Math.Log(1 + Math.Max(monthlyReturn, -0.99)) + (z * volatility / Math.Sqrt(months))) - 1;

    /// <summary>
    /// The first month a scenario reaches <paramref name="goal"/>, searching well past the horizon so
    /// "in 14 years" can be answered on a 10-year chart. Null when it never gets there.
    /// </summary>
    public static DateOnly? GoalReached(
        decimal startValue,
        DateOnly start,
        ProjectionAssumptions assumptions,
        decimal goal,
        double z = 0
    )
    {
        if (startValue >= goal)
            return start;

        for (var n = 1; n <= GoalSearchMonths; n++)
        {
            if (ValueAfter(startValue, assumptions, n, z) >= goal)
                return start.AddMonths(n);
        }

        return null;
    }

    #endregion

    #region Conversions

    public static double ToAnnualReturn(double monthly) => Math.Pow(1 + monthly, 12) - 1;

    public static double ToMonthlyReturn(double annual) => Math.Pow(1 + Math.Max(annual, -0.99), 1.0 / 12) - 1;

    /// <summary>Volatility scales with the square root of time.</summary>
    public static double ToAnnualVolatility(double monthly) => monthly * Math.Sqrt(12);

    public static double ToMonthlyVolatility(double annual) => annual / Math.Sqrt(12);

    #endregion
}
