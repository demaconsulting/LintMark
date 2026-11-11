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

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace OtsSoftwareTests;

/// <summary>
///     Integration tests of the Microsoft.Extensions.FileSystemGlobbing OTS package: they pin the
///     include/exclude semantics that LintMark's file discovery relies on.
/// </summary>
public class FileSystemGlobbingTests
{
    /// <summary>
    ///     Creates a temporary directory tree containing the given relative files and runs the matcher over it.
    /// </summary>
    /// <param name="matcher">The matcher to execute.</param>
    /// <param name="files">Relative file paths (forward slashes) to create.</param>
    /// <returns>The matched relative paths, in ordinal order.</returns>
    private static List<string> Execute(Matcher matcher, params string[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "lintmark-ots-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Create the tree
            foreach (var file in files)
            {
                var full = Path.Combine(root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, string.Empty);
            }

            // Run the matcher against the real directory
            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
            return [.. result.Files.Select(f => f.Path.Replace('\\', '/')).Order(StringComparer.Ordinal)];
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a recursive include matches files at every depth.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_RecursiveInclude_MatchesNestedFiles()
    {
        // Arrange: a matcher for every Markdown file, and a nested tree
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*.md");

        // Act: execute over the tree
        var matches = Execute(matcher, "a.md", "docs/b.md", "docs/deep/c.md", "docs/d.txt");

        // Assert: Markdown files at all depths match, other files do not
        Assert.Equal(["a.md", "docs/b.md", "docs/deep/c.md"], matches);
    }

    /// <summary>
    ///     Validates that an exclude pattern removes matches.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_Exclude_RemovesMatches()
    {
        // Arrange: include all Markdown, exclude one subtree
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*.md");
        matcher.AddExclude("generated/**");

        // Act: execute over the tree
        var matches = Execute(matcher, "a.md", "generated/b.md", "docs/c.md");

        // Assert: files below the excluded directory are gone
        Assert.Equal(["a.md", "docs/c.md"], matches);
    }

    /// <summary>
    ///     Validates that a directory-name exclude with a leading recursive wildcard matches at the root and below.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_DirectoryExcludeAnyDepth_RemovesRootAndNestedDirectories()
    {
        // Arrange: exclude node_modules wherever it occurs
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*.md");
        matcher.AddExclude("**/node_modules/**");

        // Act: execute over a tree with node_modules at the root and nested
        var matches = Execute(matcher, "a.md", "node_modules/x.md", "pkg/node_modules/y.md", "pkg/z.md");

        // Assert: both node_modules directories are excluded
        Assert.Equal(["a.md", "pkg/z.md"], matches);
    }

    /// <summary>
    ///     Validates that a pattern with no matching file yields an empty result rather than an error.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_NoMatches_ReturnsEmpty()
    {
        // Arrange: a pattern that cannot match
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*.md");

        // Act: execute over a tree without Markdown files
        var matches = Execute(matcher, "a.txt", "docs/b.txt");

        // Assert: nothing matched
        Assert.Empty(matches);
    }

    /// <summary>
    ///     Validates the two case-sensitivity modes of the matcher.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_CaseSensitivityMode_ControlsMatching()
    {
        // Arrange: one case-sensitive and one case-insensitive matcher for lower-case ".md"
        var sensitive = new Matcher(StringComparison.Ordinal);
        sensitive.AddInclude("**/*.md");
        var insensitive = new Matcher(StringComparison.OrdinalIgnoreCase);
        insensitive.AddInclude("**/*.md");

        // Act: execute both over a file with an upper-case extension
        var sensitiveMatches = Execute(sensitive, "README.MD");
        var insensitiveMatches = Execute(insensitive, "README.MD");

        // Assert: only the case-insensitive matcher matches
        Assert.Empty(sensitiveMatches);
        Assert.Equal(["README.MD"], insensitiveMatches);
    }

    /// <summary>
    ///     Validates that the matcher can filter an in-memory list of relative paths (used for user excludes).
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_InMemoryMatch_AppliesIncludeAndExclude()
    {
        // Arrange: include everything and exclude a subtree
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**");
        matcher.AddExclude("docs/generated/**");
        var files = new[] { "a.md", "docs/b.md", "docs/generated/c.md" };

        // Act: match without touching the file system
        var result = matcher.Match(Path.GetTempPath(), files);

        // Assert: the excluded path is removed
        Assert.Equal(["a.md", "docs/b.md"], result.Files.Select(f => f.Path).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    ///     Validates the wildcard dialect: a star matches within a single path segment.
    /// </summary>
    [Fact]
    public void FileSystemGlobbing_Matcher_Wildcards_StarMatchesWithinSegment()
    {
        // Arrange: a single-segment star pattern
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("docs/*.md");

        // Act: execute over files at several depths
        var matches = Execute(matcher, "docs/a.md", "docs/deep/b.md", "other/c.md");

        // Assert: only the direct children of docs match
        Assert.Equal(["docs/a.md"], matches);
    }
}
