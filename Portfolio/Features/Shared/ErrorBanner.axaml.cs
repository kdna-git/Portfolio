using Avalonia.Controls;

namespace Portfolio.Features.Shared;

/// <summary>Dismissable banner for a failed write. Bound to any <see cref="ScreenViewModel"/>.</summary>
public partial class ErrorBanner : UserControl
{
    public ErrorBanner()
    {
        InitializeComponent();
    }
}
