using FluentAssertions;
using Portfolio.Domain.Models;
using Portfolio.Storage;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Integration;

/// <summary>
/// The repository the browser build runs on. Everything goes through one JSON document, so these
/// tests also cover the round trip that a page reload depends on.
/// </summary>
public sealed class JsonPortfolioRepositoryTests
{
    private static (JsonPortfolioRepository Repository, InMemoryBlobStore Store) Create()
    {
        var store = new InMemoryBlobStore();
        return (new JsonPortfolioRepository(store), store);
    }

    [Fact]
    public async Task Saves_and_reloads_the_whole_portfolio()
    {
        var (repository, store) = Create();

        var platform = await repository.AddPlatformAsync(new Platform(0, "Broker"));
        var item = await repository.AddFinancialItemAsync(
            new FinancialItem(0, platform.Id, InvestmentType.Grow, "Fund")
        );
        await repository.AddEntryAsync(
            new Entry(0, item.Id, On(2024, 1, 1), deposit: 1_000, note: "First")
        );

        // A second repository over the same blob is what a page reload amounts to.
        var reloaded = await new JsonPortfolioRepository(store).GetPlatformsAsync();

        reloaded.Should().ContainSingle();
        reloaded[0].Name.Should().Be("Broker");
        reloaded[0].FinancialItems.Should().ContainSingle();
        reloaded[0].FinancialItems[0].Entries.Should().ContainSingle();
        reloaded[0].FinancialItems[0].Entries[0].Note.Should().Be("First");
        reloaded[0].TotalBalance.Should().Be(1_000);
    }

    [Fact]
    public async Task Assigns_ids_that_continue_after_a_reload()
    {
        var (repository, store) = Create();

        var platform = await repository.AddPlatformAsync(new Platform(0, "P"));
        var item = await repository.AddFinancialItemAsync(
            new FinancialItem(0, platform.Id, InvestmentType.Grow, "I")
        );
        var first = await repository.AddEntryAsync(new Entry(0, item.Id, On(2024, 1, 1), deposit: 10));

        var second = await new JsonPortfolioRepository(store).AddEntryAsync(
            new Entry(0, item.Id, On(2024, 2, 1), deposit: 20)
        );

        second.Id.Should().BeGreaterThan(first.Id, "ids must not restart and collide after a reload");
    }

    [Fact]
    public async Task Deleting_a_platform_takes_its_holdings_and_entries_with_it()
    {
        var (repository, _) = Create();

        var platform = await repository.AddPlatformAsync(new Platform(0, "P"));
        var item = await repository.AddFinancialItemAsync(
            new FinancialItem(0, platform.Id, InvestmentType.Grow, "I")
        );
        await repository.AddEntryAsync(new Entry(0, item.Id, On(2024, 1, 1), deposit: 10));

        await repository.DeletePlatformAsync(platform.Id);

        (await repository.GetPlatformsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Moving_a_holding_to_another_platform_keeps_its_entries()
    {
        var (repository, _) = Create();

        var from = await repository.AddPlatformAsync(new Platform(0, "From"));
        var to = await repository.AddPlatformAsync(new Platform(0, "To"));
        var item = await repository.AddFinancialItemAsync(
            new FinancialItem(0, from.Id, InvestmentType.Grow, "I")
        );
        await repository.AddEntryAsync(new Entry(0, item.Id, On(2024, 1, 1), deposit: 10));

        await repository.UpdateFinancialItemAsync(
            new FinancialItem(item.Id, to.Id, InvestmentType.Hold, "Renamed")
        );

        var platforms = await repository.GetPlatformsAsync();
        var moved = platforms.Single(p => p.Id == to.Id).FinancialItems.Single();

        moved.Name.Should().Be("Renamed");
        moved.Type.Should().Be(InvestmentType.Hold);
        moved.Entries.Should().ContainSingle("the entries travel with the holding");
        platforms.Single(p => p.Id == from.Id).FinancialItems.Should().BeEmpty();
    }

    [Fact]
    public async Task A_corrupt_document_starts_fresh_instead_of_failing_to_open()
    {
        var store = new InMemoryBlobStore("{ this is not json");
        var repository = new JsonPortfolioRepository(store);

        var platforms = await repository.GetPlatformsAsync();

        platforms.Should().BeEmpty();
    }

    [Fact]
    public async Task A_damaged_document_is_kept_and_not_seeded_over()
    {
        var store = new InMemoryBlobStore("{ this is not json");
        var damaged = new InMemoryBlobStore();
        var repository = new JsonPortfolioRepository(store)
        {
            SeedFactory = () => DemoData.CreateSnapshot(On(2026, 1, 1)),
            DamagedCopyStore = damaged,
        };

        (await repository.GetPlatformsAsync())
            .Should()
            .BeEmpty("demo data mixed into a recovery would be worse than nothing");
        (await damaged.ReadAsync()).Should().Be("{ this is not json");
        (await store.ReadAsync())
            .Should()
            .Be("{ this is not json", "nothing is written over it until the first real change");
    }

    [Fact]
    public async Task Replacing_everything_keeps_the_ids_and_continues_after_them()
    {
        var (repository, store) = Create();
        await repository.AddPlatformAsync(new Platform(0, "Old"));

        await repository.ReplaceAllAsync(
            [
                new Platform(
                    40,
                    "New",
                    [
                        new FinancialItem(
                            41,
                            40,
                            InvestmentType.Grow,
                            "Fund",
                            [new Entry(42, 41, On(2024, 1, 1), deposit: 5)]
                        ),
                    ]
                ),
            ]
        );

        var reloaded = await new JsonPortfolioRepository(store).GetPlatformsAsync();
        reloaded.Should().ContainSingle().Which.Id.Should().Be(40);
        reloaded[0].FinancialItems[0].Entries[0].Id.Should().Be(42);

        var next = await repository.AddEntryAsync(new Entry(0, 41, On(2024, 2, 1), deposit: 1));
        next.Id.Should().BeGreaterThan(42, "new rows must not collide with the imported ids");
    }

    [Fact]
    public async Task A_replacement_that_cannot_be_saved_leaves_the_old_portfolio_in_place()
    {
        var blob = new FlakyBlobStore();
        var repository = new JsonPortfolioRepository(blob);
        await repository.AddPlatformAsync(new Platform(0, "Old"));

        blob.FailWrites = true;
        var replace = () => repository.ReplaceAllAsync([new Platform(1, "New")]);

        await replace.Should().ThrowAsync<IOException>();
        (await repository.GetPlatformsAsync()).Should().ContainSingle().Which.Name.Should().Be("Old");
    }

    [Fact]
    public async Task Seeds_only_when_there_is_nothing_stored()
    {
        var store = new InMemoryBlobStore();
        var seeded = new JsonPortfolioRepository(store)
        {
            SeedFactory = () => DemoData.CreateSnapshot(On(2026, 1, 1)),
        };

        var first = await seeded.GetPlatformsAsync();
        first.Should().NotBeEmpty();

        // A second open finds the saved document and must not seed over the top of it.
        var reopened = new JsonPortfolioRepository(store)
        {
            SeedFactory = () => new PortfolioSnapshot(),
        };

        (await reopened.GetPlatformsAsync()).Should().HaveCount(first.Count);
    }

    [Fact]
    public void Demo_data_is_a_coherent_portfolio()
    {
        var platforms = DemoData.CreatePlatforms(On(2026, 1, 1));

        platforms.Should().HaveCount(3);
        platforms.SelectMany(p => p.FinancialItems).Should().HaveCount(6);
        platforms.Sum(p => p.TotalBalance).Should().BeGreaterThan(0);
        platforms
            .SelectMany(p => p.FinancialItems)
            .Should()
            .OnlyContain(i => i.Entries.Count > 0);
    }
}
