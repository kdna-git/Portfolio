using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace MVVM.Core.Views;

/// <summary>
/// A borderless window that still behaves like one: drag by a designated header control, resize
/// from the edges, and square off the corners when maximised.
/// </summary>
/// <remarks>
/// Derive a window from this and give it a header element named <c>PART_Header</c>. Everything
/// else (cursor feedback, edge detection, the maximised padding dance on Windows) is handled here,
/// so the XAML stays about layout.
/// </remarks>
public abstract class ChromeWindow : Window
{
    private const double ResizeBorderThickness = 6;

    /// <summary>Corner radius applied to the outer chrome while the window is not maximised.</summary>
    public static readonly StyledProperty<CornerRadius> ChromeCornerRadiusProperty =
        AvaloniaProperty.Register<ChromeWindow, CornerRadius>(
            nameof(ChromeCornerRadius),
            new CornerRadius(12)
        );

    public CornerRadius ChromeCornerRadius
    {
        get => GetValue(ChromeCornerRadiusProperty);
        set => SetValue(ChromeCornerRadiusProperty, value);
    }

    /// <summary>
    /// Zero when maximised, <see cref="ChromeCornerRadius"/> otherwise. Bind the shell's outer
    /// border to this instead of recomputing corners in code-behind.
    /// </summary>
    public static readonly DirectProperty<ChromeWindow, CornerRadius> EffectiveCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<ChromeWindow, CornerRadius>(
            nameof(EffectiveCornerRadius),
            owner => owner.EffectiveCornerRadius
        );

    public CornerRadius EffectiveCornerRadius =>
        WindowState == WindowState.Maximized ? default : ChromeCornerRadius;

    protected ChromeWindow()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property != WindowStateProperty)
                return;

            // Windows draws a maximised borderless window slightly oversized; the padding keeps
            // the drop shadow visible when floating and disappears when maximised.
            if (OperatingSystem.IsWindows())
                Padding = WindowState == WindowState.Maximized ? default : new Thickness(8);

            RaisePropertyChanged(
                EffectiveCornerRadiusProperty,
                default,
                EffectiveCornerRadius
            );
        };
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        AttachHeader();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AttachHeader();
    }

    private Control? _header;

    private void AttachHeader()
    {
        var header = this.FindControl<Control>("PART_Header");
        if (header is null || ReferenceEquals(header, _header))
            return;

        _header = header;
        header.PointerPressed += OnHeaderPointerPressed;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (WindowState == WindowState.Maximized || !CanResize)
            return;

        if (GetEdge(e.GetPosition(this)) is { } edge)
            BeginResizeDrag(edge, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (WindowState == WindowState.Maximized || !CanResize)
            return;

        Cursor = GetEdge(e.GetPosition(this)) switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(
                StandardCursorType.BottomRightCorner
            ),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(
                StandardCursorType.BottomLeftCorner
            ),
            _ => new Cursor(StandardCursorType.Arrow),
        };
    }

    private WindowEdge? GetEdge(Point p)
    {
        var left = p.X <= ResizeBorderThickness;
        var right = p.X >= Bounds.Width - ResizeBorderThickness;
        var top = p.Y <= ResizeBorderThickness;
        var bottom = p.Y >= Bounds.Height - ResizeBorderThickness;

        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null,
        };
    }
}
