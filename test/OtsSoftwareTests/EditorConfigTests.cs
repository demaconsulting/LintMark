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

using EditorConfig.Core;

namespace OtsSoftwareTests;

/// <summary>
///     Integration tests of the editorconfig OTS package: they pin the resolution behavior that
///     LintMark's line-ending resolution relies on, including the places where the library's typed
///     accessors differ from the raw property values.
/// </summary>
public sealed class EditorConfigTests : IDisposable
{
    /// <summary>
    ///     Root of the temporary tree used by one test; it holds a <c>root = true</c> file so the upward
    ///     search never reaches an <c>.editorconfig</c> outside the tree.
    /// </summary>
    private readonly string _root;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorConfigTests"/> class with an isolated tree.
    /// </summary>
    public EditorConfigTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lintmark-ots-ec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    ///     Deletes the temporary tree.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temporary tree that cannot be removed must not fail the test
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort, as above
        }
    }

    /// <summary>
    ///     Writes a file below the temporary root, creating parent directories.
    /// </summary>
    /// <param name="relativePath">Relative path with forward slashes.</param>
    /// <param name="content">File content.</param>
    private void Write(string relativePath, string content)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    ///     Resolves the raw <c>end_of_line</c> property of a file.
    /// </summary>
    /// <param name="file">Full path of the file (it need not exist).</param>
    /// <returns>The raw property value, or <see langword="null"/> when the property is not set.</returns>
    private static string? RawEndOfLine(string file) =>
        new EditorConfigParser().Parse(file).Properties.TryGetValue("end_of_line", out var value) ? value : null;

    /// <summary>
    ///     Validates the documented glob forms against the files they must and must not match.
    /// </summary>
    /// <param name="pattern">Section header glob.</param>
    /// <param name="file">File path relative to the directory holding the <c>.editorconfig</c>.</param>
    /// <param name="expected">Whether the section is expected to apply.</param>
    [Theory]
    [InlineData("*.md", "a.md", true)]
    [InlineData("*.md", "sub/a.md", true)]
    [InlineData("*.md", "a.txt", false)]
    [InlineData("a?.md", "ab.md", true)]
    [InlineData("a?.md", "abc.md", false)]
    [InlineData("[ab].md", "a.md", true)]
    [InlineData("[ab].md", "c.md", false)]
    [InlineData("[!ab].md", "c.md", true)]
    [InlineData("[!ab].md", "a.md", false)]
    [InlineData("{a,b}.md", "b.md", true)]
    [InlineData("{a,b}.md", "c.md", false)]
    [InlineData("f{1..3}.md", "f2.md", true)]
    [InlineData("f{1..3}.md", "f4.md", false)]
    [InlineData("docs/*.md", "docs/a.md", true)]
    [InlineData("docs/*.md", "docs/sub/a.md", false)]
    [InlineData("docs/*.md", "other/docs/a.md", false)]
    [InlineData("/top.md", "top.md", true)]
    [InlineData("/top.md", "sub/top.md", false)]
    [InlineData("**/deep/**.md", "a/deep/x.md", true)]
    [InlineData("**/deep/**.md", "a/other/x.md", false)]
    [InlineData("\\[x\\].md", "[x].md", true)]
    [InlineData("\\[x\\].md", "x.md", false)]
    public void EditorConfig_Parser_GlobPatterns_MatchPerSpecification(string pattern, string file, bool expected)
    {
        // Arrange: a root file whose only section sets crlf for the pattern
        Write(".editorconfig", $"root = true\n\n[{pattern}]\nend_of_line = crlf\n");
        var path = Path.GetFullPath(Path.Combine(_root, file));

        // Act: resolve the raw property
        var value = RawEndOfLine(path);

        // Assert: the property is present exactly when the pattern is specified to match
        Assert.Equal(expected ? "crlf" : null, value);
    }

    /// <summary>
    ///     Validates that section patterns are case-sensitive, independent of the platform.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_SectionPatternCase_IsCaseSensitive()
    {
        // Arrange: a lower-case section pattern and an upper-case one
        Write(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n\n[*.TXT]\nend_of_line = crlf\n");

        // Act: resolve a file whose name differs from each pattern only by case
        var upperMarkdown = RawEndOfLine(Path.Combine(_root, "A.MD"));
        var lowerText = RawEndOfLine(Path.Combine(_root, "a.txt"));
        var exactText = RawEndOfLine(Path.Combine(_root, "a.TXT"));

        // Assert: only the exact-case spelling matches
        Assert.Null(upperMarkdown);
        Assert.Null(lowerText);
        Assert.Equal("crlf", exactText);
    }

    /// <summary>
    ///     Validates that property names and values are matched without regard to case.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_PropertyAndValueCase_AreInsensitive()
    {
        // Arrange: an upper-case key with a mixed-case value
        Write(".editorconfig", "root = true\n\n[*]\nEND_OF_LINE = Crlf\n");

        // Act: resolve the raw property by its lower-case name
        var value = RawEndOfLine(Path.Combine(_root, "a.md"));

        // Assert: the key is found, and the value is reported in lower case
        Assert.Equal("crlf", value);
    }

    /// <summary>
    ///     Validates that a later section in one file overrides an earlier one.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_LessSpecificLaterSection_OverridesEarlier()
    {
        // Arrange: a specific section followed by a broader one
        Write(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n\n[*]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var value = RawEndOfLine(Path.Combine(_root, "a.md"));

        // Assert: the later section wins even though it is less specific
        Assert.Equal("crlf", value);
    }

    /// <summary>
    ///     Validates that a nearer file overrides a farther file property by property.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_NestedFiles_NearerFileOverridesFarther()
    {
        // Arrange: a parent file setting two properties and a nearer file overriding one of them
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = lf\nindent_style = space\n");
        Write("sub/.editorconfig", "[*]\nend_of_line = crlf\n");

        // Act: resolve a file in the nearer directory
        var configuration = new EditorConfigParser().Parse(Path.Combine(_root, "sub", "a.md"));

        // Assert: the nearer value wins, and the unset property is inherited
        Assert.Equal("crlf", configuration.Properties["end_of_line"]);
        Assert.Equal("space", configuration.Properties["indent_style"]);
    }

    /// <summary>
    ///     Validates that a file declaring root stops the upward search.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_RootTrue_StopsSearch()
    {
        // Arrange: a farther file with a value, and a nearer root file without one
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = crlf\n");
        Write("sub/.editorconfig", "root = true\n\n[*]\nindent_style = tab\n");

        // Act: resolve a file below the nearer root
        var configuration = new EditorConfigParser().Parse(Path.Combine(_root, "sub", "a.md"));

        // Assert: the farther value is not visible
        Assert.False(configuration.Properties.ContainsKey("end_of_line"));
        Assert.Equal("tab", configuration.Properties["indent_style"]);
    }

    /// <summary>
    ///     Validates that resolving a directory chain once and parsing files against it equals a direct parse.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_DirectoryChain_ParseWithChain_EqualsDirectParse()
    {
        // Arrange: nested files with different sections, and a parser
        Write(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n\n[*.txt]\nend_of_line = crlf\n");
        Write("sub/.editorconfig", "[a.md]\nindent_style = tab\n");
        var parser = new EditorConfigParser();
        var directory = Path.Combine(_root, "sub");
        var chain = parser.GetResolvedChainForDirectory(directory);

        // Act: parse several files against the chain and directly
        foreach (var name in new[] { "a.md", "b.md", "c.txt" })
        {
            var file = Path.Combine(directory, name);
            var viaChain = parser.Parse(file, chain).Properties;
            var direct = parser.Parse(file).Properties;

            // Assert: identical property sets
            Assert.Equal(direct.OrderBy(p => p.Key, StringComparer.Ordinal), viaChain.OrderBy(p => p.Key, StringComparer.Ordinal));
        }
    }

    /// <summary>
    ///     Validates that the raw property of a value set to unset is the word unset.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_UnsetValue_RawPropertyIsUnset()
    {
        // Arrange: a nearer file that unsets a value defined by a farther file
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = crlf\n");
        Write("sub/.editorconfig", "[*]\nend_of_line = unset\n");

        // Act: resolve the raw property
        var value = RawEndOfLine(Path.Combine(_root, "sub", "a.md"));

        // Assert: the raw value is the word unset, so a caller can recognize it
        Assert.Equal("unset", value);
    }

    /// <summary>
    ///     Pins why the raw property is read: the typed accessor does not report unset or unknown values
    ///     as absent.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_TypedEndOfLine_IgnoresUnsetAndInvalidValues()
    {
        // Arrange: a farther crlf value, and nearer files that set unset and an unknown word
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = crlf\n");
        Write("unset/.editorconfig", "[*]\nend_of_line = unset\n");
        Write("invalid/.editorconfig", "[*]\nend_of_line = banana\n");
        var parser = new EditorConfigParser();

        // Act: resolve both through the typed accessor and the raw dictionary
        var unset = parser.Parse(Path.Combine(_root, "unset", "a.md"));
        var invalid = parser.Parse(Path.Combine(_root, "invalid", "a.md"));

        // Assert: the raw dictionary holds the nearer value while the typed accessor still reports the
        // farther one, which is why LintMark interprets the raw value itself
        Assert.Equal("unset", unset.Properties["end_of_line"]);
        Assert.Equal("banana", invalid.Properties["end_of_line"]);
        Assert.Equal(EndOfLine.CRLF, unset.EndOfLine);
        Assert.Equal(EndOfLine.CRLF, invalid.EndOfLine);
    }

    /// <summary>
    ///     Validates that a carriage-return-only value is available through both the typed and the raw view.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_EndOfLineCr_TypedAccessorReportsCr()
    {
        // Arrange: a section selecting the classic Mac line ending
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = cr\n");

        // Act: resolve the file
        var configuration = new EditorConfigParser().Parse(Path.Combine(_root, "a.md"));

        // Assert: the value is recognized as cr in both views
        Assert.Equal(EndOfLine.CR, configuration.EndOfLine);
        Assert.Equal("cr", configuration.Properties["end_of_line"]);
    }

    /// <summary>
    ///     Validates that a file that does not exist, in a directory that does not exist, is still resolved.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_NonexistentFile_StillResolved()
    {
        // Arrange: a root file with a catch-all section
        Write(".editorconfig", "root = true\n\n[*]\nend_of_line = lf\n");

        // Act: resolve a path in a missing directory
        var value = RawEndOfLine(Path.Combine(_root, "missing", "deeper", "a.md"));

        // Assert: the section still applies
        Assert.Equal("lf", value);
    }

    /// <summary>
    ///     Validates that malformed lines are ignored and later valid sections still apply.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_MalformedLines_AreIgnored()
    {
        // Arrange: garbage lines before and between valid sections
        Write(".editorconfig", "root = true\n\n[[[\nend_of_line\n=\ngarbage here\n\n[*.md]\nend_of_line = crlf\n");

        // Act: resolve a Markdown file
        var value = RawEndOfLine(Path.Combine(_root, "a.md"));

        // Assert: no exception, and the valid section applies
        Assert.Equal("crlf", value);
    }

    /// <summary>
    ///     Validates that one parser instance can be used by many threads with identical results.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_SharedParser_ParallelParse_IdenticalResults()
    {
        // Arrange: a tree with several sections, one shared parser and the sequential expectation
        Write(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n\n[docs/*.md]\nend_of_line = crlf\n");
        Write("docs/.editorconfig", "[b.md]\nend_of_line = lf\n");
        var parser = new EditorConfigParser();
        var files = Enumerable.Range(0, 64)
            .Select(i => Path.Combine(_root, i % 2 == 0 ? "docs" : "other", $"f{i % 4}.md"))
            .ToArray();
        var expected = files.Select(f => RawEndOfLine(f)).ToArray();

        // Act: resolve every file many times from many threads
        var actual = new string?[files.Length * 20];
        Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index =>
        {
            var file = files[index % files.Length];
            actual[index] = parser.Parse(file).Properties.TryGetValue("end_of_line", out var value) ? value : null;
        });

        // Assert: every concurrent result equals the sequential one
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i % files.Length], actual[i]);
        }
    }

    /// <summary>
    ///     Validates that a parser per thread produces the same results as sequential parsing.
    /// </summary>
    [Fact]
    public void EditorConfig_Parser_ParserPerThread_ParallelParse_IdenticalResults()
    {
        // Arrange: a tree, the files to resolve and the sequential expectation
        Write(".editorconfig", "root = true\n\n[*.md]\nend_of_line = lf\n\n[docs/*.md]\nend_of_line = crlf\n");
        var files = Enumerable.Range(0, 32)
            .Select(i => Path.Combine(_root, i % 2 == 0 ? "docs" : "other", $"f{i}.md"))
            .ToArray();
        var expected = files.Select(f => RawEndOfLine(f)).ToArray();

        // Act: resolve every file from many threads, each call with its own parser
        var actual = new string?[files.Length * 20];
        Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index =>
        {
            actual[index] = RawEndOfLine(files[index % files.Length]);
        });

        // Assert: every concurrent result equals the sequential one
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i % files.Length], actual[i]);
        }
    }
}
