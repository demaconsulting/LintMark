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

using System.Collections.Immutable;
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the paragraph wrap is safe, stable and deterministic over every Markdown file of this repository and over
///     generated adversarial paragraphs, at four line lengths and in every stored form of a file, and that the safety checks
///     are sensitive enough to catch a wrap that ignores the rules of the wrapper.
/// </summary>
public class ParagraphCorpusTests
{
    /// <summary>
    ///     The line lengths that every document is wrapped at.
    /// </summary>
    private static readonly int[] Widths = [120, 80, 40, 20];

    /// <summary>
    ///     The number of generated documents per test.
    /// </summary>
    private const int GeneratedDocuments = 700;

    /// <summary>
    ///     Creates a configuration with a line length.
    /// </summary>
    /// <param name="width">The line length.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Width(int width) => LintMarkConfig.Default with { LineLength = width };

    /// <summary>
    ///     Test that every Markdown file of the repository formats safely at four line lengths and is a fixed point after one run.
    /// </summary>
    [Fact]
    public void ParagraphCorpus_Format_RepositoryMarkdownAtFourWidths_AreSafeAndAFixedPointAfterOneRun()
    {
        // Arrange: the files as LF text
        var root = RepositoryMarkdownFiles.FindRoot();
        var files = RepositoryMarkdownFiles.Enumerate();
        var names = files.SelectMany(file => Widths.Select(width => $"{Path.GetRelativePath(root, file).Replace('\\', '/')} @{width}")).ToList();
        Assert.True(files.Count > 100);

        // Act & Assert: every file passes every safety assertion, which includes that formatting the result changes nothing
        var failures = BlockFormatterAssertions.CollectFailures(
            names,
            index => BlockFormatterAssertions.AssertSafeAndStable(
                File.ReadAllText(files[index / Widths.Length]).ReplaceLineEndings("\n"),
                Width(Widths[index % Widths.Length]),
                checkParallel: false));
        BlockFormatterAssertions.AssertNone(failures);
    }

    /// <summary>
    ///     Selects the documents that the rules other than the wrap format safely and stably, so that a failure of a document
    ///     is a failure of the wrap.
    /// </summary>
    /// <param name="documents">The generated documents.</param>
    /// <returns>The documents that pass every safety assertion with the line length rule switched off.</returns>
    /// <remarks>
    ///     The hostile alphabet includes constructs that the whole-file checks refuse whatever the wrap does (for example an
    ///     unterminated HTML comment makes the page comparison exact for everything after it, so any change of whitespace
    ///     after it is refused). The tool refuses such a file as a whole, which is the safe outcome; the documents are left out
    ///     of the wrap tests, and the test that uses this method asserts that they are a small part of the corpus.
    /// </remarks>
    private static List<SyntheticDocument> WhereOtherRulesAreSafe(IReadOnlyList<SyntheticDocument> documents)
    {
        var safe = new List<SyntheticDocument>();
        foreach (var document in documents)
        {
            try
            {
                BlockFormatterAssertions.AssertSafeAndStable(document.Text, Width(0), checkParallel: false);
                safe.Add(document);
            }
            catch (Xunit.Sdk.XunitException)
            {
                // The other rules alone are refused for this document, with no wrap involved
            }
        }

        return safe;
    }

    /// <summary>
    ///     Test that generated adversarial paragraphs format safely and stably at four line lengths, and that the generator reaches
    ///     both the wrap and the findings that cannot be fixed.
    /// </summary>
    [Fact]
    public void ParagraphCorpus_Format_RandomParagraphsAtFourWidths_AreSafeAndStable()
    {
        // Arrange: documents over an alphabet of every script, construct and block syntax word
        var generated = ParagraphDocuments.Generate(20260514u, GeneratedDocuments).ToList();
        var documents = WhereOtherRulesAreSafe(generated);
        Assert.True(documents.Count >= generated.Count * 9 / 10, $"Only {documents.Count} of {generated.Count} documents are safe without the wrap.");
        var names = documents.SelectMany(document => Widths.Select(width => $"{document.Name} @{width}")).ToList();

        // Act & Assert: every document passes every safety assertion
        var failures = BlockFormatterAssertions.CollectFailures(
            names,
            index => BlockFormatterAssertions.AssertSafeAndStable(documents[index / Widths.Length].Text, Width(Widths[index % Widths.Length]), checkParallel: false));
        BlockFormatterAssertions.AssertNone(failures);

        // Assert: many documents are wrapped at 40 columns and some report a line that cannot be fixed
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var results = documents.Select(document => BlockFormatterAssertions.Apply(formatter, document.Text, Width(40))).ToList();
        Assert.True(results.Count(result => result.Result.Edits.Any(edit => edit.RuleId == "line-length")) > GeneratedDocuments / 3);
        Assert.Contains(results, result => result.Result.Diagnostics.Any(diagnostic => diagnostic.Kind == DiagnosticKind.LintOnly));
    }

    /// <summary>
    ///     Test that generated paragraphs give the same text whatever the line endings and byte order mark of the file.
    /// </summary>
    [Fact]
    public void ParagraphCorpus_Format_RandomParagraphsInEveryStoredForm_GiveTheSameText()
    {
        // Arrange: documents and the forms they may be stored in
        var documents = WhereOtherRulesAreSafe(ParagraphDocuments.Generate(77u, GeneratedDocuments / 2).ToList());
        var preamble = new UTF8Encoding(true).GetPreamble();
        var formatter = BlockFormatterAssertions.ProductionFormatter();

        // Act & Assert: every form gives the text of the LF form, and the CRLF form renders the same on disk
        var failures = BlockFormatterAssertions.CollectFailures(
            [.. documents.Select(document => document.Name)],
            index =>
            {
                var text = documents[index].Text;
                var expected = BlockFormatterAssertions.Apply(formatter, text, Width(30)).Formatted;
                foreach (var onDisk in new[] { text.Replace("\n", "\r\n", StringComparison.Ordinal), BlockFormatterAssertions.MixEndings(text) })
                {
                    var source = SourceDocument.Create("a.md", [.. preamble, .. Encoding.UTF8.GetBytes(onDisk)]);
                    var result = formatter.Format(DocumentParser.Parse(source), Width(30));
                    Assert.Equal(expected, SourcePatcher.Apply(source, result.Edits));
                }
            });
        BlockFormatterAssertions.AssertNone(failures);
    }

    /// <summary>
    ///     Test that generated paragraphs with tabs inside them format safely and stably.
    /// </summary>
    [Fact]
    public void ParagraphCorpus_Format_RandomParagraphsWithTabs_AreSafeAndStable()
    {
        // Arrange: documents with a tab in the middle of every third line
        static string Tabbed(string text) =>
            string.Join('\n', text.Split('\n').Select((line, index) => index % 3 == 0 && line.Length > 4 ? line.Insert(line.Length / 2, "\t") : line));

        var documents = WhereOtherRulesAreSafe(
            [.. ParagraphDocuments.Generate(31415u, GeneratedDocuments / 2).Select(document => document with { Text = Tabbed(document.Text) })]);

        // Act & Assert: every document passes every safety assertion
        var failures = BlockFormatterAssertions.CollectFailures(
            [.. documents.Select(document => document.Name)],
            index => BlockFormatterAssertions.AssertSafeAndStable(documents[index].Text, Width(30), checkParallel: false));
        BlockFormatterAssertions.AssertNone(failures);
    }

    /// <summary>
    ///     Test that the safety checks catch a wrap that breaks lines at every blank, ignoring the block syntax guard and the
    ///     constructs that must stay whole: each such rewrite is refused because the page renders differently.
    /// </summary>
    [Theory]
    [InlineData("ant bat - cat\n", "ant bat\n- cat\n")]
    [InlineData("ant bat 1. cat\n", "ant bat\n1. cat\n")]
    [InlineData("ant bat # cat\n", "ant bat\n# cat\n")]
    [InlineData("ant bat ``` cat\n", "ant bat\n``` cat\n")]
    [InlineData("ant bat --- cat\n", "ant bat\n---\ncat\n")]
    [InlineData("ant <span a=\"b c\">d</span>\n", "ant <span a=\"b\nc\">d</span>\n")]
    [InlineData("ant [b](c \"d e\")\n", "ant [b](c \"d\ne\")\n")]
    [InlineData("ant ![b](c \"d e\")\n", "ant ![b](c \"d\ne\")\n")]
    public void ParagraphCorpus_Mutants_WrapIgnoringTheRulesOfTheWrapper_IsCaughtByTheSafetyChecks(string original, string mutant)
    {
        // Act: compare the page of the original with the page of the rewrite a faulty wrapper would produce
        var verdict = HtmlEquivalenceChecker.Check(original, mutant);

        // Assert: the check refuses it, and the real formatter does not produce it at a width that tempts it
        Assert.False(verdict.IsEquivalent);
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(original, Width(11), checkParallel: false);
        Assert.NotEqual(mutant, formatted);
    }

    /// <summary>
    ///     Test that a rebaser that does not absorb the edits of the other rules would be caught: the composition of edits in
    ///     which an edit lies inside a rewritten paragraph differs from applying the lists one after the other.
    /// </summary>
    [Fact]
    public void ParagraphCorpus_Mutants_RebaseWithoutAbsorbing_DiffersFromApplyingInSequence()
    {
        // Arrange: an edit of another rule inside the text that a wrap rewrites
        const string original = "ant _bbb_ cat\n";
        ImmutableArray<TextEdit> first = [new TextEdit(4, 1, "*", "emphasis"), new TextEdit(8, 1, "*", "emphasis")];
        ImmutableArray<TextEdit> second = [new TextEdit(0, 14, "ant *bat*\ncat\n", "line-length")];

        // Act: the correct composition, and the faulty one that keeps both lists as they are
        var source = SourceDocument.FromText("a.md", original);
        var intermediate = SourcePatcher.Apply(source, first);
        var expected = SourcePatcher.Apply(SourceDocument.FromText("a.md", intermediate), second);
        var composed = SourcePatcher.Apply(source, EditRebaser.Rebase(first, second));

        // Assert: the correct composition equals the sequence, and the faulty one cannot even be applied (the edits conflict)
        Assert.Equal(expected, composed);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var set = new EditSet();
            foreach (var edit in first.Concat(second))
            {
                set.Add(edit);
            }
        });
    }
}
