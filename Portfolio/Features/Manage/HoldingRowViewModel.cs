using CommunityToolkit.Mvvm.Input;
using Portfolio.Charts.Data;
using Portfolio.Charts.ViewModels;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;

namespace Portfolio.Features.Manage;

/// <summary>
/// A platform or a holding prepared for a list: the figures that matter, already formatted, plus a
/// sparkline so the row shows a trend and not only a snapshot.
/// </summary>
/// <remarks>
/// Used by both the holdings list and the platforms list. The two screens differ in what they filter
/// and group by, not in what a row looks like, so a shared row type keeps them visually identical.
/// </remarks>
public sealed partial class HoldingRowViewModel
{
    public HoldingRowViewModel(
        IPortfolioNode node,
        NodeStatistics stats,
        string subtitle,
        SparklineViewModel? sparkline,
        Action<IPortfolioNode> open,
        Action<HoldingRowViewModel> edit,
        Action<HoldingRowViewModel> delete
    )
    {
        Node = node;
        Stats = stats;
        Name = node.Name;
        Subtitle = subtitle;
        Sparkline = sparkline;
        _open = open;
        _edit = edit;
        _delete = delete;

        BalanceText = ValueFormatter.Format((double)stats.TotalBalance, ValueFormat.Currency);
        CashText = ValueFormatter.Format((double)stats.Cash, ValueFormat.Currency);
        InvestedText = ValueFormatter.Format((double)stats.Invested, ValueFormat.Currency);
        MarketValueText = ValueFormatter.Format((double)stats.MarketValue, ValueFormat.Currency);
        EarningsText = ValueFormatter.FormatSigned((double)stats.NetEarnings, ValueFormat.Currency);
        CostsText = ValueFormatter.Format((double)stats.TotalCosts, ValueFormat.Currency);
        ShareText = ValueFormatter.Format((double)stats.ShareOfParent, ValueFormat.Percent);

        ReturnText = stats.Xirr is null
            ? "n/a"
            : ValueFormatter.Format(stats.Xirr.Value, ValueFormat.Percent);

        LastActivityText = stats.LastActivity is null
            ? "No entries"
            : stats.LastActivity.Value.ToString("d MMM yyyy");

        EntryCountText = $"{stats.EntryCount} entries";
    }

    private readonly Action<IPortfolioNode> _open;
    private readonly Action<HoldingRowViewModel> _edit;
    private readonly Action<HoldingRowViewModel> _delete;

    public IPortfolioNode Node { get; }

    public NodeStatistics Stats { get; }

    public string Name { get; }

    public string Subtitle { get; }

    public SparklineViewModel? Sparkline { get; }

    public string BalanceText { get; }

    public string CashText { get; }

    public string InvestedText { get; }

    public string MarketValueText { get; }

    public string EarningsText { get; }

    public string CostsText { get; }

    public string ReturnText { get; }

    public string ShareText { get; }

    public string LastActivityText { get; }

    public string EntryCountText { get; }

    public bool EarningsIsPositive => Stats.NetEarnings > 0;

    public bool EarningsIsNegative => Stats.NetEarnings < 0;

    public bool ReturnIsPositive => Stats.Xirr > 0;

    public bool ReturnIsNegative => Stats.Xirr < 0;

    /// <summary>True for a holding nothing has been recorded against yet.</summary>
    public bool IsDormant => Stats.EntryCount == 0;

    [RelayCommand]
    private void Open() => _open(Node);

    [RelayCommand]
    private void Edit() => _edit(this);

    [RelayCommand]
    private void Delete() => _delete(this);
}

/// <summary>A run of rows under one heading, with the group's own totals.</summary>
/// <remarks>
/// <paramref name="Columns"/> rides along because the panel that lays the cards out has the group as
/// its data context, not the screen. Reaching back up to the screen from inside the panel template is
/// what broke the compact layout: the binding syntax for it only works in compiled bindings, and an
/// items panel cannot use those.
/// </remarks>
public sealed record HoldingGroupViewModel(
    string? Header,
    string? Summary,
    IReadOnlyList<HoldingRowViewModel> Rows,
    int Columns = 1
)
{
    public bool HasHeader => !string.IsNullOrEmpty(Header);
}
