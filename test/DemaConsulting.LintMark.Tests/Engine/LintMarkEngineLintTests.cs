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
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the lint-only rules that the LintMarkEngine class runs by default: the findings they report, that they
///     never change a file or stop a rewrite, the command line options that switch rules on and off, the configuration, and
///     the suppression comments.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     A canonical document (no formatting finding) that triggers several lint-only rules.
    /// </summary>
    private const string LintOnlyText =
        "# One\n\n### Three\n\nsee *text * here and [ x ](u)\n\n```\nno language\n```\n";

    /// <summary>
    ///     Test that checking reports each lint-only finding with its rule id and position, and writes nothing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckLintOnlyFindings_ReportsRuleAndPositionAndWritesNothing()
    {
        // Arrange: a canonical file with an old write time
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(LintOnlyText));

        // Act: run the default engine without options
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: one finding per rule at its position, the exit code is one, the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:3:1: heading-increment",
                "a.md:5:5: emphasis-space",
                "a.md:5:22: link-space",
                "a.md:7:1: code-fence-language"
            ],
            lines.Select(line => string.Join(' ', line.Split(' ').Take(2))));
        AssertUntouched(path, Utf8(LintOnlyText), writeTime);
    }

    /// <summary>
    ///     Test that lint-only findings never make the fix option change a canonical file or refuse it, and that they stay reported.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixLintOnlyFindingsOnly_LeavesTheFileAndKeepsTheFindings()
    {
        // Arrange: a canonical file with an old write time
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(LintOnlyText));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: not written, no refusal of any kind, the four findings remain
        AssertUntouched(path, Utf8(LintOnlyText), writeTime);
        Assert.Equal(1, exitCode);
        Assert.Equal(4, lines.Length);
        Assert.DoesNotContain(lines, line => line.Contains("file-", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a file with formatting and lint-only findings is rewritten for the formatting findings, the lint-only findings
    ///     remain, and a second run is stable.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixMixedFindings_FixesFormattingAndKeepsLintOnlyFindings()
    {
        // Arrange: trailing whitespace and a closed heading (formatting), a stray emphasis space and an empty link (lint-only)
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("# Title #\n\nsee *text * here [e]()  \n\n- item \n"));

        // Act: fix, then fix again
        var (firstExit, firstLines) = Execute(directory, new LintMarkEngine(1), "--fix");
        var afterFirst = File.ReadAllBytes(path);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        var (secondExit, secondLines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: formatting is fixed, only the lint-only findings are reported, the second run does not write
        Assert.Equal(Utf8("# Title\n\nsee *text * here [e]()\n\n- item\n"), afterFirst);
        Assert.Equal(1, firstExit);
        Assert.Equal(["emphasis-space", "link-empty"], firstLines.Select(line => line.Split(' ')[1]));
        AssertUntouched(path, afterFirst, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        Assert.Equal(1, secondExit);
        Assert.Equal(firstLines, secondLines);
    }

    /// <summary>
    ///     Test that after a rewrite the remaining findings are reported at the lines of the file as it was written, not at the
    ///     lines of the file as it was read, while a check run reports the lines of the file as it is.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixMovesLines_ReportsRemainingFindingsAtWrittenPositions()
    {
        // Arrange: blank lines and trailing spaces above an undefined reference, so that rewriting moves it up
        using var directory = new TemporaryDirectory();
        const string Original = "# Title   \n\n\n\n\nA paragraph.  \n\n\n\nSee [text][nope] here.\n";
        var path = directory.WriteBytes("a.md", Utf8(Original));

        // Act: check, then fix, then fix again
        var check = Execute(directory, new LintMarkEngine(1));
        var fix = Execute(directory, new LintMarkEngine(1), "--fix");
        var written = File.ReadAllText(path);
        var again = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the check reports the undefined reference at its line in the original, the fix at its line in the written file
        var writtenLine = Array.FindIndex(written.Split('\n'), line => line.Contains("[text][nope]", StringComparison.Ordinal)) + 1;
        var originalLine = Array.FindIndex(Original.Split('\n'), line => line.Contains("[text][nope]", StringComparison.Ordinal)) + 1;
        Assert.NotEqual(originalLine, writtenLine);
        Assert.Contains(check.Lines, line => line.StartsWith($"a.md:{originalLine}:", StringComparison.Ordinal) && line.Contains("link-reference-undefined", StringComparison.Ordinal));
        var reported = Assert.Single(fix.Lines, line => line.Contains("link-reference-undefined", StringComparison.Ordinal));
        Assert.StartsWith($"a.md:{writtenLine}:", reported, StringComparison.Ordinal);
        Assert.Equal(fix.Lines, again.Lines);
    }

    /// <summary>
    ///     Test that after a rewrite of a CRLF file the remaining findings are reported at the lines of the written file.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixCrlfFileMovesLines_ReportsRemainingFindingsAtWrittenPositions()
    {
        // Arrange: a CRLF file with extra blank lines above a reversed link
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\n\r\n\r\nSee (text)[https://example.com/] here.\r\n"));

        // Act: fix
        var (_, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the finding is on the line it has in the written file
        var writtenLine = Array.FindIndex(File.ReadAllText(path).Split("\r\n"), line => line.Contains("(text)[", StringComparison.Ordinal)) + 1;
        var reported = Assert.Single(lines, line => line.Contains("link-reversed", StringComparison.Ordinal));
        Assert.StartsWith($"a.md:{writtenLine}:", reported, StringComparison.Ordinal);
        Assert.Equal(3, writtenLine);
    }

    /// <summary>
    ///     Test that a lint-only finding does not prevent a rewrite, and a rewrite and lint-only findings are reported together.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_LintOnlyFindingsWithRewrite_ChangeIsProposed()
    {
        // Arrange: a file with a formatting finding and a lint-only finding
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("# Title #\n\n[e]()\n"));

        // Act: process the file without writing it
        var result = Assert.Single(new LintMarkEngine(1).Process([path]));

        // Assert: a change exists, and both kinds of findings are present
        Assert.NotNull(result.Change);
        Assert.Equal(["heading-style", "link-empty"], result.Diagnostics.Select(d => d.RuleId));
        Assert.Equal([DiagnosticKind.Formatting, DiagnosticKind.LintOnly], result.Diagnostics.Select(d => d.Kind));
    }

    /// <summary>
    ///     Test that the disable option switches a rule off by id or legacy number, and the enable option switches an optional rule on.
    /// </summary>
    [Theory]
    [InlineData("--disable", "emphasis-space", false)]
    [InlineData("--disable", "MD037", false)]
    [InlineData("--disable", "md037,link-space", false)]
    [InlineData("--enable", "descriptive-link-text", true)]
    [InlineData("--enable", "MD059", true)]
    public void LintMarkEngine_Run_EnableAndDisableOptions_SwitchRules(string option, string rules, bool expectsDescriptive)
    {
        // Arrange: a stray emphasis space and a link that says nothing about its target
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("a.md", Utf8("see *text * here and [click here](u)\n"));

        // Act: run with the option
        var (_, lines) = Execute(directory, new LintMarkEngine(1), option, rules);

        // Assert: the switched rule is reported or not as asked, the others are unchanged
        Assert.Equal(expectsDescriptive, lines.Any(line => line.Contains("descriptive-link-text", StringComparison.Ordinal)));
        Assert.Equal(option == "--enable", lines.Any(line => line.Contains("emphasis-space", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that an option wins over the configuration, in both directions, and that the options can be repeated.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_OptionsAndConfiguration_OptionsWin()
    {
        // Arrange: a configuration that switches one rule off and one on
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "rules:\n  emphasis-space: false\n  descriptive-link-text: true\n");
        directory.WriteBytes("a.md", Utf8("see *text * here and [click here](u)\n"));

        // Act: run with the configuration alone, then with options that turn both around
        var (_, byConfig) = Execute(directory, new LintMarkEngine(1));
        var (_, byOptions) = Execute(directory, new LintMarkEngine(1), "--enable", "MD037", "--disable", "descriptive-link-text");

        // Assert: the configuration decides without options, the options decide with them
        Assert.Equal(["descriptive-link-text"], byConfig.Select(line => line.Split(' ')[1]));
        Assert.Equal(["emphasis-space"], byOptions.Select(line => line.Split(' ')[1]));
    }

    /// <summary>
    ///     Test that a configuration switches lint-only rules off by id and by legacy number.
    /// </summary>
    [Theory]
    [InlineData("rules:\n  link-space: false\n")]
    [InlineData("rules:\n  MD039: false\n")]
    public void LintMarkEngine_Run_ConfigRulesOff_RuleIsNotReported(string config)
    {
        // Arrange: a file with spaces in link text
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", config);
        directory.WriteBytes("a.md", Utf8("a [ x ](u)\n"));

        // Act: run
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: nothing is reported
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that suppression comments that name rules by legacy number are honored for lint-only rules in all their forms.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_SuppressionCommentsByNumber_SuppressLintOnlyRules()
    {
        // Arrange: four rules, each suppressed in another way (region, next line, same line, file) and one finding left over
        using var directory = new TemporaryDirectory();
        directory.WriteBytes(
            "a.md",
            Utf8(
                "<!-- markdownlint-disable-file MD042 -->\n\n"
                + "<!-- markdownlint-disable MD037 -->\n\nsee *text * here\n\n<!-- markdownlint-enable MD037 -->\n\n"
                + "<!-- markdownlint-disable-next-line MD039 -->\n[ x ](u)\n\n"
                + "[e]() and [ y ](u) <!-- markdownlint-disable-line MD039 -->\n\n"
                + "[ z ](u)\n"));

        // Act: run
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: only the link-space finding outside every suppression remains
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:14:1: link-space ", Assert.Single(lines), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the lint-only rules still run on a document that is read without grid tables, and that it is not rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixInvalidGridTableDocumentWithLintFinding_ReportsBothAndKeepsTheFile()
    {
        // Arrange: a grid border that is not a table, trailing whitespace, and an empty link
        using var directory = new TemporaryDirectory();
        var original = Utf8("para  \n+---+---+\n[e]()\n");
        var path = directory.WriteBytes("a.md", original);

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: both findings are reported and the bytes are identical
        Assert.Equal(1, exitCode);
        Assert.Equal(["grid-table-invalid", "link-empty"], lines.Select(line => line.Split(' ')[1]));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that the findings of several files come out in the same order for any degree of parallelism.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_ManyFilesWithFindings_SameReportForAnyParallelism()
    {
        // Arrange: many files, each with findings of several rules
        using var directory = new TemporaryDirectory();
        for (var i = 0; i < 12; i++)
        {
            directory.WriteBytes($"f{i:00}.md", Utf8($"# T{i}\n\n### Skip\n\n[ x ](u) and [e]() and *a * b\n"));
        }

        // Act: run sequentially and in parallel
        var (_, sequential) = Execute(directory, new LintMarkEngine(1));
        var (_, parallel) = Execute(directory, new LintMarkEngine(6));

        // Assert: identical reports, four findings per file
        Assert.Equal(sequential, parallel);
        Assert.Equal(48, sequential.Length);
        Assert.Equal(sequential.Order(StringComparer.Ordinal).Select(line => line.Split(':')[0]), sequential.Select(line => line.Split(':')[0]));
    }
}
