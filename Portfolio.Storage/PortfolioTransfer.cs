using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Portfolio.Domain.Models;

namespace Portfolio.Storage;

/// <summary>
/// Turns the portfolio into a backup file, and a backup file back into a portfolio.
/// </summary>
/// <remarks>
/// The file is the same versioned <see cref="PortfolioSnapshot"/> the browser keeps in
/// <c>localStorage</c>, plus who wrote it and when: one format, so a backup is never a second thing
/// to keep in step. Reading is strict about structure and lenient about content. A file this app
/// wrote must always import, so nothing a user could have entered is rejected; anything that would
/// break storage — a duplicated id, a missing name — is, before a single row is touched.
/// </remarks>
public static class PortfolioTransfer
{
    /// <summary>
    /// Far beyond any real portfolio — ten thousand entries come to a few megabytes — so anything
    /// larger is the wrong file, and refusing it beats reading all of it into memory first.
    /// </summary>
    public const long MaxFileBytes = 32L * 1024 * 1024;

    public static string Export(
        IEnumerable<Platform> platforms,
        string device,
        DateTimeOffset exportedAt
    )
    {
        var snapshot = PortfolioSnapshot.FromModel(platforms);
        snapshot.ExportedAt = exportedAt;
        snapshot.Device = device;

        return JsonSerializer.Serialize(snapshot, PortfolioJsonContext.Default.PortfolioSnapshot);
    }

    /// <summary>
    /// "portfolio-2026-10-06-desktop.json": files sort by date and say which device wrote them.
    /// </summary>
    public static string SuggestedFileName(string device, DateTimeOffset exportedAt)
    {
        var date = exportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var slug = new string(
            [.. device.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]
        ).Trim('-');

        return slug.Length == 0 ? $"portfolio-{date}.json" : $"portfolio-{date}-{slug}.json";
    }

    /// <summary>
    /// Parses and checks a backup. Throws <see cref="PortfolioBackupException"/>, with a message fit
    /// to show, for anything that is not a complete backup this build can read.
    /// </summary>
    public static PortfolioBackup Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PortfolioBackupException("The file is empty.");

        if (json.Length > MaxFileBytes)
            throw TooLarge();

        var version = ReadVersion(json);

        if (version > PortfolioSnapshot.CurrentVersion)
        {
            throw new PortfolioBackupException(
                "This backup was made by a newer version of Portfolio. Update the app on this device, then import it again."
            );
        }

        // Older formats would be upgraded here. Version 1 is the only one there has been.

        PortfolioSnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize(
                json,
                PortfolioJsonContext.Default.PortfolioSnapshot
            );
        }
        catch (JsonException exception)
        {
            throw Damaged($"a value could not be read{Location(exception, withPath: true)}", exception);
        }

        if (snapshot is null)
            throw NotABackup();

        Validate(snapshot);
        return new PortfolioBackup(snapshot);
    }

    /// <summary>The message for a file over <see cref="MaxFileBytes"/>, for readers that check first.</summary>
    public static PortfolioBackupException TooLarge() =>
        new("The file is too large to be a Portfolio backup.");

    /// <summary>
    /// A hash of the portfolio's content. Equal data gives an equal fingerprint whatever order it
    /// was loaded in, which is what lets a device tell whether anything changed since it last synced.
    /// </summary>
    public static string Fingerprint(IEnumerable<Platform> platforms) =>
        Fingerprint(PortfolioSnapshot.FromModel(platforms));

    /// <inheritdoc cref="Fingerprint(IEnumerable{Platform})"/>
    public static string Fingerprint(PortfolioSnapshot snapshot)
    {
        // A canonical text rather than the JSON itself: the JSON keeps load order and a decimal's
        // scale, so 10 and 10.00 — the same amount — would hash differently.
        var text = new StringBuilder();

        foreach (var platform in snapshot.Platforms.OrderBy(p => p.Id))
        {
            text.Append("P|").Append(Canonical(platform.Id)).Append('|');
            AppendText(text, platform.Name);

            foreach (var item in platform.FinancialItems.OrderBy(i => i.Id))
            {
                text.Append("I|").Append(Canonical(item.Id)).Append('|').Append(item.Type).Append('|');
                AppendText(text, item.Name);

                foreach (var entry in item.Entries.OrderBy(e => e.Id))
                {
                    text.Append("E|")
                        .Append(Canonical(entry.Id))
                        .Append('|')
                        .Append(entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                    decimal[] amounts =
                    [
                        entry.Deposit,
                        entry.Withdrawal,
                        entry.Investment,
                        entry.PrincipalRepayment,
                        entry.Interest,
                        entry.Bonus,
                        entry.Tax,
                        entry.Fee,
                        entry.DefaultLoss,
                    ];

                    foreach (var amount in amounts)
                        text.Append('|').Append(Canonical(amount));

                    text.Append('|')
                        .Append(entry.MarketValue is { } value ? Canonical(value) : "-")
                        .Append('|');
                    AppendText(text, entry.Note);
                }
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static int ReadVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // A portfolio file says what it is. Without this check any JSON object would read as an
            // empty backup — and importing an empty backup deletes everything.
            if (
                root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(nameof(PortfolioSnapshot.Version), out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var number)
                && number >= 1
                && root.TryGetProperty(nameof(PortfolioSnapshot.Platforms), out var platforms)
                && platforms.ValueKind == JsonValueKind.Array
            )
            {
                return number;
            }
        }
        catch (JsonException exception)
        {
            throw new PortfolioBackupException(
                $"The file is not valid JSON{Location(exception, withPath: false)}. It may be incomplete, or not a backup at all.",
                exception
            );
        }

        throw NotABackup();
    }

    private static void Validate(PortfolioSnapshot snapshot)
    {
        var platformIds = new HashSet<long>();
        var itemIds = new HashSet<long>();
        var entryIds = new HashSet<long>();

        foreach (var platform in snapshot.Platforms)
        {
            if (platform is null)
                throw Damaged("it contains an empty platform record");

            if (platform.Id <= 0 || !platformIds.Add(platform.Id))
                throw Damaged($"platform id {platform.Id} is missing or used twice");

            if (platform.Name is null)
                throw Damaged($"platform {platform.Id} has no name");

            // A list written as null reads back as null; an empty list is what it means.
            platform.FinancialItems ??= [];

            foreach (var item in platform.FinancialItems)
            {
                if (item is null)
                    throw Damaged($"platform \"{platform.Name}\" contains an empty holding record");

                if (item.Id <= 0 || !itemIds.Add(item.Id))
                    throw Damaged($"holding id {item.Id} is missing or used twice");

                if (item.Name is null)
                    throw Damaged($"holding {item.Id} has no name");

                // A name the enum does not know fails while reading; a number it does not know
                // reads fine and has to be caught here.
                if (!Enum.IsDefined(item.Type))
                    throw Damaged($"holding \"{item.Name}\" has an unknown type");

                item.Entries ??= [];

                foreach (var entry in item.Entries)
                {
                    if (entry is null)
                        throw Damaged($"holding \"{item.Name}\" contains an empty entry record");

                    if (entry.Id <= 0 || !entryIds.Add(entry.Id))
                        throw Damaged($"entry id {entry.Id} is missing or used twice");
                }
            }
        }
    }

    private static PortfolioBackupException NotABackup() =>
        new("This file is not a Portfolio backup.");

    private static PortfolioBackupException Damaged(string detail, Exception? inner = null) =>
        new($"The backup is damaged: {detail}.", inner);

    private static string Location(JsonException exception, bool withPath)
    {
        if (exception.LineNumber is not { } line)
            return string.Empty;

        // Zero-based in the exception; editors count from one.
        var path = withPath && exception.Path is { Length: > 2 } p ? $", at {p[2..]}" : string.Empty;
        return $" (line {line + 1}{path})";
    }

    private static string Canonical(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Trailing zeros dropped and no exponent, so equal amounts always print the same.</summary>
    private static string Canonical(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>Length-prefixed, so no name or note can be mistaken for a field boundary.</summary>
    private static void AppendText(StringBuilder text, string? value) =>
        text.Append(Canonical(value?.Length ?? 0)).Append(':').Append(value).Append('\n');
}

/// <summary>A backup file that has been read and checked, ready to import.</summary>
public sealed class PortfolioBackup
{
    internal PortfolioBackup(PortfolioSnapshot snapshot)
    {
        Snapshot = snapshot;
        PlatformCount = snapshot.Platforms.Count;
        ItemCount = snapshot.Platforms.Sum(p => p.FinancialItems.Count);
        EntryCount = snapshot.Platforms.Sum(p => p.FinancialItems.Sum(i => i.Entries.Count));
        Fingerprint = PortfolioTransfer.Fingerprint(snapshot);
    }

    public PortfolioSnapshot Snapshot { get; }

    /// <summary>Null for a file written before backups carried a date.</summary>
    public DateTimeOffset? ExportedAt => Snapshot.ExportedAt;

    public string? Device => Snapshot.Device;

    public int PlatformCount { get; }

    public int ItemCount { get; }

    public int EntryCount { get; }

    public string Fingerprint { get; }

    /// <summary>Fresh model objects on every call, so nothing the app holds is shared with the file.</summary>
    public List<Platform> ToModel() => Snapshot.ToModel();
}

/// <summary>A file that cannot be imported. The message says why, in words fit to show.</summary>
public sealed class PortfolioBackupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
