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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The document-level pass that limits runs of blank lines between blocks and empties whitespace-only lines.
/// </summary>
/// <remarks>
///     <para>
///         A run is a maximal sequence of blank lines that no leaf block covers. A run longer than the configured
///         maximum loses its surplus lines, and the lines that stay are emptied (a line of only spaces is a
///         blank line, but it is trailing whitespace). Inside a list, list item, block quote or footnote the
///         surplus lines are kept (removing a blank line there can change whether a list is loose), but their
///         whitespace is still removed. A run after an empty list item (a marker with no content) is kept as it is
///         everywhere: the parser ends a list at two blank lines after an empty item, so the number of lines is
///         structure. The run at the end of the file belongs to the final newline rule. A run
///         that follows a block that keeps its trailing blank lines (an unclosed fence), or that touches a line
///         looking like a grid table border, is left alone.
///     </para>
///     <para>
///         The pass never touches a line that a leaf block covers, so blank lines inside code, HTML or math are
///         content and stay as they are. Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class BlankLinePass
{
    /// <summary>
    ///     Adds the edits for every run of blank lines.
    /// </summary>
    /// <param name="document">The parsed document (for suppressions). Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block. Must not be null.</param>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Apply(ParsedDocument document, LineLayout layout, LineOwnership ownership, LintMarkConfig config, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(edits);

        var line = 0;
        while (line < layout.LineCount)
        {
            if (ownership.LeafAt(line) is not null || !layout.IsBlank(line))
            {
                line++;
                continue;
            }

            // Collect the maximal run of blank lines that no leaf block covers
            var first = line;
            while (line + 1 < layout.LineCount && ownership.LeafAt(line + 1) is null && layout.IsBlank(line + 1))
            {
                line++;
            }

            var last = line;
            line++;

            // The run that reaches the end of the file belongs to the final newline rule
            if (last == layout.LineCount - 1)
            {
                continue;
            }

            // Blank lines next to a line that looks like a grid table border are left alone: whether the table swallows the
            // lines after it can depend on them (an indented code block is judged after its conversion to a fence)
            if (IsBorderNeighbor(layout, ownership, first - 1) || IsBorderNeighbor(layout, ownership, last + 1))
            {
                continue;
            }

            // Blank lines after a block that keeps its trailing blank lines (an unclosed fence) are content of that block
            if (first > 0 && ownership.LeafAt(first - 1) is { } previous && LineOwnership.KeepsTrailingBlankLines(layout, previous, contentFollows: true))
            {
                continue;
            }

            // Two blank lines after an empty list item end the list for the parser, so a run there is not shortened; the same
            // holds for the content of an item whose marker line is empty
            var afterEmptyItem = first > 0
                && ((ownership.LeafAt(first - 1) is null
                        && IsEmptyItemLine(layout.Text.AsSpan(layout.LineStart(first - 1), layout.LineEnd(first - 1) - layout.LineStart(first - 1))))
                    || InItemWithEmptyMarkerLine(layout, ownership.LeafAt(first - 1)));
            HandleRun(document, layout, ownership, config.MaxBlankLines, first, last, afterEmptyItem, edits);
        }
    }

    /// <summary>
    ///     Determines whether a block lies in a list item whose marker line has no content.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="leaf">The leaf block before a run of blank lines, or <see langword="null"/> when there is none.</param>
    /// <returns>
    ///     <see langword="true"/> when an enclosing list item starts with a marker and nothing else on its line (its content
    ///     starts on a later line): the parser ends the list at two blank lines inside such an item as well.
    /// </returns>
    private static bool InItemWithEmptyMarkerLine(LineLayout layout, BlockInfo? leaf)
    {
        for (var parent = leaf?.Node?.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ListItemBlock item && item.Span.Start >= 0 && item.Span.Start < layout.Text.Length)
            {
                var line = layout.LineOf(item.Span.Start);
                if (IsEmptyItemLine(layout.Text.AsSpan(layout.LineStart(line), layout.LineEnd(line) - layout.LineStart(line))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a line is a list item that has no content: markers (after block quote markers and blanks) and nothing else.
    /// </summary>
    /// <param name="line">The text of the line, without its line ending.</param>
    /// <returns>
    ///     <see langword="true"/> for <c>-</c>, <c>1.</c>, <c>&gt; *</c> or <c>- -</c>; the parser ends a list that has an empty item
    ///     followed by two blank lines, so the number of blank lines after such a line can change the structure.
    /// </returns>
    private static bool IsEmptyItemLine(ReadOnlySpan<char> line)
    {
        var position = 0;
        var markers = 0;
        var sameBullets = 0;
        var bullet = '\0';
        while (true)
        {
            while (position < line.Length && line[position] is ' ' or '\t' or '>')
            {
                position++;
            }

            if (position == line.Length)
            {
                // Three or more of the same marker at the end is a thematic break (the thematic break rule rewrites it), not nested items
                return markers > 0 && !(sameBullets >= 3 && bullet is '-' or '*');
            }

            if (line[position] is '-' or '+' or '*')
            {
                sameBullets = line[position] == bullet ? sameBullets + 1 : 1;
                bullet = line[position];
                position++;
            }
            else
            {
                // An ordered marker: up to nine digits and a dot or a parenthesis
                var digits = 0;
                while (position < line.Length && digits < 9 && char.IsAsciiDigit(line[position]))
                {
                    position++;
                    digits++;
                }

                if (digits == 0 || position >= line.Length || line[position] is not ('.' or ')'))
                {
                    return false;
                }

                sameBullets = 0;
                bullet = '\0';
                position++;
            }

            markers++;
            if (position < line.Length && line[position] is not (' ' or '\t'))
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Determines whether a line next to a run of blank lines looks like a grid table border and stays as it is.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines belong to which block.</param>
    /// <param name="line">The zero-based line before or after the run; may be outside the text.</param>
    /// <returns>
    ///     <see langword="true"/> when the line looks like a border and is not part of an indented code block (which
    ///     the code block formatter turns into a fence, so the line is then inside code).
    /// </returns>
    private static bool IsBorderNeighbor(LineLayout layout, LineOwnership ownership, int line)
    {
        if (line < 0 || line >= layout.LineCount || ownership.LeafAt(line)?.Kind == BlockKind.IndentedCode)
        {
            return false;
        }

        if (layout.LooksLikeGridBorder(line))
        {
            return true;
        }

        // A paragraph with a border-shaped line is read by the grid table rules as a whole, whichever of its lines meets the run
        if (ownership.LeafAt(line) is { Kind: BlockKind.Paragraph } paragraph)
        {
            var (first, last) = LineOwnership.LinesOf(layout, paragraph);
            for (var other = first; other <= last; other++)
            {
                if (layout.LooksLikeGridBorder(other))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Handles one run of blank lines: removes the surplus lines and empties the lines that stay.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic.</param>
    /// <param name="ownership">Which lines belong to which block.</param>
    /// <param name="maxBlankLines">The number of blank lines that may stay.</param>
    /// <param name="first">First line of the run.</param>
    /// <param name="last">Last line of the run.</param>
    /// <param name="keepSurplus">Whether the surplus lines stay (they follow an empty list item).</param>
    /// <param name="edits">Receives the edits.</param>
    private static void HandleRun(
        ParsedDocument document,
        LineLayout layout,
        LineOwnership ownership,
        int maxBlankLines,
        int first,
        int last,
        bool keepSurplus,
        EditSet edits)
    {
        // Surplus lines are removed completely, line feed included; not inside a container
        var kept = last - first + 1;
        var start = layout.LineStart(first);
        var end = layout.LineEnd(last);
        if (kept > maxBlankLines && !keepSurplus && !ownership.IsInContainer(first)
            && !IsSuppressed(document, start, end, RuleCatalog.MaxBlankLinesRuleId))
        {
            kept = maxBlankLines;
            edits.Add(new TextEdit(
                layout.LineStart(first + kept),
                layout.LineStart(last + 1) - layout.LineStart(first + kept),
                string.Empty,
                RuleCatalog.MaxBlankLinesRuleId));
        }

        // The lines that stay lose their whitespace
        if (IsSuppressed(document, start, end, RuleCatalog.TrailingWhitespaceRuleId))
        {
            return;
        }

        for (var line = first; line < first + kept; line++)
        {
            var length = layout.LineEnd(line) - layout.LineStart(line);
            if (length > 0)
            {
                edits.Add(new TextEdit(layout.LineStart(line), length, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
            }
        }
    }

    /// <summary>
    ///     Determines whether a rule is suppressed anywhere in a range.
    /// </summary>
    /// <param name="document">The parsed document that holds the suppressions.</param>
    /// <param name="start">Offset of the first character of the range.</param>
    /// <param name="end">Offset of the end of the last line of the range (its line feed).</param>
    /// <param name="ruleId">The rule.</param>
    /// <returns><see langword="true"/> when a suppression covers part of the range.</returns>
    private static bool IsSuppressed(ParsedDocument document, int start, int end, string ruleId) =>
        document.Suppressions.IsSuppressedRange(start, end + 1, ruleId);
}

/// <summary>
///     The document-level pass that puts a blank line between top-level blocks that must be separated:
///     headings, fenced code blocks, lists and tables.
/// </summary>
/// <remarks>
///     <para>
///         Two top-level blocks that follow each other without a blank line are a violation when one of them is a
///         heading, a fenced (or convertible indented) code block, a list or a pipe table. The decision is made
///         from the parsed blocks, never from the look of the lines: <c>text</c> followed by <c>2. item</c> is
///         one paragraph, a line after a list item is a lazy continuation, and a row after a table is a table row,
///         so none of them is a pair of blocks and none is touched.
///     </para>
///     <para>
///         Only top-level blocks are separated. Inserting a blank line next to a block inside a list item or a
///         block quote can change what the document means (a tight list becomes loose), so a violation inside a
///         container is neither fixed nor reported. One blank line is inserted per boundary, attributed to the
///         first applicable rule that is not suppressed (heading, fence, list, table), because two rules that
///         insert at the same offset would conflict. Only block kinds whose end the parser reports reliably are
///         separated (not an unclosed fence, a grid table, a footnote definition or a block that the tool does not model), and
///         no blank line is inserted next to a line that looks like a grid table border or before a block that
///         is indented by more than three columns (such a block only exists because of what precedes it). Nor is a
///         blank line inserted between a next-line suppression directive and the first line of the block it
///         covers: the blank line would take the directive's place and the rule it suppresses would then fire.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class BlockSpacingPass
{
    /// <summary>
    ///     The spacing rules in the order in which they claim a boundary.
    /// </summary>
    private static readonly (string RuleId, Func<BlockInfo, bool> Applies)[] Rules =
    [
        (RuleCatalog.HeadingBlankLinesRuleId, static block => block.Kind == BlockKind.Heading),
        (RuleCatalog.FenceBlankLinesRuleId, static block => block.Kind is BlockKind.FencedCode or BlockKind.IndentedCode),
        (RuleCatalog.ListBlankLinesRuleId, static block => block.Kind == BlockKind.List),
        (RuleCatalog.TableBlankLinesRuleId, static block => block.Kind == BlockKind.Table)
    ];

    /// <summary>
    ///     Adds an insertion of a blank line for every boundary between top-level blocks that needs one.
    /// </summary>
    /// <param name="document">The parsed document (for suppressions). Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block (supplies the top-level blocks). Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Apply(ParsedDocument document, LineLayout layout, LineOwnership ownership, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(edits);

        var blocks = ownership.TopLevel;
        for (var i = 0; i + 1 < blocks.Length; i++)
        {
            var before = blocks[i];
            var after = blocks[i + 1];
            if (!IsSeparable(before) || !IsSeparable(after))
            {
                continue;
            }

            // Only blocks that follow each other directly are a violation; a block whose reported start is a blank line (the parser's
            // span of a paragraph that looks like a grid table border can include the blank lines before it) is not placed reliably
            var beforeLast = LineOwnership.LinesOf(layout, before).Last;
            var afterFirst = LineOwnership.LinesOf(layout, after).First;
            if (afterFirst != beforeLast + 1 || layout.IsBlank(afterFirst))
            {
                continue;
            }

            // A blank line next to a line that looks like a grid table border could change how Pandoc reads the table
            // (an indented code block is judged after its conversion, when the line is inside a fence)
            if (HoldsBorderLine(layout, ownership, before, beforeLast) || HoldsBorderLine(layout, ownership, after, afterFirst))
            {
                continue;
            }

            // A block that does not start at the beginning of its line, or after more than three columns of indentation or a
            // tab, is not one that the parser read as a block of its own at the left margin (for instance a definition that
            // starts after the text of another line): a blank line before its line would not separate it from what precedes it
            if (after.Kind != BlockKind.IndentedCode)
            {
                var indentation = layout.Text.AsSpan(layout.LineStart(afterFirst), after.Start - layout.LineStart(afterFirst));
                if (indentation.Length > 3 || indentation.Contains('\t'))
                {
                    continue;
                }
            }

            // A blank line after a block that keeps its trailing blank lines would become part of that block
            if (ownership.LeafAt(beforeLast) is { } lastLeaf && LineOwnership.KeepsTrailingBlankLines(layout, lastLeaf, contentFollows: true))
            {
                continue;
            }

            // A next-line suppression directive written before the second block applies to its first line only as long
            // as nothing is inserted between the two (the inserted blank line would take its place)
            if (TargetsNextLine(document, layout, afterFirst))
            {
                continue;
            }

            // A blank line must not change how the first line of the second block is read: after a link reference definition a
            // paragraph is still open, so a setext heading that starts with a list marker (an empty item, or a number other than 1,
            // cannot interrupt a paragraph) is read as a list once a blank line precedes it; and a thematic-break-shaped line that
            // the parser reads as a list item right after a list item is a thematic break once a blank line precedes it
            if (ReadsDifferentlyAfterBlankLine(layout, before, after, afterFirst))
            {
                continue;
            }

            // The first applicable rule that is not suppressed on either block inserts the blank line
            foreach (var (ruleId, applies) in Rules)
            {
                if (!applies(before) && !applies(after))
                {
                    continue;
                }

                if (document.Suppressions.IsSuppressedRange(before.Start, after.EndInclusive + 1, ruleId))
                {
                    continue;
                }

                edits.Add(new TextEdit(layout.LineStart(afterFirst), 0, "\n", ruleId));
                break;
            }
        }
    }

    /// <summary>
    ///     Determines whether a block holds a line that looks like a grid table border where it meets the next block, or anywhere in a container.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines belong to which block.</param>
    /// <param name="block">The top-level block.</param>
    /// <param name="edgeLine">The line of the block that meets the neighbor (its last line before the boundary, or its first line after it).</param>
    /// <returns>
    ///     <see langword="true"/> when the edge line looks like a border and is not indented code, or when the block is a list or a
    ///     block quote and any of its lines does (outside indented code): the parser reads the text after such a line by the rules
    ///     of the grid table extension, which can depend on the blank lines.
    /// </returns>
    private static bool HoldsBorderLine(LineLayout layout, LineOwnership ownership, BlockInfo block, int edgeLine)
    {
        if (block.Kind is BlockKind.List or BlockKind.BlockQuote)
        {
            var (first, last) = LineOwnership.LinesOf(layout, block);
            for (var line = first; line <= last; line++)
            {
                if (layout.LooksLikeGridBorder(line) && ownership.LeafAt(line)?.Kind != BlockKind.IndentedCode)
                {
                    return true;
                }
            }

            return false;
        }

        return block.Kind != BlockKind.IndentedCode && layout.LooksLikeGridBorder(edgeLine);
    }

    /// <summary>
    ///     Determines whether the first line of a block is read differently once a blank line is put before it.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="before">The block before the boundary.</param>
    /// <param name="after">The block after the boundary.</param>
    /// <param name="afterFirst">The first line of <paramref name="after"/>.</param>
    ///     <see langword="true"/> when <paramref name="before"/> is a link reference definition (it leaves a paragraph open) and
    ///     <paramref name="after"/> is a setext heading whose first line starts with a list marker, or when <paramref name="after"/>
    ///     is a list whose first line is shaped like a thematic break (or like an item that holds one).
    /// </returns>
    /// <remarks>
    ///     The setext heading case is the counterpart of the rule of the heading formatter that keeps such a heading setext
    ///     (<see cref="LineLayout.StartsWithListMarker"/>), so that a second run finds nothing to change.
    /// </remarks>
    private static bool ReadsDifferentlyAfterBlankLine(LineLayout layout, BlockInfo before, BlockInfo after, int afterFirst) =>
        (before.Kind == BlockKind.LinkReferenceDefinition && after.Node is HeadingBlock { IsSetext: true } && layout.StartsWithListMarker(afterFirst))
        || (after.Kind == BlockKind.List && layout.LooksLikeThematicBreak(afterFirst));

    /// <summary>
    ///     Determines whether a suppression covers exactly one line, the line given: the shape of a next-line directive.
    /// </summary>
    /// <param name="document">The parsed document that holds the suppressions.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when a suppression range of any rule starts at the start of the line and ends at the start of the next.</returns>
    /// <remarks>
    ///     A <c>disable-next-line</c> directive (of the lintmark or the markdownlint family) on the line before covers
    ///     exactly that range, whichever rules it names. A blank line inserted between the directive and the line would
    ///     take the directive's place, and the line would then be formatted by the rule that the author suppressed. A
    ///     <c>disable-line</c> directive on the line itself has the same shape and is treated the same way: the
    ///     blank line is then not inserted, which only leaves a missing blank line as it was.
    /// </remarks>
    private static bool TargetsNextLine(ParsedDocument document, LineLayout layout, int line)
    {
        var start = layout.LineStart(line);
        var end = Math.Min(layout.LineEnd(line) + 1, layout.Text.Length);

        // The ranges are sorted by start, so find the first one that starts at the line start by bisection and scan
        // the ranges with that start for the end (several ranges can start at the same offset)
        var ranges = document.Suppressions.Ranges;
        var low = 0;
        var high = ranges.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (ranges[middle].Start < start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var i = low; i < ranges.Count && ranges[i].Start == start; i++)
        {
            if (ranges[i].End == end)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a blank line may be inserted next to a block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>
    ///     <see langword="true"/> for the block kinds whose end the parser reports reliably; <see langword="false"/> for an
    ///     unclosed fence, a grid table, footnote definitions (their label and body are read differently once a
    ///     blank line separates them from what precedes) and blocks the tool does not model.
    /// </returns>
    private static bool IsSeparable(BlockInfo block) => block.Kind is
        BlockKind.Heading or BlockKind.Paragraph or BlockKind.FencedCode or BlockKind.IndentedCode or BlockKind.HtmlBlock
        or BlockKind.ThematicBreak or BlockKind.BlockQuote or BlockKind.List or BlockKind.Table
        or BlockKind.LinkReferenceDefinition or BlockKind.FrontMatter or BlockKind.MathBlock;
}

/// <summary>
///     The document-level pass that decides how the file ends: with exactly one line ending, or with none.
/// </summary>
/// <remarks>
///     <para>
///         Everything after the last line with content is the trailer. With <c>final-newline</c> on, the trailer
///         becomes a single line feed (this adds a missing one and removes extra blank lines and whitespace-only
///         lines); with it off, the trailer is removed. A document without any content is left alone.
///     </para>
///     <para>
///         When the last content belongs to a block whose trailing blank lines are content (an unclosed fence, an
///         HTML block that only the end of the file closes) the trailer is left as it is: changing it would change
///         what the block contains. Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class TrailerPass
{
    /// <summary>
    ///     Adds the edit that gives the file its final line ending, when it needs one.
    /// </summary>
    /// <param name="document">The parsed document (for suppressions). Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block. Must not be null.</param>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <param name="edits">Receives the edit. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Apply(ParsedDocument document, LineLayout layout, LineOwnership ownership, LintMarkConfig config, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(edits);

        // Find the last line with content (a whitespace-only line that a block owns is content: it is code); a document without content has no trailer to fix
        var lastContent = layout.LineCount - 1;
        while (lastContent >= 0 && layout.IsBlank(lastContent) && ownership.LeafAt(lastContent) is null)
        {
            lastContent--;
        }

        if (lastContent < 0)
        {
            return;
        }

        // Blank lines that are part of a block's content are never touched, nor are those after a footnote body that is not plain text
        var owner = ownership.LeafAt(lastContent);
        if (owner is not null && LineOwnership.KeepsTrailingBlankLines(layout, owner, contentFollows: false))
        {
            return;
        }

        if (ownership.IsInFootnote(lastContent) && owner?.Kind != BlockKind.Paragraph)
        {
            return;
        }

        // The trailer starts where the last content line ends
        var text = layout.Text;
        var trailerStart = layout.LineEnd(lastContent);

        // A blank line after a line shaped like a grid table border is read by the rules of the grid table extension (it can make a list loose), so it stays
        if (layout.LooksLikeGridBorder(lastContent) && owner?.Kind is not (BlockKind.IndentedCode or BlockKind.GridTable) && HasBlankLine(text.AsSpan(trailerStart)))
        {
            return;
        }

        var wanted = config.FinalNewline ? "\n" : string.Empty;
        if (text.AsSpan(trailerStart).SequenceEqual(wanted))
        {
            return;
        }

        // The query starts at the last content line so a line-scoped directive on that line suppresses the edit
        if (document.Suppressions.IsSuppressedRange(layout.LineStart(lastContent), text.Length + 1, RuleCatalog.FinalNewlineRuleId))
        {
            return;
        }

        edits.Add(new TextEdit(trailerStart, text.Length - trailerStart, wanted, RuleCatalog.FinalNewlineRuleId));
    }

    /// <summary>
    ///     Determines whether a trailer holds a blank line: something after its first line ending.
    /// </summary>
    /// <param name="trailer">The text after the last content line.</param>
    /// <returns><see langword="true"/> when anything follows the first line ending of the trailer.</returns>
    private static bool HasBlankLine(ReadOnlySpan<char> trailer)
    {
        var first = trailer.IndexOfAny('\r', '\n');
        if (first < 0)
        {
            return false;
        }

        var rest = first + 1 < trailer.Length && trailer[first] == '\r' && trailer[first + 1] == '\n' ? first + 2 : first + 1;
        return rest < trailer.Length;
    }
}
