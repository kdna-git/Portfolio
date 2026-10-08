using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;

namespace Portfolio.Charts.Behaviors;

/// <summary>
/// Lets the page keep scrolling when the pointer is over a chart.
/// </summary>
/// <remarks>
/// <para>
/// A chart control consumes the wheel for zooming, so a dashboard of charts becomes a scroll trap:
/// the page stops dead as soon as the pointer crosses a figure. With
/// <c>behaviors:ChartScroll.PassThrough="True"</c> the wheel is handed to the nearest scrollable
/// ancestor instead, and zooming stays available with Ctrl held — the convention people already know
/// from maps and documents.
/// </para>
/// <para>
/// Touch has the same conflict in a different shape. The chart treats a finger like a hovering mouse,
/// so a swipe that starts on a figure drags a tooltip along while the page scrolls; and once the page
/// takes the gesture over, the chart never hears the finger lift, so the tooltip stays stuck. A plain
/// tap, meanwhile, shows nothing at all on a phone: the chart draws its tooltip on movement and drops
/// it on release. So the chart is kept out of touch entirely. Swipes belong to the page, and a tap pins
/// the tooltip for the point under the finger until the same point is tapped again, another chart is
/// tapped, or the page scrolls.
/// </para>
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
                // Tunnelling, so each event is dealt with before the chart's own handlers see it.
                control.RemoveHandler(InputElement.PointerWheelChangedEvent, WheelHandler);
                control.RemoveHandler(InputElement.PointerPressedEvent, PressedHandler);
                control.RemoveHandler(InputElement.PointerMovedEvent, MovedHandler);
                control.RemoveHandler(InputElement.PointerReleasedEvent, ReleasedHandler);

                if (!args.GetNewValue<bool>())
                    return;

                control.AddHandler(InputElement.PointerWheelChangedEvent, WheelHandler, RoutingStrategies.Tunnel);
                control.AddHandler(InputElement.PointerPressedEvent, PressedHandler, RoutingStrategies.Tunnel);
                control.AddHandler(InputElement.PointerMovedEvent, MovedHandler, RoutingStrategies.Tunnel);
                control.AddHandler(InputElement.PointerReleasedEvent, ReleasedHandler, RoutingStrategies.Tunnel);
            }
        );
    }

    #region Wheel

    private static readonly EventHandler<PointerWheelEventArgs> WheelHandler = OnPointerWheelChanged;

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

    #endregion

    #region Touch

    private static readonly EventHandler<PointerPressedEventArgs> PressedHandler = OnPointerPressed;
    private static readonly EventHandler<PointerEventArgs> MovedHandler = OnPointerMoved;
    private static readonly EventHandler<PointerReleasedEventArgs> ReleasedHandler = OnPointerReleased;

    /// <summary>The finger currently on each chart, and the tooltip it has pinned there.</summary>
    private static readonly ConditionalWeakTable<Control, TouchState> States = new();

    /// <summary>The one chart showing a pinned tooltip. Tapping another chart clears it.</summary>
    private static WeakReference<Control>? _pinnedChart;

    // Marking these handled keeps them from the chart. The page's scroll gesture still sees them:
    // Avalonia runs gesture recognisers for handled events too.

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || sender is not Control control)
            return;

        var state = States.GetValue(control, owner => new TouchState(owner));
        state.PointerId = e.Pointer.Id;
        state.Start = e.GetPosition(control);
        state.IsTap = true;

        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || sender is not Control control)
            return;

        // Past the tap slop the finger is scrolling the page, not pointing at a value.
        if (States.TryGetValue(control, out var state) && state.PointerId == e.Pointer.Id && state.IsTap)
        {
            var moved = e.GetPosition(control) - state.Start;
            var slop = TapSlop(control);

            if (Math.Abs(moved.X) > slop.Width / 2 || Math.Abs(moved.Y) > slop.Height / 2)
                state.IsTap = false;
        }

        e.Handled = true;
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || sender is not Control control)
            return;

        e.Handled = true;

        if (!States.TryGetValue(control, out var state) || state.PointerId != e.Pointer.Id)
            return;

        state.PointerId = null;

        if (state.IsTap)
            OnTapped(control, state, e.GetPosition(control));
    }

    private static void OnTapped(Control control, TouchState state, Point position)
    {
        if (control is not IChartView chart || chart.Tooltip is not { } tooltip)
            return;

        var points = chart
            .GetPointsAt(new LvcPointD(position.X, position.Y), FindingStrategy.Automatic, FindPointFor.HoverEvent)
            .ToList();

        // Tapping the value that is already showing, or empty space, puts the tooltip away.
        if (points.Count == 0 || IsSameSelection(points, state.Shown))
        {
            Unpin(control);
            return;
        }

        if (_pinnedChart?.TryGetTarget(out var previous) == true && previous != control)
            Unpin(previous);

        var alreadyPinned = state.Shown is not null;

        tooltip.Show(points, chart.CoreChart);
        state.Shown = points;
        _pinnedChart = new WeakReference<Control>(control);

        // Moving the tooltip to another point on the same chart keeps the watchers it already has.
        if (!alreadyPinned)
            WatchForDismissal(control, state);
    }

    private static void WatchForDismissal(Control control, TouchState state)
    {
        // A tooltip answers "what is this value" for the moment of the tap; once the page moves on,
        // it would only be left pointing at something scrolled half off screen.
        state.Page = control.FindAncestorOfType<ScrollViewer>();

        if (state.Page is not null)
            state.Page.ScrollChanged += state.OnPageScrolled;

        control.DetachedFromVisualTree += OnDetached;
    }

    private static void Unpin(Control control)
    {
        if (!States.TryGetValue(control, out var state) || state.Shown is null)
            return;

        if (control is IChartView chart)
            chart.Tooltip?.Hide(chart.CoreChart);

        state.Shown = null;

        if (state.Page is not null)
            state.Page.ScrollChanged -= state.OnPageScrolled;

        state.Page = null;
        control.DetachedFromVisualTree -= OnDetached;

        if (_pinnedChart?.TryGetTarget(out var pinned) == true && pinned == control)
            _pinnedChart = null;
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            Unpin(control);
    }

    private static bool IsSameSelection(List<ChartPoint> points, List<ChartPoint>? shown) =>
        shown is not null
        && points.Count == shown.Count
        && points.Zip(shown).All(pair =>
            pair.First.Index == pair.Second.Index
            && ReferenceEquals(pair.First.Context.Series, pair.Second.Context.Series)
        );

    /// <summary>How far a finger may wander and still count as a tap, from the platform's own setting.</summary>
    private static Size TapSlop(Control control) =>
        control.GetPlatformSettings()?.GetTapSize(PointerType.Touch) ?? new Size(10, 10);

    private sealed class TouchState(Control owner)
    {
        // Weak, so a page that outlives the chart does not keep it alive through the subscription.
        private readonly WeakReference<Control> _owner = new(owner);

        public int? PointerId;
        public Point Start;
        public bool IsTap;

        /// <summary>The points whose tooltip is pinned; null when none is.</summary>
        public List<ChartPoint>? Shown;

        /// <summary>The scrolling page watched while a tooltip is pinned.</summary>
        public ScrollViewer? Page;

        public void OnPageScrolled(object? sender, ScrollChangedEventArgs e)
        {
            // Layout passes raise this too; only an actual scroll should put the tooltip away.
            if (e.OffsetDelta == default)
                return;

            if (_owner.TryGetTarget(out var control))
                Unpin(control);
        }
    }

    #endregion
}
