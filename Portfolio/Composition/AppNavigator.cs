using MVVM.Core.Layout;
using MVVM.Core.Stores;
using MVVM.Core.ViewModels;
using Portfolio.Domain.Models;
using Portfolio.Features.Analysis;
using Portfolio.Features.Backup;
using Portfolio.Features.Editors;
using Portfolio.Features.Manage;
using Portfolio.Services;
using Portfolio.Stores;

namespace Portfolio.Composition;

/// <summary>The app's top-level destinations.</summary>
public enum Destination
{
    Dashboard,
    Statistics,
    Entries,
    Items,
    Platforms,
}

/// <summary>
/// Creates screens and moves between them.
/// </summary>
/// <remarks>
/// Replaces the seven navigation-service instances the shell used to be handed. Screens now ask for
/// what they want ("edit this entry", "analyse this platform") and this decides how it should appear:
/// an editor is a modal on a wide window and a full page on a phone, because a dialog on a 5-inch
/// screen is a worse version of a page.
/// </remarks>
public sealed class AppNavigator(
    PortfolioStore store,
    LayoutStore layout,
    NavigationStore navigation,
    ModalNavigationStore modal,
    BackupService backup,
    IBackupFiles backupFiles
)
{
    /// <summary>The destination currently shown, for the navigation bar's selected state.</summary>
    public Destination Current { get; private set; } = Destination.Dashboard;

    public event Action? CurrentChanged;

    public void GoTo(Destination destination)
    {
        Current = destination;

        navigation.Reset(() => Create(destination));
        CurrentChanged?.Invoke();
    }

    /// <summary>Opens the statistics screen focused on one platform or holding.</summary>
    public void OpenScope(IPortfolioNode node)
    {
        Current = Destination.Statistics;

        navigation.Push(() => new AnalysisViewModel(store, layout, this, node.Key, isDrillDown: true));
        CurrentChanged?.Invoke();
    }

    /// <summary>
    /// Opens the projection for a scope. A page on top of the statistics rather than a destination:
    /// it is a question you ask about something you are already looking at.
    /// </summary>
    public void OpenProjection(string? scopeKey = null) =>
        navigation.Push(() => new ProjectionViewModel(store, layout, this, scopeKey));

    public void EditEntry(Entry? entry = null, long? financialItemId = null) =>
        Open(() => new EntryEditorViewModel(store, layout, this, entry, financialItemId));

    public void EditFinancialItem(FinancialItem? item = null, long? platformId = null) =>
        Open(() => new FinancialItemEditorViewModel(store, this, item, platformId));

    public void EditPlatform(Platform? platform = null) =>
        Open(() => new PlatformEditorViewModel(store, this, platform));

    /// <summary>
    /// Backup and sync. Shown the way an editor is — a utility reached from a screen rather than a
    /// destination of its own, which also keeps the phone's bottom bar to five items.
    /// </summary>
    public void OpenBackup() =>
        Open(() => new BackupViewModel(store, layout, this, backup, backupFiles));

    /// <summary>
    /// Closes whatever the editor was shown in. Editors call this after saving or cancelling without
    /// needing to know which presentation they got.
    /// </summary>
    public void CloseEditor()
    {
        if (modal.IsOpen)
            modal.Close();
        else
            navigation.GoBack();
    }

    public bool TryGoBack()
    {
        if (modal.IsOpen)
        {
            modal.Close();
            return true;
        }

        return navigation.GoBack();
    }

    private void Open(Func<ViewModelBase> create)
    {
        // Narrow viewports get a full page: there is no room for a form inside a dialog, and the
        // system back gesture then does the right thing for free.
        if (layout.IsCompact)
            navigation.Push(create);
        else
            modal.Open(create);
    }

    private ViewModelBase Create(Destination destination) =>
        destination switch
        {
            Destination.Statistics => new AnalysisViewModel(store, layout, this, mode: AnalysisMode.Explorer),
            Destination.Entries => new EntryListViewModel(store, layout, this),
            Destination.Items => new FinancialItemListViewModel(store, layout, this),
            Destination.Platforms => new PlatformListViewModel(store, layout, this),
            _ => new AnalysisViewModel(store, layout, this, mode: AnalysisMode.Dashboard),
        };
}
