using Portfolio.Charts.Data;
using Portfolio.Charts.Theming;
using Portfolio.Domain.Models;

namespace Portfolio.Features.Manage;

/// <summary>
/// One entry prepared for display: formatted once here rather than by converters in the template.
/// </summary>
/// <remarks>
/// An entry can hold several movements at once — a deposit that was invested the same day, interest
/// net of tax — so instead of pretending it has a single "type" the row exposes
/// <see cref="Chips"/>: one labelled amount per movement it actually contains.
/// </remarks>
public sealed class EntryRowViewModel(Entry entry, FinancialItem holding, Platform platform)
{
    public Entry Entry { get; } = entry;

    public FinancialItem Holding { get; } = holding;

    public Platform Platform { get; } = platform;

    public string DateText { get; } = entry.Date.ToString("d MMM yyyy");

    public string ShortDateText { get; } = entry.Date.ToString("dd/MM/yy");

    public string HoldingName { get; } = holding.Name;

    public string PlatformName { get; } = platform.Name;

    public string? Note { get; } = string.IsNullOrWhiteSpace(entry.Note) ? null : entry.Note;

    public bool HasNote { get; } = !string.IsNullOrWhiteSpace(entry.Note);

    /// <summary>Net effect on the platform balance: the one number that summarises the entry.</summary>
    public string AmountText { get; } =
        ValueFormatter.FormatSigned((double)entry.BalanceChange, ValueFormat.Currency);

    public bool AmountIsPositive { get; } = entry.BalanceChange > 0;

    public bool AmountIsNegative { get; } = entry.BalanceChange < 0;

    public string EarningsText { get; } =
        entry.NetEarnings == 0
            ? "—"
            : ValueFormatter.FormatSigned((double)entry.NetEarnings, ValueFormat.Currency);

    public bool EarningsIsPositive { get; } = entry.NetEarnings > 0;

    public bool EarningsIsNegative { get; } = entry.NetEarnings < 0;

    public string DepositText { get; } = Optional(entry.Deposit);

    public string WithdrawalText { get; } = Optional(entry.Withdrawal);

    public string InvestedText { get; } = Optional(entry.Investment);

    public string RepaymentText { get; } = Optional(entry.PrincipalRepayment);

    public string IncomeText { get; } = Optional(entry.GrossIncome);

    public string CostText { get; } = Optional(entry.Costs);

    public string LossText { get; } = Optional(entry.DefaultLoss);

    public string MarketValueText { get; } =
        entry.MarketValue is null
            ? "—"
            : ValueFormatter.Format((double)entry.MarketValue.Value, ValueFormat.Currency);

    public bool IsValuationOnly { get; } = entry.IsValuationOnly;

    public IReadOnlyList<EntryChip> Chips { get; } = BuildChips(entry);

    /// <summary>Case-insensitive match across the note, the holding and the platform.</summary>
    public bool Matches(string text) =>
        HoldingName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
        || PlatformName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
        || (Note?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || DateText.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    private static string Optional(decimal value) =>
        value == 0 ? "—" : ValueFormatter.Format((double)value, ValueFormat.Currency);

    private static IReadOnlyList<EntryChip> BuildChips(Entry entry)
    {
        var chips = new List<EntryChip>();

        void Add(string label, decimal amount, string color)
        {
            if (amount != 0)
                chips.Add(
                    new EntryChip(
                        label,
                        ValueFormatter.Format((double)amount, ValueFormat.Currency),
                        color
                    )
                );
        }

        Add("Deposit", entry.Deposit, ChartPalette.Slot(0));
        Add("Withdrawal", entry.Withdrawal, ChartPalette.Slot(1));
        Add("Invested", entry.Investment, ChartPalette.Slot(6));
        Add("Repaid", entry.PrincipalRepayment, ChartPalette.Slot(2));
        Add("Interest", entry.Interest, ChartPalette.Positive);
        Add("Bonus", entry.Bonus, ChartPalette.Slot(4));
        Add("Tax", entry.Tax, ChartPalette.Slot(3));
        Add("Fee", entry.Fee, ChartPalette.Serious);
        Add("Written off", entry.DefaultLoss, ChartPalette.Negative);

        if (entry.MarketValue is not null)
            chips.Add(
                new EntryChip(
                    "Valued",
                    ValueFormatter.Format((double)entry.MarketValue.Value, ValueFormat.Currency),
                    ChartPalette.Neutral
                )
            );

        return chips;
    }
}

/// <summary>One labelled amount inside an entry.</summary>
public sealed record EntryChip(string Label, string Amount, string Color);
