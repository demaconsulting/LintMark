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
using DemaConsulting.LintMark.Formatting.Inline;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the InlineCandidates class finds the edits of the emphasis and the bare URLs of a paragraph, in start
///     order, and rejects missing arguments.
/// </summary>
public class InlineCandidatesTests
{
    /// <summary>
    ///     Gets the inline content of the first paragraph of a text.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The inline content.</returns>
    private static ContainerInline InlineContent(string text)
    {
        var document = BlockFormatterAssertions.Parse(text);
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        return ((ParagraphBlock)block.Node!).Inline!;
    }

    /// <summary>
    ///     Test that a missing text or inline content is rejected.
    /// </summary>
    [Fact]
    public void InlineCandidates_Find_NullArguments_Throw()
    {
        // Arrange: the inline content of a paragraph
        var root = InlineContent("_a_\n");

        // Act & Assert: each argument is checked and named
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => InlineCandidates.Find(null!, root, EmphasisMarker.Asterisk, EmphasisMarker.Asterisk, true)).ParamName);
        Assert.Equal("root", Assert.Throws<ArgumentNullException>(() => InlineCandidates.Find("_a_\n", null!, EmphasisMarker.Asterisk, EmphasisMarker.Asterisk, true)).ParamName);
    }

    /// <summary>
    ///     Test that the edits of a paragraph are returned in start order, each with the rule it enforces.
    /// </summary>
    [Fact]
    public void InlineCandidates_Find_EmphasisStrongAndUrl_ReturnsTheEditsInStartOrder()
    {
        // Arrange: a paragraph with a URL, strong emphasis and emphasis, in this order
        const string text = "http://x.com __b__ _a_\n";

        // Act: find the edits
        var edits = InlineCandidates.Find(text, InlineContent(text), EmphasisMarker.Asterisk, EmphasisMarker.Asterisk, true);

        // Assert: the URL brackets, then the strong delimiters, then the emphasis delimiters, in start order
        Assert.Equal(
            [
                new TextEdit(0, 1, "<h", "bare-urls"),
                new TextEdit(11, 1, "m>", "bare-urls"),
                new TextEdit(13, 2, "**", "strong"),
                new TextEdit(16, 2, "**", "strong"),
                new TextEdit(19, 1, "*", "emphasis"),
                new TextEdit(21, 1, "*", "emphasis")
            ],
            edits);
    }

    /// <summary>
    ///     Test that no edit is found when every rule is off or there is nothing to rewrite.
    /// </summary>
    [Theory]
    [InlineData("_a_ __b__ http://x.com\n", false)]
    [InlineData("*a* **b** <http://x.com> plain\n", true)]
    public void InlineCandidates_Find_RulesOffOrNothingToRewrite_ReturnsEmpty(string text, bool rulesOn)
    {
        // Act: find the edits with the rules on or off
        var edits = InlineCandidates.Find(
            text,
            InlineContent(text),
            rulesOn ? EmphasisMarker.Asterisk : null,
            rulesOn ? EmphasisMarker.Asterisk : null,
            rulesOn);

        // Assert: none
        Assert.Empty(edits);
    }

    /// <summary>
    ///     Test that an inline that the tree holds in two places gives each of its edits once.
    /// </summary>
    [Fact]
    public void InlineCandidates_Find_SameEmphasisInTwoPlaces_ReturnsEachEditOnce()
    {
        // Arrange: a tree whose two containers each hold an emphasis inline over the same text
        const string text = "_a_ b\n";
        var root = new ContainerInline();
        foreach (var _ in new[] { 0, 1 })
        {
            var container = new ContainerInline();
            container.AppendChild(new EmphasisInline { DelimiterChar = '_', DelimiterCount = 1, Span = new SourceSpan(0, 2) });
            root.AppendChild(container);
        }

        // Act: find the edits
        var edits = InlineCandidates.Find(text, root, EmphasisMarker.Asterisk, EmphasisMarker.Asterisk, false);

        // Assert: the two delimiters are rewritten once each
        Assert.Equal([new TextEdit(0, 1, "*", "emphasis"), new TextEdit(2, 1, "*", "emphasis")], edits);
    }
}
