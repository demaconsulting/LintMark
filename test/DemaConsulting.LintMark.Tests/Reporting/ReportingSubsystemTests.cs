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

using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Reporting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the Reporting subsystem (the text reporter and the diff reporter) works as a whole.
/// </summary>
[Collection("Sequential")]
public class ReportingSubsystemTests
{
    /// <summary>
    ///     The document the diff tests start from: thirty numbered lines.
    /// </summary>
    private static readonly string Before = string.Concat(Enumerable.Range(1, 30).Select(static i => $"line {i}\n"));

    /// <summary>
    ///     The document after edits at several places: a change near the start, a deletion and an
    ///     insertion in the middle, and a change at the end without a final newline.
    /// </summary>
    private static readonly string After = string.Concat(
        Enumerable.Range(1, 30).Select(static i => i switch
        {
            2 => "line two\n",
            15 => string.Empty,
            20 => "line 20\ninserted\n",
            30 => "last line",
            _ => $"line {i}\n"
        }));

    /// <summary>
    ///     Creates a diagnostic with a path inside a temporary directory.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <param name="relativePath">The path of the file relative to the directory.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="ruleId">The rule identifier.</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic Make(TemporaryDirectory directory, string relativePath, int line, string ruleId) =>
        new(ruleId, DiagnosticKind.Formatting, directory.PathOf(relativePath), line, 1, "Problem");

    /// <summary>
    ///     Reports with both reporters to a silent context with a log, and returns the log and the exit code.
    /// </summary>
    /// <param name="directory">The temporary directory used as the base directory and for the log.</param>
    /// <param name="diagnostics">The diagnostics for the text reporter.</param>
    /// <param name="diffs">The diffs for the diff reporter.</param>
    /// <returns>The lines of the log and the exit code.</returns>
    private static (string[] Log, int ExitCode) Run(TemporaryDirectory directory, IEnumerable<Diagnostic> diagnostics, IEnumerable<FileDiff> diffs)
    {
        var log = directory.PathOf("report.log");
        int exitCode;
        using (var context = Context.Create(["--silent", "--log", log]))
        {
            DiffReporter.Report(diffs, directory.FullPath, context);
            TextReporter.Report(diagnostics, directory.FullPath, context);
            exitCode = context.ExitCode;
        }

        return (File.Exists(log) ? File.ReadAllLines(log) : [], exitCode);
    }

    /// <summary>
    ///     Test that diagnostics are written sorted with paths relative to the base directory.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_TextFormat_Diagnostics_SortedAndRelative()
    {
        // Arrange: diagnostics for files at several depths, given out of order
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "docs/z.md", 2, "rule-b"),
            Make(directory, "README.md", 9, "rule-a"),
            Make(directory, "docs/a.md", 4, "rule-b"),
            Make(directory, "docs/a.md", 4, "rule-a")
        };

        // Act: report them
        var (log, exitCode) = Run(directory, diagnostics, []);

        // Assert: relative paths in path-then-position order, and a failing exit code
        Assert.Equal(
            [
                "README.md:9:1: rule-a Problem",
                "docs/a.md:4:1: rule-a Problem",
                "docs/a.md:4:1: rule-b Problem",
                "docs/z.md:2:1: rule-b Problem"
            ],
            log);
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that a diagnostic for a file outside the base directory is written with parent-directory parts.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_TextFormat_FileOutsideBaseDirectory_WritesParentRelativePath()
    {
        // Arrange: a diagnostic for a file next to the base directory
        using var directory = new TemporaryDirectory();
        var diagnostic = Make(directory, "other/b.md", 1, "rule-a");
        var log = directory.PathOf("report.log");

        // Act: report it relative to a sibling directory
        using (var context = Context.Create(["--silent", "--log", log]))
        {
            TextReporter.Report([diagnostic], directory.PathOf("work"), context);
        }

        // Assert: the path starts with ../ and uses forward slashes
        Assert.Equal(["../other/b.md:1:1: rule-a Problem"], File.ReadAllLines(log));
    }

    /// <summary>
    ///     Test that the diff of a multi-hunk change applies cleanly to the old text.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_UnifiedDiff_Changes_ApplyCleanly()
    {
        // Arrange: a document that changes at the start, in the middle and at the end
        using var directory = new TemporaryDirectory();
        var diff = new FileDiff(directory.PathOf("doc.md"), Before, After, new LineEndingInfo(30, 0), LineEnding.Lf);

        // Act: report it, take the diff lines from the log and apply them
        var (log, _) = Run(directory, [], [diff]);
        var patched = PatchApplier.Apply(Before, log);

        // Assert: three separate hunks, and the patch reproduces the new text exactly
        Assert.Equal(3, log.Count(static l => l.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Equal(After, patched);
    }

    /// <summary>
    ///     Test that a diagnostic that is reported twice is written once.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_TextFormat_DuplicateDiagnostic_WrittenOnce()
    {
        // Arrange: the same finding twice as equal instances, and another finding
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "a.md", 3, "rule-a"),
            Make(directory, "a.md", 3, "rule-a"),
            Make(directory, "b.md", 1, "rule-b")
        };

        // Act: report them
        var (log, exitCode) = Run(directory, diagnostics, []);

        // Assert: the repeated finding is one line, and the run fails
        Assert.Equal(["a.md:3:1: rule-a Problem", "b.md:1:1: rule-b Problem"], log);
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that nothing is written and the exit code stays zero when there is nothing to report.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_TextFormat_NoDiagnostics_WritesNothing()
    {
        // Arrange: no diagnostics and no diffs
        using var directory = new TemporaryDirectory();

        // Act: report them
        var (log, exitCode) = Run(directory, [], []);

        // Assert: an empty log and a zero exit code
        Assert.Empty(log);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that the diff of a change of line endings alone names the change and applies as no change.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_UnifiedDiff_LineEndingChangeOnly_WritesNote()
    {
        // Arrange: a file whose text is equal after normalization but whose line endings change
        using var directory = new TemporaryDirectory();
        var diff = new FileDiff(directory.PathOf("a.md"), "x\n", "x\n", new LineEndingInfo(0, 1), LineEnding.Lf);

        // Act: report it and apply the logged lines
        var (log, _) = Run(directory, [], [diff]);
        var patched = PatchApplier.Apply("x\n", log);

        // Assert: headers and the note without hunks, and the text is unchanged by the patch
        Assert.Equal(["--- a/a.md", "+++ b/a.md", "\\ Line endings change from CRLF to LF"], log);
        Assert.Equal("x\n", patched);
    }

    /// <summary>
    ///     Test that shuffled input gives identical output from both reporters.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_DeterministicOutput_ShuffledInput_IdenticalOutput()
    {
        // Arrange: diagnostics and diffs for several files, and the same items in the reverse order
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "b.md", 3, "rule-b"),
            Make(directory, "a.md", 7, "rule-a"),
            Make(directory, "a.md", 1, "rule-c"),
            Make(directory, "sub/c.md", 2, "rule-a")
        };
        var diffs = new[]
        {
            new FileDiff(directory.PathOf("b.md"), Before, After, new LineEndingInfo(30, 0), LineEnding.Lf),
            new FileDiff(directory.PathOf("a.md"), "x\n", "y\n", new LineEndingInfo(1, 0), LineEnding.Lf),
            new FileDiff(directory.PathOf("sub/c.md"), "p\n", "p\n", new LineEndingInfo(0, 1), LineEnding.Lf)
        };

        // Act: report both orders
        var forward = Run(directory, diagnostics, diffs);
        var reversed = Run(directory, diagnostics.Reverse(), diffs.Reverse());

        // Assert: identical output
        Assert.Equal(forward.Log, reversed.Log);
        Assert.Equal(forward.ExitCode, reversed.ExitCode);
    }

    /// <summary>
    ///     Test that diffs alone are written to standard output and do not make the exit code fail.
    /// </summary>
    [Fact]
    public void ReportingSubsystem_ExitStatus_DiffOnly_StaysZero()
    {
        // Arrange: one changed file, and a context that is not silent with its console captured
        using var directory = new TemporaryDirectory();
        var diff = new FileDiff(directory.PathOf("a.md"), "x\n", "y\n", new LineEndingInfo(1, 0), LineEnding.Lf);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            int exitCode;
            using (var context = Context.Create([]))
            {
                // Act: report the diff
                DiffReporter.Report([diff], directory.FullPath, context);
                exitCode = context.ExitCode;
            }

            // Assert: the diff is on standard output, nothing is on standard error and the exit code is zero
            Assert.Contains("--- a/a.md", outWriter.ToString(), StringComparison.Ordinal);
            Assert.Contains("+y", outWriter.ToString(), StringComparison.Ordinal);
            Assert.Empty(errorWriter.ToString());
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
