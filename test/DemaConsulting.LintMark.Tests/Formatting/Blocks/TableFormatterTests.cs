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
    ///     Test that null arguments are rejected and a block that is not a table is refused.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_InvalidArguments_Throw()
    {
        // Arrange: a document with a table and a paragraph
        var document = BlockFormatterAssertions.Parse("text\n\n| a |\n| --- |\n");
        var context = new FormatContext(document, LintMarkConfig.Default);
        var table = BlockFormatterAssertions.FirstBlock(document, BlockKind.Table);
        var paragraph = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);

        // Act & Assert: every argument is checked and named
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, table, new EditSet())).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, new EditSet())).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, table, null!)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentException>(() => Formatter.Format(context, paragraph, new EditSet())).ParamName);
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
    ///     Test that tables are left as written when the reading is not certain.
    /// </summary>
    [Theory]
    [InlineData("|a\tb|c|\n|-|-|\n")]
    [InlineData("|a|b\\\\|\n|-|-|\n|1|2|\n")]
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
    ///     Test that tables stay the same with every stored form of the text (CRLF, mixed, byte order mark).
    /// </summary>
    [Fact]
    public void TableFormatter_Format_StoredForms_GiveTheSameEditsAndBytes()
    {
        // Arrange: a table inside a list item with an introduction
        const string input = "- item\n\n  |a|b|\n  |-|-|\n  |1|2|\n";

        // Act & Assert: the edits and the written bytes do not depend on how the text is stored
        BlockFormatterAssertions.AssertSameEditsWithCrlf(Formatter, input);
        BlockFormatterAssertions.AssertSameEditsWithByteOrderMark(Formatter, input);
        BlockFormatterAssertions.AssertFormatsOnDisk(input, "- item\n\n  | a | b |\n  | --- | --- |\n  | 1 | 2 |\n");
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

    /// <summary>
    ///     Test that generated tables (random cells with escapes, code spans, inline HTML, wide characters, missing pipes and
    ///     uneven rows, in plain text, block quotes and list items) are rewritten safely and stably in both layouts.
    /// </summary>
    [Fact]
    public void TableFormatter_Format_GeneratedTables_AreSafeAndStable()
    {
        // Arrange: a fixed seed, so the documents are the same on every run
        var random = new Random(20260514);
        string[] fragments =
        [
            "a", "bb c", "`x|y`", "\\|", "<b>x</b>", "日本", "e\u0301", string.Empty, " ", "*e*", "[l](u)", "x\\", "\\\\", "``", "&amp;", "a  b", "😀", "$x$", "![i](u)"
        ];
        string[] delimiters = ["---", ":--", "--:", ":-:", "-", ":---:", " --- "];
        (string First, string Other)[] contexts = [(string.Empty, string.Empty), ("> ", "> "), ("- ", "  "), ("1. ", "   "), ("> - ", ">   ")];

        string Row(int cells, bool padded)
        {
            var parts = Enumerable.Range(0, cells).Select(_ => fragments[random.Next(fragments.Length)]).ToList();
            var pad = padded ? "  " : string.Empty;
            var leading = random.Next(4) > 0 ? "|" : string.Empty;
            var trailing = random.Next(4) > 0 ? "|" : string.Empty;
            return leading + string.Join("|", parts.Select(part => pad + part + pad)) + trailing;
        }

        // Act & Assert: every generated document passes every safety assertion, and most of them are changed by the rewrite
        var changed = 0;
        for (var i = 0; i < 400; i++)
        {
            var columns = random.Next(1, 5);
            var (first, other) = contexts[random.Next(contexts.Length)];
            var lines = new List<string> { Row(columns, random.Next(2) == 0), "|" + string.Join("|", Enumerable.Range(0, columns).Select(_ => delimiters[random.Next(delimiters.Length)])) + "|" };
            lines.AddRange(Enumerable.Range(0, random.Next(0, 4)).Select(_ => Row(random.Next(1, 6), random.Next(2) == 0)));
            var text = first + lines[0] + "\n" + string.Concat(lines.Skip(1).Select(line => other + line + "\n"));
            try
            {
                changed += BlockFormatterAssertions.AssertSafeAndStable(text, checkParallel: false) == text ? 0 : 1;
                BlockFormatterAssertions.AssertSafeAndStable(text, Aligned, checkParallel: false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Document {i} failed:\n{text}\n{ex.Message}", ex);
            }
        }

        Assert.True(changed > 200, $"Only {changed} of 400 generated documents were rewritten.");
    }
}
