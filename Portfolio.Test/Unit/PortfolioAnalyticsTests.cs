using FluentAssertions;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

public sealed class PortfolioAnalyticsTests
{
    private static readonly DateOnly Today = On(2026, 1, 1);

    [Fact]
    public void Market_value_rolls_the_last_valuation_forward()
    {
        var item = Item(
            Deposit(On(2024, 1, 1), 1_000),
            Invest(On(2024, 1, 2), 1_000),
            Valuation(On(2024, 6, 30), 1_100),
            Invest(On(2024, 9, 1), 200)
        );

        // The reported 1,100 plus the 200 committed after it.
        item.MarketValueAt(On(2024, 12, 31)).Should().Be(1_300);
    }

    [Fact]
    public void A_valuation_recorded_alongside_a_purchase_is_not_counted_twice()
    {
        var date = On(2024, 6, 30);

        var item = new FinancialItem(
            1,
            1,
            InvestmentType.Grow,
            "Same-day",
            [
                Deposit(On(2024, 1, 1), 1_000),
                new Entry(50, 1, date, investment: 500, marketValue: 1_500),
            ]
        );

        // The 1,500 already includes the 500 bought that day.
        item.MarketValueAt(date).Should().Be(1_500);
    }

    [Fact]
    public void Market_value_falls_back_to_cost_when_nothing_was_ever_valued()
    {
        var item = Item(Deposit(On(2024, 1, 1), 1_000), Invest(On(2024, 1, 2), 600));

        item.MarketValueAt(Today).Should().Be(600);
        item.UnrealisedGain.Should().Be(0, "an unvalued holding has no gain to report");
    }

    /// <remarks>
    /// The regression that motivated rewriting the return calculation: the original fed interest and
    /// fees in as cash flows <i>and</i> counted them in the terminal value, so a holding that earned
    /// 10 % a year reported a small fraction of that.
    /// </remarks>
    [Fact]
    public void Return_counts_income_once_not_twice()
    {
        var item = Item(
            Deposit(On(2024, 1, 1), 1_000),
            Interest(On(2024, 12, 31), 100)
        );

        var xirr = item.XirrFor(DateRange.All, On(2025, 1, 1));

        xirr.Should().NotBeNull();
        xirr!.Value.Should().BeApproximately(0.10, 0.01);
    }

    [Fact]
    public void Return_over_a_later_period_opens_with_the_value_already_there()
    {
        var item = Item(
            Deposit(On(2023, 1, 1), 1_000),
            Interest(On(2023, 12, 31), 500),
            Interest(On(2024, 12, 31), 150)
        );

        // 2024 starts at 1,500 and ends at 1,650: 10 %, not the 65 % the whole life would give.
        var xirr = item.XirrFor(DateRange.Year(2024), On(2025, 6, 1));

        xirr.Should().NotBeNull();
        xirr!.Value.Should().BeApproximately(0.10, 0.015);
    }

    [Fact]
    public void Statistics_separate_period_flows_from_point_in_time_balances()
    {
        var item = Item(
            Deposit(On(2023, 1, 1), 1_000),
            Interest(On(2023, 6, 1), 40),
            Deposit(On(2024, 3, 1), 500),
            Interest(On(2024, 6, 1), 60)
        );

        var stats = item.Describe(DateRange.Year(2024), Today);

        stats.NetDeposits.Should().Be(500, "flows cover the range only");
        stats.LifetimeNetDeposits.Should().Be(1_500, "deposits are cumulative over the whole life");
        stats.TotalBalance.Should().Be(1_600, "a balance is a snapshot, not a period measure");
        stats.Interest.Should().Be(60);
    }

    [Fact]
    public void Statistics_work_on_a_platform_and_on_the_whole_portfolio_alike()
    {
        var a = new FinancialItem(1, 1, InvestmentType.Grow, "A", [Deposit(On(2024, 1, 1), 600, 1)]);
        var b = new FinancialItem(2, 1, InvestmentType.Hold, "B", [Deposit(On(2024, 1, 1), 400, 2)]);
        var platform = new Platform(1, "P", [a, b]);
        var root = new PortfolioRoot([platform]);

        root.Describe(DateRange.All, Today).TotalBalance.Should().Be(1_000);
        platform.Describe(DateRange.All, Today).TotalBalance.Should().Be(1_000);
        a.Describe(DateRange.All, Today, parentBalance: 1_000).ShareOfParent.Should().Be(0.6m);
    }

    [Fact]
    public void Distribution_across_children_adds_up_to_the_parent()
    {
        var a = new FinancialItem(1, 1, InvestmentType.Grow, "A", [Deposit(On(2024, 1, 1), 600, 1)]);
        var b = new FinancialItem(2, 1, InvestmentType.Hold, "B", [Deposit(On(2024, 1, 1), 400, 2)]);
        var platform = new Platform(1, "P", [a, b]);

        var slices = platform.DistributionByChild(PortfolioMetric.TotalBalance, DateRange.All, Today);

        slices.Sum(s => s.Value).Should().Be(platform.TotalBalance);
        slices.First().Label.Should().Be("A", "slices come back largest first");
    }

    [Fact]
    public void Concentration_is_one_when_everything_sits_in_one_place()
    {
        IReadOnlyList<Slice> single = [new Slice("Only", 1_000)];
        IReadOnlyList<Slice> even = [new Slice("A", 500), new Slice("B", 500)];

        PortfolioAnalytics.HerfindahlIndex(single).Should().Be(1);
        PortfolioAnalytics.HerfindahlIndex(even).Should().Be(0.5m);
        PortfolioAnalytics.TopShare(even).Should().Be(0.5m);
    }

    [Fact]
    public void A_running_series_starts_from_what_came_before_the_window()
    {
        var item = Item(
            Deposit(On(2023, 1, 1), 1_000),
            Deposit(On(2024, 2, 1), 200)
        );

        var series = item.BalanceSeries(DateRange.Year(2024), Granularity.Monthly, Today);

        series.Points.Should().NotBeEmpty();
        series.Points[0].Value.Should().Be(1_000, "January 2024 opens with last year's balance");
        series.Points[^1].Value.Should().Be(1_200);
    }

    [Fact]
    public void Drawdown_measures_the_fall_from_the_previous_peak()
    {
        var item = Item(
            Deposit(On(2024, 1, 1), 1_000),
            Invest(On(2024, 1, 2), 1_000),
            Valuation(On(2024, 3, 31), 1_200),
            Valuation(On(2024, 6, 30), 900)
        );

        var drawdown = item.MaxDrawdown(DateRange.All, On(2024, 12, 31));

        drawdown.Should().BeApproximately(0.25m, 0.01m, "900 is 25 % below the 1,200 peak");
    }

    [Fact]
    public void An_empty_scope_reports_nothing_rather_than_throwing()
    {
        var empty = new PortfolioRoot([]);
        var stats = empty.Describe(DateRange.All, Today);

        stats.IsEmpty.Should().BeTrue();
        stats.Xirr.Should().BeNull();
        stats.TotalBalance.Should().Be(0);
        empty.BalanceSeries(DateRange.All, Granularity.Monthly, Today).HasData.Should().BeFalse();
    }

    [Fact]
    public void Grouping_by_investment_type_is_just_another_scope()
    {
        var grow = new FinancialItem(1, 1, InvestmentType.Grow, "A", [Deposit(On(2024, 1, 1), 600, 1)]);
        var hold = new FinancialItem(2, 1, InvestmentType.Hold, "B", [Deposit(On(2024, 1, 1), 400, 2)]);
        var root = new PortfolioRoot([new Platform(1, "P", [grow, hold])]);

        var slices = root.DistributionByInvestmentType(PortfolioMetric.TotalBalance, DateRange.All, Today);

        slices.Should().HaveCount(2);
        slices.Single(s => s.Label == nameof(InvestmentType.Grow)).Value.Should().Be(600);
    }
}
