using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace ModalControl;

/// <summary>
/// Overlay host that dims the window and centres a single piece of content on top of it.
/// Dismissing happens through <see cref="BackgroundClickCommand"/>, raised when the dimmed
/// background (never the content itself) is clicked or when Escape is pressed.
/// </summary>
public class Modal : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<
        Modal,
        bool
    >(nameof(IsOpen), defaultValue: false);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ContentBackgroundProperty =
        AvaloniaProperty.Register<Modal, IBrush?>(
            nameof(ContentBackground),
            defaultValue: Brushes.White
        );

    public IBrush? ContentBackground
    {
        get => GetValue(ContentBackgroundProperty);
        set => SetValue(ContentBackgroundProperty, value);
    }

    public static readonly StyledProperty<ICommand?> BackgroundClickCommandProperty =
        AvaloniaProperty.Register<Modal, ICommand?>(nameof(BackgroundClickCommand));

    public ICommand? BackgroundClickCommand
    {
        get => GetValue(BackgroundClickCommandProperty);
        set => SetValue(BackgroundClickCommandProperty, value);
    }

    public static readonly StyledProperty<CornerRadius> ContentCornerRadiusProperty =
        AvaloniaProperty.Register<Modal, CornerRadius>(
            nameof(ContentCornerRadius),
            defaultValue: new CornerRadius(5)
        );

    public CornerRadius ContentCornerRadius
    {
        get => GetValue(ContentCornerRadiusProperty);
        set => SetValue(ContentCornerRadiusProperty, value);
    }

    /// <summary>
    /// Largest fraction of the host width/height the content may occupy. Keeps modals usable on
    /// phone screens, where an unconstrained form would overflow the viewport.
    /// </summary>
    public static readonly StyledProperty<double> ContentMaxWidthRatioProperty =
        AvaloniaProperty.Register<Modal, double>(
            nameof(ContentMaxWidthRatio),
            defaultValue: 0.94
        );

    public double ContentMaxWidthRatio
    {
        get => GetValue(ContentMaxWidthRatioProperty);
        set => SetValue(ContentMaxWidthRatioProperty, value);
    }

    public static readonly StyledProperty<double> ContentMaxHeightRatioProperty =
        AvaloniaProperty.Register<Modal, double>(
            nameof(ContentMaxHeightRatio),
            defaultValue: 0.92
        );

    public double ContentMaxHeightRatio
    {
        get => GetValue(ContentMaxHeightRatioProperty);
        set => SetValue(ContentMaxHeightRatioProperty, value);
    }

    private Border? _modalContent;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var backgroundGrid = e.NameScope.Find<Grid>("PART_BackgroundGrid");
        _modalContent = e.NameScope.Find<Border>("PART_ModalContent");

        if (backgroundGrid is not null)
        {
            backgroundGrid.PointerPressed += OnBackgroundPointerPressed;
        }

        if (_modalContent is not null)
        {
            // Clicks inside the content must never reach the dismiss handler above.
            _modalContent.PointerPressed += static (_, args) => args.Handled = true;
        }

        UpdateContentConstraints(Bounds.Size);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateContentConstraints(e.NewSize);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsOpen && e.Key == Key.Escape)
        {
            Dismiss();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void UpdateContentConstraints(Size size)
    {
        if (_modalContent is null || size.Width <= 0 || size.Height <= 0)
            return;

        _modalContent.MaxWidth = size.Width * ContentMaxWidthRatio;
        _modalContent.MaxHeight = size.Height * ContentMaxHeightRatio;
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!ReferenceEquals(args.Source, sender))
            return;

        Dismiss();
    }

    private void Dismiss()
    {
        if (BackgroundClickCommand?.CanExecute(this) == true)
            BackgroundClickCommand.Execute(this);
    }
}
