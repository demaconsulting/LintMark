// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Immutable;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     One item of a list, with the geometry of its marker line and the facts that decide whether its lines may be moved.
/// </summary>
/// <param name="Info">The block of the item.</param>
/// <param name="MarkerOffset">Offset of the first character of the marker.</param>
/// <param name="MarkerLine">Zero-based line of the marker.</param>
/// <param name="MarkerColumn">Number of characters before the marker on its line (its column when the line starts with spaces only).</param>
/// <param name="MarkerLength">Length of the marker: one for a bullet, the digits plus the delimiter for a number.</param>
/// <param name="DigitsLength">Length of the number of an ordered item; zero for a bullet item.</param>
/// <param name="SpacesAfter">Number of spaces between the marker and the text (or the end of the line).</param>
/// <param name="HasText">Whether text follows the marker on its line.</param>
/// <param name="OldContentColumn">The column at which the content of the item starts, as the parser reads it.</param>
/// <param name="FirstLine">First line of the item (the marker line).</param>
/// <param name="LastLine">Last line of the item that has content.</param>
/// <param name="Lists">The lists that are direct children of the item.</param>
/// <param name="IsMovable">
///     <see langword="true"/> when every line of the item can be moved by a constant number of columns without
///     changing its meaning (see <see cref="ListModel"/>).
/// </param>
internal sealed record ListItemModel(
    BlockInfo Info,
    int MarkerOffset,
    int MarkerLine,
    int MarkerColumn,
    int MarkerLength,
    int DigitsLength,
    int SpacesAfter,
    bool HasText,
    int OldContentColumn,
    int FirstLine,
    int LastLine,
    ImmutableArray<ListModel> Lists,
    bool IsMovable);

/// <summary>
///     An immutable model of one list of a nest of lists: its kind, its items and whether its columns may be changed.
/// </summary>
/// <remarks>
///     <para>
///         The model is built from the blocks the parser reported and from the text, and checked against both. A shape
///         that does not add up (a marker that is not where the block says it is, items whose lines overlap or leave a gap
///         with content) makes <see cref="Build"/> return <see langword="null"/>, and nothing is proposed for that list.
///     </para>
///     <para>
///         A list is <em>movable</em> when it is not inside a container that changes the meaning of columns (anything but
///         another list; the nest must start at the top level of the document) and every item is movable. A nested list
///         is only ever moved together with its item, so its own verdict concerns its items alone. An item is movable when
///         none of its lines holds a tab before its text, no line after the first has less indentation than the content
///         column of the item (a lazy line: moving the item would change what the line continues), the marker is followed
///         by one to four spaces and text (five or more spaces would be indented code, none would be an empty item), the
///         item does not start another list on its marker line, and no line of it looks like the border of a grid table or
///         holds the marker of a footnote definition (the parser reads those inconsistently). A list that is not movable
///         stays where it is, or moves as a whole with the item it belongs to. Whether a plan that moves lists keeps the
///         rendering of the document is proved by <see cref="ListPlan"/>, not assumed here.
///     </para>
///     <para>
///         Thread safety: immutable after <see cref="Build"/>.
///     </para>
/// </remarks>
internal sealed class ListModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ListModel"/> class.
    /// </summary>
    /// <param name="info">The block of the list.</param>
    /// <param name="isOrdered">Whether the list is ordered.</param>
    /// <param name="items">The items.</param>
    /// <param name="firstLine">First line of the list.</param>
    /// <param name="lastLine">Last line of the list that has content.</param>
    /// <param name="isMovable">Whether the columns of the list may be changed.</param>
    private ListModel(BlockInfo info, bool isOrdered, ImmutableArray<ListItemModel> items, int firstLine, int lastLine, bool isMovable)
    {
        Info = info;
        IsOrdered = isOrdered;
        Items = items;
        FirstLine = firstLine;
        LastLine = lastLine;
        IsMovable = isMovable;
    }

    /// <summary>
    ///     Gets the block of the list.
    /// </summary>
    public BlockInfo Info { get; }

    /// <summary>
    ///     Gets a value indicating whether the list is ordered.
    /// </summary>
    public bool IsOrdered { get; }

    /// <summary>
    ///     Gets the items of the list in source order.
    /// </summary>
    public ImmutableArray<ListItemModel> Items { get; }

    /// <summary>
    ///     Gets the first line of the list.
    /// </summary>
    public int FirstLine { get; }

    /// <summary>
    ///     Gets the last line of the list that has content.
    /// </summary>
    public int LastLine { get; }

    /// <summary>
    ///     Gets a value indicating whether the columns of the list and of everything inside it may be changed.
    /// </summary>
    public bool IsMovable { get; }

    /// <summary>
    ///     Builds the model of an outermost list and of every list inside it.
    /// </summary>
    /// <param name="context">The document, the settings and the line arithmetic. Must not be null.</param>
    /// <param name="outermost">The outermost list of the nest. Must not be null.</param>
    /// <returns>The model, or <see langword="null"/> when the shape of the list is not what the parser promised.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static ListModel? Build(FormatContext context, BlockInfo outermost)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(outermost);

        // Collect the lists and items of the nest by parent and the markers of the items
        var blocks = context.Document.Blocks;
        var children = new Dictionary<Block, List<BlockInfo>>();
        var markers = new List<int>();
        for (var i = FirstAtOrAfter(blocks, outermost.Start); i < blocks.Length && blocks[i].Start <= outermost.EndInclusive; i++)
        {
            var block = blocks[i];
            if (block.Kind is BlockKind.List or BlockKind.ListItem && block.Parent is not null)
            {
                if (!children.TryGetValue(block.Parent, out var siblings))
                {
                    siblings = [];
                    children[block.Parent] = siblings;
                }

                siblings.Add(block);
                if (block.Kind == BlockKind.ListItem)
                {
                    markers.Add(block.Start);
                }
            }
        }

        var builder = new Builder(context, children, markers);
        return builder.BuildList(outermost, inMovableContext: outermost.Parent is null, isOutermost: true);
    }

    /// <summary>
    ///     Finds the index of the first block that starts at or after an offset.
    /// </summary>
    /// <param name="blocks">The blocks in source order.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The index; the length of the array when no block starts that late.</returns>
    private static int FirstAtOrAfter(ImmutableArray<BlockInfo> blocks, int offset)
    {
        var low = 0;
        var high = blocks.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (blocks[middle].Start < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>
    ///     Builds the model of one nest, holding what every list and item of the nest needs.
    /// </summary>
    private sealed class Builder
    {
        /// <summary>
        ///     The document, settings and line arithmetic.
        /// </summary>
        private readonly FormatContext _context;

        /// <summary>
        ///     The lists and items of the nest, by the parser node of their parent, in source order.
        /// </summary>
        private readonly Dictionary<Block, List<BlockInfo>> _children;

        /// <summary>
        ///     The start offsets of the markers of every item of the nest, in source order.
        /// </summary>
        private readonly List<int> _markers;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        /// <param name="context">The document, settings and line arithmetic.</param>
        /// <param name="children">The lists and items of the nest by parent.</param>
        /// <param name="markers">The start offsets of the markers of the items.</param>
        public Builder(FormatContext context, Dictionary<Block, List<BlockInfo>> children, List<int> markers)
        {
            _context = context;
            _children = children;
            _markers = markers;
        }

        /// <summary>
        ///     Builds the model of a list and of every list inside it.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="inMovableContext">Whether the list may have its columns changed at all (the outermost list of a nest must be at the top level of the document).</param>
        /// <param name="isOutermost">Whether the list is the outermost list of the nest.</param>
        /// <returns>The model, or <see langword="null"/> when the shape of the list is not what the parser promised.</returns>
        public ListModel? BuildList(BlockInfo list, bool inMovableContext, bool isOutermost = false)
        {
            if (list.Node is not ListBlock listBlock
                || !_children.TryGetValue(listBlock, out var itemInfos)
                || itemInfos.Any(static info => info.Kind != BlockKind.ListItem))
            {
                return null;
            }

            var layout = _context.Layout;
            var (firstLine, lastLine) = LineOwnership.LinesOf(layout, list);

            // Read every item; the list can be moved only when every item can
            var items = ImmutableArray.CreateBuilder<ListItemModel>();
            var movable = inMovableContext;
            foreach (var info in itemInfos)
            {
                var item = BuildItem(info, listBlock.IsOrdered);
                if (item is null)
                {
                    return null;
                }

                items.Add(item);
                movable &= item.IsMovable;
            }

            if (items.Count == 0 || items[0].FirstLine != firstLine || items[^1].LastLine != lastLine || !IsTiled(items))
            {
                return null;
            }

            // A top-level list that starts after more than three columns only exists because of what precedes it
            movable &= !isOutermost || items[0].MarkerColumn <= 3;
            return new ListModel(list, listBlock.IsOrdered, items.ToImmutable(), firstLine, lastLine, movable);
        }

        /// <summary>
        ///     Reads one item.
        /// </summary>
        /// <param name="info">The item.</param>
        /// <param name="ordered">Whether the list of the item is ordered.</param>
        /// <returns>The item, or <see langword="null"/> when its marker is not where the parser says.</returns>
        private ListItemModel? BuildItem(BlockInfo info, bool ordered)
        {
            var layout = _context.Layout;
            var text = layout.Text;
            var markerLine = layout.LineOf(info.Start);
            var lineStart = layout.LineStart(markerLine);
            var lineEnd = layout.LineEnd(markerLine);

            // The marker: a bullet character, or digits and a delimiter
            var markerLength = MarkerLength(text, info.Start, lineEnd, ordered, out var digits);
            if (markerLength == 0)
            {
                return null;
            }

            // Whitespace between the marker and the text
            var after = info.Start + markerLength;
            var textStart = after;
            var tabAfter = false;
            while (textStart < lineEnd && LineLayout.IsBlankChar(text[textStart]))
            {
                tabAfter |= text[textStart] == '\t';
                textStart++;
            }

            var spaces = textStart - after;
            var hasText = textStart < lineEnd;
            if (hasText && spaces == 0)
            {
                return null;
            }

            // The marker line must start with spaces only for its column to mean something
            var prefix = text.AsSpan(lineStart, info.Start - lineStart);
            var blockedHere = tabAfter || !hasText || spaces > 4 || prefix.ContainsAnyExcept(' ');
            var markerColumn = info.Start - lineStart;
            var contentColumn = markerColumn + markerLength + (hasText && spaces <= 4 ? spaces : 1);

            // The lines of the item and the lists inside it
            var (first, last) = LineOwnership.LinesOf(layout, info);
            var lists = ImmutableArray.CreateBuilder<ListModel>();
            var childRanges = new List<(int First, int Last)>();
            if (info.Node is ListItemBlock itemBlock && _children.TryGetValue(itemBlock, out var nested))
            {
                foreach (var child in nested)
                {
                    var (childFirst, childLast) = LineOwnership.LinesOf(layout, child);
                    childRanges.Add((childFirst, childLast));
                    if (childFirst == first || childFirst < first || childLast > last)
                    {
                        // A list that starts on the marker line of its item, or leaves it, cannot be moved by a constant
                        blockedHere = true;
                    }
                }
            }

            blockedHere |= HasHazard(first, last, contentColumn);

            // The lists inside an item are only moved along with it, so their own verdict is about their items alone
            if (info.Node is ListItemBlock block && _children.TryGetValue(block, out var childInfos))
            {
                foreach (var child in childInfos)
                {
                    var model = BuildList(child, inMovableContext: true);
                    if (model is null)
                    {
                        return null;
                    }

                    lists.Add(model);
                }
            }

            return new ListItemModel(
                info,
                info.Start,
                markerLine,
                markerColumn,
                markerLength,
                digits,
                spaces,
                hasText,
                contentColumn,
                first,
                last,
                lists.ToImmutable(),
                !blockedHere);
        }

        /// <summary>
        ///     Measures the marker at an offset.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="offset">The offset of the marker.</param>
        /// <param name="lineEnd">The end of the line of the marker.</param>
        /// <param name="ordered">Whether the marker of an ordered list is expected.</param>
        /// <param name="digits">Receives the number of digits of an ordered marker.</param>
        /// <returns>The length of the marker, or zero when the text at the offset is not the expected marker.</returns>
        private static int MarkerLength(string text, int offset, int lineEnd, bool ordered, out int digits)
        {
            digits = 0;
            if (offset >= lineEnd)
            {
                return 0;
            }

            if (!ordered)
            {
                return text[offset] is '-' or '*' or '+' ? 1 : 0;
            }

            var end = offset;
            while (end < lineEnd && char.IsAsciiDigit(text[end]))
            {
                end++;
            }

            digits = end - offset;
            return digits is >= 1 and <= 9 && end < lineEnd && text[end] is '.' or ')' ? digits + 1 : 0;
        }

        /// <summary>
        ///     Determines whether any line of an item keeps the item from being moved by a constant.
        /// </summary>
        /// <param name="first">The first line of the item.</param>
        /// <param name="last">The last line of the item that has content.</param>
        /// <param name="contentColumn">The content column of the item.</param>
        /// <returns><see langword="true"/> when a tab, a lazy line, a grid table border or a footnote definition marker is found.</returns>
        private bool HasHazard(int first, int last, int contentColumn)
        {
            var layout = _context.Layout;
            var text = layout.Text;

            for (var line = first; line <= last; line++)
            {
                var start = layout.LineStart(line);
                var end = layout.LineEnd(line);
                if (layout.IsBlank(line))
                {
                    // A whitespace-only line that holds code or HTML is content, and a tab in it has no fixed width
                    if (_context.Ownership.IsProtected(line) && text.AsSpan(start, end - start).Contains('\t'))
                    {
                        return true;
                    }

                    continue;
                }

                // A line with the shape of the border of a grid table, or a footnote definition, is left where it is
                if (layout.LooksLikeGridBorder(line) || LineLayout.HasFootnoteDefinitionMarker(text.AsSpan(start, end - start)))
                {
                    return true;
                }

                // A tab before the text of the line (in the indentation, after a quote marker or after a list marker) makes
                // columns depend on tab stops, which a move changes
                if (HasTabInPrefix(text, start, end) || HasTabAfterMarker(start, end))
                {
                    return true;
                }

                // A line after the first with less indentation than the content column is a lazy continuation
                var indentation = 0;
                while (start + indentation < end && text[start + indentation] == ' ')
                {
                    indentation++;
                }

                if (line > first && indentation < contentColumn)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Determines whether a tab stands in the container prefix of a line: among the blanks and quote markers before its text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">The start of the line.</param>
        /// <param name="end">The end of the line.</param>
        /// <returns><see langword="true"/> when a tab is found before the first character that is not a blank or a quote marker.</returns>
        private static bool HasTabInPrefix(string text, int start, int end)
        {
            for (var position = start; position < end && text[position] is ' ' or '\t' or '>'; position++)
            {
                if (text[position] == '\t')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Determines whether a tab follows the marker of an item that starts on a line.
        /// </summary>
        /// <param name="start">The start of the line.</param>
        /// <param name="end">The end of the line.</param>
        /// <returns>
        ///     <see langword="true"/> when the blanks after the marker of an item on the line hold a tab. A number with a delimiter that is
        ///     only text (a paragraph line may start like a list item) is not a marker and does not count: the tab rule expands that tab.
        /// </returns>
        private bool HasTabAfterMarker(int start, int end)
        {
            var text = _context.Layout.Text;
            var index = _markers.BinarySearch(start);
            if (index < 0)
            {
                index = ~index;
            }

            for (; index < _markers.Count && _markers[index] < end; index++)
            {
                var offset = _markers[index];
                var length = MarkerLength(text, offset, end, char.IsAsciiDigit(text[offset]), out _);
                for (var position = offset + length; length > 0 && position < end && LineLayout.IsBlankChar(text[position]); position++)
                {
                    if (text[position] == '\t')
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Determines whether the items of a list follow each other without overlap and without content between them.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <returns><see langword="true"/> when only blank lines separate one item from the next.</returns>
        private bool IsTiled(ImmutableArray<ListItemModel>.Builder items)
        {
            var layout = _context.Layout;
            for (var i = 0; i + 1 < items.Count; i++)
            {
                if (items[i].LastLine >= items[i + 1].FirstLine)
                {
                    return false;
                }

                for (var line = items[i].LastLine + 1; line < items[i + 1].FirstLine; line++)
                {
                    if (!layout.IsBlank(line))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
