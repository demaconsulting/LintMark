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
///     Unit tests for the whitespace and block formatting that the LintMarkEngine class applies by default.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     A small document that breaks several rules, with its line endings as LF.
    /// </summary>
    private const string DirtyText = "Title\n=====\ntext  \nmore \n- a\n- b\n\n\n\n~~~\ncode\n~~~\n";

    /// <summary>
    ///     The canonical form of <see cref="DirtyText"/>.
    /// </summary>
    private const string CleanText = "# Title\n\ntext  \nmore\n\n- a\n- b\n\n```\ncode\n```\n";

    /// <summary>
    ///     Test that the default engine rewrites whitespace and block structure with the fix option and reports nothing afterwards.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixDefaultFormatters_RewritesFileAndReportsNothing()
    {
        // Arrange: a file that breaks several rules
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8(DirtyText));

        // Act: run the default engine with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--fix");

        // Assert: the file is canonical, every finding was fixed, nothing is reported
        Assert.Equal(Utf8(CleanText), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the default engine reports every violation at its place without writing the file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckDefaultFormatters_ReportsEachViolationAndWritesNothing()
    {
        // Arrange: a file that breaks several rules, with an old write time
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(DirtyText));

        // Act: run the default engine without options
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: one finding per rule and line, exit code one, the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: heading-style",
                "a.md:3:1: heading-blank-lines",
                "a.md:4:5: trailing-whitespace",
                "a.md:5:1: list-blank-lines",
                "a.md:8:1: max-blank-lines",
                "a.md:10:1: code-fence",
                "a.md:12:1: code-fence"
            ],
            lines.Select(line => string.Join(' ', line.Split(' ').Take(2))));
        AssertUntouched(path, Utf8(DirtyText), writeTime);
    }

    /// <summary>
    ///     Test that the diff option shows the changes of the default engine without writing the file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_DiffDefaultFormatters_PrintsTheChangeAndWritesNothing()
    {
        // Arrange: a file with trailing whitespace and a closed heading
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title #\n\ntext \n"));

        // Act: run with the diff option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--diff");

        // Assert: the findings, then a unified diff of both changes; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:8: heading-style ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("a.md:3:5: trailing-whitespace ", lines[1], StringComparison.Ordinal);
        Assert.Equal(["--- a/a.md", "+++ b/a.md", "@@ -1,3 +1,3 @@", "-# Title #", "+# Title", " ", "-text ", "+text"], lines[2..]);
        AssertUntouched(path, Utf8("# Title #\n\ntext \n"), writeTime);
    }

    /// <summary>
    ///     Test that a second fix run over a file that the first run rewrote leaves it alone.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixDefaultFormattersTwice_SecondRunLeavesTheFileAlone()
    {
        // Arrange: a file rewritten by a first run, with an old write time afterwards
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8(DirtyText));
        Execute(directory, new LintMarkEngine(1), "--fix");
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        // Act: run again
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file is not written and nothing is reported
        AssertUntouched(path, Utf8(CleanText), new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a rule that a comment disables by its legacy number is neither fixed nor reported.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixRuleSuppressedByAlias_LeavesTheViolation()
    {
        // Arrange: trailing whitespace inside a region that disables rule MD009 and a closed heading outside it
        using var directory = new TemporaryDirectory();
        const string text = "<!-- markdownlint-disable MD009 -->\n\ntext \nmore\n\n<!-- markdownlint-enable MD009 -->\n\n# Title #\n";
        var path = directory.WriteBytes("a.md", Utf8(text));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: only the heading changed
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        Assert.Equal(Utf8(text.Replace("# Title #", "# Title", StringComparison.Ordinal)), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that the settings of a configuration file select the fence, break, blank line maximum and final newline.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixConfiguredSettings_WritesTheConfiguredForm()
    {
        // Arrange: a configuration with tildes, asterisks, two blank lines and no final newline
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "code-fence: tilde\nthematic-break: asterisks\nmax-blank-lines: 2\nfinal-newline: false\n");
        var path = directory.WriteBytes("a.md", Utf8("text\n\n\n\n```\ncode\n```\n\n---\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file follows every setting
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        Assert.Equal(Utf8("text\n\n\n~~~\ncode\n~~~\n\n***"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that the default formatters rewrite lists, quotes and inline text, and report each under its own rule.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckListQuoteInlineViolations_ReportsEachRule()
    {
        // Arrange: a file with a bullet, a numbered list, a quote, emphasis and a bare URL that break their rules
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("* a\n\n1. b\n1. c\n\n>  d\n\n_e_ http://x.com\n"));

        // Act: run with the check option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--check");

        // Assert: each rule is reported and the file is untouched
        Assert.Equal(1, exitCode);
        foreach (var rule in new[] { "bullet", "ordered-style", "blockquote-space", "emphasis", "bare-urls" })
        {
            Assert.Contains(lines, line => line.Contains(rule, StringComparison.Ordinal));
        }

        Assert.Equal(Utf8("* a\n\n1. b\n1. c\n\n>  d\n\n_e_ http://x.com\n"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a configuration cannot switch off an always-applied formatting rule, by id or by its legacy number.
    /// </summary>
    [Theory]
    [InlineData("trailing-whitespace")]
    [InlineData("MD009")]
    [InlineData("heading-style")]
    public void LintMarkEngine_Run_ConfigNamesAlwaysAppliedRule_ReportsUnknownRule(string rule)
    {
        // Arrange: a configuration that tries to switch the rule off
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", $"rules:\n  {rule}: false\n");
        directory.WriteBytes("a.md", Utf8("text\n"));

        // Act: run
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: the rule is unknown in the configuration
        Assert.Equal(1, exitCode);
        Assert.Contains(lines, line => line.Contains("config-error", StringComparison.Ordinal) && line.Contains(rule, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that processing a file with the default engine proposes a change with the formatted bytes and the findings.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_DefaultFormatters_ChangeHasFormattedBytesAndFindings()
    {
        // Arrange: a file that breaks several rules
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8(DirtyText));

        // Act: process the file without writing it
        var result = Assert.Single(new LintMarkEngine(1).Process([path]));

        // Assert: the change holds the canonical text, and every finding is a formatting finding
        Assert.NotNull(result.Change);
        Assert.Equal(CleanText, result.Change.NewLf);
        Assert.Equal(7, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(DemaConsulting.LintMark.Linting.DiagnosticKind.Formatting, diagnostic.Kind));
    }

    /// <summary>
    ///     Test that a rewrite that the safety checks refuse reports the refusal and leaves the file, also for the default formatters.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixInvalidGridTableDocument_IsNeverRewrittenByTheDefaultFormatters()
    {
        // Arrange: trailing whitespace next to a grid table border that is not part of a valid table
        using var directory = new TemporaryDirectory();
        var original = Utf8("para  \n+---+---+\nmore\t\n");
        var path = directory.WriteBytes("a.md", original);

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the grid finding is reported and the bytes are identical
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:2:1: grid-table-invalid ", Assert.Single(lines), StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(path));
    }
}
