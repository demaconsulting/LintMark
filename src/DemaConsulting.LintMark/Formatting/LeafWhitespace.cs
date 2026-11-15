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
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Proposes the edits that remove trailing whitespace and expand tabs on the lines of one leaf block.
/// </summary>
/// <remarks>
///     <para>
///         Shared by <see cref="DocumentFormatter"/> (paragraphs and headings that no block formatter claims) and
///         by the heading formatter, so that both treat whitespace the same way. Whitespace is only changed
///         where the parser says it is plain text: whitespace inside code, inline HTML, math or a link
///         destination is part of what it says and stays.
///     </para>
///     <para>
///         Trailing whitespace is removed from every line, except that a hard break written with spaces keeps
///         exactly two (fewer would no longer be a break, more is the same break; a tab before the spaces is text
///         and stays). A run is also left alone when the character before it is a backslash, because removing the
///         spaces would turn a literal backslash into a backslash break. Tabs are expanded to the next tab stop of
///         four columns, counted from the start of the line, and only after the first plain text of the line, so
///         indentation and container prefixes never change. A line that looks like the border of a grid table is
///         never edited: Pandoc and the parser may read it as part of a table, and whether they do can depend on
///         the lines around it, which the same run may change as well.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class LeafWhitespace
{
    /// <summary>
    ///     The only trailing whitespace that survives: the two spaces of a hard break.
    /// </summary>
    private const string HardBreakSpaces = "  ";

    /// <summary>
    ///     Adds the trailing whitespace and tab edits for a range of lines of a leaf block.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="map">The inline map of the leaf block. Must not be null.</param>
    /// <param name="firstLine">The first zero-based line to handle.</param>
    /// <param name="lastLine">The last zero-based line to handle; its trailing run is the end of the block's text.</param>
    /// <param name="firstLineFloor">Offset where the content of the block starts on the first line; trailing whitespace is never taken from before it.</param>
    /// <param name="trailing">Whether to propose trailing whitespace edits.</param>
    /// <param name="tabs">Whether to propose tab edits.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/>, <paramref name="map"/> or <paramref name="edits"/> is null.</exception>
    public static void AddLineEdits(
        LineLayout layout,
        InlineMap map,
        int firstLine,
        int lastLine,
        int firstLineFloor,
        bool trailing,
        bool tabs,
        EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(edits);

        var text = layout.Text;
        for (var line = firstLine; line <= lastLine; line++)
        {
            // Find the trailing run of the line, never reaching into the container prefix before the block content
            var lineStart = layout.LineStart(line);
            var lineEnd = layout.LineEnd(line);
            var floor = line == firstLine ? Math.Max(lineStart, firstLineFloor) : lineStart;
            var runStart = lineEnd;
            while (runStart > floor && LineLayout.IsBlankChar(text[runStart - 1]))
            {
                runStart--;
            }

            // A line that looks like a grid table border is never edited: Pandoc and the parser may read it as part of
            // a table, and whether they do can depend on the lines around it, which this run may change as well
            if (layout.LooksLikeGridBorder(line))
            {
                continue;
            }

            // Whitespace may only change where the parser has a plain line end: before a line break or at the end of the block
            if (trailing && runStart < lineEnd && (line == lastLine || map.IsBreakAt(lineEnd)) && !map.IsProtected(runStart))
            {
                if (map.IsSpaceBreakAt(lineEnd))
                {
                    // A hard break is the trailing spaces only; a tab before them is text and stays
                    var spacesStart = lineEnd;
                    while (spacesStart > floor && text[spacesStart - 1] == ' ')
                    {
                        spacesStart--;
                    }

                    if (lineEnd - spacesStart != HardBreakSpaces.Length)
                    {
                        edits.Add(new TextEdit(spacesStart, lineEnd - spacesStart, HardBreakSpaces, RuleCatalog.TrailingWhitespaceRuleId));
                    }
                }
                else if (runStart == floor || text[runStart - 1] != '\\')
                {
                    edits.Add(new TextEdit(runStart, lineEnd - runStart, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
                }
            }

            // Tabs are only expanded before the trailing run, which is removed or kept as it is
            if (tabs)
            {
                foreach (var (offset, spaces) in CollectTabs(layout, map, lineStart, runStart, 0))
                {
                    edits.Add(new TextEdit(offset, 1, new string(' ', spaces), RuleCatalog.HardTabsRuleId));
                }
            }
        }
    }

    /// <summary>
    ///     Adds the trailing whitespace and tab edits for every line of a paragraph or heading.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="leaf">The paragraph or heading block. Must not be null.</param>
    /// <param name="trailing">Whether to propose trailing whitespace edits.</param>
    /// <param name="tabs">Whether to propose tab edits.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <remarks>
    ///     A block without inline content (the parser gave none) is left alone. The underline of a setext
    ///     heading is not text: only its trailing whitespace is removed, and the last line of text before it
    ///     counts as the last line of the block.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void AddBlockEdits(LineLayout layout, BlockInfo leaf, bool trailing, bool tabs, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(leaf);
        ArgumentNullException.ThrowIfNull(edits);

        if (leaf.Node is not LeafBlock { Inline: { } inline })
        {
            return;
        }

        var map = InlineMap.Build(inline);
        var (first, last) = LineOwnership.LinesOf(layout, leaf);
        if (leaf.Node is HeadingBlock { IsSetext: true } && last > first)
        {
            AddLineEdits(layout, map, first, last - 1, leaf.Start, trailing, tabs, edits);
            if (trailing)
            {
                AddTrailingEdit(layout, last, edits);
            }

            return;
        }

        AddLineEdits(layout, map, first, last, leaf.Start, trailing, tabs, edits);
    }

    /// <summary>
    ///     Adds the edit that removes the trailing whitespace of a line that has no inline structure to consult.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="line">The zero-based line.</param>
    /// <param name="edits">Receives the edit. Must not be null.</param>
    /// <remarks>
    ///     Used for table rows, single-line link reference definitions, thematic breaks and lines that hold only
    ///     a container marker. A line that holds nothing but whitespace is not handled here (the blank line
    ///     rules own it), and a run preceded by a backslash is kept.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="edits"/> is null.</exception>
    public static void AddTrailingEdit(LineLayout layout, int line, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(edits);

        // A line that looks like a grid table border is never edited (see AddLineEdits)
        if (layout.LooksLikeGridBorder(line))
        {
            return;
        }

        var text = layout.Text;
        var lineStart = layout.LineStart(line);
        var lineEnd = layout.LineEnd(line);
        var runStart = lineEnd;
        while (runStart > lineStart && LineLayout.IsBlankChar(text[runStart - 1]))
        {
            runStart--;
        }

        if (runStart < lineEnd && runStart > lineStart && text[runStart - 1] != '\\')
        {
            edits.Add(new TextEdit(runStart, lineEnd - runStart, string.Empty, RuleCatalog.TrailingWhitespaceRuleId));
        }
    }

    /// <summary>
    ///     Finds the tab characters of plain text in a stretch of one line and how many spaces replace each.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="map">The inline map of the leaf block. Must not be null.</param>
    /// <param name="from">Offset where scanning starts (the start of the line, or the start of text that moves to another position).</param>
    /// <param name="limit">Exclusive offset where scanning stops.</param>
    /// <param name="startColumn">The zero-based column that <paramref name="from"/> falls in.</param>
    /// <param name="startsInText">
    ///     Whether <paramref name="from"/> is already past the indentation and any container prefix, so that every tab
    ///     counts as inside the text (the heading formatter starts at the first character of the heading text).
    /// </param>
    /// <returns>The offset of each tab to replace with the number of spaces that reach the next tab stop; empty when there is none.</returns>
    /// <remarks>
    ///     Every tab advances the column to the next stop whether it is replaced or not, so a tab that is kept
    ///     (inside a code span, say) still positions the text after it. Unless <paramref name="startsInText"/> is set,
    ///     a tab is only replaced after the first plain text of the stretch, which keeps indentation and container
    ///     markers as they are.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="map"/> is null.</exception>
    public static ImmutableArray<(int Offset, int Spaces)> CollectTabs(LineLayout layout, InlineMap map, int from, int limit, int startColumn, bool startsInText = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(map);

        var text = layout.Text;
        var found = ImmutableArray.CreateBuilder<(int Offset, int Spaces)>();
        var column = startColumn;
        var seenText = startsInText;
        for (var i = from; i < limit; i++)
        {
            var c = text[i];
            if (c == '\t')
            {
                var stop = LineLayout.NextTabStop(column);
                if (seenText && map.IsText(i) && !map.IsProtected(i))
                {
                    found.Add((i, stop - column));
                }

                column = stop;
            }
            else
            {
                column++;
                if (!LineLayout.IsBlankChar(c) && map.IsText(i))
                {
                    seenText = true;
                }
            }
        }

        return found.ToImmutable();
    }
}

/// <summary>
///     What the inline syntax tree of one leaf block says about its characters: which spans are plain text,
///     which spans must never be edited, and where the line breaks are.
/// </summary>
/// <remarks>
///     <para>
///         Whitespace rules may only touch whitespace that the Markdown parser treats as plain text. Code spans,
///         inline HTML, math, autolink URLs, link destinations and titles keep their whitespace as it is, because it
///         is part of what the document says. The map answers three questions about an offset of the original text:
///         is it plain text, is it protected, and does a line break start there (and is that break a hard break
///         written with spaces).
///     </para>
///     <para>
///         Spans are offsets into the LF-normalized text with inclusive ends, as the parser reports them (the
///         pipeline keeps precise source locations). A line break inline starts at the offset of its line feed;
///         a break that is hard because of trailing spaces is distinguished from a backslash break, which takes no
///         trailing whitespace.
///     </para>
///     <para>
///         Thread safety: immutable after <see cref="Build"/>; safe for concurrent reads.
///     </para>
/// </remarks>
internal sealed class InlineMap
{
    /// <summary>
    ///     Sorted, non-overlapping spans of plain text (inclusive ends).
    /// </summary>
    private readonly (int Start, int End)[] _text;

    /// <summary>
    ///     Sorted, non-overlapping spans that must never be edited (inclusive ends).
    /// </summary>
    private readonly (int Start, int End)[] _protected;

    /// <summary>
    ///     The line breaks by the offset of their line feed; the value says whether the break is a hard break written with spaces.
    /// </summary>
    private readonly Dictionary<int, bool> _breaks;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InlineMap"/> class.
    /// </summary>
    /// <param name="text">Plain text spans, sorted and merged.</param>
    /// <param name="protectedSpans">Protected spans, sorted and merged.</param>
    /// <param name="breaks">The line breaks.</param>
    private InlineMap((int Start, int End)[] text, (int Start, int End)[] protectedSpans, Dictionary<int, bool> breaks)
    {
        _text = text;
        _protected = protectedSpans;
        _breaks = breaks;
    }

    /// <summary>
    ///     Gets an empty map: no text, nothing protected, no breaks.
    /// </summary>
    public static InlineMap Empty { get; } = new([], [], []);

    /// <summary>
    ///     Builds the map of an inline tree.
    /// </summary>
    /// <param name="root">The inline content of a leaf block, or <see langword="null"/> when the block has none.</param>
    /// <param name="shift">A constant added to every offset the parser reports, for the rare block whose spans are known to be off by a fixed amount (zero for none).</param>
    /// <returns>The map; <see cref="Empty"/> when <paramref name="root"/> is null.</returns>
    /// <remarks>
    ///     Literal text and line breaks are the editable parts. Emphasis and similar containers are walked
    ///     into. A link is walked into, but its destination, title and label are protected; the parser reports the
    ///     bracketed text of an inline link as its label, so the whitespace inside link text stays as written.
    ///     Every other inline (code, HTML, math, autolink URLs, footnote references, task markers) is protected as a whole.
    /// </remarks>
    public static InlineMap Build(ContainerInline? root, int shift = 0)
    {
        if (root is null)
        {
            return Empty;
        }

        var text = new List<(int, int)>();
        var guarded = new List<(int, int)>();
        var breaks = new Dictionary<int, bool>();
        Visit(root, shift, text, guarded, breaks);
        return new InlineMap(Merge(text), Merge(guarded), breaks);
    }

    /// <summary>
    ///     Determines whether an offset lies in plain text.
    /// </summary>
    /// <param name="offset">The offset into the text.</param>
    /// <returns><see langword="true"/> when a literal inline covers the offset.</returns>
    public bool IsText(int offset) => Contains(_text, offset);

    /// <summary>
    ///     Determines whether an offset lies in a span that must never be edited.
    /// </summary>
    /// <param name="offset">The offset into the text.</param>
    /// <returns><see langword="true"/> when the offset is inside code, inline HTML, math, a link destination, title or label, or another protected inline.</returns>
    public bool IsProtected(int offset) => Contains(_protected, offset);

    /// <summary>
    ///     Determines whether a line break inline starts at an offset.
    /// </summary>
    /// <param name="lineFeed">The offset of a line feed.</param>
    /// <returns><see langword="true"/> for a soft break, a hard break written with spaces or a backslash break.</returns>
    public bool IsBreakAt(int lineFeed) => _breaks.ContainsKey(lineFeed);

    /// <summary>
    ///     Determines whether a hard break written with trailing spaces starts at an offset.
    /// </summary>
    /// <param name="lineFeed">The offset of a line feed.</param>
    /// <returns><see langword="true"/> for a hard break that two or more trailing spaces make; <see langword="false"/> for any other break or no break.</returns>
    public bool IsSpaceBreakAt(int lineFeed) => _breaks.TryGetValue(lineFeed, out var spaces) && spaces;

    /// <summary>
    ///     Walks the children of an inline container and records what each child contributes.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="shift">The constant added to every offset.</param>
    /// <param name="text">Receives the plain text spans.</param>
    /// <param name="guarded">Receives the protected spans.</param>
    /// <param name="breaks">Receives the line breaks.</param>
    private static void Visit(ContainerInline container, int shift, List<(int, int)> text, List<(int, int)> guarded, Dictionary<int, bool> breaks)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LiteralInline literal:
                    AddSpan(text, literal.Span.Start, literal.Span.End, shift);
                    break;

                case LineBreakInline lineBreak:
                    breaks[lineBreak.Span.Start + shift] = lineBreak.IsHard && !lineBreak.IsBackslash;
                    break;

                // A link keeps its text editable; the destination, title and label are part of what the link says
                case LinkInline link:
                    AddSpan(guarded, link.UrlSpan.Start, link.UrlSpan.End, shift);
                    AddSpan(guarded, link.TitleSpan.Start, link.TitleSpan.End, shift);
                    AddSpan(guarded, link.LabelSpan.Start, link.LabelSpan.End, shift);
                    Visit(link, shift, text, guarded, breaks);
                    break;

                // Emphasis and other containers only wrap text
                case ContainerInline nested:
                    Visit(nested, shift, text, guarded, breaks);
                    break;

                default:
                    AddSpan(guarded, child.Span.Start, child.Span.End, shift);
                    break;
            }
        }
    }

    /// <summary>
    ///     Adds a span to a list when it is valid.
    /// </summary>
    /// <param name="spans">The list.</param>
    /// <param name="start">First offset of the span.</param>
    /// <param name="end">Last offset of the span (inclusive).</param>
    /// <param name="shift">The constant added to both offsets of a valid span.</param>
    private static void AddSpan(List<(int, int)> spans, int start, int end, int shift)
    {
        if (start >= 0 && end >= start)
        {
            spans.Add((start + shift, end + shift));
        }
    }

    /// <summary>
    ///     Sorts spans and merges those that overlap or touch.
    /// </summary>
    /// <param name="spans">The spans.</param>
    /// <returns>Sorted, non-overlapping spans.</returns>
    private static (int Start, int End)[] Merge(List<(int Start, int End)> spans)
    {
        spans.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        var merged = new List<(int Start, int End)>(spans.Count);
        foreach (var span in spans)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End + 1)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            }
            else
            {
                merged.Add(span);
            }
        }

        return [.. merged];
    }

    /// <summary>
    ///     Determines whether sorted, non-overlapping spans cover an offset.
    /// </summary>
    /// <param name="spans">The spans.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="true"/> when a span covers the offset.</returns>
    private static bool Contains((int Start, int End)[] spans, int offset)
    {
        var low = 0;
        var high = spans.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (offset < spans[middle].Start)
            {
                high = middle - 1;
            }
            else if (offset > spans[middle].End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
