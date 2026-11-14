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

using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Verify.Cli;
using DemaConsulting.LintMark.Verify.Options;
using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Sleeps in a paragraph formatter, to run into the time limit.
/// </summary>
internal sealed class SleepingFormatter : ParagraphTestFormatter
{
    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits) => Thread.Sleep(TimeSpan.FromSeconds(3));
}

/// <summary>
///     Tests for the whole run (VerifyApplication): exit codes, the console summary, the Markdown report, the time
///     limit and, above all, that the inputs are never written.
/// </summary>
public class VerifyApplicationTests
{
    /// <summary>
    ///     Runs the application.
    /// </summary>
    /// <param name="directory">The base directory.</param>
    /// <param name="verifier">The verifier, or <see langword="null"/> for the production formatter.</param>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code, the standard output and the error output.</returns>
    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(TemporaryDirectory directory, FileVerifier? verifier, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await VerifyApplication.RunAsync(VerifyOptions.Parse(args), output, error, directory.FullPath, verifier);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Test that a passing run exits with zero and says so.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_PassingRun_ExitsZero()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "#  Title\n\nText   here\n");
        directory.WriteFile("b.md", "# Title\n\nText.\n");

        // Act
        var (exitCode, output, _) = await RunAsync(directory, null, directory.FullPath);

        // Assert
        Assert.Equal(VerifyApplication.Passed, exitCode);
        Assert.Contains("Verified 2 files under 1 option set(s): 2 checks", output, StringComparison.Ordinal);
        Assert.Contains("Passed: 2  Failed: 0", output, StringComparison.Ordinal);
        Assert.Contains("RESULT: PASSED", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failing run exits with one, and that the summary and the details name the file, the check and the differences.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_FailingRun_ExitsOneWithDetails()
    {
        // Arrange: a formatter that drops hard breaks
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "a  \nb\n");
        var verifier = new FileVerifier(new DocumentFormatter([new HardBreakDroppingFormatter()]));

        // Act
        var (exitCode, output, _) = await RunAsync(directory, verifier, directory.FullPath);

        // Assert
        Assert.Equal(VerifyApplication.Failed, exitCode);
        Assert.Contains("Passed: 0  Failed: 1", output, StringComparison.Ordinal);
        Assert.Contains("html-mismatch", output, StringComparison.Ordinal);
        Assert.Contains("FAIL html-mismatch  a.md  [defaults]", output, StringComparison.Ordinal);
        Assert.Contains("formatted text diff:", output, StringComparison.Ordinal);
        Assert.Contains("HTML difference:", output, StringComparison.Ordinal);
        Assert.Contains("RESULT: FAILED", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the refusals by design are reported as such and count as passes.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_ExemptFiles_CountAsPasses()
    {
        // Arrange: an invalid grid table document and a file that is not UTF-8
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "para  \n+---+---+\nmore\t\n");
        directory.WriteBytes("b.md", [0xFF, 0x0A]);

        // Act
        var (exitCode, output, _) = await RunAsync(directory, null, directory.FullPath);

        // Assert
        Assert.Equal(VerifyApplication.Passed, exitCode);
        Assert.Contains("exempt-grid-table-invalid (refused by design, counts as a pass)", output, StringComparison.Ordinal);
        Assert.Contains("exempt-encoding-error (refused by design, counts as a pass)", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the matrix run reports the failures per option set.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_Matrix_ReportsEverySet()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "# T\n\nA paragraph with enough words in it to be wrapped at forty columns, for sure.\n");

        // Act
        var (exitCode, output, _) = await RunAsync(directory, null, directory.FullPath, "--matrix");

        // Assert
        Assert.Equal(VerifyApplication.Passed, exitCode);
        Assert.Contains("1 files under 36 option set(s): 36 checks", output, StringComparison.Ordinal);
        Assert.Contains("0  wrap=semantic width=40 tables=aligned links=on", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the Markdown report holds the summary and the evidence, and fences content that has backticks safely.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_Report_HasSummaryAndFencedEvidence()
    {
        // Arrange: a failing file whose diff contains a run of four backticks
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "a  \nx ```` y\n");
        var verifier = new FileVerifier(new DocumentFormatter([new HardBreakDroppingFormatter()]));
        var reportPath = directory.PathOf("out/report.md");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        // Act
        var (exitCode, _, _) = await RunAsync(directory, verifier, directory.PathOf("a.md"), "--report", reportPath);
        var report = await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VerifyApplication.Failed, exitCode);
        Assert.StartsWith("# LintMark verification report\n", report, StringComparison.Ordinal);
        Assert.Contains("**Result**: FAILED", report, StringComparison.Ordinal);
        Assert.Contains("| html-mismatch | 1 |", report, StringComparison.Ordinal);
        Assert.Contains("### a.md: html-mismatch", report, StringComparison.Ordinal);
        Assert.Contains("\n`````diff\n", report, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', report);
    }

    /// <summary>
    ///     Test that a report is never written over an input.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_ReportOverInput_IsRefused()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "# T\n");

        // Act
        var (exitCode, _, error) = await RunAsync(directory, null, directory.FullPath, "--report", path);

        // Assert: a usage error, and the input is intact
        Assert.Equal(VerifyApplication.UsageError, exitCode);
        Assert.Contains("inputs are never written", error, StringComparison.Ordinal);
        Assert.Equal("# T\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that setup problems exit with two.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_SetupProblems_ExitTwo()
    {
        // Arrange: an empty directory, and a directory with an invalid setting
        using var empty = new TemporaryDirectory();
        using var other = new TemporaryDirectory();
        other.WriteFile("a.md", "# T\n");

        // Act
        var none = await RunAsync(empty, null, empty.FullPath);
        var invalid = await RunAsync(other, null, other.FullPath, "--option", "bogus-setting=1");

        // Assert
        Assert.Equal(VerifyApplication.UsageError, none.ExitCode);
        Assert.Contains("No Markdown files found", none.Error, StringComparison.Ordinal);
        Assert.Equal(VerifyApplication.UsageError, invalid.ExitCode);
        Assert.Contains("invalid", invalid.Error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the help text is printed.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_Help_PrintsUsage()
    {
        using var directory = new TemporaryDirectory();
        var (exitCode, output, _) = await RunAsync(directory, null, "--help");
        Assert.Equal(VerifyApplication.Passed, exitCode);
        Assert.Contains("--matrix", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a verification that takes too long is a timeout and the run goes on.
    /// </summary>
    [Fact]
    public async Task VerifyRunner_RunAsync_SlowFormatter_IsTimeoutAndRunContinues()
    {
        // Arrange: one file whose formatter sleeps and one that needs no formatting
        using var directory = new TemporaryDirectory();
        var slow = directory.WriteFile("a.md", "text\n");
        var runner = new VerifyRunner(new FileVerifier(new DocumentFormatter([new SleepingFormatter()])), 2, TimeSpan.FromMilliseconds(300));

        // Act
        var summary = await runner.RunAsync([new DiscoveredFile(slow, isExplicit: true)], [new OptionSet("t", [])], directory.FullPath);

        // Assert
        Assert.Equal(1, summary.KindCounts[OutcomeKind.Timeout]);
        Assert.Equal(1, summary.AbandonedWorkers);
        Assert.Equal(1, summary.FailureCount);
    }

    /// <summary>
    ///     Test that a run, with the matrix and with failures, leaves every input byte for byte and timestamp for timestamp as it was.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_AnyRun_NeverWritesInputs()
    {
        // Arrange: files that the formatter would change, in several line-ending styles, with an old write time
        using var directory = new TemporaryDirectory();
        var oldTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var paths = new[]
        {
            directory.WriteFile("lf.md", "#  Title\n\nSome   text  here\n"),
            directory.WriteBytes("crlf.md", "# Title\r\n\r\nText  here\r\n"u8.ToArray()),
            directory.WriteBytes("bom.md", [0xEF, 0xBB, 0xBF, .. "#  Title\n"u8.ToArray()]),
            directory.WriteFile("hard.md", "a  \nb\n")
        };
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, oldTime);
        }

        var before = paths.Select(File.ReadAllBytes).ToArray();
        var listing = Directory.GetFileSystemEntries(directory.FullPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        // Act: a matrix run with a broken formatter (failures) and a plain run with the production formatter
        await RunAsync(directory, new FileVerifier(new DocumentFormatter([new HardBreakDroppingFormatter()])), directory.FullPath, "--matrix");
        await RunAsync(directory, null, directory.FullPath, "--matrix");

        // Assert: bytes, write times and the set of files are unchanged
        for (var i = 0; i < paths.Length; i++)
        {
            Assert.Equal(before[i], await File.ReadAllBytesAsync(paths[i], TestContext.Current.CancellationToken));
            Assert.Equal(oldTime, File.GetLastWriteTimeUtc(paths[i]));
        }

        Assert.Equal(listing, Directory.GetFileSystemEntries(directory.FullPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray());
    }
}
