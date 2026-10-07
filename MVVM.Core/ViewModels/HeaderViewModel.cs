using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MVVM.Core.ViewModels;

/// <summary>
/// Desktop title bar: the app name plus the window buttons that replace the native chrome.
/// </summary>
public sealed partial class HeaderViewModel(string title, string? subtitle = null) : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = title;

    [ObservableProperty]
    public partial string? Subtitle { get; set; } = subtitle;

    #region Window commands

    [RelayCommand]
    private static void ShutdownWindow(Window? window)
    {
        if (IsDesktop)
            window?.Close();
    }

    [RelayCommand]
    private static void MaximizeWindow(Window? window)
    {
        if (!IsDesktop || window is null)
            return;

        window.WindowState =
            window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }

    [RelayCommand]
    private static void MinimizeWindow(Window? window)
    {
        if (IsDesktop && window is not null)
            window.WindowState = WindowState.Minimized;
    }

    #endregion
}
