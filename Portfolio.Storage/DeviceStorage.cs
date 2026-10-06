namespace Portfolio.Storage;

/// <summary>
/// A device's private storage for the small documents kept beside the portfolio: the record of the
/// last sync and the restore point taken before an import.
/// </summary>
/// <remarks>
/// Separate from the repository because these belong to the device, not to the data: an import
/// replaces the portfolio but must leave its own bookkeeping where it was.
/// </remarks>
public interface IDeviceStorage
{
    /// <summary>Names this device in backup files and sync records: "Desktop", "Android", "Browser".</summary>
    string DeviceName { get; }

    /// <summary>
    /// The blob called <paramref name="name"/>. Writing empty content clears it: every store already
    /// reads an empty blob as no blob, so clearing needs no extra operation on every platform.
    /// </summary>
    IPortfolioBlobStore Open(string name);
}

/// <summary>Files in a directory: the app data folder on desktop, Android and iOS.</summary>
public sealed class FileDeviceStorage(string directory, string deviceName) : IDeviceStorage
{
    public string DeviceName { get; } = deviceName;

    public IPortfolioBlobStore Open(string name) => new FileBlobStore(Path.Combine(directory, name));
}

/// <summary>Blobs in memory. Used by tests and by previews.</summary>
public sealed class InMemoryDeviceStorage(string deviceName = "Test device") : IDeviceStorage
{
    private readonly Dictionary<string, InMemoryBlobStore> _blobs = [];

    public string DeviceName { get; } = deviceName;

    public IPortfolioBlobStore Open(string name)
    {
        if (!_blobs.TryGetValue(name, out var blob))
            _blobs[name] = blob = new InMemoryBlobStore();

        return blob;
    }
}
