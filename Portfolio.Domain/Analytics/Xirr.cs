namespace Portfolio.Domain.Analytics;

/// <summary>A dated cash flow. Positive means money came back to you.</summary>
public readonly record struct CashFlow(decimal Amount, DateOnly Date);

/// <summary>
/// Money-weighted annual return (the spreadsheet XIRR).
/// </summary>
/// <remarks>
/// Newton–Raphson first because it converges in a handful of iterations on realistic inputs, with
/// bracketed bisection as a fallback for the awkward ones. Returns null rather than 0 when the rate
/// is undefined — no flows, all flows the same sign, or no sign change in the search interval — so
/// callers can show "n/a" instead of a misleading 0 %.
/// </remarks>
public static class Xirr
{
    private const double DaysPerYear = 365.0;
    private const double LowestRate = -0.9999;
    private const double HighestRate = 1000.0;

    public static double? Calculate(
        IReadOnlyList<CashFlow> cashFlows,
        double precision = 1e-9,
        int maxIterations = 128
    )
    {
        if (cashFlows.Count < 2)
            return null;

        var hasPositive = false;
        var hasNegative = false;

        foreach (var flow in cashFlows)
        {
            if (flow.Amount > 0)
                hasPositive = true;
            else if (flow.Amount < 0)
                hasNegative = true;
        }

        // Without both directions the equation has no root: you cannot earn a return on nothing,
        // and money that only ever left has no terminal value to compare against.
        if (!hasPositive || !hasNegative)
            return null;

        var origin = cashFlows.Min(f => f.Date);
        var flows = new (double Amount, double Years)[cashFlows.Count];

        for (var i = 0; i < cashFlows.Count; i++)
        {
            flows[i] = (
                (double)cashFlows[i].Amount,
                (cashFlows[i].Date.DayNumber - origin.DayNumber) / DaysPerYear
            );
        }

        var newton = SolveNewton(flows, precision, maxIterations);
        if (newton is not null)
            return Round(newton.Value);

        var bisection = SolveBisection(flows, precision, maxIterations);
        return bisection is null ? null : Round(bisection.Value);
    }

    /// <summary>XIRR, or 0 when undefined. For display slots that cannot show "n/a".</summary>
    public static double CalculateOrZero(IReadOnlyList<CashFlow> cashFlows) =>
        Calculate(cashFlows) ?? 0;

    private static double? SolveNewton(
        (double Amount, double Years)[] flows,
        double precision,
        int maxIterations
    )
    {
        var rate = 0.1;

        for (var i = 0; i < maxIterations; i++)
        {
            var value = NetPresentValue(flows, rate);

            if (Math.Abs(value) < precision)
                return rate;

            var derivative = NetPresentValueDerivative(flows, rate);

            if (Math.Abs(derivative) < 1e-12)
                return null;

            var next = rate - (value / derivative);

            if (double.IsNaN(next) || double.IsInfinity(next) || next <= LowestRate || next > HighestRate)
                return null;

            if (Math.Abs(next - rate) < precision)
                return next;

            rate = next;
        }

        return null;
    }

    private static double? SolveBisection(
        (double Amount, double Years)[] flows,
        double precision,
        int maxIterations
    )
    {
        var low = LowestRate;
        var high = HighestRate;
        var lowValue = NetPresentValue(flows, low);
        var highValue = NetPresentValue(flows, high);

        if (double.IsNaN(lowValue) || double.IsNaN(highValue))
            return null;

        if (Math.Sign(lowValue) == Math.Sign(highValue))
            return null;

        for (var i = 0; i < maxIterations; i++)
        {
            var middle = (low + high) / 2;
            var middleValue = NetPresentValue(flows, middle);

            if (Math.Abs(middleValue) < precision || (high - low) < precision)
                return middle;

            if (Math.Sign(middleValue) == Math.Sign(lowValue))
            {
                low = middle;
                lowValue = middleValue;
            }
            else
            {
                high = middle;
                highValue = middleValue;
            }
        }

        return (low + high) / 2;
    }

    private static double NetPresentValue((double Amount, double Years)[] flows, double rate)
    {
        var sum = 0.0;
        var factor = 1 + rate;

        foreach (var (amount, years) in flows)
            sum += amount / Math.Pow(factor, years);

        return sum;
    }

    private static double NetPresentValueDerivative(
        (double Amount, double Years)[] flows,
        double rate
    )
    {
        var sum = 0.0;
        var factor = 1 + rate;

        foreach (var (amount, years) in flows)
            sum -= years * amount / Math.Pow(factor, years + 1);

        return sum;
    }

    private static double Round(double rate) => Math.Round(rate, 6);
}
