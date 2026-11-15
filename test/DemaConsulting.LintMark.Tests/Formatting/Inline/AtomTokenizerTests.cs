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
using DemaConsulting.LintMark.Formatting.Inline;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the AtomTokenizer class cuts a paragraph into the words a wrap may move, keeps links, code spans, inline
///     HTML and the other constructs whole, and refuses a paragraph it cannot cut safely.
/// </summary>
public class AtomTokenizerTests
{
    /// <summary>
    ///     Tokenizes the first paragraph of a text.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The tokenized paragraph, or <see langword="null"/> when the tokenizer refuses it.</returns>
    private static TokenizedParagraph? Tokenize(string text)
    {
        var document = BlockFormatterAssertions.Parse(text);
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var inline = ((ParagraphBlock)block.Node!).Inline!;
        return AtomTokenizer.Tokenize(document.Source.Text, inline, DocumentParser.GetLines(document, block));
    }

    /// <summary>
    ///     Tokenizes the first paragraph of a text, which must be accepted, and returns its atoms of all segments in order.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The atoms.</returns>
    private static string[] Atoms(string text) =>
        [.. Tokenize(text)!.Segments.SelectMany(static segment => segment.Atoms)];

    /// <summary>
    ///     Test that plain words are separated at blanks and line breaks, whatever the amount of whitespace.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_PlainWords_AreSeparatedAtWhitespace()
    {
        // Act: tokenize words separated by one space, several spaces and a line break
        var tokenized = Tokenize("a b   c\nd\n");

        // Assert: four atoms in one segment that ends with the paragraph
        Assert.NotNull(tokenized);
        var segment = Assert.Single(tokenized.Segments);
        Assert.Equal(["a", "b", "c", "d"], segment.Atoms);
        Assert.Equal(HardBreakKind.None, segment.End);
    }

    /// <summary>
    ///     Test that a link or an image, with its text, destination and title, is one atom.
    /// </summary>
    [Theory]
    [InlineData("see [a b](http://x.org/y \"t u\") now\n", new[] { "see", "[a b](http://x.org/y \"t u\")", "now" })]
    [InlineData("see ![alt text](i.png) now\n", new[] { "see", "![alt text](i.png)", "now" })]
    [InlineData("see [a *b* `c d`](u) now\n", new[] { "see", "[a *b* `c d`](u)", "now" })]
    [InlineData("see [a b][ref] now\n\n[ref]: http://x.org\n", new[] { "see", "[a b][ref]", "now" })]
    [InlineData("see [a b] now\n\n[a b]: http://x.org\n", new[] { "see", "[a b]", "now" })]
    [InlineData("see ([a b](u)). now\n", new[] { "see", "([a b](u)).", "now" })]
    public void AtomTokenizer_Tokenize_LinksAndImages_AreOneAtom(string text, string[] expected) =>
        Assert.Equal(expected, Atoms(text));

    /// <summary>
    ///     Test that a code span with blanks inside, with one or more backticks around it, is one atom.
    /// </summary>
    [Theory]
    [InlineData("a `b c` d\n", new[] { "a", "`b c`", "d" })]
    [InlineData("a ``b ` c`` d\n", new[] { "a", "``b ` c``", "d" })]
    [InlineData("a `` b ` c `` d\n", new[] { "a", "`` b ` c ``", "d" })]
    public void AtomTokenizer_Tokenize_CodeSpans_AreOneAtom(string text, string[] expected) =>
        Assert.Equal(expected, Atoms(text));

    /// <summary>
    ///     Test that an autolink and a bare URL are one atom each.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_AngleBracketLinksAndBareUrls_AreOneAtom() =>
        Assert.Equal(["<http://x.org/a>", "and", "http://y.org/b", "and", "web.z.org"], Atoms("<http://x.org/a> and http://y.org/b and web.z.org\n"));

    /// <summary>
    ///     Test that an inline HTML tag with blanks inside it is not split, and the text around the tags stays separate words.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_InlineHtml_TagsAreNotSplit() =>
        Assert.Equal(["<span class=\"a b\">x", "y</span>", "<br/>"], Atoms("<span class=\"a b\">x y</span> <br/>\n"));

    /// <summary>
    ///     Test that inline math with blanks inside it is one atom.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_InlineMath_IsOneAtom() =>
        Assert.Equal(["x", "$a + b$", "y"], Atoms("x $a + b$ y\n"));

    /// <summary>
    ///     Test that character references and footnote references stay whole in their words.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_EntitiesAndFootnoteReferences_StayWhole() =>
        Assert.Equal(["a", "&amp;", "&#35;", "text[^1]", "more"], Atoms("a &amp; &#35; text[^1] more\n\n[^1]: note\n"));

    /// <summary>
    ///     Test that Pandoc attribute groups, citations and LaTeX commands with blanks inside are one atom.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_PandocConstructs_AreOneAtom() =>
        Assert.Equal(["x", "{#id .cls}", "[@key, p. 3]", "y\\cmd{a b}", "z"], Atoms("x {#id .cls} [@key, p. 3] y\\cmd{a b} z\n"));

    /// <summary>
    ///     Test that escaped characters stay inside their words.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_EscapedCharacters_StayInTheirWords() =>
        Assert.Equal(["a", "\\*not", "emphasis\\*", "b", "\\\\", "c"], Atoms("a \\*not emphasis\\* b \\\\ c\n"));

    /// <summary>
    ///     Test that a hard line break written as two spaces ends a segment and the form is remembered.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_HardBreakWithSpaces_EndsASegment()
    {
        // Act: tokenize a paragraph with a hard break of three spaces
        var tokenized = Tokenize("a b   \nc d\n");

        // Assert: two segments, the first ends with the spaces form
        Assert.NotNull(tokenized);
        Assert.Equal(2, tokenized.Segments.Length);
        Assert.Equal(["a", "b"], tokenized.Segments[0].Atoms);
        Assert.Equal(HardBreakKind.Spaces, tokenized.Segments[0].End);
        Assert.Equal(["c", "d"], tokenized.Segments[1].Atoms);
        Assert.Equal(HardBreakKind.None, tokenized.Segments[1].End);
    }

    /// <summary>
    ///     Test that a hard line break written as a backslash ends a segment, keeps the backslash in the last word and an escaped backslash does not break.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_HardBreakWithBackslash_EndsASegment()
    {
        // Act: tokenize a backslash break and a line that ends with an escaped backslash
        var hard = Tokenize("a b\\\nc d\n");
        var soft = Tokenize("a b\\\\\nc d\n");

        // Assert: the backslash form splits, the escaped backslash does not
        Assert.NotNull(hard);
        Assert.Equal(2, hard.Segments.Length);
        Assert.Equal(["a", "b\\"], hard.Segments[0].Atoms);
        Assert.Equal(HardBreakKind.Backslash, hard.Segments[0].End);
        Assert.NotNull(soft);
        Assert.Single(soft.Segments);
        Assert.Equal(["a", "b\\\\", "c", "d"], soft.Segments[0].Atoms);
    }

    /// <summary>
    ///     Test that a break after a word that ends with an odd number of backslashes is not allowed, because it would become a hard break.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_WordEndingInBackslash_IsJoinedWithTheNextWord()
    {
        // Act: tokenize a path that ends with one backslash and one that ends with two
        var odd = Atoms("see C:\\temp\\ next word\n");
        var even = Atoms("see C:\\temp\\\\ next word\n");

        // Assert: the odd one is joined with the next word, the even one is not
        Assert.Equal(["see", "C:\\temp\\ next", "word"], odd);
        Assert.Equal(["see", "C:\\temp\\\\", "next", "word"], even);
    }

    /// <summary>
    ///     Test that a break after the marker of a task list item is not allowed.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_TaskMarker_IsJoinedWithTheNextWord() =>
        Assert.Equal(["[ ] task", "item", "text"], Atoms("- [ ] task item text\n"));

    /// <summary>
    ///     Test that a code span that continues on the next line becomes one atom with the line break as a space.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_MultiLineCodeSpan_JoinsTheLines() =>
        Assert.Equal(["a", "`b c`", "d"], Atoms("a `b\nc` d\n"));

    /// <summary>
    ///     Test that a code span with a blank or a delimiter next to its line break is refused.
    /// </summary>
    [Theory]
    [InlineData("a `b \nc` d\n")]
    [InlineData("a `\nb` c\n")]
    [InlineData("a `b\n` c\n")]
    [InlineData("a ``b`\nc`` d\n")]
    public void AtomTokenizer_Tokenize_MultiLineCodeSpanWithBlankOrDelimiterAtTheBreak_IsRefused(string text) =>
        Assert.Null(Tokenize(text));

    /// <summary>
    ///     Test that a code span with a line break that is padded with a space on both sides is refused, because joined into one
    ///     line it would be rewritten by the code span rule and the text would not be stable.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_MultiLineCodeSpanPaddedOnBothSides_IsRefused()
    {
        // Act & Assert: padded on both sides is refused, padded on one side only is joined
        Assert.Null(Tokenize("a ` b\nc ` d\n"));
        Assert.Equal(["a", "` b c`", "d"], Atoms("a ` b\nc` d\n"));
    }

    /// <summary>
    ///     Test that the text of a link that continues on the next line becomes one atom with the line break as a space.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_MultiLineLinkText_JoinsTheLines() =>
        Assert.Equal(["[a b](u)", "c"], Atoms("[a\nb](u) c\n"));

    /// <summary>
    ///     Test that a construct that cannot be joined safely and spans lines is refused: a link title on the next line, an
    ///     image, inline HTML, a hard break inside link text.
    /// </summary>
    [Theory]
    [InlineData("[a](u\n\"t\") x\n")]
    [InlineData("![a\nb](u) x\n")]
    [InlineData("<span\nclass=\"x\"> y\n")]
    [InlineData("[a  \nb](u) x\n")]
    [InlineData("[a\\\nb](u) x\n")]
    public void AtomTokenizer_Tokenize_MultiLineConstructThatCannotBeJoined_IsRefused(string text) =>
        Assert.Null(Tokenize(text));

    /// <summary>
    ///     Test that lines whose ranges do not fit the text of the inline tree make the paragraph untrustworthy.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_LinesThatDoNotFitTheSpans_IsRefused()
    {
        // Arrange: a paragraph with a link, and lines that end before the link does
        var document = BlockFormatterAssertions.Parse("[a](b) c\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var inline = ((ParagraphBlock)block.Node!).Inline!;
        var lines = new List<LeafLine> { new(1, 0, 0, 3, false) };

        // Act & Assert: the span of the link is outside the line
        Assert.Null(AtomTokenizer.Tokenize(document.Source.Text, inline, lines));
    }

    /// <summary>
    ///     Test that atoms of a quote or a list item are cut from the content of the lines, without the container prefixes.
    /// </summary>
    [Fact]
    public void AtomTokenizer_Tokenize_ContainerPrefixes_AreNotPartOfTheAtoms()
    {
        // Act: tokenize a paragraph in a quote and one in a list item with a continuation line
        var quote = Atoms("> a b\n> c d\n");
        var item = Atoms("- a b\n  c d\n");

        // Assert: only the words
        Assert.Equal(["a", "b", "c", "d"], quote);
        Assert.Equal(["a", "b", "c", "d"], item);
    }
}
