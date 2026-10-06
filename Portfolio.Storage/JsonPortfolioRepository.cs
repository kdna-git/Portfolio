using System.Text.Json;
using Portfolio.Domain.Models;
using Portfolio.Domain.Persistence;

namespace Portfolio.Storage;

/// <summary>
/// A repository backed by a single JSON document.
/// </summary>
/// <remarks>
/// This is what makes the browser head work. SQLite in WebAssembly needs a native build plus a
/// mounted virtual filesystem to survive a reload; a JSON document in <c>localStorage</c> needs
/// neither, and the portfolio is small enough that rewriting the whole document on each change is
/// cheaper than the machinery a database would cost. Desktop and Android keep using SQLite.
/// </remarks>
public sealed class JsonPortfolioRepository(IPortfolioBlobStore blobStore) : IPortfolioRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PortfolioSnapshot? _snapshot;
    private long _nextPlatformId;
    private long _nextItemId;
    private long _nextEntryId;

    /// <summary>
    /// Called with a freshly created snapshot the first time the store is opened and nothing was
    /// saved. Use it to seed demo data.
    /// </summary>
    public Func<PortfolioSnapshot>? SeedFactory { get; init; }

    /// <summary>
    /// Where a stored document that cannot be read is copied before anything can overwrite it, so
    /// it can still be recovered by hand.
    /// </summary>
    public IPortfolioBlobStore? DamagedCopyStore { get; init; }

    public async Task<IReadOnlyList<Platform>> GetPlatformsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.ToModel();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<Platform> AddPlatformAsync(
        Platform platform,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                var record = new PlatformRecord
                {
                    Id = ++_nextPlatformId,
                    Name = platform.Name,
                };
                snapshot.Platforms.Add(record);
                return new Platform(record.Id, record.Name);
            },
            cancellationToken
        );

    public Task UpdatePlatformAsync(
        Platform platform,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                var record = snapshot.Platforms.FirstOrDefault(p => p.Id == platform.Id);

                if (record is null)
                {
                    // Treat an update of something that is gone as an insert rather than throwing:
                    // the alternative is losing the user's edit.
                    snapshot.Platforms.Add(
                        new PlatformRecord { Id = platform.Id, Name = platform.Name }
                    );
                    return true;
                }

                record.Name = platform.Name;
                return true;
            },
            cancellationToken
        );

    public Task DeletePlatformAsync(
        long platformId,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                snapshot.Platforms.RemoveAll(p => p.Id == platformId);
                return true;
            },
            cancellationToken
        );

    public Task<FinancialItem> AddFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                var platform =
                    snapshot.Platforms.FirstOrDefault(p => p.Id == financialItem.PlatformId)
                    ?? throw new InvalidOperationException(
                        $"Platform {financialItem.PlatformId} does not exist."
                    );

                var record = new FinancialItemRecord
                {
                    Id = ++_nextItemId,
                    Name = financialItem.Name,
                    Type = financialItem.Type,
                };
                platform.FinancialItems.Add(record);

                return new FinancialItem(
                    record.Id,
                    platform.Id,
                    record.Type,
                    record.Name
                );
            },
            cancellationToken
        );

    public Task UpdateFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                var (owner, record) = FindItem(snapshot, financialItem.Id);

                if (record is null)
                {
                    var platform = snapshot.Platforms.FirstOrDefault(p =>
                        p.Id == financialItem.PlatformId
                    );
                    platform?.FinancialItems.Add(
                        new FinancialItemRecord
                        {
                            Id = financialItem.Id,
                            Name = financialItem.Name,
                            Type = financialItem.Type,
                        }
                    );
                    return true;
                }

                record.Name = financialItem.Name;
                record.Type = financialItem.Type;

                // Moving an item to another platform keeps its entries with it.
                if (owner is not null && owner.Id != financialItem.PlatformId)
                {
                    var target = snapshot.Platforms.FirstOrDefault(p =>
                        p.Id == financialItem.PlatformId
                    );

                    if (target is not null)
                    {
                        owner.FinancialItems.Remove(record);
                        target.FinancialItems.Add(record);
                    }
                }

                return true;
            },
            cancellationToken
        );

    public Task DeleteFinancialItemAsync(
        long financialItemId,
        CancellationToken cancellationToken = default
    ) =>
        MutateAsync(
            snapshot =>
            {
                foreach (var platform in snapshot.Platforms)
                    platform.FinancialItems.RemoveAll(i => i.Id == financialItemId);

                return true;
            },
            cancellationToken
        );

    public Task<Entry> AddEntryAsync(Entry entry, CancellationToken cancellationToken = default) =>
        MutateAsync(
            snapshot =>
            {
                var (_, item) = FindItem(snapshot, entry.FinancialItemId);

                if (item is null)
                    throw new InvalidOperationException(
                        $"Financial item {entry.FinancialItemId} does not exist."
                    );

                var stored = entry.With(id: ++_nextEntryId);
                item.Entries.Add(EntryRecord.FromModel(stored));
                return stored;
            },
            cancellationToken
        );

    public Task UpdateEntryAsync(Entry entry, CancellationToken cancellationToken = default) =>
        MutateAsync(
            snapshot =>
            {
                var (_, item) = FindItem(snapshot, entry.FinancialItemId);

                if (item is null)
                    return true;

                item.Entries.RemoveAll(e => e.Id == entry.Id);
                item.Entries.Add(EntryRecord.FromModel(entry));

                // An entry can be moved to a different holding; drop the stale copy elsewhere.
                foreach (var other in AllItems(snapshot))
                {
                    if (!ReferenceEquals(other, item))
                        other.Entries.RemoveAll(e => e.Id == entry.Id);
                }

                return true;
            },
            cancellationToken
        );

    public Task DeleteEntryAsync(long entryId, CancellationToken cancellationToken = default) =>
        MutateAsync(
            snapshot =>
            {
                foreach (var item in AllItems(snapshot))
                    item.Entries.RemoveAll(e => e.Id == entryId);

                return true;
            },
            cancellationToken
        );

    public async Task ReplaceAllAsync(
        IReadOnlyList<Platform> platforms,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var snapshot = PortfolioSnapshot.FromModel(platforms);

            // Saved before it is adopted: if the write fails, the old portfolio is still the one in
            // memory and in storage, so a failed import changes nothing.
            await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            Adopt(snapshot);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IEnumerable<FinancialItemRecord> AllItems(PortfolioSnapshot snapshot) =>
        snapshot.Platforms.SelectMany(p => p.FinancialItems);

    private static (PlatformRecord? Owner, FinancialItemRecord? Item) FindItem(
        PortfolioSnapshot snapshot,
        long itemId
    )
    {
        foreach (var platform in snapshot.Platforms)
        {
            var item = platform.FinancialItems.FirstOrDefault(i => i.Id == itemId);

            if (item is not null)
                return (platform, item);
        }

        return (null, null);
    }

    private async Task<T> MutateAsync<T>(
        Func<PortfolioSnapshot, T> mutate,
        CancellationToken cancellationToken
    )
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var result = mutate(snapshot);
            await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PortfolioSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (_snapshot is not null)
            return _snapshot;

        var json = await blobStore.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
        {
            // Nothing was ever saved. Seed, and save straight away so the next open finds it.
            var seeded = SeedFactory?.Invoke() ?? new PortfolioSnapshot();
            Adopt(seeded);
            await SaveAsync(seeded, cancellationToken).ConfigureAwait(false);
            return seeded;
        }

        PortfolioSnapshot snapshot;

        try
        {
            snapshot =
                JsonSerializer.Deserialize(json, PortfolioJsonContext.Default.PortfolioSnapshot)
                ?? throw new JsonException("The stored document is null.");
        }
        catch (JsonException)
        {
            // A damaged document must not brick the app, and must not be lost either: it may be the
            // only copy of someone's data. It is copied aside and left where it is — nothing is
            // saved here, so only the first real change replaces it. The app starts empty rather
            // than seeded: demo data mixed into a recovery would be worse than nothing.
            await KeepDamagedCopyAsync(json, cancellationToken).ConfigureAwait(false);
            snapshot = new PortfolioSnapshot();
        }

        Adopt(snapshot);
        return snapshot;
    }

    private async Task KeepDamagedCopyAsync(string json, CancellationToken cancellationToken)
    {
        if (DamagedCopyStore is null)
            return;

        try
        {
            await DamagedCopyStore.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Failing to make the copy must not stop the app opening; the original is still in place.
            System.Diagnostics.Trace.TraceError($"Could not keep a copy of the damaged portfolio: {exception}");
        }
    }

    /// <summary>Makes <paramref name="snapshot"/> current, continuing the id sequence from it.</summary>
    private void Adopt(PortfolioSnapshot snapshot)
    {
        (_nextPlatformId, _nextItemId, _nextEntryId) = snapshot.MaxIds();
        _snapshot = snapshot;
    }

    private async Task SaveAsync(PortfolioSnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Version = PortfolioSnapshot.CurrentVersion;
        var json = JsonSerializer.Serialize(snapshot, PortfolioJsonContext.Default.PortfolioSnapshot);
        await blobStore.WriteAsync(json, cancellationToken).ConfigureAwait(false);
    }
}
