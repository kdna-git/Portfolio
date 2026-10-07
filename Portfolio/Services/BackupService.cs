using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Portfolio.Domain.Models;
using Portfolio.Storage;
using Portfolio.Stores;

namespace Portfolio.Services;

/// <summary>
/// Backup, restore, and the monthly sync between devices: export a file on one, import it on the
/// other.
/// </summary>
/// <remarks>
/// Two rules make it safe without ever merging. Whatever replaces this device's data first saves the
/// data it replaces as a restore point, so any import can be undone. And every export or import
/// records a fingerprint of the result, so the next import can tell whether this device changed in
/// between — the one mistake a "one device is the source of truth" sync invites.
/// </remarks>
public sealed class BackupService(PortfolioStore store, IDeviceStorage device, TimeProvider? clock = null)
{
    private const string SyncRecordName = "sync.json";
    private const string RestorePointName = "restore-point.json";

    private readonly PortfolioStore _store = store;
    private readonly IDeviceStorage _device = device;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string DeviceName => _device.DeviceName;

    /// <summary>This device's last export or import; null until there has been one.</summary>
    public SyncRecord? LastSync { get; private set; }

    /// <summary>The data from before the last import or restore; null until one has happened.</summary>
    public RestorePoint? RestorePoint { get; private set; }

    /// <summary>Raised after <see cref="LastSync"/> or <see cref="RestorePoint"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Reads this device's sync record and restore point. Called once at startup.</summary>
    public async Task LoadAsync()
    {
        LastSync = await ReadAsync(SyncRecordName, PortfolioJsonContext.Default.SyncRecord)
            .ConfigureAwait(true);
        RestorePoint = await ReadAsync(RestorePointName, PortfolioJsonContext.Default.RestorePoint)
            .ConfigureAwait(true);

        Changed?.Invoke();
    }

    /// <summary>
    /// The file to save. Nothing is recorded yet: an export only counts once the file exists, so
    /// <see cref="RecordExportAsync"/> follows a successful save and a cancelled one leaves no trace.
    /// </summary>
    public PortfolioExport CreateExport()
    {
        var now = _clock.GetLocalNow();

        return new PortfolioExport(
            PortfolioTransfer.Export(_store.Platforms, DeviceName, now),
            PortfolioTransfer.SuggestedFileName(DeviceName, now),
            PortfolioTransfer.Fingerprint(_store.Platforms),
            now
        );
    }

    public async Task RecordExportAsync(PortfolioExport export)
    {
        try
        {
            await WriteSyncAsync(
                    new SyncRecord
                    {
                        Direction = SyncDirection.Exported,
                        At = export.ExportedAt,
                        Fingerprint = export.Fingerprint,
                    }
                )
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            throw new BackupFailedException(
                $"The backup was saved, but this device could not record the export, so the next import here may warn about changes that are not there. {exception.Message}",
                exception
            );
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Reads and checks a backup and works out what importing it would do. Changes nothing; throws
    /// <see cref="PortfolioBackupException"/> for a file that cannot be imported.
    /// </summary>
    public ImportReview Review(string json) => Review(PortfolioTransfer.Read(json));

    public ImportReview Review(PortfolioBackup backup) =>
        ImportReview.Create(backup, _store.Platforms, LastSync);

    /// <summary>Replaces this device's data with the backup, saving a restore point first.</summary>
    public Task ImportAsync(PortfolioBackup backup) =>
        ReplaceAsync(
            backup.ToModel(),
            new SyncRecord
            {
                Direction = SyncDirection.Imported,
                At = _clock.GetLocalNow(),
                // The data after the import is the backup's, so its fingerprint is the backup's.
                Fingerprint = backup.Fingerprint,
                SourceDevice = backup.Device,
                SourceExportedAt = backup.ExportedAt,
            }
        );

    /// <summary>
    /// Puts the restore point's data back, together with the sync record it had. The data it replaces
    /// becomes the new restore point, so a restore can be undone the same way.
    /// </summary>
    public Task RestoreAsync()
    {
        var point =
            RestorePoint ?? throw new InvalidOperationException("There is no restore point.");

        return ReplaceAsync(point.Snapshot.ToModel(), point.Sync);
    }

    /// <summary>
    /// The one path that replaces data: restore point first, then the data, then the sync record
    /// that goes with the new data. The data is built by the caller before anything is written, so
    /// input that cannot be read fails while nothing has changed.
    /// </summary>
    private async Task ReplaceAsync(IReadOnlyList<Platform> platforms, SyncRecord? syncAfterwards)
    {
        try
        {
            await SaveRestorePointAsync().ConfigureAwait(true);

            try
            {
                await _store.ReplaceAllAsync(platforms).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                throw new BackupFailedException(
                    $"Nothing was changed: the data could not be replaced. {exception.Message}",
                    exception
                );
            }

            try
            {
                await WriteSyncAsync(syncAfterwards).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                throw new BackupFailedException(
                    $"The data was replaced, but this device could not record it, so the next import may warn about changes that are not there. {exception.Message}",
                    exception
                );
            }
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    private async Task SaveRestorePointAsync()
    {
        var point = new RestorePoint
        {
            SavedAt = _clock.GetLocalNow(),
            Snapshot = PortfolioSnapshot.FromModel(_store.Platforms),
            Sync = LastSync,
        };

        try
        {
            await _device
                .Open(RestorePointName)
                .WriteAsync(JsonSerializer.Serialize(point, PortfolioJsonContext.Default.RestorePoint))
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            throw new BackupFailedException(
                $"Nothing was changed: the current data could not be saved as a restore point first. {exception.Message}",
                exception
            );
        }

        // Kept even if the replacement that follows fails: the data is then still what this restore
        // point holds, so it guards nothing, but it is never wrong.
        RestorePoint = point;
    }

    /// <summary>Writes the sync record; null clears it, for data that has never been synced.</summary>
    private async Task WriteSyncAsync(SyncRecord? record)
    {
        var json = record is null
            ? string.Empty
            : JsonSerializer.Serialize(record, PortfolioJsonContext.Default.SyncRecord);

        await _device.Open(SyncRecordName).WriteAsync(json).ConfigureAwait(true);
        LastSync = record;
    }

    private async Task<T?> ReadAsync<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            var json = await _device.Open(name).ReadAsync().ConfigureAwait(true);

            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (Exception exception)
        {
            // This is bookkeeping, not data: losing it costs a warning on the next import, and must
            // never stop the app from opening.
            System.Diagnostics.Trace.TraceError($"Could not read {name}: {exception}");
            return null;
        }
    }
}

/// <summary>A backup step that failed. The message says what did and did not happen, fit to show.</summary>
public sealed class BackupFailedException(string message, Exception innerException)
    : Exception(message, innerException);

/// <summary>A backup ready to be written, plus what is needed to record it once it has been.</summary>
public sealed record PortfolioExport(
    string Json,
    string FileName,
    string Fingerprint,
    DateTimeOffset ExportedAt
);
