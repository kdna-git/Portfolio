using FluentAssertions;
using Microsoft.Data.Sqlite;
using Portfolio.Domain.Models;
using Portfolio.EntityFramework;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Integration;

/// <summary>
/// The SQLite repository desktop and Android run on, against a real database file: the in-memory
/// provider has neither transactions nor bulk deletes, which are exactly what an import relies on.
/// </summary>
public sealed class EfPortfolioRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"portfolio-tests-{Guid.NewGuid():N}"
    );

    public void Dispose()
    {
        // Pooled connections keep the file open, and Windows will not delete an open file.
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is not worth failing a test over.
        }
    }

    [Fact]
    public async Task Replacing_everything_keeps_the_ids_and_continues_after_them()
    {
        var repository = await PortfolioDatabase.OpenAsync(_directory);
        var old = await repository.AddPlatformAsync(new Platform(0, "Old"));
        var oldItem = await repository.AddFinancialItemAsync(
            new FinancialItem(0, old.Id, InvestmentType.Grow, "Old fund")
        );
        await repository.AddEntryAsync(new Entry(0, oldItem.Id, On(2024, 1, 1), deposit: 1));

        await repository.ReplaceAllAsync(
            [
                new Platform(
                    40,
                    "New",
                    [
                        new FinancialItem(
                            41,
                            40,
                            InvestmentType.Hold,
                            "Fund",
                            [new Entry(42, 41, On(2024, 1, 1), "Note", deposit: 5.25m, marketValue: 6m)]
                        ),
                    ]
                ),
                new Platform(50, "Empty"),
            ]
        );

        var platforms = await repository.GetPlatformsAsync();
        platforms.Select(p => p.Id).Should().BeEquivalentTo(new[] { 40L, 50L });

        var entry = platforms.Single(p => p.Id == 40).FinancialItems.Single().Entries.Single();
        entry.Id.Should().Be(42);
        entry.Note.Should().Be("Note");
        entry.Deposit.Should().Be(5.25m);
        entry.MarketValue.Should().Be(6m);

        var next = await repository.AddEntryAsync(new Entry(0, 41, On(2024, 2, 1), deposit: 1));
        next.Id.Should().BeGreaterThan(42, "new rows must not collide with the imported ids");
    }

    [Fact]
    public async Task A_replacement_that_fails_part_way_rolls_back_completely()
    {
        var repository = await PortfolioDatabase.OpenAsync(_directory);
        var old = await repository.AddPlatformAsync(new Platform(0, "Old"));
        var oldItem = await repository.AddFinancialItemAsync(
            new FinancialItem(0, old.Id, InvestmentType.Grow, "Old fund")
        );
        await repository.AddEntryAsync(new Entry(0, oldItem.Id, On(2024, 1, 1), deposit: 1));

        // Two entries sharing an id fail on insert — after the deletes have already run.
        var broken = new Platform(
            1,
            "New",
            [
                new FinancialItem(
                    2,
                    1,
                    InvestmentType.Grow,
                    "I",
                    [new Entry(3, 2, On(2024, 1, 1)), new Entry(3, 2, On(2024, 1, 2))]
                ),
            ]
        );

        var replace = () => repository.ReplaceAllAsync([broken]);

        await replace.Should().ThrowAsync<Exception>();

        var platforms = await repository.GetPlatformsAsync();
        platforms.Should().ContainSingle().Which.Name.Should().Be("Old");
        platforms[0].FinancialItems.Single().Entries.Should().ContainSingle("the deletes were rolled back");
    }
}
