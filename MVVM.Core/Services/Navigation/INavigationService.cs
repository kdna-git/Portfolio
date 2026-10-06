namespace MVVM.Core.Services;

public interface INavigationService
{
    void Navigate();
}

/// <summary>
/// Navigation that carries a value into the destination view model (an entry to edit, a platform
/// to inspect).
/// </summary>
public interface INavigationService<in TParameter>
{
    void Navigate(TParameter parameter);
}

/// <summary>
/// Whether a navigation starts a new root or stacks on top of the current screen.
/// </summary>
public enum NavigationMode
{
    /// <summary>Top-level destination: clears the back stack.</summary>
    Root,

    /// <summary>Drill-down: the current screen becomes the back target.</summary>
    Push,
}
