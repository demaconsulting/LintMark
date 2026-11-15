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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level tests of file selection, configuration selection, rule names, suppression comments and
///     file problems, which run the LintMark application via dotnet on real files.
/// </summary>
/// <remarks>
///     Only relative paths are compared (never the resolved temporary directory), because the temporary directory
///     may be reached through a link (<c>/var</c> and <c>/private/var</c> on macOS).
/// </remarks>
[Collection("Sequential")]
public class IntegrationOptionTests
{
    /// <summary>
    ///     A line that the <c>emphasis-space</c> rule reports at column 5.
    /// </summary>
    private const string EmphasisSpaceLine = "see *text * here\n";

    /// <summary>
    ///     The path of the application DLL.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationOptionTests"/> class by locating the LintMark DLL.
    /// </summary>
    public IntegrationOptionTests()
    {
        _dllPath = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.LintMark.dll");
        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that a run without paths prints the banner and processes every Markdown file under the working
    ///     directory except those under the folders that the default ignore patterns exclude.
    /// </summary>
    [Fact]
    public void LintMark_DefaultBehavior_NoPaths_ProcessesMarkdownUnderWorkingDirectory()
    {
        // Arrange: a heading without a space in the root, in a folder, in each default-ignored folder, and a text file
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "#Bad\n");
        directory.WriteFile("docs/b.md", "#Bad\n");
        directory.WriteFile("node_modules/pkg/n.md", "#Bad\n");
        directory.WriteFile(".git/g.md", "#Bad\n");
        directory.WriteFile("tools/.venv/lib/v.md", "#Bad\n");
        directory.WriteFile("docs/generated/gen.md", "#Bad\n");
        directory.WriteFile("vendor/thirdparty/t.md", "#Bad\n");
        directory.WriteFile(".agent-logs/log.md", "#Bad\n");
        directory.WriteFile("notes.txt", "#Bad\n");

        // Act: run the tool in the directory without paths
        var exitCode = Runner.Run(directory.FullPath, out var output, "dotnet", _dllPath);

        // Assert: the banner comes first, the two files are reported and nothing else is
        Assert.Equal(1, exitCode);
        Assert.StartsWith("LintMark version", output, StringComparison.Ordinal);
        Assert.Contains("Copyright", output);
        Assert.Contains("a.md:1:1: heading-missing-space", output);
        Assert.Contains("docs/b.md:1:1: heading-missing-space", output);
        Assert.DoesNotContain("node_modules", output);
        Assert.DoesNotContain("g.md", output);
        Assert.DoesNotContain("v.md", output);
        Assert.DoesNotContain("gen.md", output);
        Assert.DoesNotContain("t.md", output);
        Assert.DoesNotContain("log.md", output);
        Assert.DoesNotContain("notes.txt", output);
    }

    /// <summary>
    ///     Test that a directory, a glob with an exclusion and an explicit file select the files they name, each once.
    /// </summary>
    [Fact]
    public void LintMark_SelectFiles_DirectoryGlobAndExclusion_SelectEachFileOnce()
    {
        // Arrange: four files that are all reported, one of them in node_modules
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "#Bad\n");
        directory.WriteFile("docs/b.md", "#Bad\n");
        directory.WriteFile("docs/sub/c.md", "#Bad\n");
        directory.WriteFile("node_modules/pkg/n.md", "#Bad\n");

        // Act: select by directory, by glob with an exclusion, by file and directory together, and by file inside node_modules
        var byDirectory = Runner.Run(directory.FullPath, out var directoryOutput, "dotnet", _dllPath, "docs");
        var byGlob = Runner.Run(directory.FullPath, out var globOutput, "dotnet", _dllPath, "**/*.md", "!docs/sub/**");
        var byBoth = Runner.Run(directory.FullPath, out var bothOutput, "dotnet", _dllPath, "docs/b.md", "docs");
        var explicitFile = Runner.Run(directory.FullPath, out var explicitOutput, "dotnet", _dllPath, "node_modules/pkg/n.md");

        // Assert: the directory selects its files only, the exclusion removes one file, a file named twice is reported once
        Assert.Equal(1, byDirectory);
        Assert.DoesNotContain("a.md:1:1", directoryOutput);
        Assert.Contains("docs/b.md:1:1", directoryOutput);
        Assert.Contains("docs/sub/c.md:1:1", directoryOutput);
        Assert.Equal(1, byGlob);
        Assert.Contains("a.md:1:1", globOutput);
        Assert.Contains("docs/b.md:1:1", globOutput);
        Assert.DoesNotContain("docs/sub/c.md", globOutput);
        Assert.Equal(1, byBoth);
        Assert.Equal(1, CountOccurrences(bothOutput, "docs/b.md:1:1"));
        Assert.Equal(1, explicitFile);
        Assert.Contains("node_modules/pkg/n.md:1:1", explicitOutput);
    }

    /// <summary>
    ///     Test that a link back into a folder the run is already inside does not abort the run.
    /// </summary>
    [Fact]
    public void LintMark_SelectFiles_DirectoryLinkCycle_ChecksEachFileOnceAndExitsNormally()
    {
        // Arrange: a file with a finding and a link in its folder that points at the working directory
        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/b.md", "#Bad\n");
        Assert.SkipUnless(directory.TryCreateDirectoryLink("docs/loop", directory.FullPath), "The system did not allow a directory link to be created.");

        // Act: run without paths, which searches the working directory
        var exitCode = Runner.Run(directory.FullPath, out var output, "dotnet", _dllPath);

        // Assert: the file is reported once, the cycle is not an error, and the status is the one for a finding
        Assert.Equal(1, exitCode);
        Assert.Equal(1, CountOccurrences(output, "b.md:1:1: heading-missing-space"));
        Assert.DoesNotContain("Unexpected error", output);
        Assert.DoesNotContain("loop", output);
    }

    /// <summary>
    ///     Test that an explicit configuration file replaces the discovery of the configuration file beside the Markdown file.
    /// </summary>
    [Fact]
    public void LintMark_ExplicitConfig_ConfigFileGiven_ReplacesDiscovery()
    {
        // Arrange: a discovered configuration with a limit of 20, another file with a limit of 30, and a line of 49 columns
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        directory.WriteFile("other.yaml", "line-length: 30\n");
        var file = directory.WriteFile("a.md", "ant bat cat dog eel fox gnu hen ibis jay kit lamb\n");

        // Act: check with discovery, then with the explicit file, and with a file that does not exist
        Runner.Run(out var discovered, "dotnet", _dllPath, "--check", file);
        Runner.Run(out var explicitOutput, "dotnet", _dllPath, "--check", "--config", directory.PathOf("other.yaml"), file);
        var missingExit = Runner.Run(out var missingOutput, "dotnet", _dllPath, "--config", directory.PathOf("nope.yaml"), file);

        // Assert: the limit comes from the file that was chosen; a missing configuration file is a reported error
        Assert.Contains("the limit is 20", discovered);
        Assert.Contains("the limit is 30", explicitOutput);
        Assert.Equal(1, missingExit);
        Assert.Contains("nope.yaml:1:1: config-error Configuration file not found", missingOutput);
    }

    /// <summary>
    ///     Test that the no-config option uses the defaults and ignores both the configuration file and the editor settings.
    /// </summary>
    [Fact]
    public void LintMark_ExplicitConfig_NoConfigGiven_UsesDefaultsAndIgnoresEditorConfig()
    {
        // Arrange: a configuration with a limit of 20, editor settings that ask for CRLF, and an LF file of 49 columns
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = crlf\n");
        var file = directory.WriteFile("a.md", "ant bat cat dog eel fox gnu hen ibis jay kit lamb\n");

        // Act: check with the configuration sources, then without them, and combine the option with an explicit file
        var withSources = Runner.Run(out var sourcesOutput, "dotnet", _dllPath, "--check", file);
        var withoutSources = Runner.Run(out var noConfigOutput, "dotnet", _dllPath, "--check", "--no-config", file);
        var combined = Runner.Run(out var combinedOutput, "dotnet", _dllPath, "--no-config", "--config", directory.PathOf(".lintmark.yaml"), file);

        // Assert: the sources are used without the option and ignored with it; the combination is a usage error
        Assert.Equal(1, withSources);
        Assert.Contains("line-ending Line endings are LF; expected CRLF", sourcesOutput);
        Assert.Contains("line-length", sourcesOutput);
        Assert.Equal(0, withoutSources);
        Assert.DoesNotContain("a.md:", noConfigOutput);
        Assert.Equal(1, combined);
        Assert.Contains("--config cannot be combined with --no-config", combinedOutput);
    }

    /// <summary>
    ///     Test that the style settings change the written Markdown to the value they name.
    /// </summary>
    [Fact]
    public void LintMark_ConfigSettings_StyleValues_ChangeTheFormattedOutput()
    {
        // Arrange: a document written in the default style and a configuration that asks for the other value of every style
        using var directory = new TemporaryDirectory();
        directory.WriteFile(
            ".lintmark.yaml",
            "bullet: asterisk\nordered-style: one\nthematic-break: asterisks\nemphasis: underscore\nstrong: underscore\ncode-fence: tilde\ntables: aligned\n");
        var file = directory.WriteFile(
            "a.md",
            "- a\n- b\n\n1. x\n2. y\n\n---\n\n*em* and **st**\n\n```text\nc\n```\n\n| a | b |\n| --- | --- |\n| longer | x |\n");

        // Act: fix the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: every style is written as configured and nothing is left to report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("a.md:", output);
        Assert.Equal(
            "* a\n* b\n\n1. x\n1. y\n\n***\n\n_em_ and __st__\n\n~~~text\nc\n~~~\n\n| a      | b   |\n| ------ | --- |\n| longer | x   |\n",
            File.ReadAllText(file));
    }

    /// <summary>
    ///     Test that the numeric and switch settings change what is kept: the blank lines, the final line ending and the line length.
    /// </summary>
    [Fact]
    public void LintMark_ConfigSettings_NumericAndSwitchValues_ChangeWhatIsKept()
    {
        // Arrange: three blank lines, no final line ending and a line of 181 columns, with a configuration that allows
        // two blank lines, asks for no final line ending and an unlimited line length
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "max-blank-lines: 2\nfinal-newline: false\nline-length: 0\n");
        var longLine = string.Join(' ', Enumerable.Repeat("word", 36));
        var file = directory.WriteFile("a.md", $"a\n\n\n\nb\n\n{longLine}\n");

        // Act: fix the file and check the defaults on the same text
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);
        var defaults = Runner.Run(out var defaultOutput, "dotnet", _dllPath, "--check", "--no-config", file);

        // Assert: two blank lines are kept, the long line is not wrapped and the final line ending is gone
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("a.md:", output);
        Assert.Equal($"a\n\n\nb\n\n{longLine}", File.ReadAllText(file));
        Assert.Equal(1, defaults);
        Assert.Contains("final-newline", defaultOutput);
    }

    /// <summary>
    ///     Test that rule numbers are accepted wherever a rule id is, that the number of a dropped rule is accepted and
    ///     ignored, and that findings always use the rule id.
    /// </summary>
    [Fact]
    public void LintMark_RuleAliases_MdNumbersInConfigAndComments_AreAcceptedAndFindingsUseIds()
    {
        // Arrange: the same text in three folders with their own configuration: none, the number of the rule and a dropped number
        using var plain = new TemporaryDirectory();
        var plainFile = plain.WriteFile("a.md", EmphasisSpaceLine);
        using var switched = new TemporaryDirectory();
        switched.WriteFile(".lintmark.yaml", "rules:\n  MD037: false\n  MD033: false\n");
        var switchedFile = switched.WriteFile("a.md", EmphasisSpaceLine);
        using var unknown = new TemporaryDirectory();
        unknown.WriteFile(".lintmark.yaml", "rules:\n  MD999: false\n");
        var unknownFile = unknown.WriteFile("a.md", EmphasisSpaceLine);

        // Act: check each file
        var plainExit = Runner.Run(out var plainOutput, "dotnet", _dllPath, plainFile);
        var switchedExit = Runner.Run(out var switchedOutput, "dotnet", _dllPath, switchedFile);
        var unknownExit = Runner.Run(out var unknownOutput, "dotnet", _dllPath, unknownFile);

        // Assert: the finding names the id and not the number; the number switches the rule off; a dropped number is
        // accepted; a number that never existed is reported
        Assert.Equal(1, plainExit);
        Assert.Contains("a.md:1:5: emphasis-space", plainOutput);
        Assert.DoesNotContain("MD037", plainOutput);
        Assert.Equal(0, switchedExit);
        Assert.DoesNotContain("a.md:", switchedOutput);
        Assert.Equal(1, unknownExit);
        Assert.Contains(".lintmark.yaml:2:3: config-error Unknown rule 'MD999'", unknownOutput);
    }

    /// <summary>
    ///     Test that a formatting rule disabled by a comment leaves the passage that follows untouched until it is enabled again.
    /// </summary>
    [Fact]
    public void LintMark_SuppressionComments_DisabledFormattingRule_LeavesPassageAlone()
    {
        // Arrange: trailing whitespace before, inside and after a region in which the rule is disabled
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile(
            "a.md",
            "text  \n<!-- lintmark-disable trailing-whitespace -->\ntext2  \n<!-- lintmark-enable trailing-whitespace -->\ntext3  \n");

        // Act: fix the file
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: only the whitespace inside the region is kept
        Assert.Equal(0, exitCode);
        Assert.Equal(
            "text\n<!-- lintmark-disable trailing-whitespace -->\ntext2  \n<!-- lintmark-enable trailing-whitespace -->\ntext3\n",
            File.ReadAllText(file));
    }

    /// <summary>
    ///     Test that a lint-only rule disabled by a comment in the markdownlint form, with the rule number, reports nothing in the region.
    /// </summary>
    [Fact]
    public void LintMark_SuppressionComments_DisabledLintRuleByNumber_ReportsNothingInTheRegion()
    {
        // Arrange: the same finding before, inside and after a region disabled with the markdownlint comment
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile(
            "a.md",
            $"{EmphasisSpaceLine}\n<!-- markdownlint-disable MD037 -->\n{EmphasisSpaceLine}<!-- markdownlint-enable MD037 -->\n\n{EmphasisSpaceLine}");

        // Act: check the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--check", file);

        // Assert: the first and last lines are reported and the one inside the region is not
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:5: emphasis-space", output);
        Assert.DoesNotContain("a.md:4:5", output);
        Assert.Contains("a.md:7:5: emphasis-space", output);
    }

    /// <summary>
    ///     Test that the fix option edits nothing for lint-only findings and reports them at their positions in the written file.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_LintOnlyFindings_AreKeptAndReportedAtWrittenPositions()
    {
        // Arrange: extra blank lines and trailing whitespace before three lint-only findings
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md", "# T\n\n\n\ntext  \n\n[x](#nowhere)\n\n###### H\n\n![](i.png)\n");

        // Act: fix the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the formatting is fixed, the lint-only content is as written and the findings use the new line numbers
        Assert.Equal(1, exitCode);
        Assert.Equal("# T\n\ntext\n\n[x](#nowhere)\n\n###### H\n\n![](i.png)\n", File.ReadAllText(file));
        Assert.Contains("a.md:5:1: link-fragment", output);
        Assert.Contains("a.md:7:1: heading-increment", output);
        Assert.Contains("a.md:9:1: image-alt-text", output);
    }

    /// <summary>
    ///     Test that a file that cannot be read, decoded or split into lines is reported and does not stop the run.
    /// </summary>
    [Fact]
    public void LintMark_FileProblems_UnreadableFiles_AreReportedWithoutStoppingTheRun()
    {
        // Arrange: a UTF-16 file, a file with a bare carriage return, a file that does not exist and a file that has a finding
        using var directory = new TemporaryDirectory();
        var utf16 = directory.WriteBytes("utf16.md", [0xFF, 0xFE, 0x41]);
        var bareCr = directory.WriteBytes("cr.md", "line one\rline two\n"u8.ToArray());
        var missing = directory.PathOf("missing.md");
        var reported = directory.WriteFile("ok.md", EmphasisSpaceLine);

        // Act: check all four files
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--check", utf16, bareCr, missing, reported);

        // Assert: one problem per file with its position, and the readable file was still checked
        Assert.Equal(1, exitCode);
        Assert.Contains("utf16.md:1:1: file-encoding-error Unsupported byte order mark", output);
        Assert.Contains("cr.md:1:9: file-line-ending-error Bare carriage return", output);
        Assert.Contains("missing.md:1:1: file-read-error File not found", output);
        Assert.Contains("ok.md:1:5: emphasis-space", output);
    }

    /// <summary>
    ///     Test that a file whose emphasis is nested far too deeply is reported and does not crash the process.
    /// </summary>
    [Fact]
    public void LintMark_FileProblems_DeeplyNestedEmphasis_IsReportedWithoutStoppingTheRun()
    {
        // Arrange: a word wrapped in ten thousand asterisks on each side, which a recursive walk cannot survive, and a file with a finding
        using var directory = new TemporaryDirectory();
        var nested = directory.WriteFile("nested.md", new string('*', 10_000) + "a" + new string('*', 10_000) + "\n");
        var reported = directory.WriteFile("ok.md", EmphasisSpaceLine);

        // Act: check both files in a separate process, so that a stack overflow would end that process and not the test host
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--check", nested, reported);

        // Assert: the nested file is a parse problem, the other file was still checked, and the run ended normally with status 1
        Assert.Equal(1, exitCode);
        Assert.Contains("nested.md:1:1: file-parse-error", output);
        Assert.Contains("ok.md:1:5: emphasis-space", output);
        Assert.DoesNotContain("Stack overflow", output);
    }

    /// <summary>
    ///     Counts the non-overlapping occurrences of a text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="value">The text to count.</param>
    /// <returns>The number of occurrences.</returns>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
