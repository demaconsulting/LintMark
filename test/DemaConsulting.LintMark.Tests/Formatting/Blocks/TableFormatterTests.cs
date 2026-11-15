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
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the TableFormatter class writes pipe tables in the compact or aligned layout, keeps the text of the cells
///     and the prefix of the lines, and leaves a table as written when it cannot be sure of the result.
/// </summary>
public class TableFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly TableFormatter Formatter = new();

    /// <summary>
    ///     A configuration that asks for aligned tables.
    /// </summary>
    private static readonly LintMarkConfig Aligned = LintMarkConfig.Default with { Tables = TableStyle.Aligned };

    /// <summary>
    ///     Asserts that the formatter alone turns the input into the expected text and that the result is safe and stable.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertFormats(string input, string expected, LintMarkConfig? config = null) =>
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected, config);

    /// <summary>
    ///     Test that pipe tables are accepted and grid tables and other blocks are not.
    /// </summary>
    [Theory]
    [InlineData("| a |\n| --- |\n", true)]
    [InlineData("a | b\n--|--\n", true)]
    [InlineData("+---+\n| a |\n+---+\n", false)]
    [InlineData("text\n", false)]
    [InlineData("- | a |\n  | --- |\n", true)]
    public void TableFormatter_CanFormat_Blocks_AcceptsOnlyPipeTables(string text, bool accepted)
    {
        // Arrange: a document
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: a block is accepted exactly when it is a pipe table
        Assert.Equal(accepted, document.Blocks.Any(block => Formatter.CanFormat(block)));
    }

    /// <summary>
    ///     Test that the compact layout puts one blank inside each pipe and writes the shortest delimiter row, keeping the alignment.
    /// </summary>
    [Theory]
    [InlineData("|a|b|\n|-|-|\n|1|2|\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\n")]
    [InlineData("|a|b|c|d|\n|:-|:-:|-:|-----|\n", "| a | b | c | d |\n| :--- | :---: | ---: | --- |\n")]
    [InlineData("a | b\n--- | ---\n1 | 2\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\n")]
    [InlineData("|  a   |   b|\n| ----- | ---- |\n|   1|2   |\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\n")]
    [InlineData("| a | b |   \n| --- | --- |  \n| 1 | 2 |\t\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\n")]
    [InlineData("| a | b |\n| --- | --- |\n| | x |\n", "| a | b |\n| --- | --- |\n| | x |\n")]
    [InlineData("| a | b |\n| --- | --- |\n| x | |\n", "| a | b |\n| --- | --- |\n| x | |\n")]
    [InlineData("| 日本語 | é |\n|---|---|\n| ab | 😀 |\n", "| 日本語 | é |\n| --- | --- |\n| ab | 😀 |\n")]
    public void TableFormatter_Format_CompactLayout_IsWrittenFromTheCells(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a table that is already compact is not touched.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_CompactTable_ProposesNothing() =>
        BlockFormatterAssertions.AssertUnchanged("| a | b |\n| :--- | ---: |\n| 1 | 2 |\n");

    /// <summary>
    ///     Test that the text of a cell is kept byte for byte: escaped pipes, code spans, inline HTML, links and trailing backslashes.
    /// </summary>
    [Theory]
    [InlineData("|a|b|\n|-|-|\n|`x\\|y`|<b>z</b>|\n", "| a | b |\n| --- | --- |\n| `x\\|y` | <b>z</b> |\n")]
    [InlineData("|a|b|\n|-|-|\n|[l](http://x.org \"t\")|![i](u.png)|\n", "| a | b |\n| --- | --- |\n| [l](http://x.org \"t\") | ![i](u.png) |\n")]
    [InlineData("|a|b|\n|-|-|\n|x \\\\ y|trailing\\|\n", "| a | b |\n| --- | --- |\n| x \\\\ y | trailing\\| |\n")]
    [InlineData("|a|b|\n|-|-|\n|*em*|__st__ &amp; &lt;|\n", "| a | b |\n| --- | --- |\n| *em* | __st__ &amp; &lt; |\n")]
    public void TableFormatter_Format_CellText_IsKeptByteForByte(string input, string expected) => AssertFormats(input, expected);

    /// <summary>
    ///     Test that rows with more or fewer cells than the header keep the cells they have and a line of text keeps no pipe.
    /// </summary>
    [Theory]
    [InlineData("a | b\n--- | ---\n1 | 2 | 3\n4 |\n", "| a | b |\n| --- | --- |\n| 1 | 2 | 3 |\n| 4 |\n")]
    [InlineData("| a | b |\n| --- | --- |\n| 1 | 2 |\nno pipe here\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\nno pipe here\n")]
    public void TableFormatter_Format_RowsOfOtherLength_AreNeverRepaired(string input, string expected) => AssertFormats(input, expected);

    /// <summary>
    ///     Test that the prefix of every line (list markers, indentation, quote markers) is kept.
    /// </summary>
    [Theory]
    [InlineData("- |a|b|\n  |-|-|\n  |1|2|\n", "- | a | b |\n  | --- | --- |\n  | 1 | 2 |\n")]
    [InlineData("- item\n\n  |a|b|\n  |:-:|-:|\n  |1|2|\n", "- item\n\n  | a | b |\n  | :---: | ---: |\n  | 1 | 2 |\n")]
    [InlineData("> |a|b|\n> |-|-|\n> |1|2|\n", "> | a | b |\n> | --- | --- |\n> | 1 | 2 |\n")]
    [InlineData("> > |a|b|\n> > |-|-|\n", "> > | a | b |\n> > | --- | --- |\n")]
    [InlineData("1. one\n\n   > |a|b|\n   > |-|-|\n   > |1|2|\n", "1. one\n\n   > | a | b |\n   > | --- | --- |\n   > | 1 | 2 |\n")]
    [InlineData("   |a|b|\n   |-|-|\n", "   | a | b |\n   | --- | --- |\n")]
    public void TableFormatter_Format_TableInContainer_KeepsThePrefix(string input, string expected) => AssertFormats(input, expected);

    /// <summary>
    ///     Test that tables are left as written when the reading is not certain: a tab in a cell, a trailing backslash, a cell with a
    ///     no-break space at its edge (the parser's cell is not the cell this reading finds), a grid table.
    /// </summary>
    [Theory]
    [InlineData("|a\tb|c|\n|-|-|\n")]
    [InlineData("|a|b\\\\|\n|-|-|\n|1|2|\n")]
    [InlineData("|\u00A0a|b\u00A0|\n|-|-|\n")]
    [InlineData("+---+---+\n| a | b |\n+===+===+\n| 1 | 2 |\n+---+---+\n")]
    public void TableFormatter_Format_UncertainTable_IsLeftAsWritten(string input) =>
        Assert.Empty(BlockFormatterAssertions.ApplyFormatter(Formatter, input).Edits);

    /// <summary>
    ///     Test that a pipe inside a code span, an HTML attribute, a link or math splits the cell as the parser does (so the row
    ///     has the cells the parser sees), and that the rewrite then keeps the page.
    /// </summary>
    [Theory]
    [InlineData("|a|b|\n|-|-|\n|`x|y`|z|\n", "| a | b |\n| --- | --- |\n| `x | y` | z |\n")]
    [InlineData("|a|b|\n|-|-|\n|<span title=\"a|b\">x</span>|z|\n", "| a | b |\n| --- | --- |\n| <span title=\"a | b\">x</span> | z |\n")]
    [InlineData("|a|b|\n|-|-|\n|[a|b](u)|z|\n", "| a | b |\n| --- | --- |\n| [a | b](u) | z |\n")]
    [InlineData("|a|b|\n|-|-|\n|$a|b$|z|\n", "| a | b |\n| --- | --- |\n| $a | b$ | z |\n")]
    [InlineData("|a|b|\n|-|-|\n|<http://x/a|b>|z|\n", "| a | b |\n| --- | --- |\n| <http://x/a | b> | z |\n")]
    public void TableFormatter_Format_PipeInsideInlineConstruct_KeepsTheSamePage(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the aligned layout pads every column to its widest cell, measured in display columns.
    /// </summary>
    [Theory]
    [InlineData("| a | 日本 |\n|---|---|\n| long cell | x |\n", "| a         | 日本 |\n| --------- | ---- |\n| long cell | x    |\n")]
    [InlineData("|h|i|j|\n|:-|-:|:-:|\n|aaaa|bbbb|cccc|\n", "| h    | i    | j    |\n| :--- | ---: | :--: |\n| aaaa | bbbb | cccc |\n")]
    [InlineData("|a|\n|-|\n|b|\n", "| a   |\n| --- |\n| b   |\n")]
    [InlineData("| a | b |\n| --- | --- |\n| `x\\|y` | z |\n", "| a      | b   |\n| ------ | --- |\n| `x\\|y` | z   |\n")]
    public void TableFormatter_Format_AlignedLayout_PadsTheColumns(string input, string expected) => AssertFormats(input, expected, Aligned);

    /// <summary>
    ///     Test that an aligned table inside a block quote or a list item keeps its prefix.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_AlignedTableInContainer_KeepsThePrefix() =>
        AssertFormats("> |a|b|\n> |-|-|\n> |long|x|\n", "> | a    | b   |\n> | ---- | --- |\n> | long | x   |\n", Aligned);

    /// <summary>
    ///     Test that an aligned table that does not fit the line length is written compact, and that no limit always aligns.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_AlignedTableThatDoesNotFit_FallsBackToCompact()
    {
        // Arrange: the aligned form is 33 columns wide, the compact form 23
        const string input = "| a | bbbbbbbbbbbbbbbb |\n|---|---|\n| cccccccccc | d |\n";

        // Act & Assert: it fits no limit of 25, fits a limit of 33 and always fits without a limit
        AssertFormats(input, "| a | bbbbbbbbbbbbbbbb |\n| --- | --- |\n| cccccccccc | d |\n", Aligned with { LineLength = 25 });
        AssertFormats(
            input,
            "| a          | bbbbbbbbbbbbbbbb |\n| ---------- | ---------------- |\n| cccccccccc | d                |\n",
            Aligned with { LineLength = 33 });
        AssertFormats(
            input,
            "| a          | bbbbbbbbbbbbbbbb |\n| ---------- | ---------------- |\n| cccccccccc | d                |\n",
            Aligned with { LineLength = 0 });
    }

    /// <summary>
    ///     Test that an aligned table is not changed again, and that the layout of the input never changes the output.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_AlignedAndCompactInputs_GiveTheSameResult()
    {
        // Arrange: one table written compact, padded and aligned
        string[] inputs = ["|a|b|\n|-|-|\n|long|x|\n", "|   a   |b|\n|:--:|---|\n|long  |   x|\n", "| a    | b |\n| ---- | --- |\n| long | x   |\n"];

        // Act: format each as aligned and as compact
        var aligned = inputs.Select(input => BlockFormatterAssertions.ApplyFormatter(Formatter, input, Aligned).Formatted).Distinct().ToList();
        var compact = inputs.Select(input => BlockFormatterAssertions.ApplyFormatter(Formatter, input).Formatted).Distinct().ToList();

        // Assert: the cells decide the output, but the alignment colons of the input are kept
        Assert.Equal(2, aligned.Count);
        Assert.Equal(2, compact.Count);
        Assert.Equal("| a    | b   |\n| ---- | --- |\n| long | x   |\n", aligned[0]);
    }

    /// <summary>
    ///     Test that the rule of an edit tells what changed: pipes, layout or trailing blanks.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_Edits_CarryTheRuleOfTheChange()
    {
        // Arrange: a line without pipes at its ends, a line with a wrong layout, a line with trailing blanks only and a canonical line
        var (edits, _) = BlockFormatterAssertions.ApplyFormatter(Formatter, "a | b\n|---|---|\n| 1 | 2 |  \n| 3 | 4 |\n");

        // Act & Assert: three lines change, each under its own rule, and the canonical line is not touched
        Assert.Equal(
            [RuleCatalog.TablePipeStyleRuleId, RuleCatalog.TableColumnStyleRuleId, RuleCatalog.TrailingWhitespaceRuleId],
            edits.Select(edit => edit.RuleId));
    }

    /// <summary>
    ///     Test that a suppression of a table rule leaves the table as written, by the rule id and by the markdownlint number.
    /// </summary>
    [Theory]
    [InlineData("<!-- markdownlint-disable MD060 -->\n\n|a|b|\n|-|-|\n")]
    [InlineData("<!-- lintmark-disable table-pipe-style -->\n\na | b\n--|--\n")]
    [InlineData("<!-- markdownlint-disable-next-line MD055 -->\n|a|b|\n|-|-|\n")]
    public void TableFormatter_Format_SuppressedRule_LeavesTheTableAsWritten(string text) =>
        BlockFormatterAssertions.AssertUnchanged(text);

    /// <summary>
    ///     Test that awkward tables are rewritten safely and stably with the production formatters, both layouts.
    /// </summary>
    [Theory]
    [InlineData("||\n|-|\n")]
    [InlineData("|\n|-|\n|\n")]
    [InlineData("|a|\n|-|\n||\n|||\n")]
    [InlineData("|a|b|\n|-|-|\n|x \\| y|`a|b`|\n|[a|b](u)|<span a=\"|\">|\n")]
    [InlineData("|a|b|\n|-|-|\n|trailing\\|\n|\\|lead|x|\n")]
    [InlineData("|a|b|\n|-|-|\n|1|2|\n- list\n")]
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |\n\n[^1]: | x | y |\n    |---|---|\n")]
    [InlineData("> |a|b|\n|-|-|\n> |1|2|\n")]
    [InlineData("- a\n\n    |a|b|\n    |-|-|\n")]
    [InlineData("Text\n|a|b|\n|-|-|\n|1|2|\nMore\n")]
    public void TableFormatter_Format_AwkwardTables_AreSafeAndStable(string text)
    {
        BlockFormatterAssertions.AssertSafeAndStable(text);
        BlockFormatterAssertions.AssertSafeAndStable(text, Aligned);
        BlockFormatterAssertions.AssertSafeAndStable(text.Replace("|", "\u2502", StringComparison.Ordinal));
    }
}

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
}
