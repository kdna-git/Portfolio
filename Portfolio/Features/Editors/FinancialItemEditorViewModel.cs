using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.ViewModels;
using Portfolio.Composition;
using Portfolio.Domain.Models;
using Portfolio.Stores;

namespace Portfolio.Features.Editors;

/// <summary>Creates or edits one holding.</summary>
/// <remarks>
/// The edit path used to open with every field blank and then save those blanks over the holding. Here
/// the form is populated from the holding, and the entries it owns are left alone by the store.
/// </remarks>
public sealed partial class FinancialItemEditorViewModel : ViewModelBase
{
    private readonly PortfolioStore _store;
    private readonly AppNavigator _navigator;
    private readonly FinancialItem? _original;

    public FinancialItemEditorViewModel(
        PortfolioStore store,
        AppNavigator navigator,
        FinancialItem? item = null,
        long? platformId = null
    )
    {
        _store = store;
        _navigator = navigator;
        _original = item;

        Platforms = [.. store.Platforms];
        InvestmentTypes = Enum.GetValues<InvestmentType>();

        if (item is not null)
        {
            Name = item.Name;
            SelectedType = item.Type;
            SelectedPlatform = store.FindPlatform(item.PlatformId);
            EntrySummary = item.Entries.Count == 0
                ? "No entries yet"
                : $"{item.Entries.Count} entries · first {item.FirstDate:d MMM yyyy} · last {item.LastDate:d MMM yyyy}";
        }
        else
        {
            SelectedPlatform =
                (platformId is not null ? store.FindPlatform(platformId.Value) : null)
                ?? (Platforms.Count == 1 ? Platforms[0] : null);
        }
    }

    public bool IsEditing => _original is not null;

    public string Heading => IsEditing ? "Edit holding" : "New holding";

    public string SaveText => IsEditing ? "Save changes" : "Add holding";

    public string? EntrySummary { get; }

    public bool HasEntrySummary => !string.IsNullOrEmpty(EntrySummary);

    public bool HasPlatforms => Platforms.Count > 0;

    public ObservableCollection<Platform> Platforms { get; }

    public IReadOnlyList<InvestmentType> InvestmentTypes { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Name { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial Platform? SelectedPlatform { get; set; }

    [ObservableProperty]
    public partial InvestmentType SelectedType { get; set; }

    /// <summary>What the chosen investment type is for, since the names are not self-explanatory.</summary>
    public string TypeDescription =>
        SelectedType switch
        {
            InvestmentType.Grow => "Capital you keep adding to.",
            InvestmentType.Compound => "Income is reinvested in place.",
            InvestmentType.Hold => "Left alone: neither fed nor drained.",
            InvestmentType.Buffer => "Liquidity you want to be able to reach.",
            InvestmentType.Shrink => "Being wound down gradually.",
            InvestmentType.Exit => "Being closed out.",
            _ => "Anything that does not fit the other roles.",
        };

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        var item = new FinancialItem(
            _original?.Id ?? 0,
            SelectedPlatform!.Id,
            SelectedType,
            Name!.Trim()
        );

        if (IsEditing)
            await _store.UpdateFinancialItemAsync(item).ConfigureAwait(true);
        else
            await _store.AddFinancialItemAsync(item).ConfigureAwait(true);

        _navigator.CloseEditor();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && SelectedPlatform is not null;

    [RelayCommand]
    private void Cancel() => _navigator.CloseEditor();

    [RelayCommand]
    private void AddPlatform() => _navigator.EditPlatform();

    partial void OnSelectedTypeChanged(InvestmentType value) =>
        OnPropertyChanged(nameof(TypeDescription));
}
