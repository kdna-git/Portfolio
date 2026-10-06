using Portfolio.Domain.Models;
using Portfolio.Domain.Persistence;

namespace Portfolio.Stores;

/// <summary>
/// The app's single copy of the portfolio: loaded once, mutated through here, and broadcast to every
/// screen with one event.
/// </summary>
/// <remarks>
/// One <see cref="Changed"/> event rather than nine typed ones. Nine meant every screen had to
/// subscribe to, and unsubscribe from, the right subset — and a screen that forgot one silently
/// showed stale numbers. Since any change can alter any total, every screen wants to hear about all
/// of them anyway.
/// </remarks>
public sealed class PortfolioStore(IPortfolioRepository repository)
{
    private readonly List<Platform> _platforms = [];
    private PortfolioRoot? _root;

    public IReadOnlyList<Platform> Platforms => _platforms;

    /// <summary>The whole portfolio as an analysable node. Rebuilt whenever the data changes.</summary>
    public PortfolioRoot Root => _root ??= new PortfolioRoot(_platforms);

    public IReadOnlyList<FinancialItem> FinancialItems =>
        [.. _platforms.SelectMany(p => p.FinancialItems)];

    public bool IsLoaded { get; private set; }

    /// <summary>Raised after a successful load or mutation.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when a write fails, with a message fit to show the user. Screens surface it instead of
    /// the app dying on a storage error.
    /// </summary>
    public event Action<string>? OperationFailed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var platforms = await repository.GetPlatformsAsync(cancellationToken).ConfigureAwait(false);

        _platforms.Clear();
        _platforms.AddRange(platforms);
        IsLoaded = true;
        Invalidate();
    }

    public Task AddPlatformAsync(Platform platform) =>
        RunAsync(
            async () =>
            {
                var saved = await repository.AddPlatformAsync(platform).ConfigureAwait(false);
                _platforms.Add(saved);
            },
            "Could not save the platform."
        );

    public Task UpdatePlatformAsync(Platform platform) =>
        RunAsync(
            async () =>
            {
                await repository.UpdatePlatformAsync(platform).ConfigureAwait(false);

                var index = _platforms.FindIndex(p => p.Id == platform.Id);

                // Rebuild from the existing holdings so renaming a platform cannot drop them.
                var replacement = new Platform(
                    platform.Id,
                    platform.Name,
                    index >= 0 ? _platforms[index].FinancialItems : platform.FinancialItems
                );

                if (index >= 0)
                    _platforms[index] = replacement;
                else
                    _platforms.Add(replacement);
            },
            "Could not update the platform."
        );

    public Task DeletePlatformAsync(long platformId) =>
        RunAsync(
            async () =>
            {
                await repository.DeletePlatformAsync(platformId).ConfigureAwait(false);
                _platforms.RemoveAll(p => p.Id == platformId);
            },
            "Could not delete the platform."
        );

    public Task AddFinancialItemAsync(FinancialItem item) =>
        RunAsync(
            async () =>
            {
                var platform = FindPlatform(item.PlatformId);

                if (platform is null)
                    throw new InvalidOperationException("That platform no longer exists.");

                var saved = await repository.AddFinancialItemAsync(item).ConfigureAwait(false);
                platform.AddFinancialItem(saved);
            },
            "Could not save the financial item."
        );

    public Task UpdateFinancialItemAsync(FinancialItem item) =>
        RunAsync(
            async () =>
            {
                await repository.UpdateFinancialItemAsync(item).ConfigureAwait(false);

                var existing = FindFinancialItem(item.Id);
                var owner = existing is null ? null : FindPlatform(existing.PlatformId);

                // Keep the entries: the editor only knows about the item's own fields.
                var replacement = new FinancialItem(
                    item.Id,
                    item.PlatformId,
                    item.Type,
                    item.Name,
                    existing?.Entries ?? item.Entries
                );

                owner?.RemoveFinancialItem(item.Id);
                FindPlatform(item.PlatformId)?.AddFinancialItem(replacement);
            },
            "Could not update the financial item."
        );

    public Task DeleteFinancialItemAsync(long itemId) =>
        RunAsync(
            async () =>
            {
                await repository.DeleteFinancialItemAsync(itemId).ConfigureAwait(false);

                foreach (var platform in _platforms)
                    platform.RemoveFinancialItem(itemId);
            },
            "Could not delete the financial item."
        );

    public Task AddEntryAsync(Entry entry) =>
        RunAsync(
            async () =>
            {
                var item = FindFinancialItem(entry.FinancialItemId);

                if (item is null)
                    throw new InvalidOperationException("That financial item no longer exists.");

                var saved = await repository.AddEntryAsync(entry).ConfigureAwait(false);
                item.AddEntry(saved);
            },
            "Could not save the entry."
        );

    public Task UpdateEntryAsync(Entry entry) =>
        RunAsync(
            async () =>
            {
                await repository.UpdateEntryAsync(entry).ConfigureAwait(false);

                // An entry can be reassigned to another holding, so remove it everywhere first.
                foreach (var item in FinancialItems)
                    item.RemoveEntry(entry.Id);

                FindFinancialItem(entry.FinancialItemId)?.AddEntry(entry);
            },
            "Could not update the entry."
        );

    public Task DeleteEntryAsync(long entryId) =>
        RunAsync(
            async () =>
            {
                await repository.DeleteEntryAsync(entryId).ConfigureAwait(false);

                foreach (var item in FinancialItems)
                    item.RemoveEntry(entryId);
            },
            "Could not delete the entry."
        );

    /// <summary>
    /// Replaces the whole portfolio, taking ownership of <paramref name="platforms"/>. Unlike the
    /// single-record edits this throws on failure instead of reporting through
    /// <see cref="OperationFailed"/>: an import has to know whether it happened before it can record
    /// that it did. When it throws, nothing has changed, in storage or here.
    /// </summary>
    public async Task ReplaceAllAsync(IReadOnlyList<Platform> platforms)
    {
        // Continuing on the captured context matters: Changed reaches every open screen, and they
        // must be refreshed on the UI thread.
        await repository.ReplaceAllAsync(platforms).ConfigureAwait(true);

        _platforms.Clear();
        _platforms.AddRange(platforms);
        Invalidate();
    }

    public Platform? FindPlatform(long id) => _platforms.FirstOrDefault(p => p.Id == id);

    public FinancialItem? FindFinancialItem(long id) =>
        _platforms.SelectMany(p => p.FinancialItems).FirstOrDefault(i => i.Id == id);

    public Platform? FindPlatformOf(long financialItemId) =>
        _platforms.FirstOrDefault(p => p.FinancialItems.Any(i => i.Id == financialItemId));

    private async Task RunAsync(Func<Task> action, string failureMessage)
    {
        try
        {
            await action().ConfigureAwait(true);
            Invalidate();
        }
        catch (Exception exception)
        {
            // Storage failures are expected (a locked file, a cleared browser store); losing the app
            // over one is not. Report and carry on with the in-memory state untouched.
            OperationFailed?.Invoke($"{failureMessage} {exception.Message}");
        }
    }

    private void Invalidate()
    {
        _root = null;
        Changed?.Invoke();
    }
}
