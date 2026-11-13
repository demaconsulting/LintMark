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
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Linting;
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
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The format result and the patched text.</returns>
    private static (FormatResult Result, string Patched) FormatAndPatch(
        DocumentFormatter formatter, ParsedDocument document, LintMarkConfig? config = null)
    {
        var result = formatter.Format(document, config ?? LintMarkConfig.Default);
        return (result, SourcePatcher.Apply(document.Source, result.Edits));
    }

    /// <summary>
    ///     Creates the document formatter that the tool ships: the heading, code block, thematic break, list and block
    ///     quote formatters and the inline normalizer.
    /// </summary>
    /// <returns>The document formatter.</returns>
    private static DocumentFormatter Shipped() => BlockFormatterAssertions.ProductionFormatter();

    /// <summary>
    ///     Formats text with the shipped document formatter and applies the edits.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    /// <returns>The format result and the patched text.</returns>
    private static (FormatResult Result, string Patched) FormatShipped(string text, LintMarkConfig? config = null) =>
        FormatAndPatch(Shipped(), Parse(text), config);

    /// <summary>
    ///     Asserts that the shipped formatters turn the text into the expected text, and that the given rule is
    ///     reported exactly when the text changes.
    /// </summary>
    /// <param name="input">The LF-normalized text.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="ruleId">The rule that the change belongs to.</param>
    private static void AssertShippedRule(string input, string expected, string ruleId)
    {
        var (result, patched) = FormatShipped(input);
        Assert.Equal(expected, patched);
        Assert.Equal(expected != input, result.Diagnostics.Any(diagnostic => diagnostic.RuleId == ruleId));
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
    ///     Test that every character that no edit covers is kept, including a character outside the basic plane
    ///     and the text of a block that no formatter accepts.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_OneEdit_UnchangedCharactersKept()
    {
        // Arrange: a paragraph with a surrogate pair, a fenced code block with a double space, and one paragraph to change
        var document = Parse("😀 smile\n\n```\ncode  x\n```\n\nfix  this\n");
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format and patch
        var (result, patched) = FormatAndPatch(formatter, document);

        // Assert: only the double space of the last paragraph is edited, and every other character is as it was
        Assert.Single(result.Edits);
        Assert.Equal("😀 smile\n\n```\ncode  x\n```\n\nfix this\n", patched);
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
    ///     Test that a suppression comment inside a block leaves the whole block verbatim while the others are formatted.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_SuppressionInsideBlock_LeftVerbatim()
    {
        // Arrange: a paragraph with a disable comment and an enable comment in its middle, and one paragraph after it
        var document = Parse("keep  <!-- markdownlint-disable paragraph-wrap --> this  <!-- markdownlint-enable paragraph-wrap --> text\n\nfix  this\n");
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format and patch
        var (_, patched) = FormatAndPatch(formatter, document);

        // Assert: the paragraph with the comments is unchanged, and only the other paragraph changed
        Assert.Equal(
            "keep  <!-- markdownlint-disable paragraph-wrap --> this  <!-- markdownlint-enable paragraph-wrap --> text\n\nfix this\n",
            patched);
    }

    /// <summary>
    ///     Test that an edit outside the text is refused when the edits are applied.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_OutOfRangeEdit_PatchingThrows()
    {
        // Arrange: a formatter that proposes an edit beyond the end of the text; the edit set itself accepts it
        var document = Parse("abcdef\n");
        var formatter = new DocumentFormatter([new OutOfRangeEditFormatter()]);
        var result = formatter.Format(document, LintMarkConfig.Default);

        // Act / Assert: the patcher refuses to apply the edit
        Assert.Single(result.Edits);
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(document.Source, result.Edits));
    }

    /// <summary>
    ///     Test that an exception of a block formatter reaches the caller of the subsystem.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_ThrowingFormatter_ExceptionReachesCaller()
    {
        // Arrange: a formatter that fails on a paragraph
        var document = Parse("abc\n");
        var formatter = new DocumentFormatter([new ThrowingFormatter()]);

        // Act / Assert: the failure is neither hidden nor replaced
        var exception = Assert.Throws<InvalidOperationException>(() => formatter.Format(document, LintMarkConfig.Default));
        Assert.Equal("The formatter failed on purpose", exception.Message);
    }

    /// <summary>
    ///     Test that without a configured line ending the majority ending of the file is written.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_NoConfiguredLineEnding_UsesMajority()
    {
        // Arrange: a file with two CRLF and one LF terminator, and a configuration without a line ending
        var bytes = Encoding.UTF8.GetBytes("a\r\nb\r\nc\n");
        var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));

        // Act: format, patch and convert to the bytes that would be written
        var result = new DocumentFormatter().Format(document, LintMarkConfig.Default);
        var patched = SourcePatcher.Apply(document.Source, result.Edits);
        var written = document.Source.ToBytes(patched, result.LineEnding);

        // Assert: CRLF is the target, so the one LF terminator is reported and the bytes use CRLF
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        Assert.Equal("line-ending", Assert.Single(result.Diagnostics).RuleId);
        Assert.Equal("a\r\nb\r\nc\r\n", Encoding.UTF8.GetString(written));
    }

    /// <summary>
    ///     Test that a file whose line endings all agree with the configuration has no line ending problem.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_UniformDocument_ReportsNoLineEndingProblem()
    {
        // Arrange: a CRLF file and a configuration that asks for CRLF
        var bytes = Encoding.UTF8.GetBytes("a\r\n\r\nb\r\n");
        var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));
        var config = LintMarkConfig.Default with { EndOfLine = LineEnding.CrLf };

        // Act: format
        var result = new DocumentFormatter().Format(document, config);

        // Assert: nothing is reported and the line ending is the configured one
        Assert.Empty(result.Diagnostics);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
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

    /// <summary>
    ///     Test that trailing whitespace is removed from text and from a line of only whitespace, and kept in code.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_TrailingWhitespace_IsRemovedFromTextOnly()
    {
        // Arrange: text, a line of spaces, text with a tab, and a fenced code block with trailing spaces
        const string input = "text \n  \nmore\t\n\n```\ncode  \n```\n";

        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input);

        // Assert: the whitespace of the text lines is gone, the code line is as written
        Assert.Equal("text\n\nmore\n\n```\ncode  \n```\n", patched);
    }

    /// <summary>
    ///     Test that a hard break keeps its meaning while other trailing whitespace goes.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_HardBreaks_KeepTheirMeaning()
    {
        // Arrange: a two-space break, a single trailing space, a backslash break, and a break with extra spaces
        const string input = "one  \ntwo \nthree\\\nfour   \nfive\n";

        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input);

        // Assert: both breaks stay breaks (two spaces, backslash) and the single space goes
        Assert.Equal("one  \ntwo\nthree\\\nfour  \nfive\n", patched);
    }

    /// <summary>
    ///     Test that tabs in text become spaces, and tabs in code spans, indentation and code blocks stay.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("a\tb\n", "a   b\n")]
    [InlineData("a `b\tc`\n", "a `b\tc`\n")]
    [InlineData("- a\n\t- b\n", "- a\n\t- b\n")]
    [InlineData("```\n\tx\n```\n", "```\n\tx\n```\n")]
    public void FormattingSubsystem_Format_Tabs_AreExpandedOnlyInPlainText(string input, string expected) =>
        AssertShippedRule(input, expected, "hard-tabs");

    /// <summary>
    ///     Test that a run of blank lines is reduced to the configured maximum.
    /// </summary>
    /// <param name="maximum">The configured maximum.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData(1, "a\n\nb\n")]
    [InlineData(2, "a\n\n\nb\n")]
    public void FormattingSubsystem_Format_BlankRuns_CollapseToTheConfiguredMaximum(int maximum, string expected)
    {
        // Arrange: three blank lines between two paragraphs
        var config = LintMarkConfig.Default with { MaxBlankLines = maximum };

        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped("a\n\n\n\nb\n", config);

        // Assert: the run has the configured length
        Assert.Equal(expected, patched);
    }

    /// <summary>
    ///     Test that a document ends with exactly one line ending when the configuration asks for it.
    /// </summary>
    /// <param name="input">The text.</param>
    [Theory]
    [InlineData("text")]
    [InlineData("text\n\n\n")]
    public void FormattingSubsystem_Format_FinalNewlineOn_EndsWithExactlyOneLineEnding(string input)
    {
        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input);

        // Assert: one line ending, whether it was missing or repeated
        Assert.Equal("text\n", patched);
    }

    /// <summary>
    ///     Test that a document ends without a line ending when the configuration asks for none.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_FinalNewlineOff_EndsWithoutLineEnding()
    {
        // Arrange: a configuration without a final newline
        var config = LintMarkConfig.Default with { FinalNewline = false };

        // Act: format and patch a document that ends with a line ending
        var (_, patched) = FormatShipped("text\n", config);

        // Assert: the line ending is gone
        Assert.Equal("text", patched);
    }

    /// <summary>
    ///     Test that a blank line is inserted between a heading and the text that touches it.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("# H\ntext\n", "# H\n\ntext\n")]
    [InlineData("text\n# H\n", "text\n\n# H\n")]
    public void FormattingSubsystem_Format_HeadingTouchingText_GetsBlankLines(string input, string expected) =>
        AssertShippedRule(input, expected, "heading-blank-lines");

    /// <summary>
    ///     Test that blank lines are inserted around a fenced code block that touches text.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_FenceTouchingText_GetsBlankLines()
    {
        // Act: format and patch a fence between two lines of text
        var (_, patched) = FormatShipped("a\n```\nc\n```\nb\n");

        // Assert: the fence is separated from the text on both sides
        Assert.Equal("a\n\n```\nc\n```\n\nb\n", patched);
    }

    /// <summary>
    ///     Test that a blank line is inserted before a list that touches text.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_ListTouchingText_GetsBlankLine()
    {
        // Act: format and patch a list that directly follows a line of text
        var (_, patched) = FormatShipped("text\n- a\n- b\n\nmore\n");

        // Assert: the list is separated from the text before it
        Assert.Equal("text\n\n- a\n- b\n\nmore\n", patched);
    }

    /// <summary>
    ///     Test that a blank line is inserted before a table that touches text.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_TableTouchingText_GetsBlankLine()
    {
        // Act: format and patch a table that directly follows a line of text
        var (_, patched) = FormatShipped("text\n| a | b |\n|---|---|\n| 1 | 2 |\n");

        // Assert: the table is separated from the text before it
        Assert.Equal("text\n\n| a | b |\n|---|---|\n| 1 | 2 |\n", patched);
    }

    /// <summary>
    ///     Test that a setext heading becomes an ATX heading where that keeps the meaning, and stays otherwise.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("Title\n=====\n", "# Title\n")]
    [InlineData("Multi\nline\n===\n", "Multi\nline\n===\n")]
    [InlineData("C#\n==\n", "C#\n==\n")]
    public void FormattingSubsystem_Format_SetextHeadings_BecomeAtxWhereSafe(string input, string expected) =>
        AssertShippedRule(input, expected, "heading-style");

    /// <summary>
    ///     Test that the closing number signs of a heading go, and number signs of its text stay.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("## Closed ##\n", "## Closed\n")]
    [InlineData("# C#\n", "# C#\n")]
    public void FormattingSubsystem_Format_ClosedHeadings_LoseTheClosingSigns(string input, string expected)
    {
        // Act: format and patch with the shipped formatters
        var (result, patched) = FormatShipped(input);

        // Assert: only a closing sequence is removed, and nothing else is reported for the heading
        Assert.Equal(expected, patched);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.RuleId == "heading-space");
    }

    /// <summary>
    ///     Test that the blanks after the number signs of a heading become one space, and a missing space is not invented.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("####    Spaced\n", "#### Spaced\n")]
    [InlineData("#Title\n", "#Title\n")]
    public void FormattingSubsystem_Format_HeadingMarkerSpacing_BecomesOneSpace(string input, string expected) =>
        AssertShippedRule(input, expected, "heading-space");

    /// <summary>
    ///     Test that an indented heading starts at the left margin.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_IndentedHeading_StartsAtTheLeftMargin()
    {
        // Act: format and patch a heading with three spaces of indentation
        var (_, patched) = FormatShipped("   ### Indented\n");

        // Assert: no indentation is left
        Assert.Equal("### Indented\n", patched);
    }

    /// <summary>
    ///     Test that thematic breaks are written in the style of the configuration, and only where that keeps the meaning.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="asterisks"><see langword="true"/> to configure asterisks instead of the default dashes.</param>
    [Theory]
    [InlineData("text\n\n***\n\nmore\n", "text\n\n---\n\nmore\n", false)]
    [InlineData("text\n\n---\n\nmore\n", "text\n\n***\n\nmore\n", true)]
    [InlineData("text\n***\nmore\n", "text\n***\nmore\n", false)]
    public void FormattingSubsystem_Format_ThematicBreaks_UseTheConfiguredStyle(string input, string expected, bool asterisks)
    {
        // Arrange: the default style or the asterisk style
        var config = asterisks ? LintMarkConfig.Default with { ThematicBreak = ThematicBreakStyle.Asterisks } : null;

        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input, config);

        // Assert: the configured style, and the break that touches text is as written
        Assert.Equal(expected, patched);
    }

    /// <summary>
    ///     Test that code fences are written with the configured fence character, and braced fences keep theirs.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="tilde"><see langword="true"/> to configure tildes instead of the default backticks.</param>
    [Theory]
    [InlineData("~~~js\ntilde\n~~~\n", "```js\ntilde\n```\n", false)]
    [InlineData("```\ncode\n```\n", "~~~\ncode\n~~~\n", true)]
    [InlineData("```{=html}\n<b>x</b>\n```\n", "```{=html}\n<b>x</b>\n```\n", true)]
    public void FormattingSubsystem_Format_CodeFences_UseTheConfiguredFenceCharacter(string input, string expected, bool tilde)
    {
        // Arrange: the default fence or the tilde fence
        var config = tilde ? LintMarkConfig.Default with { CodeFence = FenceMarker.Tilde } : null;

        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input, config);

        // Assert: the configured character, and the braced fence is as written
        Assert.Equal(expected, patched);
    }

    /// <summary>
    ///     Test that an indented code block becomes a fenced code block with the same content.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_IndentedCode_BecomesFencedCode()
    {
        // Act: format and patch an indented block that follows a heading
        var (_, patched) = FormatShipped("# H\n    code\n");

        // Assert: the block is fenced and separated from the heading
        Assert.Equal("# H\n\n```\ncode\n```\n", patched);
    }

    /// <summary>
    ///     Test that code, HTML, math, front matter and grid tables keep their content, and nothing is proposed or reported.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_LiteralBlocks_KeepTheirContent()
    {
        // Arrange: content with trailing whitespace, tabs and blank lines inside protected blocks
        const string content =
            "---\ntitle: x  \nlist:\n\t- a\n---\n\n```\ncode  \n\ttab\n\n\n\nend\n```\n\n<div>\nraw  \n\ttab\n</div>\n\n$$\nx  \n$$\n\n+---+---+\n| a | b |  \n+---+---+\n";

        // Act: format and patch with the shipped formatters
        var (result, patched) = FormatShipped(content);

        // Assert: nothing is edited or reported
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(content, patched);
    }

    /// <summary>
    ///     Test that a line that looks like the border of a grid table is not changed.
    /// </summary>
    /// <param name="input">The text.</param>
    [Theory]
    [InlineData("- +---+  \n- b\n")]
    [InlineData("> +---+\t\n> b\n")]
    public void FormattingSubsystem_Format_GridBorderLookalike_IsNotEdited(string input)
    {
        // Act: format and patch with the shipped formatters
        var (result, patched) = FormatShipped(input);

        // Assert: nothing is edited or reported
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(input, patched);
    }

    /// <summary>
    ///     Test that no blank line is inserted next to a line that looks like the border of a grid table.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_GridBorderLookalike_IsNotSeparated()
    {
        // Arrange: a lookalike line directly after text
        const string input = "text\n- +---+\n";

        // Act: format and patch with the shipped formatters
        var (result, patched) = FormatShipped(input);

        // Assert: no blank line is inserted
        Assert.Empty(result.Edits);
        Assert.Equal(input, patched);
    }

    /// <summary>
    ///     Test that blank lines inside quotes and lists are neither added nor removed.
    /// </summary>
    /// <param name="input">The text.</param>
    [Theory]
    [InlineData("- a\n  ```\n  code\n  ```\n- b\n\n> # H\n> text\n")]
    [InlineData("- a\n\n\n- b\n")]
    public void FormattingSubsystem_Format_QuotesAndLists_KeepTheirBlankLines(string input)
    {
        // Act: format and patch with the shipped formatters
        var (result, patched) = FormatShipped(input);

        // Assert: nothing is edited
        Assert.Empty(result.Edits);
        Assert.Equal(input, patched);
    }

    /// <summary>
    ///     Test that a fenced code block that is never closed stays as written, with the lines that follow it.
    /// </summary>
    /// <param name="input">The text.</param>
    [Theory]
    [InlineData("text\n```\ncode  \n\n\n")]
    [InlineData("text\n~~~\ncode\n")]
    public void FormattingSubsystem_Format_UnclosedFence_StaysAsWritten(string input)
    {
        // Act: format and patch with the shipped formatters
        var (_, patched) = FormatShipped(input);

        // Assert: the text is unchanged
        Assert.Equal(input, patched);
    }

    /// <summary>
    ///     Test that each change is reported as a problem of its rule at the place where the change starts.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_Violations_AreReportedPerRuleAtTheirPlace()
    {
        // Arrange: a heading with two spaces after its number sign that touches the text, and text with trailing spaces
        const string input = "#  Title\ntext   \n";

        // Act: format with the shipped formatters
        var (result, patched) = FormatShipped(input);

        // Assert: one formatting problem per rule, in document order, each at the start of its change
        Assert.Equal("# Title\n\ntext\n", patched);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(DiagnosticKind.Formatting, diagnostic.Kind));
        Assert.Equal(
            [("heading-space", 1, 2), ("heading-blank-lines", 2, 1), ("trailing-whitespace", 2, 5)],
            result.Diagnostics.Select(diagnostic => (diagnostic.RuleId, diagnostic.Line, diagnostic.Column)));
    }

    /// <summary>
    ///     Test that a canonical document, and the result of formatting a document, produce no change and no problem.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_CanonicalDocumentAndOwnResult_ProposeNothing()
    {
        // Arrange: a document that breaks several rules, its result, and a hand-written canonical document
        const string dirty = "Title\n=====\ntext  \n- a\n\n\n\n~~~\ncode\n~~~\n    indented\n***\n";
        const string canonical = "# Title\n\ntext\n\n- a\n\n```\ncode\n```\n";
        var (_, once) = FormatShipped(dirty);

        // Act: format the result and the canonical document
        var (onceResult, twice) = FormatShipped(once);
        var (canonicalResult, unchanged) = FormatShipped(canonical);

        // Assert: neither proposes an edit or reports a problem
        Assert.Empty(onceResult.Edits);
        Assert.Empty(onceResult.Diagnostics);
        Assert.Equal(once, twice);
        Assert.Empty(canonicalResult.Edits);
        Assert.Empty(canonicalResult.Diagnostics);
        Assert.Equal(canonical, unchanged);
    }

    /// <summary>
    ///     Test that bullet list items are written with the configured marker character.
    /// </summary>
    [Theory]
    [InlineData("* a\n* b\n", "- a\n- b\n", "Dash")]
    [InlineData("- a\n- b\n", "* a\n* b\n", "Asterisk")]
    [InlineData("- a\n  - b\n", "+ a\n  + b\n", "Plus")]
    public void FormattingSubsystem_Format_BulletMarkers_UseTheConfiguredCharacter(string input, string expected, string bullet)
    {
        // Act: format with the shipped formatters
        var (result, patched) = FormatShipped(input, LintMarkConfig.Default with { Bullet = Enum.Parse<BulletMarker>(bullet) });

        // Assert: the markers are rewritten and reported under the marker rule
        Assert.Equal(expected, patched);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "bullet");
    }

    /// <summary>
    ///     Test that the items of an ordered list are numbered the configured way, keeping the number the list starts at.
    /// </summary>
    [Theory]
    [InlineData("1. a\n1. b\n", "1. a\n2. b\n", "Incrementing")]
    [InlineData("5. a\n5. b\n", "5. a\n6. b\n", "Incrementing")]
    [InlineData("1. a\n2. b\n", "1. a\n1. b\n", "One")]
    public void FormattingSubsystem_Format_OrderedNumbers_FollowTheConfiguredStyle(string input, string expected, string style)
    {
        // Act: format with the shipped formatters
        var (result, patched) = FormatShipped(input, LintMarkConfig.Default with { OrderedStyle = Enum.Parse<OrderedStyle>(style) });

        // Assert: the numbers are rewritten and reported under the numbering rule
        Assert.Equal(expected, patched);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "ordered-style");
    }

    /// <summary>
    ///     Test that the items of a list start in the same column, that nested bullets are indented as configured and
    ///     that one space follows each marker.
    /// </summary>
    [Theory]
    [InlineData("  - a\n  - b\n", "- a\n- b\n", 2, "list-indent")]
    [InlineData("- a\n    - b\n", "- a\n  - b\n", 2, "list-indent")]
    [InlineData("- a\n  - b\n", "- a\n    - b\n", 4, "list-indent")]
    [InlineData("-   a\n-   b\n", "- a\n- b\n", 2, "list-marker-space")]
    public void FormattingSubsystem_Format_ListColumnsAndSpacing_AreCanonical(string input, string expected, int indent, string ruleId)
    {
        // Act: format with the shipped formatters
        var (result, patched) = FormatShipped(input, LintMarkConfig.Default with { ListIndent = indent });

        // Assert: the columns are rewritten and reported under the rule that owns them
        Assert.Equal(expected, patched);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == ruleId);
    }

    /// <summary>
    ///     Test that one space follows each block quote marker.
    /// </summary>
    [Theory]
    [InlineData(">  a\n", "> a\n")]
    [InlineData(">   # h\n", "> # h\n")]
    [InlineData(">  >  a\n", "> > a\n")]
    public void FormattingSubsystem_Format_QuoteMarkers_AreFollowedByOneSpace(string input, string expected) =>
        AssertShippedRule(input, expected, "blockquote-space");

    /// <summary>
    ///     Test that emphasis and strong emphasis use the configured delimiter.
    /// </summary>
    [Theory]
    [InlineData("_a_ __b__\n", "*a* **b**\n", "Asterisk", "emphasis")]
    [InlineData("*a* **b**\n", "_a_ __b__\n", "Underscore", "strong")]
    public void FormattingSubsystem_Format_EmphasisDelimiters_UseTheConfiguredCharacter(string input, string expected, string delimiter, string ruleId)
    {
        // Act: format with the shipped formatters
        var marker = Enum.Parse<EmphasisMarker>(delimiter);
        var (result, patched) = FormatShipped(input, LintMarkConfig.Default with { Emphasis = marker, Strong = marker });

        // Assert: the delimiters are rewritten and reported under the rule of their kind
        Assert.Equal(expected, patched);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == ruleId);
    }

    /// <summary>
    ///     Test that a bare URL is wrapped in angle brackets by default and left as written when that is configured.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_BareUrls_AreWrappedUnlessConfiguredOtherwise()
    {
        // Act: format with the default and with the setting that leaves URLs
        var (wrapped, patched) = FormatShipped("see http://x.com now\n");
        var (left, unchanged) = FormatShipped("see http://x.com now\n", LintMarkConfig.Default with { BareUrls = BareUrlStyle.Leave });

        // Assert: wrapped and reported, or untouched and silent
        Assert.Equal("see <http://x.com> now\n", patched);
        Assert.Contains(wrapped.Diagnostics, diagnostic => diagnostic.RuleId == "bare-urls");
        Assert.Equal("see http://x.com now\n", unchanged);
        Assert.Empty(left.Diagnostics);
    }

    /// <summary>
    ///     Test that two bullet lists that differ only by their marker stay two lists and a nested list stays nested,
    ///     whichever bullet is configured.
    /// </summary>
    [Theory]
    [InlineData("Dash", "- a\n\n* b\n\n+ c\n\n- d\n  - e\n  - f\n")]
    [InlineData("Asterisk", "- a\n\n* b\n\n+ c\n\n- d\n  * e\n  * f\n")]
    [InlineData("Plus", "- a\n\n* b\n\n+ c\n\n- d\n  + e\n  + f\n")]
    public void FormattingSubsystem_Format_BulletLists_AreNeverMergedOrSplit(string bullet, string expected)
    {
        // Arrange: three lists that are lists of their own only because their markers differ, and a list with a nested list
        const string input = "- a\n\n* b\n\n+ c\n\n- d\n  * e\n  * f\n";

        // Act: format with the shipped formatters
        var (_, patched) = FormatShipped(input, LintMarkConfig.Default with { Bullet = Enum.Parse<BulletMarker>(bullet) });

        // Assert: the exact text, and the lists at the margin are still four lists with the nested list inside the last
        Assert.Equal(expected, patched);
        var lists = Parse(patched).Blocks.Where(block => block.Kind == BlockKind.List).ToList();
        Assert.Equal(4, lists.Count(block => block.Parent is null));
        Assert.Equal(1, lists.Count(block => block.Parent is not null));
    }

    /// <summary>
    ///     Test that every line and block of a list item stays in its item when the item moves: a continuation line, a
    ///     second paragraph, a nested list and fenced code, a number that gains a digit, and items in different columns.
    /// </summary>
    [Theory]
    [InlineData("-   a\n    more\n", "- a\n  more\n")]
    [InlineData("-   a\n\n    second\n\n-   b\n", "- a\n\n  second\n\n- b\n")]
    [InlineData("-   a\n    - b\n\n    ```\n    code\n    ```\n", "- a\n  - b\n\n  ```\n  code\n  ```\n")]
    [InlineData("1. a\n1. b\n1. c\n1. d\n1. e\n1. f\n1. g\n1. h\n1. i\n1. j\n   more\n", "1. a\n2. b\n3. c\n4. d\n5. e\n6. f\n7. g\n8. h\n9. i\n10. j\n    more\n")]
    [InlineData("  - a\n    more\n  - b\n    other\n", "- a\n  more\n- b\n  other\n")]
    public void FormattingSubsystem_Format_ListItemsWithContent_StayInTheirItems(string input, string expected)
    {
        // Act: format with the shipped formatters
        var (_, patched) = FormatShipped(input);

        // Assert: the exact text, and the formatted document has the same items with the same content
        Assert.Equal(expected, patched);
        Assert.Equal(
            Parse(input).Blocks.Count(block => block.Kind == BlockKind.ListItem),
            Parse(patched).Blocks.Count(block => block.Kind == BlockKind.ListItem));
    }

    /// <summary>
    ///     Test that emphasis inside a list item and a block quote uses the configured delimiter and is reported.
    /// </summary>
    [Theory]
    [InlineData("- _a_\n", "- *a*\n", "Asterisk")]
    [InlineData("1. x _a_ y\n", "1. x *a* y\n", "Asterisk")]
    [InlineData("> _a_\n", "> *a*\n", "Asterisk")]
    [InlineData("- *a*\n", "- _a_\n", "Underscore")]
    [InlineData("> *a*\n", "> _a_\n", "Underscore")]
    public void FormattingSubsystem_Format_EmphasisInListsAndQuotes_UsesTheConfiguredCharacter(string input, string expected, string delimiter)
    {
        // Act: format with the shipped formatters
        var marker = Enum.Parse<EmphasisMarker>(delimiter);
        var (result, patched) = FormatShipped(input, LintMarkConfig.Default with { Emphasis = marker, Strong = marker });

        // Assert: the delimiters are rewritten and reported under the emphasis rule
        Assert.Equal(expected, patched);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId == "emphasis");
    }

    /// <summary>
    ///     Test that a document with lists, quotes and inline text is stable after one formatting.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_ListsQuotesAndInlineText_AreStableAfterOneRun()
    {
        // Arrange: a document that breaks the list, quote and inline rules
        const string dirty = "* _a_ http://x.com\n*   b\n\n1. c\n1. d\n\n>  __e__\n";

        // Act: format the document and then the result
        var (_, once) = FormatShipped(dirty);
        var (second, twice) = FormatShipped(once);

        // Assert: the result is canonical and the second run proposes nothing
        Assert.Equal("- *a* <http://x.com>\n- b\n\n1. c\n2. d\n\n> **e**\n", once);
        Assert.Empty(second.Edits);
        Assert.Equal(once, twice);
    }

    /// <summary>
    ///     Test that a document is the same whether the blocks before or after a list are formatted first, because edits are ordered by position.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_ParallelCallsOnListsAndInlineText_GiveTheSameResult()
    {
        // Arrange: one shared formatter and a document with lists, quotes and inline text
        const string input = "* _a_ http://x.com\n*   b\n\n>  __e__\n\n1. c\n1. d\n";
        var formatter = Shipped();
        var expected = FormatAndPatch(formatter, Parse(input)).Patched;
        var results = new string[8];

        // Act: format on several threads at once
        BlockFormatterAssertions.RunConcurrently(results.Length, index => results[index] = FormatAndPatch(formatter, Parse(input)).Patched);

        // Assert: every thread gives the same text
        Assert.All(results, result => Assert.Equal(expected, result));
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
