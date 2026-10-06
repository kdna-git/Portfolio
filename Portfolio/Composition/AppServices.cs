using MVVM.Core.Layout;
using MVVM.Core.Stores;
using Portfolio.Domain.Persistence;
using Portfolio.Services;
using Portfolio.Storage;
using Portfolio.Stores;

namespace Portfolio.Composition;

/// <summary>
/// The app's composition root: everything long-lived, built once per process.
/// </summary>
/// <remarks>
/// Each platform head constructs this with a repository and nothing else. That one seam is why the
/// heads are now a dozen lines each instead of forty, and why a head physically cannot start without
/// storage — the previous arrangement let the browser head launch with none, and it crashed on the
/// first screen.
/// </remarks>
public sealed class AppServices
{
    /// <param name="repository">Where the portfolio lives.</param>
    /// <param name="device">
    /// Where this device keeps its own bookkeeping — the last sync and the restore point — and what
    /// it is called in backup files.
    /// </param>
    public AppServices(IPortfolioRepository repository, IDeviceStorage device)
    {
        // Before anything formats a number: a browser can hand us a culture with no country, and
        // therefore no currency symbol.
        AppCulture.EnsureSpecific();

        Repository = repository;
        Store = new PortfolioStore(repository);
        Backup = new BackupService(Store, device);
        Layout = new LayoutStore();
        Navigation = new NavigationStore();
        Modal = new ModalNavigationStore();
        Navigator = new AppNavigator(
            Store,
            Layout,
            Navigation,
            Modal,
            Backup,
            new AvaloniaBackupFiles()
        );
    }

    public IPortfolioRepository Repository { get; }

    public PortfolioStore Store { get; }

    public BackupService Backup { get; }

    public LayoutStore Layout { get; }

    public NavigationStore Navigation { get; }

    public ModalNavigationStore Modal { get; }

    public AppNavigator Navigator { get; }

    /// <summary>Loads the portfolio and shows the first screen.</summary>
    public async Task StartAsync()
    {
        await Store.LoadAsync().ConfigureAwait(true);
        await Backup.LoadAsync().ConfigureAwait(true);
        Navigator.GoTo(Destination.Dashboard);
    }
}
