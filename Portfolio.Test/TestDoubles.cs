using Portfolio.Services;
using Portfolio.Storage;
using Portfolio.Stores;

namespace Portfolio.Test;

/// <summary>File pickers that hand over what a test sets up instead of showing a dialog.</summary>
internal sealed class FakeBackupFiles : IBackupFiles
{
    /// <summary>What the next open returns; null plays the person cancelling.</summary>
    public string? FileToOpen { get; set; }

    /// <summary>When true, the next save plays the person cancelling.</summary>
    public bool CancelSave { get; set; }

    public string? SavedJson { get; private set; }

    public string? SavedName { get; private set; }

    public Task<string?> SaveAsync(string suggestedFileName, string json)
    {
        if (CancelSave)
            return Task.FromResult<string?>(null);

        SavedJson = json;
        SavedName = suggestedFileName;
        return Task.FromResult<string?>(suggestedFileName);
    }

    public Task<string?> OpenAsync() => Task.FromResult(FileToOpen);
}

/// <summary>A clock that only moves when told to, in UTC so dates do not depend on the machine.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>A blob store whose writes can be made to fail, to prove a failure changes nothing.</summary>
internal sealed class FlakyBlobStore(string? initialContent = null) : IPortfolioBlobStore
{
    private readonly InMemoryBlobStore _inner = new(initialContent);

    public bool FailWrites { get; set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(cancellationToken);

    public Task WriteAsync(string content, CancellationToken cancellationToken = default) =>
        FailWrites
            ? throw new IOException("The disk is full.")
            : _inner.WriteAsync(content, cancellationToken);
}

/// <summary>Device storage whose blobs can be made to fail writing, one name at a time.</summary>
internal sealed class FlakyDeviceStorage(string deviceName = "Test device") : IDeviceStorage
{
    private readonly Dictionary<string, FlakyBlobStore> _blobs = [];

    public string DeviceName { get; } = deviceName;

    public IPortfolioBlobStore Open(string name) => Blob(name);

    public FlakyBlobStore Blob(string name)
    {
        if (!_blobs.TryGetValue(name, out var blob))
            _blobs[name] = blob = new FlakyBlobStore();

        return blob;
    }
}

/// <summary>
/// One device as the app runs it — a JSON repository, the store, the backup service — so sync tests
/// read as two devices handing a file to each other.
/// </summary>
internal sealed class TestDevice
{
    private TestDevice(string name, PortfolioSnapshot seed, TestClock clock)
    {
        PortfolioBlob = new FlakyBlobStore();
        Repository = new JsonPortfolioRepository(PortfolioBlob) { SeedFactory = () => seed };
        Store = new PortfolioStore(Repository);
        Storage = new FlakyDeviceStorage(name);
        Backup = new BackupService(Store, Storage, clock);
    }

    public FlakyBlobStore PortfolioBlob { get; }

    public JsonPortfolioRepository Repository { get; }

    public PortfolioStore Store { get; }

    public FlakyDeviceStorage Storage { get; }

    public BackupService Backup { get; }

    public static async Task<TestDevice> StartAsync(
        string name,
        TestClock clock,
        PortfolioSnapshot? seed = null
    )
    {
        var device = new TestDevice(name, seed ?? new PortfolioSnapshot(), clock);
        await device.Store.LoadAsync();
        await device.Backup.LoadAsync();
        return device;
    }

    /// <summary>Exports and records it, as a save that succeeded would. Returns the file's text.</summary>
    public async Task<string> ExportAsync()
    {
        var export = Backup.CreateExport();
        await Backup.RecordExportAsync(export);
        return export.Json;
    }

    public string Fingerprint => PortfolioTransfer.Fingerprint(Store.Platforms);
}
