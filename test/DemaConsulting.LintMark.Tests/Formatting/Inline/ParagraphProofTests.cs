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

using System.Collections.Immutable;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the ParagraphProof class accepts edits only when the content of the paragraph renders the same
///     afterwards, and refuses everything it cannot check.
/// </summary>
public class ParagraphProofTests
{
    /// <summary>
    ///     Gets the lines of the first paragraph of a text.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The lines.</returns>
    private static IReadOnlyList<LeafLine> ParagraphLines(string text)
    {
        var document = BlockFormatterAssertions.Parse(text);
        return DocumentParser.GetLines(document, BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph));
    }

    /// <summary>
    ///     Test that a missing text, line list or line, and a default edit array, are rejected.
    /// </summary>
    [Fact]
    public void ParagraphProof_Holds_NullArguments_Throw()
    {
        // Arrange: the lines of a paragraph and an edit
        const string text = "_a_\n";
        var lines = ParagraphLines(text);
        ImmutableArray<TextEdit> edits = [new TextEdit(0, 1, "*", "emphasis")];

        // Act & Assert: each argument is checked and named
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => ParagraphProof.Holds(null!, lines, edits)).ParamName);
        Assert.Equal("lines", Assert.Throws<ArgumentNullException>(() => ParagraphProof.Holds(text, null!, edits)).ParamName);
        Assert.Equal("lines", Assert.Throws<ArgumentNullException>(() => ParagraphProof.Holds(text, [lines[0], null!], edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => ParagraphProof.Holds(text, lines, default)).ParamName);
    }

    /// <summary>
    ///     Test that edits that keep the rendering are accepted.
    /// </summary>
    [Fact]
    public void ParagraphProof_Holds_EditsThatKeepTheRendering_ReturnsTrue()
    {
        // Arrange: a paragraph of two lines and the edits that rewrite its emphasis
        const string text = "_a\nb_\n";
        ImmutableArray<TextEdit> edits = [new TextEdit(0, 1, "*", "emphasis"), new TextEdit(4, 1, "*", "emphasis")];

        // Act & Assert: the two renderings are equal
        Assert.True(ParagraphProof.Holds(text, ParagraphLines(text), edits));
    }

    /// <summary>
    ///     Test that edits that change the rendering are refused.
    /// </summary>
    [Fact]
    public void ParagraphProof_Holds_EditsThatChangeTheRendering_ReturnsFalse()
    {
        // Arrange: an unclosed declaration swallows the angle bracket that the edits insert
        const string text = "a <!x http://x.com\n";
        ImmutableArray<TextEdit> edits = [new TextEdit(6, 1, "<h", "bare-urls"), new TextEdit(17, 1, "m>", "bare-urls")];

        // Act & Assert: the renderings differ
        Assert.False(ParagraphProof.Holds(text, ParagraphLines(text), edits));
    }

    /// <summary>
    ///     Test that a line whose content range is negative, reversed or beyond the text is refused.
    /// </summary>
    [Theory]
    [InlineData(-1, 3)]
    [InlineData(3, 1)]
    [InlineData(0, 99)]
    public void ParagraphProof_Holds_LineRangeInvalid_ReturnsFalse(int contentStart, int contentEnd)
    {
        // Arrange: a line with the given content range
        const string text = "_a_\n";
        IReadOnlyList<LeafLine> lines = [new LeafLine(1, 0, contentStart, contentEnd, false)];

        // Act & Assert: the proof does not hold
        Assert.False(ParagraphProof.Holds(text, lines, []));
    }

    /// <summary>
    ///     Test that an edit outside the content of every line, here on a block quote marker, is refused.
    /// </summary>
    [Fact]
    public void ParagraphProof_Holds_EditOutsideLineContent_ReturnsFalse()
    {
        // Arrange: a quoted paragraph and an edit on its marker, which is not part of the content
        const string text = "> _a_\n";
        ImmutableArray<TextEdit> edits = [new TextEdit(0, 1, "*", "emphasis")];

        // Act & Assert: the proof does not hold
        Assert.False(ParagraphProof.Holds(text, ParagraphLines(text), edits));
    }

    /// <summary>
    ///     Test that content that is not read as exactly one paragraph is refused.
    /// </summary>
    [Theory]
    [InlineData("# a\n")]
    [InlineData("a\n\nb\n")]
    public void ParagraphProof_Holds_ContentNotOneParagraph_ReturnsFalse(string text)
    {
        // Arrange: lines whose content is a heading, or two paragraphs
        var lines = new List<LeafLine>();
        var offset = 0;
        var number = 1;
        foreach (var line in text.TrimEnd('\n').Split('\n'))
        {
            lines.Add(new LeafLine(number++, offset, offset, offset + line.Length, false));
            offset += line.Length + 1;
        }

        // Act & Assert: the content does not parse as one paragraph, so nothing is proved, even with no edit
        Assert.False(ParagraphProof.Holds(text, lines, []));
    }
}
