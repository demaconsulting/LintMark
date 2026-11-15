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

using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Verify.Cli;
using DemaConsulting.LintMark.Verify.Verification;

namespace DemaConsulting.LintMark.Verify.Tests.Verification;

/// <summary>
///     Tests for the grouping of results by corpus category and source: the derivation from the path, and the
///     summary and report of a run over a corpus.
/// </summary>
public class CorpusGroupTests
{
    /// <summary>
    ///     Test that the category and source come from the folders below the corpus folder, with either separator.
    /// </summary>
    [Theory]
    [InlineData("/w/repo/.corpus/spec/alpha/examples/001-tabs.md", "spec", "spec/alpha")]
    [InlineData(@"C:\w\repo\.corpus\fixture\gamma\owner__repo\examples\1.md", "fixture", "fixture/gamma")]
    [InlineData("/w/.CORPUS/sample/readmes/files/README.md", "sample", "sample/readmes")]
    [InlineData("/w/.corpus/spec/loose.md", "spec", "spec")]
    [InlineData("/w/.corpus/loose.md", "other", "other")]
    [InlineData("/w/docs/a.md", "other", "other")]
    public void CorpusGroup_Of_Path_DerivesCategoryAndSource(string path, string category, string source)
    {
        // Act
        var group = CorpusGroup.Of(path, null);

        // Assert
        Assert.Equal(category, group.Category);
        Assert.Equal(source, group.Source);
    }

    /// <summary>
    ///     Test that files below the DEMA root are grouped as dema, and only when the root is given.
    /// </summary>
    [Fact]
    public void CorpusGroup_Of_BelowDemaRoot_IsDema()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DemaRootForGroups"));
        var inside = Path.Combine(root, "BuildTools", "LintMark", "README.md");

        // Act
        var with = CorpusGroup.Of(inside, root);
        var without = CorpusGroup.Of(inside, null);
        var outside = CorpusGroup.Of(Path.Combine(Path.GetTempPath(), "Elsewhere", "a.md"), root);

        // Assert
        Assert.Equal(new CorpusGroup("dema", "dema"), with);
        Assert.Equal(new CorpusGroup("other", "other"), without);
        Assert.Equal(new CorpusGroup("other", "other"), outside);
    }

    /// <summary>
    ///     Test that the category summaries add up the sources of a category.
    /// </summary>
    [Fact]
    public void GroupSummary_ByCategory_AddsUpSources()
    {
        // Arrange
        var sources = new[]
        {
            new GroupSummary(new CorpusGroup("spec", "spec/a"), 2, new Dictionary<OutcomeKind, int> { [OutcomeKind.Ok] = 2 }),
            new GroupSummary(new CorpusGroup("spec", "spec/b"), 3, new Dictionary<OutcomeKind, int> { [OutcomeKind.Ok] = 1, [OutcomeKind.NotIdempotent] = 2 }),
            new GroupSummary(new CorpusGroup("fixture", "fixture/c"), 1, new Dictionary<OutcomeKind, int> { [OutcomeKind.ExemptIgnored] = 1 })
        };

        // Act
        var categories = GroupSummary.ByCategory(sources);

        // Assert: ordinal order, summed files and counts
        Assert.Equal(["fixture", "spec"], categories.Select(c => c.Group.Source));
        var spec = categories[1];
        Assert.Equal(5, spec.Files);
        Assert.Equal(5, spec.Total);
        Assert.Equal(3, spec.PassCount);
        Assert.Equal(2, spec.FailureCount);
        Assert.Equal([OutcomeKind.NotIdempotent], spec.FailuresByKind.Select(pair => pair.Key));
    }

    /// <summary>
    ///     Test that a run over a corpus groups the console summary and the report by category and source, and
    ///     leaves the files as they were.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_Corpus_GroupsByCategoryAndSource()
    {
        // Arrange: two spec sources and a fixture source; only the files with a hard break fail
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".corpus/spec/alpha/examples/1.md", "a  \nb\n");
        directory.WriteFile(".corpus/spec/alpha/examples/2.md", "# Title\n");
        directory.WriteFile(".corpus/spec/beta/files/3.md", "# Title\n");
        directory.WriteFile(".corpus/fixture/gamma/files/4.md", "c  \nd\n");
        directory.WriteFile(".corpus/.clones/alpha/skipped.md", "# Not verified\n");
        var verifier = new FileVerifier(new DocumentFormatter([new HardBreakDroppingFormatter()]));
        var reportPath = directory.PathOf("out/report.md");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var output = new StringWriter();

        // Act
        var exitCode = await VerifyApplication.RunAsync(
            VerifyOptions.Parse([directory.PathOf(".corpus"), "--report", reportPath]),
            output,
            new StringWriter(),
            directory.FullPath,
            verifier);
        var summary = output.ToString();
        var report = await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken);

        // Assert: the console summary
        Assert.Equal(VerifyApplication.Failed, exitCode);
        Assert.Contains("Verified 4 files", summary, StringComparison.Ordinal);
        Assert.Contains("By category (files, checks passed, checks failed; failures by type):", summary, StringComparison.Ordinal);
        Assert.Matches(@"\n  fixture\s+1 files\s+0 passed\s+1 failed  \(html-mismatch 1\)", summary);
        Assert.Matches(@"\n  spec\s+3 files\s+2 passed\s+1 failed  \(html-mismatch 1\)", summary);
        Assert.Matches(@"\n  spec/alpha\s+2 files\s+1 passed\s+1 failed  \(html-mismatch 1\)", summary);
        Assert.Matches(@"\n  spec/beta\s+1 files\s+1 passed\s+0 failed\r?\n", summary);

        // Assert: the report
        Assert.Contains("\n## Results by category\n", report, StringComparison.Ordinal);
        Assert.Contains("\n| fixture | 1 | 1 | 0 | 1 | html-mismatch 1 |\n", report, StringComparison.Ordinal);
        Assert.Contains("\n| spec | 3 | 3 | 2 | 1 | html-mismatch 1 |\n", report, StringComparison.Ordinal);
        Assert.Contains("\n## Results by source\n", report, StringComparison.Ordinal);
        Assert.Contains("\n| spec/beta | 1 | 1 | 1 | 0 |  |\n", report, StringComparison.Ordinal);

        // Assert: the inputs are intact
        Assert.Equal("a  \nb\n", await File.ReadAllTextAsync(directory.PathOf(".corpus/spec/alpha/examples/1.md"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that the DEMA repositories are grouped as dema, and that a run over one ordinary directory has no grouping.
    /// </summary>
    [Fact]
    public async Task VerifyApplication_RunAsync_DemaAndPlainDirectory_GroupsOnlyWhenUseful()
    {
        // Arrange
        using var dema = new TemporaryDirectory();
        dema.WriteFile("repo/.git/HEAD", "ref");
        dema.WriteFile("repo/README.md", "# Title\n");
        using var plain = new TemporaryDirectory();
        plain.WriteFile("a.md", "# Title\n");
        var demaOutput = new StringWriter();
        var plainOutput = new StringWriter();

        // Act
        await VerifyApplication.RunAsync(VerifyOptions.Parse(["--dema", "--dema-root", dema.FullPath]), demaOutput, new StringWriter(), dema.FullPath);
        await VerifyApplication.RunAsync(VerifyOptions.Parse([plain.FullPath]), plainOutput, new StringWriter(), plain.FullPath);

        // Assert
        Assert.Matches(@"\n  dema\s+1 files\s+1 passed\s+0 failed\r?\n", demaOutput.ToString());
        Assert.DoesNotContain("By category", plainOutput.ToString(), StringComparison.Ordinal);
    }
}
