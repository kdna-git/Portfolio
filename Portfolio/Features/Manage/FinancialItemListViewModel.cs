using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using Portfolio.Charts.Data;
using Portfolio.Composition;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Portfolio.Features.Shared;
using Portfolio.Services;
using Portfolio.Stores;

namespace Portfolio.Features.Manage;

/// <summary>
/// Every holding, searchable, sortable, groupable, and each row a way into its own statistics.
/// </summary>
/// <remarks>
/// Previously this screen showed a name and a platform per holding — no figures, no sorting, and a
/// delete that did not refresh. Here each row carries the numbers you would otherwise open a detail
/// page for, plus a sparkline, and tapping it opens full statistics for that holding alone.
/// </remarks>
public sealed partial class FinancialItemListViewModel : ScreenViewModel
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public FinancialItemListViewModel(
        PortfolioStore store,
        LayoutStore layout,
        AppNavigator navigator
    )
        : base(store, layout, navigator)
    {
        Title = "Holdings";
        PeriodOptions = PortfolioPeriods.Build(_today);
        SelectedPeriod = PeriodOptions[0];
        SortOptions = Enum.GetValues<HoldingSort>();
        GroupingOptions = Enum.GetValues<ItemGrouping>();
        TypeFilters = [new TypeFilterOption(null, "All types"), .. Enum.GetValues<InvestmentType>().Select(t => new TypeFilterOption(t, t.ToString()))];
        PlatformFilters = [];
        Groups = [];
        Confirmation = new DeleteConfirmation();
        SelectedSort = HoldingSort.Balance;
        SortDescending = true;
        SelectedGrouping = ItemGrouping.Platform;
        ViewMode = layout.IsCompact ? ListViewMode.Cards : ListViewMode.Table;

        CompleteInitialisation();
    }

    public DeleteConfirmation Confirmation { get; }

    #region Filters and presentation

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    public IReadOnlyList<PeriodOption> PeriodOptions { get; }

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; }

    public IReadOnlyList<FilterOption> PlatformFilters
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial FilterOption? SelectedPlatformFilter { get; set; }

    public IReadOnlyList<TypeFilterOption> TypeFilters { get; }

    [ObservableProperty]
    public partial TypeFilterOption? SelectedTypeFilter { get; set; }

    public IReadOnlyList<HoldingSort> SortOptions { get; }

    [ObservableProperty]
    public partial HoldingSort SelectedSort { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    public IReadOnlyList<ItemGrouping> GroupingOptions { get; }

    [ObservableProperty]
    public partial ItemGrouping SelectedGrouping { get; set; }

    [ObservableProperty]
    public partial ListViewMode ViewMode { get; set; }

    /// <summary>Hide holdings with no entries. On by default once a portfolio gets large.</summary>
    [ObservableProperty]
    public partial bool HideDormant { get; set; }

    public bool IsTableView => ViewMode == ListViewMode.Table;

    public bool IsCardView => ViewMode == ListViewMode.Cards;

    #endregion

    #region Results

    public IReadOnlyList<HoldingGroupViewModel> Groups
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial bool IsEmptyResult { get; set; }

    [ObservableProperty]
    public partial bool HasAnyHoldings { get; set; }

    [ObservableProperty]
    public partial string TotalsSummary { get; set; } = string.Empty;

    #endregion

    #region Commands

    [RelayCommand]
    private void AddHolding() => Navigator.EditFinancialItem(platformId: SelectedPlatformFilter?.Id);

    [RelayCommand]
    private void AddPlatform() => Navigator.EditPlatform();

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    [RelayCommand]
    private void ShowCards() => ViewMode = ListViewMode.Cards;

    [RelayCommand]
    private void ShowTable() => ViewMode = ListViewMode.Table;

    [RelayCommand]
    private void ClearFilters()
    {
        using (SuppressRefresh())
        {
            SearchText = null;
            SelectedPeriod = PeriodOptions[0];
            SelectedPlatformFilter = PlatformFilters.Count > 0 ? PlatformFilters[0] : null;
            SelectedTypeFilter = TypeFilters[0];
            HideDormant = false;
        }

        RefreshIfReady();
    }

    #endregion

    partial void OnSearchTextChanged(string? value) => RefreshIfReady();

    partial void OnSelectedPeriodChanged(PeriodOption value) => RefreshIfReady();

    partial void OnSelectedPlatformFilterChanged(FilterOption? value) => RefreshIfReady();

    partial void OnSelectedTypeFilterChanged(TypeFilterOption? value) => RefreshIfReady();

    partial void OnSelectedSortChanged(HoldingSort value) => RefreshIfReady();

    partial void OnSortDescendingChanged(bool value) => RefreshIfReady();

    partial void OnSelectedGroupingChanged(ItemGrouping value) => RefreshIfReady();

    partial void OnHideDormantChanged(bool value) => RefreshIfReady();

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
        using (SuppressRefresh())
        {
            BuildPlatformFilters();
            SelectedTypeFilter ??= TypeFilters[0];
        }

        var range = SelectedPeriod.Range;
        var portfolioBalance = Store.Root.BalanceAt(range.EffectiveTo(_today));
        var all = Store.FinancialItems;
        HasAnyHoldings = all.Count > 0;

        var filtered = all.Where(Matches).ToList();

        var rows = filtered
            .Select(item => BuildRow(item, range, portfolioBalance))
            .Where(row => !HideDormant || !row.IsDormant)
            .ToList();

        var sorted = Sort(rows).ToList();
        IsEmptyResult = sorted.Count == 0;
        Groups = Group(sorted);

        var stats = filtered.SelectMany(i => i.Entries).Within(range.From, range.EffectiveTo(_today)).ToList();

        TotalsSummary = string.Join(
            "   ·   ",
            $"{sorted.Count} holdings",
            $"Balance {ValueFormatter.Format((double)filtered.SelectMany(i => i.Entries).UpTo(range.EffectiveTo(_today)).TotalBalance(), ValueFormat.Currency)}",
            $"Earnings {ValueFormatter.FormatSigned((double)stats.NetEarnings(), ValueFormat.Currency)}"
        );

        Subtitle = HasAnyHoldings
            ? $"{sorted.Count} of {all.Count} holdings · {SelectedPeriod.Label}"
            : "No holdings yet";
    }

    private bool Matches(FinancialItem item)
    {
        if (SelectedPlatformFilter?.Id is { } platformId && item.PlatformId != platformId)
            return false;

        if (SelectedTypeFilter?.Type is { } type && item.Type != type)
            return false;

        var search = SearchText?.Trim();

        if (string.IsNullOrEmpty(search))
            return true;

        var platformName = Store.FindPlatform(item.PlatformId)?.Name ?? string.Empty;

        return item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || platformName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || item.Type.ToString().Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private HoldingRowViewModel BuildRow(FinancialItem item, DateRange range, decimal portfolioBalance)
    {
        var platform = Store.FindPlatform(item.PlatformId);
        var stats = item.Describe(range, _today, portfolioBalance);

        return new HoldingRowViewModel(
            item,
            stats,
            $"{platform?.Name ?? "Unknown platform"} · {item.Type}",
            AnalyticsPresenter.Sparkline(item, range, _today),
            Navigator.OpenScope,
            row => Navigator.EditFinancialItem((FinancialItem)row.Node),
            AskDelete
        );
    }

    private void AskDelete(HoldingRowViewModel row)
    {
        var item = (FinancialItem)row.Node;

        Confirmation.Ask(
            $"Delete {item.Name}?",
            item.Entries.Count == 0
                ? "It has no entries."
                : $"Its {item.Entries.Count} entries will be deleted too. This cannot be undone.",
            () => Store.DeleteFinancialItemAsync(item.Id)
        );
    }

    private IEnumerable<HoldingRowViewModel> Sort(IEnumerable<HoldingRowViewModel> rows)
    {
        var ordered = SelectedSort switch
        {
            HoldingSort.Balance => rows.OrderBy(r => r.Stats.TotalBalance),
            HoldingSort.Invested => rows.OrderBy(r => r.Stats.Invested),
            HoldingSort.Earnings => rows.OrderBy(r => r.Stats.NetEarnings),
            // Holdings without a return sort to the bottom rather than counting as zero.
            HoldingSort.Return => rows.OrderBy(r => r.Stats.Xirr ?? double.MinValue),
            HoldingSort.LastActivity => rows.OrderBy(r => r.Stats.LastActivity ?? DateOnly.MinValue),
            _ => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        return SortDescending ? ordered.Reverse() : ordered;
    }

    private IReadOnlyList<HoldingGroupViewModel> Group(IReadOnlyList<HoldingRowViewModel> rows)
    {
        if (rows.Count == 0)
            return [];

        var columns = Layout.Columns;

        if (SelectedGrouping == ItemGrouping.None)
            return [new HoldingGroupViewModel(null, null, rows, columns)];

        var grouped = SelectedGrouping == ItemGrouping.Type
            ? rows.GroupBy(r => ((FinancialItem)r.Node).Type.ToString())
            : rows.GroupBy(r => Store.FindPlatform(((FinancialItem)r.Node).PlatformId)?.Name ?? "Unknown platform");

        return
        [
            .. grouped
                .Select(g =>
                {
                    var items = g.ToList();
                    var balance = items.Sum(r => r.Stats.TotalBalance);
                    var earnings = items.Sum(r => r.Stats.NetEarnings);

                    return new HoldingGroupViewModel(
                        g.Key,
                        $"{items.Count} holdings · {ValueFormatter.Format((double)balance, ValueFormat.Currency)} · {ValueFormatter.FormatSigned((double)earnings, ValueFormat.Currency)}",
                        items,
                        columns
                    );
                })
                .OrderBy(g => g.Header, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private void BuildPlatformFilters()
    {
        var previous = SelectedPlatformFilter?.Id;

        PlatformFilters =
        [
            new FilterOption(null, "All platforms"),
            .. Store
                .Platforms.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(p => new FilterOption(p.Id, p.Name)),
        ];

        SelectedPlatformFilter =
            PlatformFilters.FirstOrDefault(o => o.Id == previous) ?? PlatformFilters[0];
    }
}

/// <summary>A choice in the investment-type filter. Null means "every type".</summary>
public sealed record TypeFilterOption(InvestmentType? Type, string Label)
{
    public override string ToString() => Label;
}
