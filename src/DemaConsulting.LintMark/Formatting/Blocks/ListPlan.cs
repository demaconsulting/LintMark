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

using System.Globalization;
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Decides what changes in a nest of lists and adds the edits: the marker characters, the numbers, the columns
///     of the markers and of everything inside the items, and the spaces after the markers.
/// </summary>
/// <remarks>
///     <para>
///         The decisions are made per list and are all-or-nothing for it: the markers of a bullet list change together
///         or not at all, the numbers of an ordered list likewise. Marker characters and numbers that keep their width are
///         edits that change no column (<em>tier 1</em>) and apply at any depth, also inside block quotes. A list in the
///         body of a footnote is left as written: the proof below renders the lines of the nest by themselves, where the
///         four-space indent of a footnote body reads as indented code, so it cannot show that a rewrite keeps the meaning.
///         Everything
///         that changes a column (<em>tier 2</em>: the column of a marker, the spaces after it, a number that gains or loses a
///         digit) needs the columns of the lines to mean what they say, so it is only done for a nest at the top level of the
///         document whose items can be moved (see <see cref="ListModel"/>).
///     </para>
///     <para>
///         A moved item shifts all its lines by the change of its content column. The relative indentation of
///         everything inside it (nested lists, indented and fenced code, HTML, further paragraphs) is then what it was, so the
///         structure is the same. A nested list that cannot be moved is shifted as part of its item. The target column of a
///         list is computed from the new content column of the item it belongs to: a top-level bullet list starts at the margin,
///         a bullet list inside a bullet item starts <c>list-indent</c> columns right of the marker of the item, and any
///         other nested list keeps its offset to the content of its item; every item of a list gets the column of the list.
///     </para>
///     <para>
///         Every edit replaces existing characters (never a zero-length insertion), so it cannot collide with the
///         blank line the block spacing rule inserts at the start of a line, and each line receives at most one shift edit
///         (a line belongs to its innermost item). Whitespace-only lines are never changed, except those inside code or HTML,
///         where they are content that must move with the rest.
///     </para>
///     <para>
///         Thread safety: an instance is created for one call and used by one thread.
///     </para>
/// </remarks>
internal sealed class ListPlan
{
    /// <summary>
    ///     The largest number an ordered list marker may have (nine digits).
    /// </summary>
    private const long MaxNumber = 999_999_999;

    /// <summary>
    ///     The document, settings and line arithmetic.
    /// </summary>
    private readonly FormatContext _context;

    /// <summary>
    ///     Receives the edits once the plan is final.
    /// </summary>
    private readonly EditSet _edits;

    /// <summary>
    ///     The edits of the plan so far; they are given to <see cref="_edits"/> only when the plan is accepted.
    /// </summary>
    private readonly List<TextEdit> _planned = [];

    /// <summary>
    ///     The number of lines after a nest that the proof of the plan renders with it.
    /// </summary>
    private const int ProofTailLines = 3;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ListPlan"/> class.
    /// </summary>
    /// <param name="context">The document, settings and line arithmetic. Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public ListPlan(FormatContext context, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(edits);
        _context = context;
        _edits = edits;
    }

    /// <summary>
    ///     Where a nested list stands, as seen from the item that holds it: the columns of the item after it moved.
    /// </summary>
    /// <param name="OldContent">The content column of the item before.</param>
    /// <param name="NewContent">The content column of the item after.</param>
    /// <param name="NewMarker">The column of the marker of the item after.</param>
    /// <param name="ParentIsBullet">Whether the item belongs to a bullet list.</param>
    private readonly record struct Frame(int OldContent, int NewContent, int NewMarker, bool ParentIsBullet);

    /// <summary>
    ///     Gets the text of the document.
    /// </summary>
    private string Text => _context.Layout.Text;

    /// <summary>
    ///     Adds the edits for a nest of lists.
    /// </summary>
    /// <param name="outermost">The model of the outermost list. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outermost"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    /// <remarks>
    ///     The columns of the nest are planned first and the plan is proved: both texts, with and without the edits of the plan
    ///     (and with the edits that other formatters propose for the same lines), must render the same. A move that shrinks the
    ///     content column of an item could make a line after the item belong to it, and the parser's reading of nested
    ///     items is not always what the specification says, so the proof, not a rule of thumb, decides. A plan that fails it is made
    ///     again without any change of columns, and that one is proved too; when it fails as well the nest stays as written.
    /// </remarks>
    public void Apply(ListModel outermost)
    {
        ArgumentNullException.ThrowIfNull(outermost);
        ProcessList(outermost, frame: null, movableContext: true);
        if (_planned.Count > 0 && !Proves(outermost))
        {
            _planned.Clear();
            ProcessList(outermost, frame: null, movableContext: false);
            if (_planned.Count > 0 && !Proves(outermost))
            {
                _planned.Clear();
            }
        }

        foreach (var edit in _planned)
        {
            _edits.Add(edit);
        }
    }

    /// <summary>
    ///     Determines whether the edits of the plan leave the rendering of the nest and of the lines after it unchanged.
    /// </summary>
    /// <param name="outermost">The outermost list of the nest.</param>
    /// <returns>
    ///     <see langword="true"/> when the text from the first line of the nest to a few lines after it renders to the same HTML
    ///     with and without the edits of the plan, both with the edits that other formatters propose for those lines.
    /// </returns>
    /// <remarks>
    ///     The edits of the other formatters are part of both texts because they are part of the result: a line after a list that
    ///     another rule moves to the margin is no danger to the list, and one that it leaves indented is. The blocks after the list
    ///     were formatted first, so their edits are known here. Edits that overlap (which the edit set would reject later) cannot
    ///     be proved.
    /// </remarks>
    private bool Proves(ListModel outermost)
    {
        var layout = _context.Layout;
        var firstLine = outermost.FirstLine;
        var lastLine = outermost.LastLine;
        for (var counted = 0; lastLine + 1 < layout.LineCount && counted < ProofTailLines; lastLine++)
        {
            if (!layout.IsBlank(lastLine + 1))
            {
                counted++;
            }
        }

        var start = layout.LineStart(firstLine);
        var end = layout.LineEnd(lastLine);

        // An edit of another formatter that begins in the stretch and ends beyond it (a code block that is rewritten as a
        // fence) is applied whole, so the stretch grows to its end
        var snapshot = _edits.ToImmutable();
        var others = new List<TextEdit>();
        for (var grown = true; grown;)
        {
            others = [.. snapshot.Where(edit => edit.Start >= start && edit.Start <= end)];
            var reach = others.Count == 0 ? end : Math.Max(end, others.Max(static edit => edit.End));
            grown = reach > end;
            end = reach;
        }

        var original = Render(start, end, others);
        var combined = Render(start, end, [.. others, .. _planned.Where(edit => edit.Start >= start && edit.End <= end)]);
        return original is not null && combined is not null && string.Equals(original, combined, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Renders a stretch of the text after applying edits to it.
    /// </summary>
    /// <param name="start">The start of the stretch.</param>
    /// <param name="end">The end of the stretch.</param>
    /// <param name="edits">The edits, all inside the stretch.</param>
    /// <returns>The HTML; <see langword="null"/> when two edits overlap.</returns>
    private string? Render(int start, int end, List<TextEdit> edits)
    {
        var ordered = edits.OrderBy(static edit => edit.Start).ThenBy(static edit => edit.Length).ToList();
        var builder = new StringBuilder();
        var position = start;
        foreach (var edit in ordered)
        {
            if (edit.Start < position)
            {
                return null;
            }

            builder.Append(Text, position, edit.Start - position).Append(edit.Replacement);
            position = edit.End;
        }

        builder.Append(Text, position, end - position);
        return Markdown.ToHtml(builder.ToString(), DocumentParser.Pipeline);
    }

    /// <summary>
    ///     Adds an edit to the plan.
    /// </summary>
    /// <param name="edit">The edit.</param>
    private void Plan(TextEdit edit) => _planned.Add(edit);

    /// <summary>
    ///     Adds the edits for one list and for the lists inside it.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="frame">Where the item that holds the list stands after it moved; <see langword="null"/> for the outermost list.</param>
    /// <param name="movableContext">Whether the item that holds the list was moved by columns (false when it is kept as written).</param>
    private void ProcessList(ListModel list, Frame? frame, bool movableContext)
    {
        // Decide whether the columns of the list change; a list inside an item that does not move stays where it is
        var convertBullet = !list.IsOrdered && ShouldConvertBullet(list);
        int? target = movableContext ? MovableTarget(list, frame) : null;
        var numbers = NewNumbers(list, canShift: target is not null);

        for (var i = 0; i < list.Items.Length; i++)
        {
            var item = list.Items[i];

            // Tier 1: the marker keeps its width
            if (convertBullet)
            {
                Plan(new TextEdit(item.MarkerOffset, 1, TargetBullet().ToString(), RuleCatalog.BulletRuleId));
            }

            var digits = numbers?[i];
            if (digits is not null && !string.Equals(digits, Text.Substring(item.MarkerOffset, item.DigitsLength), StringComparison.Ordinal))
            {
                Plan(new TextEdit(item.MarkerOffset, item.DigitsLength, digits, RuleCatalog.OrderedStyleRuleId));
            }

            // Tier 2: columns; the lists inside the item follow the item when it moved
            Frame? inner = target is { } column ? MoveItem(list, item, column, digits) : null;
            foreach (var child in item.Lists)
            {
                ProcessList(child, inner, movableContext: inner is not null);
            }
        }
    }

    /// <summary>
    ///     Gets the marker character that bullet lists are written with.
    /// </summary>
    /// <returns>The character.</returns>
    private char TargetBullet() => _context.Config.Bullet switch
    {
        BulletMarker.Asterisk => '*',
        BulletMarker.Plus => '+',
        _ => '-'
    };

    /// <summary>
    ///     Computes the column a list is moved to, when its columns change at all.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="frame">The item that holds the list, as it stands after it moved; null for the outermost list.</param>
    /// <returns>The column, or <see langword="null"/> when the list is kept where it is: it cannot be moved, or its offset to the item would be out of range.</returns>
    private int? MovableTarget(ListModel list, Frame? frame) => list.IsMovable ? TargetColumn(list, frame) : null;

    /// <summary>
    ///     Determines whether a list inside a moved item is moved by its own plan rather than as part of the item.
    /// </summary>
    /// <param name="list">The nested list.</param>
    /// <param name="frame">The item that holds the list, as it stands after it moved.</param>
    /// <returns><see langword="true"/> when the list has a column of its own to move to.</returns>
    private bool MovesItself(ListModel list, Frame frame) => MovableTarget(list, frame) is not null;

    /// <summary>
    ///     Computes the column a list starts at after the change.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="frame">The item that holds the list, as it stands after it moved; null for the outermost list.</param>
    /// <returns>The column, or <see langword="null"/> when the list is kept as written (its offset to the item would be out of range).</returns>
    private int? TargetColumn(ListModel list, Frame? frame)
    {
        // The outermost list of a nest: bullets start at the margin, numbers keep the column of their first item
        if (frame is not { } parent)
        {
            return list.IsOrdered ? list.Items[0].MarkerColumn : 0;
        }

        // A bullet list inside a bullet item is indented from the marker of the item; any other list keeps its offset to the content
        var column = !list.IsOrdered && parent.ParentIsBullet
            ? parent.NewMarker + _context.Config.ListIndent
            : parent.NewContent + (list.Items[0].MarkerColumn - parent.OldContent);
        var offset = column - parent.NewContent;
        var oldOffset = list.Items[0].MarkerColumn - parent.OldContent;
        return offset is >= 0 and <= 3 && oldOffset is >= 0 and <= 3 ? column : null;
    }

    /// <summary>
    ///     Adds the tier 2 edits of one item: its marker line and the shift of its other lines.
    /// </summary>
    /// <param name="list">The list of the item.</param>
    /// <param name="item">The item.</param>
    /// <param name="column">The new column of the marker.</param>
    /// <param name="digits">The new number of an ordered item; null when the numbers are left as written.</param>
    /// <returns>Where the item stands after the change, for the lists inside it.</returns>
    private Frame MoveItem(ListModel list, ListItemModel item, int column, string? digits)
    {
        var layout = _context.Layout;
        var markerLength = list.IsOrdered ? (digits?.Length ?? item.DigitsLength) + 1 : 1;
        var newContent = column + markerLength + 1;
        var shift = newContent - item.OldContentColumn;
        var frame = new Frame(item.OldContentColumn, newContent, column, !list.IsOrdered);

        // The marker line: indentation and the spaces after the marker
        if (item.MarkerColumn != column)
        {
            Plan(new TextEdit(layout.LineStart(item.MarkerLine), item.MarkerColumn, new string(' ', column), RuleCatalog.ListIndentRuleId));
        }

        if (item.SpacesAfter != 1)
        {
            Plan(new TextEdit(item.MarkerOffset + item.MarkerLength, item.SpacesAfter, " ", RuleCatalog.ListMarkerSpaceRuleId));
        }

        // The rest of the item moves with its content column, except the lists inside it that move by themselves
        if (shift == 0)
        {
            return frame;
        }

        var rule = RuleForShift(item, column);
        var own = item.Lists
            .Where(child => MovesItself(child, frame))
            .Select(static child => (child.FirstLine, child.LastLine))
            .ToList();
        ShiftLines(item, shift, rule, own);
        return frame;
    }

    /// <summary>
    ///     Chooses the rule that the shift of the lines of an item is reported under.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="column">The new column of the marker.</param>
    /// <returns>The indentation rule when the marker moves, the marker space rule when only the spaces after it change, otherwise the numbering rule.</returns>
    private static string RuleForShift(ListItemModel item, int column)
    {
        if (item.MarkerColumn != column)
        {
            return RuleCatalog.ListIndentRuleId;
        }

        return item.SpacesAfter != 1 ? RuleCatalog.ListMarkerSpaceRuleId : RuleCatalog.OrderedStyleRuleId;
    }

    /// <summary>
    ///     Shifts the lines of an item after its marker line by a number of columns.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="shift">The number of columns, positive to the right.</param>
    /// <param name="ruleId">The rule that the edits belong to.</param>
    /// <param name="skipped">The line ranges of the lists inside the item that are moved by their own plan.</param>
    private void ShiftLines(ListItemModel item, int shift, string ruleId, List<(int First, int Last)> skipped)
    {
        var layout = _context.Layout;
        var text = layout.Text;
        for (var line = item.FirstLine + 1; line <= item.LastLine; line++)
        {
            var covered = line;
            if (skipped.Exists(range => covered >= range.First && covered <= range.Last))
            {
                continue;
            }

            // A whitespace-only line changes only where it is code or HTML, and only when it reaches into the content of the item
            var start = layout.LineStart(line);
            var end = layout.LineEnd(line);
            var width = 0;
            while (start + width < end && text[start + width] == ' ')
            {
                width++;
            }

            var blank = start + width == end;
            if (blank && (!_context.Ownership.IsProtected(line) || width < item.OldContentColumn))
            {
                continue;
            }

            // Lazy lines and tabs were excluded when the item was judged movable, so the new width is never negative
            var newWidth = width + shift;
            if (newWidth >= 0 && width > 0)
            {
                Plan(new TextEdit(start, width, new string(' ', newWidth), ruleId));
            }
        }
    }

    /// <summary>
    ///     Computes the numbers an ordered list is written with.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="canShift">Whether the columns of the list may change (a number that gains or loses a digit moves the content of its item).</param>
    /// <returns>The new number of every item, or <see langword="null"/> when the numbering is left as written.</returns>
    /// <remarks>
    ///     The first number is kept, because it is the number the list starts at when it is rendered. A first number
    ///     with leading zeros, and a number of more than nine digits, leave the numbering alone.
    /// </remarks>
    private string[]? NewNumbers(ListModel list, bool canShift)
    {
        if (!list.IsOrdered)
        {
            return null;
        }

        var first = list.Items[0];
        var written = Text.Substring(first.MarkerOffset, first.DigitsLength);
        if (written.Length > 1 && written[0] == '0')
        {
            return null;
        }

        var start = long.Parse(written, CultureInfo.InvariantCulture);
        var numbers = new string[list.Items.Length];
        var widthChanges = false;
        for (var i = 0; i < numbers.Length; i++)
        {
            var value = _context.Config.OrderedStyle == OrderedStyle.One ? start : start + i;
            if (value > MaxNumber)
            {
                return null;
            }

            numbers[i] = value.ToString(CultureInfo.InvariantCulture);
            widthChanges |= numbers[i].Length != list.Items[i].DigitsLength;
        }

        return widthChanges && !canShift ? null : numbers;
    }

    /// <summary>
    ///     Determines whether the markers of a bullet list are changed to the configured character.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns>
    ///     <see langword="true"/> when the list does not already use the character, no item is empty, no new marker line would be
    ///     a thematic break or look like a grid table border, and no bullet list directly precedes or follows the list (it would
    ///     merge with it once both use the same character).
    /// </returns>
    private bool ShouldConvertBullet(ListModel list)
    {
        var target = TargetBullet();
        var text = Text;
        if (list.Items.All(item => text[item.MarkerOffset] == target) || list.Items.Any(static item => !item.HasText))
        {
            return false;
        }

        foreach (var item in list.Items)
        {
            var lineEnd = _context.Layout.LineEnd(item.MarkerLine);
            var rest = text.AsSpan(item.MarkerOffset + 1, lineEnd - item.MarkerOffset - 1);
            if (WouldBeThematicBreak(target, rest) || IsThematicBreakText(rest) || WouldBeGridBorder(rest))
            {
                return false;
            }
        }

        return !IsAdjacentToBulletList(list.Info);
    }

    /// <summary>
    ///     Determines whether text is a thematic break by itself: three or more of one of <c>-</c>, <c>*</c> and <c>_</c>, and blanks.
    /// </summary>
    /// <param name="rest">The text after a list marker.</param>
    /// <returns><see langword="true"/> when the item holds a thematic break.</returns>
    /// <remarks>
    ///     The thematic break rule may rewrite the characters of such a break in the same run, possibly to the character
    ///     the marker would be changed to, which would turn the whole line into a break (<c>* ***</c> to <c>- ---</c>); the marker
    ///     of such an item is left as written.
    /// </remarks>
    private static bool IsThematicBreakText(ReadOnlySpan<char> rest)
    {
        var kind = '\0';
        var count = 0;
        foreach (var c in rest)
        {
            if (LineLayout.IsBlankChar(c))
            {
                continue;
            }

            if (c is not ('-' or '*' or '_') || (kind != '\0' && c != kind))
            {
                return false;
            }

            kind = c;
            count++;
        }

        return count >= 3;
    }

    /// <summary>
    ///     Determines whether a marker line would be a thematic break after the marker is replaced.
    /// </summary>
    /// <param name="marker">The new marker character.</param>
    /// <param name="rest">The text after the marker.</param>
    /// <returns><see langword="true"/> when the marker and the rest hold three or more of one of <c>-</c>, <c>*</c> and <c>_</c> and nothing but blanks.</returns>
    private static bool WouldBeThematicBreak(char marker, ReadOnlySpan<char> rest)
    {
        var count = 1;
        foreach (var c in rest)
        {
            if (c == marker)
            {
                count++;
            }
            else if (!LineLayout.IsBlankChar(c))
            {
                return false;
            }
        }

        return marker is '-' or '*' or '_' && count >= 3;
    }

    /// <summary>
    ///     Determines whether a plus sign followed by this text has the shape of the border of a grid table.
    /// </summary>
    /// <param name="rest">The text after the marker.</param>
    /// <returns><see langword="true"/> when the text holds only dashes, colons, equal signs, plus signs and blanks, and at least one dash or equal sign.</returns>
    private static bool WouldBeGridBorder(ReadOnlySpan<char> rest)
    {
        var rule = false;
        foreach (var c in rest)
        {
            if (c is '-' or '=')
            {
                rule = true;
            }
            else if (c is not (':' or '+' or ' ' or '\t'))
            {
                return false;
            }
        }

        return rule;
    }

    /// <summary>
    ///     Determines whether a bullet list is directly preceded or followed by another bullet list in the same container.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns><see langword="true"/> when the nearest sibling before or after is a bullet list, or the list cannot be located.</returns>
    /// <remarks>
    ///     Two bullet lists next to each other are only two lists because their markers differ. Writing both with one
    ///     character would merge them into one list (and can make a tight list loose), so neither is changed.
    /// </remarks>
    private bool IsAdjacentToBulletList(BlockInfo list)
    {
        var blocks = _context.Document.Blocks;
        var index = -1;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (ReferenceEquals(blocks[i], list))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return true;
        }

        // The nearest earlier block with the same parent is the previous sibling; a shallower block ends the container
        for (var i = index - 1; i >= 0 && blocks[i].Depth >= list.Depth; i--)
        {
            if (ReferenceEquals(blocks[i].Parent, list.Parent) && blocks[i].Depth == list.Depth)
            {
                if (IsBulletList(blocks[i]))
                {
                    return true;
                }

                break;
            }
        }

        // The first later block with the same parent that is not inside this list is the next sibling
        for (var i = index + 1; i < blocks.Length && blocks[i].Depth >= list.Depth; i++)
        {
            if (blocks[i].Start > list.EndInclusive && ReferenceEquals(blocks[i].Parent, list.Parent) && blocks[i].Depth == list.Depth)
            {
                return IsBulletList(blocks[i]);
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a block is a bullet list.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> for a list that is not ordered.</returns>
    private static bool IsBulletList(BlockInfo block) => block.Kind == BlockKind.List && block.Node is ListBlock { IsOrdered: false };
}
