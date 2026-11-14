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

using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the table rules that the LintMarkEngine class applies by default: the table layout, and the
///     lint-only findings about tables with the configuration and the suppression comments.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     Test that the fix option writes tables compact by default and that the file is then clean.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixTable_WritesTheCompactLayoutAndReportsNothing()
    {
        // Arrange: a padded table and a table without leading pipes
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("| a   | b |\n| --- | --- |\n| 1   | 2 |\n\nx | y\n--|--\n"));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--fix");

        // Assert: both tables are compact and nothing remains to report
        Assert.Equal(Utf8("| a | b |\n| --- | --- |\n| 1 | 2 |\n\n| x | y |\n| --- | --- |\n"), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that checking reports the layout of a table where it changes, under the rule of the change, and writes nothing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckTable_ReportsTheLayoutAndWritesNothing()
    {
        // Arrange: a table with a padded line and a line without pipes at its ends
        using var directory = new TemporaryDirectory();
        var content = Utf8("| a   | b |\n| --- | --- |\nx | y\n");
        var (path, writeTime) = WriteOld(directory, "a.md", content);

        // Act: run the default engine without options
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: one finding per changed line, under the rule that explains the change, and the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: table-column-style A table is not written in the configured compact or aligned layout.",
                "a.md:3:1: table-pipe-style A line of a table lacks the pipe at its start or at its end."
            ],
            lines);
        AssertUntouched(path, content, writeTime);
    }

    /// <summary>
    ///     Test that the aligned layout of the configuration is written, and that a table that does not fit is written compact and reported as too wide.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixTableAligned_AlignsWhatFitsAndReportsWhatDoesNot()
    {
        // Arrange: a narrow and a wide table, aligned layout and a limit of 30
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "tables: aligned\nline-length: 30\n");
        var path = directory.WriteBytes(
            "a.md",
            Utf8("|a|b|\n|-|-|\n|long|x|\n\n| head one is wide | head two is wide |\n|---|---|\n| cell | cell |\n"));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the narrow table is aligned, the wide one compact, and only the wide one is reported
        Assert.Equal(
            Utf8("| a    | b   |\n| ---- | --- |\n| long | x   |\n\n| head one is wide | head two is wide |\n| --- | --- |\n| cell | cell |\n"),
            File.ReadAllBytes(path));
        Assert.Equal(1, exitCode);
        Assert.Equal("a.md:5:1: table-too-wide Table is 39 columns wide even when compact; the limit is 30", Assert.Single(lines));
    }

    /// <summary>
    ///     Test that rows with the wrong number of cells are reported and kept, with a rule that can be switched off by id or by number.
    /// </summary>
    [Theory]
    [InlineData("rules:\n  table-column-count: false\n", false)]
    [InlineData("rules:\n  MD056: false\n", false)]
    [InlineData("", true)]
    public void LintMarkEngine_Run_FixTableWithShortRow_ReportsItUnlessSwitchedOff(string config, bool reported)
    {
        // Arrange: a table with a row that lacks a cell
        using var directory = new TemporaryDirectory();
        if (config.Length > 0)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var path = directory.WriteBytes("a.md", Utf8("|a|b|\n|-|-|\n|1|\n"));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the row is never repaired; the finding is there exactly when the rule is on
        Assert.Equal(Utf8("| a | b |\n| --- | --- |\n| 1 |\n"), File.ReadAllBytes(path));
        Assert.Equal(reported ? 1 : 0, exitCode);
        Assert.Equal(reported ? ["a.md:3:1: table-column-count Table row has 1 cells; the header has 2"] : [], lines);
    }

    /// <summary>
    ///     Test that a suppression comment keeps both the layout and the findings of a table as written.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixTableInSuppressedRegion_KeepsTheTableAndReportsNothing()
    {
        // Arrange: a padded table with a short row inside a region where the table rules are disabled
        using var directory = new TemporaryDirectory();
        var text = "<!-- markdownlint-disable MD056 MD060 -->\n\n| a   | b |\n| --- | --- |\n| 1 |\n";
        var path = directory.WriteBytes("a.md", Utf8(text));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file is as written and nothing is reported
        Assert.Equal(Utf8(text), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a grid table is kept as written and reported only when the rule is switched on.
    /// </summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("rules:\n  table-grid-unformatted: true\n", 1)]
    public void LintMarkEngine_Run_FixGridTable_KeepsTheTableAndReportsItWhenAsked(string config, int expectedExitCode)
    {
        // Arrange: a grid table
        using var directory = new TemporaryDirectory();
        if (config.Length > 0)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var text = "+-----+-----+\n| a   | b   |\n+=====+=====+\n| 1   | 2   |\n+-----+-----+\n";
        var path = directory.WriteBytes("a.md", Utf8(text));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the table is as written, and the finding is there exactly when asked for
        Assert.Equal(Utf8(text), File.ReadAllBytes(path));
        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedExitCode == 1 ? ["a.md:1:1: table-grid-unformatted Grid table is left as written"] : [], lines);
    }

    /// <summary>
    ///     Test that a lint rule that throws is reported as a failure of the file it was run for.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_LintRuleThatThrows_FailsOnlyItsFile()
    {
        // Arrange: two files and a runner whose rule throws
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("a.md", Utf8("text\n"));
        var runner = new DemaConsulting.LintMark.Linting.LintRuleEngine([new ThrowingRule()]);

        // Act: check with the broken runner
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2, lintRules: runner));

        // Assert: the failure is reported as a formatting failure of the file
        Assert.Equal(1, exitCode);
        Assert.Equal("a.md:1:1: file-format-error Formatting failed: InvalidOperationException", Assert.Single(lines));
    }

    /// <summary>
    ///     A lint rule that always throws.
    /// </summary>
    private sealed class ThrowingRule : DemaConsulting.LintMark.Linting.ILintRule
    {
        /// <summary>
        ///     Gets the identifiers the rule reports.
        /// </summary>
        public System.Collections.Immutable.ImmutableArray<string> RuleIds { get; } = ["table-too-wide"];

        /// <summary>
        ///     Throws.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>Never returns.</returns>
        public System.Collections.Immutable.ImmutableArray<DemaConsulting.LintMark.Linting.Diagnostic> Check(
            DemaConsulting.LintMark.Document.ParsedDocument document,
            DemaConsulting.LintMark.Configuration.LintMarkConfig config) =>
            throw new InvalidOperationException("broken");
    }
}
