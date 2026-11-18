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

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Cli;
using DemaConsulting.TestResults.IO;

namespace DemaConsulting.LintMark.SelfTest;

/// <summary>
///     Provides self-validation functionality for the LintMark.
/// </summary>
/// <remarks>
///     The self-tests run the tool in process through <see cref="Program.Run"/>, against Markdown files that each
///     test writes into its own temporary workspace, and check the exit code, the reported findings, and the files on
///     disk. They are the qualification evidence that the installed tool lints, fixes, previews, and honors its
///     configuration, not only that it starts.
/// </remarks>
internal static class Validation
{
    /// <summary>
    ///     A Markdown document that is already in the canonical form of the built-in defaults.
    /// </summary>
    private const string CleanMarkdown =
        "# Clean Document\n" +
        "\n" +
        "This paragraph is written in the canonical form.\n" +
        "\n" +
        "## Lists\n" +
        "\n" +
        "- first item\n" +
        "- second item\n" +
        "\n" +
        "## Code\n" +
        "\n" +
        "```text\n" +
        "sample\n" +
        "```\n" +
        "\n" +
        "## Table\n" +
        "\n" +
        "| Name | Value |\n" +
        "| --- | --- |\n" +
        "| a | 1 |\n";

    /// <summary>
    ///     A Markdown document with deliberate violations of the heading, whitespace, list, code fence, table and
    ///     emphasis rules; the line of each violation is fixed by <see cref="ExpectedViolations"/>.
    /// </summary>
    private const string ViolationsMarkdown =
        "# Violations\n" +
        "\n" +
        "### Skipped Level\n" +
        "\n" +
        "Some text   \n" +
        "* one\n" +
        "* two\n" +
        "\n" +
        "## Ending Punctuation:\n" +
        "\n" +
        "```\n" +
        "code\n" +
        "```\n" +
        "\n" +
        "|a|b|\n" +
        "|-|-|\n" +
        "|1|2|\n" +
        "\n" +
        "This is _emphasis_ text.\n";

    /// <summary>
    ///     The findings (<c>path:line:col: rule-id</c>) that checking <see cref="ViolationsMarkdown"/> must report, in
    ///     report order.
    /// </summary>
    private static readonly string[] ExpectedViolations =
    [
        "violations.md:3:1: heading-increment",
        "violations.md:5:10: trailing-whitespace",
        "violations.md:6:1: bullet",
        "violations.md:6:1: list-blank-lines",
        "violations.md:7:1: bullet",
        "violations.md:9:1: heading-trailing-punctuation",
        "violations.md:11:1: code-fence-language",
        "violations.md:15:1: table-column-style",
        "violations.md:16:1: table-column-style",
        "violations.md:17:1: table-column-style",
        "violations.md:19:9: emphasis"
    ];

    /// <summary>
    ///     A Markdown document whose every violation can be fixed: trailing whitespace, list markers and spacing, table
    ///     layout, emphasis delimiter, and an over-long line.
    /// </summary>
    private const string FixableMarkdown =
        "# Fixable\n" +
        "\n" +
        "Some text   \n" +
        "* one\n" +
        "* two\n" +
        "\n" +
        "|a|b|\n" +
        "|-|-|\n" +
        "|1|2|\n" +
        "\n" +
        "This is _emphasis_ text.\n" +
        "\n" +
        "This sentence is deliberately long so that it runs past the default limit of one hundred and twenty columns " +
        "and must be rewrapped by the fixer.\n";

    /// <summary>
    ///     The exact content <c>--fix</c> must write for <see cref="FixableMarkdown"/>.
    /// </summary>
    private const string FixedMarkdown =
        "# Fixable\n" +
        "\n" +
        "Some text\n" +
        "\n" +
        "- one\n" +
        "- two\n" +
        "\n" +
        "| a | b |\n" +
        "| --- | --- |\n" +
        "| 1 | 2 |\n" +
        "\n" +
        "This is *emphasis* text.\n" +
        "\n" +
        "This sentence is deliberately long so that it runs past the default limit of one hundred and twenty columns " +
        "and must be\n" +
        "rewrapped by the fixer.\n";

    /// <summary>
    ///     A configuration that changes a setting, switches a rule off, and ignores a folder.
    /// </summary>
    private const string ConfigurationYaml =
        "bullet: asterisk\n" +
        "rules:\n" +
        "  heading-increment: false\n" +
        "ignore:\n" +
        "  - \"skipped/**\"\n";

    /// <summary>
    ///     A Markdown document that is compliant only under <see cref="ConfigurationYaml"/>: it skips a heading level
    ///     and uses asterisk bullets.
    /// </summary>
    private const string ConfiguredMarkdown =
        "# Configured\n" +
        "\n" +
        "### Deep Heading\n" +
        "\n" +
        "* one\n" +
        "* two\n";

    /// <summary>
    ///     A non-compliant Markdown document placed in the folder that <see cref="ConfigurationYaml"/> ignores.
    /// </summary>
    private const string IgnoredMarkdown =
        "#Bad\n" +
        "* x\n";

    /// <summary>
    ///     The findings that the configured workspace must report when the configuration is not used.
    /// </summary>
    private static readonly string[] ExpectedFindingsWithoutConfiguration =
    [
        "configured.md:3:1: heading-increment",
        "configured.md:5:1: bullet",
        "configured.md:6:1: bullet",
        "skipped/bad.md:1:1: heading-missing-space",
        "skipped/bad.md:2:1: bullet",
        "skipped/bad.md:2:1: list-blank-lines"
    ];

    /// <summary>
    ///     Matches a finding line (<c>path:line:col: rule-id message</c>) and captures everything but the message.
    /// </summary>
    private static readonly Regex FindingPattern = new(
        @"^(?<finding>\S+:\d+:\d+: \S+) ",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Runs self-validation tests and optionally writes results to a file.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    ///     If any self-test fails, <c>context.WriteError</c> is called for each failure, which sets
    ///     <c>context.ExitCode</c> to 1 as a side-effect. If a results file is requested and its
    ///     extension is unsupported, <c>context.WriteError</c> is also called, resulting in a
    ///     non-zero exit code. The functional self-tests temporarily change the current directory of the
    ///     process to their workspace, so <c>Run</c> must not run concurrently with other code that depends on
    ///     the current directory.
    /// </remarks>
    public static void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Print validation header
        PrintValidationHeader(context);

        // Create test results collection
        var testResults = new DemaConsulting.TestResults.TestResults
        {
            Name = "LintMark Self-Validation"
        };

        // Run core functionality tests
        RunTest(context, testResults, "LintMark_VersionDisplay", VersionDisplayTest);
        RunTest(context, testResults, "LintMark_HelpDisplay", HelpDisplayTest);

        // Run the functional tests of linting, fixing, previewing and configuration
        RunTest(context, testResults, "LintMark_CleanFileNoFindings", CleanFileNoFindingsTest);
        RunTest(context, testResults, "LintMark_ViolationsReported", ViolationsReportedTest);
        RunTest(context, testResults, "LintMark_FixRewritesToCanonicalForm", FixRewritesToCanonicalFormTest);
        RunTest(context, testResults, "LintMark_DiffPreviewsFixes", DiffPreviewsFixesTest);
        RunTest(context, testResults, "LintMark_ConfigurationHonored", ConfigurationHonoredTest);

        // Calculate totals
        var totalTests = testResults.Results.Count;
        var passedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Passed);
        var failedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Failed);

        // Print summary
        context.WriteLine("");
        context.WriteLine($"Total Tests: {totalTests}");
        context.WriteLine($"Passed: {passedTests}");
        if (failedTests > 0)
        {
            context.WriteError($"Failed: {failedTests}");
        }
        else
        {
            context.WriteLine($"Failed: {failedTests}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>
    ///     Prints the validation header with system information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} DEMA Consulting LintMark");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs one self-test, records its outcome and duration, and reports it.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    /// <param name="testName">The name of the test, as it appears in the report and the results file.</param>
    /// <param name="test">
    ///     The test body; it returns <see langword="null"/> when every check passed, or the reason for the failure.
    /// </param>
    private static void RunTest(
        Context context,
        DemaConsulting.TestResults.TestResults testResults,
        string testName,
        Func<string?> test)
    {
        var startTime = DateTime.UtcNow;
        var result = CreateTestResult(testName);

        try
        {
            // Run the test and record its outcome
            var failure = test();
            if (failure == null)
            {
                result.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ {testName} - Passed");
            }
            else
            {
                result.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                result.ErrorMessage = failure;
                context.WriteError($"✗ {testName} - Failed: {failure}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(result, context, testName, ex);
        }

        FinalizeTestResult(result, startTime, testResults);
    }

    /// <summary>
    ///     Checks that <c>--version</c> prints a version number.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? VersionDisplayTest()
    {
        // Run the version query
        using var workspace = new TemporaryDirectory();
        var run = RunLintMark(workspace, "--version");
        if (run.ExitCode != 0)
        {
            return $"Program exited with code {run.ExitCode}";
        }

        // Verify version string is in log (version contains dots like 0.0.0)
        var versionPattern = new Regex(@"\b\d+\.\d+\.\d+", RegexOptions.None, TimeSpan.FromSeconds(1));
        return versionPattern.IsMatch(run.Log) ? null : "Version string not found in log";
    }

    /// <summary>
    ///     Checks that <c>--help</c> prints the usage text.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? HelpDisplayTest()
    {
        // Run the help query
        using var workspace = new TemporaryDirectory();
        var run = RunLintMark(workspace, "--help");
        if (run.ExitCode != 0)
        {
            return $"Program exited with code {run.ExitCode}";
        }

        // Verify help text is in log
        return run.Log.Contains("Usage:") && run.Log.Contains("Options:") ? null : "Help text not found in log";
    }

    /// <summary>
    ///     Checks that a compliant file produces no finding, exit code 0, and is not modified by a check.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? CleanFileNoFindingsTest()
    {
        // Arrange a compliant file
        using var workspace = new TemporaryDirectory();
        var file = workspace.WriteFile("clean.md", CleanMarkdown);

        // Check it
        var run = RunLintMark(workspace, "clean.md");

        // Expect no finding, success and an untouched file
        return CheckExitCode(run, 0)
               ?? CheckFindings(run, [])
               ?? CheckContent(file, CleanMarkdown);
    }

    /// <summary>
    ///     Checks that a file with violations of several rule categories reports each one at its line and column,
    ///     exits with 1, and is not modified by a check.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? ViolationsReportedTest()
    {
        // Arrange a file with deliberate violations
        using var workspace = new TemporaryDirectory();
        var file = workspace.WriteFile("violations.md", ViolationsMarkdown);

        // Check it
        var run = RunLintMark(workspace, "violations.md");

        // Expect every violation, a failure exit code, and an untouched file
        return CheckExitCode(run, 1)
               ?? CheckFindings(run, ExpectedViolations)
               ?? CheckContent(file, ViolationsMarkdown);
    }

    /// <summary>
    ///     Checks that <c>--fix</c> rewrites a fixable file into the exact canonical form, leaves a compliant file
    ///     untouched, and that a following check of both files is clean.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? FixRewritesToCanonicalFormTest()
    {
        // Arrange a fixable file and a compliant file, and remember when the compliant one was written
        using var workspace = new TemporaryDirectory();
        var fixable = workspace.WriteFile("fixable.md", FixableMarkdown);
        var clean = workspace.WriteFile("clean.md", CleanMarkdown);
        var cleanWriteTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(clean, cleanWriteTime);

        // Fix both files
        var fix = RunLintMark(workspace, "--fix", "fixable.md", "clean.md");

        // Expect success, the canonical rewrite, and an untouched compliant file
        var failure = CheckExitCode(fix, 0)
                      ?? CheckFindings(fix, [])
                      ?? CheckContent(fixable, FixedMarkdown)
                      ?? CheckContent(clean, CleanMarkdown);
        if (failure != null)
        {
            return failure;
        }

        if (File.GetLastWriteTimeUtc(clean) != cleanWriteTime)
        {
            return "The compliant file clean.md was rewritten by --fix";
        }

        // Check both files again; nothing may remain
        var check = RunLintMark(workspace, "fixable.md", "clean.md");
        return CheckExitCode(check, 0) ?? CheckFindings(check, []);
    }

    /// <summary>
    ///     Checks that <c>--diff</c> reports the findings and prints a unified diff of the fix of a fixable file,
    ///     shows no diff for a compliant file, and writes no file.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? DiffPreviewsFixesTest()
    {
        // Arrange a fixable file and a compliant file
        using var workspace = new TemporaryDirectory();
        var fixable = workspace.WriteFile("fixable.md", FixableMarkdown);
        var clean = workspace.WriteFile("clean.md", CleanMarkdown);

        // Preview the fixes
        var run = RunLintMark(workspace, "--diff", "fixable.md", "clean.md");

        // Expect a failure exit code for the findings, and nothing written
        var failure = CheckExitCode(run, 1)
                      ?? CheckContent(fixable, FixableMarkdown)
                      ?? CheckContent(clean, CleanMarkdown);
        if (failure != null)
        {
            return failure;
        }

        // Expect the diff of the fixable file only
        string[] expectedLines = ["--- a/fixable.md", "+++ b/fixable.md", "-* one", "+- one", "-|a|b|", "+| a | b |"];
        var lines = run.Log.Split('\n').Select(static l => l.TrimEnd('\r')).ToHashSet(StringComparer.Ordinal);
        var missing = expectedLines.Where(l => !lines.Contains(l)).ToList();
        if (missing.Count > 0)
        {
            return $"Diff is missing the lines: {string.Join(" | ", missing)}";
        }

        return lines.Contains("--- a/clean.md") ? "Diff shows the compliant file clean.md" : null;
    }

    /// <summary>
    ///     Checks that a discovered <c>.lintmark.yaml</c> changes a setting, switches a rule off and ignores a folder:
    ///     the workspace is clean with it and reports the expected findings without it.
    /// </summary>
    /// <returns><see langword="null"/> when the test passes; otherwise the reason for the failure.</returns>
    private static string? ConfigurationHonoredTest()
    {
        // Arrange a configuration, a file compliant only under it, and a non-compliant file in an ignored folder
        using var workspace = new TemporaryDirectory();
        workspace.WriteFile(".lintmark.yaml", ConfigurationYaml);
        workspace.WriteFile("configured.md", ConfiguredMarkdown);
        workspace.WriteFile(Path.Combine("skipped", "bad.md"), IgnoredMarkdown);

        // Check the whole workspace with the configuration discovered
        var configured = RunLintMark(workspace);
        var failure = CheckExitCode(configured, 0) ?? CheckFindings(configured, []);
        if (failure != null)
        {
            return $"With .lintmark.yaml: {failure}";
        }

        // Check it again with the built-in defaults; the findings the configuration removed must reappear
        var defaults = RunLintMark(workspace, "--no-config");
        failure = CheckExitCode(defaults, 1) ?? CheckFindings(defaults, ExpectedFindingsWithoutConfiguration);
        return failure == null ? null : $"With --no-config: {failure}";
    }

    /// <summary>
    ///     Runs the tool in process, silently and with the workspace as the current directory, and captures its log.
    /// </summary>
    /// <param name="workspace">The workspace to run in.</param>
    /// <param name="args">The command line arguments, without <c>--silent</c> and <c>--log</c>.</param>
    /// <returns>The exit code and the log content.</returns>
    /// <remarks>
    ///     The tool reports paths relative to the current directory and discovers files from it, so the current
    ///     directory is switched to the workspace for the run and restored afterwards. The log is read after the
    ///     run's context is disposed, so that it is complete.
    /// </remarks>
    private static LintMarkRun RunLintMark(TemporaryDirectory workspace, params string[] args)
    {
        // Log to a file whose extension the run never selects
        var logFile = Path.Combine(workspace.DirectoryPath, $"lintmark-{Guid.NewGuid():N}.log");

        // Run with the workspace as the current directory
        int exitCode;
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.DirectoryPath);
        try
        {
            using var runContext = Context.Create(["--silent", "--log", logFile, .. args]);
            Program.Run(runContext);
            exitCode = runContext.ExitCode;
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }

        // Read and remove the log
        var log = File.ReadAllText(logFile);
        File.Delete(logFile);
        return new LintMarkRun(exitCode, log);
    }

    /// <summary>
    ///     Compares the exit code of a run with the expected one.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="expected">The expected exit code.</param>
    /// <returns><see langword="null"/> when they match; otherwise the reason for the failure.</returns>
    private static string? CheckExitCode(LintMarkRun run, int expected) =>
        run.ExitCode == expected ? null : $"Expected exit code {expected} but got {run.ExitCode}";

    /// <summary>
    ///     Compares the findings of a run, in report order and without their messages, with the expected ones.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="expected">The expected findings as <c>path:line:col: rule-id</c>.</param>
    /// <returns><see langword="null"/> when they match; otherwise the reason for the failure.</returns>
    private static string? CheckFindings(LintMarkRun run, IReadOnlyList<string> expected)
    {
        // Extract the findings from the log
        var actual = run.Log
            .Split('\n')
            .Select(static line => FindingPattern.Match(line.TrimEnd('\r')))
            .Where(static match => match.Success)
            .Select(static match => match.Groups["finding"].Value)
            .ToList();

        // Compare them in order
        return actual.SequenceEqual(expected, StringComparer.Ordinal)
            ? null
            : $"Expected findings [{string.Join(", ", expected)}] but got [{string.Join(", ", actual)}]";
    }

    /// <summary>
    ///     Compares the content of a file with the expected text.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="expected">The expected content.</param>
    /// <returns><see langword="null"/> when they match; otherwise the reason for the failure.</returns>
    private static string? CheckContent(string path, string expected) =>
        File.ReadAllText(path) == expected ? null : $"Unexpected content in {Path.GetFileName(path)}";

    /// <summary>
    ///     Writes test results to a file in TRX or JUnit format.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results to write.</param>
    private static void WriteResultsFile(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            string content;

            if (extension == ".trx")
            {
                content = TrxSerializer.Serialize(testResults);
            }
            else if (extension == ".xml")
            {
                // Assume JUnit format for .xml extension
                content = JUnitSerializer.Serialize(testResults);
            }
            else
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified here as a top-level handler to log file write errors
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a new test result object with common properties.
    /// </summary>
    /// <param name="testName">The name of the test.</param>
    /// <returns>A new test result object.</returns>
    private static DemaConsulting.TestResults.TestResult CreateTestResult(string testName)
    {
        return new DemaConsulting.TestResults.TestResult
        {
            Name = testName,
            ClassName = "Validation",
            CodeBase = "LintMark"
        };
    }

    /// <summary>
    ///     Finalizes a test result by setting its duration and adding it to the collection.
    /// </summary>
    /// <param name="test">The test result to finalize.</param>
    /// <param name="startTime">The start time of the test.</param>
    /// <param name="testResults">The test results collection to add to.</param>
    private static void FinalizeTestResult(
        DemaConsulting.TestResults.TestResult test,
        DateTime startTime,
        DemaConsulting.TestResults.TestResults testResults)
    {
        test.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(test);
    }

    /// <summary>
    ///     Handles test exceptions by setting failure information and logging the error.
    /// </summary>
    /// <param name="test">The test result to update.</param>
    /// <param name="context">The context for output.</param>
    /// <param name="testName">The name of the test for error messages.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void HandleTestException(
        DemaConsulting.TestResults.TestResult test,
        Context context,
        string testName,
        Exception ex)
    {
        test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
        test.ErrorMessage = $"Exception: {ex.Message}";
        context.WriteError($"✗ {testName} - FAILED: {ex.Message}");
    }

    /// <summary>
    ///     The outcome of one in-process run of the tool.
    /// </summary>
    /// <param name="ExitCode">The exit code of the run.</param>
    /// <param name="Log">The complete log of the run.</param>
    private sealed record LintMarkRun(int ExitCode, string Log);

    /// <summary>
    ///     Represents an isolated temporary workspace that is automatically deleted when disposed.
    /// </summary>
    /// <remarks>
    ///     The workspace holds an empty <c>.git</c> folder and an <c>.editorconfig</c> with <c>root = true</c>, so that
    ///     configuration discovery and editor settings never reach a <c>.lintmark.yaml</c> or <c>.editorconfig</c>
    ///     above it and the results do not depend on the machine.
    /// </remarks>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        ///     Gets the path to the temporary directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the directory cannot be created.</exception>
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"lintmark_validation_{Guid.NewGuid()}");

            try
            {
                // Create the workspace and bound configuration discovery and editor settings to it
                Directory.CreateDirectory(DirectoryPath);
                Directory.CreateDirectory(Path.Combine(DirectoryPath, ".git"));
                File.WriteAllText(Path.Combine(DirectoryPath, ".editorconfig"), "root = true\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Writes a text file into the workspace with exactly the given content (UTF-8, no byte order mark).
        /// </summary>
        /// <param name="relativePath">The path of the file relative to the workspace; missing folders are created.</param>
        /// <param name="content">The content of the file.</param>
        /// <returns>The full path of the file.</returns>
        public string WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(DirectoryPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>
        ///     Deletes the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup errors during disposal
            }
        }
    }
}
