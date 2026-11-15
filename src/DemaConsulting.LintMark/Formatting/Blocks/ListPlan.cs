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
using System.Globalization;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;
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
///         body of a footnote is left as written: the four-space indent of a footnote body is not a column of the list.
///         Everything that changes a column (<em>tier 2</em>: the column of a marker, the spaces after it, a number that
///         gains or loses a digit) needs the columns of the lines to mean what they say, so it is only done for a nest at
///         the top level of the document whose items can be moved (see <see cref="ListModel"/>).
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
    ///     The items whose content column shrinks, with the new column, for the check of the line after them.
    /// </summary>
    private readonly List<(int LastLine, int NewContent)> _shrunk = [];

    /// <summary>
    ///     Whether a line that the plan moves is one whose meaning depends on its indentation (rules R-2 and G-2).
    /// </summary>
    private bool _hidesMeaning;

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
    ///     <para>
    ///         The nest is read from the text as it stands after every other rule has had its say (the document formatter gives the
    ///         list rules the text the other rules produce), so the lines around the nest are final. A nest that starts in a
    ///         footnote is left as written (F-1: the four-space indent of a footnote body is not a list column).
    ///     </para>
    ///     <para>
    ///         The columns of the nest are planned first. A plan that moves content is dropped for a plan without any change of
    ///         columns, and that one is kept, when the move could change the meaning: an item whose content column shrinks
    ///         would take over the next line when that line is indented to the new column (R-1); a fenced code, HTML or math
    ///         line that is not indented exactly to the content column of its item reads differently once the item moves (R-2);
    ///         a line of a paragraph that starts with block syntax is text only because of its indentation (G-2); a line that
    ///         the plan changes would be a thematic break or a setext underline by itself (G-1: <c>- ---</c> at the margin); two edits
    ///         overlap (R-3). The plan without changes of columns is dropped as well in the last case.
    ///     </para>
    /// </remarks>
    public void Apply(ListModel outermost)
    {
        ArgumentNullException.ThrowIfNull(outermost);
        if (_context.Ownership.IsInFootnote(outermost.FirstLine))
        {
            return;
        }

        ProcessList(outermost, frame: null, movableContext: true);
        if (_planned.Count > 0 && !IsSafe())
        {
            Reset();
            ProcessList(outermost, frame: null, movableContext: false);
            if (HasOverlap(_planned))
            {
                Reset();
            }
        }

        foreach (var edit in _planned)
        {
            _edits.Add(edit);
        }
    }

    /// <summary>
    ///     Forgets the plan made so far.
    /// </summary>
    private void Reset()
    {
        _planned.Clear();
        _shrunk.Clear();
        _hidesMeaning = false;
    }

    /// <summary>
    ///     Determines whether the plan keeps the meaning of the nest and of the line after it (rules R-1 to R-3).
    /// </summary>
    /// <returns><see langword="true"/> when no rule refuses the plan.</returns>
    private bool IsSafe() =>
        !_hidesMeaning
        && !HasOverlap(_planned)
        && _shrunk.All(item => LineAfterStaysOut(item.LastLine, item.NewContent))
        && !ChangesLineIntoBreak();

    /// <summary>
    ///     Determines whether two edits of a plan overlap (rule R-3).
    /// </summary>
    /// <param name="edits">The edits.</param>
    /// <returns><see langword="true"/> when an edit starts before the previous one ends.</returns>
    private static bool HasOverlap(List<TextEdit> edits)
    {
        var ordered = edits.OrderBy(static edit => edit.Start).ThenBy(static edit => edit.Length).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Start < ordered[i - 1].End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the first line with content after an item stays outside it once the item has a new content column (rule R-1).
    /// </summary>
    /// <param name="lastLine">The last line of the item.</param>
    /// <param name="newContent">The column from which a line is taken into the item.</param>
    /// <returns><see langword="true"/> when there is no such line, or its indentation, as the plan leaves it, is less than the column.</returns>
    private bool LineAfterStaysOut(int lastLine, int newContent)
    {
        var layout = _context.Layout;
        var next = lastLine + 1;
        while (next < layout.LineCount && layout.IsBlank(next))
        {
            next++;
        }

        if (next >= layout.LineCount)
        {
            return true;
        }

        // A tab in the indentation makes the column uncertain
        var start = layout.LineStart(next);
        var end = layout.LineEnd(next);
        var width = 0;
        while (start + width < end && Text[start + width] == ' ')
        {
            width++;
        }

        if (start + width < end && Text[start + width] == '\t')
        {
            return false;
        }

        // The line may be moved by this plan or by the plan of a nest after this one (the nests are planned from the end of the
        // document): the edit of its leading blanks decides
        bool IsLeadingBlanksEdit(TextEdit candidate) => candidate.Length <= width && candidate.Replacement.All(static c => c == ' ');
        var edit = _planned.FirstOrDefault(candidate => candidate.Start == start && IsLeadingBlanksEdit(candidate)) is { Replacement: not null } own
            ? own
            : _edits.FindStartingAt(start, IsLeadingBlanksEdit);
        var indent = edit is { } moved ? width - moved.Length + moved.Replacement.Length : width;
        return indent < newContent;
    }

    /// <summary>
    ///     Determines whether a line that the plan changes would be a thematic break or a setext underline by itself (rule G-1).
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> when, with the edits of the plan applied, a changed line outside code is three or more of one of
    ///     <c>-</c>, <c>*</c>, <c>_</c> and <c>=</c> and blanks: <c>- ---</c> at the margin is a thematic break, although the same
    ///     line inside a list may be an item that holds one.
    /// </returns>
    private bool ChangesLineIntoBreak()
    {
        var layout = _context.Layout;
        foreach (var group in _planned.GroupBy(edit => layout.LineOf(edit.Start)))
        {
            if (_context.Ownership.LeafAt(group.Key)?.Kind is BlockKind.FencedCode or BlockKind.UnclosedFence or BlockKind.IndentedCode or BlockKind.HtmlBlock or BlockKind.MathBlock)
            {
                continue;
            }

            var start = layout.LineStart(group.Key);
            var builder = new System.Text.StringBuilder();
            var position = start;
            foreach (var edit in group.OrderBy(static edit => edit.Start).ThenBy(static edit => edit.Length))
            {
                builder.Append(Text, position, edit.Start - position).Append(edit.Replacement);
                position = edit.End;
            }

            builder.Append(Text, position, layout.LineEnd(group.Key) - position);
            if (IsRuleShaped(builder.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a text is three or more of one of <c>-</c>, <c>*</c>, <c>_</c> and <c>=</c>, and blanks.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for a text of that shape.</returns>
    private static bool IsRuleShaped(string text)
    {
        var kind = '\0';
        var count = 0;
        foreach (var c in text)
        {
            if (LineLayout.IsBlankChar(c))
            {
                continue;
            }

            if (c is not ('-' or '*' or '_' or '=') || (kind != '\0' && c != kind))
            {
                return false;
            }

            kind = c;
            count++;
        }

        return count >= 3;
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
            Frame? inner = target is { } column ? MoveItem(list, item, column, digits, frame) : null;
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
        return offset is >= 0 and <= ListModel.MaxIndent && oldOffset is >= 0 and <= ListModel.MaxIndent ? column : null;
    }

    /// <summary>
    ///     Adds the tier 2 edits of one item: its marker line and the shift of its other lines.
    /// </summary>
    /// <param name="list">The list of the item.</param>
    /// <param name="item">The item.</param>
    /// <param name="column">The new column of the marker.</param>
    /// <param name="digits">The new number of an ordered item; null when the numbers are left as written.</param>
    /// <param name="parent">Where the item that holds the list stands after the change; <see langword="null"/> for the outermost list.</param>
    /// <returns>Where the item stands after the change, for the lists inside it.</returns>
    private Frame MoveItem(ListModel list, ListItemModel item, int column, string? digits, Frame? parent)
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

        // A line after the item that is indented to the column where the parser lets a line continue the item is taken into the item.
        // That column is the content column, and for a list inside an item it can be as low as the width of the marker from the
        // content column of the item that holds it (the parser counts the indentation from there)
        if (shift < 0)
        {
            _shrunk.Add((item.LastLine, Math.Min(newContent, (parent?.NewContent ?? 0) + markerLength + 1)));
        }

        var rule = RuleForShift(item, column);
        var own = item.Lists
            .Where(child => MovesItself(child, frame))
            .Select(static child => (child.FirstLine, child.LastLine))
            .ToList();
        ShiftLines(item, shift, rule, own, nested: parent is not null);
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
    /// <param name="nested">Whether the item is inside another item.</param>
    private void ShiftLines(ListItemModel item, int shift, string ruleId, List<(int First, int Last)> skipped, bool nested)
    {
        var layout = _context.Layout;
        var text = layout.Text;
        var next = 0;
        for (var line = item.FirstLine + 1; line <= item.LastLine; line++)
        {
            // The ranges are in source order and do not overlap, so one pass over them is enough
            while (next < skipped.Count && skipped[next].Last < line)
            {
                next++;
            }

            if (next < skipped.Count && line >= skipped[next].First)
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

            // The meaning of some lines depends on their indentation relative to the content column (rules R-2 and G-2)
            if (!blank && DependsOnIndentation(item, nested, line, start + width, end, width))
            {
                _hidesMeaning = true;
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
    ///     Determines whether the meaning of a line that moves with its item depends on how far it is indented (rules R-2 and G-2).
    /// </summary>
    /// <param name="item">The item that moves.</param>
    /// <param name="nested">Whether the item is inside another item.</param>
    /// <param name="line">The zero-based line, which is not blank.</param>
    /// <param name="contentStart">The offset of the first character after the leading spaces.</param>
    /// <param name="end">The offset where the line ends.</param>
    /// <param name="width">The number of leading spaces of the line.</param>
    /// <returns>
    ///     <see langword="true"/> when the line is fenced or indented code, HTML or math (the parser strips the indentation of such lines
    ///     relative to a column that depends on the geometry of the nest, and in some nests it reads the same lines
    ///     differently for different columns, so they are never moved), or a continuation line of a paragraph that starts with
    ///     block syntax (it is text only because of its indentation).
    /// </returns>
    private bool DependsOnIndentation(ListItemModel item, bool nested, int line, int contentStart, int end, int width)
    {
        var leaf = _context.Ownership.LeafAt(line);
        if (leaf is null)
        {
            return false;
        }

        if (leaf.Kind is BlockKind.FencedCode or BlockKind.UnclosedFence or BlockKind.IndentedCode or BlockKind.HtmlBlock or BlockKind.MathBlock)
        {
            // In a nested item, and for indented code in an item that holds a list, the parser measures such lines from columns
            // that are not the content column of the item, so they stay where they are; otherwise they must be indented to the
            // content column (plus four for indented code)
            return leaf.Kind == BlockKind.IndentedCode
                ? nested || !item.Lists.IsEmpty || width - item.OldContentColumn < 4
                : nested || width != item.OldContentColumn;
        }

        if (leaf.Kind != BlockKind.Paragraph || _context.Layout.LineOf(leaf.Start) == line)
        {
            return false;
        }

        // The first word of a continuation line, after the quote markers of a paragraph in a quote
        var rest = Text.AsSpan(contentStart, end - contentStart);
        while (!rest.IsEmpty && rest[0] == '>')
        {
            rest = rest[1..].TrimStart(' ');
        }

        var blank = rest.IndexOfAny(' ', '\t');
        var word = blank < 0 ? rest : rest[..blank];
        return !word.IsEmpty && BlockSyntaxGuard.IsCommonMarkBlockStart(word.ToString());
    }

    /// <summary>
    ///     Computes the numbers an ordered list is written with.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="canShift">Whether the columns of the list may change (a number that gains or loses a digit moves the content of its item).</param>
    /// <returns>The new number of every item, or <see langword="null"/> when the numbering is left as written.</returns>
    /// <remarks>
    ///     The first number is kept, because it is the number the list starts at. A first number
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
    ///     a thematic break, no item holds a thematic break (which the thematic break rule may rewrite to the new character),
    ///     no marker line that has a plus sign, as the new marker or as the one it replaces, would look like the border of a
    ///     grid table (only a plus sign can start one, and the parser may read such a line as part of a table instead of as
    ///     an item), and no bullet list directly precedes or follows the list (it would merge with it once both use the same
    ///     character).
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

            // A line that starts with a plus sign and holds only the characters of a grid table border can be read as part
            // of a grid table, whether the plus sign is the marker that is written or the one that is replaced
            var plusMarker = target == '+' || text[item.MarkerOffset] == '+';
            if (WouldBeThematicBreak(target, rest) || IsThematicBreakText(rest) || (plusMarker && WouldBeGridBorder(rest)))
            {
                return false;
            }
        }

        return !IsAdjacentToBulletList(list.Info) && !IsFollowedByBreakLine(list);
    }

    /// <summary>
    ///     Determines whether the first line with content after a list is shaped like a thematic break.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <returns>
    ///     <see langword="true"/> when that line is three or more of one of <c>-</c>, <c>*</c>, <c>_</c> and <c>=</c> and blanks (an
    ///     item of a list that uses the same marker character, such as <c>- ---</c>, may be read as an item of that list).
    /// </returns>
    private bool IsFollowedByBreakLine(ListModel list)
    {
        var layout = _context.Layout;
        var next = list.LastLine + 1;
        while (next < layout.LineCount && layout.IsBlank(next))
        {
            next++;
        }

        return next < layout.LineCount && IsRuleShaped(Text.Substring(layout.LineStart(next), layout.LineEnd(next) - layout.LineStart(next)).Replace('>', ' '));
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
        var index = IndexOf(blocks, list);
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
    ///     Finds the index of a block in the block list of the document.
    /// </summary>
    /// <param name="blocks">The blocks, in source order.</param>
    /// <param name="block">The block to find.</param>
    /// <returns>The index, or -1 when the block is not in the list.</returns>
    /// <remarks>
    ///     The list is ordered by the start of the blocks, so the search starts at the first block that begins where this
    ///     one does (a list, its first item and its first paragraph can begin at the same offset) instead of at the start
    ///     of the document.
    /// </remarks>
    private static int IndexOf(ImmutableArray<BlockInfo> blocks, BlockInfo block)
    {
        var low = 0;
        var high = blocks.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (blocks[middle].Start < block.Start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var i = low; i < blocks.Length && blocks[i].Start == block.Start; i++)
        {
            if (ReferenceEquals(blocks[i], block))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Determines whether a block is a bullet list.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> for a list that is not ordered.</returns>
    private static bool IsBulletList(BlockInfo block) => block.Kind == BlockKind.List && block.Node is ListBlock { IsOrdered: false };
}
