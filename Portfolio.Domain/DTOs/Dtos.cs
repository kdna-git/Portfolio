using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Portfolio.Domain.Models;

namespace Portfolio.Domain.DTOs;

public sealed class PlatformDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<FinancialItemDto> FinancialItems { get; set; } = [];
}

public sealed class FinancialItemDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public InvestmentType Type { get; set; }

    public long PlatformId { get; set; }

    public PlatformDto? Platform { get; set; }

    public List<EntryDto> Entries { get; set; } = [];
}

public sealed class EntryDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public DateOnly Date { get; set; }

    public string? Note { get; set; }

    public decimal Deposit { get; set; }

    public decimal Withdrawal { get; set; }

    public decimal Investment { get; set; }

    public decimal PrincipalRepayment { get; set; }

    public decimal Interest { get; set; }

    public decimal Bonus { get; set; }

    public decimal Tax { get; set; }

    public decimal Fee { get; set; }

    public decimal DefaultLoss { get; set; }

    public decimal? MarketValue { get; set; }

    public long FinancialItemId { get; set; }

    public FinancialItemDto? FinancialItem { get; set; }
}
