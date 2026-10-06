using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Portfolio.Charts.ViewModels;

namespace Portfolio.Charts.Converters;

/// <summary>Converters the chart views need, as singletons for use with <c>{x:Static}</c>.</summary>
public static class ChartConverters
{
    /// <summary>
    /// -1, 0 or 1 to a brush. Colour is never the only signal: the sign stays in the text next to it.
    /// </summary>
    public static readonly IValueConverter SentimentToBrush = new FuncValueConverter<int, IBrush?>(
        sentiment =>
            sentiment switch
            {
                > 0 => Brush.Parse(Theming.ChartPalette.Positive),
                < 0 => Brush.Parse(Theming.ChartPalette.Negative),
                _ => null,
            }
    );

    /// <summary>A hex string from the palette to a brush, for legend swatches and table rows.</summary>
    public static readonly IValueConverter HexToBrush = new FuncValueConverter<string?, IBrush?>(
        hex => string.IsNullOrEmpty(hex) ? null : Brush.Parse(hex)
    );

    /// <summary>Bolder type for a pinned (total) table row.</summary>
    public static readonly IValueConverter PinnedToWeight = new FuncValueConverter<bool, FontWeight>(
        pinned => pinned ? FontWeight.SemiBold : FontWeight.Normal
    );

    /// <summary>A fraction as a percentage string, for inline shares.</summary>
    public static readonly IValueConverter FractionToPercent = new FuncValueConverter<
        double?,
        string
    >(value => value is null ? string.Empty : value.Value.ToString("P1", CultureInfo.CurrentCulture));
}
