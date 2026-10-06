using System.Globalization;

namespace Portfolio.Domain.Analytics;

/// <summary>Bucket size for a time series.</summary>
public enum Granularity
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
}

public static class GranularityExtensions
{
    /// <summary>The first day of the bucket a date falls into.</summary>
    public static DateOnly BucketStart(this Granularity granularity, DateOnly date) =>
        granularity switch
        {
            Granularity.Daily => date,
            Granularity.Weekly => date.AddDays(
                -(((int)date.DayOfWeek + 6) % 7) // ISO weeks start on Monday
            ),
            Granularity.Monthly => new DateOnly(date.Year, date.Month, 1),
            Granularity.Quarterly => new DateOnly(date.Year, (((date.Month - 1) / 3) * 3) + 1, 1),
            _ => new DateOnly(date.Year, 1, 1),
        };

    /// <summary>The last day of the bucket a date falls into.</summary>
    public static DateOnly BucketEnd(this Granularity granularity, DateOnly date)
    {
        var start = granularity.BucketStart(date);

        return granularity switch
        {
            Granularity.Daily => start,
            Granularity.Weekly => start.AddDays(6),
            Granularity.Monthly => start.AddMonths(1).AddDays(-1),
            Granularity.Quarterly => start.AddMonths(3).AddDays(-1),
            _ => start.AddYears(1).AddDays(-1),
        };
    }

    public static DateOnly Next(this Granularity granularity, DateOnly bucketStart) =>
        granularity switch
        {
            Granularity.Daily => bucketStart.AddDays(1),
            Granularity.Weekly => bucketStart.AddDays(7),
            Granularity.Monthly => bucketStart.AddMonths(1),
            Granularity.Quarterly => bucketStart.AddMonths(3),
            _ => bucketStart.AddYears(1),
        };

    public static string Label(this Granularity granularity, DateOnly bucketStart) =>
        granularity switch
        {
            Granularity.Daily => bucketStart.ToString("dd MMM yy", CultureInfo.CurrentCulture),
            Granularity.Weekly => $"W{ISOWeek.GetWeekOfYear(bucketStart.ToDateTime(TimeOnly.MinValue))} {bucketStart:yy}",
            Granularity.Monthly => bucketStart.ToString("MMM yy", CultureInfo.CurrentCulture),
            Granularity.Quarterly => $"Q{((bucketStart.Month - 1) / 3) + 1} {bucketStart:yyyy}",
            _ => bucketStart.ToString("yyyy", CultureInfo.CurrentCulture),
        };

    /// <summary>
    /// A bucket size that keeps a span readable: roughly 12–60 points rather than one per day over
    /// five years.
    /// </summary>
    public static Granularity SuggestFor(DateOnly from, DateOnly to)
    {
        var days = Math.Max(1, to.DayNumber - from.DayNumber);

        return days switch
        {
            <= 62 => Granularity.Daily,
            <= 400 => Granularity.Weekly,
            <= 1500 => Granularity.Monthly,
            <= 4000 => Granularity.Quarterly,
            _ => Granularity.Yearly,
        };
    }

    /// <summary>Every bucket start from one date to another, inclusive.</summary>
    public static IEnumerable<DateOnly> Buckets(
        this Granularity granularity,
        DateOnly from,
        DateOnly to
    )
    {
        for (
            var bucket = granularity.BucketStart(from);
            bucket <= to;
            bucket = granularity.Next(bucket)
        )
        {
            yield return bucket;
        }
    }
}
