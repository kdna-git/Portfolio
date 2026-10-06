using MVVM.Core.ViewModels;

namespace MVVM.Core.Stores;

/// <summary>
/// The modal region of the shell. Modals never stack, so this store has no back history: opening
/// one replaces whatever was open.
/// </summary>
public sealed class ModalNavigationStore : NavigationStore
{
    public bool IsOpen => CurrentViewModel is not null;

    public void Open(Func<ViewModelBase> createViewModel)
    {
        ArgumentNullException.ThrowIfNull(createViewModel);
        SetCurrent(createViewModel());
    }

    public void Close() => SetCurrent(null);
}
