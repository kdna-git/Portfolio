namespace Portfolio.Domain.Models;

/// <summary>
/// One movement recorded against a <see cref="FinancialItem"/>. Every amount is a non-negative
/// magnitude; the direction is implied by which field it lands in.
/// </summary>
public sealed class Entry(
    long id,
    long financialItemId,
    DateOnly date,
    string? note = null,
    decimal deposit = 0,
    decimal withdrawal = 0,
    decimal investment = 0,
    decimal principalRepayment = 0,
    decimal interest = 0,
    decimal bonus = 0,
    decimal tax = 0,
    decimal fee = 0,
    decimal defaultLoss = 0,
    decimal? marketValue = null
)
{
    public long Id { get; } = id;

    public long FinancialItemId { get; } = financialItemId;

    public DateOnly Date { get; } = date;

    public string? Note { get; } = note;

    /// <summary>Cash into the platform from outside the portfolio.</summary>
    public decimal Deposit { get; } = deposit;

    /// <summary>Cash out of the platform, back to you.</summary>
    public decimal Withdrawal { get; } = withdrawal;

    /// <summary>Platform cash committed to the asset.</summary>
    public decimal Investment { get; } = investment;

    /// <summary>Capital returned by the asset to platform cash.</summary>
    public decimal PrincipalRepayment { get; } = principalRepayment;

    /// <summary>Interest credited by the asset.</summary>
    public decimal Interest { get; } = interest;

    /// <summary>Dividends, rewards, referral bonuses and similar income.</summary>
    public decimal Bonus { get; } = bonus;

    /// <summary>Tax withheld or paid.</summary>
    public decimal Tax { get; } = tax;

    /// <summary>Management, transaction or exchange fees.</summary>
    public decimal Fee { get; } = fee;

    /// <summary>Capital written off because the asset defaulted.</summary>
    public decimal DefaultLoss { get; } = defaultLoss;

    /// <summary>
    /// Reported value of the holding on this date, when known. Acts as an anchor: later entries
    /// roll the value forward until the next report.
    /// </summary>
    public decimal? MarketValue { get; } = marketValue;

    /// <summary>
    /// Money crossing the portfolio boundary, positive when it comes back to you. This is the only
    /// flow that belongs in a return calculation: income and costs stay inside the portfolio and
    /// are already reflected in its balance.
    /// </summary>
    public decimal ExternalCashFlow => Withdrawal - Deposit;

    /// <summary>Income credited by this entry, before costs.</summary>
    public decimal GrossIncome => Interest + Bonus;

    /// <summary>Costs charged by this entry.</summary>
    public decimal Costs => Tax + Fee;

    /// <summary>Income minus costs minus write-offs: the balance change not caused by you.</summary>
    public decimal NetEarnings => GrossIncome - Costs - DefaultLoss;

    /// <summary>Net change in platform balance caused by this entry.</summary>
    public decimal BalanceChange => Deposit - Withdrawal + NetEarnings;

    /// <summary>Net change in capital committed to the asset.</summary>
    public decimal InvestedChange => Investment - PrincipalRepayment - DefaultLoss;

    /// <summary>True when nothing but a market value report was recorded.</summary>
    public bool IsValuationOnly =>
        MarketValue is not null
        && Deposit == 0
        && Withdrawal == 0
        && Investment == 0
        && PrincipalRepayment == 0
        && GrossIncome == 0
        && Costs == 0
        && DefaultLoss == 0;

    /// <summary>
    /// The kinds of movement this entry contains. One entry can hold several (a deposit that is
    /// immediately invested, say), which is why filters work on a set rather than a single type.
    /// </summary>
    public MovementKinds Kinds
    {
        get
        {
            var kinds = MovementKinds.None;

            if (Deposit != 0)
                kinds |= MovementKinds.Deposit;
            if (Withdrawal != 0)
                kinds |= MovementKinds.Withdrawal;
            if (Investment != 0)
                kinds |= MovementKinds.Investment;
            if (PrincipalRepayment != 0)
                kinds |= MovementKinds.PrincipalRepayment;
            if (Interest != 0)
                kinds |= MovementKinds.Interest;
            if (Bonus != 0)
                kinds |= MovementKinds.Bonus;
            if (Tax != 0)
                kinds |= MovementKinds.Tax;
            if (Fee != 0)
                kinds |= MovementKinds.Fee;
            if (DefaultLoss != 0)
                kinds |= MovementKinds.DefaultLoss;
            if (MarketValue is not null)
                kinds |= MovementKinds.Valuation;

            return kinds;
        }
    }

    public Entry With(
        long? id = null,
        long? financialItemId = null,
        DateOnly? date = null,
        string? note = null,
        decimal? deposit = null,
        decimal? withdrawal = null,
        decimal? investment = null,
        decimal? principalRepayment = null,
        decimal? interest = null,
        decimal? bonus = null,
        decimal? tax = null,
        decimal? fee = null,
        decimal? defaultLoss = null,
        decimal? marketValue = null
    ) =>
        new(
            id ?? Id,
            financialItemId ?? FinancialItemId,
            date ?? Date,
            note ?? Note,
            deposit ?? Deposit,
            withdrawal ?? Withdrawal,
            investment ?? Investment,
            principalRepayment ?? PrincipalRepayment,
            interest ?? Interest,
            bonus ?? Bonus,
            tax ?? Tax,
            fee ?? Fee,
            defaultLoss ?? DefaultLoss,
            marketValue ?? MarketValue
        );

    public override string ToString() => $"{Date:yyyy-MM-dd} #{Id}";
}

/// <summary>Movement kinds an entry can contain, used for filtering and colour coding.</summary>
[Flags]
public enum MovementKinds
{
    None = 0,
    Deposit = 1 << 0,
    Withdrawal = 1 << 1,
    Investment = 1 << 2,
    PrincipalRepayment = 1 << 3,
    Interest = 1 << 4,
    Bonus = 1 << 5,
    Tax = 1 << 6,
    Fee = 1 << 7,
    DefaultLoss = 1 << 8,
    Valuation = 1 << 9,

    Income = Interest | Bonus,
    Cost = Tax | Fee | DefaultLoss,
    External = Deposit | Withdrawal,
    Internal = Investment | PrincipalRepayment,
}
