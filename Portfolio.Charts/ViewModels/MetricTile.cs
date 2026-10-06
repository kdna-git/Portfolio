using CommunityToolkit.Mvvm.ComponentModel;
using Portfolio.Charts.Data;

namespace Portfolio.Charts.ViewModels;

/// <summary>How much visual weight a tile carries.</summary>
public enum MetricEmphasis
{
    /// <summary>The headline of its group: filled background, largest type.</summary>
    Hero,

    /// <summary>A normal tile: outlined, medium type.</summary>
    Standard,

    /// <summary>A supporting figure: label and value on one line, small type.</summary>
    Compact,
}

/// <summary>
/// One number with a label — the right form when a chart would be overkill.
/// </summary>
/// <remarks>
/// A tile knows how to format itself and whether its value is good news, so a screen full of them
/// stays consistent without each view re-deciding. <see cref="Sparkline"/> is optional; when present
/// the tile shows the trend behind the number.
/// </remarks>
public sealed partial class MetricTile : ObservableObject
{
    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial double Value { get; set; }

    [ObservableProperty]
    public partial ValueFormat Format { get; set; }

    [ObservableProperty]
    public partial MetricEmphasis Emphasis { get; set; }

    /// <summary>Longer explanation, shown on hover or long-press.</summary>
    [ObservableProperty]
    public partial string? Description { get; set; }

    /// <summary>Change against the comparison period, as a fraction. Null hides the delta.</summary>
    [ObservableProperty]
    public partial double? Delta { get; set; }

    [ObservableProperty]
    public partial string? DeltaLabel { get; set; }

    /// <summary>
    /// Whether a negative value is bad news. False for flows, where direction is neutral.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSigned { get; set; }

    /// <summary>Shown in place of the value when there is nothing to show.</summary>
    [ObservableProperty]
    public partial bool HasValue { get; set; }

    [ObservableProperty]
    public partial SparklineViewModel? Sparkline { get; set; }

    /// <summary>Geometry for a small leading icon, as SVG path data.</summary>
    [ObservableProperty]
    public partial string? IconData { get; set; }

    public MetricTile()
    {
        Label = string.Empty;
        Format = ValueFormat.Currency;
        Emphasis = MetricEmphasis.Standard;
        HasValue = true;
    }

    public MetricTile(
        string label,
        double value,
        ValueFormat format = ValueFormat.Currency,
        MetricEmphasis emphasis = MetricEmphasis.Standard,
        bool isSigned = false,
        string? description = null,
        string? iconData = null,
        bool hasValue = true
    )
        : this()
    {
        Label = label;
        Value = value;
        Format = format;
        Emphasis = emphasis;
        IsSigned = isSigned;
        Description = description;
        IconData = iconData;
        HasValue = hasValue;
    }

    public string ValueText => HasValue ? ValueFormatter.Format(Value, Format) : "n/a";

    public bool IsHero => Emphasis == MetricEmphasis.Hero;

    public bool IsCompact => Emphasis == MetricEmphasis.Compact;

    public bool IsStandard => Emphasis == MetricEmphasis.Standard;

    /// <summary>
    /// Style-class flags for the view. The sign is also visible in <see cref="ValueText"/>, so the
    /// colour is reinforcement rather than the only signal.
    /// </summary>
    public bool IsPositive => Sentiment > 0;

    public bool IsNegative => Sentiment < 0;

    public bool IsDeltaPositive => DeltaSentiment > 0;

    public bool IsDeltaNegative => DeltaSentiment < 0;

    public string? DeltaText =>
        Delta is null ? null : ValueFormatter.FormatSigned(Delta.Value, ValueFormat.Percent);

    /// <summary>
    /// Positive, negative or neutral — consumed by the view to pick an ink colour, never used as the
    /// only signal: the sign stays visible in the text.
    /// </summary>
    public int Sentiment =>
        !IsSigned || !HasValue ? 0
        : Value > 0 ? 1
        : Value < 0 ? -1
        : 0;

    public int DeltaSentiment =>
        Delta is null ? 0
        : Delta.Value > 0 ? 1
        : Delta.Value < 0 ? -1
        : 0;

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(Sentiment));
        OnPropertyChanged(nameof(IsPositive));
        OnPropertyChanged(nameof(IsNegative));
    }

    partial void OnFormatChanged(ValueFormat value) => OnPropertyChanged(nameof(ValueText));

    partial void OnEmphasisChanged(MetricEmphasis value)
    {
        OnPropertyChanged(nameof(IsHero));
        OnPropertyChanged(nameof(IsCompact));
        OnPropertyChanged(nameof(IsStandard));
    }

    partial void OnHasValueChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(Sentiment));
        OnPropertyChanged(nameof(IsPositive));
        OnPropertyChanged(nameof(IsNegative));
    }

    partial void OnDeltaChanged(double? value)
    {
        OnPropertyChanged(nameof(DeltaText));
        OnPropertyChanged(nameof(DeltaSentiment));
        OnPropertyChanged(nameof(IsDeltaPositive));
        OnPropertyChanged(nameof(IsDeltaNegative));
    }
}

/// <summary>A titled set of tiles: one block of the dashboard.</summary>
public sealed partial class MetricTileGroup : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial MetricTile? Hero { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MetricTile> Tiles { get; set; }

    /// <summary>
    /// How many tiles per row. Set by the host from the viewport width, so the group adapts without
    /// the view knowing anything about breakpoints.
    /// </summary>
    [ObservableProperty]
    public partial int Columns { get; set; }

    public MetricTileGroup()
    {
        Title = string.Empty;
        Tiles = [];
        Columns = 2;
    }

    public MetricTileGroup(string title, MetricTile? hero, IReadOnlyList<MetricTile> tiles)
        : this()
    {
        Title = title;
        Hero = hero;
        Tiles = tiles;
    }
}
