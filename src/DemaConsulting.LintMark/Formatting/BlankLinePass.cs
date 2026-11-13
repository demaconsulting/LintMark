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
///         whitespace is still removed. The run at the end of the file belongs to the final newline rule. A run
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
            if (first > 0 && ownership.LeafAt(first - 1) is { } previous && LineOwnership.KeepsTrailingBlankLines(previous, contentFollows: true))
            {
                continue;
            }

            HandleRun(document, layout, ownership, config.MaxBlankLines, first, last, edits);
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
    private static bool IsBorderNeighbor(LineLayout layout, LineOwnership ownership, int line) =>
        line >= 0
        && line < layout.LineCount
        && layout.LooksLikeGridBorder(line)
        && ownership.LeafAt(line)?.Kind != BlockKind.IndentedCode;

    /// <summary>
    ///     Handles one run of blank lines: removes the surplus lines and empties the lines that stay.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic.</param>
    /// <param name="ownership">Which lines belong to which block.</param>
    /// <param name="maxBlankLines">The number of blank lines that may stay.</param>
    /// <param name="first">First line of the run.</param>
    /// <param name="last">Last line of the run.</param>
    /// <param name="edits">Receives the edits.</param>
    private static void HandleRun(
        ParsedDocument document,
        LineLayout layout,
        LineOwnership ownership,
        int maxBlankLines,
        int first,
        int last,
        EditSet edits)
    {
        // Surplus lines are removed completely, line feed included; not inside a container
        var kept = last - first + 1;
        var start = layout.LineStart(first);
        var end = layout.LineEnd(last);
        if (kept > maxBlankLines && !ownership.IsInContainer(first)
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
