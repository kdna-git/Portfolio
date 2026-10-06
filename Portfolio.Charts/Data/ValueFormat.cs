using System.Globalization;

namespace Portfolio.Charts.Data;

/// <summary>How a number is written out on an axis, a tooltip or a tile.</summary>
public enum ValueFormat
{
    Currency,

    /// <summary>Currency without decimals and with a k/M suffix, for axis labels.</summary>
    CompactCurrency,
    Percent,
    Number,
    Integer,
}

public static class ValueFormatter
{
    /// <summary>
    /// Formats a value for display. Percentages take a fraction (0.0825 prints as 8.25 %), which
    /// matches what rate calculations produce.
    /// </summary>
    public static string Format(double value, ValueFormat format, CultureInfo? culture = null)
    {
        var c = culture ?? CultureInfo.CurrentCulture;

        return format switch
        {
            ValueFormat.Currency => value.ToString("C", c),
            ValueFormat.CompactCurrency => Compact(value, c),
            ValueFormat.Percent => value.ToString("P2", c),
            ValueFormat.Integer => value.ToString("N0", c),
            _ => value.ToString("N2", c),
        };
    }

    public static string Format(decimal value, ValueFormat format, CultureInfo? culture = null) =>
        Format((double)value, format, culture);

    /// <summary>
    /// Thousands as k, millions as M. Axis labels are the one place where losing precision is the
    /// right trade: six-figure tick labels collide long before they inform.
    /// </summary>
    public static string Compact(double value, CultureInfo? culture = null)
    {
        var c = culture ?? CultureInfo.CurrentCulture;
        var symbol = c.NumberFormat.CurrencySymbol;
        var sign = value < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(value);

        var text = magnitude switch
        {
            >= 1_000_000_000 => (magnitude / 1_000_000_000).ToString("0.##", c) + "B",
            >= 1_000_000 => (magnitude / 1_000_000).ToString("0.##", c) + "M",
            >= 1_000 => (magnitude / 1_000).ToString("0.#", c) + "k",
            _ => magnitude.ToString("0.##", c),
        };

        return c.NumberFormat.CurrencyPositivePattern switch
        {
            // 0: "$n" and 2: "$ n" put the symbol first.
            0 or 2 => $"{sign}{symbol}{text}",
            _ => $"{sign}{text} {symbol}",
        };
    }

    /// <summary>A signed string with an explicit plus, for deltas.</summary>
    public static string FormatSigned(double value, ValueFormat format, CultureInfo? culture = null)
    {
        var text = Format(Math.Abs(value), format, culture);
        return value switch
        {
            > 0 => "+" + text,
            < 0 => "-" + text,
            _ => text,
        };
    }
}
