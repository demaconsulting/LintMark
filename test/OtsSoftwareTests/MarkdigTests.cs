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

using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace OtsSoftwareTests;

/// <summary>
///     Integration tests of the Markdig OTS package: they pin the vendor behavior that LintMark's
///     document parser relies on, independent of LintMark's own code.
/// </summary>
public class MarkdigTests
{
    /// <summary>
    ///     Builds the pipeline LintMark uses (repeated here deliberately so the OTS evidence does not depend on LintMark code).
    /// </summary>
    /// <returns>The pipeline.</returns>
    private static MarkdownPipeline CreatePipeline() => new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .UseGridTables()
        .UseFootnotes()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAlertBlocks()
        .UseMathematics()
        .Build();

    /// <summary>
    ///     Validates that block and inline spans are absolute and source-accurate.
    /// </summary>
    [Fact]
    public void Markdig_SourceSpans_BlocksAndInlines_AreSourceAccurate()
    {
        // Arrange: a heading and a paragraph with a code span and a link
        const string text = "# Title\n\nSome `code` and [link](http://x \"t\").\n";

        // Act: parse with the precise-source-location pipeline
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: spans address exactly the source text of each construct
        var heading = Assert.IsType<HeadingBlock>(document[0]);
        Assert.Equal("# Title", text.Substring(heading.Span.Start, heading.Span.Length));
        var paragraph = Assert.IsType<ParagraphBlock>(document[1]);
        Assert.Equal("Some `code` and [link](http://x \"t\").", text.Substring(paragraph.Span.Start, paragraph.Span.Length));
        var code = paragraph.Inline!.Descendants<CodeInline>().Single();
        Assert.Equal("`code`", text.Substring(code.Span.Start, code.Span.Length));
        var link = paragraph.Inline!.Descendants<LinkInline>().Single();
        Assert.Equal("[link](http://x \"t\")", text.Substring(link.Span.Start, link.Span.Length));
        Assert.Equal("http://x", text.Substring(link.UrlSpan.Start, link.UrlSpan.Length));
    }

    /// <summary>
    ///     Validates that every extension LintMark enables recognizes its construct.
    /// </summary>
    [Fact]
    public void Markdig_Pipeline_RequiredExtensions_RecognizedBlocks()
    {
        // Arrange: one document containing every enabled construct
        const string text =
            "---\ntitle: x\n---\n\n"
            + "| a | b |\n|---|---|\n| 1 | 2 |\n\n"
            + "+---+---+\n| a | b |\n+---+---+\n| 1 | 2 |\n+---+---+\n\n"
            + "Note[^1] with ~~strike~~ and https://example.com bare and $x$ math.\n\n"
            + "- [x] task\n\n"
            + "> [!NOTE]\n> alert\n\n"
            + "$$\nx = 1\n$$\n\n"
            + "[^1]: The footnote.\n";

        // Act: parse with the fixed pipeline
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: each construct produced its dedicated syntax element
        Assert.Single(document.Descendants<YamlFrontMatterBlock>());
        var tables = document.Descendants<Table>().ToList();
        Assert.Equal(2, tables.Count);
        Assert.Contains(tables, t => text[t.Span.Start] == '+');
        Assert.Contains(tables, t => text[t.Span.Start] == '|');
        Assert.Single(document.Descendants<Footnote>());
        Assert.Single(document.Descendants<TaskList>());
        Assert.Single(document.Descendants<AlertBlock>());
        Assert.Single(document.Descendants<MathBlock>());
        Assert.Contains(document.Descendants<EmphasisInline>(), e => e.DelimiterChar == '~');
        Assert.Contains(document.Descendants<LinkInline>(), l => l.IsAutoLink);
        Assert.NotEmpty(document.Descendants<MathInline>());
    }

    /// <summary>
    ///     Validates the GFM table rule: an escaped pipe inside a code span stays within its cell.
    /// </summary>
    [Fact]
    public void Markdig_PipeTables_GfmRules_EscapedPipeInCodeSpanStaysInCell()
    {
        // Arrange: a table whose cell holds a code span with an escaped pipe
        const string text = "| a |\n|---|\n| `x\\|y` |\n";

        // Act: parse with GFM table rules
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: the body row has exactly one cell
        var table = document.Descendants<Table>().Single();
        var row = Assert.IsType<TableRow>(table[1]);
        Assert.Single(row);
    }

    /// <summary>
    ///     Documents that the span of an unclosed fenced code block covers only its opening line.
    /// </summary>
    [Fact]
    public void Markdig_UnclosedFence_SpanCoversOnlyOpeningLine()
    {
        // Arrange: a fence that is never closed
        const string text = "para\n\n```\ncode\nmore\n\nafter\n";

        // Act: parse
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: the fence has no closing fence and its span ends at the opening line
        var fence = document.Descendants<FencedCodeBlock>().Single();
        Assert.Equal(0, fence.ClosingFencedCharCount);
        Assert.True(fence.Span.End < text.IndexOf("code", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Documents that the span of a list followed by a blank line includes the trailing line feed.
    /// </summary>
    [Fact]
    public void Markdig_ListSpan_IncludesTrailingNewline()
    {
        // Arrange: a list followed by a blank line and another list
        const string text = "1. x\n   y\n\n- t\n";

        // Act: parse
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: the first list's span ends with the newline
        var list = Assert.IsType<ListBlock>(document[0]);
        Assert.Equal('\n', text[list.Span.End]);
    }

    /// <summary>
    ///     Documents that link reference definitions are grouped in a shell block that holds the definition blocks with their own spans.
    /// </summary>
    [Fact]
    public void Markdig_LinkReferenceDefinitions_ExposeLabelUrlTitleAndSpan()
    {
        // Arrange: a document with a definition
        const string text = "Text [a].\n\n[a]: http://example.com \"Title\"\n";

        // Act: parse
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: the definition carries label, URL, title and a source-accurate span
        var group = document.OfType<LinkReferenceDefinitionGroup>().Single();
        var definition = Assert.IsType<LinkReferenceDefinition>(group.Single());
        Assert.Equal("a", definition.Label);
        Assert.Equal("http://example.com", definition.Url);
        Assert.Equal("Title", definition.Title);
        Assert.Equal("[a]: http://example.com \"Title\"", text.Substring(definition.Span.Start, definition.Span.Length));
    }

    /// <summary>
    ///     Documents that a definition whose title on the next line is rejected (text follows it) keeps the title in its span.
    /// </summary>
    [Fact]
    public void Markdig_LinkReferenceDefinition_RejectedNextLineTitle_SpanStillCoversTitle()
    {
        // Arrange: a definition followed by a line that starts like a title but has text after it, one with an empty title and one without a title
        const string rejectedText = "[a]: /u\n\"t\" x\n";
        const string emptyText = "[a]: /u\n\"\"\n";
        const string noneText = "[a]: /u\n";

        // Act: parse
        var rejected = ParseDefinition(rejectedText);
        var empty = ParseDefinition(emptyText);
        var none = ParseDefinition(noneText);

        // Assert: the rejected title is null but still inside the span; an empty title is an empty string; no title has an empty title span
        Assert.Null(rejected.Title);
        Assert.True(rejected.TitleSpan.End >= rejected.TitleSpan.Start);
        Assert.Equal("/u", rejectedText.Substring(rejected.UrlSpan.Start, rejected.UrlSpan.Length));
        Assert.Equal("[a]: /u\n\"t\"", rejectedText.Substring(rejected.Span.Start, rejected.Span.Length));
        Assert.Equal(string.Empty, empty.Title);
        Assert.Equal("[a]: /u\n\"\"", emptyText.Substring(empty.Span.Start, empty.Span.Length));
        Assert.Null(none.Title);
        Assert.True(none.TitleSpan.End < none.TitleSpan.Start);
    }

    /// <summary>
    ///     Documents that a definition inside a footnote that the parser drops stays a link reference definition.
    /// </summary>
    [Fact]
    public void Markdig_DroppedFootnote_DefinitionOnMarkerLine_DefinitionSurvives()
    {
        // Arrange: a footnote definition that nothing refers to, with a definition behind its marker
        const string text = "[^1]: [a]: /u\n";

        // Act: parse
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: there is no footnote block, but the definition with label "a" exists with its own span
        Assert.Empty(document.Descendants<Footnote>());
        var group = document.OfType<LinkReferenceDefinitionGroup>().Single();
        var definition = Assert.Single(group.OfType<LinkReferenceDefinition>(), d => d is not FootnoteLinkReferenceDefinition);
        Assert.Equal("a", definition.Label);
        Assert.Equal("[a]: /u", text.Substring(definition.Span.Start, definition.Span.Length));
    }

    /// <summary>
    ///     Parses a document and gets its only definition that is not the synthetic definition of a footnote label.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <returns>The definition.</returns>
    private static LinkReferenceDefinition ParseDefinition(string text) =>
        Markdown.Parse(text, CreatePipeline()).OfType<LinkReferenceDefinitionGroup>().Single()
            .OfType<LinkReferenceDefinition>().Single(d => d is not FootnoteLinkReferenceDefinition);

    /// <summary>
    ///     Validates that HTML comments are exposed as comment-typed HTML blocks and as inline HTML with exact spans.
    /// </summary>
    [Fact]
    public void Markdig_HtmlComments_BlockAndInline_AreRecognized()
    {
        // Arrange: one block-level comment and one inline comment
        const string text = "<!-- block -->\n\nText <!-- inline --> more.\n";

        // Act: parse
        var document = Markdown.Parse(text, CreatePipeline());

        // Assert: the block is a comment block and the inline span covers the whole comment
        var block = document.Descendants<HtmlBlock>().Single();
        Assert.Equal(HtmlBlockType.Comment, block.Type);
        var inline = document.Descendants<HtmlInline>().Single();
        Assert.Equal("<!-- inline -->", text.Substring(inline.Span.Start, inline.Span.Length));
    }

    /// <summary>
    ///     Validates that a single immutable pipeline can be used by many threads with identical results.
    /// </summary>
    [Fact]
    public void Markdig_SharedPipeline_ParallelParse_IdenticalResults()
    {
        // Arrange: one shared pipeline and a document with varied constructs
        var pipeline = CreatePipeline();
        const string text = "# H\n\n- a\n  b\n\n> q\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n[x]: http://x\n";
        var expected = Markdown.ToHtml(text, pipeline);

        // Act: parse and render concurrently from many tasks
        var results = new string[64];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = Markdown.ToHtml(text, pipeline));

        // Assert: every result equals the single-threaded one
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    /// <summary>
    ///     Validates that rendering the same text with the same pipeline (or an identically built one) always gives the same HTML.
    /// </summary>
    [Fact]
    public void Markdig_HtmlRendering_SamePipelineAndText_IsDeterministic()
    {
        // Arrange: a pipeline, a second pipeline built the same way, and a document with varied constructs
        var pipeline = CreatePipeline();
        var other = CreatePipeline();
        const string text = "# H\n\nSome *emphasis* and `code` and [a link](http://x \"t\").\n\n- a\n- b\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";

        // Act: render repeatedly
        var first = Markdown.ToHtml(text, pipeline);
        var second = Markdown.ToHtml(text, pipeline);
        var third = Markdown.ToHtml(text, other);

        // Assert: all renderings are identical
        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    /// <summary>
    ///     Documents that a multi-line code span whose content starts and ends with a line break renders differently for
    ///     CRLF and LF input, which is why LintMark renders LF-normalized text only.
    /// </summary>
    [Fact]
    public void Markdig_HtmlRendering_CrlfVersusLfCodeSpan_DiffersAsDocumented()
    {
        // Arrange: a code span whose content is framed by line breaks, in LF and CRLF form
        const string lf = "`\nbb\n`\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        var pipeline = CreatePipeline();

        // Act: render both
        var lfHtml = Markdown.ToHtml(lf, pipeline);
        var crlfHtml = Markdown.ToHtml(crlf, pipeline);

        // Assert: with LF the line breaks become spaces that are stripped; with CRLF the spaces stay
        Assert.Equal("<p><code>bb</code></p>\n", lfHtml);
        Assert.Equal("<p><code> bb </code></p>\n", crlfHtml);
        Assert.NotEqual(lfHtml, crlfHtml);
    }

    /// <summary>
    ///     Validates that one shared pipeline renders several different documents from many threads with the same HTML as a single thread.
    /// </summary>
    [Fact]
    public void Markdig_HtmlRendering_SharedPipelineParallel_IdenticalResults()
    {
        // Arrange: one shared pipeline, several documents, and the single-threaded HTML of each
        var pipeline = CreatePipeline();
        string[] texts =
        [
            "# H\n\nText with `code` and *emphasis*.\n",
            "- a\n  b\n\n> quote\n",
            "| a | b |\n|---|---|\n| 1 | 2 |\n",
            "+---+---+\n| a | b |\n+---+---+\n| 1 | 2 |\n+---+---+\n",
            "Note[^1].\n\n[^1]: The footnote.\n"
        ];
        var expected = texts.Select(t => Markdown.ToHtml(t, pipeline)).ToArray();
        var results = new string[128];

        // Act: render from many threads, each rendering one of the documents
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => results[i] = Markdown.ToHtml(texts[i % texts.Length], pipeline));

        // Assert: every result equals the single-threaded HTML of its document
        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(expected[i % texts.Length], results[i]);
        }
    }

    /// <summary>
    ///     Validates that a pipeline with grid tables and pipe tables renders both kinds of table as HTML tables.
    /// </summary>
    [Fact]
    public void Markdig_HtmlRendering_GridTableAndPipeTable_RenderAsTables()
    {
        // Arrange: a pipeline with only the two table extensions, and a grid table followed by a pipe table
        var pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
            .UseGridTables()
            .Build();
        const string text = "+---+---+\n| a | b |\n+---+---+\n| 1 | 2 |\n+---+---+\n\n| c | d |\n|---|---|\n| 3 | 4 |\n";

        // Act: render
        var html = Markdown.ToHtml(text, pipeline);

        // Assert: two tables, the grid table with plain cells and the pipe table with a header row
        Assert.Equal(2, html.Split("<table>").Length - 1);
        Assert.Contains("<td>a</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2</td>", html, StringComparison.Ordinal);
        Assert.Contains("<th>c</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>4</td>", html, StringComparison.Ordinal);
    }
}
