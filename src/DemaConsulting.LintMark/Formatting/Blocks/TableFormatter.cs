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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
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
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);

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

/// <summary>
///     The source of a GFM pipe table read line by line, and the two canonical layouts it can be written in.
/// </summary>
/// <remarks>
///     <para>
///         The parser decides which lines are a table; this type reads the cells from the source text of those lines
///         (never from the inline content the parser builds), so that the text of a cell survives byte for byte. A line is split
///         at every pipe that is not escaped by a backslash (inside a code span too, as GFM does); the pipe that starts
///         the content and the one that ends it are delimiters, not cell boundaries; the blanks around a cell are
///         dropped. The prefix of a line (quote markers, list markers, indentation) is kept apart and never changes.
///     </para>
///     <para>
///         A table is read only when the reading is certain: a pipe that follows two or more backslashes (the parsers
///         disagree whether it is escaped), a tab inside a cell, a delimiter row that is not one, or a result that does not
///         agree with the parser's own view of the table (the number of columns, their alignment and the number of rows)
///         makes <see cref="TryParse"/> return <see langword="null"/>, and the table is then left as written.
///     </para>
///     <para>
///         Thread safety: immutable once created; safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class PipeTable
{
    /// <summary>
    ///     The fewest characters a delimiter cell of the aligned layout has (three dashes, or the colons and dashes they replace).
    /// </summary>
    private const int MinimumColumnWidth = 3;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PipeTable"/> class.
    /// </summary>
    /// <param name="rows">Every line of the table: the header row, the delimiter row and the body rows.</param>
    /// <param name="alignments">The alignment of every column the delimiter row declares.</param>
    private PipeTable(ImmutableArray<PipeTableRow> rows, ImmutableArray<TableAlignment> alignments)
    {
        Rows = rows;
        Alignments = alignments;
    }

    /// <summary>
    ///     Gets every line of the table in order; index 0 is the header row, index 1 the delimiter row.
    /// </summary>
    public ImmutableArray<PipeTableRow> Rows { get; }

    /// <summary>
    ///     Gets the alignment of every column, as the delimiter row declares them.
    /// </summary>
    public ImmutableArray<TableAlignment> Alignments { get; }

    /// <summary>
    ///     Gets the number of columns, which is the number of cells of the header row.
    /// </summary>
    public int ColumnCount => Alignments.Length;

    /// <summary>
    ///     Reads a table block.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="block">A block of the document. Must not be null.</param>
    /// <returns>The table, or <see langword="null"/> when the block is not a pipe table or cannot be read with certainty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static PipeTable? TryParse(LineLayout layout, BlockInfo block)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(block);

        // Only a pipe table of the parser is read: a grid table is another block kind
        if (block.Kind != BlockKind.Table || block.Node is not Table table)
        {
            return null;
        }

        var (first, last) = LineOwnership.LinesOf(layout, block);
        if (last <= first)
        {
            return null;
        }

        var rows = ImmutableArray.CreateBuilder<PipeTableRow>(last - first + 1);
        for (var line = first; line <= last; line++)
        {
            if (ReadRow(layout, block, line, line == first) is not { } row)
            {
                return null;
            }

            rows.Add(row);
        }

        if (ReadAlignments(rows[1]) is not { } alignments || alignments.Length != rows[0].Cells.Length)
        {
            return null;
        }

        // The reading must agree with the parser on what the table is
        return AgreesWithParser(layout.Text, table, alignments, rows) ? new PipeTable(rows.ToImmutable(), alignments) : null;
    }

    /// <summary>
    ///     Writes the table in the compact layout: one blank inside each pipe, no alignment of the columns.
    /// </summary>
    /// <returns>The content of every line (without its prefix), in the order of <see cref="Rows"/>.</returns>
    public ImmutableArray<string> ComposeCompact()
    {
        var lines = ImmutableArray.CreateBuilder<string>(Rows.Length);
        for (var index = 0; index < Rows.Length; index++)
        {
            lines.Add(index switch
            {
                1 => Join(Alignments.Select(static alignment => Delimiter(alignment, 0))),
                _ when IsPlainText(index) => Rows[index].Cells[0],
                _ => Join(Rows[index].Cells)
            });
        }

        return lines.ToImmutable();
    }

    /// <summary>
    ///     Writes the table in the aligned layout: every column as wide as its widest cell (at least three characters).
    /// </summary>
    /// <returns>The content of every line (without its prefix), in the order of <see cref="Rows"/>.</returns>
    /// <remarks>
    ///     The width is that of the text of the cell as written (a <c>\|</c> counts for its two characters, because the
    ///     source is what is being aligned), measured in terminal columns.
    /// </remarks>
    public ImmutableArray<string> ComposeAligned()
    {
        // A column is as wide as its widest cell; a row with more cells than the header adds columns of its own
        var columns = Rows.Max(static row => row.Cells.Length);
        var widths = new int[columns];
        Array.Fill(widths, MinimumColumnWidth);
        for (var index = 0; index < Rows.Length; index++)
        {
            if (index == 1 || IsPlainText(index))
            {
                continue;
            }

            for (var column = 0; column < Rows[index].Cells.Length; column++)
            {
                widths[column] = Math.Max(widths[column], TextWidth.Of(Rows[index].Cells[column]));
            }
        }

        var lines = ImmutableArray.CreateBuilder<string>(Rows.Length);
        for (var index = 0; index < Rows.Length; index++)
        {
            lines.Add(index switch
            {
                1 => Join(Alignments.Select((alignment, column) => Delimiter(alignment, widths[column]))),
                _ when IsPlainText(index) => Rows[index].Cells[0],
                _ => Join(Rows[index].Cells.Select((cell, column) => cell + new string(' ', widths[column] - TextWidth.Of(cell))))
            });
        }

        return lines.ToImmutable();
    }

    /// <summary>
    ///     Determines whether a line is a body row that holds no pipe at all: a line of text that the parser takes as a row of one cell.
    /// </summary>
    /// <param name="index">The index of the line in <see cref="Rows"/>.</param>
    /// <returns><see langword="true"/> for such a row, which is written as it is: giving it pipes is not the formatter's business.</returns>
    private bool IsPlainText(int index) => index >= 2 && Rows[index].HasNoPipe;

    /// <summary>
    ///     Joins cells into a line with one blank inside each pipe (one blank in all for an empty cell).
    /// </summary>
    /// <param name="cells">The text of the cells.</param>
    /// <returns>The line.</returns>
    private static string Join(IEnumerable<string> cells)
    {
        // A blank on each side of a cell's text; an empty cell is a single blank, so that no cell is wider than it needs to be
        var line = new StringBuilder("|");
        foreach (var cell in cells)
        {
            line.Append(' ');
            if (cell.Length > 0)
            {
                line.Append(cell).Append(' ');
            }

            line.Append('|');
        }

        return line.ToString();
    }

    /// <summary>
    ///     Writes one cell of the delimiter row.
    /// </summary>
    /// <param name="alignment">The alignment of the column.</param>
    /// <param name="width">The width of the column, or 0 for the shortest cell with three dashes.</param>
    /// <returns>The cell: dashes, with a colon on the side or sides the alignment asks for.</returns>
    private static string Delimiter(TableAlignment alignment, int width)
    {
        var colons = alignment switch
        {
            TableAlignment.Left or TableAlignment.Right => 1,
            TableAlignment.Center => 2,
            _ => 0
        };
        var dashes = width == 0 ? MinimumColumnWidth : Math.Max(1, width - colons);
        var line = new string('-', dashes);
        return alignment switch
        {
            TableAlignment.Left => ":" + line,
            TableAlignment.Right => line + ":",
            TableAlignment.Center => ":" + line + ":",
            _ => line
        };
    }

    /// <summary>
    ///     Reads one line of a table.
    /// </summary>
    /// <param name="layout">The line arithmetic.</param>
    /// <param name="block">The table block.</param>
    /// <param name="line">The zero-based line.</param>
    /// <param name="isFirst">Whether this is the first line of the table, where the block starts at the content.</param>
    /// <returns>The row, or <see langword="null"/> when the line cannot be read with certainty.</returns>
    private static PipeTableRow? ReadRow(LineLayout layout, BlockInfo block, int line, bool isFirst)
    {
        var text = layout.Text;
        var lineStart = layout.LineStart(line);
        var lineEnd = layout.LineEnd(line);

        // The content starts after the prefix: at the block start on the first line, after quote markers and blanks on the others
        var contentStart = isFirst ? block.Start : lineStart;
        if (!isFirst)
        {
            while (contentStart < lineEnd && text[contentStart] is ' ' or '\t' or '>')
            {
                contentStart++;
            }
        }

        if (contentStart < lineStart || contentStart >= lineEnd || LineLayout.IsBlankChar(text[contentStart]))
        {
            return null;
        }

        var contentEnd = lineEnd;
        while (LineLayout.IsBlankChar(text[contentEnd - 1]))
        {
            contentEnd--;
        }

        return Split(text[contentStart..contentEnd]) is { } split
            ? new PipeTableRow(line, text[lineStart..contentStart], contentStart, contentEnd, lineEnd, split.Cells, split.Leading, split.Trailing)
            : null;
    }

    /// <summary>
    ///     Splits the content of a line into cells.
    /// </summary>
    /// <param name="content">The content of the line: not empty, no blank at either end.</param>
    /// <returns>The trimmed cells and whether the content has a leading and a trailing pipe; <see langword="null"/> when the split is uncertain.</returns>
    private static (ImmutableArray<string> Cells, bool Leading, bool Trailing)? Split(string content)
    {
        var leading = content[0] == '|';
        var segments = new List<string>();
        var segmentStart = leading ? 1 : 0;
        var lastPipe = leading ? 0 : -1;
        var index = segmentStart;
        while (index < content.Length)
        {
            var c = content[index];
            if (c == '\\' && index + 1 < content.Length)
            {
                // A pipe after a backslash is part of the cell; a pipe after two backslashes is read differently by different parsers
                if (content[index + 1] == '\\' && index + 2 < content.Length && content[index + 2] == '|')
                {
                    return null;
                }

                index += 2;
                continue;
            }

            if (c == '|')
            {
                segments.Add(content[segmentStart..index]);
                segmentStart = index + 1;
                lastPipe = index;
            }

            index++;
        }

        segments.Add(content[segmentStart..]);

        // A pipe at the very end (other than the one that starts the content) ends the last cell and does not begin another
        var trailing = lastPipe == content.Length - 1 && !(leading && content.Length == 1);
        if (trailing)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        var cells = ImmutableArray.CreateBuilder<string>(segments.Count);
        foreach (var segment in segments)
        {
            var cell = segment.Trim(' ', '\t');
            if (cell.Contains('\t'))
            {
                return null;
            }

            cells.Add(cell);
        }

        return (cells.ToImmutable(), leading, trailing);
    }

    /// <summary>
    ///     Reads the alignment of every column from the delimiter row.
    /// </summary>
    /// <param name="delimiter">The second row of the table.</param>
    /// <returns>The alignments, or <see langword="null"/> when a cell is not made of dashes with optional colons at its ends.</returns>
    private static ImmutableArray<TableAlignment>? ReadAlignments(PipeTableRow delimiter)
    {
        var alignments = ImmutableArray.CreateBuilder<TableAlignment>(delimiter.Cells.Length);
        foreach (var cell in delimiter.Cells)
        {
            var left = cell.StartsWith(':');
            var right = cell.Length > 1 && cell.EndsWith(':');
            var dashes = cell.AsSpan(left ? 1 : 0, cell.Length - (left ? 1 : 0) - (right ? 1 : 0));
            if (dashes.IsEmpty || dashes.ContainsAnyExcept('-'))
            {
                return null;
            }

            alignments.Add((left, right) switch
            {
                (true, true) => TableAlignment.Center,
                (true, false) => TableAlignment.Left,
                (false, true) => TableAlignment.Right,
                _ => TableAlignment.None
            });
        }

        return alignments.ToImmutable();
    }

    /// <summary>
    ///     Determines whether the reading of a table agrees with the parser's view of it.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="table">The parser's table.</param>
    /// <param name="alignments">The alignments read from the delimiter row.</param>
    /// <param name="rows">The rows read (the delimiter row is one of them).</param>
    /// <returns>
    ///     <see langword="true"/> when the columns, their alignment, the number of rows and the text of every cell are the same.
    /// </returns>
    private static bool AgreesWithParser(string text, Table table, ImmutableArray<TableAlignment> alignments, ImmutableArray<PipeTableRow>.Builder rows)
    {
        if (table.ColumnDefinitions.Count != alignments.Length || table.Count != rows.Count - 1)
        {
            return false;
        }

        // Every cell the parser holds must be the text this reading holds, so that a rewrite of the blanks around the cells
        // cannot move text from one cell to another
        for (var index = 0; index < rows.Count; index++)
        {
            if (index != 1 && table[index == 0 ? 0 : index - 1] is TableRow parsed && !CellsAgree(text, rows[index], parsed))
            {
                return false;
            }
        }

        for (var column = 0; column < alignments.Length; column++)
        {
            var expected = table.ColumnDefinitions[column].Alignment switch
            {
                TableColumnAlign.Left => TableAlignment.Left,
                TableColumnAlign.Center => TableAlignment.Center,
                TableColumnAlign.Right => TableAlignment.Right,
                _ => TableAlignment.None
            };
            if (expected != alignments[column])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether the cells of a row, as this reading splits them, are the cells the parser holds.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="row">The row as read here.</param>
    /// <param name="parsed">The row as the parser holds it.</param>
    /// <returns><see langword="true"/> when every cell that both hold has the same text; an empty cell has no span to compare.</returns>
    private static bool CellsAgree(string text, PipeTableRow row, TableRow parsed)
    {
        var count = Math.Min(row.Cells.Length, parsed.Count);
        for (var column = 0; column < count; column++)
        {
            var ours = row.Cells[column];
            var span = ((TableCell)parsed[column]).Span;
            if (ours.Length == 0 || span.End < span.Start)
            {
                continue;
            }

            if (span.End >= text.Length
                || !string.Equals(text.AsSpan(span.Start, span.Length).Trim().ToString(), ours, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
///     One line of a pipe table: where its content is in the text and the cells it holds.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="PipeTable"/>. Immutable; safe for concurrent use.
/// </remarks>
/// <param name="Line">The zero-based line of the row.</param>
/// <param name="Prefix">The text of the line before its content: quote markers, list markers and indentation.</param>
/// <param name="ContentStart">The offset where the content of the line starts, just after the prefix.</param>
/// <param name="ContentEnd">The offset just after the last character of the content that is not a blank.</param>
/// <param name="LineEnd">The offset where the line ends (its line feed or the end of the text).</param>
/// <param name="Cells">The text of every cell, without the blanks around it and without the delimiting pipes.</param>
/// <param name="HasLeadingPipe">Whether the content starts with a pipe.</param>
/// <param name="HasTrailingPipe">Whether the content ends with a pipe that is not also its leading pipe.</param>
internal sealed record PipeTableRow(
    int Line,
    string Prefix,
    int ContentStart,
    int ContentEnd,
    int LineEnd,
    ImmutableArray<string> Cells,
    bool HasLeadingPipe,
    bool HasTrailingPipe)
{
    /// <summary>
    ///     Gets a value indicating whether the content has no pipe at all (a line of text that the parser takes as a row of one cell).
    /// </summary>
    public bool HasNoPipe => !HasLeadingPipe && !HasTrailingPipe && Cells.Length == 1;
}
/// <summary>
///     How the cells of a pipe table column are aligned, as the delimiter row says.
/// </summary>
internal enum TableAlignment
{
    /// <summary>
    ///     No colon in the delimiter cell: the parser's default alignment.
    /// </summary>
    None,

    /// <summary>
    ///     A colon before the dashes (<c>:---</c>).
    /// </summary>
    Left,

    /// <summary>
    ///     A colon on both sides of the dashes (<c>:---:</c>).
    /// </summary>
    Center,

    /// <summary>
    ///     A colon after the dashes (<c>---:</c>).
    /// </summary>
    Right
}
