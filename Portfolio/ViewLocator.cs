using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MVVM.Core.ViewModels;
using Portfolio.Features.Analysis;
using Portfolio.Features.Backup;
using Portfolio.Features.Editors;
using Portfolio.Features.Manage;
using Portfolio.Features.Shell;

namespace Portfolio;

/// <summary>
/// Maps a view model to its view.
/// </summary>
/// <remarks>
/// An explicit table rather than name-based reflection. It costs one line per screen and buys three
/// things the reflection version could not: a compile error instead of a runtime "Not Found" when a
/// view is renamed, views that do not have to be named after their view model, and — the reason it
/// matters here — a locator that survives trimming, which the WebAssembly build does aggressively.
/// </remarks>
[SuppressMessage(
    "Trimming",
    "IL2026",
    Justification = "No reflection is used; every view is referenced directly."
)]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) =>
        param switch
        {
            AnalysisViewModel => new AnalysisView(),
            ProjectionViewModel => new ProjectionView(),
            EntryListViewModel => new EntryListView(),
            FinancialItemListViewModel => new FinancialItemListView(),
            PlatformListViewModel => new PlatformListView(),
            EntryEditorViewModel => new EntryEditorView(),
            FinancialItemEditorViewModel => new FinancialItemEditorView(),
            PlatformEditorViewModel => new PlatformEditorView(),
            BackupViewModel => new BackupView(),
            NavigationViewModel => new NavigationView(),
            HeaderViewModel => new HeaderView(),
            null => null,
            _ => new TextBlock
            {
                Text = $"No view registered for {param.GetType().Name}",
                Margin = new Avalonia.Thickness(16),
            },
        };

    public bool Match(object? data) => data is ViewModelBase;
}
