using Portfolio.Domain.Analytics;

namespace Portfolio.Domain.Models;

/// <summary>
/// The whole portfolio as a node, so "everything" is just another scope the statistics and charts
/// can be pointed at.
/// </summary>
public sealed class PortfolioRoot : IPortfolioNode
{
    private readonly List<Platform> _platforms;
    private IReadOnlyList<Entry>? _entries;

    public PortfolioRoot(IEnumerable<Platform> platforms, string name = "Whole portfolio")
    {
        _platforms = [.. platforms];
        Name = name;
    }

    public string Key => "portfolio";

    public string Name { get; }

    public PortfolioNodeKind Kind => PortfolioNodeKind.Portfolio;

    public IReadOnlyList<Platform> Platforms => _platforms;

    public IReadOnlyList<FinancialItem> FinancialItems =>
        [.. _platforms.SelectMany(p => p.FinancialItems)];

    public IReadOnlyList<IPortfolioNode> Children => _platforms;

    public IReadOnlyList<Entry> Entries =>
        _entries ??= [.. _platforms.SelectMany(p => p.FinancialItems).SelectMany(i => i.Entries).InOrder()];

    public decimal MarketValueAt(DateOnly date) => _platforms.Sum(p => p.MarketValueAt(date));

    public decimal BalanceAt(DateOnly date) => _platforms.Sum(p => p.BalanceAt(date));

    public decimal EstimatedValueAt(DateOnly date) =>
        _platforms.Sum(p => p.EstimatedValueAt(date));

    public override string ToString() => Name;
}

/// <summary>
/// A node assembled from an arbitrary set of holdings — everything of one investment type, the five
/// largest positions, a hand-picked watch list. Statistics treat it like any other scope.
/// </summary>
public sealed class GroupNode : IPortfolioNode
{
    private readonly List<FinancialItem> _items;
    private IReadOnlyList<Entry>? _entries;

    public GroupNode(string key, string name, IEnumerable<FinancialItem> items)
    {
        Key = key;
        Name = name;
        _items = [.. items];
    }

    public string Key { get; }

    public string Name { get; }

    public PortfolioNodeKind Kind => PortfolioNodeKind.Group;

    public IReadOnlyList<IPortfolioNode> Children => _items;

    public IReadOnlyList<Entry> Entries =>
        _entries ??= [.. _items.SelectMany(i => i.Entries).InOrder()];

    public decimal MarketValueAt(DateOnly date) => _items.Sum(i => i.MarketValueAt(date));
}
