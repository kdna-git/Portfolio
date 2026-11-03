using FluentAssertions;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

public sealed class ProjectionTests
{
    private static readonly DateOnly Start = On(2026, 1, 1);

    [Fact]
    public void Without_volatility_every_scenario_is_plain_compounding()
    {
        var assumptions = new ProjectionAssumptions(0.01, 0, 0, 12);

        var last = Projection.Project(1_000, Start, assumptions)[^1];

        var expected = 1_000 * Math.Pow(1.01, 12);
        ((double)last.Median).Should().BeApproximately(expected, 0.01);
        last.Low.Should().Be(last.Median);
        last.High.Should().Be(last.Median);
    }

    [Fact]
    public void Without_return_contributions_simply_add_up()
    {
        var assumptions = new ProjectionAssumptions(0, 0, 100, 24);

        Projection.Project(1_000, Start, assumptions)[^1].Median.Should().Be(3_400);
    }

    [Fact]
    public void The_projection_starts_at_today_s_value()
    {
        var points = Projection.Project(1_000, Start, new ProjectionAssumptions(0.01, 0.02, 50, 6));

        points.Should().HaveCount(7, "the starting point plus one per month");
        points[0].Should().Be(new ProjectionPoint(Start, 1_000, 1_000, 1_000));
        points[^1].Date.Should().Be(On(2026, 7, 1));
    }

    [Fact]
    public void The_band_fans_out_over_time()
    {
        var points = Projection.Project(10_000, Start, new ProjectionAssumptions(0.005, 0.03, 0, 120));

        var widths = points.Skip(1).Select(p => p.High - p.Low).ToList();

        widths.Should().BeInAscendingOrder("uncertainty compounds with time");
        points.Skip(1).Should().OnlyContain(p => p.Low < p.Median && p.Median < p.High);
    }

    [Fact]
    public void A_wider_band_puts_the_bad_case_lower()
    {
        var narrow = new ProjectionAssumptions(0.005, 0.03, 0, 60, Projection.P10P90);
        var wide = narrow with { BandWidth = Projection.P5P95 };

        Projection.Project(10_000, Start, wide)[^1].Low
            .Should().BeLessThan(Projection.Project(10_000, Start, narrow)[^1].Low);
    }

    [Fact]
    public void Withdrawing_more_than_it_holds_runs_out_rather_than_going_negative()
    {
        var points = Projection.Project(1_000, Start, new ProjectionAssumptions(0, 0, -200, 12));

        points[^1].Median.Should().Be(0);
    }

    [Fact]
    public void A_goal_is_found_past_the_horizon()
    {
        var assumptions = new ProjectionAssumptions(0, 0, 100, 12);

        // 1,000 plus 100 a month reaches 5,000 after 40 months, well past a 12-month chart.
        Projection.GoalReached(1_000, Start, assumptions, 5_000).Should().Be(Start.AddMonths(40));
        Projection.GoalReached(1_000, Start, assumptions with { MonthlyContribution = 0 }, 5_000)
            .Should().BeNull("it never grows");
        Projection.GoalReached(6_000, Start, assumptions, 5_000).Should().Be(Start, "it is already there");
    }

    [Fact]
    public void A_steady_one_percent_a_month_is_measured_as_such()
    {
        var entries = new List<Entry> { Deposit(On(2023, 12, 31), 1_000) };
        var balance = 1_000m;

        for (var month = On(2024, 1, 1); month < On(2026, 1, 1); month = month.AddMonths(1))
        {
            var interest = balance * 0.01m;
            entries.Add(Interest(month.AddMonths(1).AddDays(-1), interest));
            balance += interest;
        }

        var basis = Item([.. entries]).Basis(Start);

        basis.Months.Should().Be(24);
        basis.MonthlyReturn.Should().BeApproximately(0.01, 1e-9);
        basis.MonthlyVolatility.Should().BeApproximately(0, 1e-9);
        basis.IsReliable.Should().BeTrue();
    }

    [Fact]
    public void Deposits_are_not_mistaken_for_returns()
    {
        var entries = new List<Entry>();

        for (var month = On(2025, 1, 1); month < On(2026, 1, 1); month = month.AddMonths(1))
            entries.Add(Deposit(month, 500));

        var basis = Item([.. entries]).Basis(Start);

        basis.MonthlyReturn.Should().BeApproximately(0, 1e-9, "paying in is not performance");
        basis.MonthlyContribution.Should().Be(500);
    }

    [Fact]
    public void Contributions_average_over_the_life_of_a_young_holding()
    {
        var basis = Item(Deposit(On(2025, 11, 5), 300), Deposit(On(2025, 12, 5), 300)).Basis(Start);

        basis.MonthlyContribution.Should().Be(300, "two months old, so two months to average over");
        basis.IsReliable.Should().BeFalse();
    }

    [Fact]
    public void Annual_and_monthly_figures_round_trip()
    {
        Projection.ToMonthlyReturn(Projection.ToAnnualReturn(0.004)).Should().BeApproximately(0.004, 1e-12);
        Projection.ToMonthlyVolatility(Projection.ToAnnualVolatility(0.02)).Should().BeApproximately(0.02, 1e-12);
    }
}
