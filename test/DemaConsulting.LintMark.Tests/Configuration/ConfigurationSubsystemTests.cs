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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests for the Configuration subsystem: the configuration model, the loader and the editor-settings resolver working together.
/// </summary>
[Collection("Sequential")]
public class ConfigurationSubsystemTests
{
    /// <summary>
    ///     Test that discovery picks the nearest file within the repository and never looks above the repository root.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Discovery_NestedConfigsAndRepositoryBoundary_ResolvesNearestWithinRepository()
    {
        // Arrange: an invalid file above a repository root, and inside it a root file and a nested file
        using var outer = new TemporaryDirectory(repositoryRoot: false);
        outer.WriteFile(".lintmark.yaml", "bogus: 1\n");
        Directory.CreateDirectory(outer.PathOf("repo/.git"));
        outer.WriteFile("repo/.lintmark.yaml", "line-length: 100\nwrap: sentence\n");
        outer.WriteFile("repo/docs/.lintmark.yaml", "line-length: 80\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load files at three depths
        var root = loader.Load(outer.PathOf("repo/a.md"));
        var docs = loader.Load(outer.PathOf("repo/docs/guide/b.md"));
        var elsewhere = loader.Load(outer.PathOf("repo/src/c.md"));

        // Assert: the nearest file wins, nothing is merged, and the file above the repository is never read
        Assert.Equal((100, WrapMode.Sentence), (root.Config!.LineLength, root.Config.Wrap));
        Assert.Equal((80, WrapMode.Fill), (docs.Config!.LineLength, docs.Config.Wrap));
        Assert.Same(root, elsewhere);
    }

    /// <summary>
    ///     Test that an explicit file is used instead of discovery.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_ExplicitFile_RelativePath_UsedInsteadOfDiscovery()
    {
        // Arrange: a discoverable file and an explicit file given by a relative path
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 50\n");
        directory.WriteFile("custom/c.yaml", "line-length: 150\n");
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullPath);

            // Act: load a file with an explicit loader
            var result = ConfigLoader.ForFile("custom/c.yaml").Load("a.md");

            // Assert: the explicit file is used
            Assert.Equal(150, result.Config!.LineLength);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    /// <summary>
    ///     Test that the documented defaults apply without a file, with an empty file and for omitted settings.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Defaults_NoConfigFilesAndEmptyFile_ReturnDocumentedDefaults()
    {
        // Arrange: a tree without a file, and a tree with an empty file
        using var none = new TemporaryDirectory();
        using var empty = new TemporaryDirectory();
        empty.WriteFile(".lintmark.yaml", string.Empty);

        // Act: load a file in each
        var withoutFile = ConfigLoader.ForDiscovery().Load(none.PathOf("a.md"));
        var withEmptyFile = ConfigLoader.ForDiscovery().Load(empty.PathOf("a.md"));

        // Assert: both are the documented defaults
        Assert.Equal(LintMarkConfig.Default, withoutFile.Config);
        Assert.Equal(LintMarkConfig.Default, withEmptyFile.Config);
        Assert.Equal(120, withEmptyFile.Config!.LineLength);
        Assert.Equal(WrapMode.Fill, withEmptyFile.Config.Wrap);
        Assert.Equal(TableStyle.Compact, withEmptyFile.Config.Tables);
        Assert.False(withEmptyFile.Config.LinkRefactor.Enabled);
    }

    /// <summary>
    ///     Test that the documented sample applies every setting and rule alias.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Settings_FullSampleFile_AppliesEverySettingAndRuleAlias()
    {
        // Arrange: the documented sample as a file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", ConfigLoaderParseTests.FullSample);

        // Act: load a file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a.md"));

        // Assert: every setting differs from the default and the alias was applied to the rule
        var config = result.Config!;
        Assert.Empty(result.Errors);
        Assert.Equal(100, config.LineLength);
        Assert.Equal(LineEnding.CrLf, config.EndOfLine);
        Assert.Equal(TableStyle.Aligned, config.Tables);
        Assert.True(config.LinkRefactor.Enabled);
        Assert.False(config.Rules["line-length"]);
        Assert.Equal(["docs/generated/**", "**/vendor/**"], config.Ignore);
    }

    /// <summary>
    ///     Test that an invalid file reports every problem with its position and yields no configuration.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Validation_InvalidFile_ReportsEveryProblemWithPosition()
    {
        // Arrange: a file with an unknown setting, a wrong type and an invalid value
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile(".lintmark.yaml", "color: red\nline-length: wide\nwrap: sideways\n");

        // Act: load a file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a.md"));

        // Assert: no configuration, and each problem is reported at its position in the file
        Assert.Null(result.Config);
        Assert.Equal(
            [(file, 1, 1), (file, 2, 14), (file, 3, 7)],
            result.Errors.Select(e => (e.Path, e.Line, e.Column)));
    }

    /// <summary>
    ///     Test that problems are ordered by position.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_ErrorPositions_MultipleProblems_AreOrderedByPosition()
    {
        // Arrange: a file whose problems come from different checks
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "rules:\n  nope: true\nignore:\n  - ''\nzz: 1\nbullet: no\n");

        // Act: load a file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a.md"));

        // Assert: strictly ascending positions
        Assert.Equal([2, 4, 5, 6], result.Errors.Select(e => e.Line));
    }

    /// <summary>
    ///     Test that the editor settings resolve the line ending per file with sections and a root file.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_EditorConfig_SectionsAndRoot_ResolveLineEndingPerFile()
    {
        // Arrange: a root file with sections per file type and a nested root file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[*]\nend_of_line = lf\n\n[*.md]\nend_of_line = crlf\n");
        directory.WriteFile("legacy/.editorconfig", "root = true\n\n[*.md]\nindent_style = space\n");
        var resolver = new EditorConfigResolver();

        // Act: resolve Markdown and other files in both places
        var markdown = resolver.Resolve(directory.PathOf("a.md"));
        var text = resolver.Resolve(directory.PathOf("a.txt"));
        var legacy = resolver.Resolve(directory.PathOf("legacy/a.md"));

        // Assert: per-file sections apply, and the nested root hides the outer file
        Assert.Equal(LineEnding.CrLf, markdown.EndOfLine);
        Assert.Equal(LineEnding.Lf, text.EndOfLine);
        Assert.Null(legacy.EndOfLine);
    }

    /// <summary>
    ///     Test that unsupported values and unused settings are not specified and are not errors.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_EditorConfig_UnsupportedValues_NotSpecifiedWithoutError()
    {
        // Arrange: files with cr, unset, a malformed line and unrelated settings
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[cr.md]\nend_of_line = cr\n\n[unset.md]\nend_of_line = unset\n\n[other.md]\nmax_line_length = 80\n\n[[[\n");
        var resolver = new EditorConfigResolver();

        // Act: resolve each file
        var results = new[] { "cr.md", "unset.md", "other.md", "none.md" }.Select(f => resolver.Resolve(directory.PathOf(f))).ToList();

        // Assert: nothing is specified and nothing is an error
        Assert.All(results, r => Assert.Equal(EditorConfigResult.NotSpecified, r));
    }

    /// <summary>
    ///     Test that loader and resolver return the sequential results when used by many threads.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_ConcurrentUse_ManyThreadsLoaderAndResolver_ResultsIdenticalToSequential()
    {
        // Arrange: configuration and editor settings in several folders
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n");
        directory.WriteFile("a/.editorconfig", "[*.md]\nend_of_line = crlf\n");
        directory.WriteFile(".lintmark.yaml", "line-length: 90\n");
        directory.WriteFile("b/.lintmark.yaml", "line-length: 91\n");
        directory.WriteFile("c/.lintmark.yaml", "bogus: 1\n");
        var files = Enumerable.Range(0, 48).Select(i => directory.PathOf($"{(char)('a' + (i % 4))}/f{i}.md")).ToArray();
        var expected = files.Select(f => Describe(ConfigLoader.ForDiscovery(), new EditorConfigResolver(), f)).ToArray();
        var loader = ConfigLoader.ForDiscovery();
        var resolver = new EditorConfigResolver();

        // Act: use both from many threads
        var actual = new string[files.Length * 10];
        Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index => actual[index] = Describe(loader, resolver, files[index % files.Length]));

        // Assert: identical to the sequential results
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i % files.Length], actual[i]);
        }

        return;

        static string Describe(ConfigLoader l, EditorConfigResolver r, string file)
        {
            var load = l.Load(file);
            var editor = r.Resolve(file);
            return $"{load.Config?.LineLength}|{string.Join(";", load.Errors.Select(e => e.Message))}|{editor.EndOfLine}|{editor.ErrorMessage}";
        }
    }

    /// <summary>
    ///     Test that the collections of results cannot be modified through casts.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_ImmutableResults_RulesIgnoreAndErrors_CannotBeModified()
    {
        // Arrange: a valid and an invalid configuration
        using var directory = new TemporaryDirectory();
        directory.WriteFile("ok/.lintmark.yaml", "ignore:\n  - a/**\n");
        directory.WriteFile("bad/.lintmark.yaml", "bogus: 1\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load both
        var valid = loader.Load(directory.PathOf("ok/a.md"));
        var invalid = loader.Load(directory.PathOf("bad/a.md"));

        // Assert: none of the collections can be changed by a caller
        ReadOnlyAssertions.AssertCannotBeModified(valid.Config!.Rules);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)valid.Config.Ignore);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<ConfigError>)invalid.Errors);
    }
}
