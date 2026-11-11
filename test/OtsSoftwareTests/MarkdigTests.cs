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
    ///     Documents the unclosed fence quirk (Q-1): the block span covers only the opening line.
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
    ///     Documents the list span quirk (Q-2): a list followed by a blank line includes the trailing newline.
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
    ///     Documents the shell quirk (Q-3): reference definitions are grouped in a shell block with their own definition blocks.
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
}
