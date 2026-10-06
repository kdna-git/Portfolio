using FluentAssertions;
using Portfolio.Domain.Models;
using Portfolio.Storage;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

/// <summary>
/// The backup file format. A file this app wrote must always come back whole, and anything that is
/// not a complete backup must be refused before it can replace a single row.
/// </summary>
public sealed class PortfolioTransferTests
{
    private static readonly DateTimeOffset ExportedAt = new(2026, 10, 6, 14, 2, 0, TimeSpan.FromHours(2));

    private static List<Platform> SamplePortfolio() =>
    [
        new(
            3,
            "Broker",
            [
                new FinancialItem(
                    7,
                    3,
                    InvestmentType.Compound,
                    "Loans",
                    [
                        new Entry(11, 7, On(2025, 1, 2), "First deposit", deposit: 1_000.50m, investment: 900m),
                        new Entry(
                            12,
                            7,
                            On(2025, 2, 1),
                            interest: 7.25m,
                            tax: 1.38m,
                            fee: 0.1m,
                            bonus: 2m,
                            principalRepayment: 50m,
                            defaultLoss: 3m,
                            withdrawal: 20m,
                            marketValue: 905.12m
                        ),
                    ]
                ),
            ]
        ),
        new(4, "Bank", [new FinancialItem(8, 4, InvestmentType.Buffer, "Savings")]),
    ];

    [Fact]
    public void An_export_reads_back_with_every_field_intact()
    {
        var json = PortfolioTransfer.Export(SamplePortfolio(), "Desktop", ExportedAt);

        var backup = PortfolioTransfer.Read(json);

        backup.Device.Should().Be("Desktop");
        backup.ExportedAt.Should().Be(ExportedAt);
        (backup.PlatformCount, backup.ItemCount, backup.EntryCount).Should().Be((2, 2, 2));
        backup.Fingerprint.Should().Be(PortfolioTransfer.Fingerprint(SamplePortfolio()));

        var platforms = backup.ToModel();
        var entry = platforms[0].FinancialItems[0].Entries[1];

        platforms.Select(p => p.Id).Should().Equal(3, 4);
        platforms[0].FinancialItems[0].Type.Should().Be(InvestmentType.Compound);
        entry.Id.Should().Be(12);
        entry.Interest.Should().Be(7.25m);
        entry.Tax.Should().Be(1.38m);
        entry.Fee.Should().Be(0.1m);
        entry.Bonus.Should().Be(2m);
        entry.PrincipalRepayment.Should().Be(50m);
        entry.DefaultLoss.Should().Be(3m);
        entry.Withdrawal.Should().Be(20m);
        entry.MarketValue.Should().Be(905.12m);
        platforms[0].FinancialItems[0].Entries[0].Note.Should().Be("First deposit");
    }

    [Fact]
    public void Types_are_written_by_name_so_the_file_survives_the_enum_being_reordered()
    {
        var json = PortfolioTransfer.Export(SamplePortfolio(), "Desktop", ExportedAt);

        json.Should().Contain("\"Compound\"").And.NotContain("\"Type\": 2");
    }

    [Fact]
    public void A_document_with_numeric_types_and_no_metadata_still_reads()
    {
        // What the browser stored before types were written by name.
        const string json = """
            { "Version": 1, "Platforms": [ { "Id": 1, "Name": "P", "FinancialItems": [
                { "Id": 1, "Name": "I", "Type": 1, "Entries": [] } ] } ] }
            """;

        var backup = PortfolioTransfer.Read(json);

        backup.ExportedAt.Should().BeNull();
        backup.Device.Should().BeNull();
        backup.ToModel()[0].FinancialItems[0].Type.Should().Be(InvestmentType.Grow);
    }

    [Fact]
    public void A_list_written_as_null_reads_as_empty()
    {
        const string json = """
            { "Version": 1, "Platforms": [ { "Id": 1, "Name": "P", "FinancialItems": null } ] }
            """;

        PortfolioTransfer.Read(json).ToModel()[0].FinancialItems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("hello", "not valid JSON")]
    [InlineData("{ \"Version\": 1, \"Platforms\": [", "not valid JSON")]
    // Any JSON object would otherwise read as an empty backup, and importing that deletes everything.
    [InlineData("{}", "not a Portfolio backup")]
    [InlineData("[]", "not a Portfolio backup")]
    [InlineData("{ \"Version\": 1 }", "not a Portfolio backup")]
    [InlineData("{ \"Platforms\": [] }", "not a Portfolio backup")]
    [InlineData("{ \"Version\": \"1\", \"Platforms\": [] }", "not a Portfolio backup")]
    [InlineData("{ \"Version\": 0, \"Platforms\": [] }", "not a Portfolio backup")]
    [InlineData("{ \"Version\": 99, \"Platforms\": [] }", "newer version")]
    public void Refuses_what_is_not_a_backup_this_build_can_read(string json, string reason)
    {
        var read = () => PortfolioTransfer.Read(json);

        read.Should().Throw<PortfolioBackupException>().WithMessage($"*{reason}*");
    }

    [Theory]
    [InlineData("""{ "Id": 1, "Name": "A" }, { "Id": 1, "Name": "B" }""", "platform id 1")]
    [InlineData("""{ "Id": 0, "Name": "A" }""", "platform id 0")]
    [InlineData("""{ "Id": 1, "Name": null }""", "has no name")]
    [InlineData("null", "empty platform record")]
    [InlineData(
        """{ "Id": 1, "Name": "A", "FinancialItems": [ { "Id": 5, "Name": "X", "Type": 42 } ] }""",
        "unknown type"
    )]
    [InlineData(
        """{ "Id": 1, "Name": "A", "FinancialItems": [ { "Id": 5, "Name": "X", "Type": "Gambling" } ] }""",
        "could not be read"
    )]
    [InlineData(
        """{ "Id": 1, "Name": "A", "FinancialItems": [ { "Id": 5, "Name": "X", "Entries": [ { "Id": 9 }, { "Id": 9 } ] } ] }""",
        "entry id 9"
    )]
    [InlineData(
        """{ "Id": 1, "Name": "A", "FinancialItems": [ { "Id": 5, "Name": "X", "Entries": [ { "Id": 9, "Deposit": "lots" } ] } ] }""",
        "Deposit"
    )]
    public void Refuses_a_damaged_backup_and_says_where(string platforms, string detail)
    {
        var json = $$"""{ "Version": 1, "Platforms": [ {{platforms}} ] }""";

        var read = () => PortfolioTransfer.Read(json);

        read.Should().Throw<PortfolioBackupException>().WithMessage($"*damaged*{detail}*");
    }

    [Fact]
    public void The_fingerprint_ignores_load_order_and_how_an_amount_was_written()
    {
        var a = new Platform(1, "P", [
            new FinancialItem(1, 1, InvestmentType.Grow, "I", [
                new Entry(1, 1, On(2025, 1, 1), deposit: 10m),
                new Entry(2, 1, On(2025, 1, 1), deposit: 5m),
            ]),
        ]);
        var b = new Platform(1, "P", [
            new FinancialItem(1, 1, InvestmentType.Grow, "I", [
                new Entry(2, 1, On(2025, 1, 1), deposit: 5.000m),
                new Entry(1, 1, On(2025, 1, 1), deposit: 10.00m),
            ]),
        ]);
        var other = new Platform(2, "Q");

        PortfolioTransfer.Fingerprint([a, other]).Should().Be(PortfolioTransfer.Fingerprint([other, b]));
    }

    [Fact]
    public void The_fingerprint_changes_with_any_edit()
    {
        var original = PortfolioTransfer.Fingerprint(SamplePortfolio());

        var renamedNote = SamplePortfolio();
        var item = renamedNote[0].FinancialItems[0];
        item.ReplaceEntry(item.Entries[0].With(note: "Edited"));

        var moved = SamplePortfolio();
        var loans = moved[0].FinancialItems[0];
        moved[0].RemoveFinancialItem(loans.Id);
        moved[1].AddFinancialItem(loans);

        PortfolioTransfer.Fingerprint(renamedNote).Should().NotBe(original);
        PortfolioTransfer.Fingerprint(moved).Should().NotBe(original, "moving a holding is a change");
        PortfolioTransfer.Fingerprint([]).Should().NotBe(original);
    }

    [Theory]
    [InlineData("Desktop", "portfolio-2026-10-06-desktop.json")]
    [InlineData("My Phone", "portfolio-2026-10-06-my-phone.json")]
    [InlineData("", "portfolio-2026-10-06.json")]
    public void Suggests_a_file_name_that_sorts_by_date_and_names_the_device(string device, string expected)
    {
        PortfolioTransfer.SuggestedFileName(device, ExportedAt).Should().Be(expected);
    }
}
