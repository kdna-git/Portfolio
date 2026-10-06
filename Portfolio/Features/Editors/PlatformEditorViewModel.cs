using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.ViewModels;
using Portfolio.Charts.Data;
using Portfolio.Composition;
using Portfolio.Domain.Models;
using Portfolio.Stores;

namespace Portfolio.Features.Editors;

/// <summary>Creates or renames one platform.</summary>
public sealed partial class PlatformEditorViewModel : ViewModelBase
{
    private readonly PortfolioStore _store;
    private readonly AppNavigator _navigator;
    private readonly Platform? _original;

    public PlatformEditorViewModel(
        PortfolioStore store,
        AppNavigator navigator,
        Platform? platform = null
    )
    {
        _store = store;
        _navigator = navigator;
        _original = platform;

        if (platform is not null)
        {
            Name = platform.Name;
            Summary =
                $"{platform.FinancialItems.Count} holdings · {ValueFormatter.Format((double)platform.TotalBalance, ValueFormat.Currency)}";
        }
    }

    public bool IsEditing => _original is not null;

    public string Heading => IsEditing ? "Edit platform" : "New platform";

    public string SaveText => IsEditing ? "Save changes" : "Add platform";

    public string? Summary { get; }

    public bool HasSummary => !string.IsNullOrEmpty(Summary);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Name { get; set; }

    /// <summary>
    /// Set when another platform already uses this name. A warning rather than a block: two accounts
    /// at the same broker are a real thing people track separately.
    /// </summary>
    public string? NameWarning =>
        string.IsNullOrWhiteSpace(Name)
            ? null
            : _store.Platforms.Any(p =>
                p.Id != (_original?.Id ?? 0)
                && p.Name.Equals(Name.Trim(), StringComparison.CurrentCultureIgnoreCase)
            )
                ? "Another platform already has this name."
                : null;

    public bool HasNameWarning => NameWarning is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        var platform = new Platform(_original?.Id ?? 0, Name!.Trim());

        if (IsEditing)
            await _store.UpdatePlatformAsync(platform).ConfigureAwait(true);
        else
            await _store.AddPlatformAsync(platform).ConfigureAwait(true);

        _navigator.CloseEditor();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand]
    private void Cancel() => _navigator.CloseEditor();

    partial void OnNameChanged(string? value)
    {
        OnPropertyChanged(nameof(NameWarning));
        OnPropertyChanged(nameof(HasNameWarning));
    }
}
