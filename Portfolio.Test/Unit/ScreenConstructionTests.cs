using FluentAssertions;
using MVVM.Core.Layout;
using MVVM.Core.Stores;
using Portfolio.Charts.ViewModels;
using Portfolio.Composition;
using Portfolio.Features.Analysis;
using Portfolio.Features.Backup;
using Portfolio.Features.Manage;
using Portfolio.Services;
using Portfolio.Storage;
using Portfolio.Stores;
using Xunit;

namespace Portfolio.Test.Unit;

/// <summary>
/// Every screen must survive being constructed and then having its filters changed.
/// </summary>
/// <remarks>
/// Written after Holdings and Platforms crashed on open: assigning a filter in the constructor raised
/// its change handler, which refreshed the screen while later fields were still null. The bug was
/// invisible to a compiler and to every other test, because nothing else constructed a screen.
/// </remarks>
public sealed class ScreenConstructionTests
{
    private static async Task<(PortfolioStore Store, LayoutStore Layout, AppNavigator Navigator)> SetUpAsync(
        bool withData = true
    )
    {
        var blob = new InMemoryBlobStore();
        var repository = new JsonPortfolioRepository(blob)
        {
            SeedFactory = withData
                ? () => DemoData.CreateSnapshot(new DateOnly(2026, 1, 1))
                : () => new PortfolioSnapshot(),
        };

        var store = new PortfolioStore(repository);
        await store.LoadAsync();

        var layout = new LayoutStore();
        var navigation = new NavigationStore();
        var modal = new ModalNavigationStore();
        var backup = new BackupService(store, new InMemoryDeviceStorage());

        return (
            store,
            layout,
            new AppNavigator(store, layout, navigation, modal, backup, new FakeBackupFiles())
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Every_screen_constructs(bool withData)
    {
        var (store, layout, navigator) = await SetUpAsync(withData);

        var screens = new Func<object>[]
        {
            () => new AnalysisViewModel(store, layout, navigator, mode: AnalysisMode.Dashboard),
            () => new AnalysisViewModel(store, layout, navigator, mode: AnalysisMode.Explorer),
            () => new ProjectionViewModel(store, layout, navigator),
            () => new EntryListViewModel(store, layout, navigator),
            () => new FinancialItemListViewModel(store, layout, navigator),
            () => new PlatformListViewModel(store, layout, navigator),
            () =>
                new BackupViewModel(
                    store,
                    layout,
                    navigator,
                    new BackupService(store, new InMemoryDeviceStorage()),
                    new FakeBackupFiles()
                ),
        };

        foreach (var create in screens)
            create.Should().NotThrow();
    }

    [Fact]
    public async Task Changing_a_filter_does_not_recurse()
    {
        var (store, layout, navigator) = await SetUpAsync();
        var screen = new EntryListViewModel(store, layout, navigator);

        // Selecting a platform rebuilds the holdings filter, which reassigns its selection, which
        // raises the handler again. Without a re-entrancy guard this never returns.
        var change = () => screen.SelectedPlatformFilter = screen.PlatformFilters[1];

        change.Should().NotThrow();
        screen.HoldingFilters.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_projection_starts_from_history_and_can_go_back_to_it()
    {
        var (store, layout, navigator) = await SetUpAsync();
        var screen = new ProjectionViewModel(store, layout, navigator);

        var measured = screen.AnnualReturnPercent;
        screen.IsOverridden.Should().BeFalse();
        screen.Outcome.Should().NotBeNull();

        screen.AnnualReturnPercent = measured + 5;
        screen.IsOverridden.Should().BeTrue();

        // A horizon or a goal is a question, not an assumption about the past.
        screen.UseHistoryCommand.Execute(null);
        screen.Years = 20;
        screen.IsOverridden.Should().BeFalse();
        screen.AnnualReturnPercent.Should().Be(measured);
    }

    [Fact]
    public async Task A_narrow_viewport_switches_lists_to_cards()
    {
        var (store, layout, navigator) = await SetUpAsync();
        layout.Width = 1400;

        var screen = new FinancialItemListViewModel(store, layout, navigator);
        screen.IsTableView.Should().BeTrue("a wide window has room for a table");

        layout.Width = 420;

        screen.IsCardView.Should().BeTrue("a phone does not");
    }

    [Fact]
    public async Task Platform_rows_and_the_breakdown_table_agree()
    {
        var (store, layout, navigator) = await SetUpAsync();
        var screen = new PlatformListViewModel(store, layout, navigator);

        screen.Rows.Should().HaveCount(store.Platforms.Count);
        screen.Breakdown.Rows.Should().HaveCount(store.Platforms.Count);

        // The view binds to DisplayRow; a template typed for anything else silently falls back to
        // ToString() and the user sees "DisplayRow { Key = ... }" instead of a table.
        screen.Breakdown.Rows.Should().AllBeOfType<DisplayRow>();
        screen.Breakdown.Rows[0].Cells.Should().NotBeEmpty();
        screen.Breakdown.Rows[0].Cells[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_returns_matrix_is_built_from_display_rows_too()
    {
        var (store, layout, navigator) = await SetUpAsync();
        var screen = new AnalysisViewModel(store, layout, navigator, mode: AnalysisMode.Dashboard);

        var tables = screen
            .Sections.Concat(screen.ComparisonSections)
            .Select(s => s.Content)
            .OfType<DataTableViewModel>()
            .ToList();

        tables.Should().NotBeEmpty();
        tables.Should().OnlyContain(t => t.Rows.Count > 0);
        tables.SelectMany(t => t.Rows).Should().AllBeOfType<DisplayRow>();
    }

    [Fact]
    public async Task Sorting_a_table_column_reorders_without_losing_rows()
    {
        var (store, layout, navigator) = await SetUpAsync();
        var screen = new PlatformListViewModel(store, layout, navigator);
        var table = screen.Breakdown;
        var before = table.Rows.Count;

        table.SortCommand.Execute("name");

        table.Rows.Should().HaveCount(before);
        table.SortColumnKey.Should().Be("name");
    }
}
