using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Portfolio.Charts.ViewModels;
using Portfolio.Charts.Views;

namespace Portfolio.Charts;

/// <summary>
/// Maps every chart view model in this library to its view.
/// </summary>
/// <remarks>
/// Add it once to the host application:
/// <code>
/// &lt;Application.DataTemplates&gt;
///     &lt;charts:ChartDataTemplates /&gt;
/// &lt;/Application.DataTemplates&gt;
/// </code>
/// From then on any chart renders wherever it is put — <c>&lt;ContentControl Content="{Binding
/// MyChart}" /&gt;</c>, a list item, a tooltip — with no per-view wiring. That is what makes these
/// components usable outside the screen they were written for.
/// </remarks>
public sealed class ChartDataTemplates : IDataTemplate
{
    public Control? Build(object? param) =>
        param switch
        {
            TimeSeriesChartViewModel => new TimeSeriesChartView(),
            CategoryChartViewModel => new CategoryChartView(),
            DistributionChartViewModel => new DistributionChartView(),
            SparklineViewModel => new SparklineView(),
            MetricTile => new MetricTileView(),
            MetricTileGroup => new MetricTileGroupView(),
            DataTableViewModel => new DataTableView(),
            _ => null,
        };

    public bool Match(object? data) =>
        data
            is ChartViewModelBase
                or SparklineViewModel
                or MetricTile
                or MetricTileGroup
                or DataTableViewModel;
}
