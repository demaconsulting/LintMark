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
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests for the Document subsystem: source loading, parsing, suppression and reference
///     handling working together.
/// </summary>
public class DocumentSubsystemTests
{
    /// <summary>
    ///     Re-encodes LF text as UTF-8 bytes (without byte order mark) with CRLF line endings.
    /// </summary>
    /// <param name="lf">LF-only text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] CrLfBytes(string lf) =>
        new UTF8Encoding(false).GetBytes(lf.Replace("\n", "\r\n", StringComparison.Ordinal));

    /// <summary>
    ///     Summarizes the block structure of a document so two parses can be compared.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>Kind, span and depth of every block.</returns>
    private static List<(BlockKind Kind, int Start, int End, int Depth)> Structure(ParsedDocument document) =>
        [.. document.Blocks.Select(b => (b.Kind, b.Start, b.EndInclusive, b.Depth))];

    /// <summary>
    ///     Test that no collection of a parse result can be changed by a caller that casts it.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_ParseResult_ExposedCollections_CannotBeModified()
    {
        // Arrange / Act: parse a document that has blocks, a suppression region and a definition
        var document = DocumentParser.Parse(SourceDocument.FromText(
            "t.md", "<!-- lintmark-disable line-length -->\n\nText [a].\n\n<!-- lintmark-enable -->\n\n[a]: http://a.example\n"));
        IReadOnlyList<BlockInfo> blocks = document.Blocks;

        // Assert: every collection that the result exposes refuses changes through a cast
        ReadOnlyAssertions.AssertCannotBeModified(blocks);
        ReadOnlyAssertions.AssertCannotBeModified(document.Source.LineStarts);
        ReadOnlyAssertions.AssertCannotBeModified(document.Suppressions.Ranges);
        ReadOnlyAssertions.AssertCannotBeModified(document.Suppressions.Ranges[0].Rules.Rules);
        ReadOnlyAssertions.AssertCannotBeModified(document.References.Definitions);
    }

    /// <summary>
    ///     Test that text parsed from LF and from CRLF bytes gives identical blocks, including a code span that ends a line.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_LineEndingVariants_MultiLineCodeSpan_GivesSameBlocksForLfAndCrLf()
    {
        // Arrange: a multi-line code span ending a line (Markdig reads it differently for CRLF text), as LF and CRLF bytes
        const string text = "Use `X`\n  removes\n\n- a\n  b\n";

        // Act: parse both from bytes
        var fromLf = DocumentParser.Parse(SourceDocument.Create("a.md", new UTF8Encoding(false).GetBytes(text)));
        var fromCrLf = DocumentParser.Parse(SourceDocument.Create("a.md", CrLfBytes(text)));

        // Assert: identical normalized text and identical block lists
        Assert.Equal(fromLf.Source.Text, fromCrLf.Source.Text);
        Assert.Equal(Structure(fromLf), Structure(fromCrLf));
        Assert.Equal(
            [BlockKind.Paragraph, BlockKind.List, BlockKind.ListItem, BlockKind.Paragraph],
            fromCrLf.Blocks.Select(b => b.Kind).ToList());
    }

    /// <summary>
    ///     Test that content that cannot be loaded is rejected with the rule and the position of the first problem.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_InvalidContent_Load_ReportsFailureWithPosition()
    {
        // Arrange: invalid UTF-8 on line 2, a UTF-16 byte order mark, and a bare carriage return on line 3
        byte[] invalidUtf8 = [.. "ok\nab"u8, 0xFF, .. "\n"u8];
        byte[] utf16 = [0xFF, 0xFE, 0x61, 0x00];
        var loneCr = "a\nb\nc\rd\n"u8.ToArray();

        // Act: load each
        var encoding = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("a.md", invalidUtf8));
        var bom = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("b.md", utf16));
        var lineEnding = Assert.Throws<SourceDocumentException>(() => SourceDocument.Create("c.md", loneCr));

        // Assert: rule and position of the first offending byte or character
        Assert.Equal((SourceDocumentException.EncodingRuleId, 2, 3), (encoding.RuleId, encoding.Line, encoding.Column));
        Assert.Equal((SourceDocumentException.EncodingRuleId, 1, 1), (bom.RuleId, bom.Line, bom.Column));
        Assert.Equal((SourceDocumentException.LineEndingRuleId, 3, 2), (lineEnding.RuleId, lineEnding.Line, lineEnding.Column));
    }

    /// <summary>
    ///     Test that block offsets of a parsed document convert to the expected line and column positions and back.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_Positions_ParsedBlocks_ConvertToLineAndColumn()
    {
        // Arrange: a document from CRLF bytes with a byte order mark; positions must ignore both
        const string text = "# Title\n\n> - item\n>   more\n\n[a]: http://x\n";
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. CrLfBytes(text)];

        // Act: parse and convert the start of every block
        var document = DocumentParser.Parse(SourceDocument.Create("p.md", bytes));
        var positions = document.Blocks.Select(b => (b.Kind, document.Source.PositionOf(b.Start))).ToList();

        // Assert: exact 1-based positions, and every position converts back to the same offset
        Assert.Equal(
            [
                (BlockKind.Heading, (1, 1)),
                (BlockKind.BlockQuote, (3, 1)),
                (BlockKind.List, (3, 3)),
                (BlockKind.ListItem, (3, 3)),
                (BlockKind.Paragraph, (3, 5)),
                (BlockKind.LinkReferenceDefinition, (6, 1))
            ],
            positions);
        Assert.All(document.Blocks, b =>
        {
            var (line, column) = document.Source.PositionOf(b.Start);
            Assert.Equal(b.Start, document.Source.OffsetOf(line, column));
        });
    }

    /// <summary>
    ///     Test that every construct of the fixed dialect is recognized from file bytes and that nothing is generated.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_Dialect_AllConstructs_AreRecognizedFromBytes()
    {
        // Arrange: front matter, heading, pipe table, grid table, task list with inline constructs, alert, math and a footnote
        const string text =
            "---\ntitle: x\n---\n\n# H\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n+---+---+\n| a | b |\n+---+---+\n\n"
            + "- [x] task https://example.com ~~gone~~ $m$\n\n> [!NOTE]\n> alert\n\n$$\nx\n$$\n\nT[^n].\n\n[^n]: Note\n";

        // Act: parse from bytes
        var document = DocumentParser.Parse(SourceDocument.Create("d.md", new UTF8Encoding(false).GetBytes(text)));

        // Assert: one block per construct in source order, the inline constructs exist, and no identifier was generated
        Assert.Equal(
            [
                BlockKind.FrontMatter, BlockKind.Heading, BlockKind.Table, BlockKind.GridTable, BlockKind.List, BlockKind.ListItem,
                BlockKind.Paragraph, BlockKind.BlockQuote, BlockKind.Paragraph, BlockKind.MathBlock, BlockKind.Paragraph,
                BlockKind.Footnote, BlockKind.Paragraph
            ],
            document.Blocks.Select(b => b.Kind).ToList());
        Assert.Single(document.Ast.Descendants<TaskList>());
        Assert.Single(document.Ast.Descendants<LinkInline>(), l => l.IsAutoLink);
        Assert.Single(document.Ast.Descendants<EmphasisInline>(), e => e.DelimiterChar == '~');
        Assert.Single(document.Ast.Descendants<MathInline>());
        Assert.Empty(document.References.Definitions);
        Assert.Null(Assert.Single(document.Ast.Descendants<HeadingBlock>()).TryGetAttributes());
    }

    /// <summary>
    ///     Test the load, parse, suppress and reference flow on one document from bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_ParseToSuppressionAndReferences_Workflow_ProducesConsistentResults()
    {
        // Arrange: a document with a BOM and CRLF endings, a suppression region, definitions (the label of the
        // last one repeats the first in another letter case) and an unreferenced footnote
        const string text =
            "# Title\n\nSee [a] and [b].\n\n<!-- lintmark-disable line-length -->\n\nINSIDE paragraph.\n\n"
            + "<!-- lintmark-enable line-length -->\n\nAFTER paragraph.\n\n[a]: http://a.example\n[b]: http://b.example \"B\"\n"
            + "[A]: http://repeated.example\n\n[^orphan]: Orphan.\n";
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. CrLfBytes(text)];

        // Act: load and parse
        var document = DocumentParser.Parse(SourceDocument.Create("flow.md", bytes));
        var inside = document.Source.Text.IndexOf("INSIDE", StringComparison.Ordinal);
        var after = document.Source.Text.IndexOf("AFTER", StringComparison.Ordinal);

        // Assert: source facts, suppression, references, the orphan footnote and tiling all agree
        Assert.True(document.Source.HasBom);
        Assert.Equal(LineEnding.CrLf, document.Source.MajorityLineEnding);
        Assert.True(document.Suppressions.IsSuppressed(inside, "line-length"));
        Assert.False(document.Suppressions.IsSuppressed(inside, "other-rule"));
        Assert.False(document.Suppressions.IsSuppressed(after, "line-length"));
        Assert.Equal(["a", "b", "A"], document.References.Definitions.Select(d => d.Label).ToList());
        Assert.Equal([13, 14, 15], document.References.Definitions.Select(d => d.Line).ToList());
        Assert.True(document.References.TryGet("  A ", out var found));
        Assert.Equal("http://a.example", found.Url);
        Assert.Contains(document.Blocks, b => b.Kind == BlockKind.UnreferencedFootnote);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that a suppression comment that names a rule by its legacy rule number suppresses the rule that the number stands for.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_Suppressions_RuleNamedByLegacyNumber_SuppressesTheRule()
    {
        // Arrange: a region that disables MD009 and a document that enables it again
        const string text =
            "<!-- markdownlint-disable MD009 -->\n\nINSIDE paragraph.\n\n<!-- markdownlint-enable MD009 -->\n\nAFTER paragraph.\n";

        // Act: parse the document
        var document = DocumentParser.Parse(SourceDocument.Create("alias.md", new UTF8Encoding(false).GetBytes(text)));
        var inside = document.Source.Text.IndexOf("INSIDE", StringComparison.Ordinal);
        var after = document.Source.Text.IndexOf("AFTER", StringComparison.Ordinal);

        // Assert: the canonical rule is suppressed inside the region only, and a rule that the number does not stand for is not
        Assert.True(document.Suppressions.IsSuppressed(inside, "trailing-whitespace"));
        Assert.False(document.Suppressions.IsSuppressed(inside, "hard-tabs"));
        Assert.False(document.Suppressions.IsSuppressed(after, "trailing-whitespace"));
    }

    /// <summary>
    ///     Test that a valid grid table together with an invalid one marks the loaded document format-unsafe at the invalid line.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_GridFallback_InvalidAndValidTables_MarksUnsafeAtOffendingLine()
    {
        // Arrange: CRLF bytes with a valid grid table (lines 1 to 3) and an invalid border on line 5
        const string text = "+---+---+\n| a | b |\n+---+---+\n\n+---+\n# h\n";

        // Act: load and parse
        var document = DocumentParser.Parse(SourceDocument.Create("grid.md", CrLfBytes(text)));

        // Assert: the document is format-unsafe at the invalid line, read without grid tables, and the blocks nest
        Assert.True(document.IsFormatUnsafe);
        Assert.Equal<GridTableFallback?>(new GridTableFallback(5, 1), document.GridFallback);
        Assert.DoesNotContain(document.Blocks, b => b.Kind == BlockKind.GridTable);
        Assert.Empty(SpanTilingAssertions.CheckNesting(document));
    }

    /// <summary>
    ///     Test that a loaded document with only a valid grid table is not marked and keeps its grid table.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_GridFallback_ValidTableOnly_NotMarked()
    {
        // Arrange: CRLF bytes with a valid grid table between two paragraphs
        const string text = "before\n\n+---+---+\n| a | b |\n+---+---+\n\nafter\n";

        // Act: load and parse
        var document = DocumentParser.Parse(SourceDocument.Create("grid.md", CrLfBytes(text)));

        // Assert: no fallback, the table is a grid table, and the blocks tile the text
        Assert.False(document.IsFormatUnsafe);
        Assert.Null(document.GridFallback);
        Assert.Single(document.Blocks, b => b.Kind == BlockKind.GridTable);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }

    /// <summary>
    ///     Test that parsing the same text twice reports the same fallback location.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_GridFallback_ParsedAgain_SameFallback()
    {
        // Arrange: a document that takes the fallback
        const string text = "para\n+---+---+\n\n> quote\n> +---+\n";
        var source = SourceDocument.FromText("again.md", text);

        // Act: parse the same source twice, and an equal source once more
        var first = DocumentParser.Parse(source);
        var second = DocumentParser.Parse(source);
        var third = DocumentParser.Parse(SourceDocument.FromText("again.md", text));

        // Assert: every parse reports the same location, the first offending line
        Assert.Equal<GridTableFallback?>(new GridTableFallback(2, 1), first.GridFallback);
        Assert.Equal<GridTableFallback?>(first.GridFallback, second.GridFallback);
        Assert.Equal<GridTableFallback?>(first.GridFallback, third.GridFallback);
    }
}
