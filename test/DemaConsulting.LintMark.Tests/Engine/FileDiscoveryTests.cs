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

    /// <summary>
    ///     Test that a null element is reported as a null argument wherever it stands, even after an invalid pattern.
    /// </summary>
    /// <param name="position">Which element is null (0: first, 1: after a blank pattern, 2: last after valid patterns).</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FileDiscovery_Discover_NullElement_ThrowsArgumentNullException(int position)
    {
        // Arrange: a base directory and a pattern list with a null element (a blank pattern stands before it in one case)
        using var directory = new TemporaryDirectory();
        string[] patterns = position switch
        {
            0 => [null!, "*.md"],
            1 => ["", null!],
            _ => ["*.md", "!docs", null!]
        };

        // Act / Assert: the null element is rejected as null, not as a blank pattern
        var exception = Assert.Throws<ArgumentNullException>(() => FileDiscovery.Discover(patterns, directory.FullPath));
        Assert.Equal("patterns", exception.ParamName);
    }

    /// <summary>
    ///     Test that one file named with different casing gets the same spelling whatever the argument order.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_CaseVariantSpellings_ReturnSameSpellingForAnyOrder()
    {
        // Arrange: one file exists; whether its other spelling names the same file depends on the platform
        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/guide.md");

        // Act: name the file with two spellings in both orders, and once through a glob
        var lowerFirst = FileDiscovery.Discover(["docs/guide.md", "docs/GUIDE.md"], directory.FullPath);
        var upperFirst = FileDiscovery.Discover(["docs/GUIDE.md", "docs/guide.md"], directory.FullPath);
        var withGlob = FileDiscovery.Discover(["docs/GUIDE.md", "docs/*.md"], directory.FullPath);
        var globFirst = FileDiscovery.Discover(["docs/*.md", "docs/GUIDE.md"], directory.FullPath);

        // Assert: the result never depends on the order of the arguments
        Assert.Equal(lowerFirst, upperFirst);
        Assert.Equal(withGlob, globFirst);
        if (FileDiscovery.PathComparison == StringComparison.OrdinalIgnoreCase)
        {
            // Names ignore case, so both spellings are one file, reported under the smallest spelling
            Assert.Equal([directory.PathOf("docs/GUIDE.md")], lowerFirst);
            Assert.Equal([directory.PathOf("docs/GUIDE.md")], withGlob);
        }
        else
        {
            // Names are case-sensitive, so the spellings are two different files (the second does not exist)
            Assert.Equal(2, lowerFirst.Count);
            Assert.Equal(2, withGlob.Count);
        }
    }

    /// <summary>
    ///     Test that a literal path written with backslashes names the same file on every platform.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_BackslashLiteralPath_FindsFile()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name a file and a directory with backslash separators
        var file = FileDiscovery.Discover(["docs\\guide.md"], directory.FullPath);
        var folder = FileDiscovery.Discover(["docs\\deep"], directory.FullPath);

        // Assert: both are found, as with forward slashes
        Assert.Equal(Expected(directory, "docs/guide.md"), file);
        Assert.Equal(Expected(directory, "docs/deep/more.md"), folder);
    }

    /// <summary>
    ///     Test that a literal exclude written with backslashes removes the same files as with forward slashes.
    /// </summary>
    [Fact]
    public void FileDiscovery_Discover_BackslashLiteralExclude_RemovesFiles()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: exclude a directory and a file by backslash paths
        var files = FileDiscovery.Discover(["docs", "!docs\\deep", "!docs\\guide.md"], directory.FullPath);

        // Assert: only the remaining file is left
        Assert.Equal(Expected(directory, "docs/generated/api.md"), files);
    }

    /// <summary>
    ///     Test that a file named as an existing path is explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_NamedFile_IsExplicit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name one file
        var files = FileDiscovery.DiscoverFiles(["docs/guide.md"], directory.FullPath);

        // Assert: that file is returned and is explicit
        var file = Assert.Single(files);
        Assert.Equal(directory.PathOf("docs/guide.md"), file.Path);
        Assert.True(file.IsExplicit);
    }

    /// <summary>
    ///     Test that a file found through a glob is not explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_GlobMatch_IsNotExplicit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: select a file with a glob
        var files = FileDiscovery.DiscoverFiles(["docs/*.md"], directory.FullPath);

        // Assert: the file is returned but is not explicit
        var file = Assert.Single(files);
        Assert.Equal(directory.PathOf("docs/guide.md"), file.Path);
        Assert.False(file.IsExplicit);
    }

    /// <summary>
    ///     Test that files found beneath a directory argument are not explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_DirectoryArgument_FilesAreNotExplicit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name a directory
        var files = FileDiscovery.DiscoverFiles(["docs"], directory.FullPath);

        // Assert: several files, none explicit
        Assert.Equal(3, files.Count);
        Assert.All(files, file => Assert.False(file.IsExplicit));
    }

    /// <summary>
    ///     Test that files found through the default pattern are not explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_DefaultPattern_FilesAreNotExplicit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: use no arguments
        var files = FileDiscovery.DiscoverFiles([], directory.FullPath);

        // Assert: four files, none explicit
        Assert.Equal(4, files.Count);
        Assert.All(files, file => Assert.False(file.IsExplicit));
    }

    /// <summary>
    ///     Test that a file that is both named and matched by a glob is explicit, and only that file.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_NamedAndGlobbed_IsExplicit()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: glob everything below docs and also name one file of it
        var files = FileDiscovery.DiscoverFiles(["docs/**/*.md", "docs/guide.md"], directory.FullPath);

        // Assert: each file once; only the named one is explicit
        Assert.Equal(3, files.Count);
        var explicitFile = Assert.Single(files, static file => file.IsExplicit);
        Assert.Equal(directory.PathOf("docs/guide.md"), explicitFile.Path);
    }

    /// <summary>
    ///     Test that a missing path without wildcards is explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_MissingLiteralPath_IsExplicit()
    {
        // Arrange: a tree without the named file
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name a file that does not exist
        var files = FileDiscovery.DiscoverFiles(["missing.md"], directory.FullPath);

        // Assert: it is passed through as an explicit file
        var file = Assert.Single(files);
        Assert.Equal(directory.PathOf("missing.md"), file.Path);
        Assert.True(file.IsExplicit);
    }

    /// <summary>
    ///     Test that spellings of one file that differ only in case collapse to the smallest spelling, explicit when any spelling was named.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_CaseVariantSpellings_KeepsSmallestAndExplicit()
    {
        // Arrange: one file, found by a glob under its real spelling and named under another spelling
        if (FileDiscovery.PathComparison != StringComparison.OrdinalIgnoreCase)
        {
            // Names are case-sensitive here, so the spellings are different files
            return;
        }

        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/guide.md");

        // Act: discover in both argument orders
        var globFirst = FileDiscovery.DiscoverFiles(["docs/*.md", "docs/GUIDE.md"], directory.FullPath);
        var namedFirst = FileDiscovery.DiscoverFiles(["docs/GUIDE.md", "docs/*.md"], directory.FullPath);

        // Assert: one explicit file under the smallest spelling, whatever the order
        var file = Assert.Single(globFirst);
        Assert.Equal(directory.PathOf("docs/GUIDE.md"), file.Path);
        Assert.True(file.IsExplicit);
        Assert.Equal(globFirst, namedFirst);
    }

    /// <summary>
    ///     Test that an explicitly named file inside <c>node_modules</c> is kept and explicit.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_NamedFileInsideNodeModules_IsExplicitAndKept()
    {
        // Arrange: a tree with a file in node_modules
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name the file
        var files = FileDiscovery.DiscoverFiles(["node_modules/pkg/readme.md"], directory.FullPath);

        // Assert: the file is kept and explicit
        var file = Assert.Single(files);
        Assert.Equal(directory.PathOf("node_modules/pkg/readme.md"), file.Path);
        Assert.True(file.IsExplicit);
    }

    /// <summary>
    ///     Test that a user exclude still removes an explicitly named file.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_ExplicitFileWithExclude_IsRemoved()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: name two files and exclude one
        var files = FileDiscovery.DiscoverFiles(["README.md", "docs/guide.md", "!README.md"], directory.FullPath);

        // Assert: only the other file remains, still explicit
        var file = Assert.Single(files);
        Assert.Equal(directory.PathOf("docs/guide.md"), file.Path);
        Assert.True(file.IsExplicit);
    }

    /// <summary>
    ///     Test that the result is in ordinal order of the full path regardless of how the files were found.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_AnyMatches_ResultOrderedByOrdinalPath()
    {
        // Arrange: a tree
        using var directory = new TemporaryDirectory();
        CreateTree(directory);

        // Act: mix a named file, a glob and a directory in unsorted order
        var files = FileDiscovery.DiscoverFiles(["docs/guide.md", "README.md", "docs"], directory.FullPath);

        // Assert: the paths are ordinal-sorted and distinct
        var paths = files.Select(static file => file.Path).ToList();
        Assert.Equal(paths.Order(StringComparer.Ordinal).ToList(), paths);
        Assert.Equal(paths.Distinct(StringComparer.Ordinal).Count(), paths.Count);
        Assert.Equal(4, paths.Count);
    }

    /// <summary>
    ///     Test that null arguments, and a null element of the patterns, are rejected.
    /// </summary>
    [Fact]
    public void FileDiscovery_DiscoverFiles_NullArguments_Throw()
    {
        // Arrange / Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.DiscoverFiles(null!, "."));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.DiscoverFiles([], null!));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.DiscoverFiles(["*.md", null!], "."));
    }

    /// <summary>
    ///     Test that a discovered file keeps its path and flag.
    /// </summary>
    [Fact]
    public void DiscoveredFile_Constructor_ValidArguments_KeepsValues()
    {
        // Arrange / Act: an explicit and an implicit file
        var named = new DiscoveredFile("a.md", isExplicit: true);
        var found = new DiscoveredFile("b.md", isExplicit: false);

        // Assert: the values are kept
        Assert.Equal("a.md", named.Path);
        Assert.True(named.IsExplicit);
        Assert.Equal("b.md", found.Path);
        Assert.False(found.IsExplicit);
    }

    /// <summary>
    ///     Test that a discovered file without a path is rejected.
    /// </summary>
    [Fact]
    public void DiscoveredFile_Constructor_MissingPath_Throws()
    {
        // Arrange / Act / Assert: the path may be neither null nor empty
        Assert.Throws<ArgumentNullException>(() => new DiscoveredFile(null!, isExplicit: true));
        Assert.Throws<ArgumentException>(() => new DiscoveredFile(string.Empty, isExplicit: false));
    }

    /// <summary>
    ///     Test that blank patterns are rejected as usage errors.
    /// </summary>
    /// <param name="pattern">The blank pattern.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!")]
    public void FileDiscovery_DiscoverFiles_EmptyPattern_Throws(string pattern)
    {
        // Arrange: a base directory
        using var directory = new TemporaryDirectory();

        // Act / Assert: blank patterns are rejected
        Assert.Throws<ArgumentException>(() => FileDiscovery.DiscoverFiles([pattern], directory.FullPath));
    }

    /// <summary>
    ///     Test that an ignore pattern matches a file below the root.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_PatternMatch_True()
    {
        // Arrange: a root and a file in a generated folder
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("docs/generated/api.md");

        // Act: judge the file
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, ["docs/generated/**"]);

        // Assert: it is ignored
        Assert.True(ignored);
    }

    /// <summary>
    ///     Test that a file matching no pattern is not ignored.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_NoMatch_False()
    {
        // Arrange: a root and a file outside the generated folder
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("docs/guide.md");

        // Act: judge the file
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, ["docs/generated/**", "*.txt"]);

        // Assert: it is not ignored
        Assert.False(ignored);
    }

    /// <summary>
    ///     Test that patterns are relative to the ignore root rather than the current directory.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_PatternRelativeToConfigDirectory_Matches()
    {
        // Arrange: a root that is a subdirectory of the base
        using var directory = new TemporaryDirectory();
        var root = directory.PathOf("sub");

        // Act: judge files below the root and below the base with the same pattern
        var belowRoot = FileDiscovery.IsIgnored(directory.PathOf("sub/x/a.md"), root, ["x/**"]);
        var belowBase = FileDiscovery.IsIgnored(directory.PathOf("x/a.md"), root, ["x/**"]);
        var wrongDepth = FileDiscovery.IsIgnored(directory.PathOf("sub/x/a.md"), directory.FullPath, ["x/**"]);

        // Assert: only the file below the root matches
        Assert.True(belowRoot);
        Assert.False(belowBase);
        Assert.False(wrongDepth);
    }

    /// <summary>
    ///     Test that a file outside the root is never ignored, even by a pattern that matches everything.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_FileOutsideRoot_False()
    {
        // Arrange: a root and files beside and above it
        using var directory = new TemporaryDirectory();
        var root = directory.PathOf("sub");

        // Act: judge files outside the root and one inside it with a match-everything pattern
        var sibling = FileDiscovery.IsIgnored(directory.PathOf("other/a.md"), root, ["**"]);
        var parent = FileDiscovery.IsIgnored(directory.PathOf("a.md"), root, ["**"]);
        var inside = FileDiscovery.IsIgnored(directory.PathOf("sub/a.md"), root, ["**"]);

        // Assert: only the file inside the root is ignored
        Assert.False(sibling);
        Assert.False(parent);
        Assert.True(inside);
    }

    /// <summary>
    ///     Test that a pattern written with backslashes matches like its forward-slash form.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_BackslashPattern_Matches()
    {
        // Arrange: a root and a file in a generated folder
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("docs/generated/api.md");

        // Act: judge the file with a backslash pattern
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, [@"docs\generated\**"]);

        // Assert: it is ignored
        Assert.True(ignored);
    }

    /// <summary>
    ///     Test that pattern matching follows the case rules of the platform.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_CaseRules_FollowPlatform()
    {
        // Arrange: a path whose case differs from the pattern
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("Docs/Api.md");

        // Act: judge the file
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, ["docs/**"]);

        // Assert: ignored only when the platform compares names case-insensitively
        Assert.Equal(FileDiscovery.PathComparison == StringComparison.OrdinalIgnoreCase, ignored);
    }

    /// <summary>
    ///     Test that an empty pattern list ignores nothing.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_EmptyPatternList_False()
    {
        // Arrange: a root and a file
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf("docs/guide.md");

        // Act: judge the file against no patterns
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, []);

        // Assert: it is not ignored
        Assert.False(ignored);
    }

    /// <summary>
    ///     Test that null arguments, and a null element of the patterns, are rejected.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_NullArgument_Throws()
    {
        // Arrange / Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.IsIgnored(null!, ".", ["**"]));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.IsIgnored("a.md", null!, ["**"]));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.IsIgnored("a.md", ".", null!));
        Assert.Throws<ArgumentNullException>(() => FileDiscovery.IsIgnored("a.md", ".", ["*.txt", null!]));
    }

    /// <summary>
    ///     Test that the built-in ignore patterns match the version-control, dependency and generated folders only.
    /// </summary>
    /// <param name="relative">The path relative to the root.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData(".git/info.md", true)]
    [InlineData(".git/hooks/h.md", true)]
    [InlineData("node_modules/pkg/readme.md", true)]
    [InlineData("tools/.venv/lib/readme.md", true)]
    [InlineData("docs/generated/api.md", true)]
    [InlineData("generated/api.md", true)]
    [InlineData("a/thirdparty/lib/readme.md", true)]
    [InlineData(".agent-logs/report.md", true)]
    [InlineData("README.md", false)]
    [InlineData("docs/guide.md", false)]
    [InlineData("docs/generated.md", false)]
    [InlineData("src/agent-logs/report.md", false)]
    public void FileDiscovery_IsIgnored_DefaultIgnorePatterns_MatchGitNodeModulesAndGenerated(string relative, bool expected)
    {
        // Arrange: a root and a path relative to it
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf(relative);

        // Act: judge the file with the default ignore patterns
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, LintMarkConfig.DefaultIgnore);

        // Assert: ignored exactly when expected
        Assert.Equal(expected, ignored);
    }

    /// <summary>
    ///     Test that a recursive wildcard in the middle of a pattern matches at any depth.
    /// </summary>
    /// <param name="relative">The path relative to the root.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData("docs/x.md", true)]
    [InlineData("docs/a/x.md", true)]
    [InlineData("docs/a/b/c/x.md", true)]
    [InlineData("other/x.md", false)]
    [InlineData("docs/a/y.md", false)]
    public void FileDiscovery_IsIgnored_GlobStarStar_MatchesAtAnyDepth(string relative, bool expected)
    {
        // Arrange: a root and a path relative to it
        using var directory = new TemporaryDirectory();
        var file = directory.PathOf(relative);

        // Act: judge the file
        var ignored = FileDiscovery.IsIgnored(file, directory.FullPath, ["docs/**/x.md"]);

        // Assert: ignored exactly when expected
        Assert.Equal(expected, ignored);
    }

    /// <summary>
    ///     Test that a pattern without a directory part matches a file directly under the root only.
    /// </summary>
    [Fact]
    public void FileDiscovery_IsIgnored_FileDirectlyUnderRoot_PatternWithoutDirectoryMatches()
    {
        // Arrange: a root with a matching file directly under it, a non-matching one, and a matching one deeper
        using var directory = new TemporaryDirectory();

        // Act: judge the three files with a single-segment wildcard
        var direct = FileDiscovery.IsIgnored(directory.PathOf("a.generated.md"), directory.FullPath, ["*.generated.md"]);
        var other = FileDiscovery.IsIgnored(directory.PathOf("a.md"), directory.FullPath, ["*.generated.md"]);
        var deeper = FileDiscovery.IsIgnored(directory.PathOf("docs/a.generated.md"), directory.FullPath, ["*.generated.md"]);

        // Assert: only the file directly under the root matches
        Assert.True(direct);
        Assert.False(other);
        Assert.False(deeper);
    }
}
