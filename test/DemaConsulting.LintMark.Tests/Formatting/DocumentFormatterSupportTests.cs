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

using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the supporting types of the DocumentFormatter unit (line arithmetic, the inline map, the leaf
///     whitespace edits, the line ownership map and the front matter guard) answer what the passes ask of them.
/// </summary>
public class DocumentFormatterSupportTests
{
    /// <summary>
    ///     Creates the line layout of LF-normalized text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The layout.</returns>
    private static LineLayout Layout(string text) => new(SourceDocument.FromText("a.md", text));

    /// <summary>
    ///     Builds the inline map of the first paragraph of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The map.</returns>
    private static InlineMap MapOfFirstParagraph(string text)
    {
        var document = BlockFormatterAssertions.Parse(text);
        var paragraph = (ParagraphBlock)BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph).Node!;
        return InlineMap.Build(paragraph.Inline);
    }

    /// <summary>
    ///     Test that a layout counts the lines of a text, not counting the empty line after a final line feed.
    /// </summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("a\n", 1)]
    [InlineData("a\n\n", 2)]
    [InlineData("a\nb", 2)]
    [InlineData("\n", 1)]
    public void LineLayout_LineCount_Text_CountsTheLines(string text, int expected) =>
        Assert.Equal(expected, Layout(text).LineCount);

    /// <summary>
    ///     Test that line starts, line ends and the line of an offset agree.
    /// </summary>
    [Fact]
    public void LineLayout_LineStartLineEndLineOf_Text_AgreeOnLineExtents()
    {
        // Arrange: two lines, the second without a line feed
        var layout = Layout("ab\ncd");

        // Act & Assert: starts, ends and the line of every offset (including the end of the text)
        Assert.Equal([0, 3], [layout.LineStart(0), layout.LineStart(1)]);
        Assert.Equal([2, 5], [layout.LineEnd(0), layout.LineEnd(1)]);
        Assert.Equal([0, 0, 0, 1, 1, 1], Enumerable.Range(0, 6).Select(layout.LineOf));
    }

    /// <summary>
    ///     Test that a line outside the text is rejected.
    /// </summary>
    [Fact]
    public void LineLayout_LineStart_LineOutsideTheText_Throws()
    {
        // Arrange: a one-line text
        var layout = Layout("a\n");

        // Act & Assert: negative and too large lines are rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.LineStart(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.LineEnd(5));
    }

    /// <summary>
    ///     Test that the line after the last one can be asked for only when the text ends with a line feed.
    /// </summary>
    [Fact]
    public void LineLayout_LineStart_LineAfterTheLast_IsAllowedOnlyAfterAFinalLineFeed()
    {
        // Arrange: one text with and one without a final line feed
        var withFeed = Layout("ab\n");
        var withoutFeed = Layout("ab");

        // Act & Assert: the line equal to the line count exists after a line feed (an empty line) and not otherwise
        Assert.Equal(3, withFeed.LineStart(withFeed.LineCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => withoutFeed.LineStart(withoutFeed.LineCount));
    }

    /// <summary>
    ///     Test that a missing source is rejected.
    /// </summary>
    [Fact]
    public void LineLayout_Constructor_NullSource_Throws() =>
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => new LineLayout(null!)).ParamName);

    /// <summary>
    ///     Test that blank means empty or only spaces and tabs, and that the last line with content ignores blank lines after it.
    /// </summary>
    [Fact]
    public void LineLayout_IsBlankAndLastContentLine_BlankLines_AreRecognized()
    {
        // Arrange: a line with content, a line with blanks, an empty line
        var layout = Layout("a\n \t \n\n");

        // Act & Assert: blank lines are blank, and the last content line is the first
        Assert.False(layout.IsBlank(0));
        Assert.True(layout.IsBlank(1));
        Assert.True(layout.IsBlank(2));
        Assert.Equal(0, layout.LastContentLine(0, 2));
        Assert.Equal(1, layout.LastContentLine(1, 2));
    }

    /// <summary>
    ///     Test that a tab advances to the next multiple of four columns.
    /// </summary>
    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(7, 8)]
    public void LineLayout_NextTabStop_Column_ReturnsTheNextMultipleOfFour(int column, int expected) =>
        Assert.Equal(expected, LineLayout.NextTabStop(column));

    /// <summary>
    ///     Test that a line that starts with a plus sign after quote and list markers looks like a grid table border.
    /// </summary>
    [Theory]
    [InlineData("+---+---+", true)]
    [InlineData("  +---+", true)]
    [InlineData("> +---+", true)]
    [InlineData("- +---+", true)]
    [InlineData("1. +---+", true)]
    [InlineData("> - 10) +---+", true)]
    [InlineData("text +---+", false)]
    [InlineData("-+---+", false)]
    [InlineData("1.+---+", false)]
    [InlineData("12 +---+", false)]
    [InlineData("", false)]
    public void LineLayout_LooksLikeGridBorder_Line_RecognizesTheStart(string line, bool expected) =>
        Assert.Equal(expected, Layout(line + "\n").LooksLikeGridBorder(0));

    /// <summary>
    ///     Test that the inline map tells plain text, protected spans and line breaks apart.
    /// </summary>
    [Fact]
    public void InlineMap_Build_ParagraphWithCodeAndBreaks_ClassifiesTheOffsets()
    {
        // Arrange: text, a code span, a hard break with spaces, a backslash break and a soft break
        const string text = "ab `cd`  \nef\\\ngh\nij\n";
        var map = MapOfFirstParagraph(text);

        // Act & Assert: letters outside the code span are text, the code span is protected, breaks are found
        Assert.True(map.IsText(0));
        Assert.False(map.IsText(text.IndexOf('`', StringComparison.Ordinal)));
        Assert.True(map.IsProtected(text.IndexOf("cd", StringComparison.Ordinal)));
        Assert.False(map.IsProtected(0));
        var firstLineFeed = text.IndexOf('\n', StringComparison.Ordinal);
        Assert.True(map.IsBreakAt(firstLineFeed));
        Assert.True(map.IsSpaceBreakAt(firstLineFeed));
        var secondLineFeed = text.IndexOf('\n', firstLineFeed + 1);
        Assert.True(map.IsBreakAt(secondLineFeed - 1));
        Assert.False(map.IsSpaceBreakAt(secondLineFeed - 1));
        var thirdLineFeed = text.IndexOf('\n', secondLineFeed + 1);
        Assert.True(map.IsBreakAt(thirdLineFeed));
        Assert.False(map.IsSpaceBreakAt(thirdLineFeed));
        Assert.False(map.IsBreakAt(text.Length - 1));
    }

    /// <summary>
    ///     Test that a link protects its destination and title.
    /// </summary>
    [Fact]
    public void InlineMap_Build_Link_ProtectsDestinationAndTitle()
    {
        // Arrange: a link with a title
        const string text = "[label](http://x \"title\")\n";
        var map = MapOfFirstParagraph(text);

        // Act & Assert: the label is text, the destination and the title are protected
        Assert.True(map.IsText(text.IndexOf("label", StringComparison.Ordinal)));
        Assert.True(map.IsProtected(text.IndexOf("http", StringComparison.Ordinal)));
        Assert.True(map.IsProtected(text.IndexOf("title", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that the empty map knows nothing.
    /// </summary>
    [Fact]
    public void InlineMap_Build_NullRoot_ReturnsTheEmptyMap()
    {
        // Act: build from nothing
        var map = InlineMap.Build(null);

        // Assert: the shared empty map, which answers no to every question
        Assert.Same(InlineMap.Empty, map);
        Assert.False(map.IsText(0));
        Assert.False(map.IsProtected(0));
        Assert.False(map.IsBreakAt(0));
        Assert.False(map.IsSpaceBreakAt(0));
    }

    /// <summary>
    ///     Test that the tabs of plain text are found with the spaces that reach the next tab stop.
    /// </summary>
    [Fact]
    public void LeafWhitespace_CollectTabs_TextWithTabs_ReturnsTabsInTextOnly()
    {
        // Arrange: a tab in text, a tab in a code span, and a leading tab on the continuation line
        const string text = "a\tb `c\td` e\n\tf\tg\n";
        var layout = Layout(text);
        var map = MapOfFirstParagraph(text);

        // Act: collect on both lines
        var first = LeafWhitespace.CollectTabs(layout, map, 0, layout.LineEnd(0), 0);
        var second = LeafWhitespace.CollectTabs(layout, map, layout.LineStart(1), layout.LineEnd(1), 0);

        // Assert: only the tab in text on the first line (three spaces from column one), the leading tab is not text and the second tab follows text
        Assert.Equal([(1, 3)], first);
        Assert.Single(second);
        Assert.Equal(text.LastIndexOf('\t'), second[0].Offset);
    }

    /// <summary>
    ///     Test that a stretch that starts in text replaces a tab that follows a code span, which a stretch from the start of a line does not.
    /// </summary>
    [Fact]
    public void LeafWhitespace_CollectTabs_StartsInText_ReplacesTheTabAfterACodeSpan()
    {
        // Arrange: a code span, a tab and text
        const string text = "`a`\tb\n";
        var layout = Layout(text);
        var map = MapOfFirstParagraph(text);

        // Act: collect from the start of the line and as a stretch that starts in text
        var fromLineStart = LeafWhitespace.CollectTabs(layout, map, 0, layout.LineEnd(0), 0);
        var fromText = LeafWhitespace.CollectTabs(layout, map, 0, layout.LineEnd(0), 0, startsInText: true);

        // Assert: the tab follows no plain text from the line start, and is replaced by one space (column three to four) otherwise
        Assert.Empty(fromLineStart);
        Assert.Equal([(3, 1)], fromText);
    }

    /// <summary>
    ///     Test that missing arguments are rejected by the leaf whitespace helpers.
    /// </summary>
    [Fact]
    public void LeafWhitespace_AddLineEditsAndAddTrailingEdit_NullArguments_Throw()
    {
        // Arrange: a layout, a map and an edit set
        var layout = Layout("a\n");
        var map = InlineMap.Empty;
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("layout", Assert.Throws<ArgumentNullException>(() => LeafWhitespace.AddLineEdits(null!, map, 0, 0, 0, true, true, edits)).ParamName);
        Assert.Equal("map", Assert.Throws<ArgumentNullException>(() => LeafWhitespace.AddLineEdits(layout, null!, 0, 0, 0, true, true, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => LeafWhitespace.AddLineEdits(layout, map, 0, 0, 0, true, true, null!)).ParamName);
        Assert.Equal("layout", Assert.Throws<ArgumentNullException>(() => LeafWhitespace.AddTrailingEdit(null!, 0, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => LeafWhitespace.AddTrailingEdit(layout, 0, null!)).ParamName);
    }

    /// <summary>
    ///     Test that the trailing whitespace edit leaves blank lines, backslash runs and grid table lookalikes alone.
    /// </summary>
    [Theory]
    [InlineData("   \n")]
    [InlineData("a\\  \n")]
    [InlineData("+---+  \n")]
    public void LeafWhitespace_AddTrailingEdit_LinesThatMustStay_ProposeNothing(string text)
    {
        // Arrange: a line that must keep its whitespace
        var edits = new EditSet();

        // Act: ask for the trailing edit of the first line
        LeafWhitespace.AddTrailingEdit(Layout(text), 0, edits);

        // Assert: no edit
        Assert.True(edits.IsEmpty);
    }

    /// <summary>
    ///     Test that the ownership map knows which lines are claimed, protected, in containers and the top-level blocks.
    /// </summary>
    [Fact]
    public void LineOwnership_Build_MixedDocument_ClassifiesTheLines()
    {
        // Arrange: a heading, code, a list with a paragraph, and a paragraph; the formatter claims headings only
        var document = BlockFormatterAssertions.Parse("# H\n\n```\ncode\n```\n\n- item\n\ntext\n");
        var layout = new LineLayout(document.Source);

        // Act: build the map
        var ownership = LineOwnership.Build(document, layout, block => block.Kind == BlockKind.Heading);

        // Assert: the heading is claimed, the fence is protected, the list item is in a container, the rest is free
        Assert.True(ownership.IsClaimed(0));
        Assert.False(ownership.IsProtected(0));
        Assert.True(ownership.IsProtected(3));
        Assert.False(ownership.IsClaimed(3));
        Assert.True(ownership.IsInContainer(6));
        Assert.False(ownership.IsInContainer(8));
        Assert.Null(ownership.LeafAt(1));
        Assert.Equal(BlockKind.Paragraph, ownership.LeafAt(8)!.Kind);
        Assert.Equal(
            [BlockKind.Heading, BlockKind.FencedCode, BlockKind.List, BlockKind.Paragraph],
            ownership.TopLevel.Select(block => block.Kind));
    }

    /// <summary>
    ///     Test that the lines of a top-level indented code block also count a trailing whitespace-only line that is wider
    ///     than the indentation (what is left of it is code) and no other whitespace-only line.
    /// </summary>
    [Theory]
    [InlineData("x\n\n    code\n      \n", 2, 3)]
    [InlineData("x\n\n    code\n     \n\ny\n", 2, 3)]
    [InlineData("x\n\n    code\n\t  \n\ny\n", 2, 3)]
    [InlineData("x\n\n    code\n      \n    \n", 2, 3)]
    [InlineData("x\n\n    code\n    \n", 2, 2)]
    [InlineData("x\n\n    code\n\t\n", 2, 2)]
    [InlineData("x\n\n    code\n  \n    more\n", 2, 4)]
    public void LineOwnership_LinesOf_IndentedCodeWithWhitespaceLines_CountsTheRestOfAWideLine(string text, int expectedFirst, int expectedLast)
    {
        // Arrange: the document and its indented code block
        var document = BlockFormatterAssertions.Parse(text);
        var layout = new LineLayout(document.Source);
        var code = BlockFormatterAssertions.FirstBlock(document, BlockKind.IndentedCode);

        // Act: measure the block
        var lines = LineOwnership.LinesOf(layout, code);

        // Assert: the block ends at its last line with content or with more than the indentation
        Assert.Equal((expectedFirst, expectedLast), lines);
    }

    /// <summary>
    ///     Test that an indented code block inside a list keeps the measure up to its last line with content.
    /// </summary>
    [Fact]
    public void LineOwnership_LinesOf_IndentedCodeInList_EndsAtTheLastContentLine()
    {
        // Arrange: nested indented code followed by a wide whitespace-only line
        var document = BlockFormatterAssertions.Parse("- a\n\n      code\n          \n");
        var layout = new LineLayout(document.Source);
        var code = BlockFormatterAssertions.FirstBlock(document, BlockKind.IndentedCode);

        // Act & Assert: the parser drops that line from nested code, and so does the measure
        Assert.Equal((2, 2), LineOwnership.LinesOf(layout, code));
    }

    /// <summary>
    ///     Test that the lines after the opening of an unclosed fence inside a container belong to the fence up to the next top-level block.
    /// </summary>
    [Fact]
    public void LineOwnership_Build_UnclosedFenceInList_ClaimsTheLinesUpToTheNextTopLevelBlock()
    {
        // Arrange: a list item with an unclosed fence, then blank lines and a heading
        var document = BlockFormatterAssertions.Parse("- ```\n  code\n\n  more\n\n# H\n");
        var layout = new LineLayout(document.Source);

        // Act: build the map
        var ownership = LineOwnership.Build(document, layout, _ => false);

        // Assert: every line before the heading is protected
        Assert.All(Enumerable.Range(0, 5), line => Assert.True(ownership.IsProtected(line), $"line {line}"));
        Assert.False(ownership.IsProtected(5));
    }

    /// <summary>
    ///     Test that a block keeps its trailing blank lines only when they can be its content: an unclosed fence, a
    ///     block the tool does not model, and an HTML block of types 1 to 5 whose end condition is missing; an HTML
    ///     block that its end condition closes, a block that a blank line ends, and a paragraph do not.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n", true, true)]
    [InlineData("```\ncode\n", false, true)]
    [InlineData("<!-- c -->\n\ntext\n", true, false)]
    [InlineData("<!-- c -->\n", false, false)]
    [InlineData("<!-- c\n\n", false, true)]
    [InlineData("<!-- c\nmore\n", false, true)]
    [InlineData("<script>\nx\n</script>\n", false, false)]
    [InlineData("<SCRIPT>\nx\n</SCRIPT>\n", false, false)]
    [InlineData("<script>\nx\n", false, true)]
    [InlineData("<pre>\nx\n</pre>\n", false, false)]
    [InlineData("<pre>\nx\n", false, true)]
    [InlineData("<style>\nx\n</style>\n", false, false)]
    [InlineData("<style>\nx\n", false, true)]
    [InlineData("<?php\n?>\n", false, false)]
    [InlineData("<?php\nx\n", false, true)]
    [InlineData("<![CDATA[\nx\n]]>\n", false, false)]
    [InlineData("<![CDATA[\nx\n", false, true)]
    [InlineData("<!DOCTYPE html>\n", false, false)]
    [InlineData("<!DOCTYPE html\nx\n", false, true)]
    [InlineData("<div>\nx\n</div>\n", false, false)]
    [InlineData("text\n", false, false)]
    public void LineOwnership_KeepsTrailingBlankLines_Blocks_AreClassified(string text, bool contentFollows, bool expected)
    {
        // Arrange: the first block of the document
        var document = BlockFormatterAssertions.Parse(text);
        var layout = new LineLayout(document.Source);

        // Act & Assert: the block keeps its trailing blank lines exactly when they can be its content
        Assert.Equal(expected, LineOwnership.KeepsTrailingBlankLines(layout, document.Blocks[0], contentFollows));
    }

    /// <summary>
    ///     Test that missing arguments are rejected when asking whether a block keeps its trailing blank lines.
    /// </summary>
    [Fact]
    public void LineOwnership_KeepsTrailingBlankLines_NullArguments_Throw()
    {
        // Arrange: a document, its layout and block
        var document = BlockFormatterAssertions.Parse("text\n");
        var layout = new LineLayout(document.Source);

        // Act & Assert: each argument is checked and named
        Assert.Equal("layout", Assert.Throws<ArgumentNullException>(() => LineOwnership.KeepsTrailingBlankLines(null!, document.Blocks[0], false)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => LineOwnership.KeepsTrailingBlankLines(layout, null!, false)).ParamName);
    }

    /// <summary>
    ///     Test that an inline map built with a shift places every span, break and protected stretch that far from where the parser put it.
    /// </summary>
    [Fact]
    public void InlineMap_Build_WithShift_MovesEverySpan()
    {
        // Arrange: a paragraph with text, a code span and a hard break, built as the parser gave it and shifted by ten
        const string text = "ab `cd`  \nef\n";
        var document = BlockFormatterAssertions.Parse(text);
        var inline = ((ParagraphBlock)BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph).Node!).Inline;

        // Act: build both maps
        var plain = InlineMap.Build(inline);
        var shifted = InlineMap.Build(inline, 10);

        // Assert: every answer about an offset is the answer of the plain map ten characters earlier
        Assert.All(
            Enumerable.Range(0, text.Length),
            offset =>
            {
                Assert.Equal(plain.IsText(offset), shifted.IsText(offset + 10));
                Assert.Equal(plain.IsProtected(offset), shifted.IsProtected(offset + 10));
                Assert.Equal(plain.IsBreakAt(offset), shifted.IsBreakAt(offset + 10));
                Assert.Equal(plain.IsSpaceBreakAt(offset), shifted.IsSpaceBreakAt(offset + 10));
            });
        Assert.True(shifted.IsSpaceBreakAt(text.IndexOf('\n', StringComparison.Ordinal) + 10));
    }

    /// <summary>
    ///     Test that the front matter guard recognizes a first line of dashes without front matter.
    /// </summary>
    [Theory]
    [InlineData("---\n\ntext\n", true)]
    [InlineData("---  \n\ntext\n", true)]
    [InlineData("---\ntitle: x\n---\n\ntext\n", false)]
    [InlineData("text\n\n---\n", false)]
    [InlineData("----\n\ntext\n", false)]
    [InlineData("", false)]
    public void FrontMatterGuard_CouldOpenFrontMatter_Document_AnswersForTheFirstLine(string text, bool expected) =>
        Assert.Equal(expected, FrontMatterGuard.CouldOpenFrontMatter(BlockFormatterAssertions.Parse(text)));

    /// <summary>
    ///     Test that a missing document is rejected by the front matter guard.
    /// </summary>
    [Fact]
    public void FrontMatterGuard_CouldOpenFrontMatter_NullDocument_Throws() =>
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => FrontMatterGuard.CouldOpenFrontMatter(null!)).ParamName);
}
