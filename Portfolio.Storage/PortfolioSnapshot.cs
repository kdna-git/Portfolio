using System.Text.Json.Serialization;
using Portfolio.Domain.Models;

namespace Portfolio.Storage;

/// <summary>
/// The whole portfolio in a form that serialises cleanly. Deliberately separate from the EF DTOs:
/// no navigation properties, no cycles, and a version number so an old file can be migrated rather
/// than rejected.
/// </summary>
public sealed class PortfolioSnapshot
{
    /// <summary>Bumped when the shape changes; <see cref="CurrentVersion"/> is what this build writes.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// When this snapshot was written out as a backup file. Null for the copy a device keeps for
    /// itself. Optional fields rather than a version bump: a reader that predates them skips them.
    /// </summary>
    public DateTimeOffset? ExportedAt { get; set; }

    /// <summary>The device that wrote the backup ("Desktop", "Android"), so an import can say so.</summary>
    public string? Device { get; set; }

    public List<PlatformRecord> Platforms { get; set; } = [];

    public static PortfolioSnapshot FromModel(IEnumerable<Platform> platforms) =>
        new()
        {
            Version = CurrentVersion,
            Platforms =
            [
                .. platforms.Select(p => new PlatformRecord
                {
                    Id = p.Id,
                    Name = p.Name,
                    FinancialItems =
                    [
                        .. p.FinancialItems.Select(i => new FinancialItemRecord
                        {
                            Id = i.Id,
                            Name = i.Name,
                            Type = i.Type,
                            Entries = [.. i.Entries.Select(EntryRecord.FromModel)],
                        }),
                    ],
                }),
            ],
        };

    public List<Platform> ToModel() =>
        [
            .. Platforms.Select(p => new Platform(
                p.Id,
                p.Name,
                p.FinancialItems.Select(i => new FinancialItem(
                    i.Id,
                    p.Id,
                    i.Type,
                    i.Name,
                    i.Entries.Select(e => e.ToModel(i.Id))
                ))
            )),
        ];

    /// <summary>Highest id in use, so new records can continue the sequence.</summary>
    public (long Platform, long Item, long Entry) MaxIds()
    {
        long platform = 0;
        long item = 0;
        long entry = 0;

        foreach (var p in Platforms)
        {
            platform = Math.Max(platform, p.Id);

            foreach (var i in p.FinancialItems)
            {
                item = Math.Max(item, i.Id);

                foreach (var e in i.Entries)
                    entry = Math.Max(entry, e.Id);
            }
        }

        return (platform, item, entry);
    }
}

public sealed class PlatformRecord
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<FinancialItemRecord> FinancialItems { get; set; } = [];
}

public sealed class FinancialItemRecord
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public InvestmentType Type { get; set; }

    public List<EntryRecord> Entries { get; set; } = [];
}

public sealed class EntryRecord
{
    public long Id { get; set; }

    public DateOnly Date { get; set; }

    public string? Note { get; set; }

    public decimal Deposit { get; set; }

    public decimal Withdrawal { get; set; }

    public decimal Investment { get; set; }

    public decimal PrincipalRepayment { get; set; }

    public decimal Interest { get; set; }

    public decimal Bonus { get; set; }

    public decimal Tax { get; set; }

    public decimal Fee { get; set; }

    public decimal DefaultLoss { get; set; }

    public decimal? MarketValue { get; set; }

    public static EntryRecord FromModel(Entry entry) =>
        new()
        {
            Id = entry.Id,
            Date = entry.Date,
            Note = entry.Note,
            Deposit = entry.Deposit,
            Withdrawal = entry.Withdrawal,
            Investment = entry.Investment,
            PrincipalRepayment = entry.PrincipalRepayment,
            Interest = entry.Interest,
            Bonus = entry.Bonus,
            Tax = entry.Tax,
            Fee = entry.Fee,
            DefaultLoss = entry.DefaultLoss,
            MarketValue = entry.MarketValue,
        };

    public Entry ToModel(long financialItemId) =>
        new(
            Id,
            financialItemId,
            Date,
            Note,
            Deposit,
            Withdrawal,
            Investment,
            PrincipalRepayment,
            Interest,
            Bonus,
            Tax,
            Fee,
            DefaultLoss,
            MarketValue
        );
}

/// <summary>
/// Source-generated serialisation. WebAssembly builds get trimmed hard, and reflection-based
/// serialisation is the first thing to break there; this keeps the browser head working.
/// </summary>
/// <remarks>
/// Enums are written by name. A backup is a file people open and read, and a name survives the
/// enum being reordered where a number would silently change meaning. Numbers are still read, so
/// documents saved before this keep loading.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(PortfolioSnapshot))]
[JsonSerializable(typeof(SyncRecord))]
[JsonSerializable(typeof(RestorePoint))]
public sealed partial class PortfolioJsonContext : JsonSerializerContext;
