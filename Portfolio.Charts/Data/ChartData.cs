namespace Portfolio.Charts.Data;

/// <summary>A dated value for a time series.</summary>
public readonly record struct TimeValue(DateTime Date, double Value);

/// <summary>A labelled value for a bar, a slice or a table cell.</summary>
public readonly record struct LabeledValue(string Label, double Value, string? Key = null);

/// <summary>
/// One series of dated values, plus how it should look. The chart decides nothing about the data
/// and the data says nothing about the chart beyond this.
/// </summary>
public sealed record TimeSeries(
    string Name,
    IReadOnlyList<TimeValue> Values,
    SeriesStyle? Style = null
)
{
    public bool HasData => Values.Count > 0;
}

/// <summary>One series of labelled values, aligned to a shared label axis.</summary>
public sealed record CategorySeries(
    string Name,
    IReadOnlyList<double> Values,
    SeriesStyle? Style = null
)
{
    public bool HasData => Values.Count > 0;
}

/// <summary>How a series is drawn.</summary>
public sealed record SeriesStyle
{
    /// <summary>Shape of the series. Charts fall back to a sensible default per chart kind.</summary>
    public SeriesShape Shape { get; init; } = SeriesShape.Line;

    /// <summary>
    /// Explicit colour as <c>#RRGGBB</c> or <c>#AARRGGBB</c>. Null means "take the next palette
    /// colour", which is what keeps a dashboard's many charts looking like one system.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>Fill the area under a line.</summary>
    public bool Filled { get; init; }

    /// <summary>Draw a dot at each value.</summary>
    public bool ShowPoints { get; init; }

    public double StrokeThickness { get; init; } = 2;

    /// <summary>Dash the stroke, for reference or target lines.</summary>
    public bool Dashed { get; init; }

    /// <summary>
    /// Colour bars by sign instead of by series: green up, red down. For net flows, where the
    /// direction matters more than which series it belongs to.
    /// </summary>
    public bool ColorBySign { get; init; }

    public static SeriesStyle Line(string? color = null) => new() { Shape = SeriesShape.Line, Color = color };

    public static SeriesStyle Area(string? color = null) =>
        new()
        {
            Shape = SeriesShape.Line,
            Color = color,
            Filled = true,
        };

    public static SeriesStyle Reference(string? color = null) =>
        new()
        {
            Shape = SeriesShape.Line,
            Color = color,
            Dashed = true,
            StrokeThickness = 1.5,
        };

    public static SeriesStyle Column(string? color = null) =>
        new() { Shape = SeriesShape.Column, Color = color };

    public static SeriesStyle SignedColumn() => new() { Shape = SeriesShape.Column, ColorBySign = true };
}

public enum SeriesShape
{
    Line,
    StepLine,
    Column,
    Scatter,
}

/// <summary>How bars from several series share the axis.</summary>
public enum BarLayout
{
    /// <summary>Side by side.</summary>
    Grouped,

    /// <summary>On top of each other.</summary>
    Stacked,
}

/// <summary>Which way bars run.</summary>
public enum BarOrientation
{
    Vertical,
    Horizontal,
}
