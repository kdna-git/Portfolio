using FluentAssertions;
using Portfolio.Domain.Analytics;
using Portfolio.Domain.Models;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

/// <summary>
/// The accounting identity the whole app rests on, including the write-off correction.
/// </summary>
public sealed class EntryMathTests
{
    [Fact]
    public void Balance_is_deposits_plus_earnings_minus_costs()
    {
        var item = Item(
            Deposit(On(2024, 1, 1), 1_000),
            Interest(On(2024, 2, 1), 50),
            Cost(On(2024, 2, 1), tax: 10, fee: 5)
        );

        item.TotalBalance.Should().Be(1_035);
        item.NetDeposits.Should().Be(1_000);
        item.NetEarnings.Should().Be(35);
    }

    [Fact]
    public void Withdrawals_reduce_both_the_balance_and_net_deposits()
    {
        var item = Item(Deposit(On(2024, 1, 1), 1_000), Withdraw(On(2024, 6, 1), 400));

        item.TotalBalance.Should().Be(600);
        item.NetDeposits.Should().Be(600);
        item.TotalWithdrawals.Should().Be(400);
    }

    /// <remarks>
    /// The original model subtracted a write-off from invested capital only, which left the balance
    /// untouched and silently reclassified the lost money as cash. Money that defaulted is gone.
    /// </remarks>
    [Fact]
    public void A_write_off_reduces_the_balance_and_leaves_cash_alone()
    {
        var item = Item(
            Deposit(On(2024, 1, 1), 1_000),
            Invest(On(2024, 1, 2), 800),
            WriteOff(On(2024, 5, 1), 300)
        );

        item.TotalBalance.Should().Be(700, "the written-off capital no longer exists");
        item.Invested.Should().Be(500, "the defaulted part is no longer committed");
        item.Cash.Should().Be(200, "cash is untouched by a default");
        item.NetEarnings.Should().Be(-300);
    }

    [Fact]
    public void Cash_is_the_balance_not_committed_to_the_asset()
    {
        var item = Item(Deposit(On(2024, 1, 1), 1_000), Invest(On(2024, 1, 2), 750));

        item.Cash.Should().Be(250);
        item.Invested.Should().Be(750);
    }

    [Fact]
    public void Capital_freedom_is_zero_when_there_is_no_balance()
    {
        var empty = Item();

        // The original divided by the balance unguarded, which threw on an untouched holding.
        empty.CapitalFreedom.Should().Be(0);
    }

    [Fact]
    public void Capital_freedom_is_the_share_of_the_balance_that_was_not_deposited()
    {
        var item = Item(Deposit(On(2024, 1, 1), 800), Interest(On(2024, 6, 1), 200));

        item.TotalBalance.Should().Be(1_000);
        item.CapitalFreedom.Should().Be(0.2m);
    }

    [Fact]
    public void A_platform_total_equals_the_sum_of_its_holdings()
    {
        var first = new FinancialItem(1, 1, InvestmentType.Grow, "A", [Deposit(On(2024, 1, 1), 500, 1)]);
        var second = new FinancialItem(2, 1, InvestmentType.Hold, "B", [Deposit(On(2024, 1, 1), 300, 2)]);
        var platform = new Platform(1, "P", [first, second]);

        platform.TotalBalance.Should().Be(first.TotalBalance + second.TotalBalance).And.Be(800);
    }

    [Fact]
    public void Entries_are_kept_in_date_order_whatever_order_they_arrive_in()
    {
        var item = Item(
            Deposit(On(2024, 6, 1), 100),
            Deposit(On(2024, 1, 1), 100),
            Deposit(On(2024, 3, 1), 100)
        );

        item.Entries.Select(e => e.Date).Should().BeInAscendingOrder();
    }
}
