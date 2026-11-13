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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the DocumentFormatter class collects the edits of the first accepting formatter for every
///     block that is not suppressed, never touches a format-unsafe document, and decides the line ending of the file.
/// </summary>
public class DocumentFormatterTests
{
    /// <summary>
    ///     Parses text as a document named <c>a.md</c>.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Parses text as the bytes of a file named <c>a.md</c>, so its line endings are kept in the counts.
    /// </summary>
    /// <param name="text">The text with its original line endings.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument ParseFile(string text) => DocumentParser.Parse(SourceDocument.Create("a.md", Encoding.UTF8.GetBytes(text)));

    /// <summary>
    ///     Creates a formatter that replaces two spaces by one inside paragraphs.
    /// </summary>
    /// <returns>The formatter.</returns>
    private static DocumentFormatter DoubleSpaceFormatter() => new([new ReplacingFormatter("  ", " ")]);

    /// <summary>
    ///     Applies the edits of a result to the text of a document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="result">The result.</param>
    /// <returns>The patched text.</returns>
    private static string Patch(ParsedDocument document, FormatResult result) => SourcePatcher.Apply(document.Source, result.Edits);

    /// <summary>
    ///     Test that without formatters no edit is proposed and a uniform file has no diagnostic.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_NoFormatters_ProducesNoEdits()
    {
        // Arrange: a document that a formatter could change, and a formatter without block formatters
        var document = Parse("a  b\n");
        var formatter = new DocumentFormatter();

        // Act: format
        var result = formatter.Format(document, LintMarkConfig.Default);

        // Assert: nothing changes
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
    }

    /// <summary>
    ///     Test that the edits of an accepting formatter are collected.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MatchingFormatter_CollectsItsEdits()
    {
        // Arrange: a paragraph with two double spaces
        var document = Parse("a  b  c\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: one edit per double space, ordered, reported under the rule of the formatter
        var expectedStarts = new[] { 1, 4 };
        Assert.Equal(expectedStarts, result.Edits.Select(e => e.Start));
        Assert.All(result.Edits, edit => Assert.Equal("paragraph-wrap", edit.RuleId));
        Assert.Equal("a b c\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that the first formatter that accepts a block formats it and later formatters are not asked.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_TwoMatchingFormatters_FirstWins()
    {
        // Arrange: two formatters that accept the same paragraph and replace differently
        var document = Parse("a  b  c\n");
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", "_"), new ReplacingFormatter("  ", " ")]);

        // Act: format
        var result = formatter.Format(document, LintMarkConfig.Default);

        // Assert: only the edits of the first formatter exist
        Assert.Equal(2, result.Edits.Length);
        Assert.Equal("a_b_c\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that a block with a suppressed rule of the formatter is left verbatim.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_FormatterRuleSuppressed_LeavesBlockVerbatim()
    {
        // Arrange: three paragraphs of which the middle one is inside a disabled region of the formatter's rule
        var document = Parse(
            "before  x\n\n<!-- markdownlint-disable paragraph-wrap -->\n\nsuppressed  text\n\n<!-- markdownlint-enable paragraph-wrap -->\n\nafter  y\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the suppressed paragraph keeps its double space, the others are formatted
        Assert.Equal(
            "before x\n\n<!-- markdownlint-disable paragraph-wrap -->\n\nsuppressed  text\n\n<!-- markdownlint-enable paragraph-wrap -->\n\nafter y\n",
            Patch(document, result));
    }

    /// <summary>
    ///     Test that a region that disables every rule leaves its blocks verbatim.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_DisableAllRegion_LeavesBlockVerbatim()
    {
        // Arrange: a paragraph inside a region that disables all rules, and one after it
        var document = Parse("<!-- lintmark-disable -->\n\nsuppressed  text\n\n<!-- lintmark-enable -->\n\nafter  y\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: only the paragraph after the region is formatted
        Assert.Equal(
            "<!-- lintmark-disable -->\n\nsuppressed  text\n\n<!-- lintmark-enable -->\n\nafter y\n",
            Patch(document, result));
    }

    /// <summary>
    ///     Test that a prettier-ignore comment leaves the next block verbatim.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_PrettierIgnore_LeavesNextBlockVerbatim()
    {
        // Arrange: a prettier-ignore comment before the first paragraph
        var document = Parse("<!-- prettier-ignore -->\n\nignored  text\n\nformatted  text\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the ignored paragraph is verbatim, the following one is formatted
        Assert.Equal("<!-- prettier-ignore -->\n\nignored  text\n\nformatted text\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that suppressing one block does not stop the formatting of the other blocks.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_SuppressedBlock_OtherBlockStillFormatted()
    {
        // Arrange: a prettier-ignore comment between two paragraphs
        var document = Parse("first  text\n\n<!-- prettier-ignore -->\n\nignored  text\n\nlast  text\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: exactly the first and the last paragraph are edited
        Assert.Equal(2, result.Edits.Length);
        Assert.Equal("first text\n\n<!-- prettier-ignore -->\n\nignored  text\n\nlast text\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that suppressing another rule does not stop the formatter.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_OtherRuleSuppressed_BlockStillFormatted()
    {
        // Arrange: a paragraph inside a region that disables a different rule
        var document = Parse("<!-- markdownlint-disable line-length -->\n\nsome  text\n\n<!-- markdownlint-enable line-length -->\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the paragraph is formatted
        Assert.Equal("<!-- markdownlint-disable line-length -->\n\nsome text\n\n<!-- markdownlint-enable line-length -->\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that a block that no formatter accepts is left verbatim.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_BlockNoFormatterAccepts_LeftVerbatim()
    {
        // Arrange: a heading and a paragraph, and a formatter that accepts paragraphs only
        var document = Parse("# heading  here\n\npara  text\n");

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, LintMarkConfig.Default);

        // Assert: the heading keeps its double space
        Assert.Equal("# heading  here\n\npara text\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that a format-unsafe document gets no edit and no diagnostic, even when its line endings differ from the configuration.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_FormatUnsafeDocument_ReturnsNothingEvenWithCrlf()
    {
        // Arrange: a CRLF file with a line that looks like an invalid grid table border; the configuration asks for LF
        var document = ParseFile("para  x\r\n+---+---+\r\n");
        Assert.True(document.IsFormatUnsafe);
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.Lf };

        // Act: format
        var result = DoubleSpaceFormatter().Format(document, config);

        // Assert: nothing is proposed or reported, and the file keeps its own majority line ending
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(document.Source.MajorityLineEnding, result.LineEnding);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
    }

    /// <summary>
    ///     Test that a CRLF file with an LF configuration is reported once at line 1, column 1.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_CrlfFileLfConfig_ReportsLineEndingAt1x1()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        var document = ParseFile("a\r\n\r\nb\r\n");
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.Lf };

        // Act: format
        var result = new DocumentFormatter().Format(document, config);

        // Assert: one fixable formatting diagnostic names the difference
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("line-ending", diagnostic.RuleId);
        Assert.Equal(DiagnosticKind.Formatting, diagnostic.Kind);
        Assert.Equal("a.md", diagnostic.Path);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal("Line endings are CRLF; expected LF", diagnostic.Message);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
    }

    /// <summary>
    ///     Test that a file with both kinds of terminators is reported with their counts.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MixedFile_ReportsMixed()
    {
        // Arrange: two CRLF and one LF terminator, and a configuration that asks for LF
        var document = ParseFile("a\r\nb\r\nc\n");
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.Lf };

        // Act: format
        var result = new DocumentFormatter().Format(document, config);

        // Assert: the message names both counts
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Mixed line endings (2 CRLF, 1 LF); expected LF", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a CRLF file with a CRLF configuration has no line ending diagnostic.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_AllCrlfCrlfConfig_NoDiagnostic()
    {
        // Arrange: a CRLF file and a configuration that asks for CRLF
        var document = ParseFile("a\r\n\r\nb\r\n");
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.CrLf };

        // Act: format
        var result = new DocumentFormatter().Format(document, config);

        // Assert: nothing to report
        Assert.Empty(result.Diagnostics);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
    }

    /// <summary>
    ///     Test that an LF file with a CRLF configuration is reported as LF where CRLF is expected.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_LfFileCrlfConfig_ReportsLfToCrlf()
    {
        // Arrange: an LF file and a configuration that asks for CRLF
        var document = ParseFile("a\n\nb\n");
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.CrLf };

        // Act: format
        var result = new DocumentFormatter().Format(document, config);

        // Assert: the message names both endings
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Line endings are LF; expected CRLF", diagnostic.Message);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
    }

    /// <summary>
    ///     Test that a file without any terminator never violates the line ending rule.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_NoTerminators_NoDiagnostic()
    {
        // Arrange: a one-line file without terminator
        var document = ParseFile("abc");

        // Act: format with both configurations
        var lf = new DocumentFormatter().Format(document, LintMarkConfig.Default with { EndOfLine = LineEnding.Lf });
        var crlf = new DocumentFormatter().Format(document, LintMarkConfig.Default with { EndOfLine = LineEnding.CrLf });

        // Assert: neither reports anything
        Assert.Empty(lf.Diagnostics);
        Assert.Empty(crlf.Diagnostics);
    }

    /// <summary>
    ///     Test that without a configured line ending the majority ending of a mixed file is the target.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MixedFileNoConfig_TargetsMajority()
    {
        // Arrange: two CRLF and one LF terminator, and a configuration without a line ending
        var document = ParseFile("a\r\nb\r\nc\n");

        // Act: format
        var result = new DocumentFormatter().Format(document, LintMarkConfig.Default);

        // Assert: CRLF is the target and the one LF terminator is the violation
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Mixed line endings (2 CRLF, 1 LF); expected CRLF", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a configured line ending wins over the majority ending of the file.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ConfigLineEnding_WinsOverMajority()
    {
        // Arrange: a mostly CRLF file and a configuration that asks for LF
        var document = ParseFile("a\r\nb\r\nc\n");

        // Act: format
        var result = new DocumentFormatter().Format(document, LintMarkConfig.Default with { EndOfLine = LineEnding.Lf });

        // Assert: LF is the target
        Assert.Equal(LineEnding.Lf, result.LineEnding);
    }

    /// <summary>
    ///     Test that conflicting edits of a formatter are reported as a defect of the formatter.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_OverlappingFormatterEdits_Throws()
    {
        // Arrange: a formatter that proposes two edits of the same characters
        var document = Parse("abcdef\n");
        var formatter = new DocumentFormatter([new OverlappingEditsFormatter()]);

        // Act / Assert: the conflict is not resolved silently
        Assert.Throws<InvalidOperationException>(() => formatter.Format(document, LintMarkConfig.Default));
    }

    /// <summary>
    ///     Test that a null element in the formatter list is rejected.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Constructor_NullFormatterElement_Throws()
    {
        // Arrange / Act / Assert: the list must not contain null
        Assert.Throws<ArgumentNullException>(() => new DocumentFormatter([null!]));
    }

    /// <summary>
    ///     Test that a null formatter list means no formatters.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Constructor_NullList_Accepted()
    {
        // Arrange: a formatter created without a list
        var formatter = new DocumentFormatter(null);
        var document = Parse("a  b\n");

        // Act: format
        var result = formatter.Format(document, LintMarkConfig.Default);

        // Assert: it works and proposes no edit
        Assert.Empty(result.Edits);
    }

    /// <summary>
    ///     Test that the formatter list is copied, so later changes of the caller's list have no effect.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Constructor_ListCopied_LaterChangesIgnored()
    {
        // Arrange: a formatter built from a list that the caller changes afterwards
        var list = new List<IBlockFormatter> { new ReplacingFormatter("  ", " ") };
        var formatter = new DocumentFormatter(list);
        list.Clear();
        list.Add(new ReplacingFormatter("  ", "_"));
        var document = Parse("a  b\n");

        // Act: format
        var result = formatter.Format(document, LintMarkConfig.Default);

        // Assert: the original formatter is still used
        Assert.Equal("a b\n", Patch(document, result));
    }

    /// <summary>
    ///     Test that a format context keeps its document and configuration.
    /// </summary>
    [Fact]
    public void FormatContext_Constructor_ValidArguments_KeepsValues()
    {
        // Arrange / Act: a context for a document
        var document = Parse("a\n");
        var context = new FormatContext(document, LintMarkConfig.Default);

        // Assert: both are kept
        Assert.Same(document, context.Document);
        Assert.Same(LintMarkConfig.Default, context.Config);
    }

    /// <summary>
    ///     Test that a format context without a document or configuration is rejected.
    /// </summary>
    [Fact]
    public void FormatContext_Constructor_NullArguments_Throw()
    {
        // Arrange / Act / Assert: neither argument may be null
        Assert.Throws<ArgumentNullException>(() => new FormatContext(null!, LintMarkConfig.Default));
        Assert.Throws<ArgumentNullException>(() => new FormatContext(Parse("a\n"), null!));
    }

    /// <summary>
    ///     Test that a format result keeps its values when they are valid.
    /// </summary>
    [Fact]
    public void FormatResult_Constructor_ValidArguments_KeepsValues()
    {
        // Arrange / Act: a result with one edit and no diagnostics
        var edit = new TextEdit(0, 1, "b", "blank-lines");
        var result = new FormatResult([edit], LineEnding.CrLf, []);

        // Assert: the values are kept
        Assert.Equal(edit, Assert.Single(result.Edits));
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a format result with a default array or a default edit is rejected.
    /// </summary>
    [Fact]
    public void FormatResult_Constructor_InvalidEdits_Throws()
    {
        // Arrange / Act / Assert: a default array and a default edit are rejected
        Assert.Throws<ArgumentException>(() => new FormatResult(default, LineEnding.Lf, []));
        Assert.Throws<ArgumentException>(() => new FormatResult([default], LineEnding.Lf, []));
    }

    /// <summary>
    ///     Test that a format result with default or null diagnostics is rejected.
    /// </summary>
    [Fact]
    public void FormatResult_Constructor_InvalidDiagnostics_Throws()
    {
        // Arrange / Act / Assert: a default array and a null element are rejected
        Assert.Throws<ArgumentException>(() => new FormatResult([], LineEnding.Lf, default));
        Assert.Throws<ArgumentNullException>(() => new FormatResult([], LineEnding.Lf, [null!]));
    }

    /// <summary>
    ///     Test that a format result with an undefined line ending is rejected.
    /// </summary>
    [Fact]
    public void FormatResult_Constructor_UndefinedLineEnding_Throws()
    {
        // Arrange / Act / Assert: the line ending must be a defined value
        Assert.Throws<ArgumentOutOfRangeException>(() => new FormatResult([], (LineEnding)99, []));
    }

    /// <summary>
    ///     Test that a null argument of Format is rejected.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a formatter and a document
        var formatter = new DocumentFormatter();
        var document = Parse("a\n");

        // Act / Assert: neither argument may be null
        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!, LintMarkConfig.Default));
        Assert.Throws<ArgumentNullException>(() => formatter.Format(document, null!));
    }

    /// <summary>
    ///     Test that one formatter and one document can be used by many threads with identical results.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ParallelCalls_SameResult()
    {
        // Arrange: one shared formatter and document, and the single-threaded result
        var formatter = DoubleSpaceFormatter();
        var document = ParseFile("a  b  c\r\n\r\nd  e\r\n");
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.Lf };
        var expected = formatter.Format(document, config);
        var results = new FormatResult[64];

        // Act: format concurrently
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = formatter.Format(document, config));

        // Assert: every result equals the single-threaded one
        Assert.Equal(3, expected.Edits.Length);
        Assert.All(results, result =>
        {
            Assert.Equal(expected.Edits.ToArray(), result.Edits.ToArray());
            Assert.Equal(expected.Diagnostics.ToArray(), result.Diagnostics.ToArray());
            Assert.Equal(expected.LineEnding, result.LineEnding);
        });
    }
}
