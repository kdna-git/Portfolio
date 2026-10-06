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

namespace Portfolio.Features.Analysis;

/// <summary>How much of the analysis screen to show.</summary>
public enum AnalysisMode
{
    /// <summary>Landing page: the portfolio, a few headline figures, platforms to drill into.</summary>
    Dashboard,

    /// <summary>Everything, with a scope picker: the answer to "statistics for one platform".</summary>
    Explorer,

    /// <summary>Explorer opened on a specific platform or holding, with a way back.</summary>
    DrillDown,
}

/// <summary>
/// Statistics and charts for any scope over any period.
/// </summary>
/// <remarks>
/// One screen serves the dashboard, the statistics explorer and every drill-down, because the domain
/// computes statistics for any <see cref="IPortfolioNode"/> rather than for the portfolio alone.
/// Choosing a platform or a single holding in the scope picker re-runs exactly the same code against
/// a different node.
/// </remarks>
public sealed partial class AnalysisViewModel : ScreenViewModel
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private readonly AnalysisMode _mode;

    public AnalysisViewModel(
        PortfolioStore store,
        LayoutStore layout,
        AppNavigator navigator,
        string? scopeKey = null,
        bool isDrillDown = false,
        AnalysisMode mode = AnalysisMode.Explorer
    )
        : base(store, layout, navigator)
    {
        _mode = isDrillDown ? AnalysisMode.DrillDown : mode;

        ScopeOptions = [];
        PeriodOptions = PortfolioPeriods.Build(_today);
        MetricOptions =
        [
            .. new[]
            {
                PortfolioMetric.TotalBalance,
                PortfolioMetric.EstimatedValue,
                PortfolioMetric.Invested,
                PortfolioMetric.NetEarnings,
                PortfolioMetric.GrossIncome,
                PortfolioMetric.TotalCosts,
                PortfolioMetric.NetDeposits,
            }.Select(m => new MetricOption(m, m.DisplayName())),
        ];

        TileGroups = [];
        Sections = [];
        ComparisonSections = [];
        ChildCards = [];

        _pendingScopeKey = scopeKey;
        SelectedPeriod = PeriodOptions[0];
        SelectedMetric = MetricOptions[0];

        CompleteInitialisation();
    }

    private string? _pendingScopeKey;

    /// <summary>The data <see cref="ScopeOptions"/> was built from; see <see cref="RebuildScopesIfDataChanged"/>.</summary>
    private PortfolioRoot? _scopesBuiltFrom;

    #region Selections

    public IReadOnlyList<ScopeOption> ScopeOptions
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial ScopeOption? SelectedScope { get; set; }

    public IReadOnlyList<PeriodOption> PeriodOptions { get; }

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; }

    /// <summary>The figures the comparison charts can be drawn by, with their display names.</summary>
    public IReadOnlyList<MetricOption> MetricOptions { get; }

    [ObservableProperty]
    public partial MetricOption SelectedMetric { get; set; }

    #endregion

    #region Content

    public IReadOnlyList<MetricTileGroup> TileGroups
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The scope over time: value, activity, earnings.</summary>
    public IReadOnlyList<AnalysisSection> Sections
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// The scope's parts side by side, drawn under <see cref="ComparisonTitle"/> and the figure
    /// picker. Empty for a single holding, which has no parts.
    /// </summary>
    public IReadOnlyList<AnalysisSection> ComparisonSections
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(HasComparison));
            OnPropertyChanged(nameof(ShowMetricPicker));
        }
    }

    public bool HasComparison => ComparisonSections.Count > 0;

    /// <summary>"Compare platforms by" or "Compare holdings by": says what the picker beside it changes.</summary>
    [ObservableProperty]
    public partial string? ComparisonTitle { get; set; }

    /// <summary>The scope's children as tappable cards. The way into a per-platform view.</summary>
    public IReadOnlyList<ScopeCardViewModel> ChildCards
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial bool IsEmptyPortfolio { get; set; }

    [ObservableProperty]
    public partial string? ChildSectionTitle { get; set; }

    public bool ShowScopePicker => _mode != AnalysisMode.Dashboard;

    /// <summary>Only where there are parts to compare; on a single holding it would change nothing.</summary>
    public bool ShowMetricPicker => _mode != AnalysisMode.Dashboard && HasComparison;

    public bool ShowBackButton => _mode == AnalysisMode.DrillDown;

    /// <summary>
    /// Backup and sync from the dashboard, on the layouts whose bottom bar has no room for it. Wider
    /// layouts reach it from the side rail.
    /// </summary>
    public bool ShowBackupShortcut => _mode == AnalysisMode.Dashboard && Layout.IsCompact;

    public int TileColumns => Layout.TileColumns;

    public double ChartHeight => Layout.ChartHeight;

    #endregion

    #region Commands

    [RelayCommand]
    private void AddPlatform() => Navigator.EditPlatform();

    [RelayCommand]
    private void AddEntry() => Navigator.EditEntry();

    [RelayCommand]
    private void OpenBackup() => Navigator.OpenBackup();

    #endregion

    partial void OnSelectedScopeChanged(ScopeOption? value) => RefreshIfReady();

    partial void OnSelectedPeriodChanged(PeriodOption value) => RefreshIfReady();

    partial void OnSelectedMetricChanged(MetricOption value)
    {
        // Only the comparison follows the figure. Redrawing the whole page would replay every chart's
        // animation and pull the eye away from the two that actually changed.
        if (!IsInitialised || IsRefreshSuppressed || IsEmptyPortfolio)
            return;

        BuildComparison(SelectedScope?.Node ?? Store.Root, SelectedPeriod.Range);
    }

    protected override void Refresh()
    {
        var root = Store.Root;
        IsEmptyPortfolio = root.Platforms.Count == 0;
        OnPropertyChanged(nameof(ShowBackupShortcut));

        RebuildScopesIfDataChanged(root);

        var scope = SelectedScope?.Node ?? root;
        var range = SelectedPeriod.Range;

        Title = _mode == AnalysisMode.Dashboard ? "Dashboard" : scope.Name;
        Subtitle = _mode == AnalysisMode.Dashboard
            ? $"{root.Platforms.Count} platforms · {root.FinancialItems.Count} holdings"
            : $"{DescribeKind(scope)} · {SelectedPeriod.Label}";

        if (IsEmptyPortfolio)
        {
            TileGroups = [];
            Sections = [];
            ComparisonSections = [];
            ChildCards = [];
            return;
        }

        var stats = scope.Describe(range, _today, ParentBalanceOf(scope, root));
        BuildTiles(scope, stats, range);
        BuildSections(scope, range);
        BuildChildCards(scope, range);

        OnPropertyChanged(nameof(TileColumns));
        OnPropertyChanged(nameof(ChartHeight));
    }

    private void BuildTiles(IPortfolioNode scope, NodeStatistics stats, DateRange range)
    {
        var columns = Layout.TileColumns;
        var trend = AnalyticsPresenter.Sparkline(scope, range, _today);

        var groups = new List<MetricTileGroup>
        {
            AnalyticsPresenter.BalanceTiles(stats, columns, trend),
            AnalyticsPresenter.FundingTiles(stats, columns),
            AnalyticsPresenter.EarningsTiles(stats, columns),
        };

        if (_mode != AnalysisMode.Dashboard)
        {
            var distribution = scope.DistributionByChild(PortfolioMetric.TotalBalance, range, _today);

            groups.Add(
                AnalyticsPresenter.BehaviourTiles(
                    stats,
                    PortfolioAnalytics.HerfindahlIndex(distribution),
                    PortfolioAnalytics.TopShare(distribution),
                    columns
                )
            );
        }

        TileGroups = groups;
    }

    /// <summary>
    /// Rebuilds the scope list after the data changed — and only then.
    /// </summary>
    /// <remarks>
    /// Replacing a ComboBox's items clears its selection, and the two-way binding writes that null
    /// straight back into <see cref="SelectedScope"/>. When the list was rebuilt on every refresh, the
    /// refresh that a pick itself triggers threw the pick away, and the picker snapped back to the
    /// whole portfolio. So the list is left alone unless the data changed, and when it does change,
    /// the choice is read before the list is replaced rather than after.
    /// </remarks>
    private void RebuildScopesIfDataChanged(PortfolioRoot root)
    {
        // The store builds a new root on every change, so an unchanged root means unchanged scopes.
        if (ReferenceEquals(_scopesBuiltFrom, root))
            return;

        using (SuppressRefresh())
        {
            var key = _pendingScopeKey ?? SelectedScope?.Key;

            ScopeOptions = PortfolioScopes.Build(root);
            _scopesBuiltFrom = root;
            _pendingScopeKey = null;

            // Resolve by key, not by reference: a reload rebuilds every node, so the previously
            // selected object no longer exists even though the user's choice still does.
            SelectedScope = _mode == AnalysisMode.Dashboard
                ? ScopeOptions[0]
                : PortfolioScopes.Resolve(ScopeOptions, key);
        }
    }

    private void BuildSections(IPortfolioNode scope, DateRange range)
    {
        var overTime = new List<AnalysisSection>
        {
            Section("value", AnalyticsPresenter.ValueAgainstDeposits(scope, range, _today)),
            Section("activity", AnalyticsPresenter.PeriodActivity(scope, range, _today)),
        };

        if (_mode != AnalysisMode.Dashboard)
        {
            overTime.Add(Section("earnings", AnalyticsPresenter.CumulativeEarnings(scope, range, _today)));
            overTime.Add(Section("breakdown", AnalyticsPresenter.EarningsBreakdown(scope, range, _today)));

            if (scope.HasValuations())
                overTime.Add(Section("drawdown", AnalyticsPresenter.Drawdown(scope, range, _today)));
        }

        Sections = overTime;

        // The parts side by side: first the charts the figure picker draws, then the tables, which
        // show several figures at once and need no picker.
        ComparisonSections = scope.Children.Count == 0
            ? []
            : [.. FigureCharts(scope, range), .. ComparisonTables(scope, range)];

        var parts = scope.Kind == PortfolioNodeKind.Portfolio ? "platforms" : "holdings";
        ComparisonTitle = _mode == AnalysisMode.Dashboard ? $"Compare {parts}" : $"Compare {parts} by";
    }

    /// <summary>
    /// Redraws only what the chosen figure drives. The tables are kept as they are — including any
    /// column the person sorted them by.
    /// </summary>
    private void BuildComparison(IPortfolioNode scope, DateRange range)
    {
        if (scope.Children.Count == 0)
            return;

        ComparisonSections =
        [
            .. FigureCharts(scope, range),
            .. ComparisonSections.Where(s => s.Content is DataTableViewModel),
        ];
    }

    /// <summary>The charts drawn by <see cref="SelectedMetric"/>: the only ones the figure picker changes.</summary>
    private List<AnalysisSection> FigureCharts(IPortfolioNode scope, DateRange range)
    {
        var height = Layout.ChartHeight;
        var metric = SelectedMetric.Metric;

        var charts = new List<AnalysisSection>
        {
            Section(
                "distribution",
                AnalyticsPresenter.Distribution(scope, metric, range, _today),
                height: height + 120
            ),
        };

        if (_mode != AnalysisMode.Dashboard && scope.Children.Count > 1)
        {
            charts.Add(
                Section(
                    "comparison",
                    AnalyticsPresenter.ChildComparison(scope, metric, range, _today),
                    height: Math.Max(height, 40 + (scope.Children.Count * 34))
                )
            );
        }

        return charts;
    }

    private List<AnalysisSection> ComparisonTables(IPortfolioNode scope, DateRange range) =>
    [
        Section(
            "matrix",
            AnalyticsPresenter.ReturnMatrix(scope, PortfolioAnalytics.DefaultPeriods(_today), _today),
            subheader: "Money-weighted annual return. Blank where a period has too little data.",
            height: double.NaN
        ),
        Section(
            "breakdownTable",
            AnalyticsPresenter.ChildBreakdown(
                scope,
                [
                    PortfolioMetric.TotalBalance,
                    PortfolioMetric.Invested,
                    PortfolioMetric.NetEarnings,
                    PortfolioMetric.TotalCosts,
                    PortfolioMetric.Xirr,
                ],
                range,
                _today
            ),
            height: double.NaN
        ),
    ];

    private AnalysisSection Section(
        string key,
        object content,
        string? header = null,
        string? subheader = null,
        double? height = null
    )
    {
        var title = header;
        var subtitle = subheader;

        // Charts already carry their own heading; the card reuses it so the two cannot drift.
        if (content is ChartViewModelBase chart)
        {
            title ??= chart.Title;
            subtitle ??= chart.Subtitle;
        }
        else if (content is DataTableViewModel table)
        {
            title ??= table.Title;
        }

        return new AnalysisSection(key, title, subtitle, content, height ?? Layout.ChartHeight);
    }

    private void BuildChildCards(IPortfolioNode scope, DateRange range)
    {
        if (scope.Children.Count == 0)
        {
            ChildCards = [];
            ChildSectionTitle = null;
            return;
        }

        var parentBalance = scope.BalanceAt(range.EffectiveTo(_today));

        ChildSectionTitle = scope.Kind == PortfolioNodeKind.Portfolio ? "Platforms" : "Holdings";

        ChildCards =
        [
            .. scope
                .Children.Select(child =>
                {
                    var stats = child.Describe(range, _today, parentBalance);

                    return new ScopeCardViewModel(
                        child,
                        ValueFormatter.Format(stats.TotalBalance, ValueFormat.Currency),
                        ValueFormatter.FormatSigned((double)stats.NetEarnings, ValueFormat.Currency),
                        stats.Xirr is null
                            ? "n/a"
                            : ValueFormatter.Format(stats.Xirr.Value, ValueFormat.Percent),
                        ValueFormatter.Format((double)stats.ShareOfParent, ValueFormat.Percent),
                        stats.NetEarnings >= 0,
                        AnalyticsPresenter.Sparkline(child, range, _today),
                        Navigator.OpenScope
                    );
                })
                .OrderByDescending(c => c.Node.BalanceAt(range.EffectiveTo(_today))),
        ];
    }

    private static decimal? ParentBalanceOf(IPortfolioNode scope, PortfolioRoot root) =>
        scope.Kind == PortfolioNodeKind.Portfolio ? null : root.BalanceAt(DateOnly.FromDateTime(DateTime.Today));

    private static string DescribeKind(IPortfolioNode node) =>
        node.Kind switch
        {
            PortfolioNodeKind.Portfolio => "Whole portfolio",
            PortfolioNodeKind.Platform => "Platform",
            PortfolioNodeKind.FinancialItem => "Holding",
            _ => "Group",
        };
}

/// <summary>One figure offered by the comparison picker, shown by its display name.</summary>
public sealed record MetricOption(PortfolioMetric Metric, string Label)
{
    public override string ToString() => Label;
}
