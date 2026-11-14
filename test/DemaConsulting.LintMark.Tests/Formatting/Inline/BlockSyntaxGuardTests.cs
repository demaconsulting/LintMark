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

using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the BlockSyntaxGuard class refuses exactly the words that would turn a wrapped line into block syntax.
/// </summary>
public class BlockSyntaxGuardTests
{
    /// <summary>
    ///     Test that a missing or empty word is rejected.
    /// </summary>
    [Fact]
    public void BlockSyntaxGuard_IsGuardedLineStart_NullOrEmptyAtom_Throws()
    {
        // Act & Assert: a null word and an empty word are rejected and the parameter is named
        Assert.Equal("atom", Assert.Throws<ArgumentNullException>(() => BlockSyntaxGuard.IsGuardedLineStart(null!)).ParamName);
        Assert.Equal("atom", Assert.Throws<ArgumentException>(() => BlockSyntaxGuard.IsGuardedLineStart(string.Empty)).ParamName);
    }

    /// <summary>
    ///     Test that a missing line is rejected.
    /// </summary>
    [Fact]
    public void BlockSyntaxGuard_IsPandocStructuralLine_NullLine_Throws() =>
        Assert.Equal("line", Assert.Throws<ArgumentNullException>(() => BlockSyntaxGuard.IsPandocStructuralLine(null!)).ParamName);

    /// <summary>
    ///     Test that the markers of a bullet list item are guarded.
    /// </summary>
    [Theory]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("*")]
    public void BlockSyntaxGuard_IsGuardedLineStart_BulletMarkers_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that the markers of an ordered list item, a number and a period or a parenthesis, are guarded, also for a number that looks like a year.
    /// </summary>
    [Theory]
    [InlineData("1.")]
    [InlineData("1)")]
    [InlineData("2026.")]
    [InlineData("2026)")]
    [InlineData("0.")]
    [InlineData("123456789.")]
    public void BlockSyntaxGuard_IsGuardedLineStart_NumberMarkers_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that words that only look like an ordered list marker are not guarded.
    /// </summary>
    [Theory]
    [InlineData("1.5")]
    [InlineData("1)x")]
    [InlineData("2026.5")]
    [InlineData("1234567890.")]
    [InlineData("v1.")]
    [InlineData("1,")]
    [InlineData("1.x")]
    public void BlockSyntaxGuard_IsGuardedLineStart_NumbersThatAreNotMarkers_AreNotGuarded(string atom) =>
        Assert.False(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that the number signs of a heading are guarded and words that only start with one are not.
    /// </summary>
    [Theory]
    [InlineData("#", true)]
    [InlineData("##", true)]
    [InlineData("######", true)]
    [InlineData("#######", false)]
    [InlineData("#hashtag", false)]
    [InlineData("C#", false)]
    public void BlockSyntaxGuard_IsGuardedLineStart_HeadingMarkers_AreGuardedOnlyAsMarkers(string atom, bool expected) =>
        Assert.Equal(expected, BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that every word that starts with a quote marker is guarded.
    /// </summary>
    [Theory]
    [InlineData(">")]
    [InlineData(">quote")]
    [InlineData(">=")]
    public void BlockSyntaxGuard_IsGuardedLineStart_QuoteMarkers_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that words that open a code fence are guarded and code spans and strikethrough are not.
    /// </summary>
    [Theory]
    [InlineData("```", true)]
    [InlineData("```csharp", true)]
    [InlineData("````", true)]
    [InlineData("~~~", true)]
    [InlineData("~~~~", true)]
    [InlineData("```{.python}", true)]
    [InlineData("``code``", false)]
    [InlineData("`code`", false)]
    [InlineData("~~strike~~", false)]
    public void BlockSyntaxGuard_IsGuardedLineStart_Fences_AreGuarded(string atom, bool expected) =>
        Assert.Equal(expected, BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that a reference definition and a footnote definition are guarded and a link, a reference and a task marker are not.
    /// </summary>
    [Theory]
    [InlineData("[label]:", true)]
    [InlineData("[label]:/url", true)]
    [InlineData("[^note]:", true)]
    [InlineData("[label]", false)]
    [InlineData("[label](url)", false)]
    [InlineData("[x]", false)]
    [InlineData("[^1]", false)]
    public void BlockSyntaxGuard_IsGuardedLineStart_Definitions_AreGuarded(string atom, bool expected) =>
        Assert.Equal(expected, BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that the starts of block-level HTML are guarded.
    /// </summary>
    [Theory]
    [InlineData("<div>")]
    [InlineData("<DIV")]
    [InlineData("</div>")]
    [InlineData("<p>")]
    [InlineData("<table>")]
    [InlineData("<h1>")]
    [InlineData("<details>")]
    [InlineData("<!--")]
    [InlineData("<?php")]
    [InlineData("<![CDATA[")]
    [InlineData("<!DOCTYPE")]
    [InlineData("<pre>")]
    [InlineData("<script>")]
    [InlineData("<style")]
    [InlineData("<textarea")]
    public void BlockSyntaxGuard_IsGuardedLineStart_HtmlBlockStarts_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that inline HTML tags and links in angle brackets are not guarded.
    /// </summary>
    [Theory]
    [InlineData("<span>")]
    [InlineData("<a")]
    [InlineData("<em>")]
    [InlineData("<b>")]
    [InlineData("<br>")]
    [InlineData("<http://x.org>")]
    [InlineData("<mailto:a@b.com>")]
    public void BlockSyntaxGuard_IsGuardedLineStart_InlineHtml_IsNotGuarded(string atom) =>
        Assert.False(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that thematic breaks and setext underlines, which are runs of one character, are guarded.
    /// </summary>
    [Theory]
    [InlineData("---")]
    [InlineData("--")]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("_")]
    [InlineData("===")]
    [InlineData("=")]
    public void BlockSyntaxGuard_IsGuardedLineStart_ThematicAndSetextRuns_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that table rows and table delimiter rows are guarded.
    /// </summary>
    [Theory]
    [InlineData("|")]
    [InlineData("|a|b|")]
    [InlineData("---|---")]
    [InlineData(":--")]
    [InlineData("--:")]
    [InlineData(":-:")]
    [InlineData("|---|")]
    public void BlockSyntaxGuard_IsGuardedLineStart_TableRows_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that the starts of Pandoc constructs are guarded.
    /// </summary>
    [Theory]
    [InlineData(":::")]
    [InlineData(":::note")]
    [InlineData(":")]
    [InlineData("~")]
    [InlineData("\\section")]
    [InlineData("\\cite{x}")]
    [InlineData("$$")]
    [InlineData("$$x")]
    [InlineData("{=html}")]
    [InlineData("%")]
    [InlineData("+---+")]
    [InlineData("+===+")]
    [InlineData("+-+")]
    public void BlockSyntaxGuard_IsGuardedLineStart_PandocConstructs_AreGuarded(string atom) =>
        Assert.True(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that ordinary words, emphasis, signed numbers, dollar amounts and escapes are not guarded.
    /// </summary>
    [Theory]
    [InlineData("text")]
    [InlineData("*emphasis*")]
    [InlineData("**bold**")]
    [InlineData("_x_")]
    [InlineData("-foo")]
    [InlineData("+foo")]
    [InlineData("+1")]
    [InlineData("a-b")]
    [InlineData("a.b")]
    [InlineData("...")]
    [InlineData("::")]
    [InlineData("\\")]
    [InlineData("\\*")]
    [InlineData("\\1")]
    [InlineData("$5")]
    [InlineData("100%")]
    [InlineData("{x}")]
    [InlineData("&amp;")]
    [InlineData("日本語")]
    [InlineData("😀")]
    public void BlockSyntaxGuard_IsGuardedLineStart_OrdinaryText_IsNotGuarded(string atom) =>
        Assert.False(BlockSyntaxGuard.IsGuardedLineStart(atom));

    /// <summary>
    ///     Test that a content line that starts a Pandoc construct is recognized, also when it is indented.
    /// </summary>
    [Theory]
    [InlineData(":::", true)]
    [InlineData("::: {.note}", true)]
    [InlineData("\\section", true)]
    [InlineData("\\begin{center}", true)]
    [InlineData("$$", true)]
    [InlineData("{=html}", true)]
    [InlineData("% title", true)]
    [InlineData("%", true)]
    [InlineData("+---+---+", true)]
    [InlineData("+===+", true)]
    [InlineData("  \\section", true)]
    [InlineData("text", false)]
    [InlineData("\\*escaped", false)]
    [InlineData("\\", false)]
    [InlineData("100% sure", false)]
    [InlineData("+ item", false)]
    [InlineData("a ::: b", false)]
    [InlineData("", false)]
    public void BlockSyntaxGuard_IsPandocStructuralLine_Lines_AreClassified(string line, bool expected) =>
        Assert.Equal(expected, BlockSyntaxGuard.IsPandocStructuralLine(line));

    /// <summary>
    ///     Test that the answer for a word is the same on every thread.
    /// </summary>
    [Fact]
    public void BlockSyntaxGuard_IsGuardedLineStart_ConcurrentCalls_GiveTheSameAnswers()
    {
        // Arrange: words with both answers and their single-threaded answers
        string[] words = ["-", "2026.", "text", "<div>", "<span>", "|a", ":::", "1.5", "[x]:", "日本"];
        var expected = words.Select(BlockSyntaxGuard.IsGuardedLineStart).ToArray();

        // Act & Assert: every thread gets the same answers
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 200; round++)
                {
                    Assert.Equal(expected, words.Select(BlockSyntaxGuard.IsGuardedLineStart).ToArray());
                }
            });
    }
}
