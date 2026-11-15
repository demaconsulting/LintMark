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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Verify.Checks;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the list, block quote and inline rules are safe and stable over generated adversarial documents: nests
///     of lists with mixed markers and indentation, block quotes with every spacing, and paragraphs full of delimiters
///     and URLs, in every line ending and with every configuration choice.
/// </summary>
public class ListQuoteInlineCorpusTests
{
    /// <summary>
    ///     The number of generated list documents per test.
    /// </summary>
    private const int ListDocuments = 4000;

    /// <summary>
    ///     The number of generated quote documents per test.
    /// </summary>
    private const int QuoteDocuments = 2500;

    /// <summary>
    ///     The number of generated inline documents per test.
    /// </summary>
    private const int InlineDocuments = 6000;

    /// <summary>
    ///     Runs a check over a set of documents and collects the failures instead of stopping at the first.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="check">The check, called once per document.</param>
    /// <returns>One line per failed document.</returns>
    private static List<string> CollectFailures(IReadOnlyList<SyntheticDocument> documents, Action<string> check) =>
        BlockFormatterAssertions.CollectFailures([.. documents.Select(static document => document.Name)], index => check(documents[index].Text));

    /// <summary>
    ///     Formats a text and asserts that the same bytes are written for every stored form of it.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertSafeInEveryLineEnding(string text, LintMarkConfig? config = null)
    {
        // The LF text is checked for everything; then the CRLF and BOM forms must give the same text and render the same on disk
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(text, config, checkParallel: false);
        var preamble = new UTF8Encoding(true).GetPreamble();
        foreach (var (onDisk, uniform) in new[] { (text.Replace("\n", "\r\n", StringComparison.Ordinal), true), (BlockFormatterAssertions.MixEndings(text), false) })
        {
            var source = SourceDocument.Create("a.md", [.. preamble, .. Encoding.UTF8.GetBytes(onDisk)]);
            Assert.Equal(text, source.Text);
            var result = BlockFormatterAssertions.ProductionFormatter().Format(DocumentParser.Parse(source), config ?? LintMarkConfig.Default);
            Assert.Equal(formatted, SourcePatcher.Apply(source, result.Edits));

            // The bytes written render like the bytes read when the line endings stay uniform; a file with mixed endings is
            // unified, which the parser's multi-line code span defect can make render differently (nothing to do with the rules)
            if (uniform)
            {
                var bytes = source.ToBytes(formatted, result.LineEnding);
                var written = Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
                var verdict = HtmlEquivalenceChecker.CheckOnDisk(onDisk, written);
                Assert.True(verdict.IsEquivalent, $"{verdict.Description}\n--- on disk before ---\n{onDisk}\n--- on disk after ---\n{written}");
            }
        }
    }

    /// <summary>
    ///     Test that nests of lists of every shape format safely and stably, and that most of them change.
    /// </summary>
    [Fact]
    public void ListQuoteInlineCorpus_Format_RandomLists_AreSafeAndStable()
    {
        // Arrange: nests with mixed markers, numbers, spacing, indentation, lazy lines, blank lines, code and quotes
        var documents = ListQuoteInlineDocuments.Lists(20260101u, ListDocuments).ToList();

        // Act & Assert: every document passes the safety assertions
        BlockFormatterAssertions.AssertNone(CollectFailures(documents, text => AssertSafeInEveryLineEnding(text)));
        Assert.True(documents.Count(document => BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), document.Text).Formatted != document.Text) > ListDocuments / 4);
    }

    /// <summary>
    ///     Test that nests of lists format safely and stably with every choice of marker, indent and numbering.
    /// </summary>
    [Theory]
    [InlineData("Asterisk", 4, "One")]
    [InlineData("Plus", 2, "Incrementing")]
    [InlineData("Dash", 4, "One")]
    public void ListQuoteInlineCorpus_Format_RandomListsWithOtherSettings_AreSafeAndStable(string bullet, int indent, string style)
    {
        // Arrange: generated nests and a configuration that changes the three list settings
        var documents = ListQuoteInlineDocuments.Lists(777u, ListDocuments / 2).ToList();
        var config = LintMarkConfig.Default with
        {
            Bullet = Enum.Parse<BulletMarker>(bullet),
            ListIndent = indent,
            OrderedStyle = Enum.Parse<OrderedStyle>(style)
        };

        // Act & Assert: every document passes the safety assertions
        BlockFormatterAssertions.AssertNone(CollectFailures(documents, text => BlockFormatterAssertions.AssertSafeAndStable(text, config, checkParallel: false)));
    }

    /// <summary>
    ///     Test that block quotes with every spacing format safely and stably.
    /// </summary>
    [Fact]
    public void ListQuoteInlineCorpus_Format_RandomQuotes_AreSafeAndStable()
    {
        // Arrange: quotes of paragraphs, headings, lists, code, tables, alerts and nested quotes with every gap after the markers
        var documents = ListQuoteInlineDocuments.Quotes(20260102u, QuoteDocuments).ToList();

        // Act & Assert: every document passes the safety assertions
        BlockFormatterAssertions.AssertNone(CollectFailures(documents, text => AssertSafeInEveryLineEnding(text)));
        Assert.True(documents.Count(document => BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), document.Text).Formatted != document.Text) > QuoteDocuments / 4);
    }

    /// <summary>
    ///     Test that paragraphs of emphasis, strong emphasis and URLs format safely and stably, and that most of them change.
    /// </summary>
    [Fact]
    public void ListQuoteInlineCorpus_Format_RandomInlineText_AreSafeAndStable()
    {
        // Arrange: paragraphs over an alphabet of delimiters, backticks, brackets, angle brackets, URLs and punctuation
        var documents = ListQuoteInlineDocuments.Inlines(20260103u, InlineDocuments).ToList();

        // Act & Assert: no document is refused
        BlockFormatterAssertions.AssertNone(CollectFailures(documents, text => AssertSafeInEveryLineEnding(text)));
        Assert.True(documents.Count(document => BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), document.Text).Formatted != document.Text) > InlineDocuments / 10);
    }

    /// <summary>
    ///     Test that paragraphs format safely and stably when the underscore and no URL wrapping are configured.
    /// </summary>
    [Fact]
    public void ListQuoteInlineCorpus_Format_RandomInlineTextWithUnderscoreEmphasis_AreSafeAndStable()
    {
        // Arrange: the same paragraphs and a configuration that writes emphasis and strong emphasis with underscores
        var documents = ListQuoteInlineDocuments.Inlines(4242u, InlineDocuments / 2).ToList();
        var config = LintMarkConfig.Default with { Emphasis = EmphasisMarker.Underscore, Strong = EmphasisMarker.Underscore, BareUrls = BareUrlStyle.Leave };

        // Act & Assert: no document is refused
        BlockFormatterAssertions.AssertNone(CollectFailures(documents, text => BlockFormatterAssertions.AssertSafeAndStable(text, config, checkParallel: false)));
    }

    /// <summary>
    ///     Test that the inline rules alone never change how a generated paragraph reads (the guards are the safety, not a check).
    /// </summary>
    /// <param name="seed">The seed of the generated paragraphs.</param>
    [Theory]
    [InlineData(20260103u)]
    [InlineData(11u)]
    [InlineData(22u)]
    [InlineData(33u)]
    [InlineData(44u)]
    public void ListQuoteInlineCorpus_Format_RandomInlineText_RendersTheSame(uint seed)
    {
        // Arrange: a formatter with the inline normalizer only, over paragraphs full of delimiters, brackets and angle brackets
        var formatter = new DocumentFormatter([], new InlineNormalizer());
        var documents = ListQuoteInlineDocuments.Inlines(seed, InlineDocuments).ToList();

        // Act: collect the documents whose rewrite does not render the same
        var mismatches = documents
            .Where(document =>
            {
                var (_, formatted) = BlockFormatterAssertions.Apply(formatter, document.Text);
                return !HtmlEquivalenceChecker.Check(document.Text, formatted).IsEquivalent;
            })
            .Select(static document => document.Name)
            .ToList();

        // Assert: none
        BlockFormatterAssertions.AssertNone(mismatches);
    }
}
