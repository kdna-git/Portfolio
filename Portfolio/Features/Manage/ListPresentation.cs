namespace Portfolio.Features.Manage;

/// <summary>How a list is drawn.</summary>
public enum ListViewMode
{
    /// <summary>One card per row: readable on a phone, scannable on a desktop.</summary>
    Cards,

    /// <summary>A dense table: more rows per screen, easier to compare down a column.</summary>
    Table,
}

/// <summary>How a list is divided up.</summary>
public enum EntryGrouping
{
    None,
    Month,
    Platform,
    Holding,
}

public enum ItemGrouping
{
    None,
    Platform,
    Type,
}

/// <summary>What an entry list is ordered by.</summary>
public enum EntrySort
{
    Date,
    Amount,
    Platform,
    Holding,
}

/// <summary>What a holding or platform list is ordered by.</summary>
public enum HoldingSort
{
    Name,
    Balance,
    Invested,
    Earnings,
    Return,
    LastActivity,
}

/// <summary>Which movements an entry filter lets through.</summary>
public enum MovementFilter
{
    All,
    Deposits,
    Withdrawals,
    Income,
    Costs,
    Valuations,
}

public static class ListPresentationLabels
{
    public static string Label(this ListViewMode mode) => mode == ListViewMode.Cards ? "Cards" : "Table";

    public static string Label(this EntryGrouping grouping) =>
        grouping switch
        {
            EntryGrouping.Month => "By month",
            EntryGrouping.Platform => "By platform",
            EntryGrouping.Holding => "By holding",
            _ => "No grouping",
        };

    public static string Label(this ItemGrouping grouping) =>
        grouping switch
        {
            ItemGrouping.Platform => "By platform",
            ItemGrouping.Type => "By type",
            _ => "No grouping",
        };

    public static string Label(this EntrySort sort) =>
        sort switch
        {
            EntrySort.Amount => "Amount",
            EntrySort.Platform => "Platform",
            EntrySort.Holding => "Holding",
            _ => "Date",
        };

    public static string Label(this HoldingSort sort) =>
        sort switch
        {
            HoldingSort.Balance => "Balance",
            HoldingSort.Invested => "Invested",
            HoldingSort.Earnings => "Earnings",
            HoldingSort.Return => "Return",
            HoldingSort.LastActivity => "Last activity",
            _ => "Name",
        };

    public static string Label(this MovementFilter filter) =>
        filter switch
        {
            MovementFilter.Deposits => "Deposits",
            MovementFilter.Withdrawals => "Withdrawals",
            MovementFilter.Income => "Income",
            MovementFilter.Costs => "Costs and losses",
            MovementFilter.Valuations => "Valuations",
            _ => "All movements",
        };
}
