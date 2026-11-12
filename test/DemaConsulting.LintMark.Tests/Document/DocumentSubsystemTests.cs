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
///     handling working together, verified against this repository's own Markdown files and generated nesting fixtures.
/// </summary>
public class DocumentSubsystemTests
{
    /// <summary>
    ///     The seed of the generated adversarial documents (fixed, so every run checks the same documents).
    /// </summary>
    private const uint RandomSeed = 20260511;

    /// <summary>
    ///     The number of generated adversarial documents per family.
    /// </summary>
    private const int RandomDocumentsPerFamily = 1500;

    /// <summary>
    ///     The three line-ending variants a corpus file is tested in.
    /// </summary>
    private enum Variant
    {
        /// <summary>Every terminator is LF.</summary>
        Lf,

        /// <summary>Every terminator is CRLF.</summary>
        CrLf,

        /// <summary>Alternate terminators are CRLF and LF.</summary>
        Mixed
    }

    /// <summary>
    ///     Re-encodes file text with the line endings of a variant.
    /// </summary>
    /// <param name="text">Text of any line-ending style.</param>
    /// <param name="variant">The variant to produce.</param>
    /// <returns>UTF-8 bytes (without byte order mark).</returns>
    private static byte[] ToVariantBytes(string text, Variant variant)
    {
        // Normalize first so the result does not depend on the file's own style
        var lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var converted = variant switch
        {
            Variant.Lf => lf,
            Variant.CrLf => lf.Replace("\n", "\r\n", StringComparison.Ordinal),
            _ => MixEndings(lf)
        };
        return new UTF8Encoding(false).GetBytes(converted);
    }

    /// <summary>
    ///     Makes every second line terminator a CRLF.
    /// </summary>
    /// <param name="lf">LF-only text.</param>
    /// <returns>Text with alternating terminators.</returns>
    private static string MixEndings(string lf)
    {
        var builder = new StringBuilder(lf.Length + (lf.Length / 20));
        var count = 0;
        foreach (var c in lf)
        {
            if (c == '\n' && count++ % 2 == 1)
            {
                builder.Append('\r');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses every repository Markdown file in a line-ending variant and collects tiling violations.
    /// </summary>
    /// <param name="variant">The variant.</param>
    /// <returns>The number of files checked and all violations.</returns>
    private static (int Files, List<string> Violations) CheckRepository(Variant variant)
    {
        var violations = new List<string>();
        var files = RepositoryMarkdownFiles.Enumerate();
        var root = RepositoryMarkdownFiles.FindRoot();
        foreach (var file in files)
        {
            var bytes = ToVariantBytes(File.ReadAllText(file), variant);
            var relative = Path.GetRelativePath(root, file);
            var document = DocumentParser.Parse(SourceDocument.Create(relative, bytes));
            violations.AddRange(SpanTilingAssertions.Check(document));
        }

        return (files.Count, violations);
    }

    /// <summary>
    ///     Summarizes the block structure of a document so two parses can be compared.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>Kind, span and depth of every block.</returns>
    private static List<(BlockKind Kind, int Start, int End, int Depth)> Structure(ParsedDocument document) =>
        [.. document.Blocks.Select(b => (b.Kind, b.Start, b.EndInclusive, b.Depth))];

    /// <summary>
    ///     Test that the repository's Markdown files tile correctly with LF line endings.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SpanTiling_RepositoryMarkdownLf_HasNoViolations()
    {
        // Arrange / Act: parse every repository Markdown file with LF endings
        var (files, violations) = CheckRepository(Variant.Lf);

        // Assert: the corpus is non-trivial and every span property holds
        Assert.True(files >= 20, $"expected a real corpus, found {files} files");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that the repository's Markdown files tile correctly with CRLF line endings.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SpanTiling_RepositoryMarkdownCrLf_HasNoViolations()
    {
        // Arrange / Act: parse every repository Markdown file with CRLF endings
        var (files, violations) = CheckRepository(Variant.CrLf);

        // Assert: the corpus is non-trivial and every span property holds
        Assert.True(files >= 20, $"expected a real corpus, found {files} files");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that the repository's Markdown files tile correctly with mixed line endings.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SpanTiling_RepositoryMarkdownMixed_HasNoViolations()
    {
        // Arrange / Act: parse every repository Markdown file with alternating endings
        var (files, violations) = CheckRepository(Variant.Mixed);

        // Assert: the corpus is non-trivial and every span property holds
        Assert.True(files >= 20, $"expected a real corpus, found {files} files");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that all line-ending variants of every corpus file produce identical text and blocks, and that writing back restores the bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_LineEndingVariants_RepositoryMarkdown_IdenticalBlocksAndRoundTrip()
    {
        // Arrange: every repository Markdown file
        var differences = new List<string>();
        foreach (var file in RepositoryMarkdownFiles.Enumerate())
        {
            // Act: build the three variants and parse each
            var text = File.ReadAllText(file);
            var lfBytes = ToVariantBytes(text, Variant.Lf);
            var crlfBytes = ToVariantBytes(text, Variant.CrLf);
            var lf = DocumentParser.Parse(SourceDocument.Create(file, lfBytes));
            var crlf = DocumentParser.Parse(SourceDocument.Create(file, crlfBytes));
            var mixed = DocumentParser.Parse(SourceDocument.Create(file, ToVariantBytes(text, Variant.Mixed)));

            // Assert (collected): same normalized text and block structure; LF and CRLF round-trip byte for byte
            if (lf.Source.Text != crlf.Source.Text || lf.Source.Text != mixed.Source.Text
                || !Structure(lf).SequenceEqual(Structure(crlf)) || !Structure(lf).SequenceEqual(Structure(mixed)))
            {
                differences.Add($"{file}: variants differ");
            }

            if (!lf.Source.ToBytes(lf.Source.Text, lf.Source.MajorityLineEnding).SequenceEqual(lfBytes)
                || !crlf.Source.ToBytes(crlf.Source.Text, crlf.Source.MajorityLineEnding).SequenceEqual(crlfBytes))
            {
                differences.Add($"{file}: round trip differs");
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences.Take(25)));
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
        var fromLf = DocumentParser.Parse(SourceDocument.Create("a.md", ToVariantBytes(text, Variant.Lf)));
        var fromCrLf = DocumentParser.Parse(SourceDocument.Create("a.md", ToVariantBytes(text, Variant.CrLf)));

        // Assert: identical normalized text and identical block lists
        Assert.Equal(fromLf.Source.Text, fromCrLf.Source.Text);
        Assert.Equal(Structure(fromLf), Structure(fromCrLf));
        Assert.Equal(
            [BlockKind.Paragraph, BlockKind.List, BlockKind.ListItem, BlockKind.Paragraph],
            fromCrLf.Blocks.Select(b => b.Kind).ToList());
    }

    /// <summary>
    ///     Test that parsing the whole corpus in parallel gives the same blocks as parsing sequentially.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_ConcurrentParse_RepositoryMarkdown_MatchesSequential()
    {
        // Arrange: the corpus bytes
        var files = RepositoryMarkdownFiles.Enumerate();
        var bytes = files.Select(File.ReadAllBytes).ToList();
        var sequential = bytes.Select((b, i) => Structure(DocumentParser.Parse(SourceDocument.Create(files[i], b)))).ToList();

        // Act: parse concurrently, several rounds to give races a chance to show
        var parallel = new List<(BlockKind Kind, int Start, int End, int Depth)>[bytes.Count];
        for (var round = 0; round < 3; round++)
        {
            Parallel.For(0, bytes.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
                parallel[i] = Structure(DocumentParser.Parse(SourceDocument.Create(files[i], bytes[i]))));

            // Assert: identical to the sequential result
            for (var i = 0; i < bytes.Count; i++)
            {
                Assert.True(sequential[i].SequenceEqual(parallel[i]), $"{files[i]} differs in round {round}");
            }
        }
    }

    /// <summary>
    ///     Test that every synthetic container-nesting fixture tiles correctly (LF).
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticNesting_ContainerChainsLf_HaveNoSpanViolations()
    {
        // Arrange / Act: parse all fixtures and collect violations
        var violations = new List<string>();
        var count = 0;
        foreach (var fixture in SyntheticDocuments.Generate())
        {
            count++;
            var document = DocumentParser.Parse(SourceDocument.FromText(fixture.Name, fixture.Text));
            violations.AddRange(SpanTilingAssertions.Check(document));
        }

        // Assert: a large fixture set, no violations
        Assert.True(count >= 3000, $"expected thousands of fixtures, found {count}");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that every synthetic container-nesting fixture tiles correctly when given as CRLF bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticNesting_ContainerChainsCrLf_HaveNoSpanViolations()
    {
        // Arrange / Act: parse all fixtures from CRLF bytes and collect violations
        var violations = new List<string>();
        foreach (var fixture in SyntheticDocuments.Generate())
        {
            var bytes = ToVariantBytes(fixture.Text, Variant.CrLf);
            var document = DocumentParser.Parse(SourceDocument.Create(fixture.Name, bytes));
            violations.AddRange(SpanTilingAssertions.Check(document));
        }

        // Assert: no violations
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that every synthetic top-level footnote and definition document tiles correctly (LF).
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticDefinitionsAndFootnotes_Lf_HaveNoSpanViolations()
    {
        // Arrange / Act: parse all footnote and definition fixtures and collect violations
        var violations = new List<string>();
        foreach (var fixture in SyntheticDocuments.GenerateFootnoteDocuments())
        {
            var document = DocumentParser.Parse(SourceDocument.FromText(fixture.Name, fixture.Text));
            violations.AddRange(SpanTilingAssertions.Check(document));
        }

        // Assert: no violations
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Test that every synthetic top-level footnote and definition document tiles correctly when given as CRLF bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticDefinitionsAndFootnotes_CrLf_HaveNoSpanViolations()
    {
        // Arrange / Act: parse all footnote and definition fixtures from CRLF bytes and collect violations
        var violations = new List<string>();
        foreach (var fixture in SyntheticDocuments.GenerateFootnoteDocuments())
        {
            var document = DocumentParser.Parse(SourceDocument.Create(fixture.Name, ToVariantBytes(fixture.Text, Variant.CrLf)));
            violations.AddRange(SpanTilingAssertions.Check(document));
        }

        // Assert: no violations
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Take(25)));
    }

    /// <summary>
    ///     Escapes control characters of a generated document for a readable failure message.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <returns>The escaped text.</returns>
    private static string Escape(string text) =>
        text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    /// <summary>
    ///     Parses the random documents and collects nesting violations.
    /// </summary>
    /// <param name="variant">The line-ending variant the documents are parsed in.</param>
    /// <returns>The number of documents checked and all violations, each followed by the escaped document.</returns>
    private static (int Documents, List<string> Violations) CheckRandomDocuments(Variant variant)
    {
        var violations = new List<string>();
        var count = 0;
        foreach (var fixture in RandomDocuments.Generate(RandomSeed, RandomDocumentsPerFamily))
        {
            count++;
            var document = DocumentParser.Parse(SourceDocument.Create(fixture.Name, ToVariantBytes(fixture.Text, variant)));
            violations.AddRange(SpanTilingAssertions.CheckNesting(document).Select(v => $"{v} in \"{Escape(fixture.Text)}\""));
        }

        return (count, violations);
    }

    /// <summary>
    ///     Test that the blocks of randomly generated adversarial documents nest without overlap (LF).
    /// </summary>
    [Fact]
    public void DocumentSubsystem_RandomDocuments_Lf_BlocksNestWithoutOverlap()
    {
        // Arrange / Act: parse the generated documents with LF line endings and collect nesting violations
        var (documents, violations) = CheckRandomDocuments(Variant.Lf);

        // Assert: every document was checked and no sibling overlap or parent violation was found
        Assert.Equal(RandomDocuments.FamilyCount * RandomDocumentsPerFamily, documents);
        Assert.True(violations.Count == 0, $"{violations.Count} violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations.Take(10))}");
    }

    /// <summary>
    ///     Test that the blocks of randomly generated adversarial documents nest without overlap when given as CRLF bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_RandomDocuments_CrLf_BlocksNestWithoutOverlap()
    {
        // Arrange / Act: parse the generated documents from CRLF bytes and collect nesting violations
        var (documents, violations) = CheckRandomDocuments(Variant.CrLf);

        // Assert: every document was checked and no sibling overlap or parent violation was found
        Assert.Equal(RandomDocuments.FamilyCount * RandomDocumentsPerFamily, documents);
        Assert.True(violations.Count == 0, $"{violations.Count} violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations.Take(10))}");
    }

    /// <summary>
    ///     Parses the random documents that hold valid grid tables and collects nesting violations and lost grid tables.
    /// </summary>
    /// <param name="variant">The line-ending variant the documents are parsed in.</param>
    /// <returns>The number of documents checked and all problems, each followed by the escaped document.</returns>
    private static (int Documents, List<string> Problems) CheckValidGridTableDocuments(Variant variant)
    {
        var problems = new List<string>();
        var count = 0;
        foreach (var (fixture, tables) in RandomDocuments.GenerateWithValidGridTables(RandomSeed, RandomDocumentsPerFamily))
        {
            count++;
            var document = DocumentParser.Parse(SourceDocument.Create(fixture.Name, ToVariantBytes(fixture.Text, variant)));
            problems.AddRange(SpanTilingAssertions.CheckNesting(document).Select(v => $"{v} in \"{Escape(fixture.Text)}\""));

            // Every table is valid, so none may be lost to the fallback that reads a document without grid tables
            var actual = document.Blocks.Count(b => b.Kind == BlockKind.GridTable);
            if (actual != tables)
            {
                problems.Add($"{fixture.Name}: {actual} grid tables instead of {tables} in \"{Escape(fixture.Text)}\"");
            }
        }

        return (count, problems);
    }

    /// <summary>
    ///     Test that valid grid tables next to dropped footnotes that hold definitions stay grid tables and the blocks nest (LF).
    /// </summary>
    [Fact]
    public void DocumentSubsystem_RandomDocumentsWithValidGridTables_Lf_KeepGridTablesAndNest()
    {
        // Arrange / Act: parse the generated documents with LF line endings
        var (documents, problems) = CheckValidGridTableDocuments(Variant.Lf);

        // Assert: every document was checked, every grid table was kept and the blocks nest
        Assert.Equal(RandomDocumentsPerFamily, documents);
        Assert.True(problems.Count == 0, $"{problems.Count} problems:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Take(10))}");
    }

    /// <summary>
    ///     Test that valid grid tables next to dropped footnotes that hold definitions stay grid tables and the blocks nest when given as CRLF bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_RandomDocumentsWithValidGridTables_CrLf_KeepGridTablesAndNest()
    {
        // Arrange / Act: parse the generated documents from CRLF bytes
        var (documents, problems) = CheckValidGridTableDocuments(Variant.CrLf);

        // Assert: every document was checked, every grid table was kept and the blocks nest
        Assert.Equal(RandomDocumentsPerFamily, documents);
        Assert.True(problems.Count == 0, $"{problems.Count} problems:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Take(10))}");
    }

    /// <summary>
    ///     Test that a link reference definition inside any container chain reports the depth and enclosing blocks of that chain.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticNesting_DefinitionDepthAndParent_MatchContainerChain()
    {
        // Arrange: the fixtures whose body is a single definition, and the expected container kinds per chain letter
        var problems = new List<string>();
        var count = 0;
        foreach (var fixture in SyntheticDocuments.Generate().Where(f => f.Name.Contains(":definition:", StringComparison.Ordinal) || f.Name.EndsWith(":definition", StringComparison.Ordinal)))
        {
            // Act: parse and find the definition
            count++;
            var document = DocumentParser.Parse(SourceDocument.FromText(fixture.Name, fixture.Text));
            var definition = document.Blocks.SingleOrDefault(b => b.Kind == BlockKind.LinkReferenceDefinition);
            if (definition == null)
            {
                problems.Add($"{fixture.Name}: expected exactly one definition");
                continue;
            }

            // Assert (collected): depth equals the number of enclosing containers, the parent chain reaches the top
            var ancestors = new List<BlockKind>();
            var byNode = new Dictionary<Block, BlockInfo>(ReferenceEqualityComparer.Instance);
            foreach (var block in document.Blocks.Where(b => b.Node != null))
            {
                byNode[block.Node!] = block;
            }
            for (var parent = definition.Parent; parent != null; parent = byNode[parent].Parent)
            {
                ancestors.Add(byNode[parent].Kind);
            }

            ancestors.Reverse();
            var expected = fixture.Chain.SelectMany(c => c == 'Q' ? new[] { BlockKind.BlockQuote } : [BlockKind.List, BlockKind.ListItem]).ToList();
            if (definition.Depth != SyntheticDocuments.BodyDepth(fixture.Chain) || !ancestors.SequenceEqual(expected))
            {
                problems.Add($"{fixture.Name}: depth {definition.Depth}, ancestors [{string.Join(",", ancestors)}], expected [{string.Join(",", expected)}]");
            }
        }

        Assert.True(count > 100, $"expected many fixtures, found {count}");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(25)));
    }

    /// <summary>
    ///     Test that paragraph lines of nested fixtures are rebuilt exactly and lazy lines are flagged exactly where the fixture is lazy.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_SyntheticNesting_ParagraphLines_ContentAndLazinessMatchFixture()
    {
        // Arrange: only fixtures whose body is one multi-line paragraph
        var problems = new List<string>();
        foreach (var fixture in SyntheticDocuments.Generate().Where(f => f.ParagraphLines != null))
        {
            // Act: find the paragraph holding the body and rebuild its lines
            var document = DocumentParser.Parse(SourceDocument.FromText(fixture.Name, fixture.Text));
            var body = fixture.ParagraphLines!;
            var paragraph = document.Blocks.FirstOrDefault(b => b.Kind == BlockKind.Paragraph && ContentOf(document, b, 0) == body[0]);
            if (paragraph == null)
            {
                problems.Add($"{fixture.Name}: body paragraph not found");
                continue;
            }

            var lines = DocumentParser.GetLines(document, paragraph);
            var contents = lines.Select((_, i) => ContentOf(document, paragraph, i)).ToList();

            // Assert (collected). Tab-indented containers depend on tab stops: a tab can leave too little
            // indentation for an inner item (a genuinely lazy line) or turn a quote marker into literal text,
            // so for those chains only the end of each line is compared and laziness is not asserted.
            if (fixture.Chain.Contains('T', StringComparison.Ordinal))
            {
                if (contents.Count > body.Count || contents.Where((c, i) => !c!.EndsWith(body[i], StringComparison.Ordinal)).Any())
                {
                    problems.Add($"{fixture.Name}: content [{string.Join("|", contents)}]");
                }

                continue;
            }

            // Exact content; continuation lines lazy iff the fixture is lazy
            if (!contents.SequenceEqual(body))
            {
                problems.Add($"{fixture.Name}: content [{string.Join("|", contents)}]");
            }

            if (!lines.Skip(1).All(l => l.IsLazy == fixture.Lazy) || lines[0].IsLazy)
            {
                problems.Add($"{fixture.Name}: lazy flags [{string.Join(",", lines.Select(l => l.IsLazy))}]");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(25)));
    }

    /// <summary>
    ///     Gets the content text of one line of a leaf block.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="leaf">The leaf block.</param>
    /// <param name="index">Zero-based line index.</param>
    /// <returns>The content text, or null when the block has fewer lines.</returns>
    private static string? ContentOf(ParsedDocument document, BlockInfo leaf, int index)
    {
        var lines = DocumentParser.GetLines(document, leaf);
        return index < lines.Count ? document.Source.Text[lines[index].ContentStart..lines[index].ContentEnd] : null;
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
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal))];

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
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal))];

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
}
