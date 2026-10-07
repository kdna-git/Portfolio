using Portfolio.Domain.Analytics;

namespace Portfolio.Domain.Models;

/// <summary>
/// One holding on one platform: a loan, a fund position, a savings pot. Owns the entries that
/// describe its life.
/// </summary>
public sealed class FinancialItem(
        long id,
        long platformId,
        InvestmentType type,
        string name,
        IEnumerable<Entry>? entries = null
    ) : IPortfolioNode
{
    private readonly List<Entry> _entries = entries is null ? [] : [.. entries.InOrder()];

    public long Id { get; } = id;

    public long PlatformId { get; } = platformId;

    public InvestmentType Type { get; } = type;

    public string Name { get; } = name;

    public IReadOnlyList<Entry> Entries => _entries;

    public string Key => $"item:{Id}";

    public PortfolioNodeKind Kind => PortfolioNodeKind.FinancialItem;

    public IReadOnlyList<IPortfolioNode> Children => [];

    /// <summary>Adds an entry, keeping chronological order.</summary>
    public void AddEntry(Entry entry)
    {
        var index = _entries.FindIndex(e =>
            e.Date > entry.Date || (e.Date == entry.Date && e.Id > entry.Id)
        );

        if (index < 0)
            _entries.Add(entry);
        else
            _entries.Insert(index, entry);
    }

    public bool ReplaceEntry(Entry entry)
    {
        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index < 0)
            return false;

        _entries.RemoveAt(index);
        AddEntry(entry);
        return true;
    }

    public bool RemoveEntry(long entryId) => _entries.RemoveAll(e => e.Id == entryId) > 0;

    #region Balance

    public decimal TotalBalance => _entries.TotalBalance();

    public decimal Cash => _entries.Cash();

    public decimal Invested => _entries.InvestedBook();

    /// <summary>Last reported value rolled forward to today.</summary>
    public decimal MarketValue => MarketValueAt(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Market value minus the capital committed; zero when nothing was ever valued.</summary>
    public decimal UnrealisedGain => HasValuations ? MarketValue - Invested : 0;

    /// <summary>Cash plus market value: what the holding is worth today.</summary>
    public decimal EstimatedValue => Cash + MarketValue;

    public bool HasValuations => _entries.Any(e => e.MarketValue is not null);

    #endregion

    #region History

    public decimal NetDeposits => _entries.NetDeposits();

    public decimal TotalDeposits => _entries.TotalDeposits();

    public decimal TotalWithdrawals => _entries.TotalWithdrawals();

    public decimal CapitalFreedom => EntryMath.CapitalFreedom(TotalBalance, NetDeposits);

    public DateOnly? FirstDate => _entries.Count == 0 ? null : _entries[0].Date;

    public DateOnly? LastDate => _entries.Count == 0 ? null : _entries[^1].Date;

    #endregion

    #region Earnings

    public decimal NetEarnings => _entries.NetEarnings();

    public decimal GrossIncome => _entries.GrossIncome();

    /// <summary>Kept under its original name: interest plus bonuses, before costs.</summary>
    public decimal InterestBonuses => GrossIncome;

    public decimal TotalCosts => _entries.TotalCosts();

    public decimal Losses => _entries.TotalLosses();

    #endregion

    /// <summary>
    /// Value of the holding on a date: the most recent reported value, rolled forward by the net
    /// capital committed since that report. Falls back to capital at cost when never valued.
    /// </summary>
    public decimal MarketValueAt(DateOnly date)
    {
        var anchorIndex = -1;

        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Date > date)
                break;

            if (_entries[i].MarketValue is not null)
                anchorIndex = i;
        }

        if (anchorIndex < 0)
            return InvestedBookAt(date);

        var value = _entries[anchorIndex].MarketValue!.Value;

        // Only entries strictly after the anchor count, otherwise a valuation recorded alongside a
        // purchase would add that purchase twice.
        for (var i = anchorIndex + 1; i < _entries.Count && _entries[i].Date <= date; i++)
            value += _entries[i].InvestedChange;

        return value;
    }

    /// <summary>Book balance on a date.</summary>
    public decimal BalanceAt(DateOnly date) => _entries.UpTo(date).TotalBalance();

    /// <summary>Capital at cost committed on a date.</summary>
    public decimal InvestedBookAt(DateOnly date) => _entries.UpTo(date).InvestedBook();

    /// <summary>Cash plus market value on a date.</summary>
    public decimal EstimatedValueAt(DateOnly date) =>
        BalanceAt(date) - InvestedBookAt(date) + MarketValueAt(date);

    public override string ToString() => Name;
}
