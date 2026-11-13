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
using System.Globalization;
using System.Text;
using DemaConsulting.LintMark.Document;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the DocumentParser class.
/// </summary>
public class DocumentParserTests
{
    /// <summary>
    ///     Parses text as a document named <c>t.md</c>.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("t.md", text));

    /// <summary>
    ///     Summarizes the blocks of a document as kind plus the exact source text of the effective span.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>One entry per block.</returns>
    private static List<(BlockKind Kind, string Text)> Summarize(ParsedDocument document) =>
        [.. document.Blocks.Select(b => (b.Kind, document.Source.Text.Substring(b.Start, b.EndInclusive - b.Start + 1)))];

    /// <summary>
    ///     Summarizes the blocks of a document as kind, nesting depth and the exact source text of the effective span.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>One entry per block.</returns>
    private static List<(BlockKind Kind, int Depth, string Text)> SummarizeWithDepth(ParsedDocument document) =>
        [.. document.Blocks.Select(b => (b.Kind, b.Depth, document.Source.Text.Substring(b.Start, b.EndInclusive - b.Start + 1)))];

    /// <summary>
    ///     Gets the exact source text of a block.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="block">The block.</param>
    /// <returns>The text of the effective span.</returns>
    private static string TextOf(ParsedDocument document, BlockInfo block) =>
        document.Source.Text.Substring(block.Start, block.EndInclusive - block.Start + 1);

    /// <summary>
    ///     Finds the single block of a kind.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="kind">The kind to find.</param>
    /// <returns>The block.</returns>
    private static BlockInfo Single(ParsedDocument document, BlockKind kind) => Assert.Single(document.Blocks, b => b.Kind == kind);

    /// <summary>
    ///     Gets the text of the content of each line of a leaf block.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="leaf">The leaf block.</param>
    /// <returns>The content text of each line.</returns>
    private static List<string> LineContents(ParsedDocument document, BlockInfo leaf) =>
        [.. DocumentParser.GetLines(document, leaf).Select(l => document.Source.Text[l.ContentStart..l.ContentEnd])];

    /// <summary>
    ///     Determines whether a fallback location addresses the first border character of a border-shaped line.
    /// </summary>
    /// <param name="lines">The lines of the document text.</param>
    /// <param name="fallback">The reported location.</param>
    /// <returns><see langword="true"/> when the column is a plus sign preceded only by blanks and quote markers and followed only by border text.</returns>
    private static bool IsBorderLocation(string[] lines, GridTableFallback fallback)
    {
        if (fallback.Line < 1 || fallback.Line > lines.Length)
        {
            return false;
        }

        var line = lines[fallback.Line - 1];
        if (fallback.Column < 1 || fallback.Column > line.Length || line[fallback.Column - 1] != '+')
        {
            return false;
        }

        return line[..(fallback.Column - 1)].All(static c => c is ' ' or '>')
            && line[(fallback.Column - 1)..].All(static c => c is '+' or '-' or ':' or ' ' or '\t');
    }

    /// <summary>
    ///     Test that a simple document flattens to its blocks in source order.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_SimpleDocument_ReturnsBlocksInSourceOrder()
    {
        // Arrange: heading, paragraph, thematic break
        const string text = "# Title\n\nSome text.\n\n---\n";

        // Act: parse
        var document = Parse(text);

        // Assert: blocks in source order with exact spans
        Assert.Equal(
            [(BlockKind.Heading, "# Title"), (BlockKind.Paragraph, "Some text."), (BlockKind.ThematicBreak, "---")],
            Summarize(document));
    }

    /// <summary>
    ///     Test that an empty document parses to no blocks.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_EmptyDocument_ReturnsNoBlocks()
    {
        // Arrange / Act: parse empty text
        var document = Parse(string.Empty);

        // Assert: nothing found, collections are empty
        Assert.Empty(document.Blocks);
        Assert.Empty(document.References.Definitions);
        Assert.Empty(document.Suppressions.Ranges);
    }

    /// <summary>
    ///     Test that a null source is rejected.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_NullSource_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: null is rejected
        Assert.Throws<ArgumentNullException>(() => DocumentParser.Parse(null!));
    }

    /// <summary>
    ///     Test that nested containers get increasing depth and the correct parent chain.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_NestedContainers_ReportsDepthAndParents()
    {
        // Arrange: a quote containing a list item with a paragraph
        var document = Parse("> - a\n>   b\n");

        // Act: read the block kinds with their depths
        var kinds = document.Blocks.Select(b => (b.Kind, b.Depth)).ToList();

        // Assert: quote > list > item > paragraph, each block's parent is the node of the block before it
        Assert.Equal(
            [(BlockKind.BlockQuote, 0), (BlockKind.List, 1), (BlockKind.ListItem, 2), (BlockKind.Paragraph, 3)],
            kinds);
        Assert.Null(document.Blocks[0].Parent);
        for (var i = 1; i < document.Blocks.Length; i++)
        {
            Assert.Same(document.Blocks[i - 1].Node, document.Blocks[i].Parent);
        }
    }

    /// <summary>
    ///     Test that the pipe table extension classifies tables with GFM rules.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_PipeTable_IsTableWithGfmCells()
    {
        // Arrange: a pipe table whose cell holds a code span with an escaped pipe
        var document = Parse("| a |\n|---|\n| `x\\|y` |\n");

        // Act: find the table
        var table = Single(document, BlockKind.Table);

        // Assert: it spans the whole table and the body row has a single cell
        Assert.Equal(0, table.Start);
        var tableBlock = Assert.IsType<Table>(table.Node);
        Assert.Single(Assert.IsType<TableRow>(tableBlock[1]));
    }

    /// <summary>
    ///     Test that grid tables are recognized and distinguished from pipe tables.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_GridTable_IsGridTable()
    {
        // Arrange: a Pandoc grid table
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n| 1 | 2 |\n+---+---+\n");

        // Act: classify
        var kinds = document.Blocks.Select(b => b.Kind).ToList();

        // Assert: one grid table and no pipe table or paragraph
        Assert.Equal([BlockKind.GridTable], kinds);
    }

    /// <summary>
    ///     Test that YAML front matter is a single verbatim block.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FrontMatter_IsFrontMatterBlock()
    {
        // Arrange: front matter followed by a heading
        var document = Parse("---\ntitle: x\n---\n\n# H\n");

        // Act: summarize
        var summary = Summarize(document);

        // Assert: the whole front matter is one block
        Assert.Equal([(BlockKind.FrontMatter, "---\ntitle: x\n---"), (BlockKind.Heading, "# H")], summary);
    }

    /// <summary>
    ///     Test that every block construct of the dialect is classified with its exact source range.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_BlockConstructs_AreClassifiedWithExactSpans()
    {
        // Arrange: alert, math, closed fence, HTML block and task list separated by blank lines
        var document = Parse("> [!NOTE]\n> alert\n\n$$\nx\n$$\n\n```\ncode\n```\n\n<div>\nhi\n</div>\n\n- [x] task\n");

        // Act: summarize every block with its depth
        var summary = SummarizeWithDepth(document);

        // Assert: the complete ordered list of kinds, depths and exact texts, and no unclosed fence
        Assert.Equal(
            [
                (BlockKind.BlockQuote, 0, "> [!NOTE]\n> alert"),
                (BlockKind.Paragraph, 1, "[!NOTE]\n> alert"),
                (BlockKind.MathBlock, 0, "$$\nx\n$$"),
                (BlockKind.FencedCode, 0, "```\ncode\n```"),
                (BlockKind.HtmlBlock, 0, "<div>\nhi\n</div>"),
                (BlockKind.List, 0, "- [x] task"),
                (BlockKind.ListItem, 1, "- [x] task"),
                (BlockKind.Paragraph, 2, "[x] task")
            ],
            summary);
        Assert.IsType<AlertBlock>(document.Blocks[0].Node);
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.UnclosedFence);
    }

    /// <summary>
    ///     Test that GFM and math inline constructs are active in the fixed pipeline.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_InlineConstructs_AreRecognized()
    {
        // Arrange: strikethrough, bare URL autolink, task list items and inline math
        var document = Parse("~~gone~~ and https://example.com here.\n\n- [ ] open\n- [x] done\n\nMath $x^2$ here.\n");
        var text = document.Source.Text;

        // Act: collect inline facts with their exact source text
        var strike = document.Ast.Descendants<EmphasisInline>().Where(e => e.DelimiterChar == '~').ToList();
        var autoLinks = document.Ast.Descendants<LinkInline>().Where(l => l.IsAutoLink).ToList();
        var tasks = document.Ast.Descendants<TaskList>().ToList();
        var math = document.Ast.Descendants<MathInline>().ToList();

        // Assert: each construct exists once (tasks twice) and covers exactly its source text
        string Covered(Inline inline) => text.Substring(inline.Span.Start, inline.Span.Length);
        Assert.Equal("~~gone~~", Covered(Assert.Single(strike)));
        Assert.Equal("https://example.com", Covered(Assert.Single(autoLinks)));
        Assert.Equal([false, true], tasks.Select(t => t.Checked).ToList());
        Assert.Equal(["[ ]", "[x]"], tasks.Select(Covered).ToList());
        Assert.Equal("$x^2$", Covered(Assert.Single(math)));
    }

    /// <summary>
    ///     Test that the pipeline never adds synthetic heading identifiers or attributes.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_Headings_HaveNoAutoIdentifiers()
    {
        // Arrange: a heading and a reference link to it
        var document = Parse("# My Heading\n\n[link](#my-heading)\n");

        // Act: read the heading's attributes
        var heading = document.Ast.Descendants<HeadingBlock>().Single();

        // Assert: no identifier was injected and no synthetic definition exists
        Assert.Null(heading.TryGetAttributes());
        Assert.Empty(document.References.Definitions);
    }

    /// <summary>
    ///     Test that an unclosed fence is recorded with an effective span that reaches the end of the document.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedFence_ExtendsToEndOfDocument()
    {
        // Arrange: a fence that is never closed, followed by text that is really code
        const string text = "para\n\n```\ncode\nmore\n\nafter\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the effective span runs from the fence to the last character; the parser's own span does not
        var fence = Single(document, BlockKind.UnclosedFence);
        Assert.Equal("```\ncode\nmore\n\nafter", text.Substring(fence.Start, fence.EndInclusive - fence.Start + 1));
        Assert.True(fence.Node!.Span.End < text.IndexOf("code", StringComparison.Ordinal));
        Assert.Equal([BlockKind.Paragraph, BlockKind.UnclosedFence], document.Blocks.Select(b => b.Kind).ToList());
    }

    /// <summary>
    ///     Test that an unclosed fence inside a block quote ends with the quote, not at the end of the file.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedFenceInQuote_EndsWithQuote()
    {
        // Arrange: a quote with an unclosed fence followed by a paragraph outside the quote
        const string text = "> ```\n> code\n\nafter\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the fence stays inside the quote and the paragraph after it is a separate block
        var quote = Single(document, BlockKind.BlockQuote);
        var fence = Single(document, BlockKind.UnclosedFence);
        Assert.True(fence.EndInclusive <= quote.EndInclusive);
        Assert.Equal("> ```\n> code", text.Substring(quote.Start, quote.EndInclusive - quote.Start + 1));
        Assert.Equal("after", Summarize(document).Single(s => s.Kind == BlockKind.Paragraph).Text);
    }

    /// <summary>
    ///     Test that a list followed by a blank line ends on its last visible character, although the parser's own span ends with a line feed.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ListFollowedByBlankLine_EndsOnLastVisibleCharacter()
    {
        // Arrange: a list followed by a blank line and another list
        const string text = "1. x\n   y\n\n- t\n";

        // Act: parse
        var document = Parse(text);

        // Assert: effective ends are at the last visible character even though the raw span ends with a newline
        var first = document.Blocks.First(b => b.Kind == BlockKind.List);
        Assert.Equal('\n', text[first.Node!.Span.End]);
        Assert.Equal("1. x\n   y", text.Substring(first.Start, first.EndInclusive - first.Start + 1));
        Assert.All(document.Blocks, b => Assert.NotEqual('\n', text[b.EndInclusive]));
    }

    /// <summary>
    ///     Test that indented code followed by a blank line ends on its last visible character.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_IndentedCodeFollowedByBlankLine_EndsOnLastVisibleCharacter()
    {
        // Arrange: indented code followed by a blank line and a paragraph
        const string text = "    code\n\nafter\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the code block ends at its last visible character
        var code = Single(document, BlockKind.IndentedCode);
        Assert.Equal('e', text[code.EndInclusive]);
    }

    /// <summary>
    ///     Test that definition and footnote group shells are dropped and each definition keeps its own exact range, in source order.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionsAndFootnotes_AppearInSourceOrderWithOwnRanges()
    {
        // Arrange: definitions interleaved with a referenced footnote
        const string text = "Text [a] and [b] and[^n].\n\n[a]: http://x\n\n[^n]: Note\n\n[b]: http://y\n";

        // Act: parse
        var document = Parse(text);

        // Assert: no group shells, the complete ordered list with exact ranges (the footnote includes its marker)
        Assert.DoesNotContain(document.Blocks, b => b.Node is LinkReferenceDefinitionGroup or FootnoteGroup);
        Assert.Equal(
            [
                (BlockKind.Paragraph, 0, "Text [a] and [b] and[^n]."),
                (BlockKind.LinkReferenceDefinition, 0, "[a]: http://x"),
                (BlockKind.Footnote, 0, "[^n]: Note"),
                (BlockKind.Paragraph, 1, "Note"),
                (BlockKind.LinkReferenceDefinition, 0, "[b]: http://y")
            ],
            SummarizeWithDepth(document));
    }

    /// <summary>
    ///     Test that a footnote definition that nothing references is reported with its range and without a parser node.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnreferencedFootnote_IsReportedWithItsRange()
    {
        // Arrange: a definition without a reference and a referenced one
        const string text = "Text[^used].\n\n[^used]: Used.\n\n[^note]: Pandoc footnote.\n";

        // Act: parse
        var document = Parse(text);

        // Assert: only the unreferenced definition is reported, without a parser node
        var unreferenced = Single(document, BlockKind.UnreferencedFootnote);
        Assert.Null(unreferenced.Node);
        Assert.Equal("[^note]: Pandoc footnote.", text.Substring(unreferenced.Start, unreferenced.EndInclusive - unreferenced.Start + 1));
        Assert.Single(document.Blocks, b => b.Kind == BlockKind.Footnote);
    }

    /// <summary>
    ///     Test that the block list of a parsed document cannot be changed through a cast.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_Blocks_CastToMutableTypes_CannotBeModified()
    {
        // Arrange / Act: parse a document with several blocks and take the block list
        var document = Parse("# Title\n\nText.\n\n- item\n");
        IReadOnlyList<BlockInfo> blocks = document.Blocks;

        // Assert: a caller cannot change the list
        ReadOnlyAssertions.AssertCannotBeModified(blocks);
    }

    /// <summary>
    ///     Test that an unreferenced footnote definition is found on any line, not only the first, because
    ///     the line-start anchor of the definition expression applies to the line searched.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="expected">The expected source text of the reported definition.</param>
    [Theory]
    [InlineData("[^a]: first line\n", "[^a]: first line")]
    [InlineData("Text.\n\n[^a]: second paragraph\n", "[^a]: second paragraph")]
    [InlineData("Text.\n[^a]: right after text\n", "[^a]: right after text")]
    [InlineData("# Title\n\nText.\n\n   [^a]: indented by three\n", "[^a]: indented by three")]
    [InlineData("a\n\nb\n\nc\n\nd\n\ne\n\nf\n\n[^late]: far down the document\n", "[^late]: far down the document")]
    public void DocumentParser_Parse_UnreferencedFootnoteOnLaterLine_IsReported(string text, string expected)
    {
        // Arrange / Act: parse a document whose only definition is unreferenced
        var document = Parse(text);

        // Assert: the definition is reported with its exact range, wherever its line is
        var unreferenced = Single(document, BlockKind.UnreferencedFootnote);
        Assert.Equal(expected, text.Substring(unreferenced.Start, unreferenced.EndInclusive - unreferenced.Start + 1));
    }

    /// <summary>
    ///     Test that a footnote-looking line inside a code block is not a definition.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FootnoteLikeTextInCode_IsNotReported()
    {
        // Arrange: a fenced code block that contains a footnote-looking line
        var document = Parse("```\n[^x]: not a footnote\n```\n");

        // Act: classify
        var kinds = document.Blocks.Select(b => b.Kind).ToList();

        // Assert: only the code block exists
        Assert.Equal([BlockKind.FencedCode], kinds);
    }

    /// <summary>
    ///     Test that a referenced footnote's range starts at its <c>[^label]:</c> marker whatever the layout of the definition.
    /// </summary>
    /// <param name="definition">The footnote definition text.</param>
    /// <param name="expectedText">The text expected from the marker to the end of the definition.</param>
    [Theory]
    [InlineData("[^n]: Note", "[^n]: Note")]
    [InlineData("[^n]:\n    Note", "[^n]:\n    Note")]
    [InlineData("   [^n]: Note", "[^n]: Note")]
    [InlineData("[^n]: first\n    second\n\n    para two", "[^n]: first\n    second\n\n    para two")]
    public void DocumentParser_Parse_ReferencedFootnote_RangeIncludesLabelMarker(string definition, string expectedText)
    {
        // Arrange: a reference followed by the definition
        var document = Parse("T[^n].\n\n" + definition + "\n");

        // Act: find the footnote
        var footnote = Single(document, BlockKind.Footnote);

        // Assert: the range starts at the marker (not after it), ends on the last visible character, and parents its content
        Assert.Equal(expectedText, TextOf(document, footnote));
        Assert.Equal(0, footnote.Depth);
        Assert.All(document.Blocks.Where(b => b.Kind == BlockKind.Paragraph && b.Depth == 1), b => Assert.Same(footnote.Node, b.Parent));
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.UnreferencedFootnote);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a footnote definition repeating an earlier label (the parser drops it) is still reported as a block.
    /// </summary>
    /// <param name="first">The first label.</param>
    /// <param name="second">The repeated label (differing only in letter case in one variant).</param>
    [Theory]
    [InlineData("n", "n")]
    [InlineData("N", "n")]
    public void DocumentParser_Parse_DuplicateFootnoteDefinition_SecondIsReported(string first, string second)
    {
        // Arrange: a referenced footnote followed by a definition that repeats its label
        var text = $"T[^{first}].\n\n[^{first}]: one\n\n[^{second}]: two\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the first is a footnote, the repeated one is reported as unreferenced with its range, nothing is uncovered
        var footnote = Single(document, BlockKind.Footnote);
        var duplicate = Single(document, BlockKind.UnreferencedFootnote);
        Assert.Equal($"[^{first}]: one", TextOf(document, footnote));
        Assert.Equal($"[^{second}]: two", TextOf(document, duplicate));
        Assert.Null(duplicate.Node);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that two footnote definitions without a blank line between them and no reference are both reported.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_AdjacentUnreferencedFootnotes_AreBothReported()
    {
        // Arrange: two repeated-label definitions on consecutive lines, nothing referencing them
        var document = Parse("[^n]: one\n[^n]: two\n");

        // Act: summarize
        var summary = Summarize(document);

        // Assert: both definitions are blocks of their own
        Assert.Equal([(BlockKind.UnreferencedFootnote, "[^n]: one"), (BlockKind.UnreferencedFootnote, "[^n]: two")], summary);
    }

    /// <summary>
    ///     Test that an indented footnote-looking line after a footnote is content of that footnote, not a new definition.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_IndentedFootnoteLikeLineInFootnote_IsNotReportedAsDefinition()
    {
        // Arrange: a referenced footnote whose second paragraph looks like a repeated definition
        var document = Parse("T[^n].\n\n[^n]: one\n\n    [^n]: two\n");

        // Act: find the footnote
        var footnote = Single(document, BlockKind.Footnote);

        // Assert: the content line is inside the footnote's range and no duplicate block exists beside it
        Assert.Equal("[^n]: one\n\n    [^n]: two", TextOf(document, footnote));
        Assert.Single(document.Blocks, b => b.Kind == BlockKind.Footnote);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a referenced footnote that holds an indented HTML block ends with that block, so the block lies inside its parent.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expectedFootnote">The text expected from the footnote marker to its last visible character.</param>
    [Theory]
    [InlineData("T[^n].\n\n[^n]: body\n\n    <div>\n    x\n    </div>\n", "[^n]: body\n\n    <div>\n    x\n    </div>")]
    [InlineData("T[^n].\n\n[^n]: body\n\n    <div>\n    x\n    </div>\n\nAfter.\n", "[^n]: body\n\n    <div>\n    x\n    </div>")]
    [InlineData("T[^n].\n\n[^n]: body\n\n    - item\n\n      <div>\n      x\n      </div>\n", "[^n]: body\n\n    - item\n\n      <div>\n      x\n      </div>")]
    public void DocumentParser_Parse_FootnoteFollowedByIndentedHtmlBlock_FootnoteContainsHtmlBlock(string text, string expectedFootnote)
    {
        // Arrange / Act: parse a footnote whose body ends with an indented HTML block
        var document = Parse(text);
        var footnote = Single(document, BlockKind.Footnote);
        var html = Single(document, BlockKind.HtmlBlock);

        // Assert: the footnote covers the whole block, which is its child, and nothing overlaps
        Assert.Equal(expectedFootnote, TextOf(document, footnote));
        Assert.Equal("<div>", TextOf(document, html)[..5]);
        Assert.Same(footnote.Node, html.Parent);
        Assert.Equal(footnote.Depth + 1, html.Depth);
        Assert.True(footnote.Start <= html.Start && html.EndInclusive <= footnote.EndInclusive);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a dropped footnote definition ends before a definition behind a container prefix, so the two blocks do not overlap.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expectedFirst">The text expected for the first unreferenced footnote.</param>
    /// <param name="expectedSecond">The text expected for the second unreferenced footnote.</param>
    [Theory]
    [InlineData("[^n]: one\n- [^m]: two\n", "[^n]: one", "[^m]: two")]
    [InlineData("[^n]: one\n  > [^m]: two\n", "[^n]: one", "[^m]: two")]
    [InlineData("[^n]: one\n> [^m]: two\n", "[^n]: one", "[^m]: two")]
    [InlineData("[^n]: one\n1. - [^m]: two\n", "[^n]: one", "[^m]: two")]
    [InlineData("   [^n]: one\n- -     code\n    [^m]: two\n", "[^n]: one\n- -     code", "[^m]: two")]
    public void DocumentParser_Parse_UnreferencedFootnoteBeforeDefinitionInContainer_BlocksDoNotOverlap(string text, string expectedFirst, string expectedSecond)
    {
        // Arrange / Act: parse a footnote nothing refers to followed by a definition that starts behind a container prefix
        var document = Parse(text);

        // Assert: both definitions are reported with their own range and nothing overlaps
        var footnotes = document.Blocks.Where(b => b.Kind == BlockKind.UnreferencedFootnote).Select(b => TextOf(document, b)).ToList();
        Assert.Equal([expectedFirst, expectedSecond], footnotes);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a dropped footnote ends before a definition behind its marker on the same line, and that nothing overlaps.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expected">The expected blocks, each as the kind, a colon and the text of its span.</param>
    [Theory]
    [InlineData("[^1]: [a]: http://x\n", new[] { "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: http://x" })]
    [InlineData("[^1]:\t[a]: /u\n", new[] { "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: /u" })]
    [InlineData("[^1]:[a]: /u\n", new[] { "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: /u" })]
    [InlineData("  [^1]: [a]: /u \"t\"\n    more\n", new[] { "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: /u \"t\"" })]
    [InlineData("[^m]: [l]:\n    /u\n  > [!NOTE]\n", new[] { "UnreferencedFootnote:[^m]:", "LinkReferenceDefinition:[l]:\n    /u" })]
    [InlineData("[^1]: [a]: /u\n\n[^2]: [b]: /v\n", new[] { "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: /u", "UnreferencedFootnote:[^2]:", "LinkReferenceDefinition:[b]: /v" })]
    [InlineData("[^1]: x\n[^2]: [a]: /u\n", new[] { "UnreferencedFootnote:[^1]: x", "UnreferencedFootnote:[^2]:", "LinkReferenceDefinition:[a]: /u" })]
    [InlineData("T[^1].\n\n[^1]: x\n\n[^1]: [a]: /u\n", new[] { "Paragraph:T[^1].", "Footnote:[^1]: x", "Paragraph:x", "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: /u" })]
    [InlineData("[^1]: [^2]: x\n[^3]: [a]: /u\n", new[] { "UnreferencedFootnote:[^1]:", "UnreferencedFootnote:[^2]: x", "UnreferencedFootnote:[^3]:", "LinkReferenceDefinition:[a]: /u" })]
    [InlineData("[^m]: [^n]:\n", new[] { "UnreferencedFootnote:[^m]: [^n]:" })]
    public void DocumentParser_Parse_DroppedFootnoteBeforeDefinitionOnSameLine_FootnoteEndsBeforeDefinition(string text, string[] expected)
    {
        // Arrange / Act: parse a footnote that nothing refers to (or that repeats a label) with a definition behind its marker
        var document = Parse(text);

        // Assert: the footnote is only its marker (or the whole line when nothing follows it), the definition keeps its own range after it, and nothing overlaps
        Assert.Equal(expected, document.Blocks.Select(b => $"{b.Kind}:{TextOf(document, b)}"));
        Assert.All(
            document.Blocks.Where(b => b.Kind == BlockKind.UnreferencedFootnote && b.Node == null),
            footnote => Assert.StartsWith("[^", TextOf(document, footnote), StringComparison.Ordinal));
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that CRLF bytes give the same blocks as LF text for a dropped footnote with a definition on its marker line.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DroppedFootnoteBeforeDefinitionOnSameLine_CrLfGivesSameBlocks()
    {
        // Arrange / Act: parse the document from LF text and from CRLF bytes
        const string text = "[^1]: [a]: http://x\n\n[^2]: [l]:\n    /u\n";
        var lf = Parse(text);
        var crlf = DocumentParser.Parse(SourceDocument.Create("t.md", Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal))));

        // Assert: the block lists are equal and nest
        Assert.Equal(SummarizeWithDepth(lf), SummarizeWithDepth(crlf));
        Assert.Equal(4, crlf.Blocks.Length);
        Assert.Empty(SpanTilingAssertions.CheckNesting(crlf));
    }

    /// <summary>
    ///     Test that a dropped footnote whose body starts with another footnote marker gives two sibling blocks.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DroppedFootnoteBeforeFootnoteMarkerOnSameLine_BothAreReportedWithoutOverlap()
    {
        // Arrange / Act: parse a dropped footnote whose body starts with another footnote-shaped definition
        var document = Parse("[^m]: [^n]: x\n");

        // Assert: the outer footnote is its marker, the inner one (a definition the parser kept) follows it, and both are top-level siblings
        Assert.Equal(2, document.Blocks.Length);
        Assert.All(document.Blocks, b => Assert.Equal(BlockKind.UnreferencedFootnote, b.Kind));
        Assert.All(document.Blocks, b => Assert.Null(b.Parent));
        Assert.Equal((0, 4), (document.Blocks[0].Start, document.Blocks[0].EndInclusive));
        Assert.Equal((6, 12), (document.Blocks[1].Start, document.Blocks[1].EndInclusive));
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a definition behind the marker of a footnote that a paragraph refers to is a child of that footnote.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expectedChildKind">The expected kind of the child, by name.</param>
    /// <param name="expectedChild">The expected text of the child.</param>
    // BlockKind is internal and the test class is public: the kind is passed by name
    [Theory]
    [InlineData("T[^1].\n\n[^1]: [a]: /u\n", "LinkReferenceDefinition", "[a]: /u")]
    [InlineData("T[^m].\n\n[^m]: [^n]: x\n", "UnreferencedFootnote", "[^n]: x")]
    public void DocumentParser_Parse_ReferencedFootnoteBeforeDefinitionOnSameLine_DefinitionIsChild(string text, string expectedChildKind, string expectedChild)
    {
        // Arrange / Act: parse a footnote that a paragraph refers to, with a definition behind its marker
        var document = Parse(text);

        // Assert: the definition is a child of the footnote, one level deeper, and nothing overlaps
        var footnote = Single(document, BlockKind.Footnote);
        var child = Assert.Single(document.Blocks, b => b.Parent == footnote.Node);
        Assert.Equal(expectedChildKind, child.Kind.ToString());
        Assert.Equal(expectedChild, TextOf(document, child));
        Assert.Equal(footnote.Depth + 1, child.Depth);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a valid grid table next to a dropped footnote with a definition on its marker line stays a grid table.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ValidGridTableAndDroppedFootnoteBeforeDefinition_KeepsGridTable()
    {
        // Arrange / Act: parse a valid grid table followed by a dropped footnote with a definition behind its marker
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n\n[^1]: [a]: http://x\n");

        // Assert: the grid table is still a grid table (the document was not read again without grid tables) and the blocks nest
        Assert.Equal(
            new[] { "GridTable:+---+---+\n| a | b |\n+---+---+", "UnreferencedFootnote:[^1]:", "LinkReferenceDefinition:[a]: http://x" },
            document.Blocks.Select(b => $"{b.Kind}:{TextOf(document, b)}"));
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a document that is read without grid tables still nests with a dropped footnote that has a definition on its marker line.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_InvalidGridTableAndDroppedFootnoteBeforeDefinition_BlocksNest()
    {
        // Arrange / Act: parse a valid and an invalid grid table (the fallback) and a dropped footnote with a definition behind its marker
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n\n[^1]: [a]: http://x\n");

        // Assert: the document was read without grid tables, the footnote ends before its definition, and the blocks nest
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.GridTable);
        var kinds = document.Blocks.Where(b => b.Kind is BlockKind.UnreferencedFootnote or BlockKind.LinkReferenceDefinition).Select(b => TextOf(document, b));
        Assert.Equal(["[^1]:", "[a]: http://x"], kinds);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a definition ends at its destination when the title on the next line is rejected, and keeps a valid title.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expected">The expected blocks, each as the kind, a colon and the text of its span.</param>
    [Theory]
    [InlineData("[a]: /u\n\"t\" x\n", new[] { "LinkReferenceDefinition:[a]: /u", "Paragraph:\"t\" x" })]
    [InlineData("[a]: /u\n(t) x\ny\n", new[] { "LinkReferenceDefinition:[a]: /u", "Paragraph:(t) x\ny" })]
    [InlineData("> [a]: /u\n> \"t\" x\n", new[] { "BlockQuote:> [a]: /u\n> \"t\" x", "LinkReferenceDefinition:[a]: /u", "Paragraph:\"t\" x" })]
    [InlineData("- [a]: /u\n  (t) x\n", new[] { "List:- [a]: /u\n  (t) x", "ListItem:- [a]: /u\n  (t) x", "LinkReferenceDefinition:[a]: /u", "Paragraph:(t) x" })]
    [InlineData("- [^n]: [^n]:\n  (t) x\n", new[] { "List:- [^n]: [^n]:\n  (t) x", "ListItem:- [^n]: [^n]:\n  (t) x", "UnreferencedFootnote:[^n]: [^n]:", "Paragraph:(t) x" })]
    [InlineData("[a]: /u\n\"t\"\n", new[] { "LinkReferenceDefinition:[a]: /u\n\"t\"" })]
    [InlineData("[a]: /u\n\"\"\n", new[] { "LinkReferenceDefinition:[a]: /u\n\"\"" })]
    public void DocumentParser_Parse_DefinitionWithRejectedNextLineTitle_EndsAtDestination(string text, string[] expected)
    {
        // Arrange / Act: parse a definition followed by a line that starts like a title but has text after it (and valid titles for contrast)
        var document = Parse(text);

        // Assert: the definition ends at its destination unless its title is valid, the text is a paragraph, and nothing overlaps
        Assert.Equal(expected, document.Blocks.Select(b => $"{b.Kind}:{TextOf(document, b)}"));
        var definitions = document.Blocks.Where(b => b.Kind is BlockKind.LinkReferenceDefinition or BlockKind.UnreferencedFootnote).ToList();
        Assert.All(
            document.Blocks.Where(b => b.Kind == BlockKind.Paragraph),
            paragraph => Assert.DoesNotContain(definitions, d => d.Start <= paragraph.EndInclusive && paragraph.Start <= d.EndInclusive));
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a line that looks like a grid table row but is not a valid grid table leaves blocks that nest.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="expected">The expected blocks, each as the kind, a colon and the text of its span.</param>
    [Theory]
    [InlineData("para\n+---+---+\n", new[] { "Paragraph:para\n+---+---+" })]
    [InlineData("+---+---+\n# head\n", new[] { "Paragraph:+---+---+", "Heading:# head" })]
    [InlineData("> quote\n+---+---+\n> more\n", new[] { "BlockQuote:> quote\n+---+---+\n> more", "Paragraph:quote\n+---+---+\n> more" })]
    [InlineData("- item\n+---+---+\n- next\n", new[] { "List:- item\n+---+---+\n- next", "ListItem:- item\n+---+---+", "Paragraph:item\n+---+---+", "ListItem:- next", "Paragraph:next" })]
    public void DocumentParser_Parse_InvalidGridTable_BlocksDoNotOverlap(string text, string[] expected)
    {
        // Arrange / Act: parse a document whose grid-table-looking line is not a valid grid table
        var document = Parse(text);

        // Assert: the line is read as ordinary text and the blocks nest
        Assert.Equal(expected, document.Blocks.Select(b => $"{b.Kind}:{TextOf(document, b)}"));
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a document with only a valid grid table still has a grid table block.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ValidGridTableOnly_IsGridTable()
    {
        // Arrange / Act: parse a valid grid table
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n");

        // Assert: it is exactly one grid table
        var block = Assert.Single(document.Blocks);
        Assert.Equal(BlockKind.GridTable, block.Kind);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a document with a valid and an invalid grid table is read without grid tables.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ValidAndInvalidGridTable_ReadsWithoutGridTables()
    {
        // Arrange / Act: parse a valid grid table followed by a grid-table-looking line that is not valid
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n");

        // Assert: the valid table is read as ordinary text too, and the blocks nest
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.GridTable);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a link reference definition inside a block quote reports the quote as its parent and the next depth.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionInBlockQuote_ReportsDepthAndParent()
    {
        // Arrange: a definition inside a block quote and one outside any container
        var document = Parse("> [a]: http://x\n\n[b]: http://y\n");

        // Act: find the blocks
        var quote = Single(document, BlockKind.BlockQuote);
        var inside = document.Blocks.Single(b => b.Kind == BlockKind.LinkReferenceDefinition && TextOf(document, b) == "[a]: http://x");
        var outside = document.Blocks.Single(b => b.Kind == BlockKind.LinkReferenceDefinition && TextOf(document, b) == "[b]: http://y");

        // Assert: the nested definition hangs under the quote, the top-level one has no parent
        Assert.Equal(1, inside.Depth);
        Assert.Same(quote.Node, inside.Parent);
        Assert.Equal(0, outside.Depth);
        Assert.Null(outside.Parent);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a link reference definition inside a list item reports the item as its parent and the next depth.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionInListItem_ReportsDepthAndParent()
    {
        // Arrange: a list item that holds a definition
        var document = Parse("- [a]: http://x\n- b\n");

        // Act: find the blocks
        var item = document.Blocks.First(b => b.Kind == BlockKind.ListItem);
        var definition = Single(document, BlockKind.LinkReferenceDefinition);

        // Assert: list > item > definition
        Assert.Equal(2, definition.Depth);
        Assert.Same(item.Node, definition.Parent);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that definitions in nested containers (quote > list > item) report the innermost container as parent.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionInNestedContainers_ReportsDepthAndParent()
    {
        // Arrange: two definitions in one list item inside a quote
        var document = Parse("> - [a]: http://x\n>   [b]: http://y\n");

        // Act: find the item and the definitions
        var item = Single(document, BlockKind.ListItem);
        var definitions = document.Blocks.Where(b => b.Kind == BlockKind.LinkReferenceDefinition).ToList();

        // Assert: both are at depth 3 with the item as parent, in source order
        Assert.Equal(["[a]: http://x", "[b]: http://y"], definitions.Select(d => TextOf(document, d)).ToList());
        Assert.All(definitions, d => Assert.Equal(3, d.Depth));
        Assert.All(definitions, d => Assert.Same(item.Node, d.Parent));
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a paragraph directly after a link reference definition does not overlap it.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionFollowedByText_ParagraphDoesNotOverlapDefinition()
    {
        // Arrange: a definition immediately followed by paragraph text (no blank line)
        var document = Parse("[a]: http://x\ntext\n");

        // Act: summarize
        var summary = Summarize(document);

        // Assert: the definition and the paragraph are disjoint, the paragraph starts at its own text
        Assert.Equal([(BlockKind.LinkReferenceDefinition, "[a]: http://x"), (BlockKind.Paragraph, "text")], summary);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a paragraph directly after a definition inside a block quote does not overlap it and excludes the quote marker.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionInBlockQuoteFollowedByText_ParagraphDoesNotOverlapDefinition()
    {
        // Arrange: a quoted definition immediately followed by quoted text
        var document = Parse("> [a]: http://x\n> text\n");

        // Act: summarize
        var summary = SummarizeWithDepth(document);

        // Assert: the paragraph holds only its own text and nests next to the definition
        Assert.Equal(
            [
                (BlockKind.BlockQuote, 0, "> [a]: http://x\n> text"),
                (BlockKind.LinkReferenceDefinition, 1, "[a]: http://x"),
                (BlockKind.Paragraph, 1, "text")
            ],
            summary);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a paragraph directly after two stacked definitions inside a list item does not overlap them.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionsInListItemFollowedByText_ParagraphDoesNotOverlapDefinitions()
    {
        // Arrange: two stacked definitions in a list item, immediately followed by text of the item
        var document = Parse("- [a]: http://x\n  [b]: http://y\n  text\n");

        // Act: summarize
        var summary = SummarizeWithDepth(document);

        // Assert: the paragraph holds only its own text and nests next to the definitions
        Assert.Equal(
            [
                (BlockKind.List, 0, "- [a]: http://x\n  [b]: http://y\n  text"),
                (BlockKind.ListItem, 1, "- [a]: http://x\n  [b]: http://y\n  text"),
                (BlockKind.LinkReferenceDefinition, 2, "[a]: http://x"),
                (BlockKind.LinkReferenceDefinition, 2, "[b]: http://y"),
                (BlockKind.Paragraph, 2, "text")
            ],
            summary);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a setext heading directly after a link reference definition does not overlap it.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionFollowedBySetextHeading_HeadingDoesNotOverlapDefinition()
    {
        // Arrange: a definition immediately followed by a setext heading (no blank line)
        var document = Parse("[a]: /x\nSetext\n===\n");

        // Act: summarize and rebuild the heading lines
        var summary = Summarize(document);
        var heading = Single(document, BlockKind.Heading);

        // Assert: the definition and the heading are disjoint, the heading holds exactly its own two lines
        Assert.Equal([(BlockKind.LinkReferenceDefinition, "[a]: /x"), (BlockKind.Heading, "Setext\n===")], summary);
        Assert.Equal(["Setext", "==="], LineContents(document, heading));
        Assert.Equal([2, 3], DocumentParser.GetLines(document, heading).Select(l => l.Line).ToList());
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a setext heading directly after a definition inside a container does not overlap it and excludes the container prefix.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionInContainerFollowedBySetextHeading_HeadingDoesNotOverlapDefinition()
    {
        // Arrange: a quoted definition and a listed definition, each followed by a setext heading
        var quoted = Parse("> [a]: /x\n> Setext\n> ===\n");
        var listed = Parse("- [a]: /x\n  Setext\n  ===\n");

        // Act: summarize
        var quotedSummary = SummarizeWithDepth(quoted);
        var listedSummary = SummarizeWithDepth(listed);

        // Assert: the heading starts at its own text and nests next to the definition
        Assert.Equal(
            [
                (BlockKind.BlockQuote, 0, "> [a]: /x\n> Setext\n> ==="),
                (BlockKind.LinkReferenceDefinition, 1, "[a]: /x"),
                (BlockKind.Heading, 1, "Setext\n> ===")
            ],
            quotedSummary);
        Assert.Equal(
            [
                (BlockKind.List, 0, "- [a]: /x\n  Setext\n  ==="),
                (BlockKind.ListItem, 1, "- [a]: /x\n  Setext\n  ==="),
                (BlockKind.LinkReferenceDefinition, 2, "[a]: /x"),
                (BlockKind.Heading, 2, "Setext\n  ===")
            ],
            listedSummary);
        Assert.Equal(["Setext", "==="], LineContents(quoted, Single(quoted, BlockKind.Heading)));
        Assert.Equal(["Setext", "==="], LineContents(listed, Single(listed, BlockKind.Heading)));
        Assert.Empty(SpanTilingAssertions.Check(quoted));
        Assert.Empty(SpanTilingAssertions.Check(listed));
    }

    /// <summary>
    ///     Test that a greater-than sign that is paragraph text after a definition is not taken for a block quote marker.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_DefinitionFollowedByLiteralGreaterThan_ParagraphKeepsTheCharacter()
    {
        // Arrange: a definition followed by an indented line whose text begins with a greater-than sign
        var document = Parse("[a]: /x\n    > text\n");

        // Act: summarize
        var summary = Summarize(document);

        // Assert: the paragraph starts at the greater-than sign
        Assert.Equal([(BlockKind.LinkReferenceDefinition, "[a]: /x"), (BlockKind.Paragraph, "> text")], summary);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a footnote definition inside a block quote is a footnote, not a link reference definition.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FootnoteDefinitionInBlockQuote_IsFootnoteNotLinkDefinition()
    {
        // Arrange: a footnote-shaped definition inside a quote, next to a real definition
        var document = Parse("> [^q]: note\n\n[a]: http://x\n");

        // Act: find the blocks
        var quote = Single(document, BlockKind.BlockQuote);
        var footnote = Single(document, BlockKind.UnreferencedFootnote);

        // Assert: reported as a footnote under the quote, not indexed as a link definition
        Assert.Equal("[^q]: note", TextOf(document, footnote));
        Assert.Equal(1, footnote.Depth);
        Assert.Same(quote.Node, footnote.Parent);
        Assert.Equal(["a"], document.References.Definitions.Select(d => d.Label).ToList());
    }

    /// <summary>
    ///     Test that a footnote definition inside a list item is a footnote, not a link reference definition.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FootnoteDefinitionInListItem_IsFootnoteNotLinkDefinition()
    {
        // Arrange: a footnote-shaped definition in a list item
        var document = Parse("- [^n]: Note\n- b\n");

        // Act: summarize
        var summary = Summarize(document);

        // Assert: the definition is an unreferenced footnote, and the index stays empty
        Assert.Contains((BlockKind.UnreferencedFootnote, "[^n]: Note"), summary);
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.LinkReferenceDefinition);
        Assert.Empty(document.References.Definitions);
    }

    /// <summary>
    ///     Test that a footnote definition in a list item reports the item as its parent and the next depth.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FootnoteDefinitionInListItem_ReportsDepthAndParent()
    {
        // Arrange: a list item with a paragraph, then a footnote-shaped definition indented under it
        var document = Parse("- x\n\n  [^n]: Note\n");

        // Act: find the blocks
        var item = Single(document, BlockKind.ListItem);
        var footnote = Single(document, BlockKind.UnreferencedFootnote);

        // Assert: list > item > footnote definition
        Assert.Equal(2, footnote.Depth);
        Assert.Same(item.Node, footnote.Parent);
        Assert.Equal("[^n]: Note", TextOf(document, footnote));
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that an unclosed fence inside a list item ends with the list item.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedFenceInListItem_EndsWithListItem()
    {
        // Arrange: an item whose fence is never closed, followed by another item
        const string text = "- item\n\n  ```\n  code\n- next\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the fence ends with its item, the next item is separate
        var fence = Single(document, BlockKind.UnclosedFence);
        var firstItem = document.Blocks.First(b => b.Kind == BlockKind.ListItem);
        Assert.Equal("```\n  code", TextOf(document, fence));
        Assert.Equal(firstItem.EndInclusive, fence.EndInclusive);
        Assert.Same(firstItem.Node, fence.Parent);
        Assert.Equal(2, document.Blocks.Count(b => b.Kind == BlockKind.ListItem));
    }

    /// <summary>
    ///     Test that a math block without closing delimiter extends to the end of the document.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedMathBlock_ExtendsToEndOfDocument()
    {
        // Arrange: a math fence that is never closed, followed by text that belongs to it
        const string text = "$$\nx\n\nafter\n";

        // Act: parse
        var document = Parse(text);

        // Assert: one block of the unclosed kind (the parser derives math blocks from fenced code) covering everything
        var block = Assert.Single(document.Blocks);
        Assert.Equal(BlockKind.UnclosedFence, block.Kind);
        Assert.IsType<MathBlock>(block.Node);
        Assert.Equal("$$\nx\n\nafter", TextOf(document, block));
    }

    /// <summary>
    ///     Test that a math block without closing delimiter inside a list item ends with the list item.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedMathBlockInListItem_EndsWithListItem()
    {
        // Arrange: an item whose math fence is never closed, followed by another item
        const string text = "- item\n\n  $$\n  x\n- next\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the math block ends with its item
        var math = Single(document, BlockKind.UnclosedFence);
        var firstItem = document.Blocks.First(b => b.Kind == BlockKind.ListItem);
        Assert.IsType<MathBlock>(math.Node);
        Assert.Equal("$$\n  x", TextOf(document, math));
        Assert.Equal(firstItem.EndInclusive, math.EndInclusive);
        Assert.Same(firstItem.Node, math.Parent);
    }

    /// <summary>
    ///     Test that a lazy continuation line is flagged lazy and explicit prefixes are not.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_LazyContinuation_FlagsOnlyLazyLines()
    {
        // Arrange: a lazy list continuation, an explicit one, and a lazy quote continuation
        var lazyList = Parse("- a\nb\n");
        var explicitList = Parse("- a\n  b\n");
        var lazyQuote = Parse("> a\nb\n");
        var explicitQuote = Parse("> a\n> b\n");

        // Act: get the lines of each paragraph
        var lazyListLines = DocumentParser.GetLines(lazyList, Single(lazyList, BlockKind.Paragraph));
        var explicitListLines = DocumentParser.GetLines(explicitList, Single(explicitList, BlockKind.Paragraph));
        var lazyQuoteLines = DocumentParser.GetLines(lazyQuote, Single(lazyQuote, BlockKind.Paragraph));
        var explicitQuoteLines = DocumentParser.GetLines(explicitQuote, Single(explicitQuote, BlockKind.Paragraph));

        // Assert: only the lazy continuations are flagged; first lines never are
        Assert.Equal([false, true], lazyListLines.Select(l => l.IsLazy).ToList());
        Assert.Equal([false, false], explicitListLines.Select(l => l.IsLazy).ToList());
        Assert.Equal([false, true], lazyQuoteLines.Select(l => l.IsLazy).ToList());
        Assert.Equal([false, false], explicitQuoteLines.Select(l => l.IsLazy).ToList());
    }

    /// <summary>
    ///     Test that a setext-underline lookalike that is only paragraph text because it is lazy is flagged lazy.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_LazySetextUnderlineLookalike_IsLazyParagraphText()
    {
        // Arrange: "===" lazily continues a quoted paragraph instead of making a heading
        var document = Parse("> Setext\n==============\n");

        // Act: get the paragraph lines
        var paragraph = Single(document, BlockKind.Paragraph);
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: no heading exists; the second line is lazy paragraph text
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.Heading);
        Assert.True(lines[1].IsLazy);
        Assert.Equal("==============", LineContents(document, paragraph)[1]);
    }

    /// <summary>
    ///     Test that paragraph lines are rebuilt from the source with exact content ranges.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_ParagraphLines_HaveExactContentRanges()
    {
        // Arrange: a three-line paragraph at top level
        var document = Parse("alpha\nbeta  \ngamma\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: three lines with exact content (trailing spaces kept) and 1-based line numbers
        Assert.Equal(["alpha", "beta  ", "gamma"], LineContents(document, paragraph));
        Assert.Equal([1, 2, 3], lines.Select(l => l.Line).ToList());
        Assert.Equal([0, 6, 13], lines.Select(l => l.LineStart).ToList());
    }

    /// <summary>
    ///     Test that the content of continuation lines in nested containers excludes the container prefix.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_NestedContainerContinuation_ContentExcludesPrefix()
    {
        // Arrange: a list item in a quote with explicit and lazy continuation lines
        var document = Parse("> - one\n>   two\n> three\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: content strings are exact; the line with fewer prefix characters is lazy
        Assert.Equal(["one", "two", "three"], LineContents(document, paragraph));
        Assert.Equal([false, false, true], lines.Select(l => l.IsLazy).ToList());
    }

    /// <summary>
    ///     Test that a continuation line starting inside a multi-line code span has its quote marker stripped.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_ContinuationInsideCodeSpan_ContentExcludesQuoteMarker()
    {
        // Arrange: a code span that continues on the next quoted line
        var document = Parse("> `code\n> span` text\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act: rebuild the lines
        var contents = LineContents(document, paragraph);

        // Assert: the quote marker is stripped from the second line
        Assert.Equal(["`code", "span` text"], contents);
    }

    /// <summary>
    ///     Test that a tab-indented continuation line has its content found exactly.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_TabIndentedContinuation_ContentStartAfterTab()
    {
        // Arrange: a list item whose continuation is indented with a tab
        var document = Parse("- a\n\tb\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: the content is "b", the tab counts as one column and the line is not lazy
        Assert.Equal(["a", "b"], LineContents(document, paragraph));
        Assert.Equal(2, document.Source.PositionOf(lines[1].ContentStart).Column);
        Assert.False(lines[1].IsLazy);
    }

    /// <summary>
    ///     Test that the continuation prefix replaces everything except quote markers and whitespace with spaces.
    /// </summary>
    [Theory]
    [InlineData("- a\n", "  ")]
    [InlineData("1. a\n", "   ")]
    [InlineData("10. a\n", "    ")]
    [InlineData("> - a\n", ">   ")]
    [InlineData("> a\n", "> ")]
    [InlineData("plain\n", "")]
    public void DocumentParser_GetContinuationPrefix_ListAndQuoteMarkers_AreBlankedOrKept(string text, string expected)
    {
        // Arrange: a document whose first paragraph carries the prefix
        var document = Parse(text);
        var paragraph = document.Blocks.First(b => b.Kind == BlockKind.Paragraph);

        // Act: derive the prefix
        var prefix = DocumentParser.GetContinuationPrefix(document, paragraph);

        // Assert: markers are blanked, quote markers kept
        Assert.Equal(expected, prefix);
    }

    /// <summary>
    ///     Test that headings produce lines too (a setext heading has two).
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_SetextHeading_ReturnsTextAndUnderlineLines()
    {
        // Arrange: a setext heading
        var document = Parse("Title\n=====\n");
        var heading = Single(document, BlockKind.Heading);

        // Act: rebuild the lines
        var contents = LineContents(document, heading);

        // Assert: both lines are present
        Assert.Equal(["Title", "====="], contents);
    }

    /// <summary>
    ///     Test that tab-indented list items, code and text inside a paragraph parse with exact ranges.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_Tabs_ListCodeAndTextSpansAreExact()
    {
        // Arrange: nested list indented with a tab, a paragraph with an inner tab, and tab-indented code
        const string text = "- a\n\t- b\n\ncol1\tcol2\n\n\tcode here\n";

        // Act: parse
        var document = Parse(text);

        // Assert: the complete block list with depths and exact texts (the tab-indented item nests inside the first item)
        Assert.Equal(
            [
                (BlockKind.List, 0, "- a\n\t- b"),
                (BlockKind.ListItem, 1, "- a\n\t- b"),
                (BlockKind.Paragraph, 2, "a"),
                (BlockKind.List, 2, "- b"),
                (BlockKind.ListItem, 3, "- b"),
                (BlockKind.Paragraph, 4, "b"),
                (BlockKind.Paragraph, 0, "col1\tcol2"),
                (BlockKind.IndentedCode, 0, "\tcode here")
            ],
            SummarizeWithDepth(document));
    }

    /// <summary>
    ///     Test that parsing the same documents from many threads yields identical results.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ConcurrentCalls_ProduceIdenticalBlocks()
    {
        // Arrange: several different documents (list, quote, footnotes, definitions in containers, unclosed fence)
        string[] texts =
        [
            "# H\n\n- a\n  b\n\n> q\n\n```\nunclosed\n",
            "T[^n] [a].\n\n[^n]: Note\n    more\n\n[^n]: repeated\n\n[^u]: unused\n\n[a]: http://x\ntext\n",
            "> [a]: http://x\n> text\n>\n> - [b]: http://y\n>   item\n",
            "- item\n\n  $$\n  math\n- next\n\n1. [^q]: note\n",
            "| a | b |\n|---|---|\n| 1 | 2 |\n\n+---+\n| x |\n+---+\n",

            // A valid and an invalid grid table (takes the grid table fallback), and a valid grid table next to a dropped footnote with a definition on its marker line
            "+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n\n[^1]: [a]: http://x\n",
            "+---+---+\n| a | b |\n+---+---+\n\n[^1]: [a]: http://x\n\n[^m]: [^n]: x\n"
        ];
        var expected = texts.Select(t => SummarizeWithDepth(Parse(t))).ToList();

        // The two documents differ in the fallback: the first loses its grid table, the second keeps it
        Assert.DoesNotContain(Parse(texts[5]).Blocks, b => b.Kind == BlockKind.GridTable);
        Assert.Contains(Parse(texts[6]).Blocks, b => b.Kind == BlockKind.GridTable);

        // Act: parse every document many times concurrently
        var results = new List<(BlockKind Kind, int Depth, string Text)>[texts.Length * 32];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = SummarizeWithDepth(Parse(texts[i % texts.Length])));

        // Assert: every result equals the single-threaded result of its document
        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(expected[i % texts.Length], results[i]);
        }
    }

    /// <summary>
    ///     Test that a very large document with many definitions, footnotes, quotes and list items parses in time proportional to its size.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_LargeDocument_CompletesWithinTimeBound()
    {
        // Arrange: 20000 groups of a definition with text, an unreferenced footnote, a quote and a list item
        // (160000 blocks, about 0.8 MB), plus a referenced footnote in every fiftieth group. A scan of all
        // blocks per definition or footnote takes about a minute on this input; linear or logarithmic lookups take about a second.
        const int groups = 20_000;
        var builder = new StringBuilder();
        for (var i = 0; i < groups; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"[r{i}]: /u\ntext\n\n[^u{i}]: x\n\n> q\n\n- i\n\n");
            if (i % 50 == 0)
            {
                builder.Append(CultureInfo.InvariantCulture, $"T[^f{i}].\n\n[^f{i}]: n\n\n");
            }
        }

        var source = SourceDocument.FromText("large.md", builder.ToString());

        // Act: parse and time it
        var stopwatch = Stopwatch.StartNew();
        var document = DocumentParser.Parse(source);
        stopwatch.Stop();

        // Assert: every construct was found, the blocks are in source order, and the time is far below that of a quadratic scan
        Assert.Equal(groups, document.Blocks.Count(b => b.Kind == BlockKind.LinkReferenceDefinition));
        Assert.Equal(groups, document.Blocks.Count(b => b.Kind == BlockKind.UnreferencedFootnote));
        Assert.Equal(groups / 50, document.Blocks.Count(b => b.Kind == BlockKind.Footnote));
        Assert.Equal(groups, document.Blocks.Count(b => b.Kind == BlockKind.BlockQuote));
        Assert.Equal(groups, document.Blocks.Count(b => b.Kind == BlockKind.ListItem));
        Assert.Equal(161_200, document.Blocks.Length);
        Assert.True(document.Blocks.Zip(document.Blocks.Skip(1)).All(pair => pair.First.Start <= pair.Second.Start), "blocks are not in source order");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"parsing took {stopwatch.Elapsed.TotalSeconds:F1} s");
    }

    /// <summary>
    ///     Test that Parse fills the suppression map and the reference index.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_CommentsAndDefinitions_PopulatesSuppressionsAndReferences()
    {
        // Arrange: a disable-file directive and a reference definition
        var document = Parse("<!-- lintmark-disable-file -->\n\nSee [a].\n\n[a]: http://x\n");

        // Act: query the results
        var found = document.References.TryGet("A", out var definition);

        // Assert: both side products exist
        Assert.True(found);
        Assert.Equal("http://x", definition!.Url);
        Assert.True(document.Suppressions.IsSuppressed(10, "any-rule"));
    }

    /// <summary>
    ///     Test that an indented continuation of a footnote's first paragraph is not lazy.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_FootnoteParagraph_IndentedContinuationIsNotLazy()
    {
        // Arrange: a footnote whose paragraph continues on a line indented by four spaces
        var document = Parse("T[^n].\n\n[^n]: first\n    second\n");
        var paragraph = document.Blocks.Single(b => b.Kind == BlockKind.Paragraph && b.Depth == 1);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: both lines have exact content and neither is lazy
        Assert.Equal(["first", "second"], LineContents(document, paragraph));
        Assert.Equal([false, false], lines.Select(l => l.IsLazy).ToList());
    }

    /// <summary>
    ///     Test that an unindented continuation of a footnote's first paragraph is lazy.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_FootnoteParagraph_UnindentedContinuationIsLazy()
    {
        // Arrange: a footnote whose paragraph continues on a line without indentation
        var document = Parse("T[^n].\n\n[^n]: first\nsecond\n");
        var paragraph = document.Blocks.Single(b => b.Kind == BlockKind.Paragraph && b.Depth == 1);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: the continuation is lazy
        Assert.Equal(["first", "second"], LineContents(document, paragraph));
        Assert.Equal([false, true], lines.Select(l => l.IsLazy).ToList());
    }

    /// <summary>
    ///     Test that the lines of a footnote's second paragraph exclude the four-space indent and are not lazy.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_FootnoteSecondParagraph_ContentExcludesIndent()
    {
        // Arrange: a footnote with a second, multi-line paragraph
        var document = Parse("T[^n].\n\n[^n]: one\n\n    two\n    three\n");
        var paragraph = document.Blocks.Single(b => b.Kind == BlockKind.Paragraph && TextOf(document, b).StartsWith("two", StringComparison.Ordinal));

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: content excludes the indent and nothing is lazy
        Assert.Equal(["two", "three"], LineContents(document, paragraph));
        Assert.Equal([false, false], lines.Select(l => l.IsLazy).ToList());
    }

    /// <summary>
    ///     Test that the continuation prefix of a footnote paragraph is the four-space body indent, whatever the marker width.
    /// </summary>
    /// <param name="label">The footnote label that the text references.</param>
    /// <param name="definition">The footnote definition text.</param>
    [Theory]
    [InlineData("n", "[^n]: first")]
    [InlineData("n", "   [^n]: first")]
    [InlineData("longer-label", "[^longer-label]: first")]
    [InlineData("n", "[^n]:   first")]
    public void DocumentParser_GetContinuationPrefix_FootnoteParagraph_IsFourSpaces(string label, string definition)
    {
        // Arrange: a referenced footnote
        var document = Parse($"T[^{label}].\n\n{definition}\n");
        var paragraph = document.Blocks.First(b => b.Kind == BlockKind.Paragraph && b.Depth == 1);

        // Act: derive the prefix
        var prefix = DocumentParser.GetContinuationPrefix(document, paragraph);

        // Assert: four spaces
        Assert.Equal("    ", prefix);
    }

    /// <summary>
    ///     Test that the lines of a paragraph that follows link reference definitions start at the paragraph's own text.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_ParagraphAfterDefinition_ReturnsOnlyParagraphLines()
    {
        // Arrange: a definition immediately followed by a two-line paragraph
        var document = Parse("[a]: http://x\ntext\nmore\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act: rebuild the lines
        var lines = DocumentParser.GetLines(document, paragraph);

        // Assert: two lines, numbered from the paragraph's first line
        Assert.Equal(["text", "more"], LineContents(document, paragraph));
        Assert.Equal([2, 3], lines.Select(l => l.Line).ToList());
    }

    /// <summary>
    ///     Test that a block that is not a paragraph or heading is rejected by GetLines.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_NonLeafBlock_ThrowsArgumentException()
    {
        // Arrange: a block quote and a code block
        var document = Parse("> a\n\n    code\n");
        var quote = Single(document, BlockKind.BlockQuote);
        var code = Single(document, BlockKind.IndentedCode);

        // Act / Assert: both are rejected with the argument exception itself, not a subclass
        Assert.Throws<ArgumentException>(() => DocumentParser.GetLines(document, quote));
        Assert.Throws<ArgumentException>(() => DocumentParser.GetLines(document, code));
    }

    /// <summary>
    ///     Test that a block whose range lies outside the document text is rejected by GetLines.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_BlockOutsideDocument_ThrowsArgumentException()
    {
        // Arrange: paragraph blocks whose ranges do not fit the text of a four-character document
        var document = Parse("abc\n");
        var beyondEnd = new BlockInfo(null, BlockKind.Paragraph, 0, 100, 0, null);
        var negativeStart = new BlockInfo(null, BlockKind.Paragraph, -1, 2, 0, null);
        var inverted = new BlockInfo(null, BlockKind.Paragraph, 3, 1, 0, null);

        // Act / Assert: each is rejected
        Assert.Throws<ArgumentException>(() => DocumentParser.GetLines(document, beyondEnd));
        Assert.Throws<ArgumentException>(() => DocumentParser.GetLines(document, negativeStart));
        Assert.Throws<ArgumentException>(() => DocumentParser.GetLines(document, inverted));
    }

    /// <summary>
    ///     Test that GetContinuationPrefix rejects a block that is not a paragraph or heading and a block outside the document.
    /// </summary>
    [Fact]
    public void DocumentParser_GetContinuationPrefix_NonLeafBlock_ThrowsArgumentException()
    {
        // Arrange: a quote, and a paragraph block whose range lies outside the text
        var document = Parse("> a\n");
        var quote = Single(document, BlockKind.BlockQuote);
        var outside = new BlockInfo(null, BlockKind.Paragraph, 0, 100, 0, null);

        // Act / Assert: both are rejected
        Assert.Throws<ArgumentException>(() => DocumentParser.GetContinuationPrefix(document, quote));
        Assert.Throws<ArgumentException>(() => DocumentParser.GetContinuationPrefix(document, outside));
    }

    /// <summary>
    ///     Test that null arguments to the line helpers are rejected.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_NullArguments_Throw()
    {
        // Arrange: a parsed document
        var document = Parse("a\n");
        var paragraph = Single(document, BlockKind.Paragraph);

        // Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => DocumentParser.GetLines(null!, paragraph));
        Assert.Throws<ArgumentNullException>(() => DocumentParser.GetLines(document, null!));
        Assert.Throws<ArgumentNullException>(() => DocumentParser.GetContinuationPrefix(null!, paragraph));
        Assert.Throws<ArgumentNullException>(() => DocumentParser.GetContinuationPrefix(document, null!));
    }

    /// <summary>
    ///     Test that a grid-table-looking line that is not part of a valid grid table marks the document format-unsafe at that line.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="line">The expected 1-based line of the offending border.</param>
    /// <param name="column">The expected 1-based column of its first border character.</param>
    [Theory]
    [InlineData("para\n+---+---+\n", 2, 1)]
    [InlineData("+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n", 5, 1)]
    [InlineData("text\n\n+---+\n", 3, 1)]
    [InlineData("para\n  +---+---+\n", 2, 3)]
    [InlineData("intro\n\n  +---+---+\n", 3, 3)]
    [InlineData("> para\n> +---+---+\n", 2, 3)]
    [InlineData("> para\n+---+---+\n", 2, 1)]
    [InlineData("- item\n+---+---+\n- next\n", 2, 1)]
    [InlineData("# Title\n\npara\n+---+---+\n\n- item\n", 4, 1)]
    [InlineData("para\n+:--+--:+\n", 2, 1)]
    [InlineData("- +---+---+\n  | a | b |\n  +---+---+\n\n- para\n  +---+\n", 6, 3)]
    public void DocumentParser_Parse_InvalidGridLine_MarksFormatUnsafeAtThatLine(string text, int line, int column)
    {
        // Arrange / Act: parse a document with a grid-table-looking line that is not a valid grid table
        var document = Parse(text);

        // Assert: the document is format-unsafe and the fallback names the offending line and column
        Assert.True(document.IsFormatUnsafe);
        Assert.Equal<GridTableFallback?>(new GridTableFallback(line, column), document.GridFallback);
    }

    /// <summary>
    ///     Test that the location is the invalid line and not a line of a valid grid table in the same document.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="line">The expected 1-based line of the invalid border.</param>
    [Theory]
    [InlineData("+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n", 5)]
    [InlineData("+---+---+\n| a | b |\n+---+---+\n\npara\n+---+\n", 6)]
    [InlineData("+---+---+\n| a | b |\n+===+===+\n| c | d |\n+---+---+\n\npara\n+---+\n", 8)]
    public void DocumentParser_Parse_ValidAndInvalidGridTable_ReportsInvalidLineNotValidTableLine(string text, int line)
    {
        // Arrange / Act: parse a valid grid table followed by an invalid grid-table-looking line
        var document = Parse(text);

        // Assert: the reported line is the invalid one, not the first line of the valid table
        Assert.NotNull(document.GridFallback);
        Assert.Equal<GridTableFallback?>(new GridTableFallback(line, 1), document.GridFallback);
    }

    /// <summary>
    ///     Test that a border-looking line inside a fenced code block is ordinary text and does not mark the document.
    /// </summary>
    /// <param name="text">The document.</param>
    [Theory]
    [InlineData("```\n+---+---+\n```\n")]
    [InlineData("para\n\n```\n+---+---+\n```\n")]
    [InlineData("~~~\n+---+\n~~~\n")]
    public void DocumentParser_Parse_InvalidGridLineInsideFence_NotMarked(string text)
    {
        // Arrange / Act: parse a fenced code block that contains a border-looking line
        var document = Parse(text);

        // Assert: the first reading nests, so there is no fallback
        Assert.False(document.IsFormatUnsafe);
        Assert.Null(document.GridFallback);
    }

    /// <summary>
    ///     Test that a border-looking line inside indented code or an HTML block is ordinary text and does not mark the document.
    /// </summary>
    /// <param name="text">The document.</param>
    [Theory]
    [InlineData("para\n\n    +---+---+\n")]
    [InlineData("<div>\n+---+---+\n</div>\n")]
    public void DocumentParser_Parse_InvalidGridLineInsideOtherLiteralBlocks_NotMarked(string text)
    {
        // Arrange / Act: parse a code or HTML block that contains a border-looking line
        var document = Parse(text);

        // Assert: the document is not format-unsafe, and the literal block is a block of its own
        Assert.False(document.IsFormatUnsafe);
        Assert.Null(document.GridFallback);
        Assert.NotEmpty(document.Blocks);
    }

    /// <summary>
    ///     Test that a border line without a trailing plus sign is located like any other border line.
    /// </summary>
    /// <param name="text">The document.</param>
    [Theory]
    [InlineData("para\n+---\n")]
    [InlineData("para\n+---+---\n")]
    public void DocumentParser_Parse_GridBorderWithoutTrailingPlus_Located(string text)
    {
        // Arrange / Act: parse a paragraph followed by a border that does not end with a plus sign
        var document = Parse(text);

        // Assert: the border line is reported
        Assert.True(document.IsFormatUnsafe);
        Assert.Equal<GridTableFallback?>(new GridTableFallback(2, 1), document.GridFallback);
    }

    /// <summary>
    ///     Test that a document with only a valid grid table is not marked.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ValidGridTableOnly_NotMarked()
    {
        // Arrange / Act: parse a valid Pandoc grid table
        var document = Parse("+---+---+\n| a | b |\n+---+---+\n| c | d |\n+---+---+\n");

        // Assert: the table is a grid table and there is no fallback
        Assert.Equal(BlockKind.GridTable, Assert.Single(document.Blocks).Kind);
        Assert.False(document.IsFormatUnsafe);
        Assert.Null(document.GridFallback);
    }

    /// <summary>
    ///     Test that a document without any grid-table-looking line is not marked.
    /// </summary>
    /// <param name="text">The document.</param>
    [Theory]
    [InlineData("plain\n")]
    [InlineData("# Title\n\n- a\n- b\n\n> quote\n\n| a | b |\n|---|---|\n| 1 | 2 |\n")]
    [InlineData("")]
    public void DocumentParser_Parse_PlainDocument_NotMarked(string text)
    {
        // Arrange / Act: parse a document without grid tables
        var document = Parse(text);

        // Assert: the document is not format-unsafe, and has blocks exactly when it has text
        Assert.False(document.IsFormatUnsafe);
        Assert.Null(document.GridFallback);
        Assert.Equal(text.Length > 0, !document.Blocks.IsEmpty);
    }

    /// <summary>
    ///     Test that a fallback whose offending line cannot be located is reported at line 1, column 1.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FallbackLocationUnknown_FallsBackToLineOneColumnOne()
    {
        // Arrange / Act: parse a grid table opener without a closing border; the first reading reports the opener
        // as a grid table block, so no remaining line can be named
        var document = Parse("+---+---+\n|a|b|\n\ntext\n");

        // Assert: the document is format-unsafe and the location is the start of the document
        Assert.True(document.IsFormatUnsafe);
        Assert.Equal<GridTableFallback?>(new GridTableFallback(1, 1), document.GridFallback);
    }

    /// <summary>
    ///     Test that a reported fallback location is either the start of the document or a grid border character on a border-shaped line.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_RandomDocuments_FallbackLineIsAlwaysAGridOpenerOrLineOne()
    {
        // Arrange: the generated adversarial documents, which include many grid-table-looking lines
        const uint seed = 20260511;
        const int countPerFamily = 500;
        var fallbacks = 0;
        var problems = new List<string>();

        // Act: parse every document and check each reported location
        foreach (var fixture in RandomDocuments.Generate(seed, countPerFamily))
        {
            var document = Parse(fixture.Text);
            if (document.GridFallback is not { } fallback)
            {
                continue;
            }

            fallbacks++;
            if (fallback is { Line: 1, Column: 1 })
            {
                continue;
            }

            if (!IsBorderLocation(document.Source.Text.Split('\n'), fallback))
            {
                problems.Add($"{fixture.Name}: {fallback} in \"{fixture.Text.Replace("\n", "\\n", StringComparison.Ordinal)}\"");
            }
        }

        // Assert: the check was not vacuous and every location is plausible
        Assert.True(fallbacks > 0, "no generated document took the fallback");
        Assert.True(problems.Count == 0, $"{problems.Count} problems:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Take(10))}");
    }

    /// <summary>
    ///     Test that a document that takes the grid table fallback still produces nested blocks that account for all text.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_GridTableFallback_StillProducesBlocks()
    {
        // Arrange / Act: parse a document whose second paragraph holds an invalid grid table border
        var document = Parse("# Title\n\npara\n+---+---+\n\n- item\n");

        // Assert: the blocks are those of the document read without grid tables, and they tile the text
        Assert.True(document.IsFormatUnsafe);
        Assert.Equal(
            [BlockKind.Heading, BlockKind.Paragraph, BlockKind.List, BlockKind.ListItem, BlockKind.Paragraph],
            document.Blocks.Select(b => b.Kind));
        Assert.Equal("para\n+---+---+", TextOf(document, document.Blocks[1]));
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that grid table fallback locations compare by value.
    /// </summary>
    [Fact]
    public void GridTableFallback_Equals_SameValues_AreEqual()
    {
        // Arrange: two locations with the same values and two that differ in one component
        var first = new GridTableFallback(2, 3);
        var same = new GridTableFallback(2, 3);
        var otherLine = new GridTableFallback(3, 3);
        var otherColumn = new GridTableFallback(2, 4);

        // Act / Assert: equality and hash code follow the values
        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, otherLine);
        Assert.NotEqual(first, otherColumn);
    }

    /// <summary>
    ///     Test that a grid table fallback keeps a valid line and column.
    /// </summary>
    [Fact]
    public void GridTableFallback_Constructor_ValidPosition_KeepsValues()
    {
        // Arrange / Act: the smallest valid position and another one
        var smallest = new GridTableFallback(1, 1);
        var other = new GridTableFallback(7, 3);

        // Assert: the values are kept
        Assert.Equal(1, smallest.Line);
        Assert.Equal(1, smallest.Column);
        Assert.Equal(7, other.Line);
        Assert.Equal(3, other.Column);
    }

    /// <summary>
    ///     Test that a line below one is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GridTableFallback_Constructor_LineBelowOne_Throws(int line)
    {
        // Arrange / Act / Assert: lines are 1-based
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridTableFallback(line, 1));
    }

    /// <summary>
    ///     Test that a column below one is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GridTableFallback_Constructor_ColumnBelowOne_Throws(int column)
    {
        // Arrange / Act / Assert: columns are 1-based
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridTableFallback(1, column));
    }
}
