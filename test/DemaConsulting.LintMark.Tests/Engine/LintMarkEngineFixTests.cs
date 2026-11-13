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
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the fix, diff and safety behavior of the LintMarkEngine class.
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
    ///     Test that a file with CRLF line endings is rewritten with LF line endings when the configuration asks for them.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixCrlfFile_RewritesToLfAndReportsNothing()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var path = directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\r\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(2), "--fix");

        // Assert: the file now has LF line endings and nothing is reported
        Assert.Equal(Utf8(CompliantText), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
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
    ///     Test that the line ending of the configuration is written even to a file that uses the other one.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixConfigCrlf_WritesCrlf()
    {
        // Arrange: an LF file and a configuration that asks for CRLF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: crlf\n");
        var path = directory.WriteFile("a.md", CompliantText);

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: the file has CRLF line endings now
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
    ///     Test that a file that already complies is not touched at all.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixCompliantFile_DoesNotTouchFile()
    {
        // Arrange: a compliant file with an old last write time
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", LfConfig);
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(CompliantText));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: neither the content nor the last write time changed, and nothing is reported
        AssertUntouched(path, Utf8(CompliantText), writeTime);
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
    ///     Test that a mixed file is reported as mixed.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckMixedFile_ReportsMixedLineEndings()
    {
        // Arrange: two CRLF terminators and one LF terminator
        using var directory = new TemporaryDirectory();
        directory.WriteBytes("a.md", Utf8("# Title\r\n\r\nText.\n"));

        // Act: run in check mode
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1));

        // Assert: reported once, naming the counts and the majority style
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: line-ending Mixed line endings (2 CRLF, 1 LF); expected CRLF"], lines);
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
    ///     Test that the diff option prints nothing for a file that would not change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_DiffCompliantFile_PrintsNothing()
    {
        // Arrange: a compliant file
        using var directory = new TemporaryDirectory();
        directory.WriteFile("a.md", CompliantText);

        // Act: run with the diff option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--diff");

        // Assert: no output and exit code zero
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that the diff option shows a text change proposed by a formatter and writes nothing.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_DiffTextChange_PrintsHunkAndWritesNothing()
    {
        // Arrange: a paragraph with a double space and a formatter that collapses it
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  b\n"));

        // Act: run with the diff option
        var (exitCode, lines) = Execute(directory, EngineWith(new ReplacingFormatter("  ", " ")), "--diff");

        // Assert: a unified diff of the change; the file is untouched
        Assert.Equal(0, exitCode);
        Assert.Equal(["--- a/a.md", "+++ b/a.md", "@@ -1 +1 @@", "-a  b", "+a b"], lines);
        AssertUntouched(path, Utf8("a  b\n"), writeTime);
    }

    /// <summary>
    ///     Test that the fix option writes a text change proposed by a formatter when every safety check passes.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixTextChange_WritesSafeRewrite()
    {
        // Arrange: a paragraph with a double space and a formatter that collapses it
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", Utf8("a  b\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new ReplacingFormatter("  ", " ")), "--fix");

        // Assert: the file has the formatted text and nothing is reported
        Assert.Equal(Utf8("a b\n"), File.ReadAllBytes(path));
        Assert.Equal(0, exitCode);
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a rewrite that would change how the document renders is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixHardBreakDropped_RefusesWithHtmlMismatch()
    {
        // Arrange: a paragraph with a hard break and a formatter that drops it
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  \nb\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new HardBreakDroppingFormatter()), "--fix");

        // Assert: one safety diagnostic at the first changed character; the file is untouched
        Assert.Equal(1, exitCode);
        var line = Assert.Single(lines);
        Assert.StartsWith("a.md:1:2: safety-html-mismatch The rewrite would change how the document renders (", line, StringComparison.Ordinal);
        Assert.EndsWith("); the file is not rewritten", line, StringComparison.Ordinal);
        AssertUntouched(path, Utf8("a  \nb\n"), writeTime);
    }

    /// <summary>
    ///     Test that a rewrite that glues Pandoc fence lines into prose is refused although the rendered output is the same.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixFencesJoined_RefusesWithStructureMismatch()
    {
        // Arrange: a fenced div and a formatter that joins the lines of the paragraph
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8(":::note\ntext\n:::\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new LineJoiningFormatter()), "--fix");

        // Assert: one structure diagnostic at the first changed character; the file is untouched
        Assert.Equal(1, exitCode);
        var line = Assert.Single(lines);
        Assert.StartsWith("a.md:1:8: safety-structure-mismatch The rewrite would change the Pandoc structure (", line, StringComparison.Ordinal);
        AssertUntouched(path, Utf8(":::note\ntext\n:::\n"), writeTime);
    }

    /// <summary>
    ///     Test that a formatter whose output changes again when it is formatted again is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixFlipFlopFormatter_RefusesWithNotIdempotent()
    {
        // Arrange: a paragraph and a formatter that switches the layout on every pass
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("alpha beta\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new FlipFlopFormatter()), "--fix");

        // Assert: one idempotency diagnostic; the file is untouched
        Assert.Equal(1, exitCode);
        var line = Assert.Single(lines);
        Assert.StartsWith("a.md:1:6: safety-not-idempotent Formatting is not stable (", line, StringComparison.Ordinal);
        AssertUntouched(path, Utf8("alpha beta\n"), writeTime);
    }

    /// <summary>
    ///     Test that a formatter that keeps growing the text is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixGrowingFormatter_RefusesWithNotIdempotent()
    {
        // Arrange: a paragraph and a formatter that appends a space on every pass
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("text\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new GrowingFormatter()), "--fix");

        // Assert: one idempotency diagnostic; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:5: safety-not-idempotent ", Assert.Single(lines), StringComparison.Ordinal);
        AssertUntouched(path, Utf8("text\n"), writeTime);
    }

    /// <summary>
    ///     Test that an edit that makes a line look like the border of an invalid grid table is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixGridBorderCreated_RefusesAndLeavesFile()
    {
        // Arrange: a paragraph and a formatter that puts the text "+---" on a line of its own
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a +--- b\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new GridBorderCreatingFormatter()), "--fix");

        // Assert: refused by a safety check (the grid table parser damages the rendering), file untouched
        Assert.Equal(1, exitCode);
        var line = Assert.Single(lines);
        Assert.Matches(@"^a\.md:1:\d+: safety-(html|structure)-mismatch ", line);
        AssertUntouched(path, Utf8("a +--- b\n"), writeTime);
    }

    /// <summary>
    ///     Test that a rewrite whose text the parser would only read by dropping grid tables is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixRewriteReadAsFallback_RefusesWithStructureMismatch()
    {
        // Arrange: a harmless rewrite, and a parser that reads the rewritten text as an invalid grid table document
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  b\n"));
        var engine = new LintMarkEngine(
            1,
            documentParser: source => source.Text == "a b\n"
                ? DocumentParser.Parse(source) with { GridFallback = new GridTableFallback(1, 1) }
                : DocumentParser.Parse(source),
            formatter: new DocumentFormatter([new ReplacingFormatter("  ", " ")]));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: refused by the structure check; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:3: safety-structure-mismatch The rewrite would not be read as before (", Assert.Single(lines), StringComparison.Ordinal);
        AssertUntouched(path, Utf8("a  b\n"), writeTime);
    }

    /// <summary>
    ///     Test that a safety check that cannot be completed refuses the rewrite and says so.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixReparseThrows_RefusesWithCheckFailedAndLeavesFile()
    {
        // Arrange: a harmless rewrite, and a parser that fails for the rewritten text
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  b\n"));
        var engine = new LintMarkEngine(
            1,
            documentParser: source => source.Text == "a b\n"
                ? throw new InvalidOperationException("parse failed")
                : DocumentParser.Parse(source),
            formatter: new DocumentFormatter([new ReplacingFormatter("  ", " ")]));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: refused as a failed check; the file is untouched
        Assert.Equal(1, exitCode);
        var line = Assert.Single(lines);
        Assert.StartsWith("a.md:1:3: safety-structure-mismatch The rewrite would not be read as before (the check failed: InvalidOperationException)", line, StringComparison.Ordinal);
        Assert.EndsWith("; the file is not rewritten", line, StringComparison.Ordinal);
        AssertUntouched(path, Utf8("a  b\n"), writeTime);
    }

    /// <summary>
    ///     Test that a parser that fails during the stability check refuses the rewrite as not stable.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixIdempotencyParseThrows_RefusesWithNotIdempotentAndLeavesFile()
    {
        // Arrange: a harmless rewrite, and a parser that fails the second time it reads the rewritten text
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  b\n"));
        var reads = 0;
        var engine = new LintMarkEngine(
            1,
            documentParser: source => source.Text == "a b\n" && ++reads > 1
                ? throw new InvalidOperationException("parse failed")
                : DocumentParser.Parse(source),
            formatter: new DocumentFormatter([new ReplacingFormatter("  ", " ")]));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: refused by the stability check; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:3: safety-not-idempotent Formatting is not stable (", Assert.Single(lines), StringComparison.Ordinal);
        AssertUntouched(path, Utf8("a  b\n"), writeTime);
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
    ///     Test that conflicting edits of formatters are reported as a format error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixOverlappingEdits_ReportsFormatError()
    {
        // Arrange: a formatter that proposes two overlapping edits
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("text\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new OverlappingEditsFormatter()), "--fix");

        // Assert: a format error; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: file-format-error Formatting failed: InvalidOperationException"], lines);
        AssertUntouched(path, Utf8("text\n"), writeTime);
    }

    /// <summary>
    ///     Test that an edit outside the text is reported as a format error.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixEditOutsideText_ReportsFormatError()
    {
        // Arrange: a formatter that proposes an edit beyond the end of the text
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("text\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new OutOfRangeEditFormatter()), "--fix");

        // Assert: a format error; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: file-format-error Formatting failed: ArgumentException"], lines);
        AssertUntouched(path, Utf8("text\n"), writeTime);
    }

    /// <summary>
    ///     Test that a formatter that inserts a carriage return is refused.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixCarriageReturnInserted_ReportsFormatError()
    {
        // Arrange: a formatter that inserts a carriage return
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("text\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, EngineWith(new CarriageReturnFormatter()), "--fix");

        // Assert: a format error; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: file-format-error Formatting failed: the result contains a carriage return"], lines);
        AssertUntouched(path, Utf8("text\n"), writeTime);
    }

    /// <summary>
    ///     Test that a broken formatter is reported in check mode in the same way as with the fix option.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_CheckBrokenFormatter_ReportsSafetyDiagnostic()
    {
        // Arrange: a paragraph with a hard break and a formatter that drops it
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("a  \nb\n"));

        // Act: run in check mode
        var (exitCode, lines) = Execute(directory, EngineWith(new HardBreakDroppingFormatter()));

        // Assert: the refusal is predicted; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:2: safety-html-mismatch ", Assert.Single(lines), StringComparison.Ordinal);
        AssertUntouched(path, Utf8("a  \nb\n"), writeTime);
    }

    /// <summary>
    ///     Test that a refused file does not stop a safe file from being written.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixSafeFileNextToRefusedFile_StillWritten()
    {
        // Arrange: one file whose rewrite loses a hard break and one whose rewrite is harmless
        using var directory = new TemporaryDirectory();
        var (refused, writeTime) = WriteOld(directory, "a.md", Utf8("x  \ny\n"));
        var safe = directory.WriteBytes("b.md", Utf8("x  y\n"));

        // Act: run with the fix option and a formatter that collapses double spaces
        var (exitCode, lines) = Execute(directory, EngineWith(new ReplacingFormatter("  ", " ")), "--fix");

        // Assert: the safe file is written, the other is refused and untouched, and exactly one diagnostic is reported
        Assert.Equal(1, exitCode);
        Assert.StartsWith("a.md:1:3: safety-html-mismatch ", Assert.Single(lines), StringComparison.Ordinal);
        Assert.Equal(Utf8("x y\n"), File.ReadAllBytes(safe));
        AssertUntouched(refused, Utf8("x  \ny\n"), writeTime);
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
        var engine = new LintMarkEngine(1, fileWriter: (_, _, _) => throw new IOException("disk full"));

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
            fileWriter: (target, original, content) =>
            {
                if (Path.GetFileName(target) == "a.md")
                {
                    throw new IOException("disk full");
                }

                AtomicFileWriter.Write(target, original.Span, content.Span);
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
            fileWriter: (target, original, content) =>
            {
                File.WriteAllBytes(target, Utf8("changed by someone else\n"));
                AtomicFileWriter.Write(target, original.Span, content.Span);
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
    ///     Test that a file whose configuration is invalid is never rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixInvalidConfig_NeverRewritesFile()
    {
        // Arrange: an invalid configuration and a file with mixed line endings that a rewrite would change
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title\n\nText.\r\n"));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, new LintMarkEngine(1), "--fix");

        // Assert: only the configuration problem is reported; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal([".lintmark.yaml:1:1: config-error Unknown setting 'bogus'"], lines);
        AssertUntouched(path, Utf8("# Title\n\nText.\r\n"), writeTime);
    }

    /// <summary>
    ///     Test that a file whose editor settings cannot be read is never rewritten.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Run_FixEditorConfigUnreadable_NeverRewritesFile()
    {
        // Arrange: editor settings that cannot be read and a file with mixed line endings
        using var directory = new TemporaryDirectory();
        var (path, writeTime) = WriteOld(directory, "a.md", Utf8("# Title\n\nText.\r\n"));
        var engine = new LintMarkEngine(
            1,
            editorConfigFactory: () => new EditorConfigResolver(_ => throw new IOException("disk failure"), (_, _) => throw new InvalidOperationException()));

        // Act: run with the fix option
        var (exitCode, lines) = Execute(directory, engine, "--fix");

        // Assert: the problem is reported; the file is untouched
        Assert.Equal(1, exitCode);
        Assert.Equal(["a.md:1:1: config-error Cannot read .editorconfig settings: disk failure"], lines);
        AssertUntouched(path, Utf8("# Title\n\nText.\r\n"), writeTime);
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
        Assert.Contains(sequential.Lines, l => l.Contains("safety-html-mismatch", StringComparison.Ordinal));
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
        Assert.Equal(SafetyStatus.Ok, result.Safety);
        Assert.Equal("a  b\nc\n", change.OriginalLf);
        Assert.Equal("a b\nc\n", change.NewLf);
        Assert.Equal(LineEnding.CrLf, change.TargetEnding);
        Assert.Equal(original, change.OriginalBytes.ToArray());
        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. Utf8("a b\r\nc\r\n")], change.NewBytes.ToArray());
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a rewrite that loses a hard break is refused with the HTML status and no change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_HardBreakFormatter_SetsHtmlMismatchAndNoChange()
    {
        // Arrange: a paragraph with a hard break
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "a  \nb\n");

        // Act: process the file with a formatter that drops the hard break
        var result = Assert.Single(EngineWith(new HardBreakDroppingFormatter()).Process([path]));

        // Assert: refused, no change
        Assert.Equal(SafetyStatus.HtmlMismatch, result.Safety);
        Assert.Null(result.Change);
        Assert.Equal("safety-html-mismatch", Assert.Single(result.Diagnostics).RuleId);
    }

    /// <summary>
    ///     Test that a rewrite that joins Pandoc fences is refused with the structure status and no change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_JoiningFormatter_SetsStructureMismatchAndNoChange()
    {
        // Arrange: a fenced div
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", ":::note\ntext\n:::\n");

        // Act: process the file with a formatter that joins the lines
        var result = Assert.Single(EngineWith(new LineJoiningFormatter()).Process([path]));

        // Assert: refused, no change
        Assert.Equal(SafetyStatus.StructureMismatch, result.Safety);
        Assert.Null(result.Change);
        Assert.Equal("safety-structure-mismatch", Assert.Single(result.Diagnostics).RuleId);
    }

    /// <summary>
    ///     Test that a rewrite that is not stable is refused with the idempotency status and no change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_FlipFlopFormatter_SetsNotIdempotentAndNoChange()
    {
        // Arrange: a paragraph that the formatter flips
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "alpha beta\n");

        // Act: process the file with the flip-flop formatter
        var result = Assert.Single(EngineWith(new FlipFlopFormatter()).Process([path]));

        // Assert: refused, no change
        Assert.Equal(SafetyStatus.NotIdempotent, result.Safety);
        Assert.Null(result.Change);
        Assert.Equal("safety-not-idempotent", Assert.Single(result.Diagnostics).RuleId);
    }

    /// <summary>
    ///     Test that a safety check that cannot be completed gives the structure status, a message that says so and no change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_ReparseThrows_SetsStructureMismatchAndNoChange()
    {
        // Arrange: a harmless rewrite and a parser that fails for the rewritten text
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "a  b\n");
        var engine = new LintMarkEngine(
            1,
            documentParser: source => source.Text == "a b\n"
                ? throw new InvalidOperationException("parse failed")
                : DocumentParser.Parse(source),
            formatter: new DocumentFormatter([new ReplacingFormatter("  ", " ")]));

        // Act: process the file
        var result = Assert.Single(engine.Process([path]));

        // Assert: refused, no change, and the diagnostic says the check failed
        Assert.Equal(SafetyStatus.StructureMismatch, result.Safety);
        Assert.Null(result.Change);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("safety-structure-mismatch", diagnostic.RuleId);
        Assert.Contains("the check failed", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a formatter that throws gives a format error and no change.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_FormatterThrows_ReportsFormatErrorAndNoChange()
    {
        // Arrange: a file and a formatter that throws
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "text\n");

        // Act: process the file
        var result = Assert.Single(EngineWith(new ThrowingFormatter()).Process([path]));

        // Assert: a format error, no change, and the safety status is untouched
        Assert.Null(result.Change);
        Assert.Equal(SafetyStatus.Ok, result.Safety);
        Assert.Equal("file-format-error", Assert.Single(result.Diagnostics).RuleId);
    }

    /// <summary>
    ///     Test that a file with an invalid configuration is never given to the formatters.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_InvalidConfig_NeverFormatted()
    {
        // Arrange: an invalid configuration and a formatter that would fail if it were called
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var path = directory.WriteFile("a.md", "text\n");

        // Act: process the file
        var result = Assert.Single(EngineWith(new ThrowingFormatter()).Process([path], ConfigLoader.ForDiscovery(), null));

        // Assert: no formatting happened, so no format error and no change
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Change);
        Assert.Single(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that a file whose editor settings cannot be read is never given to the formatters.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_EditorConfigUnreadable_NeverFormatted()
    {
        // Arrange: editor settings that cannot be read and a formatter that would fail if it were called
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "text\n");
        var resolver = new EditorConfigResolver(_ => throw new IOException("disk failure"), (_, _) => throw new InvalidOperationException());

        // Act: process the file
        var result = Assert.Single(EngineWith(new ThrowingFormatter()).Process([path], ConfigLoader.ForDefaults(), resolver));

        // Assert: no formatting happened, the configuration is kept and the problem is reported
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Change);
        Assert.NotNull(result.Config);
        Assert.Single(result.ConfigErrors);
    }

    /// <summary>
    ///     Test that a format-unsafe document has no change and carries the grid table diagnostic.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_GridFallbackDocument_HasNoChangeAndGridDiagnostic()
    {
        // Arrange: a document with an invalid grid table and a formatter that would change it
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "a  b\n+---+---+\n");

        // Act: process the file
        var result = Assert.Single(EngineWith(new ReplacingFormatter("  ", " ")).Process([path]));

        // Assert: the finding at line 2 and no change
        Assert.Null(result.Change);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(("grid-table-invalid", 2, 1), (diagnostic.RuleId, diagnostic.Line, diagnostic.Column));
    }

    /// <summary>
    ///     Test that the diagnostics of a result cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_DiagnosticsCastToMutableTypes_CannotBeModified()
    {
        // Arrange: a file with a failure
        using var directory = new TemporaryDirectory();
        var path = directory.WriteBytes("a.md", [0xFF]);

        // Act: process it
        var result = Assert.Single(new LintMarkEngine(1).Process([path]));

        // Assert: a caller cannot change the list
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<Linting.Diagnostic>)result.Diagnostics);
    }

    /// <summary>
    ///     Test that a null element in the list of paths, or a discovered file without a path, is rejected.
    /// </summary>
    [Fact]
    public void LintMarkEngine_Process_NullFileElements_Throw()
    {
        // Arrange: an engine
        var engine = new LintMarkEngine(1);

        // Act & Assert: a null path and a default discovered file are rejected before anything is processed
        Assert.Throws<ArgumentNullException>(() => engine.Process(new string[] { null! }));
        Assert.Throws<ArgumentNullException>(() => engine.Process(new DiscoveredFile[] { default }, ConfigLoader.ForDefaults(), null));
        Assert.Throws<ArgumentNullException>(() => engine.Process((IReadOnlyList<DiscoveredFile>)null!, ConfigLoader.ForDefaults(), null));
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
