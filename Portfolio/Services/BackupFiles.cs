using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Portfolio.Storage;

namespace Portfolio.Services;

/// <summary>
/// Where backup files go to and come from: the platform's own pickers, behind an interface so the
/// backup screen can be tested without a window.
/// </summary>
public interface IBackupFiles
{
    /// <summary>
    /// Asks where to save, then writes <paramref name="json"/> there. Returns the name the file was
    /// saved under, or null when the person cancelled.
    /// </summary>
    Task<string?> SaveAsync(string suggestedFileName, string json);

    /// <summary>
    /// Asks for a file and reads it. Null when the person cancelled. Anything over
    /// <see cref="PortfolioTransfer.MaxFileBytes"/> is refused before it is read in full.
    /// </summary>
    Task<string?> OpenAsync();
}

/// <summary>
/// The pickers Avalonia provides on every platform: the system dialog on desktop, the storage access
/// framework on Android — so Drive, Downloads or a USB stick all work — and the File System Access API
/// in the browser, which falls back to a download where that API is missing.
/// </summary>
public sealed class AvaloniaBackupFiles : IBackupFiles
{
    private static readonly FilePickerFileType BackupFileType = new("Portfolio backup")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
        AppleUniformTypeIdentifiers = ["public.json"],
    };

    public async Task<string?> SaveAsync(string suggestedFileName, string json)
    {
        var storage = GetStorageProvider();

        if (!storage.CanSave)
            throw new NotSupportedException("Saving files is not supported here.");

        using var file = await storage
            .SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export portfolio backup",
                    SuggestedFileName = suggestedFileName,
                    DefaultExtension = "json",
                    FileTypeChoices = [BackupFileType],
                    ShowOverwritePrompt = true,
                }
            )
            .ConfigureAwait(true);

        if (file is null)
            return null;

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        EmptyExisting(stream);

        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(json).ConfigureAwait(true);
        await writer.FlushAsync().ConfigureAwait(true);

        return file.Name;
    }

    public async Task<string?> OpenAsync()
    {
        var storage = GetStorageProvider();

        if (!storage.CanOpen)
            throw new NotSupportedException("Opening files is not supported here.");

        var files = await storage
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Import portfolio backup",
                    AllowMultiple = false,
                    // "All files" too: Drive and some file managers label JSON as plain text or as
                    // generic binary, and a JSON-only filter would grey the backup out on Android.
                    FileTypeFilter = [BackupFileType, FilePickerFileTypes.All],
                }
            )
            .ConfigureAwait(true);

        try
        {
            if (files.Count == 0)
                return null;

            var file = files[0];
            var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);

            if (properties.Size > PortfolioTransfer.MaxFileBytes)
                throw PortfolioTransfer.TooLarge();

            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return await ReadCappedAsync(reader).ConfigureAwait(true);
        }
        finally
        {
            // Releases the security-scoped access iOS and macOS grant for a picked file.
            foreach (var file in files)
                file.Dispose();
        }
    }

    /// <summary>
    /// Saving over an existing file does not empty it everywhere: the browser opens it keeping its
    /// bytes and writes over the start, so a backup shorter than the one it replaces would end with
    /// the tail of the old one — and no longer be valid JSON.
    /// </summary>
    private static void EmptyExisting(Stream stream)
    {
        try
        {
            stream.SetLength(0);
        }
        catch (NotSupportedException)
        {
            // Android's document streams cannot truncate, and need not: the picker there always
            // creates a new document rather than reopening an old one.
        }
    }

    /// <summary>
    /// Not every provider reports a size — a browser fallback, some Android document providers — so
    /// the read itself stops at the limit as well.
    /// </summary>
    private static async Task<string> ReadCappedAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[64 * 1024];
        int read;

        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(true)) > 0)
        {
            text.Append(buffer, 0, read);

            if (text.Length > PortfolioTransfer.MaxFileBytes)
                throw PortfolioTransfer.TooLarge();
        }

        return text.ToString();
    }

    private static IStorageProvider GetStorageProvider()
    {
        var topLevel = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
            _ => null,
        };

        return topLevel?.StorageProvider
            ?? throw new InvalidOperationException("There is no window to show a file picker in.");
    }
}
