using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using MVVM.Core.ViewModels;
using Portfolio.Composition;
using Portfolio.Stores;

namespace Portfolio.Features.Shared;

/// <summary>
/// What every full screen in the app needs: the data, the viewport, navigation, and the plumbing that
/// keeps it in sync with both.
/// </summary>
/// <remarks>
/// Screens used to subscribe to a handful of store events each and unsubscribe in their own
/// <c>Dispose</c>; several got it wrong. Here it happens once, and a subclass only implements
/// <see cref="Refresh"/>.
/// </remarks>
public abstract partial class ScreenViewModel : ViewModelBase
{
    protected ScreenViewModel(PortfolioStore store, LayoutStore layout, AppNavigator navigator)
    {
        Store = store;
        Layout = layout;
        Navigator = navigator;
        Title = string.Empty;

        Store.Changed += OnStoreChanged;
        Store.OperationFailed += OnOperationFailed;
        Layout.PropertyChanged += OnLayoutPropertyChanged;
    }

    protected PortfolioStore Store { get; }

    protected LayoutStore Layout { get; }

    protected AppNavigator Navigator { get; }

    /// <summary>Exposed for views that need column counts or touch sizing.</summary>
    public LayoutStore ViewportLayout => Layout;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    /// <summary>Set when a write failed. Shown as a dismissable banner rather than swallowed.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    [RelayCommand]
    protected void GoBack() => Navigator.TryGoBack();

    /// <summary>Rebuilds everything the screen shows. Called on construction and on every change.</summary>
    protected abstract void Refresh();

    private bool _isReady;
    private bool _isRefreshing;
    private int _suppressDepth;

    /// <summary>
    /// Marks the screen as fully constructed and does the first <see cref="Refresh"/>. The last line
    /// of every subclass constructor.
    /// </summary>
    /// <remarks>
    /// Assigning a filter property in a constructor raises its change handler, which asks the screen
    /// to refresh — while later fields are still null. That is a plain crash as soon as the refresh
    /// touches one of them, and it depends on the order the constructor happens to assign things in,
    /// so it comes back whenever that order changes. Handlers call <see cref="RefreshIfReady"/>
    /// instead, and nothing happens until this runs.
    /// </remarks>
    protected void CompleteInitialisation()
    {
        _isReady = true;
        RefreshIfReady();
    }

    /// <summary>
    /// Refreshes, unless the screen is still being constructed, is already refreshing, or is inside a
    /// <see cref="SuppressRefresh"/> block.
    /// </summary>
    /// <remarks>
    /// The re-entrancy check is what makes a refresh safe to call from a property change handler: a
    /// refresh rebuilds the filter lists, rebuilding a list reassigns the selected item, and that
    /// raises the handler again. Without the guard that is unbounded recursion.
    /// </remarks>
    protected void RefreshIfReady()
    {
        if (!_isReady || _isRefreshing || _suppressDepth > 0)
            return;

        _isRefreshing = true;

        try
        {
            Refresh();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// True while a refresh is in flight or suppressed. Handlers use it to skip work that only makes
    /// sense when the change came from the user rather than from a rebuild.
    /// </summary>
    protected bool IsRefreshSuppressed => _isRefreshing || _suppressDepth > 0;

    /// <summary>
    /// True once <see cref="CompleteInitialisation"/> has run. For handlers that redraw only part of a
    /// screen, and so cannot rely on <see cref="RefreshIfReady"/> to hold them off during construction.
    /// </summary>
    protected bool IsInitialised => _isReady;

    /// <summary>
    /// Holds refreshes off until the returned scope is disposed, for when several properties are
    /// being set together and only the end state is worth drawing.
    /// </summary>
    protected RefreshSuppression SuppressRefresh() => new(this);

    protected readonly struct RefreshSuppression : IDisposable
    {
        private readonly ScreenViewModel _owner;

        internal RefreshSuppression(ScreenViewModel owner)
        {
            _owner = owner;
            owner._suppressDepth++;
        }

        public void Dispose() => _owner._suppressDepth--;
    }

    /// <summary>
    /// Called when the viewport class changes. The default re-runs <see cref="Refresh"/>, because
    /// most screens vary their column counts and chart heights with it.
    /// </summary>
    protected virtual void OnBreakpointChanged() => Refresh();

    protected override void OnDispose()
    {
        Store.Changed -= OnStoreChanged;
        Store.OperationFailed -= OnOperationFailed;
        Layout.PropertyChanged -= OnLayoutPropertyChanged;
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void OnStoreChanged() => Refresh();

    private void OnOperationFailed(string message) => ErrorMessage = message;

    private void OnLayoutPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutStore.Breakpoint))
            OnBreakpointChanged();
    }
}
