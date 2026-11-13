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
    ///     Test that the lines of a block are measured up to the last line with content, not up to whitespace-only lines the parser may include.
    /// </summary>
    [Fact]
    public void LineOwnership_LinesOf_IndentedCodeFollowedByWhitespaceLine_EndsAtTheLastContentLine()
    {
        // Arrange: indented code followed by a line of four spaces and text
        var document = BlockFormatterAssertions.Parse("    code\n    \n\ntext\n");
        var layout = new LineLayout(document.Source);
        var code = BlockFormatterAssertions.FirstBlock(document, BlockKind.IndentedCode);

        // Act: measure the block
        var (first, last) = LineOwnership.LinesOf(layout, code);

        // Assert: only the first line
        Assert.Equal((0, 0), (first, last));
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
    ///     Test that the blocks that keep their trailing blank lines are the unclosed fence, the blocks the tool does not model and an HTML block nothing closes.
    /// </summary>
    [Fact]
    public void LineOwnership_KeepsTrailingBlankLines_Blocks_AreClassified()
    {
        // Arrange: an unclosed fence, a closed comment followed by text, and a paragraph
        var fence = BlockFormatterAssertions.FirstBlock(BlockFormatterAssertions.Parse("```\ncode\n"), BlockKind.UnclosedFence);
        var comment = BlockFormatterAssertions.FirstBlock(BlockFormatterAssertions.Parse("<!-- c -->\n\ntext\n"), BlockKind.HtmlBlock);
        var div = BlockFormatterAssertions.FirstBlock(BlockFormatterAssertions.Parse("<div>\nx\n</div>\n"), BlockKind.HtmlBlock);
        var paragraph = BlockFormatterAssertions.FirstBlock(BlockFormatterAssertions.Parse("text\n"), BlockKind.Paragraph);

        // Act & Assert: the fence keeps them, the comment only when nothing follows, the div and the paragraph never
        Assert.True(LineOwnership.KeepsTrailingBlankLines(fence, contentFollows: true));
        Assert.False(LineOwnership.KeepsTrailingBlankLines(comment, contentFollows: true));
        Assert.True(LineOwnership.KeepsTrailingBlankLines(comment, contentFollows: false));
        Assert.False(LineOwnership.KeepsTrailingBlankLines(div, contentFollows: false));
        Assert.False(LineOwnership.KeepsTrailingBlankLines(paragraph, contentFollows: false));
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
