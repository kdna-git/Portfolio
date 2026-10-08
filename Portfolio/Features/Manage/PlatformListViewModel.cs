using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using Portfolio.Charts.Data;
using Portfolio.Charts.ViewModels;
using Portfolio.Composition;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Portfolio.Features.Shared;
using Portfolio.Services;
using Portfolio.Stores;

namespace Portfolio.Features.Manage;

/// <summary>
/// Platforms, with the split of the portfolio between them and a way into each one's statistics.
/// </summary>
/// <remarks>
/// The list is paired with a distribution chart because "how much sits where" is the first question a
/// platform list has to answer, and the figure and the rows share the same colours so they read as one
/// view rather than two.
/// </remarks>
public sealed partial class PlatformListViewModel : ScreenViewModel
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public PlatformListViewModel(PortfolioStore store, LayoutStore layout, AppNavigator navigator)
        : base(store, layout, navigator)
    {
        Title = "Platforms";
        PeriodOptions = PortfolioPeriods.Build(_today);
        SelectedPeriod = PeriodOptions[0];
        SortOptions = Enum.GetValues<HoldingSort>();
        Rows = [];
        Distribution = new DistributionChartViewModel { Title = "Balance by platform" };
        Breakdown = new DataTableViewModel();
        Confirmation = new DeleteConfirmation();
        SelectedSort = HoldingSort.Balance;
        SortDescending = true;
        ViewMode = layout.IsCompact ? ListViewMode.Cards : ListViewMode.Table;

        CompleteInitialisation();
    }

    public DeleteConfirmation Confirmation { get; }

    public DistributionChartViewModel Distribution { get; }

    /// <summary>Side-by-side figures for every platform, as the sortable table.</summary>
    public DataTableViewModel Breakdown
    {
        get;
        private set => SetProperty(ref field, value);
    }

    #region Filters and presentation

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    public IReadOnlyList<PeriodOption> PeriodOptions { get; }

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; }

    public IReadOnlyList<HoldingSort> SortOptions { get; }

    [ObservableProperty]
    public partial HoldingSort SelectedSort { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    [ObservableProperty]
    public partial ListViewMode ViewMode { get; set; }

    public bool IsTableView => ViewMode == ListViewMode.Table;

    public bool IsCardView => ViewMode == ListViewMode.Cards;

    /// <summary>Tall enough for a full-size donut plus a row per platform under it.</summary>
    public double DistributionHeight => Distribution.HeightFor(Layout.ChartHeight);

    #endregion

    #region Results

    public IReadOnlyList<HoldingRowViewModel> Rows
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial bool IsEmptyResult { get; set; }

    [ObservableProperty]
    public partial bool HasAnyPlatforms { get; set; }

    [ObservableProperty]
    public partial string TotalsSummary { get; set; } = string.Empty;

    #endregion

    #region Commands

    [RelayCommand]
    private void AddPlatform() => Navigator.EditPlatform();

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    [RelayCommand]
    private void ShowCards() => ViewMode = ListViewMode.Cards;

    [RelayCommand]
    private void ShowTable() => ViewMode = ListViewMode.Table;

    #endregion

    partial void OnSearchTextChanged(string? value) => RefreshIfReady();

    partial void OnSelectedPeriodChanged(PeriodOption value) => RefreshIfReady();

    partial void OnSelectedSortChanged(HoldingSort value) => RefreshIfReady();

    partial void OnSortDescendingChanged(bool value) => RefreshIfReady();

    partial void OnViewModeChanged(ListViewMode value)
    {
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCardView));
    }

    protected override void OnBreakpointChanged()
    {
        if (Layout.IsCompact && ViewMode == ListViewMode.Table)
            ViewMode = ListViewMode.Cards;

        RefreshIfReady();
    }

    protected override void Refresh()
    {
        var range = SelectedPeriod.Range;
        var asOf = range.EffectiveTo(_today);
        var root = Store.Root;
        var portfolioBalance = root.BalanceAt(asOf);

        HasAnyPlatforms = root.Platforms.Count > 0;

        var search = SearchText?.Trim();

        var matching = root
            .Platforms.Where(p =>
                string.IsNullOrEmpty(search)
                || p.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || p.FinancialItems.Any(i =>
                    i.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                )
            )
            .ToList();

        var rows = matching
            .Select(platform => new HoldingRowViewModel(
                platform,
                platform.Describe(range, _today, portfolioBalance),
                $"{platform.FinancialItems.Count} holdings",
                AnalyticsPresenter.Sparkline(platform, range, _today),
                Navigator.OpenScope,
                row => Navigator.EditPlatform((Platform)row.Node),
                AskDelete
            ))
            .ToList();

        Rows = Sort(rows).ToList();
        IsEmptyResult = Rows.Count == 0;

        Distribution.IsCompact = Layout.IsCompact;
        Distribution.SetData(
            AnalyticsPresenter.ToLabeledValues(
                root.DistributionByChild(PortfolioMetric.TotalBalance, range, _today)
            )
        );
        OnPropertyChanged(nameof(DistributionHeight));

        Breakdown = AnalyticsPresenter.ChildBreakdown(
            root,
            [
                PortfolioMetric.TotalBalance,
                PortfolioMetric.Cash,
                PortfolioMetric.Invested,
                PortfolioMetric.NetEarnings,
                PortfolioMetric.Xirr,
            ],
            range,
            _today
        );

        TotalsSummary = string.Join(
            "   ·   ",
            $"{Rows.Count} platforms",
            $"Balance {ValueFormatter.Format((double)portfolioBalance, ValueFormat.Currency)}",
            $"Earnings {ValueFormatter.FormatSigned((double)root.Entries.Within(range.From, asOf).NetEarnings(), ValueFormat.Currency)}"
        );

        Subtitle = HasAnyPlatforms
            ? $"{Rows.Count} of {root.Platforms.Count} platforms · {SelectedPeriod.Label}"
            : "Add a platform to get started";
    }

    private void AskDelete(HoldingRowViewModel row)
    {
        var platform = (Platform)row.Node;
        var entryCount = platform.FinancialItems.Sum(i => i.Entries.Count);

        Confirmation.Ask(
            $"Delete {platform.Name}?",
            platform.FinancialItems.Count == 0
                ? "It has no holdings."
                : $"Its {platform.FinancialItems.Count} holdings and {entryCount} entries will be deleted too. This cannot be undone.",
            () => Store.DeletePlatformAsync(platform.Id)
        );
    }

    private IEnumerable<HoldingRowViewModel> Sort(IEnumerable<HoldingRowViewModel> rows)
    {
        var ordered = SelectedSort switch
        {
            HoldingSort.Balance => rows.OrderBy(r => r.Stats.TotalBalance),
            HoldingSort.Invested => rows.OrderBy(r => r.Stats.Invested),
            HoldingSort.Earnings => rows.OrderBy(r => r.Stats.NetEarnings),
            HoldingSort.Return => rows.OrderBy(r => r.Stats.Xirr ?? double.MinValue),
            HoldingSort.LastActivity => rows.OrderBy(r => r.Stats.LastActivity ?? DateOnly.MinValue),
            _ => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        return SortDescending ? ordered.Reverse() : ordered;
    }
}
