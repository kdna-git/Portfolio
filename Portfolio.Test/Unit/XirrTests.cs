using FluentAssertions;
using Portfolio.Domain.Analytics;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

public sealed class XirrTests
{
    [Fact]
    public void Doubling_your_money_in_a_year_is_a_hundred_percent()
    {
        var result = Xirr.Calculate(
            [new CashFlow(-1_000, On(2024, 1, 1)), new CashFlow(2_000, On(2025, 1, 1))]
        );

        result.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Getting_back_what_you_put_in_is_zero()
    {
        var result = Xirr.Calculate(
            [new CashFlow(-1_000, On(2024, 1, 1)), new CashFlow(1_000, On(2026, 1, 1))]
        );

        result.Should().BeApproximately(0, 1e-4);
    }

    [Fact]
    public void Losing_money_gives_a_negative_rate()
    {
        var result = Xirr.Calculate(
            [new CashFlow(-1_000, On(2024, 1, 1)), new CashFlow(800, On(2025, 1, 1))]
        );

        result.Should().BeApproximately(-0.2, 0.01);
    }

    [Fact]
    public void Matches_the_spreadsheet_on_an_irregular_schedule()
    {
        // The worked example from Excel's XIRR documentation.
        var result = Xirr.Calculate(
            [
                new CashFlow(-10_000, On(2008, 1, 1)),
                new CashFlow(2_750, On(2008, 3, 1)),
                new CashFlow(4_250, On(2008, 10, 30)),
                new CashFlow(3_250, On(2009, 2, 15)),
                new CashFlow(2_750, On(2009, 4, 1)),
            ]
        );

        result.Should().BeApproximately(0.3733, 0.001);
    }

    /// <remarks>
    /// Returning null rather than 0 matters: 0 reads as "it broke even", which is a different and
    /// much more misleading claim than "there is not enough here to say".
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Undefined_when_there_are_too_few_flows(int count)
    {
        var flows = Enumerable
            .Range(0, count)
            .Select(i => new CashFlow(-100, On(2024, 1, 1).AddDays(i)))
            .ToList();

        Xirr.Calculate(flows).Should().BeNull();
    }

    [Fact]
    public void Undefined_when_every_flow_points_the_same_way()
    {
        var result = Xirr.Calculate(
            [new CashFlow(-1_000, On(2024, 1, 1)), new CashFlow(-500, On(2024, 6, 1))]
        );

        result.Should().BeNull("there is no return to compute without money coming back");
    }

    [Fact]
    public void Survives_a_long_irregular_history_without_diverging()
    {
        var flows = new List<CashFlow>();
        var date = On(2015, 1, 1);

        for (var i = 0; i < 120; i++)
        {
            flows.Add(new CashFlow(-250, date));
            date = date.AddMonths(1);
        }

        flows.Add(new CashFlow(45_000, date));

        var result = Xirr.Calculate(flows);

        result.Should().NotBeNull();
        result!.Value.Should().BeInRange(0.02, 0.12);
    }
}
