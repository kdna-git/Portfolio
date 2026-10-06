using Portfolio.Domain.Models;

namespace Portfolio.Domain.Persistence;

/// <summary>
/// Everything the app needs from storage, in one interface.
/// </summary>
/// <remarks>
/// The previous shape was twelve single-method interfaces that every platform head had to construct
/// and wire by hand, which is why the browser head shipped with no store at all. One interface means
/// one line per head and a compiler error if a head forgets.
/// </remarks>
public interface IPortfolioRepository
{
    /// <summary>Loads the whole portfolio. Called once at startup and on explicit refresh.</summary>
    Task<IReadOnlyList<Platform>> GetPlatformsAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a new platform and returns it with its assigned id.</summary>
    Task<Platform> AddPlatformAsync(
        Platform platform,
        CancellationToken cancellationToken = default
    );

    Task UpdatePlatformAsync(Platform platform, CancellationToken cancellationToken = default);

    /// <summary>Removes a platform together with its items and entries.</summary>
    Task DeletePlatformAsync(long platformId, CancellationToken cancellationToken = default);

    Task<FinancialItem> AddFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    );

    Task UpdateFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    );

    Task DeleteFinancialItemAsync(
        long financialItemId,
        CancellationToken cancellationToken = default
    );

    Task<Entry> AddEntryAsync(Entry entry, CancellationToken cancellationToken = default);

    Task UpdateEntryAsync(Entry entry, CancellationToken cancellationToken = default);

    Task DeleteEntryAsync(long entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the whole portfolio, keeping the ids it carries. All or nothing: if this throws,
    /// what was stored before is still there, untouched.
    /// </summary>
    /// <remarks>
    /// One call rather than a delete followed by hundreds of adds, which would leave half a
    /// portfolio behind if any one of them failed — the opposite of what an import is for.
    /// </remarks>
    Task ReplaceAllAsync(
        IReadOnlyList<Platform> platforms,
        CancellationToken cancellationToken = default
    );
}
