namespace Portfolio.Storage;

/// <summary>
/// Reads and writes one blob of text. The only thing each platform has to implement differently:
/// a file on desktop and Android, <c>localStorage</c> in the browser.
/// </summary>
public interface IPortfolioBlobStore
{
    /// <summary>The stored document, or null when nothing has been saved yet.</summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(string content, CancellationToken cancellationToken = default);
}

/// <summary>A blob kept in a file. Writes go to a temporary file first, then replace the original.</summary>
public sealed class FileBlobStore(string path) : IPortfolioBlobStore
{
    public string Path { get; } = path;

    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
            return null;

        return await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write-then-move, so a crash mid-save cannot leave a half-written portfolio behind.
        var temporary = Path + ".tmp";
        await File.WriteAllTextAsync(temporary, content, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, Path, overwrite: true);
    }
}

/// <summary>A blob kept in memory. Used by tests and by previews.</summary>
public sealed class InMemoryBlobStore(string? initialContent = null) : IPortfolioBlobStore
{
    private string? _content = initialContent;

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_content);

    public Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        _content = content;
        return Task.CompletedTask;
    }
}
