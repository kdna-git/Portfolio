using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using Portfolio.Charts.ViewModels;
using Portfolio.Composition;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Portfolio.Features.Shared;
using Portfolio.Services;
using Portfolio.Stores;

namespace Portfolio.Features.Analysis;

/// <summary>
/// Where a scope is heading if it carries on as it has: a median line with a bad and a good case
/// that fan out over time, drawn on from the scope's real history.
/// </summary>
/// <remarks>
/// Every assumption starts out measured from the scope's own history (<see cref="ProjectionBasis"/>)
/// and can be overridden. Overrides are deliberately not saved: they are a what-if, and a what-if
/// that silently survives into next month's visit reads as a measurement.
/// </remarks>
public sealed partial class ProjectionViewModel : ScreenViewModel
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    private string? _pendingScopeKey;
    private PortfolioRoot? _scopesBuiltFrom;
    private ProjectionBasis? _basis;
    private string? _basisKey;

    /// <summary>Set while inputs are being filled from the basis, so that does not count as an override.</summary>
    private bool _isApplyingBasis;

    public ProjectionViewModel(
        PortfolioStore store,
        LayoutStore layout,
        AppNavigator navigator,
        string? scopeKey = null
    )
        : base(store, layout, navigator)
    {
        Title = "Projection";
        ScopeOptions = [];
        BandOptions =
        [
            new BandOption("Likely (P10–P90)", Projection.P10P90, "1 in 10"),
            new BandOption("Wide (P5–P95)", Projection.P5P95, "1 in 20"),
        ];

        Chart = new TimeSeriesChartViewModel
        {
            Title = "Value, then where it is heading",
            Subtitle = "History up to today, then the median with a bad and a good case",
            EmptyMessage = "No history to project from yet",
        };

        _pendingScopeKey = scopeKey;
        SelectedBand = BandOptions[0];
        Years = 10;

        CompleteInitialisation();
    }

    #region Selections

    public IReadOnlyList<ScopeOption> ScopeOptions
    {
        get;
        private set => SetProperty(ref field, value);
    }

    [ObservableProperty]
    public partial ScopeOption? SelectedScope { get; set; }

    public IReadOnlyList<BandOption> BandOptions { get; }

    [ObservableProperty]
    public partial BandOption SelectedBand { get; set; }

    #endregion

    #region Assumptions

    /// <summary>Median return a year, in percent. Annual because that is how returns are quoted.</summary>
    [ObservableProperty]
    public partial decimal? AnnualReturnPercent { get; set; }

    /// <summary>How much a year's return swings, in percent: the width of the band.</summary>
    [ObservableProperty]
    public partial decimal? AnnualVolatilityPercent { get; set; }

    /// <summary>Net money added each month. Negative to model drawing it down.</summary>
    [ObservableProperty]
    public partial decimal? MonthlyContribution { get; set; }

    [ObservableProperty]
    public partial decimal? Years { get; set; }

    /// <summary>A target value to look for. Empty or zero for none.</summary>
    [ObservableProperty]
    public partial decimal? Goal { get; set; }

    /// <summary>True once any measured assumption has been changed by hand.</summary>
    [ObservableProperty]
    public partial bool IsOverridden { get; set; }

    #endregion

    #region Results

    public TimeSeriesChartViewModel Chart { get; }

    public MetricTileGroup? Outcome
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>When the goal is reached in each scenario. Null without a goal.</summary>
    [ObservableProperty]
    public partial string? GoalText { get; set; }

    /// <summary>What the measured assumptions were measured from, so a strange band can be judged.</summary>
    [ObservableProperty]
    public partial string? BasisText { get; set; }

    /// <summary>Why the measured figures may not deserve trust. Null when there is nothing to warn about.</summary>
    [ObservableProperty]
    public partial string? Warning { get; set; }

    [ObservableProperty]
    public partial bool IsEmptyScope { get; set; }

    public double ChartHeight => Layout.ChartHeight;

    public int Columns => Layout.Columns;

    #endregion

    [RelayCommand]
    private void UseHistory()
    {
        if (_basis is not null)
            ApplyBasis(_basis);

        Recalculate();
    }

    partial void OnSelectedScopeChanged(ScopeOption? value) => RefreshIfReady();

    partial void OnSelectedBandChanged(BandOption value) => OnInputChanged(isAssumption: false);

    partial void OnAnnualReturnPercentChanged(decimal? value) => OnInputChanged(isAssumption: true);

    partial void OnAnnualVolatilityPercentChanged(decimal? value) => OnInputChanged(isAssumption: true);

    partial void OnMonthlyContributionChanged(decimal? value) => OnInputChanged(isAssumption: true);

    partial void OnYearsChanged(decimal? value) => OnInputChanged(isAssumption: false);

    partial void OnGoalChanged(decimal? value) => OnInputChanged(isAssumption: false);

    /// <summary>
    /// Redraws the projection only. Re-measuring the history would be wasted work: typing a number
    /// changes the assumptions, not the past.
    /// </summary>
    private void OnInputChanged(bool isAssumption)
    {
        if (_isApplyingBasis || !IsInitialised || IsRefreshSuppressed)
            return;

        if (isAssumption)
            IsOverridden = true;

        Recalculate();
    }

    protected override void Refresh()
    {
        var root = Store.Root;
        RebuildScopesIfDataChanged(root);

        var scope = SelectedScope?.Node ?? root;
        Subtitle = scope.Name;

        _basis = scope.Basis(_today);

        // A new scope starts from its own history; the same scope keeps what was typed in, so editing
        // an entry in another window does not throw a what-if away.
        if (_basisKey != scope.Key || !IsOverridden)
            ApplyBasis(_basis);

        _basisKey = scope.Key;

        OnPropertyChanged(nameof(ChartHeight));
        OnPropertyChanged(nameof(Columns));
        Recalculate();
    }

    /// <summary>Same approach as the statistics screen: only rebuild the list when the data changed.</summary>
    private void RebuildScopesIfDataChanged(PortfolioRoot root)
    {
        if (ReferenceEquals(_scopesBuiltFrom, root))
            return;

        using (SuppressRefresh())
        {
            var key = _pendingScopeKey ?? SelectedScope?.Key;

            ScopeOptions = PortfolioScopes.Build(root);
            _scopesBuiltFrom = root;
            _pendingScopeKey = null;
            SelectedScope = PortfolioScopes.Resolve(ScopeOptions, key);
        }
    }

    private void ApplyBasis(ProjectionBasis basis)
    {
        _isApplyingBasis = true;

        try
        {
            AnnualReturnPercent = Math.Round((decimal)(basis.AnnualReturn * 100), 1);
            AnnualVolatilityPercent = Math.Round((decimal)(basis.AnnualVolatility * 100), 1);
            MonthlyContribution = Math.Round(basis.MonthlyContribution, 0);
            IsOverridden = false;
        }
        finally
        {
            _isApplyingBasis = false;
        }
    }

    private void Recalculate()
    {
        var scope = SelectedScope?.Node ?? Store.Root;
        var basis = _basis ?? scope.Basis(_today);

        IsEmptyScope = scope.Entries.Count == 0;
        BasisText = DescribeBasis(basis);
        Warning = WarningFor(basis);

        if (IsEmptyScope)
        {
            Chart.SetSeries([]);
            Outcome = null;
            GoalText = null;
            return;
        }

        var months = (int)Math.Clamp(Years ?? 10, 1, 60) * 12;

        var assumptions = new ProjectionAssumptions(
            Projection.ToMonthlyReturn((double)(AnnualReturnPercent ?? 0) / 100),
            Projection.ToMonthlyVolatility(Math.Max(0, (double)(AnnualVolatilityPercent ?? 0) / 100)),
            MonthlyContribution ?? 0,
            months,
            SelectedBand.Width
        );

        var points = Projection.Project(basis.StartValue, _today, assumptions);

        DrawChart(scope, points, months);
        Outcome = BuildOutcome(points, assumptions, months);
        GoalText = DescribeGoal(basis.StartValue, assumptions);
    }

    private void DrawChart(IPortfolioNode scope, IReadOnlyList<ProjectionPoint> points, int months)
    {
        // As much history as there is future, capped by what exists: the past is the context the
        // projection should be read against, not the main event.
        var history = scope.EstimatedValueSeries(
            new DateRange(_today.AddMonths(-months), _today),
            Granularity.Monthly,
            _today,
            "History"
        );

        var color = ChartPalette.Slot(0);
        var edge = new SeriesStyle { Color = color, StrokeThickness = 1 };

        var series = new List<TimeSeries>
        {
            new("History", AnalyticsPresenter.ToTimeValues(history), SeriesStyle.Line(ChartPalette.Neutral)),
            new("Good case", Values(points, p => p.High), edge),
            new("Median", Values(points, p => p.Median), SeriesStyle.Line(color)),
            new("Bad case", Values(points, p => p.Low), edge),
        };

        if (Goal is > 0)
        {
            var goal = (double)Goal.Value;
            var from = (history.HasData ? history.Points[0].Date : _today).ToDateTime(TimeOnly.MinValue);
            var to = points[^1].Date.ToDateTime(TimeOnly.MinValue);

            series.Add(
                new TimeSeries(
                    "Goal",
                    [new TimeValue(from, goal), new TimeValue(to, goal)],
                    SeriesStyle.Reference(ChartPalette.Serious)
                )
            );
        }

        Chart.SetSeries(series);
    }

    private static IReadOnlyList<TimeValue> Values(
        IReadOnlyList<ProjectionPoint> points,
        Func<ProjectionPoint, decimal> pick
    ) => [.. points.Select(p => new TimeValue(p.Date.ToDateTime(TimeOnly.MinValue), (double)pick(p)))];

    private MetricTileGroup BuildOutcome(
        IReadOnlyList<ProjectionPoint> points,
        ProjectionAssumptions assumptions,
        int months
    )
    {
        var end = points[^1];
        var paidIn = assumptions.MonthlyContribution * months;
        var odds = SelectedBand.Odds;

        var hero = new MetricTile(
            $"Median in {months / 12} years",
            (double)end.Median,
            description: "Half the outcomes land above this, half below.",
            emphasis: MetricEmphasis.Hero
        );

        var tiles = new List<MetricTile>
        {
            new("Bad case", (double)end.Low, description: $"Only {odds} outcomes land below this."),
            new("Good case", (double)end.High, description: $"Only {odds} outcomes land above this."),
            new(
                "You add",
                (double)paidIn,
                isSigned: true,
                description: "The monthly contribution over the whole horizon."
            ),
            new(
                "Growth (median)",
                (double)(end.Median - points[0].Median - paidIn),
                isSigned: true,
                description: "What the median ends up with beyond today's value and what you add."
            ),
        };

        return new MetricTileGroup("Outcome", hero, tiles) { Columns = Layout.TileColumns };
    }

    private string? DescribeGoal(decimal startValue, ProjectionAssumptions assumptions)
    {
        if (Goal is not > 0)
            return null;

        var goal = Goal.Value;
        var target = ValueFormatter.Format(goal, ValueFormat.Currency);

        string When(double z)
        {
            var date = Projection.GoalReached(startValue, _today, assumptions, goal, z);
            if (date is null)
                return "never";

            if (date.Value <= _today)
                return "already there";

            var years = (date.Value.DayNumber - _today.DayNumber) / 365.25;
            return $"{date.Value.ToString("MMM yyyy", CultureInfo.CurrentCulture)} ({years:0.#} years)";
        }

        return $"{target}: median {When(0)} · good case {When(SelectedBand.Width)} · bad case {When(-SelectedBand.Width)}";
    }

    private static string DescribeBasis(ProjectionBasis basis)
    {
        if (basis.Months == 0)
            return "No complete month of history yet, so nothing was measured.";

        var parts = new List<string>
        {
            $"Measured over {basis.Months} months",
            $"{ValueFormatter.Format(basis.AnnualReturn, ValueFormat.Percent)} a year",
            $"volatility {ValueFormatter.Format(basis.AnnualVolatility, ValueFormat.Percent)}",
            $"{ValueFormatter.Format(basis.MonthlyContribution, ValueFormat.Currency)} a month added",
        };

        if (basis.Holdings > 0)
            parts.Add($"{basis.ValuedHoldings} of {basis.Holdings} holdings report a value");

        return string.Join(" · ", parts);
    }

    private static string? WarningFor(ProjectionBasis basis)
    {
        if (basis.Months > 0 && !basis.IsReliable)
        {
            return $"Only {basis.Months} months of history. The measured return and volatility are "
                + "mostly noise at this point; consider typing in your own.";
        }

        if (basis.Holdings > 0 && basis.ValuedHoldings < basis.Holdings)
        {
            return "Holdings that never report a market value count at cost plus interest, which looks "
                + "steadier than it is. The band is probably narrower than it should be.";
        }

        return null;
    }
}

/// <summary>A band width offered in the picker.</summary>
/// <param name="Width">Standard deviations from the median to each edge.</param>
/// <param name="Odds">How rare an outcome beyond either edge is, for the tile descriptions.</param>
public sealed record BandOption(string Label, double Width, string Odds)
{
    public override string ToString() => Label;
}
