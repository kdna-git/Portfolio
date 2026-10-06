using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Portfolio.Charts.Behaviors;

/// <summary>
/// Lets the page keep scrolling when the pointer is over a chart.
/// </summary>
/// <remarks>
/// A chart control consumes the wheel for zooming, so a dashboard of charts becomes a scroll trap:
/// the page stops dead as soon as the pointer crosses a figure. With
/// <c>behaviors:ChartScroll.PassThrough="True"</c> the wheel is handed to the nearest scrollable
/// ancestor instead, and zooming stays available with Ctrl held — the convention people already know
/// from maps and documents.
/// </remarks>
public static class ChartScroll
{
    public static readonly AttachedProperty<bool> PassThroughProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("PassThrough", typeof(ChartScroll));

    public static void SetPassThrough(Control control, bool value) =>
        control.SetValue(PassThroughProperty, value);

    public static bool GetPassThrough(Control control) => control.GetValue(PassThroughProperty);

    static ChartScroll()
    {
        PassThroughProperty.Changed.AddClassHandler<Control>(
            (control, args) =>
            {
                // Tunnelling, so the wheel is redirected before the chart gets a chance to zoom.
                control.RemoveHandler(InputElement.PointerWheelChangedEvent, Handler);

                if (args.GetNewValue<bool>())
                {
                    control.AddHandler(
                        InputElement.PointerWheelChangedEvent,
                        Handler,
                        Avalonia.Interactivity.RoutingStrategies.Tunnel
                    );
                }
            }
        );
    }

    private static readonly EventHandler<PointerWheelEventArgs> Handler = OnPointerWheelChanged;

    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Ctrl means "zoom this chart", which is what the chart would do anyway.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (sender is not Control control)
            return;

        var scrollViewer = control.FindAncestorOfType<ScrollViewer>();

        if (scrollViewer is null)
            return;

        const double step = 60;
        scrollViewer.Offset = new Vector(
            scrollViewer.Offset.X,
            Math.Clamp(
                scrollViewer.Offset.Y - (e.Delta.Y * step),
                0,
                Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height)
            )
        );

        e.Handled = true;
    }
}
