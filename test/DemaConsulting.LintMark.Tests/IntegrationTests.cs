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

using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Testing;
using DemaConsulting.LintMark.Cli;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level integration tests that run the LintMark application via dotnet.
/// </summary>
[Collection("Sequential")]
public class IntegrationTests
{
    private readonly string _dllPath;

    /// <summary>
    ///     The folder of the input files that help examples read, with one subfolder per card id.
    /// </summary>
    private readonly string _helpExamplesPath = Path.Combine(AppContext.BaseDirectory, "TestData", "HelpExamples");

    /// <summary>
    ///     Initialize test by locating the LintMark DLL.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the main project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = Path.Combine(baseDir, "DemaConsulting.LintMark.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void LintMark_VersionFlag_Provided_OutputsVersion()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with version flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag outputs usage information.
    /// </summary>
    [Fact]
    public void LintMark_HelpFlag_Provided_OutputsUsageInformation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--help");

        // Assert: usage text contains required sections and key flags
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs summary.
    /// </summary>
    [Fact]
    public void LintMark_ValidateFlag_Provided_RunsValidation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate");

        // Assert: validation summary is present; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
    }

    /// <summary>
    ///     Test that the depth option sets the level of the heading that opens the self-validation summary.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithDepthFlag_Provided_HeadsSummaryAtThatDepth()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run self-validation with a heading depth of two
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate",
            "--depth",
            "2");

        // Assert: the summary opens with a second-level heading and no first-level one
        Assert.Equal(0, exitCode);
        Assert.Matches(@"(?m)^## DEMA Consulting LintMark\r?$", output);
        Assert.DoesNotMatch(@"(?m)^# DEMA Consulting LintMark", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void LintMark_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void LintMark_SilentFlag_Provided_SuppressesOutput()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version",
            "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    [Fact]
    public void LintMark_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--log",
                logFile);

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("LintMark version", logContent);
            Assert.Contains("LintMark version", output);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unknown argument causes an error message and non-zero exit code.
    /// </summary>
    [Fact]
    public void LintMark_UnknownArgument_Provided_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with an unknown argument
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized flag
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that a valid Markdown file given as a path is processed successfully.
    /// </summary>
    [Fact]
    public void LintMark_PathsProvided_ValidMarkdown_ReturnsSuccess()
    {
        // Arrange: a valid Markdown file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.md", "# Title\n\nSome text.\n\n- a\n- b\n");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: success and no diagnostics
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("file-", output);
    }

    /// <summary>
    ///     Test that a missing file is reported as a read error.
    /// </summary>
    [Fact]
    public void LintMark_PathsProvided_MissingFile_ReportsReadErrorAndReturnsError()
    {
        // Arrange: a path that does not exist
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("missing.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: error exit code and a read-error diagnostic
        Assert.Equal(1, exitCode);
        Assert.Contains("missing.md:1:1: file-read-error File not found", output);
    }

    /// <summary>
    ///     Test that the check flag lints without modifying the file.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_Provided_LintsWithoutWriting()
    {
        // Arrange: a valid file with CRLF endings
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("ok.md", "# Title\r\n\r\ntext\r\n"u8.ToArray());
        var before = File.ReadAllBytes(file);
        var timestamp = File.GetLastWriteTimeUtc(file);

        // Act: run the tool with --check
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--check", file);

        // Assert: success and the file is byte-identical with the same timestamp
        Assert.Equal(0, exitCode);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(file));
    }

    /// <summary>
    ///     Test that the nearest configuration is used and its problems are reported with their position.
    /// </summary>
    [Fact]
    public void LintMark_ConfigDiscovery_NearestConfigInvalid_ReportsItsErrors()
    {
        // Arrange: a valid configuration in the root and an invalid one nearer the file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 100\n");
        directory.WriteFile("docs/.lintmark.yaml", "line-length: wide\n");
        var file = directory.WriteFile("docs/a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: the nearer file's problem is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("docs/.lintmark.yaml:1:14: config-error Invalid value 'wide' for setting 'line-length'", output);
    }

    /// <summary>
    ///     Test that an invalid value is rejected with the allowed values.
    /// </summary>
    [Fact]
    public void LintMark_ConfigValidation_InvalidValue_ReportsErrorAndReturnsError()
    {
        // Arrange: a configuration with an invalid value
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bullet: star\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: reported at the value, naming the allowed values
        Assert.Equal(1, exitCode);
        Assert.Contains(".lintmark.yaml:1:9: config-error Invalid value 'star' for setting 'bullet'; expected one of: dash, asterisk, plus", output);
    }

    /// <summary>
    ///     Test that configuration problems are reported in the documented one-line form.
    /// </summary>
    [Fact]
    public void LintMark_ConfigErrors_Reported_AsPathLineColRuleMessage()
    {
        // Arrange: a configuration with two problems
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "color: red\nwrap: nope\n");
        var file = directory.WriteFile("a.md");

        // Act: run the tool with the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, file);

        // Assert: one line per problem, in the documented form
        Assert.Equal(1, exitCode);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains("config-error", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, lines.Count);
        Assert.Matches(@"\.lintmark\.yaml:1:1: config-error Unknown setting 'color'$", lines[0]);
        Assert.Matches(@"\.lintmark\.yaml:2:7: config-error Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence, semantic$", lines[1]);
    }

    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => new System.Text.UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Test that the fix option rewrites a CRLF file with LF line endings when the configuration asks for them.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CrlfFile_RewritesToLf()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the file has LF line endings and nothing is reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-ending", output);
        Assert.Equal(Bytes("# Title\n\nText.\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a second run of the fix option reports nothing and changes nothing.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_Twice_SecondRunReportsNothing()
    {
        // Arrange: a mixed file, fixed by a first run
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));
        Runner.Run(out _, "dotnet", _dllPath, "--fix", file);
        var afterFirst = File.ReadAllBytes(file);

        // Act: run again
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: success, no report, same bytes
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-ending", output);
        Assert.Equal(afterFirst, File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the fix option cannot be combined with the check option.
    /// </summary>
    [Fact]
    public void LintMark_FixWithCheck_Combined_ReturnsError()
    {
        // Arrange: a file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("a.md");

        // Act: run the tool with both options
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", "--check", file);

        // Assert: a usage error
        Assert.Equal(1, exitCode);
        Assert.Contains("--fix cannot be combined with --check or --diff", output);
    }

    /// <summary>
    ///     Test that the line ending of the configuration wins and is written by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_LineEnding_ConfigWins_FixWritesCrlf()
    {
        // Arrange: an LF file, a configuration that asks for CRLF and editor settings that ask for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: crlf\n");
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\n\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the configuration's ending is written
        Assert.Equal(0, exitCode);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the fix option keeps the byte order mark and all text it does not change.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_BomFile_KeepsBomAndText()
    {
        // Arrange: a CRLF file with a byte order mark and non-ASCII text
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", [0xEF, 0xBB, 0xBF, .. Bytes("# 日本語\r\n\r\n日本語 text\r\n")]);

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the mark and the text are intact; only the line endings changed
        Assert.Equal(0, exitCode);
        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. Bytes("# 日本語\n\n日本語 text\n")], File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a rewrite leaves no temporary file behind.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_Rewrite_LeavesNoTemporaryFiles()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the directory holds the same files as before
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [".lintmark.yaml", "a.md"],
            Directory.GetFiles(directory.FullPath).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that a document with an invalid grid table is never rewritten by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_GridTableFallback_LeavesFileUntouched()
    {
        // Arrange: a CRLF document with an invalid grid table and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var file = directory.WriteBytes("a.md", Bytes("para\r\n+---+---+\r\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: reported, exit code one, bytes identical
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:2:1: grid-table-invalid", output);
        Assert.Equal(Bytes("para\r\n+---+---+\r\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a file with an invalid configuration is never rewritten by the fix option.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_InvalidConfig_LeavesFileUntouched()
    {
        // Arrange: an invalid configuration and a file with mixed line endings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\n"));

        // Act: run the tool with the fix option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the configuration problem is reported and the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Contains("config-error Unknown setting 'bogus'", output);
        Assert.Equal(Bytes("# Title\r\n\r\nText.\n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the files of a glob that the ignore patterns match are skipped.
    /// </summary>
    [Fact]
    public void LintMark_IgnorePatterns_GlobSelected_SkipsMatches()
    {
        // Arrange: a configuration that ignores a folder, a broken file in it and a broken file outside it
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - \"drafts/**\"\n");
        directory.WriteBytes("drafts/bad.md", [0xFF]);
        directory.WriteBytes("kept/bad.md", [0xFF]);

        // Act: run the tool with a glob
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, Path.Combine(directory.FullPath, "**", "*.md"));

        // Assert: only the file that is not ignored is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("kept/bad.md:1:1: file-encoding-error", output);
        Assert.DoesNotContain("drafts/bad.md", output);
    }

    /// <summary>
    ///     Test that the disable option switches a lint-only rule off end to end, and the enable option an optional rule on.
    /// </summary>
    [Fact]
    public void LintMark_EnableAndDisableFlags_Provided_SwitchLintOnlyRules()
    {
        // Arrange: a stray emphasis space and a link text that says nothing
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("see *text * here and [click here](u)\n"));

        // Act: run with the defaults, with the rule switched off by number, and with the optional rule switched on
        var byDefault = Runner.Run(out var defaultOutput, "dotnet", _dllPath, file);
        var disabled = Runner.Run(out var disabledOutput, "dotnet", _dllPath, "--disable", "MD037", file);
        var enabled = Runner.Run(out var enabledOutput, "dotnet", _dllPath, "--enable", "descriptive-link-text", file);

        // Assert: the default reports one rule, disabling it leaves a clean run, enabling adds the other
        Assert.Equal(1, byDefault);
        Assert.Contains("a.md:1:5: emphasis-space", defaultOutput);
        Assert.DoesNotContain("descriptive-link-text", defaultOutput);
        Assert.Equal(0, disabled);
        Assert.DoesNotContain("emphasis-space", disabledOutput);
        Assert.Equal(1, enabled);
        Assert.Contains("descriptive-link-text", enabledOutput);
    }

    /// <summary>
    ///     Test that a rule list that names an unknown rule is a usage error and nothing is processed.
    /// </summary>
    [Fact]
    public void LintMark_EnableFlag_UnknownRule_IsAUsageError()
    {
        // Arrange: a file that would be reported
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("see *text * here\n"));

        // Act: run with an unknown rule
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--enable", "no-such-rule", file);

        // Assert: the error names the option and the rule, and no finding is reported
        Assert.Equal(1, exitCode);
        Assert.Contains("--enable names an unknown rule 'no-such-rule'", output);
        Assert.DoesNotContain("emphasis-space", output);
    }

    /// <summary>
    ///     Test that the diff option shows what the fix option would change and writes nothing.
    /// </summary>
    [Fact]
    public void LintMark_DiffFlag_FormattingViolations_ShowsTheChange()
    {
        // Arrange: a closed heading and trailing whitespace
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("# Title #\n\ntext \n"));

        // Act: run the tool with the diff option
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--diff", file);

        // Assert: the removed and added lines are shown, the file is unchanged
        Assert.Equal(1, exitCode);
        Assert.Contains("-# Title #", output);
        Assert.Contains("+# Title", output);
        Assert.Contains("-text ", output);
        Assert.Contains("+text", output);
        Assert.Equal(Bytes("# Title #\n\ntext \n"), File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that a second fix run after the first one reports nothing and changes nothing.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_FormattingRules_SecondRunReportsNothing()
    {
        // Arrange: a document that breaks many rules, fixed by a first run
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("a.md", Bytes("Title\n=====\ntext  \n- a\n\n\n\n~~~text\ncode\n~~~\n***\n"));
        Runner.Run(out _, "dotnet", _dllPath, "--fix", file);
        var afterFirst = File.ReadAllBytes(file);

        // Act: run the check option on the result, then the fix option again
        var checkExit = Runner.Run(out var checkOutput, "dotnet", _dllPath, "--check", file);
        var fixExit = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: nothing is reported and the bytes stay the same
        Assert.Equal(0, checkExit);
        Assert.DoesNotContain("a.md:", checkOutput);
        Assert.Equal(0, fixExit);
        Assert.Equal(afterFirst, File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that editor settings that cannot be read are reported at the Markdown file.
    /// </summary>
    [Fact]
    public void LintMark_EditorConfig_UnreadableSettings_ReportedAtMarkdownFile()
    {
        // Arrange: editor settings for Markdown files and a CRLF Markdown file
        using var directory = new TemporaryDirectory();
        var settings = directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n");
        var file = directory.WriteBytes("a.md", Bytes("# Title\r\n"));

        // Arrange: make the settings unreadable, by locking them on Windows and by removing every permission elsewhere
        FileStream? hold = null;
        if (OperatingSystem.IsWindows())
        {
            hold = new FileStream(settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        else
        {
            File.SetUnixFileMode(settings, UnixFileMode.None);
            if (CanRead(settings))
            {
                Assert.Skip("The file system does not stop this user from reading a file without permissions.");
                return;
            }
        }

        try
        {
            // Act: run the tool with the fix option
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

            // Assert: the problem is reported at line 1 and column 1 of the Markdown file, and the file is as it was
            Assert.Equal(1, exitCode);
            Assert.Contains("a.md:1:1: config-error Cannot read .editorconfig settings", output);
            Assert.Equal(Bytes("# Title\r\n"), File.ReadAllBytes(file));
        }
        finally
        {
            // Restore access so that the directory can be deleted
            hold?.Dispose();
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(settings, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    /// <summary>
    ///     Test that the help verb lists the help topics.
    /// </summary>
    [Fact]
    public void LintMark_HelpTopics_HelpVerb_ListsTopics()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with the help verb
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "help");

        // Assert: every topic is listed with its command, without the banner
        Assert.Equal(0, exitCode);
        foreach (var topic in HelpCommand.RootCardIds)
        {
            Assert.Contains($"{HelpCommand.ToolName} help {topic}", output);
        }

        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that an unknown help topic returns the usage-error exit code.
    /// </summary>
    [Fact]
    public void LintMark_HelpTopics_UnknownTopic_ReturnsUsageError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: ask for a topic that does not exist
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "help",
            "no-such-topic");

        // Assert: exit code 2 and a message that names the topic
        Assert.Equal(2, exitCode);
        Assert.Contains("no-such-topic", output);
    }

    /// <summary>
    ///     Test that every command example in the help cards gives the result that its card states.
    /// </summary>
    [Fact]
    public void LintMark_HelpExamples_AllCommandExamples_GiveStatedResults()
    {
        // Arrange: the catalog the tool ships
        var catalog = HelpCatalog.Create(HelpCommand.CreateOptions().GetCatalogConfiguration());

        // Act: run every command example against the built tool in the runner's scratch folder; the runner
        // compares the exit codes and the 'expect' fragments. Before every command the scratch folder is
        // marked as a repository root, so that configuration discovery stops there, and the input files of
        // the card (TestData/HelpExamples/<card id>/) are copied in; they are kept per card because a
        // discovered .lintmark.yaml would change the results of every other card.
        var report = HelpExampleRunner.Run(catalog, invocation =>
        {
            Directory.CreateDirectory(Path.Combine(invocation.WorkingDirectory, ".git"));
            var cardFiles = Path.Combine(_helpExamplesPath, invocation.Card.Id);
            if (Directory.Exists(cardFiles))
            {
                foreach (var file in Directory.GetFiles(cardFiles))
                {
                    File.Copy(file, Path.Combine(invocation.WorkingDirectory, Path.GetFileName(file)), true);
                }
            }

            var exitCode = Runner.Run(
                invocation.WorkingDirectory,
                out var output,
                "dotnet",
                [_dllPath, .. invocation.Arguments]);
            return new HelpExampleOutcome(exitCode, output);
        });

        // Assert: the cards hold command examples, and every one gave the result its card states
        Assert.True(report.CommandsRun > 0, "The help cards contain no command examples.");
        report.AssertSucceeded();
    }

    /// <summary>
    ///     Determines whether a file can be opened for reading.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><see langword="true"/> when the file can be read.</returns>
    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

