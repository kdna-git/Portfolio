using Portfolio.Domain.Models;

namespace Portfolio.Domain.Analytics;

/// <summary>
/// Every headline figure for one scope over one date range. Computed once and handed to the UI, so
/// a screen showing twenty numbers walks the entries once rather than twenty times.
/// </summary>
/// <remarks>
/// Balance figures are always as of the end of the range (a balance is a snapshot). Flow and
/// earnings figures cover the range only (a flow is a period measure). That distinction is why
/// <see cref="TotalBalance"/> and <see cref="NetEarnings"/> do not add up for a partial range.
/// </remarks>
public sealed record NodeStatistics
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required PortfolioNodeKind Kind { get; init; }

    public required DateRange Range { get; init; }

    /// <summary>The day the balance figures refer to.</summary>
    public required DateOnly AsOf { get; init; }

    #region Balance (as of AsOf)

    public decimal TotalBalance { get; init; }

    public decimal Cash { get; init; }

    public decimal Invested { get; init; }

    public decimal MarketValue { get; init; }

    public decimal EstimatedValue { get; init; }

    public decimal UnrealisedGain { get; init; }

    #endregion

    #region Flows (within Range)

    public decimal Deposits { get; init; }

    public decimal Withdrawals { get; init; }

    public decimal NetDeposits { get; init; }

    /// <summary>Net money you put in over the whole life of the scope, regardless of range.</summary>
    public decimal LifetimeNetDeposits { get; init; }

    #endregion

    #region Earnings (within Range)

    public decimal Interest { get; init; }

    public decimal Bonuses { get; init; }

    public decimal GrossIncome { get; init; }

    public decimal Taxes { get; init; }

    public decimal Fees { get; init; }

    public decimal TotalCosts { get; init; }

    public decimal Losses { get; init; }

    public decimal NetEarnings { get; init; }

    #endregion

    #region Ratios

    /// <summary>Money-weighted annual return over the range. Null when undefined.</summary>
    public double? Xirr { get; init; }

    /// <summary>Share of the balance not funded by deposits.</summary>
    public decimal CapitalFreedom { get; init; }

    /// <summary>Costs as a share of gross income.</summary>
    public decimal CostRatio { get; init; }

    /// <summary>Net earnings over average capital employed in the range.</summary>
    public decimal SimpleReturn { get; init; }

    /// <summary>Net earnings over lifetime net deposits: "what each euro paid in has earned".</summary>
    public decimal YieldOnDeposits { get; init; }

    /// <summary>Share of the parent scope's balance this node holds. 1 for the root.</summary>
    public decimal ShareOfParent { get; init; }

    #endregion

    #region Activity

    public int EntryCount { get; init; }

    public int ItemCount { get; init; }

    /// <summary>Items with a non-zero balance at <see cref="AsOf"/>.</summary>
    public int ActiveItemCount { get; init; }

    public DateOnly? FirstActivity { get; init; }

    public DateOnly? LastActivity { get; init; }

    public int MonthsTracked { get; init; }

    public decimal AverageMonthlyEarnings { get; init; }

    public decimal BestMonthEarnings { get; init; }

    public DateOnly? BestMonth { get; init; }

    public decimal WorstMonthEarnings { get; init; }

    public DateOnly? WorstMonth { get; init; }

    /// <summary>
    /// Largest peak-to-trough fall of the estimated value within the range, as a fraction of the
    /// peak. 0 when the value never fell.
    /// </summary>
    public decimal MaxDrawdown { get; init; }

    /// <summary>Consecutive months ending at <see cref="LastActivity"/> with positive earnings.</summary>
    public int PositiveMonthStreak { get; init; }

    #endregion

    public bool IsEmpty => EntryCount == 0;
}
