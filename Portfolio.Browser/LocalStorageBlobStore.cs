using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Portfolio.Storage;

namespace Portfolio.Browser;

/// <summary>
/// Stores the portfolio document in the browser's <c>localStorage</c>.
/// </summary>
/// <remarks>
/// Survives a reload and needs no server. The quota is a few megabytes, far more than a portfolio of
/// JSON needs, but a write can still fail — private browsing refuses it, and so does a full quota — so
/// failures surface as a message rather than taking the app down with them.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class LocalStorageBlobStore(string key) : IPortfolioBlobStore
{
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var value = LocalStorageInterop.Read(key);
            return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
        }
        catch (JSException)
        {
            // Site data is blocked. The app runs from memory for this session.
            return Task.FromResult<string?>(null);
        }
    }

    public Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            LocalStorageInterop.Write(key, content);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException(
                "The browser refused to save. Private browsing and a full storage quota both do this.",
                exception
            );
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// This browser's sync record and restore point, as <c>localStorage</c> keys beside the portfolio.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class LocalStorageDeviceStorage(string keyPrefix, string deviceName) : IDeviceStorage
{
    public string DeviceName { get; } = deviceName;

    public IPortfolioBlobStore Open(string name) => new LocalStorageBlobStore(keyPrefix + name);
}

/// <summary>
/// Bound to the helpers <c>wwwroot/main.js</c> puts on <c>globalThis</c> rather than to
/// <c>localStorage</c>'s own methods, so the call does not depend on how the receiver is bound.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class LocalStorageInterop
{
    [JSImport("globalThis.portfolioStorage.read")]
    internal static partial string? Read(string key);

    [JSImport("globalThis.portfolioStorage.write")]
    internal static partial void Write(string key, string value);
}
