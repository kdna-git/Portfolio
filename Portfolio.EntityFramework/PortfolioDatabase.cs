using Microsoft.EntityFrameworkCore;
using Portfolio.Domain.Persistence;

namespace Portfolio.EntityFramework;

/// <summary>
/// One call to get a working SQLite repository, so every platform head does not repeat the
/// connection-string and schema-creation dance.
/// </summary>
public static class PortfolioDatabase
{
    public const string FileName = "Portfolio.db";

    /// <summary>
    /// Opens (creating if needed) the database in <paramref name="directory"/> and returns a
    /// repository over it.
    /// </summary>
    public static async Task<IPortfolioRepository> OpenAsync(
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder()
            .UseSqlite($"Data Source={Path.Combine(directory, FileName)}")
            .Options;

        var factory = new PortfolioDbContextFactory(options);

        await using var context = factory.Create();
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        return new EfPortfolioRepository(factory);
    }

    /// <summary>Synchronous variant for platform entry points that cannot await (Android's OnCreate).</summary>
    public static IPortfolioRepository Open(string directory)
    {
        Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder()
            .UseSqlite($"Data Source={Path.Combine(directory, FileName)}")
            .Options;

        var factory = new PortfolioDbContextFactory(options);

        using var context = factory.Create();
        context.Database.EnsureCreated();

        return new EfPortfolioRepository(factory);
    }
}
