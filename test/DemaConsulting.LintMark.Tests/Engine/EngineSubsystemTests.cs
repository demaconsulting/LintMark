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
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests of the Engine subsystem: file discovery, the per-file pipeline, the atomic writer and the
///     reporters are driven from the engine's boundary, with real files and no substitutes.
/// </summary>
[Collection("Sequential")]
public class EngineSubsystemTests
{
    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Test that patterns, directories, exclusions and explicit files select the files to process, each once and in path order.
    /// </summary>
    [Fact]
    public void EngineSubsystem_Discovery_PatternsExclusionsAndExplicitFiles_SelectTheFilesToProcess()
    {
        // Arrange: Markdown files in the root, a folder and node_modules, and a text file
        using var directory = new TemporaryDirectory();
        directory.WriteFile("README.md");
        directory.WriteFile("notes.txt");
        directory.WriteFile("docs/guide.md");
        directory.WriteFile("docs/deep/more.md");
        directory.WriteFile("node_modules/pkg/readme.md");

        // Act: select with a glob and an exclusion, with a file and its directory, and with an explicit file in node_modules
        var byGlob = FileDiscovery.Discover(["**/*.md", "!docs/deep/**"], directory.FullPath);
        var byBoth = FileDiscovery.Discover(["docs/guide.md", "docs"], directory.FullPath);
        var explicitFile = FileDiscovery.DiscoverFiles(["node_modules/pkg/readme.md"], directory.FullPath);

        // Assert: the exclusion removes a folder, node_modules is skipped, a file named twice is listed once, explicit files are marked
        Assert.Equal([directory.PathOf("README.md"), directory.PathOf("docs/guide.md")], byGlob);
        Assert.Equal([directory.PathOf("docs/deep/more.md"), directory.PathOf("docs/guide.md")], byBoth);
        var discovered = Assert.Single(explicitFile);
        Assert.Equal(directory.PathOf("node_modules/pkg/readme.md"), discovered.Path);
        Assert.True(discovered.IsExplicit);
    }

    /// <summary>
    ///     Test that many files processed in parallel give the same results, in path order, as a sequential run.
    /// </summary>
    [Fact]
    public void EngineSubsystem_Processing_ManyFilesInParallel_GiveTheSameResultsInPathOrder()
    {
        // Arrange: files with different findings, named in an order that is not the path order
        using var directory = new TemporaryDirectory();
        string[] names = ["e.md", "b.md", "d.md", "a.md", "c.md", "f.md"];
        string[] contents =
        [
            "see *text * here\n", "#NoSpace\n", "text  \n", "# T\n\n### Skip\n", "[x](#nowhere)\n", "fine\n"
        ];
        var files = names.Select((name, index) => directory.WriteFile(name, contents[index])).ToList();

        // Act: process sequentially and with several workers
        var sequential = Project(new LintMarkEngine(1).Process(files));
        var parallel = Project(new LintMarkEngine(8).Process(files));

        // Assert: the same findings in path order, with the rule that each file is expected to report
        Assert.Equal(sequential, parallel);
        Assert.Equal(["a.md", "b.md", "c.md", "d.md", "e.md", "f.md"], parallel.Select(p => Path.GetFileName(p.Path)));
        Assert.Equal(
            ["heading-increment", "heading-missing-space", "link-fragment", "trailing-whitespace", "emphasis-space", string.Empty],
            parallel.Select(p => p.Rules));
    }

    /// <summary>
    ///     Test that the fix option writes the rewrite in place without leaving a temporary file, never writes a document
    ///     that must not be rewritten, and that the writer refuses a file that changed after it was read.
    /// </summary>
    [Fact]
    public void EngineSubsystem_Writing_FixOption_RewritesAtomicallyAndNeverOverChangedOrUnsafeFiles()
    {
        // Arrange: a CRLF file, a document with an invalid grid table, and a configuration that asks for LF
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "end-of-line: lf\n");
        var rewritten = directory.WriteBytes("a.md", Bytes("# Title\r\n\r\nText.\r\n"));
        var unsafeText = Bytes("para\r\n+---+---+\r\n");
        var unsafePath = directory.WriteBytes("b.md", unsafeText);

        // Act: fix both files with the engine, then offer the writer a rewrite that was computed from other bytes
        var (exitCode, _) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(1), "--fix", "a.md", "b.md");
        var stale = Assert.Throws<IOException>(() =>
            AtomicFileWriter.Write(rewritten, AtomicFileWriter.ResolveTarget(rewritten), Bytes("something else\n"), Bytes("overwrite\n")));

        // Assert: the first file is LF, the second is untouched and reported, no temporary file remains (the log is the run's own), the stale write changed nothing
        Assert.Equal(1, exitCode);
        Assert.Equal(Bytes("# Title\n\nText.\n"), File.ReadAllBytes(rewritten));
        Assert.Equal(unsafeText, File.ReadAllBytes(unsafePath));
        Assert.Equal(
            [".lintmark.yaml", "a.md", "b.md", "report.log"],
            Directory.GetFiles(directory.FullPath).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.NotEmpty(stale.Message);
    }

    /// <summary>
    ///     Test that the check option reports findings as path, line, column, rule and message in a stable order, and that
    ///     the diff option writes a unified diff of the rewrite without changing the file.
    /// </summary>
    [Fact]
    public void EngineSubsystem_Reporting_CheckAndDiffOptions_WriteFindingsInOrderAndUnifiedDiff()
    {
        // Arrange: two files named in reverse path order, and a file that has a rewrite
        using var directory = new TemporaryDirectory();
        directory.WriteFile("b.md", "see *text * here\n");
        directory.WriteFile("a.md", "#NoSpace\n");
        var changed = directory.WriteFile("c.md", "# Title #\n\ntext \n");

        // Act: check the first two files, then diff the third
        var (checkExit, checkLines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(1), "--check", "b.md", "a.md");
        var (diffExit, diffLines) = LintMarkEngineTests.Execute(directory, new LintMarkEngine(1), "--diff", "c.md");

        // Assert: the findings are in the documented form and in path order; the diff shows the change and the file is unchanged
        Assert.Equal(1, checkExit);
        Assert.Equal(
            [
                "a.md:1:1: heading-missing-space Heading has no space after its number signs, so it is a paragraph",
                "b.md:1:5: emphasis-space Emphasis has a space inside one of its delimiters; use *text* instead of *text *"
            ],
            checkLines.Where(line => line.Contains(".md:", StringComparison.Ordinal)));
        Assert.Equal(1, diffExit);
        Assert.Contains("-# Title #", diffLines);
        Assert.Contains("+# Title", diffLines);
        Assert.Contains("-text ", diffLines);
        Assert.Contains("+text", diffLines);
        Assert.Equal("# Title #\n\ntext \n", File.ReadAllText(changed));
    }

    /// <summary>
    ///     Projects results to the path and the rule identifiers of their findings.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>One entry per result.</returns>
    private static List<(string Path, string Rules)> Project(IReadOnlyList<FileResult> results) =>
        [.. results.Select(r => (r.Path, string.Join(",", r.Diagnostics.Select(d => d.RuleId))))];
}
