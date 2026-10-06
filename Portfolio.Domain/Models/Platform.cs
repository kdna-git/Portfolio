using Portfolio.Domain.Analytics;

namespace Portfolio.Domain.Models;

/// <summary>
/// Where holdings live: a broker, a bank, a lending site. Aggregates its financial items and never
/// stores figures of its own, so a platform total always equals the sum of its parts.
/// </summary>
public sealed class Platform : IPortfolioNode
{
    private readonly List<FinancialItem> _financialItems;

    public Platform(long id, string name, IEnumerable<FinancialItem>? financialItems = null)
    {
        Id = id;
        Name = name;
        _financialItems = financialItems is null ? [] : [.. financialItems];
    }

    public long Id { get; }

    public string Name { get; }

    public IReadOnlyList<FinancialItem> FinancialItems => _financialItems;

    public string Key => $"platform:{Id}";

    public PortfolioNodeKind Kind => PortfolioNodeKind.Platform;

    public IReadOnlyList<IPortfolioNode> Children => _financialItems;

    public IReadOnlyList<Entry> Entries =>
        [.. _financialItems.SelectMany(i => i.Entries).InOrder()];

    public void AddFinancialItem(FinancialItem item) => _financialItems.Add(item);

    public bool ReplaceFinancialItem(FinancialItem item)
    {
        var index = _financialItems.FindIndex(i => i.Id == item.Id);
        if (index < 0)
            return false;

        _financialItems[index] = item;
        return true;
    }

    public bool RemoveFinancialItem(long itemId) =>
        _financialItems.RemoveAll(i => i.Id == itemId) > 0;

    public FinancialItem? FindFinancialItem(long itemId) =>
        _financialItems.FirstOrDefault(i => i.Id == itemId);

    #region Balance

    public decimal TotalBalance => _financialItems.Sum(i => i.TotalBalance);

    public decimal Cash => _financialItems.Sum(i => i.Cash);

    public decimal Invested => _financialItems.Sum(i => i.Invested);

    public decimal MarketValue => _financialItems.Sum(i => i.MarketValue);

    public decimal UnrealisedGain => _financialItems.Sum(i => i.UnrealisedGain);

    public decimal EstimatedValue => _financialItems.Sum(i => i.EstimatedValue);

    #endregion

    #region History

    public decimal NetDeposits => _financialItems.Sum(i => i.NetDeposits);

    public decimal TotalDeposits => _financialItems.Sum(i => i.TotalDeposits);

    public decimal TotalWithdrawals => _financialItems.Sum(i => i.TotalWithdrawals);

    public decimal CapitalFreedom => EntryMath.CapitalFreedom(TotalBalance, NetDeposits);

    #endregion

    #region Earnings

    public decimal NetEarnings => _financialItems.Sum(i => i.NetEarnings);

    public decimal GrossIncome => _financialItems.Sum(i => i.GrossIncome);

    public decimal InterestBonuses => GrossIncome;

    public decimal TotalCosts => _financialItems.Sum(i => i.TotalCosts);

    public decimal Losses => _financialItems.Sum(i => i.Losses);

    #endregion

    public decimal MarketValueAt(DateOnly date) =>
        _financialItems.Sum(i => i.MarketValueAt(date));

    public decimal BalanceAt(DateOnly date) => _financialItems.Sum(i => i.BalanceAt(date));

    public decimal EstimatedValueAt(DateOnly date) =>
        _financialItems.Sum(i => i.EstimatedValueAt(date));

    public override string ToString() => Name;
}
