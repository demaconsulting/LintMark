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
using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the whitespace and block rules are safe, stable and (for this repository) silent over every
///     Markdown file of the repository and over the generated nesting and hostile documents.
/// </summary>
public class BlocksCorpusTests
{
    /// <summary>
    ///     The number of random documents per family; the families are described by the generator.
    /// </summary>
    private const int RandomDocumentsPerFamily = 600;

    /// <summary>
    ///     Gets the repository's Markdown files with their root, for relative names in failure messages.
    /// </summary>
    /// <returns>The root and the files.</returns>
    private static (string Root, IReadOnlyList<string> Files) Corpus() => (RepositoryMarkdownFiles.FindRoot(), RepositoryMarkdownFiles.Enumerate());

    /// <summary>
    ///     Formats bytes the way the engine does and checks that the rewrite is safe and stable, also as written on disk.
    /// </summary>
    /// <param name="bytes">The bytes of the file.</param>
    private static void AssertSafeOnDisk(byte[] bytes)
    {
        // The normalized text is checked for everything; then the bytes that would be written are checked as they are on disk
        var source = SourceDocument.Create("a.md", bytes);
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(source.Text, checkParallel: false);
        var result = BlockFormatterAssertions.ProductionFormatter().Format(DocumentParser.Parse(source), LintMarkConfig.Default);
        var skip = source.HasBom ? source.Encoding.GetPreamble().Length : 0;
        var before = Encoding.UTF8.GetString(bytes, skip, bytes.Length - skip);
        var newBytes = source.ToBytes(formatted, result.LineEnding);
        var after = Encoding.UTF8.GetString(newBytes, skip, newBytes.Length - skip);
        var verdict = HtmlEquivalenceChecker.CheckOnDisk(before, after);
        Assert.True(verdict.IsEquivalent, verdict.Description);
    }

    /// <summary>
    ///     Runs a check over a set of documents and collects the failures instead of stopping at the first.
    /// </summary>
    /// <param name="names">The names of the documents, for the failure messages.</param>
    /// <param name="check">The check, called once per document index.</param>
    /// <returns>One line per failed document.</returns>
    private static List<string> CollectFailures(IReadOnlyList<string> names, Action<int> check)
    {
        var failures = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            try
            {
                check(i);
            }
            catch (Exception ex)
            {
                failures.Add($"{names[i]}: {ex.Message.ReplaceLineEndings("\\n")}");
            }
        }

        return failures;
    }

    /// <summary>
    ///     Asserts that no document failed, showing the first failures in the message.
    /// </summary>
    /// <param name="failures">The failures collected by <see cref="CollectFailures"/>.</param>
    private static void AssertNone(List<string> failures) =>
        Assert.True(failures.Count == 0, $"{failures.Count} document(s) failed:\n{string.Join("\n", failures.Take(8))}");

    /// <summary>
    ///     Test that this repository's own Markdown files need no change.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Process_RepositoryMarkdown_ProposesNoChangeAndNoFinding()
    {
        // Arrange: every Markdown file of the repository, and an engine with the shipped formatters
        var (root, files) = Corpus();
        var engine = new LintMarkEngine(4);

        // Act: process all files (nothing is written without the fix option)
        var results = engine.Process(files);

        // Assert: every file was found, none has a finding or a pending rewrite, and nothing was refused
        Assert.True(files.Count > 100);
        var problems = results
            .Where(result => result.Diagnostics.Length > 0 || result.Change is not null)
            .Select(result => $"{Path.GetRelativePath(root, result.Path).Replace('\\', '/')}: {string.Join("; ", result.Diagnostics.Select(d => $"{d.Line}:{d.Column} {d.RuleId}"))}")
            .ToList();
        Assert.Empty(problems);
    }

    /// <summary>
    ///     Test that every Markdown file of the repository formats safely and stably as LF text.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_RepositoryMarkdownLf_IsSafeAndStable()
    {
        // Arrange: the files as LF text
        var (root, files) = Corpus();
        var names = files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToList();

        // Act & Assert: every file passes the safety assertions
        var failures = CollectFailures(names, i => AssertSafeOnDisk(Encoding.UTF8.GetBytes(File.ReadAllText(files[i]).ReplaceLineEndings("\n"))));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that every Markdown file of the repository formats safely and stably with CRLF line endings.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_RepositoryMarkdownCrlf_IsSafeAndStable()
    {
        // Arrange: the files with CRLF line endings
        var (root, files) = Corpus();
        var names = files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToList();

        // Act & Assert: every file passes the safety assertions
        var failures = CollectFailures(names, i => AssertSafeOnDisk(Encoding.UTF8.GetBytes(File.ReadAllText(files[i]).ReplaceLineEndings("\r\n"))));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that every Markdown file of the repository formats safely and stably when its line endings are mixed.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_RepositoryMarkdownMixedEndings_IsSafeAndStable()
    {
        // Arrange: the files with every second line ending changed to CRLF, and a byte order mark
        var (root, files) = Corpus();
        var names = files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToList();

        static byte[] Mix(string text)
        {
            var lines = text.ReplaceLineEndings("\n").Split('\n');
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                builder.Append(lines[i]);
                if (i < lines.Length - 1)
                {
                    builder.Append(i % 2 == 0 ? "\r\n" : "\n");
                }
            }

            return [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
        }

        // Act & Assert: every file passes the safety assertions
        var failures = CollectFailures(names, i => AssertSafeOnDisk(Mix(File.ReadAllText(files[i]))));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that the container nesting fixtures format safely and stably.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_SyntheticNestingDocuments_AreSafeAndStable()
    {
        // Arrange: every generated chain of containers around every body, and the footnote documents
        var fixtures = SyntheticDocuments.Generate().Concat(SyntheticDocuments.GenerateFootnoteDocuments()).ToList();

        // Act & Assert: every fixture passes the safety assertions
        var failures = CollectFailures(
            fixtures.Select(fixture => fixture.Name).ToList(),
            i => BlockFormatterAssertions.AssertSafeAndStable(fixtures[i].Text, checkParallel: false));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that the container nesting fixtures with trailing whitespace added to every line format safely and stably.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_SyntheticNestingDocumentsWithTrailingWhitespace_AreSafeAndStable()
    {
        // Arrange: the fixtures with two trailing spaces on every line that has content, and a missing final newline
        var fixtures = SyntheticDocuments.Generate().ToList();

        static string Dirty(string text) =>
            string.Join('\n', text.Split('\n').Select(line => line.Length > 0 ? line + "  " : line)).TrimEnd('\n');

        // Act & Assert: every fixture passes the safety assertions
        var failures = CollectFailures(
            fixtures.Select(fixture => fixture.Name).ToList(),
            i => BlockFormatterAssertions.AssertSafeAndStable(Dirty(fixtures[i].Text), checkParallel: false));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that the hostile random documents format safely and stably.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_RandomHostileDocuments_AreSafeAndStable()
    {
        // Arrange: documents of every random family
        var fixtures = RandomDocuments.Generate(8675309u, RandomDocumentsPerFamily).ToList();

        // Act & Assert: every document passes the safety assertions
        var failures = CollectFailures(
            fixtures.Select(fixture => fixture.Name).ToList(),
            i => BlockFormatterAssertions.AssertSafeAndStable(fixtures[i].Text, checkParallel: false));
        AssertNone(failures);
    }

    /// <summary>
    ///     Test that the hostile random documents with tab and trailing whitespace noise format safely and stably.
    /// </summary>
    [Fact]
    public void BlocksCorpus_Format_RandomHostileDocumentsWithNoise_AreSafeAndStable()
    {
        // Arrange: documents with a tab inside every third line and two trailing spaces on every second line
        var fixtures = RandomDocuments.Generate(1001u, RandomDocumentsPerFamily / 2).ToList();

        static string Noisy(string text) =>
            string.Join('\n', text.Split('\n').Select((line, i) => (i % 3 == 0 && line.Length > 2 ? line.Insert(2, "\t") : line) + (i % 2 == 0 ? "  " : string.Empty)));

        // Act & Assert: every document passes the safety assertions
        var failures = CollectFailures(
            fixtures.Select(fixture => fixture.Name).ToList(),
            i => BlockFormatterAssertions.AssertSafeAndStable(Noisy(fixtures[i].Text), checkParallel: false));
        AssertNone(failures);
    }
}
