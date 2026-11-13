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

using System.Diagnostics;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the HtmlEquivalenceChecker class accepts rewrites that render to the same page and refuses
///     every rewrite that changes the rendered page.
/// </summary>
public class HtmlEquivalenceCheckerTests
{
    /// <summary>
    ///     Gets a fixed set of reference cases: the text before, the text after, and whether they render equivalently.
    /// </summary>
    /// <returns>The cases.</returns>
    public static TheoryData<string, string, bool> ReferenceCases() => new()
    {
        { "a b\n", "a\nb\n", true },
        { "a  b\n", "a b\n", true },
        { "> a\nb\n", "> a\n> b\n", true },
        { "- a\n  b\n", "- a\n    b\n", true },
        { "1. a\n1. b\n", "1. a\n2. b\n", true },
        { "*a*\n", "_a_\n", true },
        { "* a\n", "- a\n", true },
        { "A\n=\n", "# A\n", true },
        { "|a|b|\n|-|-|\n|1|2|\n", "| a | b |\n| --- | --- |\n| 1 | 2 |\n", true },
        { "a \n", "a\n", true },
        { "a  \nb\n", "a\nb\n", false },
        { "a - b\n", "a\n- b\n", false },
        { "a # b\n", "a\n# b\n", false },
        { "a > b\n", "a\n> b\n", false },
        { "a 1. b\n", "a\n1. b\n", false },
        { "a ===\n", "a\n===\n", false },
        { "`a  b`\n", "`a b`\n", false },
        { "```\na  b\n```\n", "```\na b\n```\n", false },
        { "[a](http://x)\n", "[a](http://y)\n", false },
        { "[a](http://x \"t\")\n", "[a](http://x)\n", false },
        { "## a\n", "### a\n", false },
        { "*a*\n", "a\n", false },
        { "a b\n", "ab\n", false },
        { "*a* *b*\n", "*a**b*\n", false },
        { "| a |\n|---|\n| 1 |\n", "| a |\n|---|\n| 2 |\n", false },
        { "- a\n- b\n", "- a\n", false },
        { "- a\n- b\n", "- a\n\n- b\n", false },
        { "`a\nb`\n", "`a b`\n", true },
        { "<a href=\"x\">t</a>\n", "<a href=\"y\">t</a>\n", false }
    };

    /// <summary>
    ///     Test that every reference case gets its expected verdict.
    /// </summary>
    /// <param name="before">The text before.</param>
    /// <param name="after">The text after.</param>
    /// <param name="expectEquivalent">Whether the texts must be equivalent.</param>
    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void HtmlEquivalenceChecker_Check_ReferenceCases_ReportExpectedVerdict(string before, string after, bool expectEquivalent)
    {
        // Arrange / Act: compare the two versions
        var result = HtmlEquivalenceChecker.Check(before, after);

        // Assert: the verdict is the expected one, and a failure always has a description
        Assert.Equal(expectEquivalent, result.IsEquivalent);
        Assert.Equal(expectEquivalent, result.Description is null);
    }

    /// <summary>
    ///     Test that a soft line break and a space are equivalent.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_SoftBreakVersusSpace_Equivalent()
    {
        // Arrange / Act: a paragraph that wraps and one that does not
        var result = HtmlEquivalenceChecker.Check("alpha beta\n", "alpha\nbeta\n");

        // Assert: same page
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
    }

    /// <summary>
    ///     Test that another bullet marker is equivalent.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_BulletMarkerChanged_Equivalent()
    {
        // Arrange / Act: a list with an asterisk marker and the same list with a hyphen marker
        var result = HtmlEquivalenceChecker.Check("* a\n* b\n", "- a\n- b\n");

        // Assert: same page
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that table cell spacing is equivalent.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_TableSpacingChanged_Equivalent()
    {
        // Arrange / Act: a table without padding and the same table with padded cells
        var result = HtmlEquivalenceChecker.Check(
            "|a|b|\n|-|-|\n|1|2|\n",
            "| a | b |\n| --- | --- |\n| 1 | 2 |\n");

        // Assert: same page
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a setext heading and an ATX heading are equivalent.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_SetextToAtx_Equivalent()
    {
        // Arrange / Act: the same heading in both syntaxes
        var result = HtmlEquivalenceChecker.Check("Title\n=====\n\nbody\n", "# Title\n\nbody\n");

        // Assert: same page
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that dropping the two trailing spaces of a hard break is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_HardBreakDropped_Different()
    {
        // Arrange / Act: a paragraph with a hard break and the same without it
        var result = HtmlEquivalenceChecker.Check("a  \nb\n", "a\nb\n");

        // Assert: the break tag is lost
        Assert.False(result.IsEquivalent);
        Assert.Contains("<br", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a line break that lets a list start is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_BreakCreatesList_Different()
    {
        // Arrange / Act: a break before a dash turns prose into a list
        var result = HtmlEquivalenceChecker.Check("a - b\n", "a\n- b\n");

        // Assert: the list is found
        Assert.False(result.IsEquivalent);
        Assert.Contains("<li>", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a line break that lets a heading start is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_BreakCreatesHeading_Different()
    {
        // Arrange / Act: a break before a hash turns prose into a heading
        var result = HtmlEquivalenceChecker.Check("a # b\n", "a\n# b\n");

        // Assert: the heading is found
        Assert.False(result.IsEquivalent);
        Assert.Contains("<h1>", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that collapsing the spaces inside a code span is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_CodeSpanDoubleSpaceCollapsed_Different()
    {
        // Arrange / Act: a code span with two spaces and the same with one
        var result = HtmlEquivalenceChecker.Check("`a  b`\n", "`a b`\n");

        // Assert: the content of a code span is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a change inside a fenced code block is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_FencedCodeChanged_Different()
    {
        // Arrange / Act: a fenced block with two spaces and the same with one
        var result = HtmlEquivalenceChecker.Check("```\na  b\n```\n", "```\na b\n```\n");

        // Assert: the content of a code block is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a changed link target is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_LinkUrlChanged_Different()
    {
        // Arrange / Act: two links to different addresses
        var result = HtmlEquivalenceChecker.Check("[a](http://x)\n", "[a](http://y)\n");

        // Assert: the address is part of the page
        Assert.False(result.IsEquivalent);
        Assert.Contains("http://y", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that losing the space between two emphasized words is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_EmphasisSpacingLost_Different()
    {
        // Arrange / Act: a space between strong and emphasized text, and the same without it
        var result = HtmlEquivalenceChecker.Check("**a** *b*\n", "**a***b*\n");

        // Assert: whitespace between inline tags is never ignored
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that losing the emphasis of a word is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_EmphasisLost_Different()
    {
        // Arrange / Act: an emphasized word and the same word without emphasis
        var result = HtmlEquivalenceChecker.Check("*a*\n", "a\n");

        // Assert: the emphasis is part of the page
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that gluing two words together is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_GluedWords_Different()
    {
        // Arrange / Act: two words and one word
        var result = HtmlEquivalenceChecker.Check("a b\n", "ab\n");

        // Assert: the text differs
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that losing the space between two inline HTML elements is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_LostInlineTagSpace_Different()
    {
        // Arrange / Act: a space between two inline elements and the same without it
        var result = HtmlEquivalenceChecker.Check("<span>a</span> <span>b</span>\n", "<span>a</span><span>b</span>\n");

        // Assert: whitespace between inline tags is never ignored
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that different heading identifiers alone are not a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_HeadingIdsOnly_Equivalent()
    {
        // Arrange / Act: raw HTML headings that differ in their identifier
        var result = HtmlEquivalenceChecker.Check("<h2 id=\"a\">x</h2>\n", "<h2 id=\"b\">x</h2>\n");

        // Assert: identifiers are dropped
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a bounded run time is kept for input made of very many unclosed tags.
    /// </summary>
    /// <param name="tag">The opening tag that is repeated without closing tag.</param>
    /// <param name="count">How many times the tag is repeated.</param>
    [Theory]
    [InlineData("<pre>", 20000)]
    [InlineData("<code>", 20000)]
    [InlineData("<p ", 200000)]
    [InlineData("<div ", 200000)]
    [InlineData("<h1 ", 200000)]
    public void HtmlEquivalenceChecker_Check_PathologicalUnclosedTags_CompletesWithinBound(string tag, int count)
    {
        // Arrange: very many unclosed tags, and the same with one more character
        var before = string.Concat(Enumerable.Repeat(tag, count)) + "\n";
        var after = before + "z\n";

        // Act: compare and measure
        var stopwatch = Stopwatch.StartNew();
        var result = HtmlEquivalenceChecker.Check(before, after);
        stopwatch.Stop();

        // Assert: the check finished in time and found the difference
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a bounded run time is kept for input made of very many code spans.
    /// </summary>
    /// <param name="open">The opening tag of the verbatim element that is repeated.</param>
    /// <param name="close">The closing tag of the verbatim element that is repeated.</param>
    [Theory]
    [InlineData("<code>", "</code>")]
    [InlineData("<pre>", "</pre>")]
    public void HtmlEquivalenceChecker_Check_ManyVerbatimElements_CompletesWithinBound(string open, string close)
    {
        // Arrange: fifty thousand closed elements, and the same with a word appended
        var spans = string.Concat(Enumerable.Range(0, 50000).Select(i => $"{open}a{i}{close} "));
        var before = spans + "\n";
        var after = spans + "z\n";

        // Act: compare and measure
        var stopwatch = Stopwatch.StartNew();
        var result = HtmlEquivalenceChecker.Check(before, after);
        stopwatch.Stop();

        // Assert: the check finished in time and found the difference
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a renderer that fails is reported as a difference that names the exception type.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_RendererThrows_ReportsDifferenceNamingExceptionType()
    {
        // Arrange / Act: two different texts whose rendering fails
        var result = HtmlEquivalenceChecker.Check(
            "a\n",
            "b\n",
            _ => throw new InvalidOperationException("render failed"));

        // Assert: the undecided check is a difference, with a one-line description
        Assert.False(result.IsEquivalent);
        Assert.NotNull(result.Description);
        Assert.Contains("could not be compared", result.Description, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that identical text is equivalent without the renderer being used.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_IdenticalTextRendererThrows_StillEquivalent()
    {
        // Arrange / Act: the same text twice, including text that is only a lone tag, with a renderer that
        // must never run
        var result = HtmlEquivalenceChecker.Check(
            "a\n",
            "a\n",
            _ => throw new InvalidOperationException("render failed"));
        var loneTag = HtmlEquivalenceChecker.Check(
            "<pre>\n",
            "<pre>\n",
            _ => throw new InvalidOperationException("render failed"));

        // Assert: identical text needs no rendering
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
        Assert.True(loneTag.IsEquivalent);
        Assert.Null(loneTag.Description);
    }

    /// <summary>
    ///     Test that a missing renderer is rejected.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_NullRenderer_Throws()
    {
        // Arrange / Act / Assert: the renderer may not be null
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.Check("a\n", "b\n", null!));
    }

    /// <summary>
    ///     Test that the description of a difference gives the offset and excerpts of both renderings.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_FirstDifference_DescribedWithContext()
    {
        // Arrange / Act: two texts that differ in one word
        var result = HtmlEquivalenceChecker.Check("alpha\n", "omega\n");

        // Assert: offset and both excerpts are named
        Assert.False(result.IsEquivalent);
        Assert.Contains("offset", result.Description, StringComparison.Ordinal);
        Assert.Contains("before \"<p>alpha</p>\"", result.Description, StringComparison.Ordinal);
        Assert.Contains("after \"<p>omega</p>\"", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that many threads get the same verdicts from the shared pipeline.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_ParallelCalls_SameResult()
    {
        // Arrange: one equivalent and one different pair, and the single-threaded results
        var equivalent = HtmlEquivalenceChecker.Check("a b\n", "a\nb\n");
        var different = HtmlEquivalenceChecker.Check("a  \nb\n", "a\nb\n");
        var results = new (EquivalenceResult Equivalent, EquivalenceResult Different)[64];

        // Act: check concurrently
        Parallel.For(
            0,
            results.Length,
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            i => results[i] = (HtmlEquivalenceChecker.Check("a b\n", "a\nb\n"), HtmlEquivalenceChecker.Check("a  \nb\n", "a\nb\n")));

        // Assert: every result equals the single-threaded one
        Assert.All(results, r =>
        {
            Assert.Equal(equivalent, r.Equivalent);
            Assert.Equal(different, r.Different);
        });
    }

    /// <summary>
    ///     Test that a carriage return in the original text is rejected.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_CarriageReturnInBefore_Throws()
    {
        // Arrange / Act / Assert: only LF-normalized text is accepted
        Assert.Throws<ArgumentException>(() => HtmlEquivalenceChecker.Check("a\r\nb\n", "a\nb\n"));
    }

    /// <summary>
    ///     Test that a carriage return in the rewritten text is rejected.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_CarriageReturnInAfter_Throws()
    {
        // Arrange / Act / Assert: only LF-normalized text is accepted
        Assert.Throws<ArgumentException>(() => HtmlEquivalenceChecker.Check("a\nb\n", "a\r\nb\n"));
    }

    /// <summary>
    ///     Test that a null argument is rejected.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_NullArguments_Throw()
    {
        // Arrange / Act / Assert: neither text may be null
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.Check(null!, "a\n"));
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.Check("a\n", null!));
    }

    /// <summary>
    ///     Test that the same code span with CRLF and with LF line endings is a difference, as the renderer shows it differently.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_CrlfVersusLfCodeSpan_Different()
    {
        // Arrange / Act: a code span framed by line breaks, with CRLF and with LF line endings
        var result = HtmlEquivalenceChecker.CheckOnDisk("`\r\nbb\r\n`\r\n", "`\nbb\n`\n");
        var reverse = HtmlEquivalenceChecker.CheckOnDisk("`\nbb\n`\n", "`\r\nbb\r\n`\r\n");

        // Assert: both directions are differences that name the renderings
        Assert.False(result.IsEquivalent);
        Assert.Contains("offset", result.Description, StringComparison.Ordinal);
        Assert.False(reverse.IsEquivalent);
    }

    /// <summary>
    ///     Test that a conversion of the line endings of an ordinary document is equivalent.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_CrlfVersusLfOrdinaryText_Equivalent()
    {
        // Arrange: a document with a soft break, a hard break, a list, a code span, and a fenced and an HTML pre block
        const string lf = "# T\n\nA `code` span\nand a soft break,  \nand a hard break.\n\n- one\n- two\n\n```\nx  y\n\nz\n```\n\n<pre>\na\n\nb\n</pre>\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        // Act: compare the two line ending styles in both directions
        var toLf = HtmlEquivalenceChecker.CheckOnDisk(crlf, lf);
        var toCrlf = HtmlEquivalenceChecker.CheckOnDisk(lf, crlf);

        // Assert: the page is the same
        Assert.True(toLf.IsEquivalent, toLf.Description);
        Assert.True(toCrlf.IsEquivalent, toCrlf.Description);
    }

    /// <summary>
    ///     Test that a change of the text is still a difference when the line endings are compared as they are on disk.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_TextChangedWithCrlf_Different()
    {
        // Arrange / Act: a hard break that is dropped, with CRLF line endings
        var result = HtmlEquivalenceChecker.CheckOnDisk("a  \r\nb\r\n", "a\r\nb\r\n");

        // Assert: still detected
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that identical text is equivalent without the renderer being used.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_IdenticalTextRendererThrows_StillEquivalent()
    {
        // Arrange / Act: the same text with a carriage return twice and a renderer that must never run
        var result = HtmlEquivalenceChecker.CheckOnDisk(
            "a\r\n",
            "a\r\n",
            _ => throw new InvalidOperationException("render failed"));

        // Assert: identical text needs no rendering
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
    }

    /// <summary>
    ///     Test that a renderer that fails is reported as a difference that names the exception type.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_RendererThrows_ReportsDifferenceNamingExceptionType()
    {
        // Arrange / Act: two different texts whose rendering fails
        var result = HtmlEquivalenceChecker.CheckOnDisk(
            "a\r\n",
            "a\n",
            _ => throw new InvalidOperationException("render failed"));

        // Assert: the undecided check is a difference
        Assert.False(result.IsEquivalent);
        Assert.Contains("could not be compared", result.Description, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a null argument is rejected.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_CheckOnDisk_NullArguments_Throw()
    {
        // Arrange / Act / Assert: neither text and not the renderer may be null
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.CheckOnDisk(null!, "a\r\n"));
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.CheckOnDisk("a\r\n", null!));
        Assert.Throws<ArgumentNullException>(() => HtmlEquivalenceChecker.CheckOnDisk("a\r\n", "b\r\n", null!));
    }

    /// <summary>
    ///     Test that a changed no-break space is a difference although it looks like a space.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_NoBreakSpaceChanged_Different()
    {
        // Arrange / Act: a no-break space replaced by a space
        var result = HtmlEquivalenceChecker.Check("a\u00A0b\n", "a b\n");

        // Assert: only HTML whitespace is collapsed
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a whitespace change inside a raw HTML pre element is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_RawHtmlPreWhitespaceChanged_Different()
    {
        // Arrange / Act: a pre element with two spaces and the same with one
        var result = HtmlEquivalenceChecker.Check("<pre>a  b</pre>\n", "<pre>a b</pre>\n");

        // Assert: the content of pre is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a whitespace change after an opening pre tag that is never closed is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_UnclosedPreWhitespaceChanged_Different()
    {
        // Arrange / Act: an unclosed pre element whose content has two spaces, and the same with one
        var result = HtmlEquivalenceChecker.Check("<pre>\na  b\n", "<pre>\na b\n");

        // Assert: an unclosed element runs to the end of the text and is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that the element name is matched ignoring case.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_UppercasePreWhitespaceChanged_Different()
    {
        // Arrange / Act: an upper-case PRE element with two spaces and the same with one
        var result = HtmlEquivalenceChecker.Check("<PRE>a  b</PRE>\n", "<PRE>a b</PRE>\n");

        // Assert: the content is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a whitespace change inside a raw HTML script, style, or textarea element is a difference.
    /// </summary>
    /// <param name="open">The opening tag of the element.</param>
    /// <param name="close">The closing tag of the element.</param>
    [Theory]
    [InlineData("<script>", "</script>")]
    [InlineData("<style>", "</style>")]
    [InlineData("<textarea>", "</textarea>")]
    public void HtmlEquivalenceChecker_Check_ScriptStyleTextareaWhitespaceChanged_Different(string open, string close)
    {
        // Arrange / Act: an element with two spaces in its content and the same with one
        var result = HtmlEquivalenceChecker.Check($"{open}a  b{close}\n", $"{open}a b{close}\n");

        // Assert: the content of the element is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a whitespace change after an inline code tag that is never closed is a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_UnclosedCodeWhitespaceChanged_Different()
    {
        // Arrange / Act: an unclosed code element whose content has two spaces, and the same with one
        var result = HtmlEquivalenceChecker.Check("a <code>b  c\n", "a <code>b c\n");

        // Assert: an unclosed element runs to the end of the text and is compared exactly
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that tabs and form feeds collapse like spaces outside verbatim elements.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_TabAndFormFeedCollapsed_Equivalent()
    {
        // Arrange / Act: HTML given directly, with a tab, a form feed, and a mixed run against one space
        var tab = HtmlEquivalenceChecker.Check("<p>a\tb</p>", "<p>a b</p>", text => text);
        var formFeed = HtmlEquivalenceChecker.Check("<p>a\fb</p>", "<p>a b</p>", text => text);
        var mixed = HtmlEquivalenceChecker.Check("<p>a \t\f b</p>", "<p>a b</p>", text => text);

        // Assert: all HTML whitespace collapses to one space
        Assert.True(tab.IsEquivalent);
        Assert.True(formFeed.IsEquivalent);
        Assert.True(mixed.IsEquivalent);
    }

    /// <summary>
    ///     Test that single-quoted heading identifiers are not a difference.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_SingleQuotedHeadingIds_Equivalent()
    {
        // Arrange / Act: HTML given directly, with a single-quoted and a double-quoted identifier
        var result = HtmlEquivalenceChecker.Check("<h2 id='a'>x</h2>", "<h2 id=\"b\">x</h2>", text => text);

        // Assert: identifiers are dropped whatever the quotes
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that an identifier that does not directly follow the tag name is compared like any attribute.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_HeadingIdAfterOtherAttribute_Different()
    {
        // Arrange / Act: HTML given directly, with the identifier after another attribute
        var result = HtmlEquivalenceChecker.Check(
            "<h2 class=\"c\" id=\"a\">x</h2>",
            "<h2 class=\"c\" id=\"b\">x</h2>",
            text => text);

        // Assert: only the identifier that the renderer writes is dropped
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a heading identifier inside a verbatim element is compared exactly.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_HeadingIdInsidePreChanged_Different()
    {
        // Arrange / Act: a pre element that contains heading markup with different identifiers
        var result = HtmlEquivalenceChecker.Check(
            "<pre><h2 id=\"a\">x</h2></pre>\n",
            "<pre><h2 id=\"b\">x</h2></pre>\n");

        // Assert: verbatim content is compared exactly, so the identifier is a difference
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that trailing whitespace and trailing blank lines do not change the page.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_TrailingWhitespaceOnly_Equivalent()
    {
        // Arrange / Act: a paragraph with trailing spaces and blank lines, and the same without
        var spaces = HtmlEquivalenceChecker.Check("a\n", "a   \n");
        var blankLines = HtmlEquivalenceChecker.Check("a\n\n\n\n", "a\n");

        // Assert: both are equivalent
        Assert.True(spaces.IsEquivalent);
        Assert.True(blankLines.IsEquivalent);
    }

    /// <summary>
    ///     Test that the description of a difference is one bounded line, however long the texts are.
    /// </summary>
    [Fact]
    public void HtmlEquivalenceChecker_Check_Difference_DescriptionIsSingleLineAndBounded()
    {
        // Arrange: two long texts with many lines that differ from the first word on
        var before = string.Join("\n", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var after = string.Join("\n", Enumerable.Range(0, 2000).Select(i => $"term{i}"));

        // Act: compare
        var result = HtmlEquivalenceChecker.Check(before, after);

        // Assert: the description has no line break and stays short
        Assert.False(result.IsEquivalent);
        Assert.NotNull(result.Description);
        Assert.DoesNotContain("\n", result.Description);
        Assert.DoesNotContain("\r", result.Description);
        Assert.True(result.Description.Length <= 243, $"length {result.Description.Length}");
    }
}
