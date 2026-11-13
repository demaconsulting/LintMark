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

using System.Collections.Immutable;
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the units of the formatting subsystem (block formatters, edit set, document formatter and
///     source patcher) work together: formatters propose edits, conflicts are refused, and the patched text and
///     line ending are what the user's file receives.
/// </summary>
public class FormattingSubsystemTests
{
    /// <summary>
    ///     Parses text as a document named <c>a.md</c>.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Formats a document and applies the edits.
    /// </summary>
    /// <param name="formatter">The document formatter.</param>
    /// <param name="document">The document.</param>
    /// <returns>The format result and the patched text.</returns>
    private static (FormatResult Result, string Patched) FormatAndPatch(DocumentFormatter formatter, ParsedDocument document)
    {
        var result = formatter.Format(document, LintMarkConfig.Default);
        return (result, SourcePatcher.Apply(document.Source, result.Edits));
    }

    /// <summary>
    ///     Test that edits of different formatters for different blocks are both applied, in document order.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_TwoFormatters_AppliesBothEditsInOrder()
    {
        // Arrange: a heading and a paragraph, one formatter for each kind of block
        var document = Parse("# title\n\nbody  text\n");
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " "), new SubsystemHeadingUppercaseFormatter()]);

        // Act: format and patch
        var (result, patched) = FormatAndPatch(formatter, document);

        // Assert: the edits are ordered by position and both are applied
        Assert.Equal(2, result.Edits.Length);
        Assert.Equal("heading-style", result.Edits[0].RuleId);
        Assert.Equal("paragraph-wrap", result.Edits[1].RuleId);
        Assert.Equal("# TITLE\n\nbody text\n", patched);
    }

    /// <summary>
    ///     Test that without a formatter the text stays identical.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_NoFormatter_LeavesTextIdentical()
    {
        // Arrange: a document with text that a formatter could change
        var document = Parse("# title\n\nbody  text\n");

        // Act: format and patch
        var (result, patched) = FormatAndPatch(new DocumentFormatter(), document);

        // Assert: no edit, and the same text instance comes back
        Assert.Empty(result.Edits);
        Assert.Same(document.Source.Text, patched);
    }

    /// <summary>
    ///     Test that formatters that propose conflicting edits are refused.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_OverlappingEdits_Throws()
    {
        // Arrange: a formatter that proposes two edits of the same characters
        var document = Parse("abcdef\n");
        var formatter = new DocumentFormatter([new OverlappingEditsFormatter()]);

        // Act / Assert: formatting fails instead of choosing one of the edits
        Assert.Throws<InvalidOperationException>(() => formatter.Format(document, LintMarkConfig.Default));
    }

    /// <summary>
    ///     Test that a format-unsafe document produces nothing.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_FormatUnsafeDocument_ProducesNothing()
    {
        // Arrange: a document whose grid-table-looking line is not a valid grid table
        var document = Parse("para  x\n+---+---+\n");
        Assert.True(document.IsFormatUnsafe);
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format and patch
        var (result, patched) = FormatAndPatch(formatter, document);

        // Assert: no edit, no diagnostic, text unchanged
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("para  x\n+---+---+\n", patched);
    }

    /// <summary>
    ///     Test that a block with a suppressed rule is left verbatim while the others are formatted.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_SuppressedBlock_LeftVerbatim()
    {
        // Arrange: a paragraph inside a region where the formatter's rule is disabled, and one after it
        var document = Parse("<!-- markdownlint-disable paragraph-wrap -->\n\nkeep  this\n\n<!-- markdownlint-enable paragraph-wrap -->\n\nfix  this\n");
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format and patch
        var (_, patched) = FormatAndPatch(formatter, document);

        // Assert: only the paragraph outside of the region changed
        Assert.Equal(
            "<!-- markdownlint-disable paragraph-wrap -->\n\nkeep  this\n\n<!-- markdownlint-enable paragraph-wrap -->\n\nfix this\n",
            patched);
    }

    /// <summary>
    ///     Test that a CRLF file is patched as LF text and that the difference from the configured line ending is reported.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_CrlfDocument_PatchedTextKeepsLfAndReportsLineEnding()
    {
        // Arrange: a CRLF file and a configuration that asks for LF
        var bytes = Encoding.UTF8.GetBytes("a  b\r\n\r\nc  d\r\n");
        var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.Lf };

        // Act: format, patch and convert to the bytes that would be written
        var result = formatter.Format(document, config);
        var patched = SourcePatcher.Apply(document.Source, result.Edits);
        var written = document.Source.ToBytes(patched, result.LineEnding);

        // Assert: the patched text has no carriage return, the violation is reported, and the file is written with LF
        Assert.Equal("a b\n\nc d\n", patched);
        Assert.Equal("line-ending", Assert.Single(result.Diagnostics).RuleId);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
        Assert.Equal("a b\n\nc d\n", Encoding.UTF8.GetString(written));
    }
}

/// <summary>
///     A block formatter for headings that writes the heading in upper case, registered under the
///     <c>heading-style</c> rule; it lets the subsystem tests combine formatters for different kinds of block.
/// </summary>
internal sealed class SubsystemHeadingUppercaseFormatter : IBlockFormatter
{
    /// <summary>
    ///     Gets the rule the formatter is registered under.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } = ["heading-style"];

    /// <summary>
    ///     Accepts headings only.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> for a heading.</returns>
    public bool CanFormat(BlockInfo block) => block.Kind == BlockKind.Heading;

    /// <summary>
    ///     Replaces the text of the heading by its upper-case form.
    /// </summary>
    /// <param name="context">The document and settings.</param>
    /// <param name="block">The heading.</param>
    /// <param name="edits">Receives the edit.</param>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        var length = block.EndInclusive - block.Start + 1;
        var text = context.Document.Source.Text.Substring(block.Start, length);
        edits.Add(new TextEdit(block.Start, length, text.ToUpperInvariant(), "heading-style"));
    }
}
