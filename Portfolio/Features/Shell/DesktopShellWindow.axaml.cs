using MVVM.Core.Views;

namespace Portfolio.Features.Shell;

/// <summary>
/// The desktop window: custom chrome, a navigation rail that becomes a bottom bar when the window is
/// narrow, and the modal region editors open into.
/// </summary>
public partial class DesktopShellWindow : ChromeWindow
{
    public DesktopShellWindow()
    {
        InitializeComponent();
    }
}
