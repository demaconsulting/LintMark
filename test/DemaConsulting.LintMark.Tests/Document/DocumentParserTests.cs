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

using System.Text;
using DemaConsulting.LintMark.Document;
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

        // Assert: quote > list > item > paragraph
        Assert.Equal(
            [(BlockKind.BlockQuote, 0), (BlockKind.List, 1), (BlockKind.ListItem, 2), (BlockKind.Paragraph, 3)],
            kinds);
        var paragraph = Single(document, BlockKind.Paragraph);
        Assert.IsType<ListItemBlock>(paragraph.Parent);
        Assert.Null(Single(document, BlockKind.BlockQuote).Parent);
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
        var tableBlock = Assert.IsType<Markdig.Extensions.Tables.Table>(table.Node);
        Assert.Single(Assert.IsType<Markdig.Extensions.Tables.TableRow>(tableBlock[1]));
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
    ///     Test that block quotes, alerts, math blocks, indented code and HTML blocks are classified.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_VariousBlocks_AreClassified()
    {
        // Arrange: alert, math, closed fence, indented code and HTML block separated by blank lines
        var document = Parse("> [!NOTE]\n> alert\n\n$$\nx\n$$\n\n```\ncode\n```\n\n<div>\nhi\n</div>\n\n- [x] task\n");

        // Act: collect the kinds
        var kinds = document.Blocks.Select(b => b.Kind).Distinct().ToList();

        // Assert: each construct has its kind
        Assert.Contains(BlockKind.BlockQuote, kinds);
        Assert.Contains(BlockKind.MathBlock, kinds);
        Assert.Contains(BlockKind.FencedCode, kinds);
        Assert.Contains(BlockKind.HtmlBlock, kinds);
        Assert.Contains(BlockKind.List, kinds);
        Assert.DoesNotContain(BlockKind.UnclosedFence, kinds);
    }

    /// <summary>
    ///     Test that GFM inline extensions are active in the fixed pipeline.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_InlineExtensions_AreRecognized()
    {
        // Arrange: strikethrough, bare URL autolink, task list item
        var document = Parse("~~gone~~ and https://example.com here.\n\n- [ ] open\n");

        // Act: collect inline facts
        var emphasis = document.Ast.Descendants<EmphasisInline>().ToList();
        var links = document.Ast.Descendants<LinkInline>().ToList();

        // Assert: strikethrough and autolink exist
        Assert.Contains(emphasis, e => e.DelimiterChar == '~');
        Assert.Contains(links, l => l.IsAutoLink);
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
    ///     Test Q-1: an unclosed fence is recorded with an effective span that reaches the end of the document.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedFence_Q1_EffectiveSpanReachesEnd()
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
    ///     Test Q-1: an unclosed fence inside a container ends with its parent, not at the end of the file.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnclosedFenceInQuote_Q1_EndsWithParent()
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
    ///     Test Q-2: list and list item spans never include the trailing newline, although the parser's own span does.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ListTrailingNewline_Q2_EffectiveEndTrimmed()
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
    ///     Test Q-2: indented code spans never include the trailing newline.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_IndentedCodeTrailingNewline_Q2_EffectiveEndTrimmed()
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
    ///     Test Q-3: reference definition groups are dropped and each definition keeps its own exact span, in source order.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ReferenceAndFootnoteGroups_Q3_SourceOrderedNoShells()
    {
        // Arrange: definitions interleaved with a footnote
        const string text = "Text [a] and [b] and[^n].\n\n[a]: http://x\n\n[^n]: Note\n\n[b]: http://y\n";

        // Act: parse
        var document = Parse(text);

        // Assert: no group shells, blocks sorted by start, definitions have their own spans
        var summary = Summarize(document);
        Assert.DoesNotContain(document.Blocks, b => b.Node is LinkReferenceDefinitionGroup or Markdig.Extensions.Footnotes.FootnoteGroup);
        Assert.Contains((BlockKind.LinkReferenceDefinition, "[a]: http://x"), summary);
        Assert.Contains((BlockKind.LinkReferenceDefinition, "[b]: http://y"), summary);
        Assert.Contains(document.Blocks, b => b.Kind == BlockKind.Footnote);
        Assert.Equal(document.Blocks.Select(b => b.Start).Order().ToList(), document.Blocks.Select(b => b.Start).ToList());
    }

    /// <summary>
    ///     Test Q-3: a footnote definition that nothing references is found by scanning, because the parser drops it.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_UnreferencedFootnote_Q3_DetectedByScan()
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
    ///     Test Q-3: a footnote-looking line inside a code block is not a definition.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_FootnoteLikeLineInCode_Q3_IsNotReported()
    {
        // Arrange: a fenced code block that contains a footnote-looking line
        var document = Parse("```\n[^x]: not a footnote\n```\n");

        // Act: classify
        var kinds = document.Blocks.Select(b => b.Kind).ToList();

        // Assert: only the code block exists
        Assert.Equal([BlockKind.FencedCode], kinds);
    }

    /// <summary>
    ///     Test Q-4: parsing is independent of the original line-ending style.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_CrLfCodeSpan_Q4_ParsesLfNormalizedText()
    {
        // Arrange: a multi-line code span ending a line, as bytes in LF and CRLF form
        const string lf = "Use `X`\n  removes\n\n- a\n  b\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        // Act: parse both from bytes
        var fromLf = DocumentParser.Parse(SourceDocument.Create("a.md", Encoding.UTF8.GetBytes(lf)));
        var fromCrLf = DocumentParser.Parse(SourceDocument.Create("a.md", Encoding.UTF8.GetBytes(crlf)));

        // Assert: identical text and identical block lists
        Assert.Equal(fromLf.Source.Text, fromCrLf.Source.Text);
        Assert.Equal(Summarize(fromLf), Summarize(fromCrLf));
        Assert.Equal(
            fromLf.Blocks.Select(b => (b.Kind, b.Start, b.EndInclusive, b.Depth)).ToList(),
            fromCrLf.Blocks.Select(b => (b.Kind, b.Start, b.EndInclusive, b.Depth)).ToList());
    }

    /// <summary>
    ///     Test Q-5: a lazy continuation line is flagged lazy; explicit prefixes are not.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_LazyContinuation_Q5_FlagsLazyLines()
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
    ///     Test Q-5: a setext-underline lookalike that is only paragraph text because it is lazy is flagged lazy.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_LazySetextLookalike_Q5_IsParagraphTextAndLazy()
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
    ///     Test Q-8: paragraph lines are rebuilt from the source with exact content.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_ParagraphLines_Q8_RebuiltFromSpan()
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
    ///     Test R-4: the content start of continuation lines in nested containers comes from the first inline on the line.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_NestedContainers_R4_ContentStartIsFirstInline()
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
    ///     Test R-4: a continuation line starting inside a multi-line code span falls back to prefix stripping.
    /// </summary>
    [Fact]
    public void DocumentParser_GetLines_MultiLineCodeSpan_R4_FallsBackToPrefixStripping()
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
    ///     Test R-4 with a tab: a tab-indented continuation line has its content found exactly.
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
    ///     Test R-5: the continuation prefix replaces everything except quote markers and whitespace with spaces.
    /// </summary>
    [Theory]
    [InlineData("- a\n", "  ")]
    [InlineData("1. a\n", "   ")]
    [InlineData("10. a\n", "    ")]
    [InlineData("> - a\n", ">   ")]
    [InlineData("> a\n", "> ")]
    [InlineData("plain\n", "")]
    public void DocumentParser_GetContinuationPrefix_R5_BlanksMarkers(string text, string expected)
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
    ///     Test that a UTF-8 BOM fixture parses with offsets relative to the text without the mark.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_Bom_OffsetsExcludeByteOrderMark()
    {
        // Arrange: a BOM followed by a heading
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Title\n\ntext\n")];

        // Act: parse
        var document = DocumentParser.Parse(SourceDocument.Create("a.md", bytes));

        // Assert: the heading starts at offset 0 and the BOM is remembered
        Assert.Equal(0, Single(document, BlockKind.Heading).Start);
        Assert.True(document.Source.HasBom);
        Assert.Equal("# Title", Summarize(document)[0].Text);
    }

    /// <summary>
    ///     Test that tab fixtures (tab-indented list, tab-indented code, tabs inside text) parse with sensible spans.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_Tabs_ListCodeAndTextSpansAreExact()
    {
        // Arrange: nested list indented with a tab, tab-indented code, and a paragraph with an inner tab
        const string text = "- a\n\t- b\n\ncol1\tcol2\n\n\tcode here\n";

        // Act: parse
        var document = Parse(text);

        // Assert: nested items, paragraph with the tab intact, indented code
        var summary = Summarize(document);
        Assert.Contains((BlockKind.Paragraph, "col1\tcol2"), summary);
        Assert.Equal(2, document.Blocks.Count(b => b.Kind == BlockKind.ListItem));
        Assert.Single(document.Blocks, b => b.Kind == BlockKind.IndentedCode);
        var nested = document.Blocks.Where(b => b.Kind == BlockKind.ListItem).Max(b => b.Depth);
        Assert.True(nested >= 3);
    }

    /// <summary>
    ///     Test that parsing the same document from many threads yields identical results.
    /// </summary>
    [Fact]
    public void DocumentParser_Parse_ConcurrentCalls_ProduceIdenticalBlocks()
    {
        // Arrange: a document with varied constructs and the single-threaded result
        const string text = "# H\n\n- a\n  b\n\n> q\n\n```\nunclosed\n";
        var expected = Summarize(Parse(text));

        // Act: parse concurrently
        var results = new List<(BlockKind Kind, string Text)>[64];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = Summarize(Parse(text)));

        // Assert: every result equals the single-threaded one
        Assert.All(results, r => Assert.Equal(expected, r));
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
}
