using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Portfolio.Charts.Data;

namespace Portfolio.Charts.ViewModels;

/// <summary>
/// A sortable table of numbers.
/// </summary>
/// <remarks>
/// Pulls double duty: it is the per-period returns matrix, and it is the table view any chart in this
/// library can fall back to — the accessible path out of a colour-coded figure, and the only honest
/// way to present values a chart has to leave out.
/// <para>
/// Input rows carry raw numbers (<see cref="TableRow"/>); the view binds to
/// <see cref="Rows"/>, whose cells are already formatted and already know whether they read as good
/// or bad news. Formatting once here keeps it out of the data templates.
/// </para>
/// </remarks>
public sealed partial class DataTableViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LabelColumn))]
    [NotifyPropertyChangedFor(nameof(ValueColumns))]
    public partial IReadOnlyList<TableColumn> Columns { get; set; }

    /// <summary>
    /// The heading for the row-label column, and the headings for the value columns, split apart.
    /// </summary>
    /// <remarks>
    /// A row lays out as "label in a star column, values in shared columns". The heading row has to
    /// use the same split or the two drift: spreading all headings evenly across the full width puts
    /// every one of them over the wrong column.
    /// </remarks>
    public TableColumn? LabelColumn => Columns.Count > 0 ? Columns[0] : null;

    public IReadOnlyList<TableColumn> ValueColumns =>
        Columns.Count > 1 ? [.. Columns.Skip(1)] : [];

    [ObservableProperty]
    public partial IReadOnlyList<DisplayRow> Rows { get; set; }

    [ObservableProperty]
    public partial string? SortColumnKey { get; set; }

    [ObservableProperty]
    public partial bool SortAscending { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>
    /// Width below which the table scrolls sideways instead of squeezing. Derived from the column
    /// count so a wide matrix stays readable on a phone.
    /// </summary>
    [ObservableProperty]
    public partial double MinWidth { get; set; }

    private IReadOnlyList<TableRow> _source = [];

    public DataTableViewModel()
    {
        Columns = [];
        Rows = [];
        IsEmpty = true;
    }

    public void SetData(IReadOnlyList<TableColumn> columns, IReadOnlyList<TableRow> rows)
    {
        Columns = columns;
        _source = rows;
        IsEmpty = rows.Count == 0;
        MinWidth = 150 + (Math.Max(0, columns.Count - 1) * 78);
        ApplySort();
    }

    /// <summary>Sorts by a column, flipping direction when the same column is chosen again.</summary>
    [RelayCommand]
    private void Sort(string? columnKey)
    {
        if (string.IsNullOrEmpty(columnKey))
            return;

        if (SortColumnKey == columnKey)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumnKey = columnKey;
            // Numbers are most useful largest-first; names read better A to Z.
            SortAscending = Columns.FirstOrDefault(c => c.Key == columnKey)?.IsNumeric != true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        var columnIndex = SortColumnKey is null ? -1 : IndexOfColumn(SortColumnKey);

        // Pinned rows (the "Total" line) stay on top regardless of the sort.
        var pinned = _source.Where(r => r.IsPinned);
        IEnumerable<TableRow> sortable = _source.Where(r => !r.IsPinned);

        if (columnIndex == 0)
        {
            sortable = SortAscending
                ? sortable.OrderBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase)
                : sortable.OrderByDescending(r => r.Label, StringComparer.CurrentCultureIgnoreCase);
        }
        else if (columnIndex > 0)
        {
            var cellIndex = columnIndex - 1;
            sortable = SortAscending
                ? sortable.OrderBy(r => SortKey(r, cellIndex))
                : sortable.OrderByDescending(r => SortKey(r, cellIndex));
        }

        Rows = [.. pinned.Concat(sortable).Select(ToDisplayRow)];
    }

    private int IndexOfColumn(string key)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Key == key)
                return i;
        }

        return -1;
    }

    private DisplayRow ToDisplayRow(TableRow row)
    {
        var cells = new List<DisplayCell>(row.Cells.Count);

        for (var i = 0; i < row.Cells.Count; i++)
        {
            // Cell i belongs to column i + 1: column 0 is the row label.
            var format = i + 1 < Columns.Count ? Columns[i + 1].Format : ValueFormat.Currency;
            var cell = row.Cells[i];

            cells.Add(
                new DisplayCell(
                    cell.Text(format),
                    cell.Sentiment > 0,
                    cell.Sentiment < 0
                )
            );
        }

        return new DisplayRow(row.Key, row.Label, row.Sublabel, row.Color, row.IsPinned, cells);
    }

    // Missing values sort last in either direction rather than counting as zero, which would drop
    // "no data" into the middle of the ranking.
    private static double SortKey(TableRow row, int cellIndex) =>
        cellIndex < row.Cells.Count
            ? row.Cells[cellIndex].Number ?? double.MinValue
            : double.MinValue;
}

/// <summary>
/// One column. The first column is the row label; the rest map to cells, so a cell's index is its
/// column index minus one.
/// </summary>
public sealed record TableColumn(
    string Key,
    string Header,
    bool IsNumeric = true,
    ValueFormat Format = ValueFormat.Currency,
    string? Description = null
);

/// <summary>One input value. A null <paramref name="Number"/> renders as "n/a".</summary>
public sealed record TableCell(double? Number, bool IsSigned = false, string? OverrideText = null)
{
    public string Text(ValueFormat format) =>
        OverrideText ?? (Number is null ? "n/a" : ValueFormatter.Format(Number.Value, format));

    public int Sentiment =>
        !IsSigned || Number is null ? 0
        : Number.Value > 0 ? 1
        : Number.Value < 0 ? -1
        : 0;
}

/// <summary>One input row, with an optional colour swatch tying it to a chart.</summary>
public sealed record TableRow(
    string Key,
    string Label,
    IReadOnlyList<TableCell> Cells,
    bool IsPinned = false,
    string? Color = null,
    string? Sublabel = null
);

/// <summary>A row as the view consumes it: everything already formatted.</summary>
public sealed record DisplayRow(
    string Key,
    string Label,
    string? Sublabel,
    string? Color,
    bool IsPinned,
    IReadOnlyList<DisplayCell> Cells
);

/// <summary>A cell as the view consumes it.</summary>
public sealed record DisplayCell(string Text, bool IsPositive, bool IsNegative);
