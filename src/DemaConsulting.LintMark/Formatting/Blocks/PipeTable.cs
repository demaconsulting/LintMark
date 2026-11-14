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
using DemaConsulting.LintMark.Utilities;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     The source of a GFM pipe table read line by line, and the two canonical layouts it can be written in.
/// </summary>
/// <remarks>
///     <para>
///         The parser decides which lines are a table; this type reads the cells from the source text of those lines
///         (never from the rendered inline content), so that the text of a cell survives byte for byte. A line is split
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
        return AgreesWithParser(table, alignments, rows.Count) ? new PipeTable(rows.ToImmutable(), alignments) : null;
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
    /// <param name="table">The parser's table.</param>
    /// <param name="alignments">The alignments read from the delimiter row.</param>
    /// <param name="lineCount">The number of lines read (the delimiter row is one of them).</param>
    /// <returns><see langword="true"/> when the columns, their alignment and the number of rows are the same.</returns>
    private static bool AgreesWithParser(Table table, ImmutableArray<TableAlignment> alignments, int lineCount)
    {
        if (table.ColumnDefinitions.Count != alignments.Length || table.Count != lineCount - 1)
        {
            return false;
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
}
