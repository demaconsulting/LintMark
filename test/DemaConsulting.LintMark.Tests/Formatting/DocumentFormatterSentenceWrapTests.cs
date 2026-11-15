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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the sentence and semantic wrap modes lay out every paragraph canonically, report a paragraph that is not laid
///     out that way under the paragraph-wrap rule, keep the two rules independent, and stay safe and idempotent.
/// </summary>
public class DocumentFormatterSentenceWrapTests
{
    /// <summary>
    ///     Creates a configuration for a wrap mode.
    /// </summary>
    /// <param name="mode">The wrap mode.</param>
    /// <param name="width">The line length.</param>
    /// <param name="lineLength">Whether the line-length rule is on.</param>
    /// <param name="paragraphWrap">Whether the paragraph-wrap rule is on.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Config(WrapMode mode, int width = 40, bool lineLength = true, bool paragraphWrap = true) =>
        LintMarkConfig.Default with
        {
            Wrap = mode,
            LineLength = width,
            Rules = LintMarkConfig.Default.Rules.SetItem("line-length", lineLength).SetItem("paragraph-wrap", paragraphWrap)
        };

    /// <summary>
    ///     Formats a text with the shipped formatters.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The result and the patched text.</returns>
    private static (FormatResult Result, string Formatted) Format(string text, LintMarkConfig config) =>
        BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), text, config);

    /// <summary>
    ///     Test that the sentence mode puts every sentence on its own line, reports the paragraph under the paragraph-wrap rule at
    ///     the first line that differs, and that the semantic mode does the same for sentences that fit.
    /// </summary>
    /// <param name="modeName">The name of the wrap mode.</param>
    [Theory]
    [InlineData("Sentence")]
    [InlineData("Semantic")]
    public void DocumentFormatter_Format_SentencesOnOneLine_AreSplitAndReportedAsParagraphWrap(string modeName)
    {
        var mode = Enum.Parse<WrapMode>(modeName);
        // Act: format a short paragraph with three sentences
        var (result, formatted) = Format("# T\n\nOne two. Three four. Five six.\n", Config(mode, 80));

        // Assert: one sentence per line, one fixable finding on the paragraph, credited to the paragraph-wrap rule
        Assert.Equal("# T\n\nOne two.\nThree four.\nFive six.\n", formatted);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal(("paragraph-wrap", DiagnosticKind.Formatting, 3, 1), (finding.RuleId, finding.Kind, finding.Line, finding.Column));
        Assert.All(result.Edits, edit => Assert.Equal("paragraph-wrap", edit.RuleId));
        BlockFormatterAssertions.AssertSafeAndStable("# T\n\nOne two. Three four. Five six.\n", Config(mode, 80));
    }

    /// <summary>
    ///     Test that a paragraph that is already in the layout is left as written and nothing is reported, and that a paragraph whose
    ///     lines are broken in the middle of a sentence is joined.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ParagraphInTheLayout_IsLeftAsWrittenAndWrappedLinesAreJoined()
    {
        // Act & Assert: the canonical text is a fixed point, and a sentence broken by hand is joined
        const string canonical = "One two.\nThree four.\n";
        var (same, kept) = Format(canonical, Config(WrapMode.Sentence));
        Assert.Equal(canonical, kept);
        Assert.Empty(same.Edits);
        Assert.Empty(same.Diagnostics);
        Assert.Equal(canonical, Format("One\ntwo. Three\nfour.\n", Config(WrapMode.Sentence)).Formatted);
    }

    /// <summary>
    ///     Test the combinations of the line-length and paragraph-wrap rules in the sentence mode for a paragraph that is too wide and
    ///     one that is only laid out differently.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_RuleCombinationsInTheSentenceMode_AreIndependent()
    {
        // Arrange: a paragraph with a sentence that is too wide for 20 columns, and a short one on one line
        const string wide = "ant bat cat dog eel fox gnu. Hen ink.\n";
        const string wrapped = "ant bat cat dog eel\nfox gnu.\nHen ink.\n";
        const string shortText = "One. Two.\n";
        const string shortWrapped = "One.\nTwo.\n";

        // Act & Assert: both rules on - one wrap, one finding of each rule
        var (both, text) = Format(wide, Config(WrapMode.Sentence, 20));
        Assert.Equal(wrapped, text);
        Assert.Equal(["line-length", "paragraph-wrap"], both.Diagnostics.Select(finding => finding.RuleId).Order());
        Assert.All(both.Diagnostics, finding => Assert.Equal(DiagnosticKind.Formatting, finding.Kind));

        // Act & Assert: only line-length - the paragraph is wrapped in the layout of the mode and only that rule reports
        var (length, lengthText) = Format(wide, Config(WrapMode.Sentence, 20, paragraphWrap: false));
        Assert.Equal(wrapped, lengthText);
        Assert.Equal("line-length", Assert.Single(length.Diagnostics).RuleId);

        // Act & Assert: only paragraph-wrap - the same wrap, reported by the other rule
        var (layout, layoutText) = Format(wide, Config(WrapMode.Sentence, 20, lineLength: false));
        Assert.Equal(wrapped, layoutText);
        Assert.Equal("paragraph-wrap", Assert.Single(layout.Diagnostics).RuleId);

        // Act & Assert: neither - nothing happens
        var (neither, untouched) = Format(wide, Config(WrapMode.Sentence, 20, lineLength: false, paragraphWrap: false));
        Assert.Equal(wide, untouched);
        Assert.Empty(neither.Edits);
        Assert.Empty(neither.Diagnostics);

        // Act & Assert: a paragraph that fits is only a matter for paragraph-wrap
        Assert.Equal(shortWrapped, Format(shortText, Config(WrapMode.Sentence, 20)).Formatted);
        Assert.Equal(shortText, Format(shortText, Config(WrapMode.Sentence, 20, paragraphWrap: false)).Formatted);
        Assert.Equal(shortWrapped, Format(shortText, Config(WrapMode.Sentence, 20, lineLength: false)).Formatted);
        Assert.Equal(shortText, Format(shortText, Config(WrapMode.Sentence, 20, lineLength: false, paragraphWrap: false)).Formatted);
    }

    /// <summary>
    ///     Test the combinations of the rules in the semantic mode.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_RuleCombinationsInTheSemanticMode_AreIndependent()
    {
        // Arrange: one sentence of 76 columns with clauses
        const string text = "However, the first clause is here; the second clause is here, and the third.\n";
        const string expected = "However,\nthe first clause is here;\nthe second clause is here,\nand the third.\n";

        // Act & Assert: both rules on, only line-length and only paragraph-wrap give the clause layout
        Assert.Equal(expected, Format(text, Config(WrapMode.Semantic, 40)).Formatted);
        Assert.Equal(expected, Format(text, Config(WrapMode.Semantic, 40, paragraphWrap: false)).Formatted);
        Assert.Equal(expected, Format(text, Config(WrapMode.Semantic, 40, lineLength: false)).Formatted);

        // Act & Assert: the sentence mode fills the same sentence instead
        Assert.Equal(
            "However, the first clause is here; the\nsecond clause is here, and the third.\n",
            Format(text, Config(WrapMode.Sentence, 40)).Formatted);

        // Act & Assert: without a line length the sentence is not split into clauses
        Assert.Equal(text, Format(text, Config(WrapMode.Semantic, 0)).Formatted);
    }

    /// <summary>
    ///     Test that a suppression comment keeps its rule from reporting and rewriting while the other rule still works.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_SuppressedRules_AreRespectedIndependently()
    {
        // Arrange: three paragraphs, each too wide and not in the layout, with a suppression before the first and the second
        const string input =
            "<!-- markdownlint-disable-next-line paragraph-wrap -->\nant bat cat dog eel fox gnu. Hen ink.\n\n"
            + "<!-- markdownlint-disable-next-line line-length -->\nant bat cat dog eel fox gnu. Hen ink.\n\n"
            + "ant bat cat dog eel fox gnu. Hen ink.\n";

        // Act: format
        var (result, formatted) = Format(input, Config(WrapMode.Sentence, 20));

        // Assert: the first paragraph is rewritten because of the line length, the second because of its layout, the third for both
        Assert.Equal(
            "<!-- markdownlint-disable-next-line paragraph-wrap -->\nant bat cat dog eel\nfox gnu.\nHen ink.\n\n"
            + "<!-- markdownlint-disable-next-line line-length -->\nant bat cat dog eel\nfox gnu.\nHen ink.\n\n"
            + "ant bat cat dog eel\nfox gnu.\nHen ink.\n",
            formatted);
        Assert.Equal(
            [("line-length", 2), ("paragraph-wrap", 5), ("line-length", 7), ("paragraph-wrap", 7)],
            result.Diagnostics.Select(finding => (finding.RuleId, finding.Line)).OrderBy(pair => pair.Line).ThenBy(pair => pair.RuleId));

        // Act & Assert: suppressing both leaves the paragraph alone
        const string both = "<!-- markdownlint-disable line-length paragraph-wrap -->\n\nOne. Two three four five six seven.\n";
        Assert.Equal(both, Format(both, Config(WrapMode.Sentence, 20)).Formatted);
    }

    /// <summary>
    ///     Test that a suppression of another rule that edits the text of a paragraph keeps the paragraph as written.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_OtherRuleSuppressed_KeepsTheParagraph()
    {
        // Arrange: a paragraph with two sentences and an emphasis rule that is suppressed in it
        const string input = "<!-- markdownlint-disable emphasis -->\n\nOne _two_. Three four.\n";

        // Act & Assert: left as written
        Assert.Equal(input, Format(input, Config(WrapMode.Sentence)).Formatted);
    }

    /// <summary>
    ///     Test the layout of sentences in lists, quotes, hard breaks, links, code spans, abbreviations, numbers and East Asian text.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text in the sentence mode at 40 columns.</param>
    [Theory]
    [InlineData("- First sentence. Second sentence.\n- Third.\n", "- First sentence.\n  Second sentence.\n- Third.\n")]
    [InlineData("1. First sentence. Second sentence.\n2. Third.\n", "1. First sentence.\n   Second sentence.\n2. Third.\n")]
    [InlineData("> First. Second.\n", "> First.\n> Second.\n")]
    [InlineData("> - Ab cd. Ef gh.\n", "> - Ab cd.\n>   Ef gh.\n")]
    [InlineData("- Item one.\n  > Quote. More.\n", "- Item one.\n  > Quote.\n  > More.\n")]
    [InlineData("One. Two.  \nThree. Four.\n", "One.\nTwo.  \nThree.\nFour.\n")]
    [InlineData("One. Two.\\\nThree. Four.\n", "One.\nTwo.\\\nThree.\nFour.\n")]
    [InlineData("See [Fig. 3](http://x.y/a.b). Then more.\n", "See [Fig. 3](http://x.y/a.b).\nThen more.\n")]
    [InlineData("Use `a. B` here. Then.\n", "Use `a. B` here.\nThen.\n")]
    [InlineData("Ask Dr. Smith, e.g. Bob. Then go.\n", "Ask Dr. Smith, e.g. Bob.\nThen go.\n")]
    [InlineData("Wait... Then go. See Fig. 3 and U.S. law.\n", "Wait... Then go.\nSee Fig. 3 and U.S. law.\n")]
    [InlineData("Count them. 1. Then 2. Then.\n", "Count them. 1. Then 2. Then.\n")]
    [InlineData("你好。 世界。\n", "你好。\n世界。\n")]
    [InlineData("这是一个很长的句子。这是另一个。\n", "这是一个很长的句子。这是另一个。\n")]
    [InlineData("It works.[^1] Next one.\n\n[^1]: Note. More.\n", "It works.[^1]\nNext one.\n\n[^1]: Note. More.\n")]
    [InlineData("Done. <b>bold</b> then. Fine.\n", "Done. <b>bold</b> then.\nFine.\n")]
    [InlineData("# One. Two.\n\nText. More.\n\n```\nA. B.\n```\n", "# One. Two.\n\nText.\nMore.\n\n```\nA. B.\n```\n")]
    public void DocumentFormatter_Format_SentenceMode_LaysOutSentencesInContext(string input, string expected)
    {
        // Act & Assert: the sentence layout, which is safe, renders the same and is stable
        Assert.Equal(expected, BlockFormatterAssertions.AssertSafeAndStable(input, Config(WrapMode.Sentence)));
    }

    /// <summary>
    ///     Test that paragraphs that must be left as written are left as written in the sentence mode, and nothing is reported.
    /// </summary>
    /// <param name="input">The text.</param>
    [Theory]
    [InlineData("Apple\n~ A fruit. Sweet.\n")]
    [InlineData("| One. Two.\n| Three. Four.\n")]
    [InlineData("::: note\nOne. Two.\n:::\n")]
    [InlineData("a. One. Two.\nb. Three.\n")]
    [InlineData("\\newpage One. Two.\n")]
    public void DocumentFormatter_Format_PandocOrTabParagraphInTheSentenceMode_IsLeftAsWritten(string input)
    {
        // Act & Assert: byte for byte
        var (result, formatted) = Format(input, Config(WrapMode.Sentence));
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a lazy continuation line in a list item is reported as a paragraph that cannot be laid out, not rewritten.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_LazyLineInListItemInTheSentenceMode_IsNotRewritten()
    {
        // Act: format a list item whose second line is lazy and holds a sentence end
        const string input = "- One two. Three\nfour five.\n";
        var (result, formatted) = Format(input, Config(WrapMode.Sentence));

        // Assert: unchanged and no finding (the layout of a paragraph that cannot be rewritten is not reported)
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
    }

    /// <summary>
    ///     Test that both modes are safe and stable for a set of paragraphs, at several widths.
    /// </summary>
    /// <param name="modeName">The name of the wrap mode.</param>
    /// <param name="width">The line length.</param>
    [Theory]
    [InlineData("Sentence", 0)]
    [InlineData("Sentence", 20)]
    [InlineData("Sentence", 40)]
    [InlineData("Semantic", 0)]
    [InlineData("Semantic", 20)]
    [InlineData("Semantic", 40)]
    [InlineData("Semantic", 80)]
    public void DocumentFormatter_Format_BothModes_AreSafeAndStable(string modeName, int width)
    {
        var mode = Enum.Parse<WrapMode>(modeName);
        // Arrange: a document with the constructs that make wrapping risky
        const string input =
            "# Title. With sentences.\n\n"
            + "Setext risk. Next line follows. Then a plain sentence, with a clause; and another clause: done!\n"
            + "- not a list. 2026. Is it? Yes. Maybe.\n\n"
            + "- Item one, with a clause. Second sentence in the item, which is rather long and has several words in it.\n"
            + "  - Nested item. Another sentence here; with a clause.\n\n"
            + "> A quote, with text. Another quote sentence that is long enough to wrap around the limit. End.\n"
            + "> > Nested quote. Deeper.\n\n"
            + "A [link, with comma](http://example.com/a,b). And `code, with comma`. Then ![image. alt](i.png). Done.\n\n"
            + "Hard break here. Yes.  \nAfter it. Again.\\\nLast. One.\n\n"
            + "Dr. Smith vs. Mr. Jones e.g. in the U.S. at 3.5 p.m. on Fig. 2. See No. 5.\n\n"
            + "日本語の文章です。 これは二番目です。 English after. 한국어 문장. 끝.\n\n"
            + "Ends with a long URL http://example.com/a/very/long/path/that/never/ends/anywhere. Then more.\n";

        // Act & Assert: safe, equivalent and stable
        BlockFormatterAssertions.AssertSafeAndStable(input, Config(mode, width));
    }

    /// <summary>
    ///     Test that the sentence mode keeps a paragraph that is not wrapped per the layout as written, and reports it as a finding that
    ///     cannot be fixed, when the wrap would start the paragraph with a complete HTML tag (rule W-7).
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_WrapThatWouldOpenAnHtmlBlock_IsReportedAsParagraphWrapThatCannotBeFixed()
    {
        // Arrange: a formatter with the paragraph formatter, and a first sentence whose first 18 columns are one tag
        var formatter = new DocumentFormatter(
            [new DemaConsulting.LintMark.Formatting.Blocks.HeadingFormatter()],
            null,
            new DemaConsulting.LintMark.Formatting.Blocks.ParagraphFormatter());
        const string input = "<span class=\"a b\"> One. Two.\n";

        // Act: format it into 18 columns
        var (result, formatted) = BlockFormatterAssertions.Apply(formatter, input, Config(WrapMode.Sentence, 18));

        // Assert: unchanged, one lint-only finding
        Assert.Equal(input, formatted);
        var finding = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.RuleId == "paragraph-wrap");
        Assert.Equal(DiagnosticKind.LintOnly, finding.Kind);
        Assert.Contains("cannot be wrapped automatically", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the layout depends on the paragraph only: the same words laid out differently by hand give the same result.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_DifferentHandLayouts_GiveTheSameResult()
    {
        // Arrange: the same words in three layouts
        string[] inputs = ["One two. Three four.\n", "One\ntwo.\nThree four.\n", "One two.\n\nThree four.\n"];

        // Act: format them
        var results = inputs.Select(input => Format(input, Config(WrapMode.Semantic)).Formatted).ToArray();

        // Assert: the first two are the same paragraph, and the third has two paragraphs that are already in the layout
        Assert.Equal(results[0], results[1]);
        Assert.Equal("One two.\nThree four.\n", results[0]);
        Assert.Equal(inputs[2], results[2]);
    }
}
