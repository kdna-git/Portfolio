using Portfolio.Domain.Models;

namespace Portfolio.Storage;

/// <summary>
/// A plausible three-year portfolio, used to seed the browser build and to give the XAML previewer
/// something to draw. Deterministic, so screenshots and tests stay stable.
/// </summary>
public static class DemoData
{
    public static PortfolioSnapshot CreateSnapshot(DateOnly? today = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        var start = new DateOnly(now.Year - 3, 1, 1);
        var random = new Random(20260101);
        var nextEntryId = 0L;
        var nextItemId = 0L;

        var snapshot = new PortfolioSnapshot();

        var definitions = new (string Platform, (string Name, InvestmentType Type, decimal Monthly, decimal AnnualRate, bool Valued)[] Items)[]
        {
            (
                "Indexa Capital",
                [
                    ("Global equity 70/30", InvestmentType.Grow, 300m, 0.082m, true),
                    ("Bond ladder", InvestmentType.Hold, 100m, 0.031m, true),
                ]
            ),
            (
                "Mintos",
                [
                    ("Consumer loans EUR", InvestmentType.Compound, 150m, 0.108m, false),
                    ("Business loans", InvestmentType.Shrink, 0m, 0.094m, false),
                ]
            ),
            (
                "Trade Republic",
                [
                    ("Cash savings", InvestmentType.Buffer, 200m, 0.025m, false),
                    ("Dividend stocks", InvestmentType.Grow, 120m, 0.061m, true),
                ]
            ),
        };

        foreach (var (platformName, items) in definitions)
        {
            var platform = new PlatformRecord
            {
                Id = snapshot.Platforms.Count + 1,
                Name = platformName,
            };

            foreach (var (name, type, monthly, annualRate, valued) in items)
            {
                var item = new FinancialItemRecord
                {
                    Id = ++nextItemId,
                    Name = name,
                    Type = type,
                };

                decimal invested = 0;
                var seed = type == InvestmentType.Shrink ? 2_500m : 1_000m;

                // Opening position.
                item.Entries.Add(
                    new EntryRecord
                    {
                        Id = ++nextEntryId,
                        Date = start,
                        Note = "Opening balance",
                        Deposit = seed,
                        Investment = seed,
                    }
                );
                invested += seed;

                for (var month = start.AddMonths(1); month <= now; month = month.AddMonths(1))
                {
                    var date = new DateOnly(
                        month.Year,
                        month.Month,
                        Math.Min(28, 1 + random.Next(0, 27))
                    );

                    var contribution = type == InvestmentType.Shrink ? 0m : monthly;
                    var repayment = type == InvestmentType.Shrink ? 60m : 0m;
                    var interest = Math.Round(
                        invested * (annualRate / 12) * (decimal)(0.8 + random.NextDouble() * 0.4),
                        2
                    );
                    var fee = Math.Round(invested * 0.0004m, 2);
                    var tax = Math.Round(interest * 0.19m, 2);
                    var bonus = random.Next(0, 12) == 0 ? Math.Round(15 + (decimal)random.NextDouble() * 35, 2) : 0m;
                    var loss =
                        type == InvestmentType.Compound && random.Next(0, 18) == 0
                            ? Math.Round(20 + (decimal)random.NextDouble() * 60, 2)
                            : 0m;

                    item.Entries.Add(
                        new EntryRecord
                        {
                            Id = ++nextEntryId,
                            Date = date,
                            Deposit = contribution,
                            Investment = contribution,
                            PrincipalRepayment = repayment,
                            Interest = interest,
                            Bonus = bonus,
                            Tax = tax,
                            Fee = fee,
                            DefaultLoss = loss,
                        }
                    );

                    invested += contribution - repayment - loss;

                    // Quarterly valuation for the holdings that report one.
                    if (valued && month.Month % 3 == 0)
                    {
                        var drift = 1 + ((decimal)(random.NextDouble() - 0.42) * 0.05m);

                        item.Entries.Add(
                            new EntryRecord
                            {
                                Id = ++nextEntryId,
                                Date = new DateOnly(month.Year, month.Month, 28),
                                Note = "Quarterly valuation",
                                MarketValue = Math.Round(invested * drift, 2),
                            }
                        );
                    }
                }

                platform.FinancialItems.Add(item);
            }

            snapshot.Platforms.Add(platform);
        }

        return snapshot;
    }

    public static IReadOnlyList<Platform> CreatePlatforms(DateOnly? today = null) =>
        CreateSnapshot(today).ToModel();
}
