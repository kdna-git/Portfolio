using MVVM.Core.Stores;
using MVVM.Core.ViewModels;

namespace MVVM.Core.Services;

public sealed class NavigationService(
    NavigationStore navigationStore,
    Func<ViewModelBase> createViewModel,
    NavigationMode mode = NavigationMode.Root
) : INavigationService
{
    public void Navigate()
    {
        if (mode == NavigationMode.Push)
            navigationStore.Push(createViewModel);
        else
            navigationStore.Reset(createViewModel);
    }
}

public sealed class ParameterNavigationService<TParameter, TViewModel>(
    NavigationStore navigationStore,
    Func<TParameter, TViewModel> createViewModel,
    NavigationMode mode = NavigationMode.Push
) : INavigationService<TParameter>
    where TViewModel : ViewModelBase
{
    public void Navigate(TParameter parameter)
    {
        if (mode == NavigationMode.Push)
            navigationStore.Push(() => createViewModel(parameter));
        else
            navigationStore.Reset(() => createViewModel(parameter));
    }
}

/// <summary>Opens a view model in the modal region.</summary>
public sealed class ModalNavigationService(
    ModalNavigationStore modalNavigationStore,
    Func<ViewModelBase> createViewModel
) : INavigationService
{
    public void Navigate() => modalNavigationStore.Open(createViewModel);
}

/// <summary>Opens a parameterised view model in the modal region.</summary>
public sealed class ModalParameterNavigationService<TParameter, TViewModel>(
    ModalNavigationStore modalNavigationStore,
    Func<TParameter, TViewModel> createViewModel
) : INavigationService<TParameter>
    where TViewModel : ViewModelBase
{
    public void Navigate(TParameter parameter) =>
        modalNavigationStore.Open(() => createViewModel(parameter));
}

/// <summary>Closes the modal region.</summary>
public sealed class CloseModalNavigationService(ModalNavigationStore modalNavigationStore)
    : INavigationService
{
    public void Navigate() => modalNavigationStore.Close();
}

/// <summary>Runs several navigations in order (for example: save, close the modal, refresh).</summary>
public sealed class CompositeNavigationService(params INavigationService[] navigationServices)
    : INavigationService
{
    public void Navigate()
    {
        foreach (var navigationService in navigationServices)
            navigationService.Navigate();
    }
}

/// <summary>Navigation that does nothing. Keeps view models free of null checks.</summary>
public sealed class NoNavigationService : INavigationService
{
    public static readonly NoNavigationService Instance = new();

    public void Navigate() { }
}
