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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the FileDiscovery class.
/// </summary>
public class FileDiscoveryTests
{
    /// <summary>
    ///     Creates a directory tree used by most tests.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    private static void CreateTree(TemporaryDirectory directory)
    {
        directory.WriteFile("README.md");
        directory.WriteFile("notes.txt", "not markdown");
        directory.WriteFile("docs/guide.md");
        directory.WriteFile("docs/deep/more.md");
        directory.WriteFile("docs/generated/api.md");
        directory.WriteFile("node_modules/pkg/readme.md");
        directory.WriteFile(".git/info.md");
    }

    /// <summary>
    ///     Converts relative paths to the expected sorted full paths.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <param name="relative">Relative paths.</param>
    /// <returns>The sorted full paths.</returns>
    private static List<string> Expected(TemporaryDirectory directory, params string[] relative) =>
        [.. relative.Select(directory.PathOf).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Test that an existing file is returned as its full path.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ExistingFile_ReturnsFullPath()
    {
        // Arrange: a tree and an explicit relative file
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover
        var files = FileDiscovery.Discover(["docs/guide.md"], directory.FullPath);

        // Assert: the full path of that file only
        Assert.Equal(Expected(directory, "docs/guide.md"), files);
    }

    /// <summary>
    ///     Test that an explicit file is accepted even when it is not a Markdown file or lives in a built-in excluded directory.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ExplicitFile_IsNotSubjectToDefaultExcludesOrExtension()
    {
        // Arrange: a non-Markdown file and a file in node_modules
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name both explicitly
        var files = FileDiscovery.Discover(["notes.txt", "node_modules/pkg/readme.md"], directory.FullPath);

        // Assert: both are returned
        Assert.Equal(Expected(directory, "notes.txt", "node_modules/pkg/readme.md"), files);
    }

    /// <summary>
    ///     Test that a directory argument selects every Markdown file beneath it.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_Directory_ReturnsMarkdownFilesBeneathIt()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover a directory
        var files = FileDiscovery.Discover(["docs"], directory.FullPath);

        // Assert: Markdown files at all depths below docs
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"), files);
    }

    /// <summary>
    ///     Test that a recursive glob selects nested files and ignores other extensions.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_RecursiveGlob_ReturnsMatchingFiles()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with a glob rooted in a subdirectory
        var files = FileDiscovery.Discover(["docs/**/*.md"], directory.FullPath);

        // Assert: matches below docs only
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"), files);
    }

    /// <summary>
    ///     Test that a single-segment wildcard does not descend into subdirectories.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_SingleSegmentGlob_DoesNotDescend()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with a star glob
        var files = FileDiscovery.Discover(["*.md"], directory.FullPath);

        // Assert: only the root file matches
        Assert.Equal(Expected(directory, "README.md"), files);
    }

    /// <summary>
    ///     Test that a glob given with backslash separators works.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_BackslashGlob_IsAccepted()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with Windows-style separators
        var files = FileDiscovery.Discover([@"docs\*.md"], directory.FullPath);

        // Assert: the direct children of docs
        Assert.Equal(Expected(directory, "docs/guide.md"), files);
    }

    /// <summary>
    ///     Test that an absolute glob is resolved from its wildcard-free prefix.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_AbsoluteGlob_ReturnsMatchingFiles()
    {
        // Arrange: a tree and an unrelated base directory
        using var directory = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        CreateTree(directory);
        var pattern = directory.PathOf("docs").Replace('\\', '/') + "/**/*.md";

        // Act: discover with an absolute pattern
        var files = FileDiscovery.Discover([pattern], elsewhere.FullPath);

        // Assert: the files below the absolute prefix
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"), files);
    }

    /// <summary>
    ///     Test that a parent-relative glob is resolved against the base directory.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ParentRelativeGlob_ReturnsMatchingFiles()
    {
        // Arrange: a base directory two levels below the tree root
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with a ".." glob
        var files = FileDiscovery.Discover(["../*.md"], directory.PathOf("docs/deep"));

        // Assert: the parent directory's files
        Assert.Equal(Expected(directory, "docs/guide.md"), files);
    }

    /// <summary>
    ///     Test that no arguments mean every Markdown file under the base directory, minus the built-in excludes.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_NoArguments_UsesDefaultPatternAndAppliesBuiltInExcludes()
    {
        // Arrange: a tree with node_modules and .git directories
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with no arguments
        var files = FileDiscovery.Discover([], directory.FullPath);

        // Assert: all Markdown files except those under node_modules and .git
        Assert.Equal(
            Expected(directory, "README.md", "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"),
            files);
    }

    /// <summary>
    ///     Test that built-in excludes also apply to directory and glob expansion.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_DirectoryAndGlob_SkipNodeModulesAndGit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover with an explicit root directory and a recursive glob
        var byDirectory = FileDiscovery.Discover(["."], directory.FullPath);
        var byGlob = FileDiscovery.Discover(["**/*.md"], directory.FullPath);

        // Assert: neither contains the excluded directories
        Assert.Equal(byDirectory, byGlob);
        Assert.DoesNotContain(byGlob, f => f.Contains("node_modules", StringComparison.Ordinal) || f.Contains(".git", StringComparison.Ordinal));
        Assert.Equal(4, byGlob.Count);
    }

    /// <summary>
    ///     Test that a directory argument naming <c>node_modules</c> selects nothing.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_NodeModulesDirectoryArgument_ReturnsNothing()
    {
        // Arrange: a tree whose node_modules holds files at several depths
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        directory.WriteFile("node_modules/pkg/sub/deep.md");

        // Act: name the folder, a folder inside it, and the folder with a trailing separator
        var folder = FileDiscovery.Discover(["node_modules"], directory.FullPath);
        var inside = FileDiscovery.Discover(["node_modules/pkg"], directory.FullPath);
        var trailing = FileDiscovery.Discover(["./node_modules/"], directory.FullPath);

        // Assert: nothing is selected from any of them
        Assert.Empty(folder);
        Assert.Empty(inside);
        Assert.Empty(trailing);
    }

    /// <summary>
    ///     Test that a directory argument naming <c>.git</c> selects nothing.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_GitDirectoryArgument_ReturnsNothing()
    {
        // Arrange: a tree whose .git holds files at several depths
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        directory.WriteFile(".git/hooks/h.md");

        // Act: name the folder and a folder inside it
        var folder = FileDiscovery.Discover([".git"], directory.FullPath);
        var inside = FileDiscovery.Discover([".git/hooks"], directory.FullPath);

        // Assert: nothing is selected
        Assert.Empty(folder);
        Assert.Empty(inside);
    }

    /// <summary>
    ///     Test that a directory argument naming a nested <c>node_modules</c> folder selects nothing.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_NestedNodeModulesDirectoryArgument_ReturnsNothing()
    {
        // Arrange: a tree with a node_modules folder below docs
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        directory.WriteFile("docs/node_modules/x.md");

        // Act: name the nested folder, and docs itself
        var nested = FileDiscovery.Discover(["docs/node_modules"], directory.FullPath);
        var parent = FileDiscovery.Discover(["docs"], directory.FullPath);

        // Assert: the nested folder selects nothing and docs still excludes it
        Assert.Empty(nested);
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"), parent);
    }

    /// <summary>
    ///     Test that a glob whose wildcard-free prefix is, or lies inside, an excluded folder selects nothing.
    /// </summary>
    /// <param name="pattern">The glob.</param>
    [Theory]
    [InlineData("node_modules/*/*.md")]
    [InlineData("node_modules/**/*.md")]
    [InlineData("node_modules/**")]
    [InlineData("node_modules/pkg/*.md")]
    [InlineData(".git/**")]
    [InlineData(".git/*.md")]
    [InlineData("docs/node_modules/**")]
    [InlineData("docs/node_modules/**/*.md")]
    [InlineData(@"node_modules\**\*.md")]
    public void FileDiscovery_Discover_GlobRootedInNodeModules_ReturnsNothing(string pattern)
    {
        // Arrange: a tree with excluded folders at the top and below docs
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        directory.WriteFile("node_modules/pkg/sub/deep.md");
        directory.WriteFile(".git/hooks/h.md");
        directory.WriteFile("docs/node_modules/x.md");

        // Act: discover
        var files = FileDiscovery.Discover([pattern], directory.FullPath);

        // Assert: nothing is selected
        Assert.Empty(files);
    }

    /// <summary>
    ///     Test that an absolute directory argument naming <c>node_modules</c> or <c>.git</c> selects nothing.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_AbsoluteNodeModulesDirectory_ReturnsNothing()
    {
        // Arrange: a tree and an unrelated base directory
        using var directory = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name the excluded folders by absolute path, and by absolute glob
        var nodeModules = FileDiscovery.Discover([directory.PathOf("node_modules")], elsewhere.FullPath);
        var git = FileDiscovery.Discover([directory.PathOf(".git")], elsewhere.FullPath);
        var glob = FileDiscovery.Discover([directory.PathOf("node_modules").Replace('\\', '/') + "/**/*.md"], elsewhere.FullPath);

        // Assert: nothing is selected
        Assert.Empty(nodeModules);
        Assert.Empty(git);
        Assert.Empty(glob);
    }

    /// <summary>
    ///     Test that naming a third-party folder together with a file inside it selects only the file.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_DirectoryAndExplicitFileInNodeModules_ReturnsOnlyTheFile()
    {
        // Arrange: a tree with several files in node_modules
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        directory.WriteFile("node_modules/pkg/sub/deep.md");

        // Act: name the folder and one file inside it
        var files = FileDiscovery.Discover(["node_modules", "node_modules/pkg/readme.md"], directory.FullPath);

        // Assert: only the explicitly named file
        Assert.Equal(Expected(directory, "node_modules/pkg/readme.md"), files);
    }

    /// <summary>
    ///     Test that a base directory that itself lies inside <c>node_modules</c> still finds its own files.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_BaseDirectoryInsideNodeModules_StillFindsItsFiles()
    {
        // Arrange: the tool runs from inside a package folder below node_modules
        using var directory = new TemporaryDirectory();
        directory.WriteFile("node_modules/pkg/readme.md");
        directory.WriteFile("node_modules/pkg/sub/deep.md");
        directory.WriteFile("node_modules/pkg/node_modules/inner/skipped.md");
        var baseDirectory = directory.PathOf("node_modules/pkg");

        // Act: discover with no arguments, the current directory, a glob and a subdirectory
        var none = FileDiscovery.Discover([], baseDirectory);
        var dot = FileDiscovery.Discover(["."], baseDirectory);
        var glob = FileDiscovery.Discover(["**/*.md"], baseDirectory);
        var sub = FileDiscovery.Discover(["sub"], baseDirectory);

        // Assert: its own files are found; a node_modules folder below the base directory is still skipped
        var all = Expected(directory, "node_modules/pkg/readme.md", "node_modules/pkg/sub/deep.md");
        Assert.Equal(all, none);
        Assert.Equal(all, dot);
        Assert.Equal(all, glob);
        Assert.Equal(Expected(directory, "node_modules/pkg/sub/deep.md"), sub);
    }

    /// <summary>
    ///     Test that a root reached by climbing out of a base directory inside <c>node_modules</c> is judged on its full path.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ParentOfBaseDirectoryInsideNodeModules_ReturnsNothing()
    {
        // Arrange: the tool runs from inside a package folder; its parent and a sibling are inside node_modules too
        using var directory = new TemporaryDirectory();
        directory.WriteFile("node_modules/pkg/readme.md");
        directory.WriteFile("node_modules/other/readme.md");
        directory.WriteFile("top.md");
        var baseDirectory = directory.PathOf("node_modules/pkg");

        // Act: name the node_modules folder, a sibling and a glob in the node_modules folder with parent segments, and the project root
        var parent = FileDiscovery.Discover([".."], baseDirectory);
        var sibling = FileDiscovery.Discover(["../other"], baseDirectory);
        var glob = FileDiscovery.Discover(["../**/*.md"], baseDirectory);
        var projectRoot = FileDiscovery.Discover(["../.."], baseDirectory);

        // Assert: nothing is selected inside node_modules; the project root still selects its own non-third-party files
        Assert.Empty(parent);
        Assert.Empty(sibling);
        Assert.Empty(glob);
        Assert.Equal(Expected(directory, "top.md"), projectRoot);
    }

    /// <summary>
    ///     Test that an exclude pattern removes matches from an include.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ExcludeGlob_RemovesMatches()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: include docs and exclude its generated subtree
        var files = FileDiscovery.Discover(["docs", "!docs/generated/**"], directory.FullPath);

        // Assert: the generated file is gone
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md"), files);
    }

    /// <summary>
    ///     Test that excluding a directory by name excludes everything beneath it.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ExcludeDirectoryName_RemovesEverythingBeneath()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: exclude the directory without a wildcard
        var files = FileDiscovery.Discover(["!docs/generated"], directory.FullPath);

        // Assert: default expansion minus that directory
        Assert.Equal(Expected(directory, "README.md", "docs/guide.md", "docs/deep/more.md"), files);
    }

    /// <summary>
    ///     Test that an exclude given as an absolute path is honored.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_AbsoluteExclude_RemovesMatches()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);
        var exclude = "!" + directory.PathOf("docs").Replace('\\', '/') + "/**";

        // Act: exclude everything under docs by absolute pattern
        var files = FileDiscovery.Discover([exclude], directory.FullPath);

        // Assert: only the root file remains
        Assert.Equal(Expected(directory, "README.md"), files);
    }

    /// <summary>
    ///     Test that an exclude also applies to an explicitly named file.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ExcludeMatchingExplicitFile_RemovesIt()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name a file and exclude it too
        var files = FileDiscovery.Discover(["README.md", "docs/guide.md", "!README.md"], directory.FullPath);

        // Assert: only the other file remains
        Assert.Equal(Expected(directory, "docs/guide.md"), files);
    }

    /// <summary>
    ///     Test that a missing path without wildcards is passed through to be reported when read.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_MissingLiteralPath_PassesThrough()
    {
        // Arrange: a tree without the named file
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover a path that does not exist
        var files = FileDiscovery.Discover(["missing.md"], directory.FullPath);

        // Assert: it is returned so the read failure can be reported
        Assert.Equal([directory.PathOf("missing.md")], files);
    }

    /// <summary>
    ///     Test that a glob without matches, or in a missing directory, yields nothing and is not an error.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_GlobWithoutMatches_ReturnsEmpty()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover patterns that cannot match
        var noMatch = FileDiscovery.Discover(["docs/*.mdx"], directory.FullPath);
        var noDirectory = FileDiscovery.Discover(["nothing/**/*.md"], directory.FullPath);

        // Assert: empty results
        Assert.Empty(noMatch);
        Assert.Empty(noDirectory);
    }

    /// <summary>
    ///     Test that overlapping arguments produce each file once.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_OverlappingArguments_ReturnsDistinctFiles()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name a file, its directory, a glob that matches it and the same file again
        var files = FileDiscovery.Discover(["docs/guide.md", "docs", "docs/*.md", "docs/guide.md"], directory.FullPath);

        // Assert: each file appears once
        Assert.Equal(files.Distinct().Count(), files.Count);
        Assert.Equal(Expected(directory, "docs/guide.md", "docs/deep/more.md", "docs/generated/api.md"), files);
    }

    /// <summary>
    ///     Test that the result is ordinal-sorted regardless of argument order.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_ArgumentOrder_DoesNotAffectSortedResult()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: discover the same files in two argument orders
        var forward = FileDiscovery.Discover(["README.md", "docs/guide.md", "docs/deep/more.md"], directory.FullPath);
        var backward = FileDiscovery.Discover(["docs/deep/more.md", "docs/guide.md", "README.md"], directory.FullPath);

        // Assert: identical and ordinal-sorted
        Assert.Equal(forward, backward);
        Assert.Equal(forward.Order(StringComparer.Ordinal).ToList(), forward);
    }

    /// <summary>
    ///     Test the case behavior of glob matching: insensitive on Windows and macOS, sensitive elsewhere.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_GlobCase_FollowsPlatformComparison()
    {
        // Arrange: a lower-case file name and an upper-case extension pattern
        using var directory = new TemporaryDirectory();
        directory.WriteFile("readme.md");

        // Act: discover with a different case
        var files = FileDiscovery.Discover(["*.MD"], directory.FullPath);

        // Assert: matched only when the platform compares names case-insensitively
        var insensitive = FileDiscovery.PathComparison == StringComparison.OrdinalIgnoreCase;
        Assert.Equal(insensitive ? 1 : 0, files.Count);
    }

    /// <summary>
    ///     Test that blank patterns are rejected as usage errors.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!")]
    public void FileDiscovery_Discover_BlankPattern_ThrowsArgumentException(string pattern)
    {
        // Arrange: a base directory
        using var directory = new TemporaryDirectory();

        // Act / Assert: blank patterns are rejected
        Assert.Throws<ArgumentException>(() => FileDiscovery.Discover([pattern], directory.FullPath));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_NullArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.Discover(null!, "."));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.Discover([], null!));
    }
}
