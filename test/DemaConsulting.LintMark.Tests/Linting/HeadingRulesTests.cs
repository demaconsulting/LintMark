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
///     Test that the HeadingRules class reports headings that skip a level, repeat a text or end with punctuation, and
///     paragraphs that are only emphasis.
/// </summary>
public class HeadingRulesTests
{
    /// <summary>
    ///     The rules under test.
    /// </summary>
    private static readonly HeadingRules Rules = new();

    /// <summary>
    ///     Test that null arguments are rejected and the rule identifiers are the four heading rules.
    /// </summary>
    [Fact]
    public void HeadingRules_CheckAndRuleIds_NullArgumentsAndIds_AreHandled()
    {
        // Act & Assert: both arguments are checked and named, and the identifiers are the documented ones
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => Rules.Check(null!, LintMarkConfig.Default)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => Rules.Check(LintRuleTestSupport.Parse("x\n"), null!)).ParamName);
        Assert.Equal(
            ["heading-increment", "heading-duplicate", "heading-trailing-punctuation", "emphasis-as-heading"],
            Rules.RuleIds);
    }

    /// <summary>
    ///     Test that a heading that is more than one level deeper than the one before is reported, setext headings included.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_LevelSkipped_IsReportedAtTheDeeperHeading()
    {
        // Act & Assert: 1 to 3 and 2 to 4 are reported; the first heading, a drop of levels and a step of one are not
        Assert.Equal(
            ["3:1 heading-increment", "6:1 heading-increment"],
            LintRuleTestSupport.Findings(Rules, "# One\n\n### Three\n\n## Two\n#### Deep\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "### Start\n\n## Up\n\n# Top\n\n## Down\n\n### Down again\n"));
        Assert.Equal(["9:1 heading-increment"], LintRuleTestSupport.Findings(Rules, "# One\n\nTwo\n---\n\nThree\n===\n\n#### Four\n"));
        Assert.Equal(["7:1 heading-increment"], LintRuleTestSupport.Findings(Rules, "Setext\n===\n\nsub\n---\n\n#### Deep\n"));
    }

    /// <summary>
    ///     Test that the message names the levels.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_LevelSkipped_MessageNamesTheLevels() =>
        Assert.Equal(["Heading level increases from 2 to 4; expected at most 3"], LintRuleTestSupport.Messages(Rules, "## A\n\n#### B\n"));

    /// <summary>
    ///     Test that a heading with the text of an earlier heading is reported, the comparison keeps the letter case and collapses blanks.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_RepeatedHeadingText_IsReportedAtTheLaterHeading()
    {
        // Act & Assert: the second and third "Same" are reported, a different case and different text are not
        Assert.Equal(
            ["3:1 heading-duplicate", "5:1 heading-duplicate"],
            LintRuleTestSupport.Findings(Rules, "# Same\n\n## Same\n\n## Same\n\n## same\n\n## Other\n"));
        Assert.Equal(["3:1 heading-duplicate"], LintRuleTestSupport.Findings(Rules, "# A  B\n\n## A B\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# A\n\n## B\n\n## C\n"));
    }

    /// <summary>
    ///     Test that a heading that ends with one of the punctuation characters is reported, and question marks and character references are not.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_TrailingPunctuation_IsReportedExceptQuestionMarksAndEntities()
    {
        // Act & Assert: period, colon, exclamation mark and the full-width forms are reported
        Assert.Equal(["1:1 heading-trailing-punctuation"], LintRuleTestSupport.Findings(Rules, "# Title.\n"));
        Assert.Equal(["1:1 heading-trailing-punctuation"], LintRuleTestSupport.Findings(Rules, "# Title:\n"));
        Assert.Equal(["1:1 heading-trailing-punctuation"], LintRuleTestSupport.Findings(Rules, "# Title!\n"));
        Assert.Equal(["1:1 heading-trailing-punctuation"], LintRuleTestSupport.Findings(Rules, "# 見出し。\n"));
        Assert.Equal(["1:1 heading-trailing-punctuation"], LintRuleTestSupport.Findings(Rules, "Title;\n=====\n"));

        // A question mark, a character reference, punctuation inside the text and a closed heading are fine
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Why?\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Copyright &copy;\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Version 1.2 notes\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Title #\n"));
    }

    /// <summary>
    ///     Test that headings inside block quotes and list items take part in the heading rules.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_HeadingInQuote_IsRead() =>
        Assert.Equal(["3:3 heading-increment"], LintRuleTestSupport.Findings(Rules, "# One\n\n> ### Three\n"));

    /// <summary>
    ///     Test that a paragraph that is one line of emphasis is reported, with and without punctuation at its end.
    /// </summary>
    [Fact]
    public void HeadingRules_Check_EmphasisOnlyParagraph_IsReportedUnlessPunctuated()
    {
        // Act & Assert: strong and plain emphasis, with underscores, are reported
        Assert.Equal(["1:1 emphasis-as-heading"], LintRuleTestSupport.Findings(Rules, "**Bold line**\n"));
        Assert.Equal(["1:1 emphasis-as-heading"], LintRuleTestSupport.Findings(Rules, "_Emphasis line_\n"));
        Assert.Equal(["3:1 emphasis-as-heading"], LintRuleTestSupport.Findings(Rules, "text\n\n***Both***\n"));

        // Punctuation at the end, other text around it, several lines, a link inside, a quote and a list are not reported
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "*A sentence.*\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "**Question?**\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "Text **bold** text\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "**bold** and *more*\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "**line one\nline two**\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "**[link](x)**\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "> **Quoted**\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "- **Item**\n"));
    }
}
