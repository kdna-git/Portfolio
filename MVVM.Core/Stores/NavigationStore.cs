using MVVM.Core.ViewModels;

namespace MVVM.Core.Stores;

/// <summary>
/// Holds the view model currently shown in a region of the shell, plus a back stack so Android's
/// system back gesture and the desktop/browser back affordance have somewhere to go.
/// </summary>
/// <remarks>
/// The stack stores <i>factories</i>, not instances: a view model is disposed as soon as it leaves
/// the screen, so going back rebuilds it from the current data instead of restoring a stale object
/// that stopped listening to the store.
/// </remarks>
public class NavigationStore
{
    private readonly List<Func<ViewModelBase>> _back = [];
    private Func<ViewModelBase>? _currentFactory;

    public ViewModelBase? CurrentViewModel { get; private set; }

    public event Action? CurrentViewModelChanged;

    public event Action? CanGoBackChanged;

    public bool CanGoBack => _back.Count > 0;

    /// <summary>
    /// Shows a view model and remembers the current one, so <see cref="GoBack"/> returns to it.
    /// Use for drill-downs (list to detail).
    /// </summary>
    public void Push(Func<ViewModelBase> createViewModel)
    {
        ArgumentNullException.ThrowIfNull(createViewModel);

        if (_currentFactory is not null)
            _back.Add(_currentFactory);

        _currentFactory = createViewModel;
        SetCurrent(createViewModel());
        CanGoBackChanged?.Invoke();
    }

    /// <summary>
    /// Shows a view model as a new root, discarding the back stack. Use for top-level navigation
    /// (switching tabs), where "back" should leave the app rather than cycle through tabs.
    /// </summary>
    public void Reset(Func<ViewModelBase> createViewModel)
    {
        ArgumentNullException.ThrowIfNull(createViewModel);

        var hadHistory = _back.Count > 0;
        _back.Clear();
        _currentFactory = createViewModel;
        SetCurrent(createViewModel());

        if (hadHistory)
            CanGoBackChanged?.Invoke();
    }

    /// <summary>Returns to the previous view model. False when the back stack is empty.</summary>
    public bool GoBack()
    {
        if (_back.Count == 0)
            return false;

        var previous = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _currentFactory = previous;
        SetCurrent(previous());
        CanGoBackChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Rebuilds the current view model from its factory. Used after a data reload so the screen
    /// reflects the new state without the user navigating away and back.
    /// </summary>
    public void Refresh()
    {
        if (_currentFactory is not null)
            SetCurrent(_currentFactory());
    }

    protected void SetCurrent(ViewModelBase? viewModel)
    {
        if (ReferenceEquals(CurrentViewModel, viewModel))
            return;

        CurrentViewModel?.Dispose();
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();
    }
}
