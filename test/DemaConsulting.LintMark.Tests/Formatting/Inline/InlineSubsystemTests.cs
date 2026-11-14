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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the inline subsystem works inside the production formatter: emphasis, strong emphasis and bare URLs of the
///     paragraphs that no block formatter claims are written canonically, and everything the subsystem does not own keeps its text.
/// </summary>
public class InlineSubsystemTests
{
    /// <summary>
    ///     Test that emphasis and strong emphasis written with underscores are written with asterisks.
    /// </summary>
    [Theory]
    [InlineData("_a_\n", "*a*\n")]
    [InlineData("__a__\n", "**a**\n")]
    [InlineData("A _b_ and __c__.\n", "A *b* and **c**.\n")]
    public void InlineSubsystem_Format_UnderscoreDelimiters_BecomeAsterisksByDefault(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that emphasis and strong emphasis are written with underscores when that is configured.
    /// </summary>
    [Theory]
    [InlineData("*a*\n", "_a_\n")]
    [InlineData("**a**\n", "__a__\n")]
    public void InlineSubsystem_Format_UnderscoreConfigured_AsteriskDelimitersBecomeUnderscores(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(
            input,
            expected,
            LintMarkConfig.Default with { Emphasis = EmphasisMarker.Underscore, Strong = EmphasisMarker.Underscore });

    /// <summary>
    ///     Test that emphasis that starts or ends inside a word keeps its asterisks when underscores are configured.
    /// </summary>
    [Theory]
    [InlineData("a*b*c\n")]
    [InlineData("*a*b\n")]
    public void InlineSubsystem_Format_UnderscoreConfigured_WithinWordEmphasis_KeepsAsterisks(string input) =>
        BlockFormatterAssertions.AssertFormats(
            input,
            input,
            LintMarkConfig.Default with { Emphasis = EmphasisMarker.Underscore, Strong = EmphasisMarker.Underscore });

    /// <summary>
    ///     Test that bare URLs are wrapped in angle brackets, and left as written when that is configured.
    /// </summary>
    [Theory]
    [InlineData("see http://x.com.\n", "see <http://x.com>.\n", "Autolink")]
    [InlineData("see (https://x.org/a)\n", "see (<https://x.org/a>)\n", "Autolink")]
    [InlineData("see ftp://f.org/x\n", "see <ftp://f.org/x>\n", "Autolink")]
    [InlineData("see http://x.com.\n", "see http://x.com.\n", "Leave")]
    public void InlineSubsystem_Format_BareUrls_AreWrappedUnlessConfiguredOtherwise(string input, string expected, string style) =>
        BlockFormatterAssertions.AssertFormats(input, expected, LintMarkConfig.Default with { BareUrls = Enum.Parse<BareUrlStyle>(style) });

    /// <summary>
    ///     Test that the text of a code span keeps its emphasis delimiters and its URL.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_CodeSpans_KeepTheirText() =>
        BlockFormatterAssertions.AssertFormats("`_a_ http://x.com`\n", "`_a_ http://x.com`\n");

    /// <summary>
    ///     Test that the destination of a link keeps its text, while emphasis in link text is written like any other emphasis.
    /// </summary>
    [Theory]
    [InlineData("[_a_](http://x.com/_b_)\n", "[*a*](http://x.com/_b_)\n")]
    [InlineData("[a](http://x.com/_b_)\n", "[a](http://x.com/_b_)\n")]
    [InlineData("- [x](http://y.com/_b_)\n", "- [x](http://y.com/_b_)\n")]
    public void InlineSubsystem_Format_LinkDestinations_KeepTheirText(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that inline HTML keeps its emphasis delimiters and its URL.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_Html_KeepsItsText() =>
        BlockFormatterAssertions.AssertFormats("<span>_a_ http://x.com</span>\n", "<span>_a_ http://x.com</span>\n");

    /// <summary>
    ///     Test that inline math keeps its emphasis delimiters.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_Math_KeepsItsText() =>
        BlockFormatterAssertions.AssertFormats("$_a_$\n", "$_a_$\n");

    /// <summary>
    ///     Test that a www address keeps its form.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_WwwAddress_IsKept() =>
        BlockFormatterAssertions.AssertFormats("see www.x.com\n", "see www.x.com\n");

    /// <summary>
    ///     Test that an e-mail address and a mailto link keep their form.
    /// </summary>
    [Theory]
    [InlineData("a@b.com\n")]
    [InlineData("mailto:a@b.com\n")]
    public void InlineSubsystem_Format_MailAddress_IsKept(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that a URL that is not bare keeps its form: one in angle brackets, one that is link text, one that is the
    ///     destination of a link in a list item, one attached to a quotation mark, and one with a capitalized scheme.
    /// </summary>
    [Theory]
    [InlineData("<http://x.com>\n")]
    [InlineData("[http://x.com](http://y.com)\n")]
    [InlineData("- [x](http://y.com)\n")]
    [InlineData("\"http://x.com\"\n")]
    [InlineData("HTTP://X.COM/a\n")]
    public void InlineSubsystem_Format_UrlsThatAreNotBare_AreKept(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that only paragraphs are normalized: headings and table cells keep their text.
    /// </summary>
    [Theory]
    [InlineData("# _a_ http://x.com\n")]
    [InlineData("| _a_ | http://x.com |\n|---|---|\n| 1 | 2 |\n")]
    public void InlineSubsystem_Format_HeadingsAndTableCells_KeepTheirText(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that paragraphs in lists and quotes are normalized like any other.
    /// </summary>
    [Theory]
    [InlineData("- _a_ http://x.com\n", "- *a* <http://x.com>\n")]
    [InlineData("> _a_ http://x.com\n", "> *a* <http://x.com>\n")]
    [InlineData("1. __a__\n\n   _b_ c\n", "1. **a**\n\n   *b* c\n")]
    public void InlineSubsystem_Format_ParagraphsInContainers_AreNormalized(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that paragraphs in footnotes are normalized like any other.
    /// </summary>
    [Theory]
    [InlineData("a[^n]\n\n[^n]: x _b_ y\n", "a[^n]\n\n[^n]: x *b* y\n")]
    [InlineData("a[^n]\n\n[^n]: x http://x.com\n", "a[^n]\n\n[^n]: x <http://x.com>\n")]
    public void InlineSubsystem_Format_ParagraphsInFootnotes_AreNormalized(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that emphasis that ends the last paragraph of a footnote keeps its text.
    /// </summary>
    [Theory]
    [InlineData("a[^n]\n\n[^n]: _b_\n")]
    [InlineData("a[^n]\n\n[^n]: x __b__\n")]
    public void InlineSubsystem_Format_EmphasisEndingFootnote_IsKept(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that a rule that a comment disables is not applied to the paragraph, by identifier or by alias, while the
    ///     other rules still are.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable emphasis", "_a_ __b__ http://x.com", "_a_ **b** <http://x.com>")]
    [InlineData("markdownlint-disable MD050", "_a_ __b__ http://x.com", "*a* __b__ <http://x.com>")]
    [InlineData("lintmark-disable bare-urls", "_a_ __b__ http://x.com", "*a* **b** http://x.com")]
    [InlineData("markdownlint-disable MD034", "_a_ __b__ http://x.com", "*a* **b** http://x.com")]
    public void InlineSubsystem_Format_RuleSuppressed_LeavesThatRule(string directive, string paragraph, string expected) =>
        BlockFormatterAssertions.AssertFormats($"<!-- {directive} -->\n\n{paragraph}\n", $"<!-- {directive} -->\n\n{expected}\n");

    /// <summary>
    ///     Test that a rule is still applied to a paragraph when only a different rule is suppressed in it.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable emphasis", "_a_ __b__", "_a_ **b**")]
    [InlineData("markdownlint-disable MD050", "_a_ __b__", "*a* __b__")]
    [InlineData("lintmark-disable bare-urls", "_a_ http://x.com", "*a* http://x.com")]
    public void InlineSubsystem_Format_OtherRuleSuppressed_StillAppliesTheRule(string directive, string paragraph, string expected) =>
        BlockFormatterAssertions.AssertFormats($"<!-- {directive} -->\n\n{paragraph}\n", $"<!-- {directive} -->\n\n{expected}\n");

    /// <summary>
    ///     Test that a paragraph that cannot be proved to render the same is left alone and the rest of the file is normalized.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_ParagraphThatMayRenderDifferently_IsLeftAlone()
    {
        // Arrange: a paragraph with an unclosed declaration before a URL, and a plain paragraph
        const string input = "a <!x http://x.com\n\n_b_\n";

        // Act: format and check the rewrite renders the same
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input);

        // Assert: the first paragraph is exactly as written and the plain paragraph is normalized
        Assert.Equal("a <!x http://x.com\n\n*b*\n", formatted);
    }

    /// <summary>
    ///     Test that a paragraph is kept as written when the rewrite could change its meaning: emphasis inside emphasis,
    ///     a delimiter character in a literal, and a bracket while link references are defined.
    /// </summary>
    [Theory]
    [InlineData("_a *b* c_\n", "_a *b* c_\n")]
    [InlineData("_a_ and snake_case\n", "_a_ and snake_case\n")]
    [InlineData("[_a_]\n\n[_a_]: http://u.com\n", "[_a_]\n\n[_a_]: http://u.com\n")]
    public void InlineSubsystem_Format_ParagraphsThatCannotBeShownSafe_AreKept(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);


    /// <summary>
    ///     Test that the same text gives the same result when several threads format it at once.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_ManyThreads_GiveTheSameResult()
    {
        // Arrange: a text with every kind of rewrite, and the production formatter shared by all threads
        const string input = "_a_ __b__ http://x.com\n\n- *c* ftp://f.org\n\n> _d_\n";
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var expected = BlockFormatterAssertions.Apply(formatter, input).Formatted;
        var results = new string[8];

        // Act: format on several threads at once
        BlockFormatterAssertions.RunConcurrently(results.Length, index => results[index] = BlockFormatterAssertions.Apply(formatter, input).Formatted);

        // Assert: every thread gives the same text
        Assert.All(results, result => Assert.Equal(expected, result));
    }
}
