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
