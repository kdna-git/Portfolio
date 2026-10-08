using System.Collections.Specialized;
using Avalonia.Controls;

namespace Portfolio.Charts.Controls;

/// <summary>
/// A one-row grid that puts each child in its own column and shares that column's width, by
/// position, with every other <see cref="SharedColumnsGrid"/> in the same
/// <see cref="Grid.IsSharedSizeScopeProperty">shared size scope</see>.
/// </summary>
/// <remarks>
/// Made to be the items panel of a table whose columns are data. Each row of such a table is laid
/// out on its own, so a panel that only looks at its own cells gives every row different column
/// widths and the numbers stop lining up. Sharing the size groups makes column <c>n</c> as wide as
/// the widest cell in column <c>n</c> across the heading and all the rows, which is what a table
/// needs and what a fixed set of column definitions in markup cannot do when the count varies.
/// </remarks>
public sealed class SharedColumnsGrid : Grid
{
    // Group names must be valid identifiers, and are scoped, so two tables on one page do not mix.
    private const string GroupPrefix = "SharedColumn";

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);

        while (ColumnDefinitions.Count < Children.Count)
        {
            ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto)
                {
                    SharedSizeGroup = GroupPrefix + ColumnDefinitions.Count,
                }
            );
        }

        while (ColumnDefinitions.Count > Children.Count)
            ColumnDefinitions.RemoveAt(ColumnDefinitions.Count - 1);

        for (var i = 0; i < Children.Count; i++)
            SetColumn(Children[i], i);
    }
}
