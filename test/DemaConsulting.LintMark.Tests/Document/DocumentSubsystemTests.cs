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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests for the Document subsystem: source loading, parsing, suppression and reference
///     handling working together, verified against this repository's own Markdown files and generated nesting fixtures.
/// </summary>
public class DocumentSubsystemTests
{
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
    ///     Test the load, parse, suppress and reference flow on one document from bytes.
    /// </summary>
    [Fact]
    public void DocumentSubsystem_ParseToSuppressionAndReferences_Workflow_ProducesConsistentResults()
    {
        // Arrange: a document with a BOM and CRLF endings, a suppression region, definitions and an unreferenced footnote
        const string text =
            "# Title\n\nSee [a] and [b].\n\n<!-- lintmark-disable line-length -->\n\nINSIDE paragraph.\n\n"
            + "<!-- lintmark-enable line-length -->\n\nAFTER paragraph.\n\n[a]: http://a.example\n[b]: http://b.example \"B\"\n\n[^orphan]: Orphan.\n";
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
        Assert.Equal(["a", "b"], document.References.Definitions.Select(d => d.Label).ToList());
        Assert.Contains(document.Blocks, b => b.Kind == BlockKind.UnreferencedFootnote);
        Assert.Empty(SpanTilingAssertions.Check(document));
    }
}
