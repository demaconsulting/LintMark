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
using System.Text;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes headings in the canonical form: ATX (<c># Title</c>) with exactly one space after the number signs,
///     no closing number signs, no indentation, no trailing whitespace and no tabs in the text.
/// </summary>
/// <remarks>
///     <para>
///         The formatter rewrites what the author wrote in other forms and leaves everything else alone. A
///         single-line setext heading becomes an ATX heading of the same level, closing number signs
///         (<c># Title #</c>) are removed, the spaces or tabs after the opening number signs become one space,
///         the one to three spaces before a top-level heading are removed, trailing whitespace is removed and tabs
///         in the heading text become spaces. A multi-line setext heading and a setext heading inside a quote or
///         list stay setext headings (the underline is only cleaned up), and a heading inside a container keeps
///         its indentation: both changes could alter what the document means.
///     </para>
///     <para>
///         Every edit is computed from the source line of the heading, never from the parser's span, because the
///         span of an ATX heading ends inconsistently (after trailing spaces, but before closing number signs). A
///         line that does not have the shape the parser promised produces no edit at all. The parser's inline spans
///         are used only to tell plain text from code, and only after they have been matched to the line (see
///         the design): a heading with a tab in its text whose spans do not fit is left alone as a whole. Text that merely looks
///         like a heading but is a paragraph (<c>#Title</c> without a space) is not a heading to the parser and is
///         never touched: adding the space would change how the document renders.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class HeadingFormatter : IBlockFormatter
{
    /// <summary>
    ///     The single space between the number signs and the text of a canonical heading.
    /// </summary>
    private const string CanonicalSpace = " ";

    /// <summary>
    ///     The most spaces that may indent a heading that is still a heading (four make it code).
    /// </summary>
    private const int MaxIndentation = 3;

    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.HeadingStyleRuleId,
        RuleCatalog.HeadingSpaceRuleId,
        RuleCatalog.HeadingStartLeftRuleId,
        RuleCatalog.TrailingWhitespaceRuleId,
        RuleCatalog.HardTabsRuleId
    ];

    /// <summary>
    ///     Determines whether a block is a heading this formatter handles.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for an ATX or setext heading.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Kind == BlockKind.Heading && block.Node is HeadingBlock;
    }

    /// <summary>
    ///     Proposes the edits that bring a heading into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The heading. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a heading.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be a heading, not {block.Kind}.", nameof(block));
        }

        var layout = new LineLayout(context.Document.Source);
        var heading = (HeadingBlock)block.Node!;
        if (heading.IsSetext)
        {
            FormatSetext(layout, block, heading, edits);
        }
        else
        {
            FormatAtx(layout, block, heading, edits);
        }
    }

    /// <summary>
    ///     Formats an ATX heading from its source line.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The heading block.</param>
    /// <param name="heading">The parser's heading.</param>
    /// <param name="edits">Receives the edits.</param>
    /// <remarks>
    ///     The line is split into the indentation, the opening number signs, the spaces after them, the content, an
    ///     optional closing sequence and the trailing whitespace; each part that is not canonical gets one edit
    ///     under its own rule. The parts are disjoint, so the edits never conflict.
    /// </remarks>
    private static void FormatAtx(LineLayout layout, BlockInfo block, HeadingBlock heading, EditSet edits)
    {
        var text = layout.Text;
        var line = layout.LineOf(block.Start);
        var lineStart = layout.LineStart(line);
        var lineEnd = layout.LineEnd(line);

        // The heading must start with exactly its number of number signs, followed by a blank or the end of the line
        var marker = block.Start;
        var afterMarker = marker + heading.Level;
        if (afterMarker > lineEnd || text.AsSpan(marker, heading.Level).ContainsAnyExcept('#')
            || (afterMarker < lineEnd && !LineLayout.IsBlankChar(text[afterMarker])))
        {
            return;
        }

        // The indentation of a top-level heading must be spaces only, or the shape is not what the parser promised
        var indented = block.Parent is null && marker > lineStart;
        if (indented && text.AsSpan(lineStart, marker - lineStart).ContainsAnyExcept(' '))
        {
            return;
        }

        // Find the content: skip the blanks after the marker, and drop the trailing whitespace
        var contentStart = afterMarker;
        while (contentStart < lineEnd && LineLayout.IsBlankChar(text[contentStart]))
        {
            contentStart++;
        }

        var trimmedEnd = lineEnd;
        while (trimmedEnd > contentStart && LineLayout.IsBlankChar(text[trimmedEnd - 1]))
        {
            trimmedEnd--;
        }

        // A closing sequence is a run of number signs after a blank (or the whole content); it is removed with the blanks before it
        var closingStart = FindClosingSequence(text, contentStart, trimmedEnd);
        var contentEnd = closingStart >= 0 ? closingStart : trimmedEnd;
        while (contentEnd > contentStart && LineLayout.IsBlankChar(text[contentEnd - 1]))
        {
            contentEnd--;
        }

        // Tabs in the heading text are found first: a heading whose inline spans cannot be matched to its line is left
        // as it is, so that no rule of the formatter does half of its work in one run and the rest in the next
        var markerColumn = indented ? 0 : ColumnOf(text, lineStart, marker);
        if (!TryCollectTabs(layout, heading, contentStart, contentEnd, markerColumn + heading.Level + CanonicalSpace.Length, out var tabs))
        {
            return;
        }

        // Indentation of a top-level heading is removed; a heading in a container keeps its prefix
        if (indented)
        {
            edits.Add(new TextEdit(lineStart, marker - lineStart, string.Empty, RuleCatalog.HeadingStartLeftRuleId));
        }

        // Without a closing sequence: empty headings lose every blank after the marker, others get exactly one blank
        if (closingStart < 0 && contentStart == trimmedEnd)
        {
            AddTrailing(edits, afterMarker, lineEnd);
            return;
        }

        if (closingStart >= 0)
        {
            // An empty heading keeps its marker only, so the blanks after the marker go as well
            var removeFrom = contentEnd == contentStart ? afterMarker : contentEnd;
            edits.Add(new TextEdit(removeFrom, trimmedEnd - removeFrom, string.Empty, RuleCatalog.HeadingStyleRuleId));
        }

        AddTrailing(edits, trimmedEnd, lineEnd);
        AddSpaceEdit(edits, text, afterMarker, contentStart, contentEnd > contentStart);
        foreach (var (offset, spaces) in tabs)
        {
            edits.Add(new TextEdit(offset, 1, new string(' ', spaces), RuleCatalog.HardTabsRuleId));
        }
    }

    /// <summary>
    ///     Gets the column of an offset in a line, counting a tab as far as the next tab stop.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="lineStart">Offset of the start of the line.</param>
    /// <param name="offset">The offset in the line.</param>
    /// <returns>The zero-based column.</returns>
    private static int ColumnOf(string text, int lineStart, int offset)
    {
        var column = 0;
        for (var position = lineStart; position < offset; position++)
        {
            column = text[position] == '\t' ? LineLayout.NextTabStop(column) : column + 1;
        }

        return column;
    }

    /// <summary>
    ///     Finds the tabs of the text of a heading that become spaces.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="heading">The parser's heading.</param>
    /// <param name="contentStart">Offset of the first character of the heading text.</param>
    /// <param name="contentEnd">Offset after the last character of the heading text (before any closing sequence).</param>
    /// <param name="startColumn">The column that the heading text starts in once the heading is canonical.</param>
    /// <param name="tabs">Receives the offset of each tab and the spaces that replace it.</param>
    /// <returns>
    ///     <see langword="true"/> when the text has no tab, or has tabs and the parser's inline spans fit the line;
    ///     <see langword="false"/> when the spans cannot be trusted and the heading must be left alone.
    /// </returns>
    private static bool TryCollectTabs(
        LineLayout layout,
        HeadingBlock heading,
        int contentStart,
        int contentEnd,
        int startColumn,
        out ImmutableArray<(int Offset, int Spaces)> tabs)
    {
        tabs = [];
        if (!layout.Text.AsSpan(contentStart, contentEnd - contentStart).Contains('\t'))
        {
            return true;
        }

        if (!TryBuildMap(heading, contentStart, contentEnd, out var map))
        {
            return false;
        }

        tabs = LeafWhitespace.CollectTabs(layout, map, contentStart, contentEnd, startColumn, startsInText: true);
        return true;
    }

    /// <summary>
    ///     Builds the inline map of a heading whose text lies at known offsets of its line.
    /// </summary>
    /// <param name="heading">The parser's heading.</param>
    /// <param name="contentStart">Offset of the first character of the heading text.</param>
    /// <param name="contentEnd">Offset after the last character of the heading text.</param>
    /// <param name="map">Receives the map, with the spans moved to where the text really is.</param>
    /// <returns><see langword="true"/> when the first and the last inline fit the text once the spans are moved.</returns>
    /// <remarks>
    ///     When the opening number signs are followed by a tab the parser reports the spans of the inlines of an
    ///     ATX heading relative to the start of the heading instead of the start of the document (a heading that is
    ///     not on the first line gets spans that are too small by a constant). The shift is taken from the first
    ///     inline and confirmed by the end of the last: spans that still do not fit are not trusted.
    /// </remarks>
    private static bool TryBuildMap(HeadingBlock heading, int contentStart, int contentEnd, out InlineMap map)
    {
        map = InlineMap.Empty;
        if (heading.Inline is not { FirstChild: { } first, LastChild: { } last } inline)
        {
            return false;
        }

        var shift = contentStart - first.Span.Start;
        if (first.Span.Start < 0 || last.Span.End + shift != contentEnd - 1)
        {
            return false;
        }

        map = InlineMap.Build(inline, shift);
        return true;
    }

    /// <summary>
    ///     Finds the closing sequence of an ATX heading.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="contentStart">Offset of the first character after the opening blanks.</param>
    /// <param name="trimmedEnd">Offset after the last character that is not whitespace.</param>
    /// <returns>The offset of the first closing number sign, or -1 when the heading has no closing sequence.</returns>
    /// <remarks>
    ///     A closing sequence is a run of number signs that ends the content and is preceded by a blank, or fills
    ///     the whole content. A run that follows another character directly (<c>C#</c>) or an escape
    ///     (<c>\#</c>) is text.
    /// </remarks>
    private static int FindClosingSequence(string text, int contentStart, int trimmedEnd)
    {
        var start = trimmedEnd;
        while (start > contentStart && text[start - 1] == '#')
        {
            start--;
        }

        if (start == trimmedEnd)
        {
            return -1;
        }

        return start == contentStart || LineLayout.IsBlankChar(text[start - 1]) ? start : -1;
    }

    /// <summary>
    ///     Adds the edit that makes the blanks after the opening number signs a single space.
    /// </summary>
    /// <param name="edits">Receives the edit.</param>
    /// <param name="text">The document text.</param>
    /// <param name="afterMarker">Offset after the opening number signs.</param>
    /// <param name="contentStart">Offset of the first character of the content.</param>
    /// <param name="hasContent">Whether the heading has content (an empty heading needs no space).</param>
    private static void AddSpaceEdit(EditSet edits, string text, int afterMarker, int contentStart, bool hasContent)
    {
        if (!hasContent || contentStart == afterMarker)
        {
            return;
        }

        if (contentStart - afterMarker != 1 || text[afterMarker] != ' ')
        {
            edits.Add(new TextEdit(afterMarker, contentStart - afterMarker, CanonicalSpace, RuleCatalog.HeadingSpaceRuleId));
        }
    }

    /// <summary>
    ///     Adds the edit that removes trailing whitespace.
    /// </summary>
    /// <param name="edits">Receives the edit.</param>
    /// <param name="from">Offset where the whitespace starts.</param>
    /// <param name="lineEnd">Offset where the line ends.</param>
    private static void AddTrailing(EditSet edits, int from, int lineEnd)
    {
        if (lineEnd > from)
        {
            edits.Add(new TextEdit(from, lineEnd - from, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
        }
    }

    /// <summary>
    ///     Formats a setext heading.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The heading block.</param>
    /// <param name="heading">The parser's heading.</param>
    /// <param name="edits">Receives the edits.</param>
    /// <remarks>
    ///     A top-level heading with one line of text becomes an ATX heading, unless its text ends with a number
    ///     sign or a backslash (the ATX form would read it differently). Any other setext heading only gets its
    ///     whitespace cleaned up.
    /// </remarks>
    private static void FormatSetext(LineLayout layout, BlockInfo block, HeadingBlock heading, EditSet edits)
    {
        var text = layout.Text;
        var first = layout.LineOf(block.Start);
        var underline = layout.LineOf(block.EndInclusive);
        if (underline <= first || !IsUnderline(text, layout.LineStart(underline), layout.LineEnd(underline)))
        {
            return;
        }

        // Multi-line and nested headings keep the setext form; only their whitespace is normalized
        if (block.Parent is not null || underline != first + 1 || !TryConvertToAtx(layout, heading, first, underline, edits))
        {
            LeafWhitespace.AddBlockEdits(layout, block, trailing: true, tabs: true, edits);
        }
    }

    /// <summary>
    ///     Replaces a one-line setext heading by an ATX heading.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="heading">The parser's heading.</param>
    /// <param name="textLine">The line with the heading text.</param>
    /// <param name="underline">The line with the underline.</param>
    /// <param name="edits">Receives the edit.</param>
    /// <returns><see langword="true"/> when the heading was converted; <see langword="false"/> when the shape rules it out.</returns>
    private static bool TryConvertToAtx(LineLayout layout, HeadingBlock heading, int textLine, int underline, EditSet edits)
    {
        var text = layout.Text;
        var lineStart = layout.LineStart(textLine);
        var lineEnd = layout.LineEnd(textLine);

        // The text without its indentation and trailing whitespace
        var contentStart = lineStart;
        while (contentStart < lineEnd && LineLayout.IsBlankChar(text[contentStart]))
        {
            contentStart++;
        }

        var contentEnd = lineEnd;
        while (contentEnd > contentStart && LineLayout.IsBlankChar(text[contentEnd - 1]))
        {
            contentEnd--;
        }

        var indentation = contentStart - lineStart;
        if (contentEnd == contentStart || indentation > MaxIndentation || text.AsSpan(lineStart, indentation).Contains('\t')
            || StartsLikeStructure(text, contentStart) || text[contentEnd - 1] is '#' or '\\')
        {
            return false;
        }

        // Tabs in the text become spaces in the same edit, counted from the column the text has in the new line; spans that do not fit the line keep the heading setext
        var prefix = new string('#', heading.Level) + CanonicalSpace;
        var builder = new StringBuilder(prefix);
        var position = contentStart;
        if (!TryCollectTabs(layout, heading, contentStart, contentEnd, prefix.Length, out var tabs))
        {
            return false;
        }

        foreach (var (offset, spaces) in tabs)
        {
            builder.Append(text, position, offset - position).Append(' ', spaces);
            position = offset + 1;
        }

        builder.Append(text, position, contentEnd - position);
        edits.Add(new TextEdit(lineStart, layout.LineEnd(underline) - lineStart, builder.ToString(), RuleCatalog.HeadingStyleRuleId));
        return true;
    }

    /// <summary>
    ///     Determines whether text starts like a Pandoc structure line that must stay at the start of its line.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Offset of the first character of the heading text.</param>
    /// <returns>
    ///     <see langword="true"/> when the text starts with a plus sign (a grid table border), a colon (a fenced
    ///     div), a backslash (a LaTeX command) or a dollar sign (display math): written after the number signs it
    ///     would no longer be that line.
    /// </returns>
    private static bool StartsLikeStructure(string text, int start) =>
        text[start] is '+' or ':' or '\\' or '$';

    /// <summary>
    ///     Determines whether a line is a setext underline: a run of equals signs or hyphens that ends the line,
    ///     followed only by blanks and preceded only by blanks and quote markers (the container prefix).
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Offset of the start of the line.</param>
    /// <param name="end">Offset of the end of the line.</param>
    /// <returns><see langword="true"/> when the line has that shape.</returns>
    private static bool IsUnderline(string text, int start, int end)
    {
        // Drop the trailing blanks, then the run of underline characters
        var runEnd = end;
        while (runEnd > start && LineLayout.IsBlankChar(text[runEnd - 1]))
        {
            runEnd--;
        }

        if (runEnd == start || text[runEnd - 1] is not ('=' or '-'))
        {
            return false;
        }

        var marker = text[runEnd - 1];
        var runStart = runEnd;
        while (runStart > start && text[runStart - 1] == marker)
        {
            runStart--;
        }

        // Only blanks and quote markers may stand before the run
        return !text.AsSpan(start, runStart - start).ContainsAnyExcept(' ', '\t', '>');
    }
}
