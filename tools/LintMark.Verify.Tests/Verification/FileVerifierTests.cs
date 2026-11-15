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
using DemaConsulting.LintMark.Verify.Options;
using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Inserts the link refactoring marker comment after a paragraph once: a stand-in for the one thing the
///     link refactoring writes that the page contains.
/// </summary>
internal sealed class MarkerAddingFormatter : ParagraphTestFormatter
{
    /// <summary>
    ///     The marker comment.
    /// </summary>
    public const string Marker = "<!-- Link References -->";

    /// <inheritdoc />
    public override void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        if (!context.Document.Source.Text.Contains(Marker, StringComparison.Ordinal))
        {
            edits.Add(new TextEdit(block.EndInclusive + 1, 0, "\n\n" + Marker, "paragraph-wrap"));
        }
    }
}

/// <summary>
///     Tests for the FileVerifier class: a correct formatter passes, every kind of harmful rewrite is detected,
///     and the refusals by design count as passes.
/// </summary>
[Collection("ConfigLoaderTemporaryDirectory")]
public class FileVerifierTests
{
    /// <summary>
    ///     Verifies one file with the given formatter and option set.
    /// </summary>
    /// <param name="directory">The directory that holds the file.</param>
    /// <param name="path">The file.</param>
    /// <param name="formatter">The formatter, or <see langword="null"/> for the production one.</param>
    /// <param name="set">The option set, or <see langword="null"/> for the defaults.</param>
    /// <returns>The outcome.</returns>
    private static Outcome Verify(TemporaryDirectory directory, string path, DocumentFormatter? formatter = null, OptionSet? set = null)
    {
        set ??= new OptionSet("test", []);
        return new FileVerifier(formatter).Verify(new DiscoveredFile(path, isExplicit: true), set.Label, set.CreateLoader(), null, directory.FullPath);
    }

    /// <summary>
    ///     Test that a document the formatter changes correctly passes every check.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_CorrectRewrite_IsOk()
    {
        // Arrange: a document with a wrong heading style and extra spaces
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "#  Title #\n\nSome   text  here\n- one\n- two\n");

        // Act: verify with the production formatter
        var outcome = Verify(directory, path);

        // Assert: changed, and every check passed
        Assert.Equal(OutcomeKind.Ok, outcome.Kind);
        Assert.False(outcome.IsFailure);
        Assert.Empty(outcome.Findings);
    }

    /// <summary>
    ///     Test that a document that is already formatted is unchanged.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_AlreadyFormatted_IsUnchanged()
    {
        // Arrange: a formatted document
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "# Title\n\nText.\n");

        // Act / Assert: nothing to verify
        Assert.Equal(OutcomeKind.Unchanged, Verify(directory, path).Kind);
    }

    /// <summary>
    ///     Test that a rewrite that drops a hard break is an HTML mismatch with the evidence of both differences.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_HardBreakDropped_IsHtmlMismatchWithEvidence()
    {
        // Arrange: a paragraph with a hard break and a formatter that drops it
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "a  \nb\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new HardBreakDroppingFormatter()]));

        // Assert: the failure names the HTML and carries the text diff and the HTML diff
        Assert.Equal(OutcomeKind.HtmlMismatch, outcome.Kind);
        Assert.True(outcome.IsFailure);
        var finding = Assert.Single(outcome.Findings);
        Assert.Contains("-a  ", finding.TextDiff);
        Assert.Contains("+a", finding.TextDiff);
        Assert.Contains(finding.HtmlDiff, line => line.StartsWith("-", StringComparison.Ordinal) && line.Contains("<br", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(finding.Description));
    }

    /// <summary>
    ///     Test that a rewrite that glues Pandoc fences into prose is a structure mismatch although the HTML is the same.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_FencesJoined_IsStructureMismatch()
    {
        // Arrange: a fenced div and a formatter that joins the lines
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", ":::note\ntext\n:::\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new LineJoiningFormatter()]));

        // Assert
        Assert.Equal(OutcomeKind.StructureMismatch, outcome.Kind);
    }

    /// <summary>
    ///     Test that a formatter that flips between two layouts is not idempotent.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_FlipFlopFormatter_IsNotIdempotent()
    {
        // Arrange: a paragraph the formatter flips
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "alpha beta\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new FlipFlopFormatter()]));

        // Assert
        Assert.Equal(OutcomeKind.NotIdempotent, outcome.Kind);
    }

    /// <summary>
    ///     Test that a formatter that keeps growing the text is not idempotent.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_GrowingFormatter_IsNotIdempotent()
    {
        // Arrange: a paragraph and a formatter that appends a space every time
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "text\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new GrowingFormatter()]));

        // Assert
        Assert.Equal(OutcomeKind.NotIdempotent, outcome.Kind);
    }

    /// <summary>
    ///     Test that an edit that makes a line look like a grid table border is a failure.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_GridBorderCreated_IsFailure()
    {
        // Arrange: a paragraph and a formatter that puts "+---" on a line of its own
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "a +--- b\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new GridBorderCreatingFormatter()]));

        // Assert: caught by the HTML, structure or reparse check
        Assert.True(outcome.IsFailure);
        Assert.Contains(outcome.Kind, new[] { OutcomeKind.HtmlMismatch, OutcomeKind.StructureMismatch, OutcomeKind.ReparseGridTable });
    }

    /// <summary>
    ///     Test that a document with an invalid grid table is a pass by design.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_InvalidGridTable_IsExemptPass()
    {
        // Arrange: trailing whitespace next to a border line that is not part of a valid table
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "para  \n+---+---+\nmore\t\n");

        // Act
        var outcome = Verify(directory, path);

        // Assert: refused by design, which counts as a pass
        Assert.Equal(OutcomeKind.ExemptGridTableInvalid, outcome.Kind);
        Assert.True(outcome.Kind.IsExempt());
        Assert.False(outcome.IsFailure);
    }

    /// <summary>
    ///     Test that undecodable files are passes by design.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_UndecodableFiles_AreExemptPasses()
    {
        // Arrange: invalid UTF-8 and a bare carriage return
        using var directory = new TemporaryDirectory();
        var invalid = directory.WriteBytes("invalid.md", [0xFF, 0x0A]);
        var bareCr = directory.WriteBytes("cr.md", "a\rb\n"u8.ToArray());

        // Act / Assert
        Assert.Equal(OutcomeKind.ExemptEncodingError, Verify(directory, invalid).Kind);
        Assert.Equal(OutcomeKind.ExemptLineEndingError, Verify(directory, bareCr).Kind);
    }

    /// <summary>
    ///     Test that a formatter that throws is a crash.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_FormatterThrows_IsCrash()
    {
        // Arrange: a formatter that throws
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "text\n");

        // Act
        var outcome = Verify(directory, path, new DocumentFormatter([new ThrowingFormatter()]));

        // Assert
        Assert.Equal(OutcomeKind.Crash, outcome.Kind);
        Assert.Contains("file-format-error", Assert.Single(outcome.Findings).Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a configuration problem is a verifier error, not a failure of the formatter.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_MissingFile_IsVerifierError()
    {
        // Arrange: a path that does not exist
        using var directory = new TemporaryDirectory();

        // Act
        var outcome = Verify(directory, directory.PathOf("missing.md"));

        // Assert
        Assert.Equal(OutcomeKind.VerifierError, outcome.Kind);
    }

    /// <summary>
    ///     Test that a rewrite of the line endings of a multi-line code span, which the parser renders differently for
    ///     CRLF and LF, is caught by the check of the file as written.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_CodeSpanLineEndingChange_IsHtmlMismatchOnDisk()
    {
        // Arrange: a code span framed by line breaks and a configuration that asks for CRLF
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "`\nbb\n`\n");
        var set = new OptionSet("crlf", [new("end-of-line", "crlf")]);

        // Act
        var outcome = Verify(directory, path, set: set);

        // Assert
        Assert.Equal(OutcomeKind.HtmlMismatchOnDisk, outcome.Kind);
    }

    /// <summary>
    ///     Test that the link refactoring marker comment is allowed when the option is on, and a failure when it is off.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_AddedMarker_IsAllowedOnlyWithLinkRefactoring()
    {
        // Arrange: a formatter that adds the marker comment once
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "text\n");
        var formatter = new DocumentFormatter([new MarkerAddingFormatter()]);

        // Act
        var on = Verify(directory, path, formatter, new OptionSet("on", [new("link-refactor.enabled", "true")]));
        var off = Verify(directory, path, formatter, new OptionSet("off", [new("link-refactor.enabled", "false")]));

        // Assert: the comment is ignored only with the option on
        Assert.Equal(OutcomeKind.Ok, on.Kind);
        Assert.Equal(OutcomeKind.HtmlMismatch, off.Kind);
    }

    /// <summary>
    ///     Test that a real link refactoring passes with the option on.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_RealLinkRefactoring_IsOk()
    {
        // Arrange: a long inline link
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("a.md", "A [long link text](https://example.com/some/very/long/path/to/a/page/that/goes/on) here.\n");

        // Act
        var outcome = Verify(directory, path, set: new OptionSet("on", [new("link-refactor.enabled", "true")]));

        // Assert: the link was converted and the verification passed
        Assert.Equal(OutcomeKind.Ok, outcome.Kind);
    }

    /// <summary>
    ///     Test that an ignored file is a pass by design when the configuration is discovered.
    /// </summary>
    [Fact]
    public void FileVerifier_Verify_IgnoredFile_IsExemptPass()
    {
        // Arrange: a configuration that ignores the file, which was not named explicitly
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "ignore:\n  - a.md\n");
        var path = directory.WriteFile("a.md", "#  Title\n");

        // Act
        var outcome = new FileVerifier().Verify(
            new DiscoveredFile(path, isExplicit: false),
            "repo",
            OptionSet.RepoConfiguration().CreateLoader(),
            null,
            directory.FullPath);

        // Assert
        Assert.Equal(OutcomeKind.ExemptIgnored, outcome.Kind);
    }
}
