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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Utilities;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes GFM pipe tables in the canonical layout: a pipe at both ends of every line, one blank inside each pipe
///     and a minimal delimiter row (compact, the default), or every column padded to its widest cell (aligned).
/// </summary>
/// <remarks>
///     <para>
///         The compact layout depends only on the cells of a table, so editing one row never changes another row: the
///         reason it is the default (smaller diffs, fewer merge conflicts). The aligned layout is opt-in
///         (<see cref="TableStyle.Aligned"/>); it is used for a table only when every line of it fits the line length,
///         and the table is written compact otherwise. The text of every cell is taken from the source and written back
///         unchanged (escapes such as <c>\|</c>, code spans and inline HTML survive byte for byte); only the blanks around
///         the cells, the delimiter row and the pipes at the ends of the lines change. The alignment markers of the
///         delimiter row (<c>:---</c>, <c>:---:</c>, <c>---:</c>) are kept. The prefix of every line (block quote markers,
///         list markers, indentation) is never touched, and so a table inside a list item or a block quote keeps it.
///     </para>
///     <para>
///         Rows that have more or fewer cells than the header are written with the cells they have: a parser pads or
///         ignores cells silently, and inventing or dropping content is not the formatter's business (the lint-only
///         <c>table-column-count</c> rule reports them). Pandoc grid tables are another block kind and are not
///         accepted. A table is left as written when it cannot be read with certainty (see <see cref="PipeTable"/>) or lies
///         in a footnote. The table extension of the parser splits a line at every unescaped pipe, even inside a code span, a link
///         or an HTML attribute, exactly as <see cref="PipeTable"/> does, and the rewrite changes only blanks and the pipes at the
///         ends of the lines, so no further check of the result is needed.
///     </para>
///     <para>
///         Thread safety: the instance holds only immutable data; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class TableFormatter : IBlockFormatter
{
    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.TablePipeStyleRuleId,
        RuleCatalog.TableColumnStyleRuleId,
        RuleCatalog.TrailingWhitespaceRuleId
    ];

    /// <summary>
    ///     Determines whether a block is a pipe table.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for a pipe table; <see langword="false"/> for a grid table and every other block.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Kind == BlockKind.Table && block.Node is Table;
    }

    /// <summary>
    ///     Proposes the edits that bring a table into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The table. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives one edit for every line that changes. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a pipe table.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be a pipe table, not {block.Kind}.", nameof(block));
        }

        // A table that cannot be read with certainty, or that lies where the container rules are unreliable, is left as written
        var layout = context.Layout;
        var firstLine = layout.LineOf(block.Start);
        if (context.Ownership.IsInFootnote(firstLine) || context.Ownership.IsProtected(firstLine) || PipeTable.TryParse(layout, block) is not { } table)
        {
            return;
        }

        // Choose the layout: aligned when it is asked for and every line fits, compact otherwise
        var lines = table.ComposeCompact();
        if (context.Config.Tables == TableStyle.Aligned)
        {
            var aligned = table.ComposeAligned();
            if (Fits(table, aligned, context.Config.LineLength))
            {
                lines = aligned;
            }
        }

        // Propose one edit for every line whose content differs
        var proposed = new List<TextEdit>();
        for (var index = 0; index < table.Rows.Length; index++)
        {
            var row = table.Rows[index];
            var original = layout.Text[row.ContentStart..row.LineEnd];
            if (!string.Equals(original, lines[index], StringComparison.Ordinal))
            {
                proposed.Add(new TextEdit(row.ContentStart, row.LineEnd - row.ContentStart, lines[index], ChooseRule(layout.Text, row, lines[index])));
            }
        }

        foreach (var edit in proposed)
        {
            edits.Add(edit);
        }
    }

    /// <summary>
    ///     Determines whether every line of a layout, with its prefix, fits the line length.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="lines">The content of every line of the layout.</param>
    /// <param name="limit">The line length; 0 means no limit.</param>
    /// <returns><see langword="true"/> when no limit is set or no line is wider than the limit.</returns>
    private static bool Fits(PipeTable table, ImmutableArray<string> lines, int limit)
    {
        if (limit == 0)
        {
            return true;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            if (PrefixWidth(table.Rows[index].Prefix) + TextWidth.Of(lines[index]) > limit)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Measures the prefix of a line in columns, with tabs advancing to the next tab stop.
    /// </summary>
    /// <param name="prefix">The prefix: quote markers, list markers and blanks.</param>
    /// <returns>The width in columns.</returns>
    internal static int PrefixWidth(string prefix)
    {
        var column = 0;
        foreach (var c in prefix)
        {
            column = c == '\t' ? LineLayout.NextTabStop(column) : column + 1;
        }

        return column;
    }

    /// <summary>
    ///     Chooses the rule an edit of a line is reported under.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="row">The row.</param>
    /// <param name="replacement">The new content of the line.</param>
    /// <returns>
    ///     The trailing whitespace rule when only blanks at the end of the line go; the pipe style rule when the line lacks
    ///     its leading or trailing pipe; the column style rule otherwise.
    /// </returns>
    private static string ChooseRule(string text, PipeTableRow row, string replacement)
    {
        if (string.Equals(text[row.ContentStart..row.ContentEnd], replacement, StringComparison.Ordinal))
        {
            return RuleCatalog.TrailingWhitespaceRuleId;
        }

        return row.HasLeadingPipe && row.HasTrailingPipe ? RuleCatalog.TableColumnStyleRuleId : RuleCatalog.TablePipeStyleRuleId;
    }
}
