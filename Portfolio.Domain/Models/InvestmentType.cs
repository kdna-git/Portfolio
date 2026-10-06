namespace Portfolio.Domain.Models;

/// <summary>
/// The role a financial item plays in the portfolio. Not an asset class: two index funds can be
/// <see cref="Grow"/> and <see cref="Exit"/> depending on what you intend to do with them.
/// </summary>
public enum InvestmentType
{
    Other,

    /// <summary>Capital you keep adding to.</summary>
    Grow,

    /// <summary>Income is reinvested in place.</summary>
    Compound,

    /// <summary>Left alone; neither fed nor drained.</summary>
    Hold,

    /// <summary>Liquidity reserve.</summary>
    Buffer,

    /// <summary>Being wound down gradually.</summary>
    Shrink,

    /// <summary>Being closed out.</summary>
    Exit,
}
