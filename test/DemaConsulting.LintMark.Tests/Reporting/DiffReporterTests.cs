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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using DemaConsulting.LintMark.Cli;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Reporting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the DiffReporter class writes correct unified diffs for the changes a rewrite would make.
/// </summary>
[Collection("Sequential")]
public class DiffReporterTests
{
    /// <summary>
    ///     Creates a file diff of LF texts that has no line-ending change.
    /// </summary>
    /// <param name="before">The text on disk.</param>
    /// <param name="after">The text after rewriting.</param>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The file diff.</returns>
    private static FileDiff Diff(string before, string after, string path = "doc.md") =>
        new(path, before, after, new LineEndingInfo(before.Count(static c => c == '\n'), 0), LineEnding.Lf);

    /// <summary>
    ///     Builds a text of numbered lines <c>l1</c> to <c>lN</c>, with some lines replaced by <c>Xn</c>.
    /// </summary>
    /// <param name="count">The number of lines.</param>
    /// <param name="changed">The 1-based numbers of the lines to replace.</param>
    /// <returns>The LF-terminated text.</returns>
    private static string Numbered(int count, params int[] changed)
    {
        var builder = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            builder.Append(changed.Contains(i) ? "X" : "l").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Reports diffs to a context with a log and captures what went to the console and the log.
    /// </summary>
    /// <param name="directory">The temporary directory used as the base directory and for the log.</param>
    /// <param name="diffs">The diffs to report.</param>
    /// <param name="silent">Whether the context is silent.</param>
    /// <returns>The lines written to the console and to the log.</returns>
    private static (string[] Console, string[] Log) Run(TemporaryDirectory directory, IEnumerable<FileDiff> diffs, bool silent)
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            var log = directory.PathOf("report.log");
            var arguments = silent ? new[] { "--silent", "--log", log } : new[] { "--log", log };
            using (var context = Context.Create(arguments))
            {
                DiffReporter.Report(diffs, directory.FullPath, context);
            }

            var console = outWriter.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            return (console, File.Exists(log) ? File.ReadAllLines(log) : []);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a one-line change is written as headers and one hunk with context.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_SingleLineChange_WritesHeadersAndHunk()
    {
        // Arrange: three lines of which the middle one changes
        var diff = Diff("a\nb\nc\n", "a\nX\nc\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: headers, hunk header, context and the replaced line
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "@@ -1,3 +1,3 @@", " a", "-b", "+X", " c"], lines);
    }

    /// <summary>
    ///     Test that two changes far apart are written as two hunks.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_TwoDistantChanges_WritesTwoHunks()
    {
        // Arrange: lines 3 and 15 of 20 change, with 11 unchanged lines between them
        var diff = Diff(Numbered(20), Numbered(20, 3, 15));

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: two hunks, each with three lines of context
        Assert.Equal(
            [
                "--- a/doc.md",
                "+++ b/doc.md",
                "@@ -1,6 +1,6 @@",
                " l1",
                " l2",
                "-l3",
                "+X3",
                " l4",
                " l5",
                " l6",
                "@@ -12,7 +12,7 @@",
                " l12",
                " l13",
                " l14",
                "-l15",
                "+X15",
                " l16",
                " l17",
                " l18"
            ],
            lines);
    }

    /// <summary>
    ///     Test that changes with six unchanged lines between them share one hunk.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_NearbyChanges_MergesHunks()
    {
        // Arrange: lines 3 and 10 change, leaving exactly six unchanged lines between them
        var diff = Diff(Numbered(20), Numbered(20, 3, 10));

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: one hunk that spans from the context of the first change to the context of the second
        Assert.Equal(
            [
                "--- a/doc.md",
                "+++ b/doc.md",
                "@@ -1,13 +1,13 @@",
                " l1",
                " l2",
                "-l3",
                "+X3",
                " l4",
                " l5",
                " l6",
                " l7",
                " l8",
                " l9",
                "-l10",
                "+X10",
                " l11",
                " l12",
                " l13"
            ],
            lines);
    }

    /// <summary>
    ///     Test that changes with seven unchanged lines between them stay separate hunks.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_ChangesSevenLinesApart_StaysTwoHunks()
    {
        // Arrange: lines 3 and 11 change, leaving seven unchanged lines between them
        var diff = Diff(Numbered(20), Numbered(20, 3, 11));

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: two hunks with the expected headers
        Assert.Equal(["@@ -1,6 +1,6 @@", "@@ -8,7 +8,7 @@"], lines.Where(static l => l.StartsWith("@@", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that an insertion before the first line is described from the start of the file.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_InsertedAtStart_Correct()
    {
        // Arrange: a line is added in front of four lines
        var diff = Diff("a\nb\nc\nd\n", "X\na\nb\nc\nd\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the hunk has no leading context and three lines of trailing context
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "@@ -1,3 +1,4 @@", "+X", " a", " b", " c"], lines);
    }

    /// <summary>
    ///     Test that a deletion at the end of the file is described with leading context only.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_DeletedAtEnd_Correct()
    {
        // Arrange: the last of five lines is removed
        var diff = Diff("a\nb\nc\nd\ne\n", "a\nb\nc\nd\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: three lines of leading context and the removal
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "@@ -2,4 +2,3 @@", " b", " c", " d", "-e"], lines);
    }

    /// <summary>
    ///     Test that identical texts write nothing.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_IdenticalText_WritesNothing()
    {
        // Arrange: the same text before and after
        var diff = Diff("a\nb\n", "a\nb\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: no lines at all, not even headers
        Assert.Empty(lines);
    }

    /// <summary>
    ///     Test that a CRLF file that is only converted to LF shows the note and no hunk.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_CrlfToLfOnly_WritesNoteLine()
    {
        // Arrange: equal LF-normalized texts, two CRLF terminators, LF target
        var diff = new FileDiff("doc.md", "a\nb\n", "a\nb\n", new LineEndingInfo(0, 2), LineEnding.Lf);

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: headers and the note, no hunks
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "\\ Line endings change from CRLF to LF"], lines);
    }

    /// <summary>
    ///     Test that an LF file that is only converted to CRLF shows the note and no hunk.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_LfToCrlfOnly_WritesNoteLine()
    {
        // Arrange: equal LF-normalized texts, two LF terminators, CRLF target
        var diff = new FileDiff("doc.md", "a\nb\n", "a\nb\n", new LineEndingInfo(2, 0), LineEnding.CrLf);

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: headers and the note, no hunks
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "\\ Line endings change from LF to CRLF"], lines);
    }

    /// <summary>
    ///     Test that a mixed file names both counts in the note.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_MixedToLf_NoteNamesMixed()
    {
        // Arrange: two CRLF and one LF terminator, LF target
        var diff = new FileDiff("doc.md", "a\nb\nc\n", "a\nb\nc\n", new LineEndingInfo(1, 2), LineEnding.Lf);

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the note names the mix
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "\\ Line endings change from mixed (2 CRLF, 1 LF) to LF"], lines);
    }

    /// <summary>
    ///     Test that a line-ending change and a text change write the note before the hunks.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_EndingAndTextChange_WritesNoteBeforeHunk()
    {
        // Arrange: a CRLF file whose text also changes
        var diff = new FileDiff("doc.md", "a\nb\n", "a\nX\n", new LineEndingInfo(0, 2), LineEnding.Lf);

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: headers, note, then the hunk
        Assert.Equal(
            ["--- a/doc.md", "+++ b/doc.md", "\\ Line endings change from CRLF to LF", "@@ -1,2 +1,2 @@", " a", "-b", "+X"],
            lines);
    }

    /// <summary>
    ///     Test that a new text without a final newline gets the marker after its last line.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_MissingFinalNewline_WritesMarker()
    {
        // Arrange: the final line loses its terminator
        var diff = Diff("a\nb\n", "a\nb");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the new last line is followed by the marker
        Assert.Equal(
            ["--- a/doc.md", "+++ b/doc.md", "@@ -1,2 +1,2 @@", " a", "-b", "+b", "\\ No newline at end of file"],
            lines);
    }

    /// <summary>
    ///     Test that adding a final newline puts the marker after the old line.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_FinalNewlineAdded_WritesMarkerOnOldLine()
    {
        // Arrange: the final line gains a terminator
        var diff = Diff("a\nb", "a\nb\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the old last line is followed by the marker
        Assert.Equal(
            ["--- a/doc.md", "+++ b/doc.md", "@@ -1,2 +1,2 @@", " a", "-b", "\\ No newline at end of file", "+b"],
            lines);
    }

    /// <summary>
    ///     Test that several files are written in display-path order, not input order.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_ManyFiles_OrderedByPath()
    {
        // Arrange: three changed files given out of order, one in a folder
        using var directory = new TemporaryDirectory();
        var diffs = new[]
        {
            Diff("x\n", "y\n", directory.PathOf("z.md")),
            Diff("x\n", "y\n", directory.PathOf("a.md")),
            Diff("x\n", "y\n", directory.PathOf("m/b.md"))
        };

        // Act: report them
        var (_, log) = Run(directory, diffs, silent: true);

        // Assert: the old-file headers are in path order
        Assert.Equal(["--- a/a.md", "--- a/m/b.md", "--- a/z.md"], log.Where(static l => l.StartsWith("--- ", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that two completely different large texts still give a diff that applies cleanly and quickly.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_VeryDifferentLargeTexts_StillAppliesCleanly()
    {
        // Arrange: 3000 lines against 3000 other lines, more edits than the search limit allows
        var before = string.Concat(Enumerable.Range(0, 3000).Select(static i => $"a{i}\n"));
        var after = string.Concat(Enumerable.Range(0, 3000).Select(static i => $"b{i}\n"));

        // Act: format the diff and apply it
        var stopwatch = Stopwatch.StartNew();
        var lines = DiffReporter.Format(Diff(before, after), "doc.md");
        stopwatch.Stop();
        var patched = PatchApplier.Apply(before, lines);

        // Assert: one hunk that replaces everything, found quickly, and the patch gives the new text
        Assert.Equal("@@ -1,3000 +1,3000 @@", Assert.Single(lines, static l => l.StartsWith("@@", StringComparison.Ordinal)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Formatting took {stopwatch.Elapsed}.");
        Assert.Equal(after, patched);
    }

    /// <summary>
    ///     Test that a non-silent context writes the diff to the console and to the log.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_NonSilentContext_WritesConsoleAndLog()
    {
        // Arrange: one changed file and a context that is not silent, with a log
        using var directory = new TemporaryDirectory();
        var diff = Diff("a\n", "b\n", directory.PathOf("a.md"));

        // Act: report it
        var (console, log) = Run(directory, [diff], silent: false);

        // Assert: the same full diff reaches the console and the log
        string[] expected = ["--- a/a.md", "+++ b/a.md", "@@ -1 +1 @@", "-a", "+b"];
        Assert.Equal(expected, console);
        Assert.Equal(expected, log);
    }

    /// <summary>
    ///     Test that a silent context writes nothing to the console but keeps the diff in the log.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_SilentContext_SuppressesConsoleKeepsLog()
    {
        // Arrange: one changed file and a silent context with a log
        using var directory = new TemporaryDirectory();
        var diff = Diff("a\n", "b\n", directory.PathOf("a.md"));

        // Act: report it
        var (console, log) = Run(directory, [diff], silent: true);

        // Assert: no console output, and the full diff in the log
        Assert.Empty(console);
        Assert.Equal(["--- a/a.md", "+++ b/a.md", "@@ -1 +1 @@", "-a", "+b"], log);
    }

    /// <summary>
    ///     Test that a range of one line is written without a count.
    /// </summary>
    [Theory]
    [InlineData("a\n", "b\n", "@@ -1 +1 @@")]
    [InlineData("a\n", "a\nb\n", "@@ -1 +1,2 @@")]
    [InlineData("a\nb\n", "a\n", "@@ -1,2 +1 @@")]
    public void DiffReporter_Report_SingleLineHunkHeader_OmitsCountOfOne(string before, string after, string expectedHeader)
    {
        // Arrange: files whose hunk covers one line on at least one side
        var diff = Diff(before, after);

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the hunk header has no ",1"
        Assert.Equal(expectedHeader, Assert.Single(lines, static l => l.StartsWith("@@", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that an insertion into an empty file uses a zero count that starts at line zero.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_PureInsertionIntoEmptyFile_UsesZeroCount()
    {
        // Arrange: an empty file that gains two lines
        var diff = Diff(string.Empty, "a\nb\n");

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: the old range is empty and starts at the line before it
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "@@ -0,0 +1,2 @@", "+a", "+b"], lines);
    }

    /// <summary>
    ///     Test that a hunk shows three lines of context on each side of a change.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_ContextLinesAreThree_ThreeEachSide()
    {
        // Arrange: line 10 of 20 changes
        var diff = Diff(Numbered(20), Numbered(20, 10));

        // Act: format it
        var lines = DiffReporter.Format(diff, "doc.md");

        // Assert: lines 7 to 9 before and 11 to 13 after the change
        Assert.Equal(
            ["--- a/doc.md", "+++ b/doc.md", "@@ -7,7 +7,7 @@", " l7", " l8", " l9", "-l10", "+X10", " l11", " l12", " l13"],
            lines);
    }

    /// <summary>
    ///     Test that file content that looks like the no-newline marker is not treated as one.
    /// </summary>
    [Fact]
    public void DiffReporter_Report_BackslashInContent_NotMistakenForMarker()
    {
        // Arrange: a line that is exactly the marker text, inside the context of a change
        const string Look = "\\ No newline at end of file";
        var before = $"a\n{Look}\nb\n";
        var after = $"a\n{Look}\nc\n";

        // Act: format the diff and apply it
        var lines = DiffReporter.Format(Diff(before, after), "doc.md");
        var patched = PatchApplier.Apply(before, lines);

        // Assert: the look-alike is written as a context line behind a space, and the patch is exact
        Assert.Equal(["--- a/doc.md", "+++ b/doc.md", "@@ -1,3 +1,3 @@", " a", " " + Look, "-b", "+c"], lines);
        Assert.Equal(after, patched);
    }
}
