using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MusicDecrypto.Avalonia.Controls;

/// <summary>
/// Arranges child elements into a staggered grid pattern where items are added to the column that has used least amount of space.
/// </summary>
public sealed class StaggeredPanel : Panel
{

    private double _columnWidth;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaggeredPanel"/> class.
    /// </summary>
    public StaggeredPanel()
    {
        AffectsMeasure<StaggeredPanel>(DesiredColumnWidthProperty, PaddingProperty, ColumnSpacingProperty, RowSpacingProperty, HorizontalAlignmentProperty);
    }

    /// <summary>
    /// Gets or sets the desired width for each column.
    /// </summary>
    /// <remarks>
    /// The width of columns can exceed the DesiredColumnWidth if the HorizontalAlignment is set to Stretch.
    /// </remarks>
    public double DesiredColumnWidth
    {
        get { return (double)GetValue(DesiredColumnWidthProperty); }
        set { SetValue(DesiredColumnWidthProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="DesiredColumnWidth"/> dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="DesiredColumnWidth"/> dependency property.</returns>
    public static readonly StyledProperty<double> DesiredColumnWidthProperty =
        AvaloniaProperty.Register<StaggeredPanel, double>(nameof(DesiredColumnWidth), defaultValue: 250d);

    /// <summary>
    /// Gets or sets the distance between the border and its child object.
    /// </summary>
    /// <returns>
    /// The dimensions of the space between the border and its child as a Thickness value.
    /// Thickness is a structure that stores dimension values using pixel measures.
    /// </returns>
    public Thickness Padding
    {
        get { return GetValue(PaddingProperty); }
        set { SetValue(PaddingProperty, value); }
    }

    /// <summary>
    /// Identifies the Padding dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="Padding"/> dependency property.</returns>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<StaggeredPanel, Thickness>(nameof(Padding), defaultValue: default);

    /// <summary>
    /// Gets or sets the spacing between columns of items.
    /// </summary>
    public double ColumnSpacing
    {
        get { return GetValue(ColumnSpacingProperty); }
        set { SetValue(ColumnSpacingProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<StaggeredPanel, double>(nameof(ColumnSpacing), defaultValue: 0d);

    /// <summary>
    /// Gets or sets the spacing between rows of items.
    /// </summary>
    public double RowSpacing
    {
        get { return GetValue(RowSpacingProperty); }
        set { SetValue(RowSpacingProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<StaggeredPanel, double>(nameof(RowSpacing), defaultValue: 0d);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        double availableWidth = availableSize.Width - Padding.Left - Padding.Right;
        double availableHeight = availableSize.Height - Padding.Top - Padding.Bottom;

        _columnWidth = Math.Min(DesiredColumnWidth, availableWidth);
        int numColumns = Math.Max(1, (int)Math.Floor((availableWidth + ColumnSpacing) / (_columnWidth + ColumnSpacing)));

        if (double.IsInfinity(availableWidth))
        {
            availableWidth = _columnWidth + ((numColumns - 1) * (_columnWidth + ColumnSpacing));
        }

        if (HorizontalAlignment == HorizontalAlignment.Stretch)
        {
            availableWidth -= ((numColumns - 1) * ColumnSpacing);
            _columnWidth = availableWidth / numColumns;
        }

        if (Children.Count == 0)
        {
            return new Size(0, 0);
        }

        var columnHeights = new double[numColumns];
        var itemsPerColumn = new double[numColumns];

        for (int i = 0; i < Children.Count; i++)
        {
            var columnIndex = GetColumnIndex(columnHeights);

            var child = Children[i];
            child.Measure(new Size(_columnWidth, availableHeight));
            var elementSize = child.DesiredSize;
            columnHeights[columnIndex] += elementSize.Height + (itemsPerColumn[columnIndex] > 0 ? RowSpacing : 0);
            itemsPerColumn[columnIndex]++;
        }

        double desiredHeight = columnHeights.Max() + Padding.Top + Padding.Bottom;

        return new Size(availableWidth, desiredHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double horizontalOffset = Padding.Left;
        double verticalOffset = Padding.Top;
        int numColumns = Math.Max(1, (int)Math.Floor((finalSize.Width + ColumnSpacing) / (_columnWidth + ColumnSpacing)));

        double totalWidth = _columnWidth + ((numColumns - 1) * (_columnWidth + ColumnSpacing));

        if (HorizontalAlignment == HorizontalAlignment.Right)
        {
            horizontalOffset += finalSize.Width - totalWidth;
        }
        else if (HorizontalAlignment == HorizontalAlignment.Center)
        {
            horizontalOffset += (finalSize.Width - totalWidth) / 2;
        }

        var columnHeights = new double[numColumns];
        var itemsPerColumn = new double[numColumns];

        for (int i = 0; i < Children.Count; i++)
        {
            var columnIndex = GetColumnIndex(columnHeights);

            var child = Children[i];
            var elementSize = child.DesiredSize;

            double elementHeight = elementSize.Height;

            double itemHorizontalOffset = horizontalOffset + (_columnWidth * columnIndex) + (ColumnSpacing * columnIndex);
            double itemVerticalOffset = columnHeights[columnIndex] + verticalOffset + (RowSpacing * itemsPerColumn[columnIndex]);

            Rect bounds = new(itemHorizontalOffset, itemVerticalOffset, _columnWidth, elementHeight);
            child.Arrange(bounds);

            columnHeights[columnIndex] += elementSize.Height;
            itemsPerColumn[columnIndex]++;
        }

        return finalSize;
    }

    private static int GetColumnIndex(double[] columnHeights)
    {
        int columnIndex = 0;
        double height = columnHeights[0];
        for (int j = 1; j < columnHeights.Length; j++)
        {
            if (columnHeights[j] < height)
            {
                columnIndex = j;
                height = columnHeights[j];
            }
        }

        return columnIndex;
    }
}
