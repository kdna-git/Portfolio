using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using MVVM.Core.Stores;

namespace MVVM.Core.ViewModels;

/// <summary>
/// The shell. Owns nothing domain specific: it exposes whatever the navigation stores currently
/// hold, the chrome view models supplied by the host app, and the layout state the views adapt to.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly NavigationStore _navigationStore;
    private readonly ModalNavigationStore _modalNavigationStore;

    /// <summary>The view model filling the content region.</summary>
    public ViewModelBase? CurrentViewModel => _navigationStore.CurrentViewModel;

    /// <summary>The view model filling the modal region, or null when no modal is open.</summary>
    public ViewModelBase? CurrentModalViewModel => _modalNavigationStore.CurrentViewModel;

    /// <summary>Title bar content on desktop; null on single-view platforms.</summary>
    public ViewModelBase? HeaderViewModel { get; }

    /// <summary>Navigation rail or bottom bar content.</summary>
    public ViewModelBase? NavigationBarViewModel { get; }

    /// <summary>Viewport metrics every view binds to for responsive layout.</summary>
    public LayoutStore Layout { get; }

    public bool IsModalOpen => _modalNavigationStore.IsOpen;

    public bool CanGoBack => _navigationStore.CanGoBack;

    [RelayCommand]
    private void CloseModal() => _modalNavigationStore.Close();

    /// <summary>
    /// Back for the whole shell: closes an open modal first, then unwinds the content back stack.
    /// Wired to the Android system back button and to the shell's back affordance.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        if (_modalNavigationStore.IsOpen)
        {
            _modalNavigationStore.Close();
            return;
        }

        _navigationStore.GoBack();
    }

    /// <summary>
    /// True when the shell consumed a back request. Platform heads call this from their own back
    /// handling and only exit the app when it returns false.
    /// </summary>
    public bool TryGoBack()
    {
        if (_modalNavigationStore.IsOpen)
        {
            _modalNavigationStore.Close();
            return true;
        }

        return _navigationStore.GoBack();
    }

    public MainViewModel(
        NavigationStore navigationStore,
        ModalNavigationStore modalNavigationStore,
        LayoutStore layoutStore,
        ViewModelBase? headerViewModel = null,
        ViewModelBase? navigationBarViewModel = null
    )
    {
        _navigationStore = navigationStore;
        _modalNavigationStore = modalNavigationStore;
        Layout = layoutStore;

        _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
        _navigationStore.CanGoBackChanged += OnCanGoBackChanged;
        _modalNavigationStore.CurrentViewModelChanged += OnCurrentModalViewModelChanged;

        HeaderViewModel = headerViewModel;
        NavigationBarViewModel = navigationBarViewModel;
    }

    protected override void OnDispose()
    {
        _navigationStore.CurrentViewModelChanged -= OnCurrentViewModelChanged;
        _navigationStore.CanGoBackChanged -= OnCanGoBackChanged;
        _modalNavigationStore.CurrentViewModelChanged -= OnCurrentModalViewModelChanged;

        HeaderViewModel?.Dispose();
        NavigationBarViewModel?.Dispose();
    }

    private void OnCurrentViewModelChanged() => OnPropertyChanged(nameof(CurrentViewModel));

    private void OnCanGoBackChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.NotifyCanExecuteChanged();
    }

    private void OnCurrentModalViewModelChanged()
    {
        OnPropertyChanged(nameof(CurrentModalViewModel));
        OnPropertyChanged(nameof(IsModalOpen));
    }
}
