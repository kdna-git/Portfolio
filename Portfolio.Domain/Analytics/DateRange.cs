namespace Portfolio.Domain.Analytics;

/// <summary>
/// An inclusive, open-ended date window. Both bounds are optional so "everything" and "since
/// January" are the same type as "2024".
/// </summary>
public readonly record struct DateRange(DateOnly? From, DateOnly? To)
{
    public static DateRange All => new(null, null);

    public static DateRange Year(int year) => new(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    public static DateRange YearToDate(DateOnly today) => new(new DateOnly(today.Year, 1, 1), today);

    public static DateRange LastMonths(int months, DateOnly today) =>
        new(today.AddMonths(-months).AddDays(1), today);

    public static DateRange LastYears(int years, DateOnly today) =>
        new(today.AddYears(-years).AddDays(1), today);

    public static DateRange UpTo(DateOnly to) => new(null, to);

    public bool IsAll => From is null && To is null;

    public bool Contains(DateOnly date) =>
        (From is null || date >= From) && (To is null || date <= To);

    /// <summary>The first day actually covered, given the earliest data available.</summary>
    public DateOnly EffectiveFrom(DateOnly earliest) =>
        From is null ? earliest : (From.Value > earliest ? From.Value : earliest);

    /// <summary>The last day actually covered, given today's date.</summary>
    public DateOnly EffectiveTo(DateOnly today) =>
        To is null ? today : (To.Value < today ? To.Value : today);

    public DateRange Intersect(DateRange other) =>
        new(
            From is null ? other.From : (other.From is null ? From : Max(From.Value, other.From.Value)),
            To is null ? other.To : (other.To is null ? To : Min(To.Value, other.To.Value))
        );

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    public override string ToString() =>
        (From, To) switch
        {
            (null, null) => "All time",
            (not null, null) => $"From {From:yyyy-MM-dd}",
            (null, not null) => $"Until {To:yyyy-MM-dd}",
            _ when From!.Value.Month == 1
                && From.Value.Day == 1
                && To!.Value.Month == 12
                && To.Value.Day == 31
                && From.Value.Year == To.Value.Year => From.Value.Year.ToString(),
            _ => $"{From:yyyy-MM-dd} – {To:yyyy-MM-dd}",
        };
}
