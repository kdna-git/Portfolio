using FluentAssertions;
using MVVM.Core.Layout;
using MVVM.Core.Stores;
using Portfolio.Charts.ViewModels;
using Portfolio.Composition;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Portfolio.Features.Analysis;
using Portfolio.Features.Shared;
using Portfolio.Services;
using Portfolio.Storage;
using Portfolio.Stores;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

/// <summary>
/// The statistics screen's pickers, driven the way the real controls drive them.
/// </summary>
public sealed class AnalysisScreenTests
{
    private static async Task<(AnalysisViewModel Screen, PortfolioStore Store)> OpenExplorerAsync()
    {
        var repository = new JsonPortfolioRepository(new InMemoryBlobStore())
        {
            SeedFactory = () => DemoData.CreateSnapshot(On(2026, 1, 1)),
        };

        var store = new PortfolioStore(repository);
        await store.LoadAsync();

        var layout = new LayoutStore();
        var navigator = new AppNavigator(
            store,
            layout,
            new NavigationStore(),
            new ModalNavigationStore(),
            new BackupService(store, new InMemoryDeviceStorage()),
            new FakeBackupFiles()
        );

        return (new AnalysisViewModel(store, layout, navigator, mode: AnalysisMode.Explorer), store);
    }

    /// <summary>
    /// What an Avalonia ComboBox does when its items are replaced: it clears its selection, and the
    /// two-way binding writes that null straight back into the view model.
    /// </summary>
    private static void BehaveLikeAComboBox(AnalysisViewModel screen) =>
        screen.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AnalysisViewModel.ScopeOptions))
                screen.SelectedScope = null;
        };

    private static ScopeOption Option(AnalysisViewModel screen, string label) =>
        screen.ScopeOptions.Single(o => o.Label == label);

    [Fact]
    public async Task Picking_a_scope_keeps_it()
    {
        var (screen, _) = await OpenExplorerAsync();
        BehaveLikeAComboBox(screen);

        screen.SelectedScope = Option(screen, "Mintos");

        screen.SelectedScope!.Label.Should().Be("Mintos");
        screen.Title.Should().Be("Mintos");
    }

    [Fact]
    public async Task The_scope_survives_a_change_to_the_data()
    {
        var (screen, store) = await OpenExplorerAsync();
        BehaveLikeAComboBox(screen);
        screen.SelectedScope = Option(screen, "Mintos");

        await store.AddEntryAsync(
            new Entry(0, store.FinancialItems[0].Id, On(2026, 2, 1), deposit: 10)
        );

        screen.SelectedScope!.Label.Should().Be("Mintos", "new data rebuilds the list, but keeps the choice");
        screen.ScopeOptions.Should().Contain(screen.SelectedScope);
        screen.Title.Should().Be("Mintos");
    }

    [Fact]
    public async Task The_figure_picker_shows_names_not_enum_values()
    {
        var (screen, _) = await OpenExplorerAsync();

        // A ComboBox with no item template shows ToString(), which is what the person reads.
        var shown = screen.MetricOptions.Select(o => o.ToString()).ToList();

        shown.Should().Contain(new[] { "Balance", "Net earnings", "Estimated value" });
        shown.Should().NotContain(label => label.Contains("TotalBalance") || label.Contains("NetEarnings"));
    }

    [Fact]
    public async Task The_figure_picker_says_what_it_compares_and_appears_only_where_there_are_parts()
    {
        var (screen, _) = await OpenExplorerAsync();

        screen.ComparisonTitle.Should().Be("Compare platforms by");
        screen.ShowMetricPicker.Should().BeTrue();

        screen.SelectedScope = Option(screen, "Mintos");
        screen.ComparisonTitle.Should().Be("Compare holdings by");
        screen.ShowMetricPicker.Should().BeTrue();

        screen.SelectedScope = Option(screen, "Consumer loans EUR");
        screen.HasComparison.Should().BeFalse("a single holding has no parts to compare");
        screen.ShowMetricPicker.Should().BeFalse("it would change nothing there");
    }

    [Fact]
    public async Task Changing_the_figure_redraws_the_charts_beneath_it_and_nothing_else()
    {
        var (screen, _) = await OpenExplorerAsync();
        var overTime = screen.Sections;
        var tables = screen.ComparisonSections.Where(s => s.Content is DataTableViewModel).ToList();

        screen.SelectedMetric = screen.MetricOptions.Single(o => o.Metric == PortfolioMetric.NetEarnings);

        screen
            .ComparisonSections.Select(s => s.Header)
            .Should()
            .Contain(new[] { "Net earnings by platform", "Net earnings by platform, ranked" });
        screen.Sections.Should().BeSameAs(overTime, "the charts above do not depend on the figure");
        screen
            .ComparisonSections.Where(s => s.Content is DataTableViewModel)
            .Should()
            .Equal(tables, "the tables keep their state, sort order included");
    }
}
