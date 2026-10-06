using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using Portfolio.Charts.Data;
using Portfolio.Composition;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Portfolio.Features.Shared;
using Portfolio.Stores;

namespace Portfolio.Features.Manage;

/// <summary>
/// Every movement, with the controls needed to actually find one.
/// </summary>
/// <remarks>
/// The previous screen listed all entries in creation order with no search, filter or total, which
/// stops being usable at a few hundred rows. This one searches, filters by platform, holding, movement
/// kind and period, sorts, groups, switches between cards and a table, shows totals for whatever is on
/// screen, and loads in pages so a long history does not block the first paint.
/// </remarks>
public sealed partial class EntryListViewModel : ScreenViewModel
{
    private const int PageSize = 60;

    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private int _visibleCount = PageSize;

    public EntryListViewModel(PortfolioStore store, LayoutStore layout, AppNavigator navigator)
        : base(store, layout, navigator)
    {
        Title = "Entries";
        PeriodOptions = PortfolioPeriods.Build(_today);
        SelectedPeriod = PeriodOptions[0];
        MovementFilters = Enum.GetValues<MovementFilter>();
        SortOptions = Enum.GetValues<EntrySort>();
        GroupingOptions = Enum.GetValues<EntryGrouping>();
        PlatformFilters = [];
        HoldingFilters = [];
        Groups = [];
        Confirmation = new DeleteConfirmation();
        ViewMode = layout.IsCompact ? ListViewMode.Cards : ListViewMode.Table;
        SortDescending = true;

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

    public IReadOnlyList<FilterOption> HoldingFilters
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial FilterOption? SelectedHoldingFilter { get; set; }

    public IReadOnlyList<MovementFilter> MovementFilters { get; }

    [ObservableProperty]
    public partial MovementFilter SelectedMovement { get; set; }

    public IReadOnlyList<EntrySort> SortOptions { get; }

    [ObservableProperty]
    public partial EntrySort SelectedSort { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    public IReadOnlyList<EntryGrouping> GroupingOptions { get; }

    [ObservableProperty]
    public partial EntryGrouping SelectedGrouping { get; set; }

    [ObservableProperty]
    public partial ListViewMode ViewMode { get; set; }

    public bool IsTableView => ViewMode == ListViewMode.Table;

    public bool IsCardView => ViewMode == ListViewMode.Cards;

    #endregion

    #region Results

    public IReadOnlyList<EntryGroupViewModel> Groups
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial int MatchCount { get; set; }

    [ObservableProperty]
    public partial int ShownCount { get; set; }

    [ObservableProperty]
    public partial bool CanLoadMore { get; set; }

    [ObservableProperty]
    public partial bool IsEmptyResult { get; set; }

    [ObservableProperty]
    public partial bool HasAnyEntries { get; set; }

    /// <summary>Totals for the rows that match the filters — not for the whole portfolio.</summary>
    [ObservableProperty]
    public partial string TotalsSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NetTotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool NetTotalIsPositive { get; set; }

    #endregion

    #region Commands

    [RelayCommand]
    private void AddEntry() => Navigator.EditEntry();

    [RelayCommand]
    private void Edit(EntryRowViewModel? row)
    {
        if (row is not null)
            Navigator.EditEntry(row.Entry);
    }

    [RelayCommand]
    private void Delete(EntryRowViewModel? row)
    {
        if (row is null)
            return;

        Confirmation.Ask(
            "Delete this entry?",
            $"{row.DateText} · {row.HoldingName} · {row.AmountText}",
            () => Store.DeleteEntryAsync(row.Entry.Id)
        );
    }

    [RelayCommand]
    private void LoadMore()
    {
        _visibleCount += PageSize;
        RefreshIfReady();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        using (SuppressRefresh())
        {
            SearchText = null;
            SelectedPeriod = PeriodOptions[0];
            SelectedPlatformFilter = PlatformFilters.Count > 0 ? PlatformFilters[0] : null;
            SelectedHoldingFilter = HoldingFilters.Count > 0 ? HoldingFilters[0] : null;
            SelectedMovement = MovementFilter.All;
        }

        RefreshIfReady();
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortDescending = !SortDescending;
    }

    [RelayCommand]
    private void ShowCards() => ViewMode = ListViewMode.Cards;

    [RelayCommand]
    private void ShowTable() => ViewMode = ListViewMode.Table;

    #endregion

    partial void OnSearchTextChanged(string? value) => ResetAndRefresh();

    partial void OnSelectedPeriodChanged(PeriodOption value) => ResetAndRefresh();

    partial void OnSelectedPlatformFilterChanged(FilterOption? value)
    {
        if (IsRefreshSuppressed)
            return;

        // Narrowing to a platform makes a holding from another one meaningless, so the holding filter
        // is rebuilt and reset rather than silently excluding everything.
        using (SuppressRefresh())
        {
            BuildHoldingFilters();
        }

        ResetAndRefresh();
    }

    partial void OnSelectedHoldingFilterChanged(FilterOption? value) => ResetAndRefresh();

    partial void OnSelectedMovementChanged(MovementFilter value) => ResetAndRefresh();

    partial void OnSelectedSortChanged(EntrySort value) => ResetAndRefresh();

    partial void OnSortDescendingChanged(bool value) => ResetAndRefresh();

    partial void OnSelectedGroupingChanged(EntryGrouping value) => ResetAndRefresh();

    partial void OnViewModeChanged(ListViewMode value)
    {
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCardView));
    }

    protected override void OnBreakpointChanged()
    {
        // A table has no room on a phone; switch once rather than fighting the user's later choice.
        if (Layout.IsCompact && ViewMode == ListViewMode.Table)
            ViewMode = ListViewMode.Cards;

        RefreshIfReady();
    }

    private void ResetAndRefresh()
    {
        if (IsRefreshSuppressed)
            return;

        // Changing a filter puts you back on the first page: page 4 of the previous result set means
        // nothing against a different one.
        _visibleCount = PageSize;
        RefreshIfReady();
    }

    protected override void Refresh()
    {
        using (SuppressRefresh())
        {
            BuildPlatformFilters();
            BuildHoldingFilters();
        }

        var all = CollectRows().ToList();
        HasAnyEntries = Store.FinancialItems.Any(i => i.Entries.Count > 0);

        var filtered = Filter(all).ToList();
        MatchCount = filtered.Count;

        var sorted = Sort(filtered).ToList();
        var visible = sorted.Take(_visibleCount).ToList();

        ShownCount = visible.Count;
        CanLoadMore = sorted.Count > visible.Count;
        IsEmptyResult = sorted.Count == 0;

        Groups = Group(visible);
        BuildTotals(filtered);

        Subtitle = HasAnyEntries
            ? $"{MatchCount} of {all.Count} entries"
            : "No entries recorded yet";
    }

    private IEnumerable<EntryRowViewModel> CollectRows()
    {
        foreach (var platform in Store.Platforms)
        foreach (var item in platform.FinancialItems)
        foreach (var entry in item.Entries)
            yield return new EntryRowViewModel(entry, item, platform);
    }

    private IEnumerable<EntryRowViewModel> Filter(IEnumerable<EntryRowViewModel> rows)
    {
        var range = SelectedPeriod.Range;
        var platformId = SelectedPlatformFilter?.Id;
        var holdingId = SelectedHoldingFilter?.Id;
        var search = SearchText?.Trim();

        foreach (var row in rows)
        {
            if (!range.Contains(row.Entry.Date))
                continue;

            if (platformId is not null && row.Platform.Id != platformId)
                continue;

            if (holdingId is not null && row.Holding.Id != holdingId)
                continue;

            if (!MatchesMovement(row.Entry, SelectedMovement))
                continue;

            if (!string.IsNullOrEmpty(search) && !row.Matches(search))
                continue;

            yield return row;
        }
    }

    private static bool MatchesMovement(Entry entry, MovementFilter filter) =>
        filter switch
        {
            MovementFilter.Deposits => entry.Deposit != 0,
            MovementFilter.Withdrawals => entry.Withdrawal != 0,
            MovementFilter.Income => entry.GrossIncome != 0,
            MovementFilter.Costs => entry.Costs != 0 || entry.DefaultLoss != 0,
            MovementFilter.Valuations => entry.MarketValue is not null,
            _ => true,
        };

    private IEnumerable<EntryRowViewModel> Sort(IEnumerable<EntryRowViewModel> rows)
    {
        var ordered = SelectedSort switch
        {
            EntrySort.Amount => rows.OrderBy(r => Math.Abs(r.Entry.BalanceChange)),
            EntrySort.Platform => rows.OrderBy(r => r.Platform.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Entry.Date),
            EntrySort.Holding => rows.OrderBy(r => r.Holding.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Entry.Date),
            _ => rows.OrderBy(r => r.Entry.Date).ThenBy(r => r.Entry.Id),
        };

        return SortDescending ? ordered.Reverse() : ordered;
    }

    private IReadOnlyList<EntryGroupViewModel> Group(IReadOnlyList<EntryRowViewModel> rows)
    {
        if (SelectedGrouping == EntryGrouping.None)
            return rows.Count == 0 ? [] : [new EntryGroupViewModel(null, null, rows)];

        var grouped = SelectedGrouping switch
        {
            EntryGrouping.Platform => rows.GroupBy(r => r.Platform.Name),
            EntryGrouping.Holding => rows.GroupBy(r => $"{r.Platform.Name} · {r.Holding.Name}"),
            _ => rows.GroupBy(r => r.Entry.Date.ToString("MMMM yyyy")),
        };

        return
        [
            .. grouped.Select(g =>
            {
                var items = g.ToList();
                var net = items.Sum(r => r.Entry.BalanceChange);

                return new EntryGroupViewModel(
                    g.Key,
                    $"{items.Count} entries · {ValueFormatter.FormatSigned((double)net, ValueFormat.Currency)}",
                    items
                );
            }),
        ];
    }

    private void BuildTotals(IReadOnlyList<EntryRowViewModel> rows)
    {
        var entries = rows.Select(r => r.Entry).ToList();
        var net = entries.TotalBalance();

        NetTotalText = ValueFormatter.FormatSigned((double)net, ValueFormat.Currency);
        NetTotalIsPositive = net >= 0;

        TotalsSummary = string.Join(
            "   ·   ",
            $"In {ValueFormatter.Format((double)entries.TotalDeposits(), ValueFormat.Currency)}",
            $"Out {ValueFormatter.Format((double)entries.TotalWithdrawals(), ValueFormat.Currency)}",
            $"Income {ValueFormatter.Format((double)entries.GrossIncome(), ValueFormat.Currency)}",
            $"Costs {ValueFormatter.Format((double)entries.TotalCosts(), ValueFormat.Currency)}",
            $"Written off {ValueFormatter.Format((double)entries.TotalLosses(), ValueFormat.Currency)}"
        );
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

        SelectedPlatformFilter = PlatformFilters.FirstOrDefault(o => o.Id == previous) ?? PlatformFilters[0];
    }

    private void BuildHoldingFilters()
    {
        var previous = SelectedHoldingFilter?.Id;
        var platformId = SelectedPlatformFilter?.Id;

        var items = platformId is null
            ? Store.FinancialItems
            : (IReadOnlyList<FinancialItem>)(Store.FindPlatform(platformId.Value)?.FinancialItems ?? []);

        HoldingFilters =
        [
            new FilterOption(null, "All holdings"),
            .. items
                .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(i => new FilterOption(i.Id, i.Name)),
        ];

        SelectedHoldingFilter = HoldingFilters.FirstOrDefault(o => o.Id == previous) ?? HoldingFilters[0];
    }
}

/// <summary>A choice in a filter dropdown. A null <paramref name="Id"/> means "no restriction".</summary>
public sealed record FilterOption(long? Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A run of rows under one heading.</summary>
public sealed record EntryGroupViewModel(
    string? Header,
    string? Summary,
    IReadOnlyList<EntryRowViewModel> Rows
)
{
    public bool HasHeader => !string.IsNullOrEmpty(Header);
}
