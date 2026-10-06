using Microsoft.EntityFrameworkCore;
using Portfolio.Domain.DTOs;

namespace Portfolio.EntityFramework;

public sealed class PortfolioDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<EntryDto> Entries => Set<EntryDto>();

    public DbSet<FinancialItemDto> FinancialItems => Set<FinancialItemDto>();

    public DbSet<PlatformDto> Platforms => Set<PlatformDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlatformDto>(platform =>
        {
            platform.Property(p => p.Name).IsRequired().HasMaxLength(200);

            // Deleting a platform must take its holdings and their entries with it, otherwise the
            // database keeps rows nothing can reach.
            platform
                .HasMany(p => p.FinancialItems)
                .WithOne(i => i.Platform!)
                .HasForeignKey(i => i.PlatformId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinancialItemDto>(item =>
        {
            item.Property(i => i.Name).IsRequired().HasMaxLength(200);
            item.HasIndex(i => i.PlatformId);

            item.HasMany(i => i.Entries)
                .WithOne(e => e.FinancialItem!)
                .HasForeignKey(e => e.FinancialItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntryDto>(entry =>
        {
            // Entries are almost always read in date order; the index keeps that free.
            entry.HasIndex(e => new { e.FinancialItemId, e.Date });
            entry.Property(e => e.Note).HasMaxLength(1000);
        });
    }
}

public sealed class PortfolioDbContextFactory(DbContextOptions options)
{
    public PortfolioDbContext Create() => new(options);
}
