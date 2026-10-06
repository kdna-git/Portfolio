using Portfolio.Domain.Models;

namespace Portfolio.Domain.Analytics;

/// <summary>
/// The single implementation of every balance figure. Models and statistics both call in here, so
/// a platform total can never drift from the sum of its items.
/// </summary>
/// <remarks>
/// The accounting identity the whole app relies on:
/// <code>
/// TotalBalance = NetDeposits + NetEarnings
/// NetEarnings  = GrossIncome - Costs - Losses
/// Cash         = TotalBalance - InvestedBook
/// </code>
/// A default loss therefore reduces the balance and the invested capital by the same amount, which
/// leaves cash untouched — money written off is gone rather than silently turned into cash.
/// </remarks>
public static class EntryMath
{
    public static decimal TotalDeposits(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.Deposit);

    public static decimal TotalWithdrawals(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.Withdrawal);

    public static decimal NetDeposits(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.Deposit - e.Withdrawal);

    public static decimal TotalInterest(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.Interest);

    public static decimal TotalBonuses(this IEnumerable<Entry> entries) => entries.Sum(e => e.Bonus);

    public static decimal GrossIncome(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.GrossIncome);

    public static decimal TotalTaxes(this IEnumerable<Entry> entries) => entries.Sum(e => e.Tax);

    public static decimal TotalFees(this IEnumerable<Entry> entries) => entries.Sum(e => e.Fee);

    public static decimal TotalCosts(this IEnumerable<Entry> entries) => entries.Sum(e => e.Costs);

    public static decimal TotalLosses(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.DefaultLoss);

    public static decimal NetEarnings(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.NetEarnings);

    /// <summary>Book balance: what you put in, plus what it earned, minus what it cost.</summary>
    public static decimal TotalBalance(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.BalanceChange);

    /// <summary>Capital currently committed to assets, at cost.</summary>
    public static decimal InvestedBook(this IEnumerable<Entry> entries) =>
        entries.Sum(e => e.InvestedChange);

    /// <summary>Uncommitted balance sitting on the platform.</summary>
    public static decimal Cash(this IEnumerable<Entry> entries)
    {
        decimal balance = 0;
        decimal invested = 0;

        foreach (var entry in entries)
        {
            balance += entry.BalanceChange;
            invested += entry.InvestedChange;
        }

        return balance - invested;
    }

    /// <summary>
    /// Share of the balance that was not funded by you. 1 means everything is earnings, 0 means it
    /// is all deposits, negative means the balance is below what you paid in.
    /// </summary>
    public static decimal CapitalFreedom(decimal totalBalance, decimal netDeposits) =>
        totalBalance == 0 ? 0 : 1 - (netDeposits / totalBalance);

    /// <summary>Costs as a share of gross income. 0 when nothing was earned.</summary>
    public static decimal CostRatio(decimal grossIncome, decimal totalCosts) =>
        grossIncome == 0 ? 0 : totalCosts / grossIncome;

    public static IEnumerable<Entry> Within(
        this IEnumerable<Entry> entries,
        DateOnly? from,
        DateOnly? to
    ) => entries.Where(e => (from is null || e.Date >= from) && (to is null || e.Date <= to));

    public static IEnumerable<Entry> UpTo(this IEnumerable<Entry> entries, DateOnly date) =>
        entries.Where(e => e.Date <= date);

    /// <summary>Chronological order with a stable tie-break, so running totals are reproducible.</summary>
    public static IOrderedEnumerable<Entry> InOrder(this IEnumerable<Entry> entries) =>
        entries.OrderBy(e => e.Date).ThenBy(e => e.Id);
}
