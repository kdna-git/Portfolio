using Microsoft.EntityFrameworkCore;
using Portfolio.Domain.DTOs;
using Portfolio.Domain.Models;
using Portfolio.Domain.Persistence;

namespace Portfolio.EntityFramework;

/// <summary>
/// SQLite-backed repository for the platforms that have a real filesystem (desktop, Android, iOS).
/// </summary>
public sealed class EfPortfolioRepository(PortfolioDbContextFactory dbContextFactory)
    : IPortfolioRepository
{
    public async Task<IReadOnlyList<Platform>> GetPlatformsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var platforms = await dbContext
            .Platforms.AsNoTracking()
            .Include(p => p.FinancialItems)
            .ThenInclude(i => i.Entries)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. platforms.Select(p => p.ToModel())];
    }

    public async Task<Platform> AddPlatformAsync(
        Platform platform,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = new PlatformDto { Name = platform.Name };
        dbContext.Platforms.Add(dto);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new Platform(dto.Id, dto.Name);
    }

    public async Task UpdatePlatformAsync(
        Platform platform,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = await dbContext
            .Platforms.FirstOrDefaultAsync(p => p.Id == platform.Id, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            // The row vanished (deleted on another device, or a stale screen). Re-create it rather
            // than discarding what the user just typed.
            dbContext.Platforms.Add(new PlatformDto { Name = platform.Name });
        }
        else
        {
            dto.Name = platform.Name;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePlatformAsync(
        long platformId,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        await dbContext
            .Platforms.Where(p => p.Id == platformId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FinancialItem> AddFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = new FinancialItemDto
        {
            Name = financialItem.Name,
            Type = financialItem.Type,
            PlatformId = financialItem.PlatformId,
        };

        dbContext.FinancialItems.Add(dto);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new FinancialItem(dto.Id, dto.PlatformId, dto.Type, dto.Name);
    }

    public async Task UpdateFinancialItemAsync(
        FinancialItem financialItem,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = await dbContext
            .FinancialItems.FirstOrDefaultAsync(i => i.Id == financialItem.Id, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            dbContext.FinancialItems.Add(
                new FinancialItemDto
                {
                    Name = financialItem.Name,
                    Type = financialItem.Type,
                    PlatformId = financialItem.PlatformId,
                }
            );
        }
        else
        {
            // Only the item's own columns are touched: its entries are rows of their own and must
            // not be rewritten because the name changed.
            dto.Name = financialItem.Name;
            dto.Type = financialItem.Type;
            dto.PlatformId = financialItem.PlatformId;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteFinancialItemAsync(
        long financialItemId,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        await dbContext
            .FinancialItems.Where(i => i.Id == financialItemId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Entry> AddEntryAsync(
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = new EntryDto().Fill(entry);
        dbContext.Entries.Add(dto);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dto.ToModel();
    }

    public async Task UpdateEntryAsync(Entry entry, CancellationToken cancellationToken = default)
    {
        await using var dbContext = dbContextFactory.Create();

        var dto = await dbContext
            .Entries.FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
            dbContext.Entries.Add(new EntryDto().Fill(entry));
        else
            dto.Fill(entry);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteEntryAsync(long entryId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = dbContextFactory.Create();

        await dbContext
            .Entries.Where(e => e.Id == entryId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReplaceAllAsync(
        IReadOnlyList<Platform> platforms,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = dbContextFactory.Create();

        // One transaction around the deletes and the inserts: if any insert fails, the rollback
        // brings back every row the deletes removed.
        await using var transaction = await dbContext
            .Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Children first, so the deletes do not depend on the cascade being configured.
        await dbContext.Entries.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.FinancialItems.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Platforms.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // The ids come along: SQLite accepts explicit values for an integer key, and keeping them
        // means the data on both devices is the same down to the row.
        dbContext.Platforms.AddRange(platforms.Select(p => p.ToDto(includeId: true)));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
