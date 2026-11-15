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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Verify.Cli;
using DemaConsulting.LintMark.Verify.Options;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Tests for the command line parsing (VerifyOptions), the option sets and the option matrix.
/// </summary>
[Collection("ConfigLoaderTemporaryDirectory")]
public class OptionTests
{
    /// <summary>
    ///     Test that every option of the command line is parsed.
    /// </summary>
    [Fact]
    public void VerifyOptions_Parse_AllOptions_AreRead()
    {
        // Arrange / Act
        var options = VerifyOptions.Parse(
        [
            "docs", "**/*.md", "!docs/old/**", "--config", "c.yaml", "--option", "wrap=semantic", "--option", "link-refactor.enabled=true",
            "--report", "r.md", "--matrix", "--dema", "--dema-root", "D:\\repos", "--sample", "50", "--jobs", "3", "--timeout", "9", "--max-diff-lines", "12"
        ]);

        // Assert
        Assert.Equal(["docs", "**/*.md", "!docs/old/**"], options.Paths);
        Assert.Equal("c.yaml", options.ConfigFile);
        Assert.Equal(
            [new KeyValuePair<string, string>("wrap", "semantic"), new KeyValuePair<string, string>("link-refactor.enabled", "true")],
            options.Overrides);
        Assert.Equal("r.md", options.ReportPath);
        Assert.True(options.Matrix);
        Assert.True(options.Dema);
        Assert.Equal("D:\\repos", options.DemaRoot);
        Assert.Equal(50, options.Sample);
        Assert.Equal(3, options.Jobs);
        Assert.Equal(9, options.TimeoutSeconds);
        Assert.Equal(12, options.MaxDiffLines);
    }

    /// <summary>
    ///     Test that invalid command lines are rejected with a usage error.
    /// </summary>
    /// <param name="argument">The arguments, separated by a vertical bar.</param>
    [Theory]
    [InlineData("")]
    [InlineData("docs|--bogus")]
    [InlineData("docs|--config")]
    [InlineData("docs|--option|missing-equals-sign")]
    [InlineData("docs|--jobs|0")]
    [InlineData("docs|--sample|many")]
    [InlineData("docs|--repo-config|--matrix")]
    [InlineData("docs|--repo-config|--option|wrap=fill")]
    public void VerifyOptions_Parse_InvalidArguments_ThrowUsageException(string argument)
    {
        // Arrange
        var args = argument.Length == 0 ? [] : argument.Split('|');

        // Act / Assert
        Assert.Throws<UsageException>(() => VerifyOptions.Parse(args));
    }

    /// <summary>
    ///     Test that the help option needs no paths.
    /// </summary>
    [Fact]
    public void VerifyOptions_Parse_Help_NeedsNoPaths()
    {
        Assert.True(VerifyOptions.Parse(["--help"]).Help);
    }

    /// <summary>
    ///     Test that <c>--dema</c> alone selects something to verify.
    /// </summary>
    [Fact]
    public void VerifyOptions_Parse_DemaOnly_IsValid()
    {
        Assert.True(VerifyOptions.Parse(["--dema"]).Dema);
    }

    /// <summary>
    ///     Test that the matrix has 36 distinct sets that each parse as a real configuration.
    /// </summary>
    [Fact]
    public void OptionMatrix_Create_Matrix_Has36DistinctValidSets()
    {
        // Arrange / Act
        var sets = OptionMatrix.Create(VerifyOptions.Parse(["docs", "--matrix"]));

        // Assert: 3 x 3 x 2 x 2 distinct labels, and each configuration loads without a problem
        Assert.Equal(36, sets.Count);
        Assert.Equal(36, sets.Select(set => set.Label).Distinct().Count());
        Assert.All(sets, set => Assert.NotNull(set.CreateLoader()));
    }

    /// <summary>
    ///     Test that the matrix values reach the configuration, and replace the same keys from the command line.
    /// </summary>
    [Fact]
    public void OptionMatrix_Create_MatrixWithOverride_MatrixValueWins()
    {
        // Arrange: an override of a key the matrix sets, and one it does not
        var sets = OptionMatrix.Create(VerifyOptions.Parse(["docs", "--matrix", "--option", "wrap=fill", "--option", "link-refactor.threshold=33"]));

        // Act: find the semantic, 40 wide, aligned set with links on
        var set = Assert.Single(sets, s => s.Label == "wrap=semantic width=40 tables=aligned links=on");
        var config = set.CreateLoader().Load("a.md").Config!;

        // Assert
        Assert.Equal(WrapMode.Semantic, config.Wrap);
        Assert.Equal(40, config.LineLength);
        Assert.Equal(TableStyle.Aligned, config.Tables);
        Assert.True(config.LinkRefactor.Enabled);
        Assert.Equal(33, config.LinkRefactor.Threshold);
    }

    /// <summary>
    ///     Test that the settings are laid over a base configuration file.
    /// </summary>
    [Fact]
    public void OptionSet_CreateLoader_BaseConfigAndOptions_AreMerged()
    {
        // Arrange: a base file with a line length and a nested setting
        using var directory = new TemporaryDirectory();
        var baseFile = directory.WriteFile("base.yaml", "line-length: 100\nlink-refactor:\n  threshold: 50\n");
        var options = VerifyOptions.Parse(["docs", "--config", baseFile, "--option", "wrap=sentence", "--option", "link-refactor.enabled=true"]);
        var set = Assert.Single(OptionMatrix.Create(options));

        // Act
        var config = set.CreateLoader().Load("a.md").Config!;

        // Assert: the base values stay, the options are added
        Assert.Equal(100, config.LineLength);
        Assert.Equal(WrapMode.Sentence, config.Wrap);
        Assert.Equal(50, config.LinkRefactor.Threshold);
        Assert.True(config.LinkRefactor.Enabled);
    }

    /// <summary>
    ///     Test that an invalid setting or an unreadable base file is reported.
    /// </summary>
    [Fact]
    public void OptionSet_CreateLoader_InvalidSettings_Throw()
    {
        // Arrange / Act / Assert: an unknown key and a missing base file
        Assert.Throws<InvalidOperationException>(() => new OptionSet("bad", [new("bogus-setting", "1")]).CreateLoader());
        Assert.Throws<InvalidOperationException>(() => new OptionSet("missing", [], "does-not-exist.yaml").CreateLoader());
    }

    /// <summary>
    ///     Test that creating a loader leaves no temporary files behind. The classes that create loaders share a collection, so
    ///     that no other test creates a directory while this one looks.
    /// </summary>
    [Fact]
    public void OptionSet_CreateLoader_LeavesNoTemporaryDirectory()
    {
        // Arrange
        var before = Directory.GetDirectories(Path.GetTempPath(), "lintmark-verify-????????????????????????????????");

        // Act
        _ = new OptionSet("x", [new("line-length", "90")]).CreateLoader();

        // Assert: no directory exists now that did not exist before (a directory that another test removed meanwhile does not matter)
        var created = Directory.GetDirectories(Path.GetTempPath(), "lintmark-verify-????????????????????????????????").Except(before).ToList();
        Assert.Empty(created);
    }
}
