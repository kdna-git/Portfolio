namespace Portfolio.Domain.Models;

/// <summary>What a node represents, so the UI can label and icon it.</summary>
public enum PortfolioNodeKind
{
    Portfolio,
    Platform,
    FinancialItem,

    /// <summary>A synthetic grouping, e.g. "everything of type Grow".</summary>
    Group,
}

/// <summary>
/// Anything statistics can be computed for: the whole portfolio, one platform, one financial item,
/// or an ad-hoc group.
/// </summary>
/// <remarks>
/// This interface is the reason the dashboard is no longer portfolio-only. Every chart and every
/// metric takes an <see cref="IPortfolioNode"/>, so pointing them at a platform or a single holding
/// needs no new code — only a different node.
/// </remarks>
public interface IPortfolioNode
{
    /// <summary>Stable identity, unique within a portfolio. Used for selection and persistence.</summary>
    string Key { get; }

    string Name { get; }

    PortfolioNodeKind Kind { get; }

    /// <summary>Direct children, empty for a leaf.</summary>
    IReadOnlyList<IPortfolioNode> Children { get; }

    /// <summary>
    /// Every entry belonging to this node and its descendants, ordered by date then id.
    /// </summary>
    IReadOnlyList<Entry> Entries { get; }

    /// <summary>
    /// Reported value of the holdings on a date. Rolled forward from the most recent valuation, so
    /// it must be computed per holding and summed — never from a merged entry list.
    /// </summary>
    decimal MarketValueAt(DateOnly date);
}
