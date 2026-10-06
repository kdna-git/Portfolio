using Portfolio.Domain.Models;

namespace Portfolio.Storage;

public enum SyncDirection
{
    Exported,
    Imported,
}

/// <summary>
/// The last time this device exchanged its data with a backup file, and what the data looked like
/// straight after. Kept per device; it is what lets the next import notice changes made since.
/// </summary>
public sealed class SyncRecord
{
    public SyncDirection Direction { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>
    /// <see cref="PortfolioTransfer.Fingerprint(PortfolioSnapshot)"/> of this device's data right
    /// after the exchange.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>For an import: the device the file came from.</summary>
    public string? SourceDevice { get; set; }

    /// <summary>For an import: when the file was exported.</summary>
    public DateTimeOffset? SourceExportedAt { get; set; }
}

/// <summary>
/// The data as it was just before an import or a restore replaced it, with the sync record that
/// went with it, so the replacement can be undone completely.
/// </summary>
/// <remarks>
/// Restoring swaps the two — the data being replaced becomes the new restore point — so an undo can
/// itself be undone, and no step ever needs to delete anything.
/// </remarks>
public sealed class RestorePoint
{
    public DateTimeOffset SavedAt { get; set; }

    public PortfolioSnapshot Snapshot { get; set; } = new();

    public SyncRecord? Sync { get; set; }
}

/// <summary>Data on this device that no backup holds, which an import would discard.</summary>
public enum UnsyncedChanges
{
    /// <summary>Nothing would be lost: the device is empty, or unchanged since its last sync.</summary>
    None,

    /// <summary>Changed since the last export or import.</summary>
    SinceLastSync,

    /// <summary>Holds data but has never exported or imported, so no file has a copy of it.</summary>
    NeverSynced,
}

/// <summary>What importing a backup would do to this device, worked out before anything is touched.</summary>
/// <remarks>
/// This is the whole of the sync logic, and it deliberately stops short of merging. With one device
/// treated as the source of truth each month, the only real risk is forgetting which side was edited
/// — and that only needs noticing, which a fingerprint per device does.
/// </remarks>
public sealed class ImportReview
{
    private ImportReview(
        PortfolioBackup backup,
        bool isAlreadyCurrent,
        UnsyncedChanges localChanges,
        SyncRecord? lastSync,
        DateTimeOffset? newestKnownExport
    )
    {
        Backup = backup;
        IsAlreadyCurrent = isAlreadyCurrent;
        LocalChanges = localChanges;
        LastSync = lastSync;
        NewestKnownExport = newestKnownExport;
    }

    public static ImportReview Create(
        PortfolioBackup backup,
        IReadOnlyCollection<Platform> local,
        SyncRecord? lastSync
    )
    {
        var localFingerprint = PortfolioTransfer.Fingerprint(local);

        var changes =
            local.Count == 0 ? UnsyncedChanges.None
            : lastSync is null ? UnsyncedChanges.NeverSynced
            : lastSync.Fingerprint != localFingerprint ? UnsyncedChanges.SinceLastSync
            : UnsyncedChanges.None;

        // The newest export this device has seen: the file it last imported, or its own export.
        var newestKnown =
            lastSync?.Direction == SyncDirection.Imported ? lastSync.SourceExportedAt : lastSync?.At;

        return new ImportReview(
            backup,
            backup.Fingerprint == localFingerprint,
            changes,
            lastSync,
            newestKnown
        );
    }

    public PortfolioBackup Backup { get; }

    /// <summary>The backup holds exactly what this device already has, so importing changes nothing.</summary>
    public bool IsAlreadyCurrent { get; }

    public UnsyncedChanges LocalChanges { get; }

    public SyncRecord? LastSync { get; }

    /// <summary>The export date of the newest backup this device has exchanged, if any.</summary>
    public DateTimeOffset? NewestKnownExport { get; }

    /// <summary>
    /// The backup was exported before the newest one this device has exchanged, so importing it
    /// would most likely wind the data back. A file with no date cannot be judged and is not flagged.
    /// </summary>
    public bool IsOlderThanLastSync =>
        Backup.ExportedAt is { } exported && NewestKnownExport is { } known && exported < known;

    /// <summary>True when the import should be confirmed as a deliberate overwrite, not routine.</summary>
    public bool NeedsCaution => LocalChanges != UnsyncedChanges.None || IsOlderThanLastSync;
}
