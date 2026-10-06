using CommunityToolkit.Mvvm.Input;
using Portfolio.Charts.ViewModels;
using Portfolio.Domain.Models;

namespace Portfolio.Features.Shared;

/// <summary>
/// One figure on an analysis screen: a heading and whatever should be drawn under it.
/// </summary>
/// <remarks>
/// Screens expose a list of these instead of laying each chart out in markup. Adding a figure is then
/// one line in a view model, and the view stays a single template — which is also what makes the
/// layout adapt to a phone without a second copy of the page.
/// </remarks>
public sealed record AnalysisSection(
    string Key,
    string? Header,
    string? Subheader,
    object Content,
    double? Height = null,
    string? Footer = null
);

/// <summary>
/// A platform or holding summarised as a tappable card, for drilling into its own statistics.
/// </summary>
public sealed partial class ScopeCardViewModel(
    IPortfolioNode node,
    string valueText,
    string earningsText,
    string returnText,
    string shareText,
    bool earningsArePositive,
    SparklineViewModel? sparkline,
    Action<IPortfolioNode> open
)
{
    public IPortfolioNode Node { get; } = node;

    public string Name { get; } = node.Name;

    public string Kind { get; } =
        node.Kind switch
        {
            PortfolioNodeKind.Platform => $"{node.Children.Count} holdings",
            PortfolioNodeKind.FinancialItem => "Holding",
            PortfolioNodeKind.Group => $"{node.Children.Count} holdings",
            _ => "Portfolio",
        };

    public string ValueText { get; } = valueText;

    public string EarningsText { get; } = earningsText;

    public string ReturnText { get; } = returnText;

    public string ShareText { get; } = shareText;

    public bool EarningsArePositive { get; } = earningsArePositive;

    public bool EarningsAreNegative { get; } = !earningsArePositive;

    public SparklineViewModel? Sparkline { get; } = sparkline;

    [RelayCommand]
    private void Open() => open(Node);
}
