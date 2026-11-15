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
///     Test that the LinkRules class reports empty links, images without alternative text, fragments without a target,
///     undefined and unused references, reversed links, spaces inside link text, non-descriptive link text and unused footnotes.
/// </summary>
public class LinkRulesTests
{
    /// <summary>
    ///     The rules under test.
    /// </summary>
    private static readonly LinkRules Rules = new();

    /// <summary>
    ///     Test that null arguments are rejected and the rule identifiers are the link rules.
    /// </summary>
    [Fact]
    public void LinkRules_CheckAndRuleIds_NullArgumentsAndIds_AreHandled()
    {
        // Act & Assert: both arguments are checked and named, and the identifiers are the documented ones
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => Rules.Check(null!, LintMarkConfig.Default)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => Rules.Check(LintRuleTestSupport.Parse("x\n"), null!)).ParamName);
        Assert.Equal(9, Rules.RuleIds.Length);
        Assert.Contains("link-space", Rules.RuleIds);
        Assert.Contains("link-definition-unused", Rules.RuleIds);
    }

    /// <summary>
    ///     Test that links without a destination are reported, and links with one are not.
    /// </summary>
    [Fact]
    public void LinkRules_Check_LinkWithoutDestination_IsReported()
    {
        // Act & Assert: an empty destination, a lone number sign and an empty pointy destination are reported
        Assert.Equal(
            ["1:1 link-empty", "1:7 link-empty", "1:14 link-empty"],
            LintRuleTestSupport.Findings(Rules, "[a]() [b](#) [c](<>) [d](x)\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[d](x) [e](https://example.com) <https://example.com>\n"));
    }

    /// <summary>
    ///     Test that an image without alternative text is reported, also as an HTML tag, and one with text is not.
    /// </summary>
    [Fact]
    public void LinkRules_Check_ImageWithoutAlternativeText_IsReported()
    {
        // Act & Assert: empty and blank alternative text are reported
        Assert.Equal(["1:1 image-alt-text", "1:12 image-alt-text"], LintRuleTestSupport.Findings(Rules, "![](a.png) ![ ](b.png) ![alt](c.png)\n"));

        // An HTML tag needs an alt attribute, empty is allowed; the check reads inline HTML and HTML blocks
        Assert.Equal(["1:6 image-alt-text"], LintRuleTestSupport.Findings(Rules, "text <img src=\"x.png\"> <img src=\"y.png\" alt=\"\">\n"));
        Assert.Equal(["1:1 image-alt-text"], LintRuleTestSupport.Findings(Rules, "<img src=\"z.png\">\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "<div>\n<img src=\"w.png\" alt=\"ok\">\n</div>\n"));

        // A reference image with text is fine, and an image that is not a link text does not make a link finding
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "![alt][r]\n\n[r]: a.png\n"));
    }

    /// <summary>
    ///     Test that a fragment link must match a heading, repeated headings are numbered, and explicit identifiers and HTML anchors count.
    /// </summary>
    [Fact]
    public void LinkRules_Check_FragmentLinks_AreCheckedAgainstHeadingsAndAnchors()
    {
        // Arrange: headings, a repeated heading, an HTML anchor and an explicit identifier
        const string known =
            "# Intro Text\n\n# Intro Text\n\n## What's new? (v2)\n\n<a id=\"custom\"></a>\n\n# Custom {#my-id}\n\n## Logo ![alt](x.png)\n\n"
            + "[a](#intro-text) [b](#intro-text-1) [c](#whats-new-v2) [d](#custom) [e](#my-id) [f](#top) [g](#L10) [h](#L10-L20) [i](#logo-)\n";

        // Act & Assert: every fragment is provided
        Assert.Empty(LintRuleTestSupport.Findings(Rules, known));

        // A fragment that no heading provides is reported at the link, also a numbered repeat that does not exist
        Assert.Equal(
            ["3:1 link-fragment", "3:14 link-fragment"],
            LintRuleTestSupport.Findings(Rules, "# Intro Text\n\n[bad](#nope) [again](#intro-text-1)\n"));

        // Letter case does not matter, a percent-encoded fragment is decoded, and a link to another file is not checked
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Intro\n\n[a](#INTRO) [c](other.md#nope)\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "# Über uns\n\n[b](#%C3%BCber-uns) [c](other.md#nope)\n"));

        // Text in a code span is not a link
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "`[x](#nope)`\n"));
    }

    /// <summary>
    ///     Test that heading text is slugified the way the hosting site does it.
    /// </summary>
    [Theory]
    [InlineData("Intro Text", "intro-text")]
    [InlineData("What's new? (v2)", "whats-new-v2")]
    [InlineData("A  &  B", "a----b")]
    [InlineData("snake_case and-dash", "snake_case-and-dash")]
    [InlineData("Ünïcode Ελληνικά 日本語", "ünïcode-ελληνικά-日本語")]
    [InlineData("`code` and *emphasis*", "code-and-emphasis")]
    public void LinkRules_Slug_HeadingText_IsSlugified(string heading, string expected)
    {
        // Act: link to the slug of the heading that holds the text
        var text = $"# {heading}\n\n[x](#{expected})\n";

        // Assert: the link finds its heading, and the slug function agrees
        Assert.Empty(LintRuleTestSupport.Findings(Rules, text));
        Assert.Equal(expected, LinkRules.Slug(heading.Replace("`", string.Empty, StringComparison.Ordinal).Replace("*", string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that a reference link or image with a label that has no definition is reported, and that shortcuts, footnotes, escapes and code are not.
    /// </summary>
    [Fact]
    public void LinkRules_Check_UndefinedReference_IsReportedAtTheLink()
    {
        // Act & Assert: a full and a collapsed reference without definition
        Assert.Equal(
            ["1:5 link-reference-undefined", "1:22 link-reference-undefined"],
            LintRuleTestSupport.Findings(Rules, "see [text][nope] and [other][]\n"));

        // A defined label (in any letter case), a shortcut, a footnote reference, an escape and a code span are not reported
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[a][Ref] and [short] and [^1] and \\[a\\]\\[b\\] and `[c][d]`\n\n[ref]: http://x\n\n[^1]: note\n"));

        // An unresolved reference next to a resolved one is still reported
        Assert.Equal(["1:15 link-reference-undefined"], LintRuleTestSupport.Findings(Rules, "[ok][ref] and [x][y]\n\n[ref]: http://x\n"));

        // The message names the label
        Assert.Equal(
            ["Reference link uses the label 'nope', which has no definition"],
            LintRuleTestSupport.Messages(Rules, "[text][nope]\n"));
    }

    /// <summary>
    ///     Test that an unused definition and a repeated definition are reported, the comment form is ignored, and uses by images and other letter case count.
    /// </summary>
    [Fact]
    public void LinkRules_Check_Definitions_UnusedAndRepeatedAreReported()
    {
        // Arrange: a used definition, an unused one, a comment definition, an image use and a repeated label
        const string text =
            "[ref]: http://x\n[Unused]: http://y\n[//]: # (comment)\n[img]: a.png\n[dup]: http://z\n[DUP]: http://z\n\n"
            + "[REF] and ![alt][img] and [dup]\n";

        // Act & Assert: only the unused and the repeated definition are reported, each at its own line
        Assert.Equal(["2:1 link-definition-unused", "6:1 link-definition-unused"], LintRuleTestSupport.Findings(Rules, text));
        Assert.Equal(
            ["Link reference definition 'Unused' is never used", "Link reference definition 'DUP' repeats the one at line 5"],
            LintRuleTestSupport.Messages(Rules, text));
    }

    /// <summary>
    ///     Test that a definition used inside a footnote or a table cell counts as used.
    /// </summary>
    [Fact]
    public void LinkRules_Check_DefinitionUsedInFootnoteOrTable_IsUsed() =>
        Assert.Empty(LintRuleTestSupport.Findings(
            Rules,
            "text[^1]\n\n[^1]: see [a][r]\n\n| x |\n| --- |\n| [b][s] |\n\n[r]: http://r\n[s]: http://s\n"));

    /// <summary>
    ///     Test that a reversed link is reported, and that function calls and real links are not.
    /// </summary>
    [Fact]
    public void LinkRules_Check_ReversedLink_IsReported()
    {
        // Act & Assert: the reversed form is reported with the corrected form in the message
        Assert.Equal(["1:1 link-reversed"], LintRuleTestSupport.Findings(Rules, "(text)[url] is wrong\n"));
        Assert.Equal(["Link is written backwards; use [text](url)"], LintRuleTestSupport.Messages(Rules, "(text)[url]\n"));

        // A call with an index, a real link after a group, and code are not reported
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "f(x)[0] and (a)[b](c) and `(t)[u]`\n"));
    }

    /// <summary>
    ///     Test that leading and trailing spaces in the text of inline, reference and collapsed links are reported with the corrected text.
    /// </summary>
    [Fact]
    public void LinkRules_Check_SpacesInLinkText_AreReportedWithTheCorrectedText()
    {
        // Act & Assert: inline links, a reference link and a collapsed link
        Assert.Equal(["1:1 link-space"], LintRuleTestSupport.Findings(Rules, "[ text ](u)\n"));
        Assert.Equal(["1:1 link-space"], LintRuleTestSupport.Findings(Rules, "[text ](u)\n"));
        Assert.Equal(["1:1 link-space"], LintRuleTestSupport.Findings(Rules, "[ text](u)\n"));
        Assert.Equal(["1:1 link-space"], LintRuleTestSupport.Findings(Rules, "[ text ][r]\n\n[r]: x\n"));
        Assert.Equal(["1:1 link-space"], LintRuleTestSupport.Findings(Rules, "[ text ][]\n\n[text]: x\n"));
        Assert.Equal(["Link text has a space at its start and end; use [text] instead of [ text ]"], LintRuleTestSupport.Messages(Rules, "[ text ](u)\n"));
        Assert.Equal(["Link text has a space at its end; use [a *b*] instead of [a *b* ]"], LintRuleTestSupport.Messages(Rules, "[a *b* ](u)\n"));

        // The position is the opening bracket, also in the middle of a line
        Assert.Equal(["1:6 link-space"], LintRuleTestSupport.Findings(Rules, "text [ x ](u)\n"));
    }

    /// <summary>
    ///     Test that image alternative text, shortcut references, text that is wrapped and clean links are not reported as spaces in link text.
    /// </summary>
    [Fact]
    public void LinkRules_Check_SpacesWhereTheyAreAllowed_AreNotReported()
    {
        // Act & Assert: an image, a shortcut reference, a wrapped link text, clean links and text spaces in the middle
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "![ alt ](i.png)\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[ text ]\n\n[text]: x\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[wrapped\ntext](u) [a\n](u)\n"));
        Assert.Empty(LintRuleTestSupport.Findings(Rules, "[ok](u) [two words](u) [ok][r] [ok][]\n\n[r]: x\n[ok]: y\n"));
    }

    /// <summary>
    ///     Test that the non-descriptive link text rule reports the known texts (in any case, ignoring punctuation) and not other text.
    /// </summary>
    [Fact]
    public void LinkRules_Check_NonDescriptiveLinkText_IsReported()
    {
        // Act & Assert: four texts are reported, a longer text is not
        Assert.Equal(
            ["1:1 descriptive-link-text", "1:17 descriptive-link-text", "1:27 descriptive-link-text", "1:37 descriptive-link-text"],
            LintRuleTestSupport.Findings(Rules, "[click here](u) [Here](u) [link](u) [more.](u) [more info](u)\n"));
    }

    /// <summary>
    ///     Test that a footnote that no reference uses is reported at its marker, and a used one is not.
    /// </summary>
    [Fact]
    public void LinkRules_Check_UnusedFootnote_IsReported()
    {
        // Act & Assert: the second footnote is never referenced
        Assert.Equal(["4:1 footnote-unused"], LintRuleTestSupport.Findings(Rules, "Text[^1] and more.\n\n[^1]: used\n[^2]: unused one\n"));
        Assert.Equal(["Footnote '[^2]' is defined but never referenced"], LintRuleTestSupport.Messages(Rules, "Text[^1].\n\n[^1]: used\n[^2]: unused one\n"));
    }

    /// <summary>
    ///     Test that the rules read a document that the grid table fallback parsed, and that a wide mix of constructs does not throw.
    /// </summary>
    [Fact]
    public void LinkRules_Check_AwkwardDocuments_DoNotThrow()
    {
        // Act & Assert: empty text, only definitions, a link in a heading, nested brackets and unterminated constructs
        foreach (var text in new[] { "", "[a]: b\n", "# [h](#h)\n", "[[a]](b) [a [b]](c)\n", "[a](b\n", "![](\n", "[x][\n", "(a)[\n", "<img\n", "[^1]:\n" })
        {
            var exception = Record.Exception(() => LintRuleTestSupport.Findings(Rules, text));
            Assert.Null(exception);
        }
    }
}
