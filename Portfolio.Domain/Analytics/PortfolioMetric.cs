namespace Portfolio.Domain.Analytics;

/// <summary>
/// A figure a distribution, ranking or sort can be based on. Having one enum for this is what lets
/// a single chart component answer "balance by platform", "earnings by item" and "cost by type".
/// </summary>
public enum PortfolioMetric
{
    TotalBalance,
    EstimatedValue,
    Invested,
    Cash,
    MarketValue,
    NetDeposits,
    Deposits,
    Withdrawals,
    GrossIncome,
    Interest,
    Bonuses,
    TotalCosts,
    Taxes,
    Fees,
    Losses,
    NetEarnings,
    UnrealisedGain,
    Xirr,
    EntryCount,
}

public static class PortfolioMetricExtensions
{
    public static string DisplayName(this PortfolioMetric metric) =>
        metric switch
        {
            PortfolioMetric.TotalBalance => "Balance",
            PortfolioMetric.EstimatedValue => "Estimated value",
            PortfolioMetric.Invested => "Invested",
            PortfolioMetric.Cash => "Cash",
            PortfolioMetric.MarketValue => "Market value",
            PortfolioMetric.NetDeposits => "Net deposits",
            PortfolioMetric.Deposits => "Deposits",
            PortfolioMetric.Withdrawals => "Withdrawals",
            PortfolioMetric.GrossIncome => "Gross income",
            PortfolioMetric.Interest => "Interest",
            PortfolioMetric.Bonuses => "Bonuses",
            PortfolioMetric.TotalCosts => "Costs",
            PortfolioMetric.Taxes => "Taxes",
            PortfolioMetric.Fees => "Fees",
            PortfolioMetric.Losses => "Losses",
            PortfolioMetric.NetEarnings => "Net earnings",
            PortfolioMetric.UnrealisedGain => "Unrealised gain",
            PortfolioMetric.Xirr => "XIRR",
            _ => "Entries",
        };

    /// <summary>How the value should be formatted when shown.</summary>
    public static MetricFormat Format(this PortfolioMetric metric) =>
        metric switch
        {
            PortfolioMetric.Xirr => MetricFormat.Percent,
            PortfolioMetric.EntryCount => MetricFormat.Count,
            _ => MetricFormat.Currency,
        };

    /// <summary>True when a negative value is bad news (earnings) rather than neutral (a flow).</summary>
    public static bool IsSigned(this PortfolioMetric metric) =>
        metric
            is PortfolioMetric.NetEarnings
                or PortfolioMetric.UnrealisedGain
                or PortfolioMetric.Xirr
                or PortfolioMetric.NetDeposits;
}

/// <summary>How to render a number.</summary>
public enum MetricFormat
{
    Currency,
    Percent,
    Count,
    Number,
    Date,
}
