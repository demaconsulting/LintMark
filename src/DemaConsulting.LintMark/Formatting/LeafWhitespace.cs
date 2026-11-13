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

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Proposes the edits that remove trailing whitespace and expand tabs on the lines of one leaf block.
/// </summary>
/// <remarks>
///     <para>
///         Shared by <see cref="DocumentFormatter"/> (paragraphs and headings that no block formatter claims) and
///         by the heading formatter, so that both treat whitespace the same way. Whitespace is only changed
///         where the parser says it is plain text: whitespace inside code, inline HTML, math or a link
///         destination is part of the rendered result and stays.
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
    /// <returns>The offset of each tab to replace with the number of spaces that reach the next tab stop; empty when there is none.</returns>
    /// <remarks>
    ///     Every tab advances the column to the next stop whether it is replaced or not, so a tab that is kept
    ///     (inside a code span, say) still positions the text after it. A tab is only replaced after the first
    ///     plain text of the stretch, which keeps indentation and container markers as they are.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="map"/> is null.</exception>
    public static ImmutableArray<(int Offset, int Spaces)> CollectTabs(LineLayout layout, InlineMap map, int from, int limit, int startColumn)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(map);

        var text = layout.Text;
        var found = ImmutableArray.CreateBuilder<(int Offset, int Spaces)>();
        var column = startColumn;
        var seenText = false;
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
