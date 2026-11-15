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

using DemaConsulting.LintMark.Engine;
using DemaConsulting.LintMark.Verify.Cli;

namespace DemaConsulting.LintMark.Verify.Tests;

/// <summary>
///     Tests for the FileSelection class: expansion of directories and globs, the excluded folders, the DEMA
///     repositories and the sample.
/// </summary>
public class FileSelectionTests
{
    /// <summary>
    ///     Creates a tree with ordinary and excluded folders.
    /// </summary>
    /// <param name="directory">The root.</param>
    private static void CreateTree(TemporaryDirectory directory)
    {
        foreach (var file in new[]
        {
            "a.md", "sub/s.md", "generated/g.md", "node_modules/n.md", "bin/b.md", "obj/o.md", ".corpus/c.md", ".corpus/.clones/x/c.md", "sub/obj/deep.md", "TestResults/t.md"
        })
        {
            directory.WriteFile(file, "# T\n");
        }

        directory.WriteFile("notes.txt", "x");
    }

    /// <summary>
    ///     Gets the display paths of the selected files, relative to a directory.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <param name="root">The directory.</param>
    /// <returns>The relative paths with forward slashes.</returns>
    private static string[] Relative(IReadOnlyList<DiscoveredFile> files, string root) =>
        [.. files.Select(f => Path.GetRelativePath(root, f.Path).Replace('\\', '/')).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Test that a directory argument skips the generated and third-party folders.
    /// </summary>
    [Fact]
    public void FileSelection_Select_Directory_SkipsExcludedFolders()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act
        var files = FileSelection.Select(VerifyOptions.Parse([directory.FullPath]), directory.FullPath);

        // Assert: only the ordinary files remain
        Assert.Equal(["a.md", "sub/s.md"], Relative(files, directory.FullPath));
        Assert.All(files, file => Assert.False(file.IsExplicit));
    }

    /// <summary>
    ///     Test that a glob argument skips the same folders, judged from the root of the glob.
    /// </summary>
    [Fact]
    public void FileSelection_Select_Glob_SkipsExcludedFolders()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act
        var files = FileSelection.Select(VerifyOptions.Parse(["**/*.md"]), directory.FullPath);

        // Assert
        Assert.Equal(["a.md", "sub/s.md"], Relative(files, directory.FullPath));
    }

    /// <summary>
    ///     Test that the corpus folder can be named as the root of a search although it is excluded below other roots.
    /// </summary>
    [Fact]
    public void FileSelection_Select_CorpusFolderAsRoot_IsSearched()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act
        var files = FileSelection.Select(VerifyOptions.Parse([Path.Combine(directory.FullPath, ".corpus")]), directory.FullPath);

        // Assert
        Assert.Equal([".corpus/c.md"], Relative(files, directory.FullPath));
    }

    /// <summary>
    ///     Test that an exclude pattern removes files, and that a named file is explicit.
    /// </summary>
    [Fact]
    public void FileSelection_Select_ExcludeAndNamedFile_AreHonored()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act
        var excluded = FileSelection.Select(VerifyOptions.Parse(["**/*.md", "!sub/**"]), directory.FullPath);
        var named = FileSelection.Select(VerifyOptions.Parse(["a.md"]), directory.FullPath);

        // Assert
        Assert.Equal(["a.md"], Relative(excluded, directory.FullPath));
        Assert.True(Assert.Single(named).IsExplicit);
    }

    /// <summary>
    ///     Test that the DEMA option adds every repository below the root, and only those, without the excluded folders.
    /// </summary>
    [Fact]
    public void FileSelection_Select_Dema_AddsRepositoriesOnly()
    {
        // Arrange: two repositories (one nested in a group folder) and a folder that is not a repository
        using var directory = new TemporaryDirectory();
        Directory.Delete(Path.Combine(directory.FullPath, ".git"));
        directory.WriteFile("repo1/.git/HEAD", "ref");
        directory.WriteFile("repo1/a.md", "# T\n");
        directory.WriteFile("repo1/node_modules/x.md", "# T\n");
        directory.WriteFile("repo1/.corpus/y.md", "# T\n");
        directory.WriteFile("group/repo2/.git/HEAD", "ref");
        directory.WriteFile("group/repo2/docs/b.md", "# T\n");
        directory.WriteFile("loose/z.md", "# T\n");

        // Act
        var files = FileSelection.Select(VerifyOptions.Parse(["--dema", "--dema-root", directory.FullPath]), directory.FullPath);

        // Assert
        Assert.Equal(["group/repo2/docs/b.md", "repo1/a.md"], Relative(files, directory.FullPath));
        Assert.Equal(
            ["group/repo2", "repo1"],
            FileSelection.FindRepositories(directory.FullPath).Select(r => Path.GetRelativePath(directory.FullPath, r).Replace('\\', '/')));
    }

    /// <summary>
    ///     Test that a missing DEMA root is a usage error.
    /// </summary>
    [Fact]
    public void FileSelection_Select_MissingDemaRoot_ThrowsUsageException()
    {
        using var directory = new TemporaryDirectory();
        Assert.Throws<UsageException>(() => FileSelection.Select(VerifyOptions.Parse(["--dema", "--dema-root", directory.PathOf("missing")]), directory.FullPath));
    }

    /// <summary>
    ///     Test that a sample is deterministic, of the requested size, in path order and a subset.
    /// </summary>
    [Fact]
    public void FileSelection_Sample_IsDeterministicSubset()
    {
        // Arrange: 100 files
        var files = Enumerable.Range(0, 100).Select(i => new DiscoveredFile($"/x/f{i:D3}.md", IsExplicit: true)).ToList();

        // Act
        var first = FileSelection.Sample(files, 10);
        var second = FileSelection.Sample(files, 10);

        // Assert
        Assert.Equal(10, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(first.OrderBy(f => f.Path, StringComparer.Ordinal), first);
        Assert.Subset(files.ToHashSet(), first.ToHashSet());
        Assert.Same(files, FileSelection.Sample(files, 100));
    }
}
