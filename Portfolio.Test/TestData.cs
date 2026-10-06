using Portfolio.Domain.Models;

namespace Portfolio.Test;

/// <summary>Small helpers so a test reads as the scenario it describes.</summary>
internal static class TestData
{
    private static long _nextId;

    public static DateOnly On(int year, int month, int day) => new(year, month, day);

    public static Entry Deposit(DateOnly date, decimal amount, long itemId = 1) =>
        new(++_nextId, itemId, date, deposit: amount);

    public static Entry Withdraw(DateOnly date, decimal amount, long itemId = 1) =>
        new(++_nextId, itemId, date, withdrawal: amount);

    public static Entry Invest(DateOnly date, decimal amount, long itemId = 1) =>
        new(++_nextId, itemId, date, investment: amount);

    public static Entry Interest(DateOnly date, decimal amount, long itemId = 1) =>
        new(++_nextId, itemId, date, interest: amount);

    public static Entry Cost(DateOnly date, decimal tax = 0, decimal fee = 0, long itemId = 1) =>
        new(++_nextId, itemId, date, tax: tax, fee: fee);

    public static Entry WriteOff(DateOnly date, decimal amount, long itemId = 1) =>
        new(++_nextId, itemId, date, defaultLoss: amount);

    public static Entry Valuation(DateOnly date, decimal value, long itemId = 1) =>
        new(++_nextId, itemId, date, marketValue: value);

    public static FinancialItem Item(
        params Entry[] entries
    ) => new(1, 1, InvestmentType.Grow, "Test holding", entries);

    public static Platform Platform(params FinancialItem[] items) =>
        new(1, "Test platform", items);
}
