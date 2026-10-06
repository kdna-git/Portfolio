using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using MVVM.Core.ViewModels;
using Portfolio.Composition;

namespace Portfolio.Features.Shell;

/// <summary>
/// The app's destinations, rendered as a side rail on wide layouts and a bottom bar on narrow ones.
/// </summary>
/// <remarks>
/// One view model for both presentations. The old shell had two hand-written navigation views that
/// drifted apart — the phone one crammed seven full-length labels into a 64-pixel strip — so the item
/// list now carries a short label for the bar and a full one for the rail.
/// </remarks>
public sealed partial class NavigationViewModel : ViewModelBase
{
    private readonly AppNavigator _navigator;

    public NavigationViewModel(AppNavigator navigator, LayoutStore layout)
    {
        _navigator = navigator;
        Layout = layout;

        Items =
        [
            new NavigationItem(
                Destination.Dashboard,
                "Dashboard",
                "Home",
                "M3 10.5 12 3l9 7.5 M5 9.5V20h14V9.5 M10 20v-6h4v6"
            ),
            new NavigationItem(
                Destination.Statistics,
                "Statistics",
                "Stats",
                "M4 20V10 M10 20V4 M16 20v-7 M22 20H2"
            ),
            new NavigationItem(
                Destination.Entries,
                "Entries",
                "Entries",
                "M5 3h14v18H5z M8 8h8 M8 12h8 M8 16h5"
            ),
            new NavigationItem(
                Destination.Items,
                "Holdings",
                "Holdings",
                "M3 7h18v12H3z M3 7l3-4h12l3 4 M12 11v4 M10 13h4"
            ),
            new NavigationItem(
                Destination.Platforms,
                "Platforms",
                "Places",
                "M4 21V8l8-5 8 5v13 M9 21v-6h6v6"
            ),
        ];

        _navigator.CurrentChanged += OnCurrentChanged;
        OnCurrentChanged();
    }

    public LayoutStore Layout { get; }

    public IReadOnlyList<NavigationItem> Items { get; }

    public bool UsesSideNavigation => Layout.UsesSideNavigation;

    [RelayCommand]
    private void Navigate(NavigationItem? item)
    {
        if (item is not null)
            _navigator.GoTo(item.Destination);
    }

    /// <summary>The primary create action, offered from the navigation chrome on every screen.</summary>
    [RelayCommand]
    private void AddEntry() => _navigator.EditEntry();

    /// <summary>
    /// Backup and sync, from the rail. The bottom bar has no room for it; on a phone the dashboard
    /// offers it instead.
    /// </summary>
    [RelayCommand]
    private void OpenBackup() => _navigator.OpenBackup();

    protected override void OnDispose() => _navigator.CurrentChanged -= OnCurrentChanged;

    private void OnCurrentChanged()
    {
        foreach (var item in Items)
            item.IsSelected = item.Destination == _navigator.Current;
    }
}

/// <summary>One destination in the navigation chrome.</summary>
public sealed partial class NavigationItem(
    Destination destination,
    string label,
    string shortLabel,
    string iconData
) : ObservableObject
{
    public Destination Destination { get; } = destination;

    public string Label { get; } = label;

    /// <summary>Used by the bottom bar, where there is room for one short word.</summary>
    public string ShortLabel { get; } = shortLabel;

    public string IconData { get; } = iconData;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
