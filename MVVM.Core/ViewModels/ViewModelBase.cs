using CommunityToolkit.Mvvm.ComponentModel;

namespace MVVM.Core.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    #region Operating system

    /// <summary>
    /// Which operating system we run on. Useful for platform capabilities (window chrome, file
    /// pickers, back buttons) but <b>not</b> for layout decisions: use
    /// <see cref="MVVM.Core.Layout.LayoutStore"/> for those, because a desktop window can be
    /// narrower than a tablet and a browser can be either.
    /// </summary>
    protected static bool IsDesktop => IsWindows || IsLinux || IsMacOS;

    protected static bool IsMobile => IsAndroid || IsIOS;

    protected static bool IsWeb => IsBrowser;

    protected static bool IsWindows => OperatingSystem.IsWindows();

    protected static bool IsLinux => OperatingSystem.IsLinux();

    protected static bool IsMacOS => OperatingSystem.IsMacOS();

    protected static bool IsAndroid => OperatingSystem.IsAndroid();

    protected static bool IsIOS => OperatingSystem.IsIOS();

    protected static bool IsBrowser => OperatingSystem.IsBrowser();

    #endregion

    private bool _disposed;

    /// <summary>
    /// Releases subscriptions held by this view model. Called by the navigation stores when the
    /// view model is replaced; safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        OnDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to unsubscribe from events or release resources.
    /// </summary>
    protected virtual void OnDispose() { }
}
