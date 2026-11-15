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

namespace DemaConsulting.LintMark.Tests.Formatting;

/// <summary>
///     Test that the paragraphs after an HTML start tag of an element that keeps its whitespace (<c>pre</c>, <c>script</c>,
///     <c>style</c>, <c>textarea</c>) are left as written, and that other paragraphs are still wrapped.
/// </summary>
public class RawTextElementsTests
{
    /// <summary>
    ///     The text of a paragraph that is too wide for a line length of 40.
    /// </summary>
    private const string Wide = "one two three four five six seven eight nine ten";

    /// <summary>
    ///     Formats a text with the production formatters and a line length of 40.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The patched text.</returns>
    private static string Format(string text) =>
        BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), text, LintMarkConfig.Default with { LineLength = 40 }).Formatted;

    /// <summary>
    ///     Test that a paragraph after an unclosed start tag, or that holds one, is not wrapped.
    /// </summary>
    [Theory]
    [InlineData("<template>\n<script>\n\n" + Wide + "\nsecond\n\n</script>\n</template>\n")]
    [InlineData("<div>\n<pre>\n\n" + Wide + "\n\n</pre>\n</div>\n")]
    [InlineData("<style>\n\n" + Wide + "\n")]
    [InlineData("text <textarea>\n" + Wide + "\n\n</textarea>\n")]
    [InlineData("para\n<pre >\n\n" + Wide + "\n</pre>\n")]
    [InlineData("a <PRE>" + Wide + "\nb</PRE>\n")]
    [InlineData("<div>\n<PRE>\n\n- " + Wide + "\n\n</PRE>\n</div>\n")]
    public void RawTextElements_FindProtectedBlocks_OpenElement_LeavesTheParagraphAsWritten(string input) =>
        Assert.Equal(input, Format(input));

    /// <summary>
    ///     Test that a paragraph after the element is closed is wrapped again.
    /// </summary>
    [Fact]
    public void RawTextElements_FindProtectedBlocks_ClosedElement_WrapsTheNextParagraph()
    {
        // Arrange: the element is opened and closed in the HTML blocks before the paragraph
        var input = "<div>\n<pre>\n\nx\n\n</pre>\n</div>\n\n" + Wide + "\n";

        // Act and assert: the last paragraph is wrapped
        Assert.Equal("<div>\n<pre>\n\nx\n\n</pre>\n</div>\n\none two three four five six seven eight\nnine ten\n", Format(input));
    }

    /// <summary>
    ///     Test that a tag in code, a comment, a longer name or an end tag without a start tag does not protect the text after it.
    /// </summary>
    [Theory]
    [InlineData("`<pre>`\n\n" + Wide + "\n")]
    [InlineData("```\n<script>\n```\n\n" + Wide + "\n")]
    [InlineData("<!-- <pre> -->\n\n" + Wide + "\n")]
    [InlineData("<div>\n<prefix>\n\n" + Wide + "\n")]
    [InlineData("</pre>\n\n" + Wide + "\n")]
    public void RawTextElements_FindProtectedBlocks_NoOpenElement_WrapsTheParagraph(string input)
    {
        // Act and assert: the paragraph is wrapped at the narrow line length
        Assert.Contains("one two three four five six seven eight\nnine ten", Format(input));
    }

    /// <summary>
    ///     Test that the argument is validated.
    /// </summary>
    [Fact]
    public void RawTextElements_FindProtectedBlocks_NullDocument_Throws() =>
        Assert.Throws<ArgumentNullException>(() => RawTextElements.FindProtectedBlocks(null!));
}
