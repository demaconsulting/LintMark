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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the ParagraphFormatter class wraps a paragraph only when one of its lines is wider than the line length, keeps
///     everything else as written, and reports what it cannot wrap.
/// </summary>
public class ParagraphFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly ParagraphFormatter Formatter = new();

    /// <summary>
    ///     Creates a configuration with a line length.
    /// </summary>
    /// <param name="width">The line length.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Width(int width) => LintMarkConfig.Default with { LineLength = width };

    /// <summary>
    ///     Runs the formatter on a paragraph of a text and applies the edit it proposes.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for a line length of 20.</param>
    /// <param name="paragraph">The zero-based index of the paragraph in the document.</param>
    /// <returns>The outcome and the text after the edit.</returns>
    private static (ParagraphOutcome Outcome, string Text) Run(string text, LintMarkConfig? config = null, int paragraph = 0)
    {
        var document = BlockFormatterAssertions.Parse(text);
        var block = document.Blocks.Where(candidate => candidate.Kind == BlockKind.Paragraph).ElementAt(paragraph);
        var outcome = Formatter.Format(new FormatContext(document, config ?? Width(20)), block);
        var patched = outcome.Edit is { } edit ? SourcePatcher.Apply(document.Source, [edit]) : text;
        return (outcome, patched);
    }

    /// <summary>
    ///     Asserts that a paragraph is wrapped to the expected text, that the edit is credited to the line length rule and that
    ///     wrapping the result proposes nothing.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for a line length of 20.</param>
    /// <param name="paragraph">The zero-based index of the paragraph in the document.</param>
    private static void AssertWrapped(string text, string expected, LintMarkConfig? config = null, int paragraph = 0)
    {
        var (outcome, wrapped) = Run(text, config, paragraph);
        Assert.Equal(expected, wrapped);
        Assert.NotNull(outcome.Edit);
        Assert.Equal("line-length", outcome.Edit.Value.RuleId);
        Assert.False(outcome.HasResidualOverflow);
        Assert.Null(Run(expected, config, paragraph).Outcome.Edit);
    }

    /// <summary>
    ///     Asserts that a paragraph is kept exactly as written and that nothing is reported about it.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for a line length of 20.</param>
    /// <param name="paragraph">The zero-based index of the paragraph in the document.</param>
    private static void AssertKept(string text, LintMarkConfig? config = null, int paragraph = 0)
    {
        var (outcome, kept) = Run(text, config, paragraph);
        Assert.Equal(text, kept);
        Assert.Same(ParagraphOutcome.None, outcome);
    }

    /// <summary>
    ///     Asserts that a paragraph is kept exactly as written and that its overflow is reported as one that cannot be fixed.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for a line length of 20.</param>
    private static void AssertKeptAndReported(string text, LintMarkConfig? config = null)
    {
        var (outcome, kept) = Run(text, config, 0);
        Assert.Equal(text, kept);
        Assert.Null(outcome.Edit);
        Assert.True(outcome.HasResidualOverflow);
        Assert.True(outcome.OverflowOffset >= 0);
    }

    /// <summary>
    ///     Test that a missing argument is rejected and named.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a paragraph and its context
        var document = BlockFormatterAssertions.Parse("a b\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var context = new FormatContext(document, LintMarkConfig.Default);

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => ParagraphFormatter.IsEnabled(null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not a paragraph is rejected.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_HeadingBlock_ThrowsArgumentException()
    {
        // Arrange: a heading
        var document = BlockFormatterAssertions.Parse("# a b\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading);

        // Act & Assert: the block is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentException>(() => Formatter.Format(new FormatContext(document, LintMarkConfig.Default), block));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that the formatter lists the rules that edit the text of a paragraph, each registered, in a list that cannot be changed.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_RuleIds_Read_ListsTheRulesThatEditParagraphTextAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the eleven rules, each registered, in an immutable array
        Assert.Equal(
            [
                "line-length", "trailing-whitespace", "hard-tabs", "emphasis", "strong", "bare-urls", "code-span-space",
                "list-indent", "list-marker-space", "blockquote-space", "paragraph-wrap"
            ],
            ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that a paragraph with a line wider than the line length is filled up to the width.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_OverflowingParagraph_IsFilledUpToTheWidth()
    {
        // Act: wrap seven words into lines of at most twenty columns
        var (outcome, wrapped) = Run("ant bat cat dog eel fox gnu\n");

        // Assert: the lines are filled, and the finding is at the first column beyond the width
        Assert.Equal("ant bat cat dog eel\nfox gnu\n", wrapped);
        Assert.Equal(20, outcome.OverflowOffset);
        Assert.Equal(27, outcome.OverflowWidth);
        Assert.False(outcome.HasResidualOverflow);
        Assert.Equal("line-length", outcome.Edit!.Value.RuleId);
    }

    /// <summary>
    ///     Test that a paragraph whose lines all fit is kept as written, also when its lines are short.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat\ndog eel\n")]
    [InlineData("a\nb\nc\n")]
    [InlineData("ant bat cat dog eel\n")]
    public void ParagraphFormatter_Format_ParagraphWithinTheWidth_IsKeptAsWritten(string text) => AssertKept(text);

    /// <summary>
    ///     Test that the width is measured in display columns: a line of wide characters is wrapped although it has
    ///     fewer characters than the width, and a line of combining marks is kept although it has more.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_WideAndCombiningCharacters_AreMeasuredInColumns()
    {
        // Act & Assert: eleven characters of twenty-one columns are wrapped
        AssertWrapped("日本語日本 日本語日本\n", "日本語日本\n日本語日本\n");

        // Act & Assert: thirty-one characters of sixteen columns are kept
        AssertKept("e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301 e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301e\u0301\n");
    }

    /// <summary>
    ///     Test that emoji are counted with two columns each.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_Emoji_AreCountedWithTwoColumns() =>
        AssertWrapped("😀😀😀😀😀 😀😀😀😀😀\n", "😀😀😀😀😀\n😀😀😀😀😀\n");

    /// <summary>
    ///     Test that a line that is wide only because of one link is kept, and that a link is moved whole to its own line when another word forces a break.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_SingleLongAtom_IsKeptAndAWordBeforeItIsSeparated()
    {
        // Act & Assert: one link is never reported or changed
        AssertKept("[a link](http://example.org/very/long/path/to/something/long)\n");

        // Act & Assert: a word before it is moved apart, the link stays whole
        AssertWrapped(
            "see [a link](http://example.org/very/long/path/to/something/long)\n",
            "see\n[a link](http://example.org/very/long/path/to/something/long)\n");
    }

    /// <summary>
    ///     Test that a code span with blanks, a bare URL and an autolink are never split.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_CodeSpansAndUrls_AreNeverSplit()
    {
        // Act & Assert: the code span moves whole
        AssertWrapped("ant bat cat dog `x y z w v` fox\n", "ant bat cat dog\n`x y z w v` fox\n");

        // Act & Assert: the URL moves whole
        AssertWrapped("ant bat cat dog http://example.org/a/b fox\n", "ant bat cat dog\nhttp://example.org/a/b\nfox\n");
    }

    /// <summary>
    ///     Test that a hard line break stays where it is, written with spaces or with a backslash, and that the segments are wrapped on their own.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_HardBreaks_StayWhereTheyAre()
    {
        // Act & Assert: the spaces form
        AssertWrapped("ant bat cat dog eel fox gnu  \nhen ink\n", "ant bat cat dog eel\nfox gnu  \nhen ink\n");

        // Act & Assert: the backslash form
        AssertWrapped("ant bat cat dog eel fox gnu\\\nhen ink\n", "ant bat cat dog eel\nfox gnu\\\nhen ink\n");

        // Act & Assert: a hard break right after a line that fills the width
        AssertWrapped("ant bat cat dog eel  \nfox gnu hen ink jam key\n", "ant bat cat dog eel  \nfox gnu hen ink jam\nkey\n");
    }

    /// <summary>
    ///     Test that the continuation lines of a list item are written with the indentation of the item, whatever its marker.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_ListItems_KeepTheIndentationOfTheContent()
    {
        // Act & Assert: a bullet, a two-digit number, a nested item and a task item
        AssertWrapped("- ant bat cat dog eel fox gnu\n", "- ant bat cat dog\n  eel fox gnu\n");
        AssertWrapped("10. ant bat cat dog eel fox gnu\n", "10. ant bat cat dog\n    eel fox gnu\n");
        AssertWrapped("- a\n  - bat cat dog eel fox gnu hen\n", "- a\n  - bat cat dog eel\n    fox gnu hen\n", paragraph: 1);
        AssertWrapped("- [ ] ant bat cat dog eel fox\n", "- [ ] ant bat cat\n  dog eel fox\n");
    }

    /// <summary>
    ///     Test that the continuation lines of a quote carry the quote markers, also when the quotes are nested.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_Quotes_KeepTheirMarkers()
    {
        // Act & Assert: one level and two levels
        AssertWrapped("> ant bat cat dog eel fox gnu\n", "> ant bat cat dog\n> eel fox gnu\n");
        AssertWrapped("> > ant bat cat dog eel fox\n", "> > ant bat cat dog\n> > eel fox\n");
        AssertWrapped("> - ant bat cat dog eel fox\n", "> - ant bat cat dog\n>   eel fox\n");
    }

    /// <summary>
    ///     Test that a lazy continuation line of a quote is written with the quote marker when the paragraph is wrapped.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_LazyQuoteLine_GetsTheQuoteMarker() =>
        AssertWrapped(
            "> ant bat cat dog\neel fox gnu hen ink jam\n",
            "> ant bat cat dog\n> eel fox gnu hen\n> ink jam\n");

    /// <summary>
    ///     Test that a lazy continuation line of a list item keeps the paragraph as written, and that the overflow is reported.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_LazyListLine_KeepsTheParagraphAndReportsIt() =>
        AssertKeptAndReported("- ant bat cat dog\neel fox gnu hen ink jam\n");

    /// <summary>
    ///     Test that a paragraph with a line after a hard break that starts with block syntax is kept as written and reported.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_ForcedLineStartThatIsBlockSyntax_KeepsTheParagraphAndReportsIt() =>
        AssertKeptAndReported("ant bat cat dog eel fox gnu  \n2026. hen\n");

    /// <summary>
    ///     Test that no wrapped line starts with a list marker, a number marker or a heading marker, and that the words move to the previous line instead.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel - fox gnu\n", "ant bat cat dog\neel - fox gnu\n")]
    [InlineData("ant bat cat dog eel + fox gnu\n", "ant bat cat dog\neel + fox gnu\n")]
    [InlineData("ant bat cat dog eel * fox gnu\n", "ant bat cat dog\neel * fox gnu\n")]
    [InlineData("ant bat cat dog eel 2026. fox gnu\n", "ant bat cat dog\neel 2026. fox gnu\n")]
    [InlineData("ant bat cat dog eel 1) fox gnu\n", "ant bat cat dog\neel 1) fox gnu\n")]
    [InlineData("ant bat cat dog eel # fox gnu\n", "ant bat cat dog\neel # fox gnu\n")]
    [InlineData("ant bat cat dog eel > fox gnu\n", "ant bat cat dog\neel > fox gnu\n")]
    public void ParagraphFormatter_Format_BlockSyntaxAtTheBreak_NeverStartsALine(string text, string expected) =>
        AssertWrapped(text, expected);

    /// <summary>
    ///     Test that a paragraph with a Pandoc structural line is kept as written, and nothing is reported.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel fox gnu\n\\section\nhen\n")]
    [InlineData("::: note\nant bat cat dog eel fox gnu\n")]
    [InlineData("ant bat cat dog eel fox gnu\n{=html}\n")]
    public void ParagraphFormatter_Format_PandocStructuralParagraph_IsKeptAsWritten(string text) => AssertKept(text);

    /// <summary>
    ///     Test that a paragraph with a line that starts a definition, a line block, a fancy list or an example list is kept as
    ///     written at a width that its lines exceed, and that nothing is reported.
    /// </summary>
    [Theory]
    [InlineData("Apple\n~ A fruit that is red or green and rather sweet to eat.\n")]
    [InlineData("Banana\n:   A fruit that is yellow and long and curved and sweet.\n")]
    [InlineData("| The limerick packs laughs anatomical\n| In space that is quite economical.\n|    But the good ones I've seen\n|    So seldom are clean\n")]
    [InlineData("a. First item that is quite long enough to overflow\nb. Second item that is also long enough to overflow\n")]
    [InlineData("(@) First example sentence that is long enough to overflow\n(@) Second example sentence that is long enough to overflow\n")]
    [InlineData("(a) First item that is quite long enough to overflow\n(b) Second item that is also long enough to overflow\n")]
    [InlineData("#. First item that is quite long enough to overflow\n#. Second item that is also long enough to overflow\n")]
    [InlineData("i. First item that is quite long enough to overflow\nii. Second item that is also long enough to overflow\n")]
    public void ParagraphFormatter_Format_DefinitionLineBlockOrFancyListParagraph_IsKeptAsWritten(string text)
    {
        // Act & Assert: the paragraph is left as written and unreported by the formatter and byte for byte by the whole tool
        AssertKept(text, Width(30));
        Assert.Equal(text, BlockFormatterAssertions.AssertSafeAndStable(text, Width(30)));
    }

    /// <summary>
    ///     Test that ordinary text that merely looks a little like a marker is still wrapped.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel fox gnu\nhen: ink (jam) a.b\n", "ant bat cat dog eel\nfox gnu hen: ink\n(jam) a.b\n")]
    [InlineData("ant bat cat dog eel fox gnu\nsee:~ ink\n", "ant bat cat dog eel\nfox gnu see:~ ink\n")]
    public void ParagraphFormatter_Format_TextThatLooksLikeAMarkerInTheMiddle_IsStillWrapped(string text, string expected) =>
        AssertWrapped(text, expected);

    /// <summary>
    ///     Test that a wrap never starts a line with a fancy list or example list marker.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel a. fox gnu\n", "ant bat cat dog\neel a. fox gnu\n")]
    [InlineData("ant bat cat dog eel (a) fox gnu\n", "ant bat cat dog\neel (a) fox gnu\n")]
    [InlineData("ant bat cat dog eel (@) fox gnu\n", "ant bat cat dog\neel (@) fox gnu\n")]
    public void ParagraphFormatter_Format_FancyMarkerAtTheBreak_NeverStartsALine(string text, string expected) =>
        AssertWrapped(text, expected);

    /// <summary>
    ///     Test that a paragraph inside a footnote definition is kept as written, and nothing is reported.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_FootnoteParagraph_IsKeptAsWritten() =>
        AssertKept("text[^1]\n\n[^1]: ant bat cat dog eel fox gnu hen\n", paragraph: 1);

    /// <summary>
    ///     Test that a paragraph with a tab is kept as written, and nothing is reported.
    /// </summary>
    [Theory]
    [InlineData("ant\tbat cat dog eel fox gnu hen\n")]
    [InlineData("ant bat cat dog eel fox gnu\n\then\n")]
    public void ParagraphFormatter_Format_TabInTheParagraph_IsKeptAsWritten(string text) => AssertKept(text);

    /// <summary>
    ///     Test that a paragraph is kept as written, and nothing is reported, when any rule that edits its text is suppressed in it.
    /// </summary>
    [Theory]
    [InlineData("line-length")]
    [InlineData("trailing-whitespace")]
    [InlineData("hard-tabs")]
    [InlineData("emphasis")]
    [InlineData("strong")]
    [InlineData("bare-urls")]
    [InlineData("code-span-space")]
    [InlineData("list-indent")]
    [InlineData("list-marker-space")]
    [InlineData("blockquote-space")]
    public void ParagraphFormatter_Format_SuppressedRule_KeepsTheParagraphAsWritten(string rule)
    {
        // Arrange: a region in which one rule is disabled
        var text = $"<!-- lintmark-disable {rule} -->\n\nant bat cat dog eel fox gnu\n";

        // Act & Assert: the paragraph is kept
        AssertKept(text);
    }

    /// <summary>
    ///     Test that a rule that does not edit paragraph text, when suppressed, does not keep the paragraph.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_UnrelatedRuleSuppressed_StillWraps() =>
        AssertWrapped(
            "<!-- lintmark-disable heading-style -->\n\nant bat cat dog eel fox gnu\n",
            "<!-- lintmark-disable heading-style -->\n\nant bat cat dog eel\nfox gnu\n",
            paragraph: 0);

    /// <summary>
    ///     Test that a line that cannot be shortened without block syntax at its start stays too wide, and that this is reported next to the edit.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_NoGoodBreak_ReportsTheResidualOverflow()
    {
        // Act: wrap three words around a lone dash into four columns
        var (outcome, wrapped) = Run("ant - bat\n", Width(4));

        // Assert: the dash stays on the first line, which is too wide
        Assert.Equal("ant -\nbat\n", wrapped);
        Assert.NotNull(outcome.Edit);
        Assert.True(outcome.HasResidualOverflow);

        // Act & Assert: the wrapped text proposes no edit and still reports it
        var (again, same) = Run(wrapped, Width(4));
        Assert.Equal(wrapped, same);
        Assert.Null(again.Edit);
        Assert.True(again.HasResidualOverflow);
    }

    /// <summary>
    ///     Test that a paragraph with a shape whose meaning depends on its line breaks is kept as written and its overflow is
    ///     reported: an alert marker (W-1), a line that starts with block syntax (W-2), a line break at the edge of link text
    ///     (W-3), a literal angle bracket or dollar sign across a break (W-4), extra indentation (W-5), a task marker look-alike
    ///     (W-6), a first line that is one HTML tag (W-7) and a quote marker without a blank (W-8).
    /// </summary>
    /// <param name="text">The paragraph.</param>
    [Theory]
    [InlineData("> [!NOTE] ant bat cat dog eel fox gnu\n")]
    [InlineData("> ant bat cat dog eel fox gnu\n===\n")]
    [InlineData("a [b\n](c) ant bat cat dog eel fox\n")]
    [InlineData("a [\nlink\n](u) ant bat cat dog eel fox\n")]
    [InlineData("[foo](<\nbar>) ant bat cat dog eel fox\n")]
    [InlineData("$math\ninline$ ant bat cat dog eel fox gnu\n")]
    [InlineData("ant bat cat\n  dog eel fox gnu hen\n")]
    [InlineData("- [  ] ant bat cat dog eel fox gnu\n")]
    [InlineData("<span class=\"a b\"> ant bat cat dog eel fox gnu\n")]
    [InlineData("+---+ xxxxxxxxxxxxxxxxxxx ant bat\n")]
    [InlineData("--- xxxxxxxxxxxxxxxxxxxxxx ant bat\n")]
    [InlineData(">http://a.b/c ant bat cat dog eel\n")]
    [InlineData("ant bat cat dog eel fox gnu hen\nink +---+\n")]
    [InlineData("ant bat cat dog eel fox gnu hen\nink + ----\n")]
    public void ParagraphFormatter_Format_ShapeThatDependsOnItsBreaks_KeepsTheParagraphAndReportsIt(string text) =>
        AssertKeptAndReported(text);

    /// <summary>
    ///     Test that ordinary paragraphs are still wrapped (the controls of the rules W-1 to W-8): the same shapes without the
    ///     part that depends on the line breaks.
    /// </summary>
    /// <param name="text">The paragraph.</param>
    [Theory]
    [InlineData("> [NOTE] ant bat cat dog eel fox gnu\n")]
    [InlineData("> ant bat cat dog eel fox gnu\ncat\n")]
    [InlineData("a [b c](d) ant bat cat dog eel fox\n")]
    [InlineData("a < b ant bat cat dog eel fox gnu\n")]
    [InlineData("costs $5 ant bat cat dog eel fox gnu\n")]
    [InlineData("- ant bat cat dog eel fox gnu\n  hen ink\n")]
    [InlineData("- [ ] ant bat cat dog eel fox gnu\n")]
    [InlineData("<span class=\"a b\">ant</span> bat cat dog eel fox gnu\n")]
    [InlineData("> ant bat cat dog eel fox gnu\n")]
    [InlineData("ant bat cat dog eel fox gnu hen\nink +x+\n")]
    [InlineData("ant bat cat dog eel fox gnu hen\nink + x\n")]
    public void ParagraphFormatter_Format_OrdinaryShape_IsStillWrapped(string text)
    {
        // Act
        var (outcome, wrapped) = Run(text);

        // Assert: an edit is proposed, and wrapping its result proposes nothing more
        Assert.NotNull(outcome.Edit);
        Assert.NotEqual(text, wrapped);
        Assert.Null(Run(wrapped).Outcome.Edit);
    }

    /// <summary>
    ///     Test that a quote marker without a blank, followed by a URL that the parser links only when the URL ends the line, keeps the
    ///     paragraph as written and the overflow is reported (rule W-8).
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_QuoteMarkerWithoutBlank_KeepsTheParagraphAndReportsIt()
    {
        // Arrange: a quote without a space after its marker, whose first line starts with a URL that the parser links
        // only when the URL ends the line
        var text = ">http://a.b/c " + new string('x', 24) + " yak\n";

        // Act & Assert: nothing is rewritten
        AssertKeptAndReported(text);
    }

    /// <summary>
    ///     Test that nothing is wrapped when the line length is zero or the rule is switched off, in the fill mode.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_SettingsThatDisableWrapping_KeepTheParagraph()
    {
        // Arrange: a paragraph that is too wide for twenty columns
        const string text = "ant bat cat dog eel fox gnu\n";

        // Act & Assert: no limit and the rule off keep it, and so does the paragraph-wrap rule in the fill mode
        AssertKept(text, Width(0));
        AssertKept(text, Width(20) with { Rules = LintMarkConfig.Default.Rules.SetItem("line-length", false) });
        AssertKept(text, Width(0) with { Rules = LintMarkConfig.Default.Rules.SetItem("paragraph-wrap", true) });
        Assert.False(ParagraphFormatter.IsEnabled(Width(0)));
        Assert.True(ParagraphFormatter.IsEnabled(Width(20)));
    }

    /// <summary>
    ///     Test that the same edit is proposed whatever the line endings and the byte order mark of the file.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_LineEndingsAndByteOrderMark_DoNotChangeTheEdit()
    {
        // Arrange: a paragraph in a list item, as LF text and as bytes with CRLF, with a mark, and with mixed endings
        const string text = "# T\n\n- ant bat cat dog eel fox gnu\n  hen ink\n";
        var expected = Run(text).Outcome;
        var preamble = new System.Text.UTF8Encoding(true).GetPreamble();
        byte[][] variants =
        [
            System.Text.Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal)),
            [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(text)],
            [.. preamble, .. System.Text.Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal))],
            System.Text.Encoding.UTF8.GetBytes(BlockFormatterAssertions.MixEndings(text))
        ];

        // Act & Assert: every form gives the same outcome
        Assert.NotNull(expected.Edit);
        foreach (var bytes in variants)
        {
            var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));
            var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
            Assert.Equal(expected, Formatter.Format(new FormatContext(document, Width(20)), block));
        }
    }

    /// <summary>
    ///     Test that every thread gets the same outcome for the same paragraph.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Format_ConcurrentCalls_GiveTheSameOutcome()
    {
        // Arrange: a shared document and the single-threaded outcome
        var document = BlockFormatterAssertions.Parse("> ant bat cat dog eel fox gnu `x y` [a b](c)\n> hen ink\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var context = new FormatContext(document, Width(20));
        var reference = Formatter.Format(context, block);

        // Act & Assert: every thread gets the same outcome
        Assert.NotNull(reference.Edit);
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 100; round++)
                {
                    Assert.Equal(reference, Formatter.Format(context, block));
                }
            });
    }

    /// <summary>
    ///     Test that the outcome rejects inconsistent arguments.
    /// </summary>
    [Fact]
    public void ParagraphFormatter_Outcome_InconsistentArguments_Throw()
    {
        // Act & Assert: an offset below -1, a negative width, and an edit, a residual or a width without an offset are rejected
        var edit = new TextEdit(0, 1, "x", "line-length");
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParagraphOutcome(null, -2, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParagraphOutcome(null, 0, -1, false));
        Assert.Throws<ArgumentException>(() => new ParagraphOutcome(edit, -1, 0, false));
        Assert.Throws<ArgumentException>(() => new ParagraphOutcome(null, -1, 0, true));
        Assert.Throws<ArgumentException>(() => new ParagraphOutcome(null, -1, 5, false));
        Assert.Throws<ArgumentException>(() => new ParagraphOutcome(default(TextEdit), 0, 1, false));
        Assert.Equal(-1, ParagraphOutcome.None.OverflowOffset);
    }
}
