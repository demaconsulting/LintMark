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

using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the fix and diff behavior of the LintMarkEngine class.
/// </summary>
public partial class LintMarkEngineTests
{
    /// <summary>
    ///     A small compliant document with LF line endings.
    /// </summary>
    private const string CompliantText = "# Title\n\nText.\n";

    /// <summary>
    ///     A configuration that asks for LF line endings.
    /// </summary>
    private const string LfConfig = "end-of-line: lf\n";

    /// <summary>
    ///     Creates an engine that formats with the given block formatters and runs sequentially.
    /// </summary>
    /// <param name="formatters">The block formatters, in priority order.</param>
    /// <returns>The engine.</returns>
    private static LintMarkEngine EngineWith(params IBlockFormatter[] formatters) =>
        new(1, formatter: new DocumentFormatter(formatters));

    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Asserts that a file still has the given bytes and the given last write time.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="bytes">The expected bytes.</param>
    /// <param name="writeTime">The expected last write time (UTC).</param>
    private static void AssertUntouched(string path, byte[] bytes, DateTime writeTime)
    {
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(path));
    }

    /// <summary>
    ///     Writes a file with an old last write time, so that a later write is visible.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <param name="name">The relative path of the file.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The full path and the fixed last write time.</returns>
    private static (string Path, DateTime WriteTime) WriteOld(TemporaryDirectory directory, string name, byte[] bytes)
    {
        var path = directory.WriteBytes(name, bytes);
        var writeTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, writeTime);
        return (path, writeTime);
    }

    /// <summary>
    ///     Test that the byte order mark of a file survives a rewrite of its line endings.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixBomCrlfFile_KeepsBomAndConvertsEndings()
    {
        // Arrange: a CRLF file with a byte order mark and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", [0xEF, 0xBB, 0xBF, .. Utf8("# Title\r\n\r\nText.\r\n")]);

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the byte order mark is still there and only the line endings changed
        Assert.Equal([0xEF, 0xBB, 0xBF, .. Utf8(CompliantText)], File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a file with mixed line endings is rewritten with its majority ending when nothing else is configured.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixMixedFile_WritesMajorityEnding()
    {
        // Arrange: two CRLF terminators and one LF terminator, no configuration
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: every terminator is CRLF now and nothing is reported
        Assert.Equal(Utf8("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the line ending of the editor settings is written when the configuration gives none.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixEditorConfigCrlf_WritesCrlf()
    {
        // Arrange: an LF file and editor settings that ask for CRLF
        using var directory = new TemporaryDirectory();
        WriteEditorConfig(directory, "crlf");
        var path = directory.WriteFile("a.md", CompliantText);

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file has CRLF line endings now
        Assert.Equal(Utf8("# Title\r\n\r\nText.\r\n"), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a second run of the fix option finds nothing to do.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixTwice_SecondRunLeavesFileAndReportsNothing()
    {
        // Arrange: a CRLF file, rewritten by a first run
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\r\n"));
        Execute(directory, new LintMarkEngine(1), "--fix");
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        // Act: run again
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file is not written again and nothing is reported
        AssertUntouched(path, Utf8(CompliantText), new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a written file no longer reports the violation that the rewrite fixed, while a check run reports it.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixSuccessfulWrite_DropsFixedDiagnostics()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\r\n"));

        // Act: check first, then fix
        var check = Execute(directory, new LintMarkEngine(1));
        var fix = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the check reports the violation; the fix writes the file and reports nothing
        Assert.Equal(1, check.ExitCode);
        Assert.Equal(["a.md:1:1: line-ending Line endings are CRLF; expected LF"], check.Lines);
        Assert.Equal(0, fix.ExitCode);
        Assert.Empty(fix.Lines);
    }

    /// <summary>
    ///     Test that the check option reports a line ending violation and writes nothing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckCrlfFile_ReportsLineEndingAndWritesNothing()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title\r\n\r\nText.\r\n"));

        // Act: run in check mode
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--check");

        // Assert: one violation at 1:1, exit code one, file untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: line-ending Line endings are CRLF; expected LF"], lines);
        AssertUntouched(path, Utf8("# Title\r\n\r\nText.\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that the diff option shows the change of line endings and writes nothing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_DiffCrlfFile_PrintsDiffAndWritesNothing()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title\r\n\r\nText.\r\n"));

        // Act: run with the diff option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--diff");

        // Assert: the violation, then the headers and the note; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: line-ending Line endings are CRLF; expected LF",
                "--- a/a.md",
                "+++ b/a.md",
                "\\ Line endings change from CRLF to LF"
            ],
            lines);
        AssertUntouched(path, Utf8("# Title\r\n\r\nText.\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that a rewrite that only changes the line endings of an ordinary document is still written.
    /// </summary>
    /// <param name="before">The document with its original line endings.</param>
    /// <param name="after">The document with the line endings that the configuration asks for.</param>
    /// <param name="endOfLine">The line ending that the configuration asks for.</param>
    [Theory]
    [InlineData("# T\n\nA `code` span\nand a soft break.\n\n- one\n- two\n\n```text\nx  y\n```\n", "# T\r\n\r\nA `code` span\r\nand a soft break.\r\n\r\n- one\r\n- two\r\n\r\n```text\r\nx  y\r\n```\r\n", "crlf")]
    [InlineData("# T\r\n\r\nA `code` span\r\nand a soft break.\r\n\r\n- one\r\n- two\r\n\r\n```text\r\nx  y\r\n```\r\n", "# T\n\nA `code` span\nand a soft break.\n\n- one\n- two\n\n```text\nx  y\n```\n", "lf")]
    public void LintMarkEngine_Run_FixOrdinaryLineEndingChange_StillWritten(string before, string after, string endOfLine)
    {
        // Arrange: an ordinary document with a code span, a soft break, a list and a fenced block
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", $"end-of-line: {endOfLine}\n");
        var path = directory.WriteBytes("a.md", Utf8(before));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the line endings are converted and nothing is reported
        Assert.Equal(Utf8(after), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a formatter that throws fails only its file and leaves it untouched.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixFormatterThrows_ReportsFormatErrorAndLeavesFile()
    {
        // Arrange: a formatter that throws
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("text\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new ThrowingFormatter()), "--fix");

        // Assert: a format error naming the exception type; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: file-format-error Formatting failed: InvalidOperationException"], lines);
        AssertUntouched(path, Utf8("text\n"), writeTime);
    }

    /// <summary>
    ///     Test that a failure to write is reported, keeps the formatting diagnostic and leaves the file as it was.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixWriterThrows_ReportsWriteErrorKeepsFormattingDiagnostic()
    {
        // Arrange: a CRLF file, a configuration that asks for LF and a writer that fails
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title\r\n\r\nText.\r\n"));
        var engine = new LintMarkEngine(1, fileWriter: (_, _, _, _) => throw new IOException("disk full"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the write error and the violation that was not fixed; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: file-write-error Cannot write file: disk full",
                "a.md:1:1: line-ending Line endings are CRLF; expected LF"
            ],
            lines);
        AssertUntouched(path, Utf8("# Title\r\n\r\nText.\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that a failure to write one file does not stop the other files from being written.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixOneWriteFails_OtherFilesStillWritten()
    {
        // Arrange: two CRLF files and a writer that fails for the first one only
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var first = directory.WriteBytes("a.md", Utf8("# A\r\n"));
        var second = directory.WriteBytes("b.md", Utf8("# B\r\n"));
        var engine = new LintMarkEngine(
            1,
            fileWriter: (target, resolved, original, content) =>
            {
                if (Path.GetFileName(target) == "a.md")
                {
                    throw new IOException("disk full");
                }

                AtomicFileWriter.Write(target, resolved, original.Span, content.Span);
            });

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the first file is reported and untouched; the second one is written
        Assert.Equal(1, exitCode);
        Assert.Equal(
            [
                "a.md:1:1: file-write-error Cannot write file: disk full",
                "a.md:1:1: line-ending Line endings are CRLF; expected LF"
            ],
            lines);
        Assert.Equal(Utf8("# A\r\n"), File.ReadAllBytes(first));
        Assert.Equal(Utf8("# B\n"), File.ReadAllBytes(second));
    }

    /// <summary>
    ///     Test that a file that was changed by someone else after it was read is not overwritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixFileChangedAfterRead_ReportsWriteErrorAndLeavesFile()
    {
        // Arrange: a writer that changes the file just before the real write
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\r\n"));
        var engine = new LintMarkEngine(
            1,
            fileWriter: (target, resolved, original, content) =>
            {
                File.WriteAllBytes(target, Utf8("changed by someone else\n"));
                AtomicFileWriter.Write(target, resolved, original.Span, content.Span);
            });

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the write is refused and the other change is intact
        Assert.Equal(1, exitCode);
        Assert.Equal(2, lines.Length);
        Assert.Equal("a.md:1:1: file-write-error Cannot write file: The file changed after it was read; it was not rewritten.", lines[0]);
        Assert.Equal(Utf8("changed by someone else\n"), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that the writer is given the full path of the file that was read as the expected target.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixRegularFile_PassesResolvedTargetToWriter()
    {
        // Arrange: a CRLF file and a writer that records what it is given before writing for real
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\r\n"));
        string? received = null;
        var engine = new LintMarkEngine(
            1,
            fileWriter: (target, resolved, original, content) =>
            {
                received = resolved;
                AtomicFileWriter.Write(target, resolved, original.Span, content.Span);
            });

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the expected target is an absolute path of the file that was read (the platform may spell the directory differently) and the rewrite happened
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        Assert.NotNull(received);
        Assert.True(Path.IsPathRooted(received));
        Assert.Equal("a.md", Path.GetFileName(received));
        Assert.Equal(Utf8(CompliantText), File.ReadAllBytes(received));
        Assert.Equal(Utf8(CompliantText), File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that a link redirected to another file with the same bytes after the read is refused and neither file is rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixLinkRedirectedAfterRead_ReportsWriteErrorAndLeavesBothFiles()
    {
        // Arrange: a link to a CRLF file, another file with the same bytes, and a writer that redirects the link before the real write
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows needs privileges that a test run may not have.");
            return;
        }

        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var bytes = Utf8("# Title\r\n\r\nText.\r\n");
        var first = directory.WriteBytes("first.txt", bytes);
        var second = directory.WriteBytes("second.txt", bytes);
        var link = directory.PathOf("a.md");
        File.CreateSymbolicLink(link, first);
        var engine = new LintMarkEngine(
            1,
            fileWriter: (target, resolved, original, content) =>
            {
                File.Delete(target);
                File.CreateSymbolicLink(target, second);
                AtomicFileWriter.Write(target, resolved, original.Span, content.Span);
            });

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the write is reported as failed and neither file was rewritten
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:1: file-write-error Cannot write file: The file link now points elsewhere", lines[0], StringComparison.Ordinal);
        Assert.Equal(bytes, File.ReadAllBytes(first));
        Assert.Equal(bytes, File.ReadAllBytes(second));
        Assert.Empty(Directory.GetFiles(directory.FullPath, "*.lintmark-tmp", SearchOption.AllDirectories));
    }

    /// <summary>
    ///     Test that a document with an invalid grid table is left byte for byte as it was and the offending line is reported.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixGridFallbackDocument_ByteIdenticalAndGridDiagnostic()
    {
        // Arrange: a CRLF document with an invalid grid table and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("para\r\n+---+---+\r\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the grid table finding at the offending line; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(
            ["a.md:2:1: grid-table-invalid Line looks like a grid table border but the table is not valid; the file is never rewritten"],
            lines);
        AssertUntouched(path, Utf8("para\r\n+---+---+\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that the check option reports the offending grid table line and exits with one.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckGridFallbackDocument_ReportsOffendingLineAndExitsOne()
    {
        // Arrange: a document with an invalid grid table after valid text
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", "# Title\n\npara\n+---+---+\n");

        // Act: run in check mode
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: reported at line 4, column 1
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:4:1: grid-table-invalid ", Assert.Single(lines), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that switching off the grid table rule silences the report but never lets the file be rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixGridFallbackRuleDisabled_NoDiagnosticStillUntouched()
    {
        // Arrange: a CRLF document with an invalid grid table; a configuration that asks for LF and switches the rule off
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\nrules:\n  grid-table-invalid: false\n");
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("para\r\n+---+---+\r\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: nothing is reported, and the file is untouched all the same
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        AssertUntouched(path, Utf8("para\r\n+---+---+\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that a suppression comment silences the grid table report but never lets the file be rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixGridFallbackSuppressed_NoDiagnosticStillUntouched()
    {
        // Arrange: a CRLF document whose invalid grid table is inside a suppressed region
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        const string Text = "<!-- markdownlint-disable grid-table-invalid -->\r\npara\r\n+---+---+\r\n<!-- markdownlint-enable grid-table-invalid -->\r\n";
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(Text));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: nothing is reported, and the file is untouched all the same
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
        AssertUntouched(path, Utf8(Text), writeTime);
    }

    /// <summary>
    ///     Test that a valid grid table is an ordinary block: no report, and the file is formatted.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixValidGridTable_FormattedNormallyNoDiagnostic()
    {
        // Arrange: a CRLF document with a valid grid table and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", Utf8("+---+---+\r\n| a | b |\r\n+---+---+\r\n| c | d |\r\n+---+---+\r\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the line endings are fixed and nothing is reported
        Assert.Equal(Utf8("+---+---+\n| a | b |\n+---+---+\n| c | d |\n+---+---+\n"), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the files written and the report are the same for any degree of parallelism.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixParallelAndSequential_ProduceIdenticalOutputAndFiles()
    {
        // Arrange: two identical trees of compliant, CRLF, mixed, refused and failing files
        using var sequentialDirectory = new TemporaryDirectory();
        using var parallelDirectory = new TemporaryDirectory();
        foreach (var directory in new[] { sequentialDirectory, parallelDirectory })
        {
            directory.WriteFile(".lintmark.yaml", LfConfig);
            for (var i = 0; i < 60; i++)
            {
                var name = $"d{i % 5}/f{i:D2}.md";
                switch (i % 6)
                {
                    case 0:
                        directory.WriteBytes(name, Utf8("# T\r\n\r\ntext  here\r\n"));
                        break;
                    case 1:
                        directory.WriteBytes(name, Utf8("# T\n\nclean\n"));
                        break;
                    case 2:
                        directory.WriteBytes(name, Utf8("x  \ny\n"));
                        break;
                    case 3:
                        directory.WriteBytes(name, [0xFF]);
                        break;
                    case 4:
                        directory.WriteBytes(name, Utf8("a\r\nb\nc\n"));
                        break;
                    default:
                        directory.WriteBytes(name, Utf8("para\n+---+---+\n"));
                        break;
                }
            }
        }

        // Act: fix sequentially and in parallel with a formatter that collapses double spaces
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);
        var sequential = Execute(sequentialDirectory, new LintMarkEngine(1, formatter: formatter), "--fix");
        var parallel = Execute(parallelDirectory, new LintMarkEngine(8, formatter: formatter), "--fix");

        // Assert: identical report, exit code and file contents
        Assert.Equal(1, sequential.ExitCode);
        Assert.Equal(sequential.ExitCode, parallel.ExitCode);
        Assert.Equal(sequential.Lines, parallel.Lines);
        Assert.Contains(sequential.Lines, l => l.Contains("grid-table-invalid", StringComparison.Ordinal));
        Assert.Contains(sequential.Lines, l => l.Contains("file-encoding-error", StringComparison.Ordinal));
        Assert.Equal(sequential.Lines.Order(StringComparer.Ordinal), sequential.Lines);
        foreach (var file in Directory.GetFiles(sequentialDirectory.FullPath, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sequentialDirectory.FullPath, file);
            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(parallelDirectory.PathOf(relative)));
        }
    }

    /// <summary>
    ///     Test that processing with a formatter returns the change with the exact new bytes.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_FormatterEditsApplied_ChangeHasNewBytes()
    {
        // Arrange: a BOM file with CRLF endings and a formatter that collapses double spaces
        using var directory = new TemporaryDirectory();
        var original = (byte[])[0xEF, 0xBB, 0xBF, .. Utf8("a  b\r\nc\r\n")];
        var path = directory.WriteBytes("a.md", original);

        // Act: process the file
        var result = Assert.Single(EngineWith(new ReplacingFormatter("  ", " ")).Process([path]));

        // Assert: the change keeps the BOM and the CRLF endings and carries both byte arrays
        var change = Assert.IsType<FileChange>(result.Change);
        Assert.Equal("a  b\nc\n", change.OriginalLf);
        Assert.Equal("a b\nc\n", change.NewLf);
        Assert.Equal(LineEnding.CrLf, change.TargetEnding);
        Assert.Equal(original, change.OriginalBytes.ToArray());
        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. Utf8("a b\r\nc\r\n")], change.NewBytes.ToArray());
        Assert.Equal(Path.GetFullPath(path), change.ResolvedTarget);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that every rule identifier the engine emits is registered in the rule catalog.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_EmittedRuleIds_AreRegisteredInRuleCatalog()
    {
        // Arrange / Act: the identifiers the engine declares
        var ids = new[]
        {
            LintMarkEngine.ReadErrorRuleId,
            LintMarkEngine.ParseErrorRuleId,
            LintMarkEngine.FormatErrorRuleId,
            LintMarkEngine.WriteErrorRuleId,
            ConfigLoader.ErrorRuleId,
            DocumentFormatter.LineEndingRuleId
        };

        // Assert: every one is registered
        Assert.All(ids, id => Assert.True(Linting.RuleCatalog.TryGet(id, out _), id));
    }
}
