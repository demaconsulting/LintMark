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
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Reporting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the TextReporter class writes diagnostics in a deterministic order and sets the exit code.
/// </summary>
[Collection("Sequential")]
public class TextReporterTests
{
    /// <summary>
    ///     What a report produced: the exit code, the lines in the log and the text on both consoles.
    /// </summary>
    /// <param name="ExitCode">The exit code of the context after reporting.</param>
    /// <param name="Log">The lines of the log file.</param>
    /// <param name="ErrorConsole">Everything written to the error console.</param>
    /// <param name="OutputConsole">Everything written to the standard output console.</param>
    private sealed record Outcome(int ExitCode, string[] Log, string ErrorConsole, string OutputConsole);

    /// <summary>
    ///     Creates a diagnostic with a path inside a temporary directory.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <param name="relativePath">The path of the file relative to the directory.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="column">The 1-based column.</param>
    /// <param name="ruleId">The rule identifier.</param>
    /// <param name="message">The message.</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic Make(TemporaryDirectory directory, string relativePath, int line, int column, string ruleId = "rule-a", string message = "Message") =>
        new(ruleId, DiagnosticKind.Formatting, directory.PathOf(relativePath), line, column, message);

    /// <summary>
    ///     Reports diagnostics to a context with a log file and captures the results.
    /// </summary>
    /// <param name="directory">The temporary directory used as the base directory and for the log.</param>
    /// <param name="diagnostics">The diagnostics to report.</param>
    /// <param name="silent">Whether the context is silent.</param>
    /// <param name="baseDirectory">The directory paths are shown relative to; the temporary directory when null.</param>
    /// <returns>The outcome.</returns>
    private static Outcome Run(TemporaryDirectory directory, IEnumerable<Diagnostic> diagnostics, bool silent, string? baseDirectory = null)
    {
        var originalError = Console.Error;
        var originalOut = Console.Out;
        try
        {
            using var errorWriter = new StringWriter();
            using var outWriter = new StringWriter();
            Console.SetError(errorWriter);
            Console.SetOut(outWriter);
            var log = directory.PathOf("report.log");
            var arguments = silent ? new[] { "--silent", "--log", log } : new[] { "--log", log };
            int exitCode;
            using (var context = Context.Create(arguments))
            {
                TextReporter.Report(diagnostics, baseDirectory ?? directory.FullPath, context);
                exitCode = context.ExitCode;
            }

            return new Outcome(exitCode, File.Exists(log) ? File.ReadAllLines(log) : [], errorWriter.ToString(), outWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a diagnostic is written as path, line, column, rule identifier and message.
    /// </summary>
    [Fact]
    public void TextReporter_Report_Diagnostic_WritesPathLineColRuleMessage()
    {
        // Arrange: one diagnostic for a file in the base directory
        using var directory = new TemporaryDirectory();
        var diagnostic = Make(directory, "a.md", 3, 5, "heading-style", "Use an ATX heading.");

        // Act: report it
        var outcome = Run(directory, [diagnostic], silent: true);

        // Assert: one line in the documented format
        Assert.Equal(["a.md:3:5: heading-style Use an ATX heading."], outcome.Log);
    }

    /// <summary>
    ///     Test that a path outside the base directory keeps its parent-relative parts.
    /// </summary>
    [Fact]
    public void TextReporter_Report_OutsideBaseDirectory_WritesParentRelativePath()
    {
        // Arrange: a diagnostic for a file next to the base directory
        using var directory = new TemporaryDirectory();
        var diagnostic = Make(directory, "other/b.md", 1, 1);

        // Act: report it relative to a sibling directory
        var outcome = Run(directory, [diagnostic], silent: true, baseDirectory: directory.PathOf("work"));

        // Assert: the path starts with ../
        Assert.Equal(["../other/b.md:1:1: rule-a Message"], outcome.Log);
    }

    /// <summary>
    ///     Test that unsorted input is written ordered by path, line, column and then the rest.
    /// </summary>
    [Fact]
    public void TextReporter_Report_UnsortedInput_WritesPathThenPositionOrder()
    {
        // Arrange: diagnostics in no particular order; paths are compared by ordinal value and lines numerically
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "sub/c.md", 1, 1),
            Make(directory, "a.md", 10, 1),
            Make(directory, "B.md", 1, 1),
            Make(directory, "a.md", 2, 7),
            Make(directory, "a.md", 2, 3)
        };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: true);

        // Assert: uppercase before lowercase (ordinal), then line 2 before line 10, then by column
        Assert.Equal(
            [
                "B.md:1:1: rule-a Message",
                "a.md:2:3: rule-a Message",
                "a.md:2:7: rule-a Message",
                "a.md:10:1: rule-a Message",
                "sub/c.md:1:1: rule-a Message"
            ],
            outcome.Log);
    }

    /// <summary>
    ///     Test that equal diagnostics are written once.
    /// </summary>
    [Fact]
    public void TextReporter_Report_EqualDiagnostics_WritesOnce()
    {
        // Arrange: the same finding twice, as separate but equal instances
        using var directory = new TemporaryDirectory();
        var diagnostics = new[] { Make(directory, "a.md", 1, 1), Make(directory, "a.md", 1, 1) };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: true);

        // Assert: a single line
        Assert.Single(outcome.Log);
    }

    /// <summary>
    ///     Test that columns are ordered numerically, so column 3 comes before column 10.
    /// </summary>
    [Fact]
    public void TextReporter_Report_MultiDigitColumns_OrderedNumerically()
    {
        // Arrange: two findings on one line whose columns differ in the number of digits
        using var directory = new TemporaryDirectory();
        var diagnostics = new[] { Make(directory, "a.md", 1, 10), Make(directory, "a.md", 1, 3) };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: true);

        // Assert: column 3 before column 10 (a text order would put 10 first)
        Assert.Equal(["a.md:1:3: rule-a Message", "a.md:1:10: rule-a Message"], outcome.Log);
    }

    /// <summary>
    ///     Test that an empty list writes nothing and leaves the exit code at zero.
    /// </summary>
    [Fact]
    public void TextReporter_Report_NoDiagnostics_WritesNothing()
    {
        // Arrange: no diagnostics
        using var directory = new TemporaryDirectory();

        // Act: report them to a context that is not silent
        var outcome = Run(directory, [], silent: false);

        // Assert: nothing in the log or on the console, and the exit code is zero
        Assert.Empty(outcome.Log);
        Assert.Empty(outcome.ErrorConsole);
        Assert.Equal(0, outcome.ExitCode);
    }

    /// <summary>
    ///     Test that a silent context writes no console output but still fails and logs the line.
    /// </summary>
    [Fact]
    public void TextReporter_Report_SilentContext_StillSetsExitCode()
    {
        // Arrange: one diagnostic and a silent context
        using var directory = new TemporaryDirectory();
        var diagnostic = Make(directory, "a.md", 1, 2);

        // Act: report it
        var outcome = Run(directory, [diagnostic], silent: true);

        // Assert: no console output, exit code one, and the line in the log
        Assert.Empty(outcome.ErrorConsole);
        Assert.Equal(1, outcome.ExitCode);
        Assert.Equal(["a.md:1:2: rule-a Message"], outcome.Log);
    }

    /// <summary>
    ///     Test that an element that is null is refused before anything is written.
    /// </summary>
    [Fact]
    public void TextReporter_Report_NullElement_Throws()
    {
        // Arrange: a list with a valid diagnostic followed by null
        using var directory = new TemporaryDirectory();
        var diagnostics = new List<Diagnostic> { Make(directory, "a.md", 1, 1), null! };
        var log = directory.PathOf("report.log");
        using var context = Context.Create(["--silent", "--log", log]);

        // Act / Assert: the null element is refused
        Assert.Throws<ArgumentNullException>(() => TextReporter.Report(diagnostics, directory.FullPath, context));

        // Assert: nothing was written, not even the valid diagnostic
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that a null argument is refused.
    /// </summary>
    [Fact]
    public void TextReporter_Report_NullArguments_Throw()
    {
        // Arrange: a valid context
        using var context = Context.Create(["--silent"]);

        // Act / Assert: each argument is checked
        Assert.Throws<ArgumentNullException>(() => TextReporter.Report(null!, "dir", context));
        Assert.Throws<ArgumentNullException>(() => TextReporter.Report([], null!, context));
        Assert.Throws<ArgumentNullException>(() => TextReporter.Report([], "dir", null!));
    }

    /// <summary>
    ///     Test that a context with a log and a console receives the lines on both.
    /// </summary>
    [Fact]
    public void TextReporter_Report_LogContext_CapturesLines()
    {
        // Arrange: two diagnostics and a context that is not silent
        using var directory = new TemporaryDirectory();
        var diagnostics = new[] { Make(directory, "b.md", 1, 1), Make(directory, "a.md", 1, 1) };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: false);

        // Assert: the log has the lines in order and the error console shows them
        Assert.Equal(["a.md:1:1: rule-a Message", "b.md:1:1: rule-a Message"], outcome.Log);
        Assert.Contains("a.md:1:1: rule-a Message", outcome.ErrorConsole, StringComparison.Ordinal);
        Assert.Contains("b.md:1:1: rule-a Message", outcome.ErrorConsole, StringComparison.Ordinal);

        // Assert: nothing reached standard output
        Assert.Empty(outcome.OutputConsole);
    }

    /// <summary>
    ///     Test that any diagnostic, even one that is not fixable, makes the exit code one.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TextReporter_Report_AnyDiagnostic_SetsExitCodeOne(bool formatting)
    {
        // Arrange: one diagnostic of either kind
        using var directory = new TemporaryDirectory();
        var kind = formatting ? DiagnosticKind.Formatting : DiagnosticKind.LintOnly;
        var diagnostic = new Diagnostic("rule-a", kind, directory.PathOf("a.md"), 1, 1, "Message");

        // Act: report it
        var outcome = Run(directory, [diagnostic], silent: true);

        // Assert: the run fails
        Assert.Equal(1, outcome.ExitCode);
    }

    /// <summary>
    ///     Test that a path in a nested folder is written with forward slashes.
    /// </summary>
    [Fact]
    public void TextReporter_Report_NestedPath_UsesForwardSlashes()
    {
        // Arrange: a diagnostic two folders deep
        using var directory = new TemporaryDirectory();
        var diagnostic = Make(directory, "docs/guide/a.md", 4, 2);

        // Act: report it
        var outcome = Run(directory, [diagnostic], silent: true);

        // Assert: the separators are forward slashes
        Assert.Equal(["docs/guide/a.md:4:2: rule-a Message"], outcome.Log);
    }

    /// <summary>
    ///     Test that findings at the same position are ordered by rule identifier.
    /// </summary>
    [Fact]
    public void TextReporter_Report_SamePathDifferentRules_OrderedByRuleId()
    {
        // Arrange: three rules at the same position, given out of order
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "a.md", 1, 1, "zeta"),
            Make(directory, "a.md", 1, 1, "alpha"),
            Make(directory, "a.md", 1, 1, "beta")
        };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: true);

        // Assert: alphabetical by rule identifier
        Assert.Equal(["a.md:1:1: alpha Message", "a.md:1:1: beta Message", "a.md:1:1: zeta Message"], outcome.Log);
    }

    /// <summary>
    ///     Test that findings with the same position and rule are ordered by message in ordinal order.
    /// </summary>
    [Fact]
    public void TextReporter_Report_SameCoordinatesDifferentMessages_OrderedByMessage()
    {
        // Arrange: messages that differ only by letter and case
        using var directory = new TemporaryDirectory();
        var diagnostics = new[]
        {
            Make(directory, "a.md", 1, 1, message: "b text"),
            Make(directory, "a.md", 1, 1, message: "B text"),
            Make(directory, "a.md", 1, 1, message: "A text")
        };

        // Act: report them
        var outcome = Run(directory, diagnostics, silent: true);

        // Assert: uppercase sorts before lowercase in ordinal order
        Assert.Equal(["a.md:1:1: rule-a A text", "a.md:1:1: rule-a B text", "a.md:1:1: rule-a b text"], outcome.Log);
    }

    /// <summary>
    ///     Test that the order of the input does not change the output.
    /// </summary>
    [Fact]
    public void TextReporter_Report_InputOrderDoesNotMatter_IdenticalOutput()
    {
        // Arrange: a set of diagnostics and three different orders of it
        using var directory = new TemporaryDirectory();
        var diagnostics = new List<Diagnostic>
        {
            Make(directory, "b.md", 2, 1, "rule-b", "Second"),
            Make(directory, "a.md", 1, 1, "rule-a", "First"),
            Make(directory, "a.md", 1, 1, "rule-b", "Third"),
            Make(directory, "sub/c.md", 7, 3, "rule-a", "Fourth"),
            Make(directory, "a.md", 1, 1, "rule-a", "First"),
            Make(directory, "B.md", 5, 5, "rule-c", "Fifth")
        };
        var reversed = Enumerable.Reverse(diagnostics).ToList();
        var rotated = diagnostics.Skip(2).Concat(diagnostics.Take(2)).ToList();

        // Act: report each order
        var original = Run(directory, diagnostics, silent: true);
        var fromReversed = Run(directory, reversed, silent: true);
        var fromRotated = Run(directory, rotated, silent: true);

        // Assert: the output is identical
        Assert.Equal(original.Log, fromReversed.Log);
        Assert.Equal(original.Log, fromRotated.Log);
    }

    /// <summary>
    ///     Test that the display path is relative with forward slashes, and keeps parent parts outside the base.
    /// </summary>
    [Fact]
    public void TextReporter_GetDisplayPath_RelativeAndOutside_ReturnsForwardSlashPaths()
    {
        // Arrange: a base directory, a path inside it and a path next to it
        using var directory = new TemporaryDirectory();
        var baseDirectory = directory.PathOf("work");

        // Act: convert both paths
        var inside = TextReporter.GetDisplayPath(directory.PathOf("work/docs/a.md"), baseDirectory);
        var outside = TextReporter.GetDisplayPath(directory.PathOf("other/b.md"), baseDirectory);

        // Assert: relative paths with forward slashes
        Assert.Equal("docs/a.md", inside);
        Assert.Equal("../other/b.md", outside);
    }
}
