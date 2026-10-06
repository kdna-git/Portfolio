using Portfolio.Domain.Models;

namespace Portfolio.Domain.DTOs;

/// <summary>
/// The one place that knows how a stored row becomes a model and back.
/// </summary>
/// <remarks>
/// Previously every query and command spelled out the fourteen-argument <see cref="Entry"/>
/// constructor, so adding a field meant editing six files and silently dropping it in the one that
/// was missed. Now adding a field is a two-line change here.
/// </remarks>
public static class DtoMapping
{
    public static Entry ToModel(this EntryDto dto) =>
        new(
            dto.Id,
            dto.FinancialItemId,
            dto.Date,
            dto.Note,
            dto.Deposit,
            dto.Withdrawal,
            dto.Investment,
            dto.PrincipalRepayment,
            dto.Interest,
            dto.Bonus,
            dto.Tax,
            dto.Fee,
            dto.DefaultLoss,
            dto.MarketValue
        );

    public static FinancialItem ToModel(this FinancialItemDto dto) =>
        new(dto.Id, dto.PlatformId, dto.Type, dto.Name, dto.Entries.Select(ToModel));

    public static Platform ToModel(this PlatformDto dto) =>
        new(dto.Id, dto.Name, dto.FinancialItems.Select(ToModel));

    /// <summary>
    /// Copies model values onto a DTO. <paramref name="dto"/>'s identity columns are left alone, so
    /// the same method serves inserts (fresh DTO, database assigns the id) and updates.
    /// </summary>
    public static EntryDto Fill(this EntryDto dto, Entry entry)
    {
        dto.FinancialItemId = entry.FinancialItemId;
        dto.Date = entry.Date;
        dto.Note = entry.Note;
        dto.Deposit = entry.Deposit;
        dto.Withdrawal = entry.Withdrawal;
        dto.Investment = entry.Investment;
        dto.PrincipalRepayment = entry.PrincipalRepayment;
        dto.Interest = entry.Interest;
        dto.Bonus = entry.Bonus;
        dto.Tax = entry.Tax;
        dto.Fee = entry.Fee;
        dto.DefaultLoss = entry.DefaultLoss;
        dto.MarketValue = entry.MarketValue;
        return dto;
    }

    public static EntryDto ToDto(this Entry entry, bool includeId = true)
    {
        var dto = new EntryDto().Fill(entry);

        if (includeId)
            dto.Id = entry.Id;

        return dto;
    }

    public static FinancialItemDto ToDto(this FinancialItem item, bool includeId = true) =>
        new()
        {
            Id = includeId ? item.Id : 0,
            Name = item.Name,
            Type = item.Type,
            PlatformId = item.PlatformId,
            Entries = [.. item.Entries.Select(e => e.ToDto(includeId))],
        };

    public static PlatformDto ToDto(this Platform platform, bool includeId = true) =>
        new()
        {
            Id = includeId ? platform.Id : 0,
            Name = platform.Name,
            FinancialItems = [.. platform.FinancialItems.Select(i => i.ToDto(includeId))],
        };
}
