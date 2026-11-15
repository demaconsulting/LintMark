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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     The lint-only rules about tables: a table too wide for the line length, rows with the wrong number of cells, and
///     grid tables that are left as written.
/// </summary>
/// <remarks>
///     <para>
///         <c>table-too-wide</c> is reported for the first line of a pipe table that is wider than the line length
///         when the table is written in the compact layout, whichever layout is configured: a pipe table row is one line
///         by specification, so no rewrite can make it narrower. The width is that of the line with its container prefix, in
///         terminal columns, and does not depend on the layout the table has now. Nothing is reported when the line length
///         is 0 (no limit).
///     </para>
///     <para>
///         <c>table-column-count</c> is reported for every row whose number of cells differs from the number of cells of the
///         header; the parsers pad or ignore cells silently, and the rewrite never repairs the row (it would have to invent
///         or drop content). <c>table-grid-unformatted</c> (off unless switched on) names every Pandoc grid table, which
///         the formatter never touches.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class TableRules : ILintRule
{
    /// <summary>
    ///     Gets the identifiers of the rules this class reports.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.TableTooWideRuleId,
        RuleCatalog.TableColumnCountRuleId,
        RuleCatalog.TableGridUnformattedRuleId
    ];

    /// <summary>
    ///     Finds the problems of the tables of a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>The problems, in document order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        var source = document.Source;
        var layout = new LineLayout(source);
        var found = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var block in document.Blocks)
        {
            if (block.Kind == BlockKind.GridTable)
            {
                var (line, column) = source.PositionOf(block.Start);
                found.Add(Diagnostic.LintOnly(source.Path, RuleCatalog.TableGridUnformattedRuleId, line, column, "Grid table is left as written"));
            }
            else if (block.Kind == BlockKind.Table && PipeTable.TryParse(layout, block) is { } table)
            {
                CheckColumnCount(source, table, found);
                CheckWidth(source, table, config.LineLength, found);
            }
        }

        return found.ToImmutable();
    }

    /// <summary>
    ///     Reports the rows that do not have as many cells as the header.
    /// </summary>
    /// <param name="source">The document, for the path and the positions.</param>
    /// <param name="table">The table.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckColumnCount(SourceDocument source, PipeTable table, ImmutableArray<Diagnostic>.Builder found)
    {
        // The delimiter row (index 1) has as many cells as the header by construction
        for (var index = 2; index < table.Rows.Length; index++)
        {
            var row = table.Rows[index];
            if (row.Cells.Length != table.ColumnCount)
            {
                var (line, column) = source.PositionOf(row.ContentStart);
                found.Add(Diagnostic.LintOnly(source.Path, RuleCatalog.TableColumnCountRuleId, line, column, $"Table row has {row.Cells.Length} cells; the header has {table.ColumnCount}"));
            }
        }
    }

    /// <summary>
    ///     Reports the first line of a table that is wider than the line length in the compact layout.
    /// </summary>
    /// <param name="source">The document, for the path and the positions.</param>
    /// <param name="table">The table.</param>
    /// <param name="limit">The line length; 0 means no limit.</param>
    /// <param name="found">Receives the finding.</param>
    private static void CheckWidth(SourceDocument source, PipeTable table, int limit, ImmutableArray<Diagnostic>.Builder found)
    {
        if (limit == 0)
        {
            return;
        }

        var compact = table.ComposeCompact();
        for (var index = 0; index < compact.Length; index++)
        {
            var row = table.Rows[index];
            var width = TableFormatter.PrefixWidth(row.Prefix) + TextWidth.Of(compact[index]);
            if (width > limit)
            {
                var (line, column) = source.PositionOf(row.ContentStart);
                found.Add(Diagnostic.LintOnly(source.Path, RuleCatalog.TableTooWideRuleId, line, column, $"Table is {width} columns wide even when compact; the limit is {limit}"));
                return;
            }
        }
    }
}
