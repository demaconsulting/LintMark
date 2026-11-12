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
using EditorConfig.Core;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the EditorConfigResolver class.
/// </summary>
/// <remarks>
///     Every temporary tree starts with an <c>.editorconfig</c> that declares <c>root = true</c>, so the
///     upward search never reaches a file outside the tree (except in the tests about the end of the chain).
/// </remarks>
[Collection("Sequential")]
public class EditorConfigResolverTests
{
    /// <summary>
    ///     Creates a temporary directory whose root <c>.editorconfig</c> has the given sections.
    /// </summary>
    /// <param name="sections">The text after <c>root = true</c>.</param>
    /// <returns>The directory.</returns>
    private static TemporaryDirectory Tree(string sections)
    {
        var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n" + sections);
        return directory;
    }

    /// <summary>
    ///     Resolves a file below a directory with a new resolver.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="relative">Relative path of the file.</param>
    /// <returns>The result.</returns>
    private static EditorConfigResult Resolve(TemporaryDirectory directory, string relative) =>
        new EditorConfigResolver().Resolve(directory.PathOf(relative));

    /// <summary>
    ///     Test that lf is resolved.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_LfSection_ReturnsLf()
    {
        // Arrange: a section that selects lf
        using var directory = Tree("[*]\nend_of_line = lf\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: lf without an error
        Assert.Equal(LineEnding.Lf, result.EndOfLine);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    ///     Test that crlf is resolved.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_CrLfSection_ReturnsCrLf()
    {
        // Arrange: a section that selects crlf
        using var directory = Tree("[*]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: crlf without an error
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    ///     Test that the property name and the value are matched in any letter case.
    /// </summary>
    [Theory]
    [InlineData("END_OF_LINE = CRLF")]
    [InlineData("End_Of_Line = Crlf")]
    [InlineData("end_of_line=crlf")]
    [InlineData("  end_of_line   =   crlf  ")]
    public void EditorConfigResolver_Resolve_UpperCaseValueAndKey_Accepted(string line)
    {
        // Arrange: a section with the spelling
        using var directory = Tree($"[*]\n{line}\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: crlf
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
    }

    /// <summary>
    ///     Test that a carriage-return-only value is not specified and not an error.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_CrValue_NotSpecified()
    {
        // Arrange: a section that selects cr
        using var directory = Tree("[*]\nend_of_line = cr\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: not specified and no error
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that unset is not specified.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_UnsetValue_NotSpecified()
    {
        // Arrange: a section that unsets the value
        using var directory = Tree("[*]\nend_of_line = unset\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: not specified
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that unset in a nearer file hides a value of a farther file.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_UnsetInNearerFile_HidesFartherValue()
    {
        // Arrange: crlf in the root and unset in a nested file
        using var directory = Tree("[*]\nend_of_line = crlf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nend_of_line = unset\n");

        // Act: resolve files in the nested folder and beside the root file
        var nested = Resolve(directory, "sub/a.md");
        var top = Resolve(directory, "a.md");

        // Assert: the nested file is not specified, while the root keeps crlf
        Assert.Null(nested.EndOfLine);
        Assert.Equal(LineEnding.CrLf, top.EndOfLine);
    }

    /// <summary>
    ///     Test that an unknown word is not specified and not an error.
    /// </summary>
    [Theory]
    [InlineData("banana")]
    [InlineData("")]
    [InlineData("lf crlf")]
    public void EditorConfigResolver_Resolve_InvalidValue_NotSpecified(string value)
    {
        // Arrange: a section with an unusable value
        using var directory = Tree($"[*]\nend_of_line = {value}\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: not specified and no error
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that an invalid value in a nearer file hides a valid value of a farther file.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_InvalidValueInNearerFile_HidesFartherValue()
    {
        // Arrange: crlf in the root and an unusable word in a nested file
        using var directory = Tree("[*]\nend_of_line = crlf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nend_of_line = banana\n");

        // Act: resolve a file in the nested folder
        var result = Resolve(directory, "sub/a.md");

        // Assert: the nearer, unusable value wins, so nothing is specified
        Assert.Null(result.EndOfLine);
    }

    /// <summary>
    ///     Test that a file without an end_of_line setting is not specified.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NoEndOfLineSetting_NotSpecified()
    {
        // Arrange: a section that sets another property
        using var directory = Tree("[*]\nindent_style = space\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: not specified
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that a file that no section matches is not specified.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_FileOutsideAnySection_NotSpecified()
    {
        // Arrange: a section for another file type
        using var directory = Tree("[*.txt]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: not specified
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that a file that declares root stops the search for farther files.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_RootTrue_StopsSearch()
    {
        // Arrange: crlf in the outer file, and a nested root file that says nothing about line endings
        using var directory = Tree("[*]\nend_of_line = crlf\n");
        directory.WriteFile("sub/.editorconfig", "root = true\n\n[*]\nindent_style = tab\n");

        // Act: resolve a file below the nested root
        var result = Resolve(directory, "sub/a.md");

        // Assert: the outer value is not visible
        Assert.Null(result.EndOfLine);
    }

    /// <summary>
    ///     Test that a nearer file without the property uses the value of a farther file when it is not a root.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NoRoot_NearerFileLacksProperty_UsesFartherFile()
    {
        // Arrange: crlf in the outer file, and a nested file without root that sets another property
        using var directory = Tree("[*]\nend_of_line = crlf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nindent_style = tab\n");

        // Act: resolve a file in the nested folder
        var result = Resolve(directory, "sub/a.md");

        // Assert: the farther value applies
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
    }

    /// <summary>
    ///     Test that the nearest file wins for the property.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NestedEditorConfigFiles_NearestFileWinsPerProperty()
    {
        // Arrange: lf in the outer file and crlf in the nested one
        using var directory = Tree("[*]\nend_of_line = lf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nend_of_line = crlf\n");

        // Act: resolve files in both folders
        var nested = Resolve(directory, "sub/a.md");
        var outer = Resolve(directory, "a.md");

        // Assert: each gets the value of its nearest file
        Assert.Equal(LineEnding.CrLf, nested.EndOfLine);
        Assert.Equal(LineEnding.Lf, outer.EndOfLine);
    }

    /// <summary>
    ///     Test that a later section in a file overrides an earlier one.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_LessSpecificLaterSection_LaterSectionWins()
    {
        // Arrange: a specific section followed by a broader one
        using var directory = Tree("[*.md]\nend_of_line = lf\n\n[*]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: the later section wins even though it is less specific
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
    }

    /// <summary>
    ///     Test that the later section wins regardless of how specific the earlier one is.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_MoreSpecificEarlierSection_LaterSectionOverrides()
    {
        // Arrange: a broad section followed by an exact file name section
        using var directory = Tree("[*]\nend_of_line = crlf\n\n[a.md]\nend_of_line = lf\n");

        // Act: resolve the named file and another file
        var named = Resolve(directory, "a.md");
        var other = Resolve(directory, "b.md");

        // Assert: the later, specific section applies to its file only
        Assert.Equal(LineEnding.Lf, named.EndOfLine);
        Assert.Equal(LineEnding.CrLf, other.EndOfLine);
    }

    /// <summary>
    ///     Test the section patterns of the editor settings specification.
    /// </summary>
    [Theory]
    [InlineData("*", "a.md", true)]
    [InlineData("**", "a/b/c.md", true)]
    [InlineData("*.md", "sub/a.md", true)]
    [InlineData("*.md", "a.txt", false)]
    [InlineData("a?.md", "ab.md", true)]
    [InlineData("a?.md", "abc.md", false)]
    [InlineData("[ab].md", "a.md", true)]
    [InlineData("[!ab].md", "a.md", false)]
    [InlineData("[!ab].md", "c.md", true)]
    [InlineData("{a,b}.md", "b.md", true)]
    [InlineData("{a,b}.md", "c.md", false)]
    [InlineData("f{1..3}.md", "f3.md", true)]
    [InlineData("f{1..3}.md", "f4.md", false)]
    [InlineData("docs/*.md", "docs/a.md", true)]
    [InlineData("docs/*.md", "docs/x/a.md", false)]
    [InlineData("/top.md", "top.md", true)]
    [InlineData("/top.md", "sub/top.md", false)]
    [InlineData("**/deep/**.md", "a/deep/x.md", true)]
    [InlineData("\\[x\\].md", "[x].md", true)]
    public void EditorConfigResolver_Resolve_SectionPatterns_MatchPerSpecification(string pattern, string file, bool matches)
    {
        // Arrange: a section with the pattern
        using var directory = Tree($"[{pattern}]\nend_of_line = crlf\n");

        // Act: resolve the file
        var result = Resolve(directory, file);

        // Assert: the section applies exactly when the pattern is specified to match
        Assert.Equal(matches ? LineEnding.CrLf : null, result.EndOfLine);
    }

    /// <summary>
    ///     Test that section patterns are case-sensitive on every platform.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_SectionPatternCase_IsCaseSensitiveOnEveryPlatform()
    {
        // Arrange: a lower-case pattern and an upper-case pattern
        using var directory = Tree("[*.md]\nend_of_line = lf\n\n[*.TXT]\nend_of_line = crlf\n");

        // Act: resolve files whose extension differs only by case
        var upperMarkdown = Resolve(directory, "A.MD");
        var lowerText = Resolve(directory, "a.txt");
        var exactText = Resolve(directory, "a.TXT");

        // Assert: only the exact spelling matches
        Assert.Null(upperMarkdown.EndOfLine);
        Assert.Null(lowerText.EndOfLine);
        Assert.Equal(LineEnding.CrLf, exactText.EndOfLine);
    }

    /// <summary>
    ///     Test that a section pattern with a slash is relative to the folder of the settings file.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NestedDirectory_PatternIsRelativeToEditorConfigDirectory()
    {
        // Arrange: a nested settings file with a pattern that contains a folder
        using var directory = Tree("[*]\nend_of_line = lf\n");
        directory.WriteFile("docs/.editorconfig", "[guide/*.md]\nend_of_line = crlf\n");

        // Act: resolve files that match the pattern relative to docs, and relative to the root
        var inGuide = Resolve(directory, "docs/guide/a.md");
        var rootGuide = Resolve(directory, "guide/a.md");

        // Assert: the pattern applies below docs only
        Assert.Equal(LineEnding.CrLf, inGuide.EndOfLine);
        Assert.Equal(LineEnding.Lf, rootGuide.EndOfLine);
    }

    /// <summary>
    ///     Test that a file in a missing folder is still resolved.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NonexistentFileInMissingDirectory_StillResolves()
    {
        // Arrange: a catch-all section
        using var directory = Tree("[*]\nend_of_line = crlf\n");

        // Act: resolve a path in folders that do not exist
        var result = Resolve(directory, "missing/deeper/a.md");

        // Assert: the section applies
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
    }

    /// <summary>
    ///     Test that a relative path is resolved against the current directory.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_RelativePath_ResolvedAgainstCurrentDirectory()
    {
        // Arrange: a settings file in a sub folder
        using var directory = Tree("[*]\nend_of_line = lf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nend_of_line = crlf\n");
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullPath);

            // Act: resolve a relative path
            var result = new EditorConfigResolver().Resolve("sub/a.md");

            // Assert: the nested file applied
            Assert.Equal(LineEnding.CrLf, result.EndOfLine);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    /// <summary>
    ///     Test that backslash separators are accepted on every platform.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_BackslashSeparators_AreNormalized()
    {
        // Arrange: a nested settings file and a path written with backslashes
        using var directory = Tree("[*]\nend_of_line = lf\n");
        directory.WriteFile("sub/.editorconfig", "[*]\nend_of_line = crlf\n");
        var path = directory.FullPath + "\\sub\\a.md";

        // Act: resolve the path
        var result = new EditorConfigResolver().Resolve(path);

        // Assert: the nested file applied
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
    }

    /// <summary>
    ///     Test that malformed lines are ignored and later valid sections still apply.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_MalformedEditorConfig_BadLinesIgnored_LaterSectionApplies()
    {
        // Arrange: garbage before and between valid sections
        using var directory = Tree("[[[\nend_of_line\n=\ngarbage here\n\n[*.md]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var result = Resolve(directory, "a.md");

        // Assert: no error, and the valid section applies
        Assert.Equal(LineEnding.CrLf, result.EndOfLine);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    ///     Test that a settings file that cannot be read is reported as a message and leaves the ending unspecified.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_UnreadableEditorConfig_ReturnsErrorMessage()
    {
        // Arrange: a resolver whose chain lookup fails, and one whose evaluation fails
        var failingChain = new EditorConfigResolver(
            _ => throw new IOException("disk\nfailure"),
            (_, _) => throw new InvalidOperationException("must not be reached"));
        using var directory = Tree("[*]\nend_of_line = lf\n");
        var parser = new EditorConfigParser();
        var failingEvaluate = new EditorConfigResolver(
            parser.GetResolvedChainForDirectory,
            (_, _) => throw new UnauthorizedAccessException("denied"));

        // Act: resolve through both
        var chainFailure = failingChain.Resolve(directory.PathOf("a.md"));
        var evaluateFailure = failingEvaluate.Resolve(directory.PathOf("a.md"));

        // Assert: a one-line message and no line ending in both cases
        Assert.Null(chainFailure.EndOfLine);
        Assert.Equal("Cannot read .editorconfig settings: disk failure", chainFailure.ErrorMessage);
        Assert.Null(evaluateFailure.EndOfLine);
        Assert.Equal("Cannot read .editorconfig settings: denied", evaluateFailure.ErrorMessage);
    }

    /// <summary>
    ///     Test that the chain of settings files is cached per directory.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_SameDirectoryRepeated_UsesCachedChain()
    {
        // Arrange: a settings file that is resolved once
        using var directory = Tree("[*]\nend_of_line = lf\n");
        var resolver = new EditorConfigResolver();
        var first = resolver.Resolve(directory.PathOf("a.md"));

        // Act: change the file and resolve another file of the same folder
        File.WriteAllText(directory.PathOf(".editorconfig"), "root = true\n\n[*]\nend_of_line = crlf\n");
        var second = resolver.Resolve(directory.PathOf("b.md"));

        // Assert: the cached chain is used
        Assert.Equal(LineEnding.Lf, first.EndOfLine);
        Assert.Equal(LineEnding.Lf, second.EndOfLine);
    }

    /// <summary>
    ///     Test that many threads resolving many files get the sequential results.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_ManyThreads_ResultsEqualSequential()
    {
        // Arrange: nested settings files with different sections and many files
        using var directory = Tree("[*.md]\nend_of_line = lf\n\n[docs/*.md]\nend_of_line = crlf\n");
        directory.WriteFile("docs/sub/.editorconfig", "[b*.md]\nend_of_line = lf\n");
        var folders = new[] { "docs/sub", "docs", "other" };
        var files = Enumerable.Range(0, 60)
            .Select(i => directory.PathOf($"{folders[i % 3]}/{(char)('a' + (i % 4))}{i}.md"))
            .ToArray();
        var expected = files.Select(f => new EditorConfigResolver().Resolve(f)).ToArray();
        var shared = new EditorConfigResolver();

        // Act: resolve every file many times from many threads through one resolver
        var actual = new EditorConfigResult[files.Length * 10];
        Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index => actual[index] = shared.Resolve(files[index % files.Length]));

        // Assert: every complete result equals the sequential one
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i % files.Length], actual[i]);
        }
        Assert.Contains(expected, r => r.EndOfLine == LineEnding.CrLf);
        Assert.Contains(expected, r => r.EndOfLine == LineEnding.Lf);
    }

    /// <summary>
    ///     Test that a settings lookup that fails is attempted once per directory and reported for every file of it.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_FailedChain_CachedAndReportedForEveryFileInDirectory()
    {
        // Arrange: a resolver whose chain lookup fails and counts its calls
        var calls = 0;
        var resolver = new EditorConfigResolver(
            _ =>
            {
                Interlocked.Increment(ref calls);
                throw new IOException("disk failure");
            },
            (_, _) => throw new InvalidOperationException("must not be reached"));
        using var directory = new TemporaryDirectory();

        // Act: resolve several files of one directory, then a file of another
        var results = new[] { "a.md", "b.md", "c.md" }.Select(f => resolver.Resolve(directory.PathOf(f))).ToList();
        var calledForFirstDirectory = Volatile.Read(ref calls);
        var other = resolver.Resolve(directory.PathOf("sub/d.md"));

        // Assert: the failure was determined once for the first directory, reported identically for every file, and
        // the other directory was looked up separately
        Assert.Equal(1, calledForFirstDirectory);
        Assert.All(results, r => Assert.Equal(new EditorConfigResult(null, "Cannot read .editorconfig settings: disk failure"), r));
        Assert.Equal(results[0], other);
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    /// <summary>
    ///     Test that a path that cannot be resolved has no editor settings.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_PathWithNullCharacter_NotSpecified()
    {
        // Arrange: a resolver that must not be consulted for a path no file system accepts
        var resolver = new EditorConfigResolver(
            _ => throw new InvalidOperationException("must not be reached"),
            (_, _) => throw new InvalidOperationException("must not be reached"));

        // Act: resolve the path
        var result = resolver.Resolve("docs/a\0b.md");

        // Assert: nothing is specified and nothing is reported
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that a path without a containing directory, the root of the file system, has no editor settings.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_FileSystemRootPath_NotSpecified()
    {
        // Arrange: a resolver that must not be consulted for a path without a directory, and the file system root
        var resolver = new EditorConfigResolver(
            _ => throw new InvalidOperationException("must not be reached"),
            (_, _) => throw new InvalidOperationException("must not be reached"));
        var root = Path.GetPathRoot(Directory.GetCurrentDirectory())!;

        // Act: resolve the root
        var result = resolver.Resolve(root);

        // Assert: nothing is specified and nothing is reported
        Assert.Equal(EditorConfigResult.NotSpecified, result);
    }

    /// <summary>
    ///     Test that a null or empty path is rejected.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Resolve_NullOrEmptyPath_Throws()
    {
        // Arrange: a resolver
        var resolver = new EditorConfigResolver();

        // Act & Assert: the path is validated
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        Assert.Throws<ArgumentException>(() => resolver.Resolve(string.Empty));
    }

    /// <summary>
    ///     Test that a missing library call is rejected when the resolver is created.
    /// </summary>
    [Fact]
    public void EditorConfigResolver_Constructor_NullDelegate_ThrowsArgumentNullException()
    {
        // Act & Assert: each delegate is validated
        var chain = Assert.Throws<ArgumentNullException>(() => new EditorConfigResolver(null!, (_, _) => throw new InvalidOperationException()));
        Assert.Equal("resolveChain", chain.ParamName);
        var evaluate = Assert.Throws<ArgumentNullException>(() => new EditorConfigResolver(_ => throw new InvalidOperationException(), null!));
        Assert.Equal("evaluate", evaluate.ParamName);
    }

    /// <summary>
    ///     Test that a result cannot carry both a line ending and an error, or a blank error.
    /// </summary>
    [Fact]
    public void EditorConfigResult_Constructor_InvalidCombination_ThrowsArgumentException()
    {
        // Act & Assert: the invalid combinations are rejected
        Assert.Throws<ArgumentException>(() => new EditorConfigResult(LineEnding.Lf, "problem"));
        Assert.Throws<ArgumentException>(() => new EditorConfigResult(null, "  "));
        Assert.Throws<ArgumentException>(() => new EditorConfigResult((LineEnding)99, null));
        Assert.Equal("one two", new EditorConfigResult(null, "one\ntwo").ErrorMessage);
    }
}
