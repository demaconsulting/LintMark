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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the Formatting subsystem fills the paragraphs that are wider than the line length, and only those, with the
///     production formatter, and that the inline code span rule works next to it. Every rewrite is checked for the rendered page,
///     the Pandoc structure and stability.
/// </summary>
public class FormattingSubsystemWrapTests
{
    /// <summary>
    ///     Creates a configuration with a line length.
    /// </summary>
    /// <param name="width">The line length.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Width(int width) => LintMarkConfig.Default with { LineLength = width };

    /// <summary>
    ///     Formats a text with the production formatter and a line length, and checks every property of a safe, stable rewrite.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="width">The line length.</param>
    private static void AssertWraps(string input, string expected, int width = 20) =>
        BlockFormatterAssertions.AssertFormats(input, expected, Width(width));

    /// <summary>
    ///     Asserts that a text produces no edit and no finding with a configuration.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="config">The configuration.</param>
    private static void AssertSilent(string input, LintMarkConfig config)
    {
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, config);
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a line that is wider than the line length and can be broken is reported at the first column beyond the width.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_OverlongParagraph_ReportsLineLengthAtTheFirstColumnBeyondTheWidth()
    {
        // Act: format a paragraph of 27 columns with a limit of 20
        var (result, _) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), "# T\n\nant bat cat dog eel fox gnu\n", Width(20));

        // Assert: one fixable line-length finding at line 3, column 21
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal(("line-length", DiagnosticKind.Formatting, 3, 21), (finding.RuleId, finding.Kind, finding.Line, finding.Column));
        Assert.Equal("Line is 27 columns wide; the limit is 20", finding.Message);
    }

    /// <summary>
    ///     Test that a paragraph with a line that is too wide is filled up to the width.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_OverlongParagraph_IsFilledUpToTheWidth() =>
        AssertWraps(
            "ant bat cat dog eel fox gnu hen ink jam key log\n",
            "ant bat cat dog eel\nfox gnu hen ink jam\nkey log\n");

    /// <summary>
    ///     Test that paragraphs in which no line is too wide are kept byte for byte, whether their lines are full, short or exactly as wide as the limit.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel\n")]
    [InlineData("ant bat cat dog else\n")]
    [InlineData("a\nbat cat\nd\n")]
    [InlineData("ant bat cat\ndog eel fox\ngnu hen ink\n")]
    public void FormattingSubsystem_Format_ParagraphsWithinTheWidth_StayByteIdentical(string input) =>
        AssertWraps(input, input);

    /// <summary>
    ///     Test that the line length decides what is reported and wrapped: the same text is left at 120 columns and wrapped at 40.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_ConfiguredWidth_DecidesWhatIsReported()
    {
        // Arrange: a paragraph of 99 columns and one of 124 columns
        var ninetyNine = string.Join(' ', Enumerable.Repeat("word", 20));
        var longer = string.Join(' ', Enumerable.Repeat("word", 25));
        var input = $"{ninetyNine}\n\n{longer}\n";

        // Act & Assert: the default is 120 columns, so only the second paragraph changes
        var (defaultResult, defaultText) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input);
        Assert.Equal($"{ninetyNine}\n\n{string.Join(' ', Enumerable.Repeat("word", 24))}\nword\n", defaultText);
        Assert.Equal(3, Assert.Single(defaultResult.Diagnostics).Line);

        // Act & Assert: with 40 columns both change
        var (narrowResult, _) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Width(40));
        Assert.Equal([1, 3], narrowResult.Diagnostics.Select(finding => finding.Line));
        BlockFormatterAssertions.AssertSafeAndStable(input, Width(40));
    }

    /// <summary>
    ///     Test that a line length of zero reports and changes nothing.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WidthZero_ReportsAndChangesNothing() =>
        AssertSilent(string.Join(' ', Enumerable.Repeat("word", 80)) + "\n", Width(0));

    /// <summary>
    ///     Test that switching the line length rule off reports and changes nothing.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_LineLengthRuleOff_ReportsAndChangesNothing() =>
        AssertSilent(
            "ant bat cat dog eel fox gnu\n",
            Width(20) with { Rules = LintMarkConfig.Default.Rules.SetItem("line-length", false) });

    /// <summary>
    ///     Test that a line that is wide only because of one link, URL, autolink, code span or image is neither reported nor changed.
    /// </summary>
    [Theory]
    [InlineData("[a link](http://example.org/very/long/path/to/x)\n")]
    [InlineData("<http://example.org/very/long/path/to/something>\n")]
    [InlineData("`a code span with spaces in it`\n")]
    [InlineData("![an image](http://example.org/image.png)\n")]
    [InlineData("- [a link](http://example.org/very/long/path/to/x)\n")]
    [InlineData("> <http://example.org/very/long/path/to/something>\n")]
    public void FormattingSubsystem_Format_LineWithOneLongUnbreakableItem_IsNotReportedOrChanged(string input) =>
        AssertSilent(input, Width(20));

    /// <summary>
    ///     Test that code, HTML, tables, headings, link reference definitions and front matter that are wider than the line length are never reported or changed.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_LiteralBlocksWiderThanTheWidth_AreNeverReported()
    {
        // Arrange: one block of each literal kind, every line far wider than twenty columns
        const string input =
            "---\ntitle: a long title with many words in it here\n---\n\n"
            + "# a heading with many words in it that is long\n\n"
            + "```text\ncode line with many words in it that is long\n```\n\n"
            + "<div>html block with many words in it that is long</div>\n\n"
            + "| column one is long | column two is long |\n| ------------------ | ------------------ |\n| cell with many words in it | another cell with many words |\n\n"
            + "[ref]: http://example.org/very/long/path/to/something \"a long title here\"\n";

        // Act & Assert: nothing is reported and nothing changes
        AssertSilent(input, Width(20));
    }

    /// <summary>
    ///     Test that a wrapped paragraph keeps its links, code spans, inline HTML, math, footnote references, attributes and citations whole.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WrappedParagraph_KeepsInlineConstructsWhole()
    {
        // Arrange: a paragraph with every kind of construct that has a blank inside it or must not be split
        string[] constructs = ["[bb cc](dd)", "`ee ff`", "<span a=\"b c\">gg</span>", "$hh ii$", "jj[^1]", "{#kk ll}", "[@mm nn]", "&amp;"];
        var input = "ant " + string.Join(" xxxx ", constructs) + " oo\n\n[^1]: note\n";

        // Act: format with a narrow width
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input, Width(20));

        // Assert: every construct is on one line, and the words are the same
        Assert.All(constructs, construct => Assert.Contains(construct, formatted, StringComparison.Ordinal));
        Assert.NotEqual(input, formatted);
        Assert.Equal(
            input.Replace("\n", " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries),
            formatted.Replace("\n", " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     Test that a hard line break, written with spaces or with a backslash, stays where it is when the paragraph is wrapped.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WrappedParagraph_KeepsHardBreaks()
    {
        // Act & Assert: the spaces form and the backslash form
        AssertWraps("ant bat cat dog eel fox gnu  \nhen ink\n", "ant bat cat dog eel\nfox gnu  \nhen ink\n");
        AssertWraps("ant bat cat dog eel fox gnu\\\nhen ink\n", "ant bat cat dog eel\nfox gnu\\\nhen ink\n");
    }

    /// <summary>
    ///     Test that every wrapped line of a list item or a quote carries the prefix that keeps it in the item or the quote.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WrappedListAndQuoteParagraphs_KeepTheirPrefixes()
    {
        // Act & Assert: bullet, number, nested item, quote, nested quote, quote with a list and a lazy quote line
        AssertWraps("- ant bat cat dog eel fox gnu\n", "- ant bat cat dog\n  eel fox gnu\n");
        AssertWraps("10. ant bat cat dog eel fox gnu\n", "10. ant bat cat dog\n    eel fox gnu\n");
        AssertWraps("- a\n  - bat cat dog eel fox gnu hen\n", "- a\n  - bat cat dog eel\n    fox gnu hen\n");
        AssertWraps("> ant bat cat dog eel fox gnu\n", "> ant bat cat dog\n> eel fox gnu\n");
        AssertWraps("> > ant bat cat dog eel fox\n", "> > ant bat cat dog\n> > eel fox\n");
        AssertWraps("> - ant bat cat dog eel fox\n", "> - ant bat cat dog\n>   eel fox\n");
        AssertWraps("> ant bat cat dog\neel fox gnu hen ink jam\n", "> ant bat cat dog\n> eel fox gnu hen\n> ink jam\n");
    }

    /// <summary>
    ///     Test that no wrapped line starts with text that Markdown reads as another block.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel - fox gnu\n", "ant bat cat dog\neel - fox gnu\n")]
    [InlineData("ant bat cat dog eel + fox gnu\n", "ant bat cat dog\neel + fox gnu\n")]
    [InlineData("ant bat cat dog eel * fox gnu\n", "ant bat cat dog\neel * fox gnu\n")]
    [InlineData("ant bat cat dog eel 2026. fox gnu\n", "ant bat cat dog\neel 2026. fox gnu\n")]
    [InlineData("ant bat cat dog eel 1. fox gnu\n", "ant bat cat dog\neel 1. fox gnu\n")]
    [InlineData("ant bat cat dog eel 1) fox gnu\n", "ant bat cat dog\neel 1) fox gnu\n")]
    [InlineData("ant bat cat dog eel # fox gnu\n", "ant bat cat dog\neel # fox gnu\n")]
    [InlineData("ant bat cat dog eel > fox gnu\n", "ant bat cat dog\neel > fox gnu\n")]
    [InlineData("ant bat cat dog eel === fox gnu\n", "ant bat cat dog\neel === fox gnu\n")]
    [InlineData("ant bat cat dog eel --- fox gnu\n", "ant bat cat dog\neel --- fox gnu\n")]
    [InlineData("ant bat cat dog eel ``` fox gnu\n", "ant bat cat dog\neel ``` fox gnu\n")]
    [InlineData("ant bat cat dog eel [x]: fox gnu\n", "ant bat cat dog\neel [x]: fox gnu\n")]
    [InlineData("ant bat cat dog eel <div> fox gnu\n", "ant bat cat dog\neel <div> fox gnu\n")]
    [InlineData("ant bat cat dog eel | fox gnu\n", "ant bat cat dog\neel | fox gnu\n")]
    [InlineData("ant bat cat dog eel ::: fox gnu\n", "ant bat cat dog\neel ::: fox gnu\n")]
    public void FormattingSubsystem_Format_WrappedParagraph_NeverStartsALineWithBlockSyntax(string input, string expected) =>
        AssertWraps(input, expected);

    /// <summary>
    ///     Test that a paragraph with a Pandoc structural line is left as written.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel fox gnu\n\\section\nhen\n")]
    [InlineData("::: note\nant bat cat dog eel fox gnu\n")]
    [InlineData("ant bat cat dog eel fox gnu\n{=html}\n")]
    public void FormattingSubsystem_Format_PandocStructuralParagraph_IsLeftAsWritten(string input) =>
        AssertWraps(input, input);

    /// <summary>
    ///     Test that a paragraph inside a footnote definition is left as written.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_FootnoteParagraph_IsLeftAsWritten()
    {
        // Arrange: a footnote whose paragraph is wider than the limit
        const string input = "text[^1]\n\n[^1]: ant bat cat dog eel fox gnu hen\n";

        // Act & Assert: nothing changes and nothing is reported about the footnote
        AssertSilent(input, Width(20));
    }

    /// <summary>
    ///     Test that a line that could not be shortened is reported as a problem that rewriting cannot fix, also after a rewrite.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WrapThatCannotFit_ReportsLineLengthThatCannotBeFixed()
    {
        // Arrange: a lazy continuation line in a list item, which is never rewritten
        const string input = "- ant bat cat dog\neel fox gnu hen ink jam\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Width(20));

        // Assert: unchanged, with one finding that is not a formatting violation
        Assert.Equal(input, formatted);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal(("line-length", DiagnosticKind.LintOnly, 2, 21), (finding.RuleId, finding.Kind, finding.Line, finding.Column));
        BlockFormatterAssertions.AssertSafeAndStable(input, Width(20));
    }

    /// <summary>
    ///     Test that the sentence and semantic wrap modes are accepted and change nothing.
    /// </summary>
    [Theory]
    [InlineData("Sentence")]
    [InlineData("Semantic")]
    public void FormattingSubsystem_Format_ReservedWrapModes_AreAcceptedAndChangeNothing(string mode) =>
        AssertSilent("ant bat cat dog eel fox gnu. Hen ink jam.\n", Width(20) with { Wrap = Enum.Parse<WrapMode>(mode) });

    /// <summary>
    ///     Test that formatting a wrapped text proposes nothing more, also when other rules change the text in the same run.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_WrappedParagraphs_ProposeNothingMoreWhenFormattedAgain()
    {
        // Arrange: lists with a marker that changes, a quote with extra spaces, underscore emphasis and a padded code span
        const string input =
            "*   ant bat cat _ddd_ eel fox gnu\n    hen ink\n*   x\n\n"
            + ">   jam key log ` map ` nut owl pig quo\n\n"
            + "rat sun toy urn van web fix yak zoo\n";

        // Act: format, then format the result with the production formatter
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var (first, formatted) = BlockFormatterAssertions.Apply(formatter, input, Width(20));
        var (second, _) = BlockFormatterAssertions.Apply(formatter, formatted, Width(20));

        // Assert: the first run changes things, the second proposes and reports nothing
        Assert.NotEmpty(first.Edits);
        Assert.Empty(second.Edits);
        Assert.Empty(second.Diagnostics);
        BlockFormatterAssertions.AssertSafeAndStable(input, Width(20));
    }

    /// <summary>
    ///     Test that one space on each side of a code span is removed and reported.
    /// </summary>
    [Fact]
    public void FormattingSubsystem_Format_PaddedCodeSpan_LosesItsPadding()
    {
        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), "a ` x ` b\n");

        // Assert: the padding is gone, and one code-span-space finding at the code span
        Assert.Equal("a `x` b\n", formatted);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal(("code-span-space", DiagnosticKind.Formatting, 1, 4), (finding.RuleId, finding.Kind, finding.Line, finding.Column));
        BlockFormatterAssertions.AssertSafeAndStable("a ` x ` b\n");
    }

    /// <summary>
    ///     Test that the padding of a code span whose content starts or ends with a backtick is kept.
    /// </summary>
    [Theory]
    [InlineData("a `` `x `` b\n")]
    [InlineData("a `` x` `` b\n")]
    public void FormattingSubsystem_Format_CodeSpanWithBacktickAtAnEdge_KeepsItsPadding(string input) =>
        AssertSilent(input, LintMarkConfig.Default);

    /// <summary>
    ///     Test that a code span with padding on one side only, or with more than one space, is kept.
    /// </summary>
    [Theory]
    [InlineData("a ` x` b\n")]
    [InlineData("a `x ` b\n")]
    [InlineData("a `  x  ` b\n")]
    public void FormattingSubsystem_Format_AsymmetricOrDoublePadding_IsKept(string input) =>
        AssertSilent(input, LintMarkConfig.Default);

    /// <summary>
    ///     Test that a code span that holds only spaces is kept.
    /// </summary>
    [Theory]
    [InlineData("a ` ` b\n")]
    [InlineData("a `  ` b\n")]
    public void FormattingSubsystem_Format_CodeSpanOfSpaces_IsKept(string input) =>
        AssertSilent(input, LintMarkConfig.Default);
}
