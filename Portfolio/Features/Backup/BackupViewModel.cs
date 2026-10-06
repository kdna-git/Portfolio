using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Core.Layout;
using Portfolio.Composition;
using Portfolio.Domain.Models;
using Portfolio.Features.Shared;
using Portfolio.Services;
using Portfolio.Storage;
using Portfolio.Stores;

namespace Portfolio.Features.Backup;

/// <summary>
/// Export, import and restore: the whole backup-and-sync workflow on one screen.
/// </summary>
/// <remarks>
/// An import is never one tap. Picking a file only reviews it — what it holds, where it came from,
/// what on this device it would discard — and nothing changes until the review is confirmed. When
/// the review finds a reason for caution, the confirmation becomes an explicit "replace anyway",
/// offered next to the option that removes the risk instead: export this device's data first.
/// </remarks>
public sealed partial class BackupViewModel : ScreenViewModel
{
    private readonly BackupService _backup;
    private readonly IBackupFiles _files;
    private ImportReview? _review;

    public BackupViewModel(
        PortfolioStore store,
        LayoutStore layout,
        AppNavigator navigator,
        BackupService backup,
        IBackupFiles files
    )
        : base(store, layout, navigator)
    {
        _backup = backup;
        _files = files;

        Title = "Backup & sync";
        ReviewWarnings = [];

        _backup.Changed += OnBackupChanged;
        CompleteInitialisation();
    }

    #region This device

    [ObservableProperty]
    public partial string DeviceSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SyncStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSyncNote))]
    [NotifyPropertyChangedFor(nameof(ShowSyncWarning))]
    public partial string? SyncDetail { get; set; }

    /// <summary>This device holds data that no backup file has a copy of.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSyncNote))]
    [NotifyPropertyChangedFor(nameof(ShowSyncWarning))]
    public partial bool HasUnsyncedChanges { get; set; }

    public bool ShowSyncNote => SyncDetail is not null && !HasUnsyncedChanges;

    /// <summary>The same line, drawn as a warning: data no file has a copy of.</summary>
    public bool ShowSyncWarning => SyncDetail is not null && HasUnsyncedChanges;

    #endregion

    #region Outcome

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportThenImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(AskRestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRestoreCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>What the last action did, when it succeeded. Failures go to the error banner.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    #endregion

    #region Import review

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRestorePoint))]
    public partial bool IsReviewing { get; set; }

    [ObservableProperty]
    public partial string ReviewSource { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReviewContents { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReviewImpact { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<string> ReviewWarnings { get; set; }

    [ObservableProperty]
    public partial bool HasReviewWarnings { get; set; }

    /// <summary>Offered when the import would discard changes no file has: save them first.</summary>
    [ObservableProperty]
    public partial bool CanExportFirst { get; set; }

    [ObservableProperty]
    public partial string ConfirmImportText { get; set; } = string.Empty;

    #endregion

    #region Restore point

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRestorePoint))]
    public partial bool HasRestorePoint { get; set; }

    /// <summary>Hidden during a review, which already says the current data will become one.</summary>
    public bool ShowRestorePoint => HasRestorePoint && !IsReviewing;

    [ObservableProperty]
    public partial string RestorePointSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsConfirmingRestore { get; set; }

    #endregion

    #region Commands

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Export() =>
        RunAsync(
            async () =>
            {
                if (await ExportToFileAsync().ConfigureAwait(true) is { } fileName)
                    StatusMessage = $"Saved {fileName}. Copy it to the other device and import it there.";
            },
            "The backup could not be saved."
        );

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Import() =>
        RunAsync(
            async () =>
            {
                var json = await _files.OpenAsync().ConfigureAwait(true);

                if (json is not null)
                    ShowReview(_backup.Review(json));
            },
            "The file could not be read."
        );

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task ConfirmImport() =>
        RunAsync(
            async () =>
            {
                if (_review is not { } review)
                    return;

                CloseReview();
                await _backup.ImportAsync(review.Backup).ConfigureAwait(true);
                StatusMessage = $"Imported {Counts(review.Backup)}{Origin(review.Backup)}. The data it replaced is kept as a restore point.";
            },
            "The import failed."
        );

    /// <summary>Saves this device's data to a file, then imports, so the import loses nothing.</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task ExportThenImport() =>
        RunAsync(
            async () =>
            {
                if (_review is not { } review)
                    return;

                // Cancelling the save cancels the import too, and leaves the review open.
                if (await ExportToFileAsync().ConfigureAwait(true) is not { } fileName)
                    return;

                CloseReview();
                await _backup.ImportAsync(review.Backup).ConfigureAwait(true);
                StatusMessage = $"Saved this device's data as {fileName}, then imported {Counts(review.Backup)}{Origin(review.Backup)}.";
            },
            "The import failed."
        );

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void CancelImport() => CloseReview();

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void AskRestore() => IsConfirmingRestore = true;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task ConfirmRestore() =>
        RunAsync(
            async () =>
            {
                IsConfirmingRestore = false;

                var savedAt = _backup.RestorePoint?.SavedAt;
                await _backup.RestoreAsync().ConfigureAwait(true);
                StatusMessage = $"Restored the data saved {When(savedAt)}. What it replaced is now the restore point, so this can be undone the same way.";
            },
            "The restore failed."
        );

    [RelayCommand]
    private void CancelRestore() => IsConfirmingRestore = false;

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    private bool CanAct() => !IsBusy;

    #endregion

    protected override void Refresh()
    {
        Subtitle = $"This device: {_backup.DeviceName}";

        var platforms = Store.Platforms;
        var hasData = platforms.Count > 0;

        DeviceSummary = hasData ? Counts(platforms) : "No data on this device yet.";

        var last = _backup.LastSync;
        var changed = last is not null && last.Fingerprint != PortfolioTransfer.Fingerprint(platforms);

        SyncStatus = last switch
        {
            null => "Nothing has been exported or imported on this device yet.",
            { Direction: SyncDirection.Exported } => $"Last exported {When(last.At)}.",
            _ => $"Last imported {When(last.At)}, from {Source(last.SourceDevice, last.SourceExportedAt)}.",
        };

        HasUnsyncedChanges = hasData && (last is null || changed);
        SyncDetail =
            !hasData ? null
            : last is null ? "No backup file has a copy of this data yet."
            : changed ? "Changed since then. Export before importing anything here."
            : "No changes since then.";

        var point = _backup.RestorePoint;
        HasRestorePoint = point is not null;
        RestorePointSummary = point is null
            ? string.Empty
            : $"Saved {When(point.SavedAt)}, just before the last import or restore replaced the data: {Counts(point.Snapshot)}.";

        if (point is null)
            IsConfirmingRestore = false;

        // The review compares against this device's data, so it is redone whenever that changes.
        if (_review is not null)
            ShowReview(_backup.Review(_review.Backup));
    }

    protected override void OnDispose()
    {
        _backup.Changed -= OnBackupChanged;
        base.OnDispose();
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    private void OnBackupChanged() => RefreshIfReady();

    /// <summary>Saves a backup file. The name it was saved under, or null when the save was cancelled.</summary>
    private async Task<string?> ExportToFileAsync()
    {
        var export = _backup.CreateExport();
        string? fileName;

        try
        {
            fileName = await _files.SaveAsync(export.FileName, export.Json).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            throw new BackupFailedException($"The backup could not be saved. {exception.Message}", exception);
        }

        if (fileName is not null)
            await _backup.RecordExportAsync(export).ConfigureAwait(true);

        return fileName;
    }

    private void ShowReview(ImportReview review)
    {
        if (review.IsAlreadyCurrent)
        {
            CloseReview();
            StatusMessage = "That backup holds exactly what is on this device already. Nothing to import.";
            return;
        }

        _review = review;

        var backup = review.Backup;
        var platforms = Store.Platforms;
        var hasData = platforms.Count > 0;

        ReviewSource = backup.ExportedAt is { } exported
            ? $"From {backup.Device ?? "an unnamed device"}, exported {When(exported)}."
            : $"From {backup.Device ?? "an unnamed device"}. The file does not say when it was exported.";

        ReviewContents = backup.PlatformCount == 0 ? "The backup is empty." : $"It holds {Counts(backup)}.";

        ReviewImpact = hasData
            ? $"Everything on this device ({Counts(platforms)}) will be replaced. It is kept as a restore point first, so this can be undone."
            : "This device has no data yet, so nothing will be replaced.";

        var warnings = new List<string>();

        if (review.LocalChanges == UnsyncedChanges.SinceLastSync && review.LastSync is { } last)
        {
            var verb = last.Direction == SyncDirection.Exported ? "exported" : "imported";
            warnings.Add($"This device has changes made since it last {verb} ({When(last.At)}). Importing discards them.");
        }
        else if (review.LocalChanges == UnsyncedChanges.NeverSynced)
        {
            warnings.Add("The data on this device has never been exported, so no backup file has a copy of it.");
        }

        if (review.IsOlderThanLastSync)
        {
            warnings.Add(
                review.LastSync?.Direction == SyncDirection.Imported
                    ? $"This backup is older than the one this device imported last, which was exported {When(review.NewestKnownExport)}. It is probably out of date."
                    : $"This backup is older than this device's own last export ({When(review.NewestKnownExport)}). Importing it would wind this device back."
            );
        }

        if (backup.PlatformCount == 0 && hasData)
            warnings.Add("Importing an empty backup leaves this device with no data at all.");

        ReviewWarnings = warnings;
        HasReviewWarnings = warnings.Count > 0;
        CanExportFirst = review.LocalChanges != UnsyncedChanges.None;
        ConfirmImportText =
            !hasData ? "Import"
            : HasReviewWarnings ? "Replace anyway"
            : "Replace this device's data";

        IsReviewing = true;
    }

    private void CloseReview()
    {
        _review = null;
        IsReviewing = false;
        ReviewWarnings = [];
        HasReviewWarnings = false;
    }

    private async Task RunAsync(Func<Task> action, string failure)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (PortfolioBackupException exception)
        {
            ErrorMessage = $"{exception.Message} Nothing on this device was changed.";
        }
        catch (BackupFailedException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"{failure} {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string When(DateTimeOffset? at) =>
        at?.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture) ?? "at an unknown time";

    /// <summary>"Desktop's backup of 6 Oct 2026, 15:00", or as much of that as the file said.</summary>
    private static string Source(string? device, DateTimeOffset? exportedAt) =>
        (device, exportedAt) switch
        {
            (null, null) => "a backup",
            (null, { } at) => $"a backup of {When(at)}",
            ({ } name, null) => $"{name}'s backup",
            ({ } name, { } at) => $"{name}'s backup of {When(at)}",
        };

    private static string Origin(PortfolioBackup backup) =>
        backup.Device is null ? string.Empty : $" from {backup.Device}";

    private static string Counts(PortfolioBackup backup) =>
        Counts(backup.PlatformCount, backup.ItemCount, backup.EntryCount);

    private static string Counts(IReadOnlyCollection<Platform> platforms) =>
        Counts(
            platforms.Count,
            platforms.Sum(p => p.FinancialItems.Count),
            platforms.Sum(p => p.FinancialItems.Sum(i => i.Entries.Count))
        );

    private static string Counts(PortfolioSnapshot snapshot) =>
        Counts(
            snapshot.Platforms.Count,
            snapshot.Platforms.Sum(p => p.FinancialItems.Count),
            snapshot.Platforms.Sum(p => p.FinancialItems.Sum(i => i.Entries.Count))
        );

    private static string Counts(int platforms, int holdings, int entries) =>
        $"{Plural(platforms, "platform", "platforms")} · {Plural(holdings, "holding", "holdings")} · {Plural(entries, "entry", "entries")}";

    private static string Plural(int count, string one, string many) =>
        count == 1 ? $"1 {one}" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} {many}";
}
