using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Portfolio.Charts.Controls;

/// <summary>
/// The frame every figure sits in: a title, an optional subtitle, a slot for controls on the right,
/// the figure itself and an optional footer.
/// </summary>
/// <remarks>
/// Having one card means a dashboard's figures line up without each view re-deciding padding, corner
/// radius and heading size — and it is plain Avalonia, so it works for a chart, a table or anything
/// else you drop in <see cref="ContentControl.Content"/>.
/// </remarks>
public class ChartCard : ContentControl
{
    public static readonly StyledProperty<string?> HeaderProperty = AvaloniaProperty.Register<
        ChartCard,
        string?
    >(nameof(Header));

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<string?> SubheaderProperty = AvaloniaProperty.Register<
        ChartCard,
        string?
    >(nameof(Subheader));

    public string? Subheader
    {
        get => GetValue(SubheaderProperty);
        set => SetValue(SubheaderProperty, value);
    }

    /// <summary>Controls shown on the heading row: a period picker, a view toggle, a menu.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<
        ChartCard,
        object?
    >(nameof(Actions));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Notes under the figure: a caption, an exclusion note, a source.</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<
        ChartCard,
        object?
    >(nameof(Footer));

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>Height of the content area. Bind to the layout store so phones get shorter figures.</summary>
    public static readonly StyledProperty<double> ContentHeightProperty =
        AvaloniaProperty.Register<ChartCard, double>(nameof(ContentHeight), double.NaN);

    public double ContentHeight
    {
        get => GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ChartCard);
}
