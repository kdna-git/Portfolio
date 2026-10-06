using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using MVVM.Core.ViewModels;
using Portfolio.Charts.Data;
using Portfolio.Composition;
using Portfolio.Domain.Models;
using Portfolio.Stores;

namespace Portfolio.Features.Editors;

/// <summary>
/// Creates or edits one entry.
/// </summary>
/// <remarks>
/// The form shows what it is about to do: as the amounts change, the summary at the bottom reports the
/// resulting change to the balance and to the invested total. Entries are the one place in the app
/// where a typo quietly corrupts every statistic, so the preview is worth the space.
/// </remarks>
public sealed partial class EntryEditorViewModel : ViewModelBase
{
    private readonly PortfolioStore _store;
    private readonly LayoutStore _layout;
    private readonly AppNavigator _navigator;
    private readonly Entry? _original;

    public EntryEditorViewModel(
        PortfolioStore store,
        LayoutStore layout,
        AppNavigator navigator,
        Entry? entry = null,
        long? financialItemId = null
    )
    {
        _store = store;
        _layout = layout;
        _navigator = navigator;
        _original = entry;

        Platforms = [.. store.Platforms];
        Holdings = [];
        Date = DateTimeOffset.Now;

        var targetItemId = entry?.FinancialItemId ?? financialItemId;

        if (targetItemId is not null)
        {
            SelectedPlatform = store.FindPlatformOf(targetItemId.Value);
            SelectedHolding = SelectedPlatform?.FindFinancialItem(targetItemId.Value);
        }
        else if (Platforms.Count == 1)
        {
            SelectedPlatform = Platforms[0];
        }

        if (entry is not null)
        {
            Date = entry.Date.ToDateTime(TimeOnly.MinValue);
            Note = entry.Note;
            Deposit = Nullify(entry.Deposit);
            Withdrawal = Nullify(entry.Withdrawal);
            Investment = Nullify(entry.Investment);
            PrincipalRepayment = Nullify(entry.PrincipalRepayment);
            Interest = Nullify(entry.Interest);
            Bonus = Nullify(entry.Bonus);
            Tax = Nullify(entry.Tax);
            Fee = Nullify(entry.Fee);
            DefaultLoss = Nullify(entry.DefaultLoss);
            MarketValue = entry.MarketValue;
        }

        UpdatePreview();
    }

    public bool IsEditing => _original is not null;

    public string Heading => IsEditing ? "Edit entry" : "New entry";

    public string SaveText => IsEditing ? "Save changes" : "Add entry";

    public int Columns => _layout.IsCompact ? 1 : _layout.IsMedium ? 2 : 3;

    #region Fields

    public ObservableCollection<Platform> Platforms { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial Platform? SelectedPlatform { get; set; }

    public ObservableCollection<FinancialItem> Holdings { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial FinancialItem? SelectedHolding { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DateTimeOffset? Date { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial decimal? Deposit { get; set; }

    [ObservableProperty]
    public partial decimal? Withdrawal { get; set; }

    [ObservableProperty]
    public partial decimal? Investment { get; set; }

    [ObservableProperty]
    public partial decimal? PrincipalRepayment { get; set; }

    [ObservableProperty]
    public partial decimal? Interest { get; set; }

    [ObservableProperty]
    public partial decimal? Bonus { get; set; }

    [ObservableProperty]
    public partial decimal? Tax { get; set; }

    [ObservableProperty]
    public partial decimal? Fee { get; set; }

    [ObservableProperty]
    public partial decimal? DefaultLoss { get; set; }

    [ObservableProperty]
    public partial decimal? MarketValue { get; set; }

    #endregion

    #region Preview

    [ObservableProperty]
    public partial string BalanceChangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool BalanceChangeIsPositive { get; set; }

    [ObservableProperty]
    public partial bool BalanceChangeIsNegative { get; set; }

    [ObservableProperty]
    public partial string InvestedChangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EarningsChangeText { get; set; } = string.Empty;

    /// <summary>A note about something questionable. Never blocks saving.</summary>
    [ObservableProperty]
    public partial string? Warning { get; set; }

    public bool HasWarning => !string.IsNullOrEmpty(Warning);

    [ObservableProperty]
    public partial bool HasPlatforms { get; set; }

    #endregion

    #region Commands

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        var entry = new Entry(
            _original?.Id ?? 0,
            SelectedHolding!.Id,
            DateOnly.FromDateTime(Date!.Value.Date),
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            Deposit ?? 0,
            Withdrawal ?? 0,
            Investment ?? 0,
            PrincipalRepayment ?? 0,
            Interest ?? 0,
            Bonus ?? 0,
            Tax ?? 0,
            Fee ?? 0,
            DefaultLoss ?? 0,
            MarketValue
        );

        if (IsEditing)
            await _store.UpdateEntryAsync(entry).ConfigureAwait(true);
        else
            await _store.AddEntryAsync(entry).ConfigureAwait(true);

        _navigator.CloseEditor();
    }

    private bool CanSave() =>
        SelectedHolding is not null && Date is not null && HasSomethingToRecord();

    [RelayCommand]
    private void Cancel() => _navigator.CloseEditor();

    [RelayCommand]
    private void AddPlatform() => _navigator.EditPlatform();

    /// <summary>
    /// Clears every amount, keeping the holding and the date. Faster than deleting ten fields when a
    /// row was started by mistake.
    /// </summary>
    [RelayCommand]
    private void ClearAmounts()
    {
        Deposit = null;
        Withdrawal = null;
        Investment = null;
        PrincipalRepayment = null;
        Interest = null;
        Bonus = null;
        Tax = null;
        Fee = null;
        DefaultLoss = null;
        MarketValue = null;
    }

    #endregion

    partial void OnSelectedPlatformChanged(Platform? value)
    {
        var previous = SelectedHolding?.Id;

        Holdings.Clear();

        if (value is not null)
        {
            foreach (var item in value.FinancialItems.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                Holdings.Add(item);
        }

        SelectedHolding =
            Holdings.FirstOrDefault(i => i.Id == previous)
            // One holding means there is no choice to make; pre-select it.
            ?? (Holdings.Count == 1 ? Holdings[0] : null);

        UpdatePreview();
    }

    partial void OnSelectedHoldingChanged(FinancialItem? value) => UpdatePreview();

    partial void OnDepositChanged(decimal? value) => UpdatePreview();

    partial void OnWithdrawalChanged(decimal? value) => UpdatePreview();

    partial void OnInvestmentChanged(decimal? value) => UpdatePreview();

    partial void OnPrincipalRepaymentChanged(decimal? value) => UpdatePreview();

    partial void OnInterestChanged(decimal? value) => UpdatePreview();

    partial void OnBonusChanged(decimal? value) => UpdatePreview();

    partial void OnTaxChanged(decimal? value) => UpdatePreview();

    partial void OnFeeChanged(decimal? value) => UpdatePreview();

    partial void OnDefaultLossChanged(decimal? value) => UpdatePreview();

    partial void OnMarketValueChanged(decimal? value) => UpdatePreview();

    partial void OnWarningChanged(string? value) => OnPropertyChanged(nameof(HasWarning));

    private bool HasSomethingToRecord() =>
        (Deposit ?? 0) != 0
        || (Withdrawal ?? 0) != 0
        || (Investment ?? 0) != 0
        || (PrincipalRepayment ?? 0) != 0
        || (Interest ?? 0) != 0
        || (Bonus ?? 0) != 0
        || (Tax ?? 0) != 0
        || (Fee ?? 0) != 0
        || (DefaultLoss ?? 0) != 0
        || MarketValue is not null;

    private void UpdatePreview()
    {
        HasPlatforms = Platforms.Count > 0;

        var balanceChange =
            (Deposit ?? 0)
            - (Withdrawal ?? 0)
            + (Interest ?? 0)
            + (Bonus ?? 0)
            - (Tax ?? 0)
            - (Fee ?? 0)
            - (DefaultLoss ?? 0);

        var investedChange = (Investment ?? 0) - (PrincipalRepayment ?? 0) - (DefaultLoss ?? 0);
        var earnings = (Interest ?? 0) + (Bonus ?? 0) - (Tax ?? 0) - (Fee ?? 0) - (DefaultLoss ?? 0);

        BalanceChangeText = ValueFormatter.FormatSigned((double)balanceChange, ValueFormat.Currency);
        BalanceChangeIsPositive = balanceChange > 0;
        BalanceChangeIsNegative = balanceChange < 0;
        InvestedChangeText = ValueFormatter.FormatSigned((double)investedChange, ValueFormat.Currency);
        EarningsChangeText = ValueFormatter.FormatSigned((double)earnings, ValueFormat.Currency);

        Warning = BuildWarning(balanceChange, investedChange);
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Catches the mistakes that are easy to make and hard to spot later. All of them are warnings
    /// rather than errors, because every one of them is legitimate in some real situation.
    /// </summary>
    private string? BuildWarning(decimal balanceChange, decimal investedChange)
    {
        if (SelectedHolding is null)
            return Platforms.Count == 0
                ? "Add a platform and a holding before recording entries."
                : null;

        if (Date is { } date && DateOnly.FromDateTime(date.Date) > DateOnly.FromDateTime(DateTime.Today))
            return "This entry is dated in the future, so it will not show up in figures for today.";

        var cash = SelectedHolding.Cash;

        if ((Withdrawal ?? 0) > 0 && (Withdrawal ?? 0) > cash + (Deposit ?? 0))
            return $"Withdrawing more than the cash available on this holding ({ValueFormatter.Format((double)cash, ValueFormat.Currency)}). It will go negative.";

        if ((Investment ?? 0) > 0 && (Investment ?? 0) > cash + (Deposit ?? 0))
            return "Investing more than the cash available on this holding. Record the deposit in the same entry if that is what happened.";

        if ((PrincipalRepayment ?? 0) > SelectedHolding.Invested + (Investment ?? 0))
            return "Repaying more capital than is currently invested in this holding.";

        if (MarketValue is not null && !HasOtherAmounts())
            return "Valuation only: this records what the holding is worth without moving any money.";

        return null;
    }

    private bool HasOtherAmounts() =>
        (Deposit ?? 0) != 0
        || (Withdrawal ?? 0) != 0
        || (Investment ?? 0) != 0
        || (PrincipalRepayment ?? 0) != 0
        || (Interest ?? 0) != 0
        || (Bonus ?? 0) != 0
        || (Tax ?? 0) != 0
        || (Fee ?? 0) != 0
        || (DefaultLoss ?? 0) != 0;

    // Zeros are shown as empty fields: an empty box reads as "nothing happened here", a 0 reads as a
    // value someone entered.
    private static decimal? Nullify(decimal value) => value == 0 ? null : value;
}
