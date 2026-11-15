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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the TableRules class reports tables that are too wide, rows with the wrong number of cells and grid tables.
/// </summary>
public class TableRulesTests
{
    /// <summary>
    ///     The rules under test.
    /// </summary>
    private static readonly TableRules Rules = new();

    /// <summary>
    ///     Parses LF-normalized text as a document named <c>a.md</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Runs the rules over a text and formats the findings as <c>line:column rule</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The findings.</returns>
    private static string[] Findings(string text, LintMarkConfig? config = null) =>
        [.. Rules.Check(Parse(text), config ?? LintMarkConfig.Default).Select(d => $"{d.Line}:{d.Column} {d.RuleId}")];

    /// <summary>
    ///     Test that the rule identifiers are the three table lint rules.
    /// </summary>
    [Fact]
    public void TableRules_RuleIds_AreTheTableLintRules() =>
        Assert.Equal(["table-too-wide", "table-column-count", "table-grid-unformatted"], Rules.RuleIds);

    /// <summary>
    ///     Test that a row with another number of cells than the header is reported at its content, and that a good table is not.
    /// </summary>
    [Fact]
    public void TableRules_Check_RowsWithOtherCellCounts_AreReportedAtTheRow()
    {
        // Act & Assert: the short row and the long row are reported; the delimiter row and the good rows are not
        Assert.Equal(["4:1 table-column-count", "5:1 table-column-count"], Findings("| a | b |\n| --- | --- |\n| 1 | 2 |\n| 3 |\n| 4 | 5 | 6 |\n"));
        Assert.Empty(Findings("| a | b |\n| --- | --- |\n| 1 | 2 |\n"));

        // The position is the start of the content, after the prefix of a container
        Assert.Equal(["4:3 table-column-count"], Findings("- | a | b |\n  | --- | --- |\n  | 1 | 2 |\n  | 3 |\n"));
    }

    /// <summary>
    ///     Test that a table that is too wide when compact is reported at its first line that is too wide, whatever its layout is now.
    /// </summary>
    [Fact]
    public void TableRules_Check_TableTooWideWhenCompact_IsReportedAtTheFirstWideLine()
    {
        // Arrange: a limit of 20 columns
        var config = LintMarkConfig.Default with { LineLength = 20 };

        // Act & Assert: the row of 24 columns is reported; a padded table that is compact-narrow is not; no limit reports nothing
        Assert.Equal(["1:1 table-too-wide"], Findings("| a | bbbbbbbbbbbbbbbb |\n| --- | --- |\n| c | d |\n", config));
        Assert.Equal(["3:1 table-too-wide"], Findings("| a | b |\n| --- | --- |\n| cccccccccccc | dddddddd |\n", config));
        Assert.Empty(Findings("| a                    | b    |\n| -------------------- | ---- |\n| c                    | d    |\n", config));
        Assert.Empty(Findings("| a | bbbbbbbbbbbbbbbb |\n| --- | --- |\n", config with { LineLength = 0 }));

        // The width counts the prefix of the container and the display width of wide characters
        Assert.Equal(["1:3 table-too-wide"], Findings("> | 日本語日本語日本語 | b |\n> | --- | --- |\n", config));
        Assert.Empty(Findings("| 日本語日本語 | b |\n| --- | --- |\n", config));
    }

    /// <summary>
    ///     Test that grid tables are reported by the rule that is off by default, and never as a pipe table.
    /// </summary>
    [Fact]
    public void TableRules_Check_GridTable_IsReportedWithoutOtherFindings()
    {
        // Act & Assert: the grid table is named once, at its first line
        Assert.Equal(["3:1 table-grid-unformatted"], Findings("text\n\n+---+---+\n| a | b |\n+===+===+\n| 1 | 2 |\n+---+---+\n"));
    }

    /// <summary>
    ///     Test that a table that cannot be read with certainty is not reported.
    /// </summary>
    [Fact]
    public void TableRules_Check_UncertainTable_ReportsNothing() =>
        Assert.Empty(Findings("| a \\\\| b | c |\n| --- | --- |\n| 1 |\n", LintMarkConfig.Default with { LineLength = 5 }));
}
