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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the PipeTable class reads the cells of a table from its source lines, and writes the compact and aligned layouts.
/// </summary>
public class PipeTableTests
{
    /// <summary>
    ///     Reads the first table block of a text.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The table, or <see langword="null"/> when the first table-like block cannot be read.</returns>
    private static PipeTable? Read(string text)
    {
        var document = BlockFormatterAssertions.Parse(text);
        var block = document.Blocks.First(candidate => candidate.Kind is BlockKind.Table or BlockKind.GridTable);
        return PipeTable.TryParse(new LineLayout(document.Source), block);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void PipeTable_TryParse_NullArguments_Throw()
    {
        // Arrange: a document with a table
        var document = BlockFormatterAssertions.Parse("| a |\n| --- |\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Table);

        // Act & Assert: both arguments are checked and named
        Assert.Equal("layout", Assert.Throws<ArgumentNullException>(() => PipeTable.TryParse(null!, block)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => PipeTable.TryParse(new LineLayout(document.Source), null!)).ParamName);
    }

    /// <summary>
    ///     Test that the cells, the pipes at the ends and the alignment are read from the source lines.
    /// </summary>
    [Fact]
    public void PipeTable_TryParse_Table_ReadsCellsPipesAndAlignment()
    {
        // Arrange and Act: a table with every alignment, an escaped pipe, a missing cell and a line without leading pipe
        var table = Read("|  a  | b |c|d|\n|:--|:-:|--:|---|\n| 1 \\| 2 | `x` | |\n3 | 4 | 5 | 6 | 7\n");

        // Assert: four columns with their alignment, and the cells of each line as written, trimmed
        Assert.NotNull(table);
        Assert.Equal(4, table.ColumnCount);
        Assert.Equal([TableAlignment.Left, TableAlignment.Center, TableAlignment.Right, TableAlignment.None], table.Alignments);
        Assert.Equal(["a", "b", "c", "d"], table.Rows[0].Cells);
        Assert.Equal(["1 \\| 2", "`x`", ""], table.Rows[2].Cells);
        Assert.Equal(["3", "4", "5", "6", "7"], table.Rows[3].Cells);
        Assert.True(table.Rows[0].HasLeadingPipe && table.Rows[0].HasTrailingPipe);
        Assert.True(table.Rows[2].HasLeadingPipe && table.Rows[2].HasTrailingPipe);
        Assert.False(table.Rows[3].HasLeadingPipe);
        Assert.False(table.Rows[3].HasTrailingPipe);
        Assert.Equal([0, 1, 2, 3], table.Rows.Select(row => row.Line));
    }

    /// <summary>
    ///     Test that the prefix of every line is the text before its content, also in a list item and a block quote.
    /// </summary>
    [Theory]
    [InlineData("| a |\n| --- |\n| 1 |\n", "", "")]
    [InlineData("- | a |\n  | --- |\n  | 1 |\n", "- ", "  ")]
    [InlineData("> | a |\n> | --- |\n> | 1 |\n", "> ", "> ")]
    [InlineData("> - | a |\n>   | --- |\n>   | 1 |\n", "> - ", ">   ")]
    public void PipeTable_TryParse_TableInContainer_SeparatesThePrefix(string text, string first, string other)
    {
        // Act: read the table
        var table = Read(text);

        // Assert: the first line has the prefix with the list marker, the others the prefix without it
        Assert.NotNull(table);
        Assert.Equal(first, table.Rows[0].Prefix);
        Assert.All(table.Rows.Skip(1), row => Assert.Equal(other, row.Prefix));
        Assert.All(table.Rows, row => Assert.Equal('|', text[row.ContentStart]));
    }

    /// <summary>
    ///     Test that a table that cannot be read with certainty is not read.
    /// </summary>
    [Theory]
    [InlineData("+---+---+\n| a | b |\n+===+===+\n| 1 | 2 |\n+---+---+\n")]
    [InlineData("| a\tb |\n| --- |\n")]
    [InlineData("| a \\\\| b |\n| --- |\n")]
    public void PipeTable_TryParse_UncertainOrOtherBlock_ReturnsNull(string text) => Assert.Null(Read(text));

    /// <summary>
    ///     Test that the compact layout needs one blank inside each pipe and that a line of text without any pipe is kept as it is.
    /// </summary>
    [Fact]
    public void PipeTable_ComposeCompact_Table_WritesOneBlankInsideEachPipe()
    {
        // Arrange: a table with a row that is a line of text
        var table = Read("a|b\n:-:|-\n1|2\ntext\n");

        // Act: compose
        Assert.NotNull(table);
        var lines = table.ComposeCompact();

        // Assert: the pipes are added, the line of text is not changed
        Assert.Equal(["| a | b |", "| :---: | --- |", "| 1 | 2 |", "text"], lines);
    }

    /// <summary>
    ///     Test that the aligned layout is as wide as the widest cell of each column and never narrower than three columns.
    /// </summary>
    [Fact]
    public void PipeTable_ComposeAligned_Table_PadsToTheWidestCell()
    {
        // Arrange: a wide cell, a narrow column, a row with an extra cell and a line of text
        var table = Read("| a | 日本語 |\n|:-|-:|\n| widest | b |\n| x | y | extra |\ntext\n");

        // Act: compose
        Assert.NotNull(table);
        var lines = table.ComposeAligned();

        // Assert: the columns are 6 and 6 wide, the extra cell has a column of its own, the text is kept
        Assert.Equal(
            ["| a      | 日本語 |", "| :----- | -----: |", "| widest | b      |", "| x      | y      | extra |", "text"],
            lines);
        Assert.Equal(table.Rows.Length, lines.Length);
    }

    /// <summary>
    ///     Test that the result of a layout is a function of the cells: composing the composed table gives the same lines.
    /// </summary>
    [Fact]
    public void PipeTable_Compose_ComposedTable_IsStable()
    {
        // Arrange: a table and its layouts
        var original = Read("|a|:-:|\n|-|:-:|\n|1|22222|\n");
        Assert.NotNull(original);

        foreach (var lines in new[] { original.ComposeCompact(), original.ComposeAligned() })
        {
            // Act: read the composed layout again
            var again = Read(string.Join('\n', lines) + "\n");

            // Assert: the same cells, and the same layouts
            Assert.NotNull(again);
            Assert.Equal(original.ComposeCompact(), again.ComposeCompact());
            Assert.Equal(original.ComposeAligned(), again.ComposeAligned());
        }
    }

    /// <summary>
    ///     Test that a row cannot be created with invalid arguments.
    /// </summary>
    [Fact]
    public void PipeTableRow_Constructor_InvalidArguments_Throw()
    {
        // Act & Assert: each invalid argument is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipeTableRow(-1, string.Empty, 0, 0, 0, [], false, false));
        Assert.Throws<ArgumentNullException>(() => new PipeTableRow(0, null!, 0, 0, 0, [], false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipeTableRow(0, string.Empty, 2, 1, 3, [], false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipeTableRow(0, string.Empty, 0, 2, 1, [], false, false));
        Assert.Throws<ArgumentException>(() => new PipeTableRow(0, string.Empty, 0, 1, 1, default, false, false));
        Assert.Throws<ArgumentNullException>(() => new PipeTableRow(0, string.Empty, 0, 1, 1, new string[] { null! }.ToImmutableArray(), false, false));
    }
}
