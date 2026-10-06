using Avalonia.Controls;
using Avalonia.Controls.Platform;
using MVVM.Core.ViewModels;

namespace Portfolio.Features.Shell;

/// <summary>
/// The shell for every platform without its own window chrome: Android, iOS and the browser.
/// </summary>
/// <remarks>
/// Also the one place that reads the platform's safe-area insets — the strip under a display cutout,
/// a home-gesture bar, a rounded corner — and pushes them into the layout store. Reading them here
/// rather than in each head means one implementation covers Android, iOS and a browser running
/// full-screen on a notched phone.
/// </remarks>
public partial class SingleViewShell : UserControl
{
    private IInsetsManager? _insets;

    public SingleViewShell()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);
        _insets = topLevel?.InsetsManager;

        if (_insets is null)
            return;

        _insets.SafeAreaChanged += OnSafeAreaChanged;
        ApplySafeArea(_insets.SafeAreaPadding);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
            _insets = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) =>
        ApplySafeArea(e.SafeAreaPadding);

    private void ApplySafeArea(Avalonia.Thickness padding)
    {
        if (DataContext is MainViewModel shell)
        {
            // The bottom inset is left to the navigation bar, which sits below the content and pads
            // itself; applying it here as well would leave a gap under the bar.
            shell.Layout.SafeAreaPadding = new Avalonia.Thickness(
                padding.Left,
                padding.Top,
                padding.Right,
                0
            );
        }
    }
}
