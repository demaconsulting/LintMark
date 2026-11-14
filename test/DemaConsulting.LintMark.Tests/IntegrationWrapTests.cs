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

using System.Text;
using DemaConsulting.LintMark.Utilities;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level tests of the line length rule, the paragraph wrap and the code span rule, which run the LintMark
///     application via dotnet on real files.
/// </summary>
/// <remarks>
///     Only relative paths are compared (never the resolved temporary directory), because the temporary directory
///     may be reached through a link (<c>/var</c> and <c>/private/var</c> on macOS).
/// </remarks>
[Collection("Sequential")]
public class IntegrationWrapTests
{
    /// <summary>
    ///     The path of the application DLL.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationWrapTests"/> class by locating the LintMark DLL.
    /// </summary>
    public IntegrationWrapTests()
    {
        _dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.LintMark.dll");
        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Encodes text as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>
    ///     Runs the tool with the fix option over one file and returns the file's bytes as text.
    /// </summary>
    /// <param name="directory">The temporary directory that holds the file and, when given, the configuration.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The exit code, the output and the text of the file after the run.</returns>
    private (int ExitCode, string Output, string Text) FixFile(TemporaryDirectory directory, string content, string? config = "line-length: 20\n")
    {
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteBytes("a.md", Bytes(content));
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);
        return (exitCode, output, Encoding.UTF8.GetString(File.ReadAllBytes(file)));
    }

    /// <summary>
    ///     Runs the tool with the check option over one file.
    /// </summary>
    /// <param name="directory">The temporary directory that holds the file and, when given, the configuration.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The exit code and the output; the file is checked to be unchanged.</returns>
    private (int ExitCode, string Output) CheckFile(TemporaryDirectory directory, string content, string? config = "line-length: 20\n")
    {
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteBytes("a.md", Bytes(content));
        var before = File.ReadAllBytes(file);
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--check", file);
        Assert.Equal(before, File.ReadAllBytes(file));
        return (exitCode, output);
    }

    /// <summary>
    ///     Test that the check option reports a paragraph line that is wider than the line length at the first column beyond the width.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_OverlongParagraph_ReportsLineLengthAtTheFirstColumnBeyondTheWidth()
    {
        // Arrange: a paragraph of 27 columns and a limit of 20
        using var directory = new TemporaryDirectory();

        // Act: check the file
        var (exitCode, output) = CheckFile(directory, "ant bat cat dog eel fox gnu\n");

        // Assert: the rule, the position and the width are reported, and the exit code is one
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:21: line-length Line is 27 columns wide; the limit is 20", output);
    }

    /// <summary>
    ///     Test that the fix option wraps such a paragraph so that no line is wider than the line length.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_OverlongParagraph_IsWrappedWithinTheWidth()
    {
        // Arrange and Act: fix a paragraph of 27 columns with a limit of 20
        using var directory = new TemporaryDirectory();
        var (exitCode, output, text) = FixFile(directory, "ant bat cat dog eel fox gnu\n");

        // Assert: wrapped, nothing remains to report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal("ant bat cat dog eel\nfox gnu\n", text);
    }

    /// <summary>
    ///     Test that the fix option leaves every paragraph that has no line that is too wide byte for byte as written.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_ParagraphsWithinTheWidth_StayByteIdentical()
    {
        // Arrange: ragged paragraphs and one whose line is exactly as wide as the limit
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var file = directory.WriteBytes("a.md", Bytes("a\nbat cat\nd e f\n\nant bat cat dog else\n"));
        var before = File.ReadAllBytes(file);

        // Act: fix the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: success, no report, the same bytes
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(before, File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the width comes from the line-length setting, with 120 columns by default.
    /// </summary>
    [Fact]
    public void LintMark_LineLengthSetting_ConfiguredWidth_DecidesWhatIsReported()
    {
        // Arrange: a paragraph of 99 columns
        using var defaultDirectory = new TemporaryDirectory();
        using var narrowDirectory = new TemporaryDirectory();
        var content = string.Join(' ', Enumerable.Repeat("word", 20)) + "\n";

        // Act: check with the default width and with 80 columns
        var (defaultExit, defaultOutput) = CheckFile(defaultDirectory, content, config: null);
        var (narrowExit, narrowOutput) = CheckFile(narrowDirectory, content, "line-length: 80\n");

        // Assert: nothing at 120 columns, a finding at 80
        Assert.Equal(0, defaultExit);
        Assert.DoesNotContain("line-length", defaultOutput);
        Assert.Equal(1, narrowExit);
        Assert.Contains("a.md:1:81: line-length Line is 99 columns wide; the limit is 80", narrowOutput);
    }

    /// <summary>
    ///     Test that a line length of zero reports and changes nothing.
    /// </summary>
    [Fact]
    public void LintMark_LineLengthSetting_Zero_ReportsAndChangesNothing()
    {
        // Arrange and Act: check a very long paragraph with no limit
        using var directory = new TemporaryDirectory();
        var (exitCode, output) = CheckFile(directory, string.Join(' ', Enumerable.Repeat("word", 80)) + "\n", "line-length: 0\n");

        // Assert: success and no report
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
    }

    /// <summary>
    ///     Test that switching the rule off with its legacy number reports and changes nothing.
    /// </summary>
    [Theory]
    [InlineData("MD013")]
    [InlineData("line-length")]
    public void LintMark_ConfigRules_LineLengthOffByAlias_ReportsAndChangesNothing(string name)
    {
        // Arrange and Act: fix a paragraph that is too wide with the rule switched off
        using var directory = new TemporaryDirectory();
        var (exitCode, output, text) = FixFile(directory, "ant bat cat dog eel fox gnu\n", $"line-length: 20\nrules:\n  {name}: false\n");

        // Assert: success, no report, no change
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal("ant bat cat dog eel fox gnu\n", text);
    }

    /// <summary>
    ///     Test that the width of a line is counted in display columns: a line of wide characters is reported although it has fewer
    ///     than 120 characters, and a line of combining marks is not although it has more.
    /// </summary>
    [Fact]
    public void LintMark_LineLength_WideCharacters_AreMeasuredInDisplayColumns()
    {
        // Arrange: a paragraph of 63 characters that need 123 columns, and one of 167 characters that need 87
        using var directory = new TemporaryDirectory();
        using var fixDirectory = new TemporaryDirectory();
        var wide = string.Join(' ', Enumerable.Repeat("日本語日本語日本語日本語日本語", 4));
        var combining = string.Join(' ', Enumerable.Repeat(string.Concat(Enumerable.Repeat("e\u0301", 10)), 8));
        Assert.True(wide.Length <= 120 && combining.Length > 120);

        // Act: check, then fix, with the default width
        var (exitCode, output) = CheckFile(directory, $"{wide}\n\n{combining}\n", config: null);
        var (_, _, fixedText) = FixFile(fixDirectory, $"{wide}\n\n{combining}\n", config: null);

        // Assert: only the paragraph of wide characters is reported, at its first column beyond 120, and only it is wrapped
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:62: line-length Line is 123 columns wide; the limit is 120", output);
        Assert.DoesNotContain("a.md:3", output);
        var firstFour = string.Join(' ', Enumerable.Repeat("日本語日本語日本語日本語日本語", 3));
        Assert.Equal($"{firstFour}\n日本語日本語日本語日本語日本語\n\n{combining}\n", fixedText);
    }

    /// <summary>
    ///     Test that a line that is wide only because of one link, URL, autolink, code span or image is neither reported nor changed.
    /// </summary>
    [Theory]
    [InlineData("[a link](http://example.org/very/long/path/to/x)\n")]
    [InlineData("<http://example.org/very/long/path/to/something>\n")]
    [InlineData("`a code span with spaces in it`\n")]
    [InlineData("![an image](http://example.org/image.png)\n")]
    public void LintMark_FixFlag_LineWithOneLongUnbreakableItem_IsNotReportedOrChanged(string content)
    {
        // Arrange and Act: fix the file
        using var directory = new TemporaryDirectory();
        var (exitCode, output, text) = FixFile(directory, content);

        // Assert: success, no report, the same text
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that code, HTML, tables, headings, link reference definitions and front matter that are wider than the line length are never reported.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_LiteralBlocksWiderThanTheWidth_AreNeverReported()
    {
        // Arrange: one block of each literal kind
        using var directory = new TemporaryDirectory();
        const string content =
            "---\ntitle: a long title with many words in it here\n---\n\n"
            + "# a heading with many words in it that is long\n\n"
            + "```text\ncode line with many words in it that is long\n```\n\n"
            + "<div>html block with many words in it that is long</div>\n\n"
            + "| column one is long | column two is long |\n| ------------------ | ------------------ |\n| cell with many words in it | another cell with many words |\n\n"
            + "[ref]: http://example.org/very/long/path/to/something \"a long title here\"\n";

        // Act: check the file
        var (exitCode, output) = CheckFile(directory, content);

        // Assert: nothing is reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
    }

    /// <summary>
    ///     Test that a wrapped paragraph keeps its links, code spans and inline HTML whole.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrappedParagraph_KeepsInlineConstructsWhole()
    {
        // Arrange and Act: fix a paragraph with a link, a code span and inline HTML that all have blanks inside
        using var directory = new TemporaryDirectory();
        var (exitCode, _, text) = FixFile(directory, "ant [bb cc dd](ee) fox `gg hh ii` jam <span a=\"b c\">kk</span> log\n");

        // Assert: every construct is on one line
        Assert.Equal(0, exitCode);
        Assert.Contains("[bb cc dd](ee)", text, StringComparison.Ordinal);
        Assert.Contains("`gg hh ii`", text, StringComparison.Ordinal);
        Assert.Contains("<span a=\"b c\">kk</span>", text, StringComparison.Ordinal);
        Assert.All(text.TrimEnd('\n').Split('\n'), line => Assert.True(line.Length <= 30));
    }

    /// <summary>
    ///     Test that a wrapped paragraph keeps each hard line break where it is.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrappedParagraph_KeepsHardBreaks()
    {
        // Arrange and Act: fix paragraphs with a hard break of each form
        using var spaces = new TemporaryDirectory();
        using var backslash = new TemporaryDirectory();
        var (_, _, withSpaces) = FixFile(spaces, "ant bat cat dog eel fox gnu  \nhen ink\n");
        var (_, _, withBackslash) = FixFile(backslash, "ant bat cat dog eel fox gnu\\\nhen ink\n");

        // Assert: the breaks stay at the end of their lines
        Assert.Equal("ant bat cat dog eel\nfox gnu  \nhen ink\n", withSpaces);
        Assert.Equal("ant bat cat dog eel\nfox gnu\\\nhen ink\n", withBackslash);
    }

    /// <summary>
    ///     Test that every wrapped line of a list item or a quote carries the prefix that keeps it in the item or the quote.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrappedListAndQuoteParagraphs_KeepTheirPrefixes()
    {
        // Arrange and Act: fix a list, a quote and a lazy quote
        using var directory = new TemporaryDirectory();
        var (exitCode, _, text) = FixFile(
            directory,
            "- ant bat cat dog eel fox gnu\n  - hen ink jam key log map nut\n\n> ant bat cat dog eel fox gnu\n\n> ant bat cat dog\neel fox gnu hen ink jam\n");

        // Assert: prefixes on every line
        Assert.Equal(0, exitCode);
        Assert.Equal(
            "- ant bat cat dog\n  eel fox gnu\n  - hen ink jam key\n    log map nut\n\n> ant bat cat dog\n> eel fox gnu\n\n> ant bat cat dog\n> eel fox gnu hen\n> ink jam\n",
            text);
    }

    /// <summary>
    ///     Test that no wrapped line starts with text that Markdown reads as another block, whether it is a list marker, a number marker or a heading.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel - fox gnu\n", "ant bat cat dog\neel - fox gnu\n")]
    [InlineData("ant bat cat dog eel 2026. fox gnu\n", "ant bat cat dog\neel 2026. fox gnu\n")]
    [InlineData("ant bat cat dog eel + fox gnu\n", "ant bat cat dog\neel + fox gnu\n")]
    [InlineData("ant bat cat dog eel # fox gnu\n", "ant bat cat dog\neel # fox gnu\n")]
    public void LintMark_FixFlag_WrappedParagraph_NeverStartsALineWithBlockSyntax(string content, string expected)
    {
        // Arrange and Act: fix the file
        using var directory = new TemporaryDirectory();
        var (exitCode, _, text) = FixFile(directory, content);

        // Assert: the marker stays inside the line
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Test that a paragraph with a Pandoc structural line is left as written.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_PandocStructuralParagraph_IsLeftAsWritten()
    {
        // Arrange and Act: fix a paragraph with a LaTeX command on a line of its own
        using var directory = new TemporaryDirectory();
        const string content = "ant bat cat dog eel fox gnu\n\\section\nhen\n";
        var (exitCode, output, text) = FixFile(directory, content);

        // Assert: unchanged and nothing reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a paragraph inside a footnote definition is left as written.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_FootnoteParagraph_IsLeftAsWritten()
    {
        // Arrange and Act: fix a footnote with a paragraph that is too wide
        using var directory = new TemporaryDirectory();
        const string content = "text[^1]\n\n[^1]: ant bat cat dog eel fox gnu hen\n";
        var (exitCode, output, text) = FixFile(directory, content);

        // Assert: unchanged and nothing reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a line the tool could not shorten is reported after the fix as a problem that the fix cannot repair.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrapThatCannotFit_ReportsLineLengthAfterTheFix()
    {
        // Arrange and Act: fix a list item with a lazy continuation line that is too wide and trailing whitespace on its first line
        using var directory = new TemporaryDirectory();
        var (exitCode, output, text) = FixFile(directory, "- ant bat cat dog \neel fox gnu hen ink jam\n");

        // Assert: the whitespace is fixed, the line that could not be wrapped is reported, and the exit code says so
        Assert.Equal(1, exitCode);
        Assert.Equal("- ant bat cat dog\neel fox gnu hen ink jam\n", text);
        Assert.Contains("a.md:2:21: line-length Line is 23 columns wide; the limit is 20 and the paragraph cannot be wrapped automatically", output);
        Assert.DoesNotContain("trailing-whitespace", output);
    }

    /// <summary>
    ///     Test that the sentence and semantic wrap modes are accepted and change nothing.
    /// </summary>
    [Theory]
    [InlineData("sentence")]
    [InlineData("semantic")]
    public void LintMark_ConfigWrap_SentenceAndSemantic_AreAcceptedAndChangeNothing(string mode)
    {
        // Arrange and Act: fix a paragraph that is too wide under a wrap mode that is accepted but not applied
        using var directory = new TemporaryDirectory();
        const string content = "ant bat cat dog eel fox gnu. Hen ink jam.\n";
        var (exitCode, output, text) = FixFile(directory, content, $"line-length: 20\nwrap: {mode}\n");

        // Assert: the configuration is accepted and the paragraph is unchanged
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("config-error", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a second run over wrapped files changes nothing and reports nothing.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrappedParagraphs_SecondRunReportsNothing()
    {
        // Arrange: a document with a list that moves, a quote and a long paragraph, fixed once
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var file = directory.WriteBytes("a.md", Bytes("*   ant bat cat _ddd_ eel fox gnu\n\n>   jam key log ` map ` nut owl pig\n\nrat sun toy urn van web fix yak zoo\n"));
        Assert.Equal(0, Runner.Run(out _, "dotnet", _dllPath, "--fix", file));
        var afterFirst = File.ReadAllBytes(file);

        // Act: fix again
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: success, no report, the same bytes
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(afterFirst, File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the line length rule disabled by a comment with its legacy number is neither fixed nor reported.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_LineLengthSuppressedByAlias_LeavesTheParagraph()
    {
        // Arrange and Act: fix a paragraph that is too wide inside a region that disables MD013
        using var directory = new TemporaryDirectory();
        const string content = "<!-- markdownlint-disable MD013 -->\n\nant bat cat dog eel fox gnu\n";
        var (exitCode, output, text) = FixFile(directory, content);

        // Assert: unchanged and nothing reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("line-length", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a file with CRLF line endings and a byte order mark is wrapped with the same endings and keeps its mark.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_WrappedParagraphWithCrlfAndBom_KeepsEndingsAndMark()
    {
        // Arrange: a quote with CRLF line endings and a byte order mark
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var file = directory.WriteBytes("a.md", [0xEF, 0xBB, 0xBF, .. Bytes("> ant bat cat dog eel fox gnu\r\n")]);

        // Act: fix the file
        var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: every line ends with CRLF, has the quote marker, and the mark is kept
        Assert.Equal(0, exitCode);
        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. Bytes("> ant bat cat dog\r\n> eel fox gnu\r\n")], File.ReadAllBytes(file));
    }

    /// <summary>
    ///     Test that the fix option removes the space on each side of a padded code span.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_PaddedCodeSpan_LosesItsPadding()
    {
        // Arrange and Act: fix a padded code span
        using var directory = new TemporaryDirectory();
        var (exitCode, _, text) = FixFile(directory, "a ` x ` b\n", config: null);

        // Assert: the padding is gone
        Assert.Equal(0, exitCode);
        Assert.Equal("a `x` b\n", text);
    }

    /// <summary>
    ///     Test that the check option reports a padded code span under its rule at its position.
    /// </summary>
    [Fact]
    public void LintMark_CheckFlag_PaddedCodeSpan_ReportsTheRuleAtTheCodeSpan()
    {
        // Arrange and Act: check a padded code span
        using var directory = new TemporaryDirectory();
        var (exitCode, output) = CheckFile(directory, "a ` x ` b\n", config: null);

        // Assert: the rule is reported at the opening backtick
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:4: code-span-space", output);
    }

    /// <summary>
    ///     Test that the padding of a code span whose content starts or ends with a backtick is kept.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CodeSpanWithBacktickAtAnEdge_KeepsItsPadding()
    {
        // Arrange and Act: fix code spans whose content starts or ends with a backtick
        using var directory = new TemporaryDirectory();
        const string content = "a `` `x `` and `` y` `` b\n";
        var (exitCode, output, text) = FixFile(directory, content, config: null);

        // Assert: unchanged, nothing reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("code-span-space", output);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a code span with padding on one side only, or with more than one space, is kept.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_AsymmetricOrDoublePadding_IsKept()
    {
        // Arrange and Act: fix spans with uneven padding
        using var directory = new TemporaryDirectory();
        const string content = "a ` x` and `x ` and `  x  ` b\n";
        var (exitCode, _, text) = FixFile(directory, content, config: null);

        // Assert: unchanged
        Assert.Equal(0, exitCode);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that a code span that holds only spaces is kept.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CodeSpanOfSpaces_IsKept()
    {
        // Arrange and Act: fix spans of spaces
        using var directory = new TemporaryDirectory();
        const string content = "a ` ` and `  ` and `   ` b\n";
        var (exitCode, _, text) = FixFile(directory, content, config: null);

        // Assert: unchanged
        Assert.Equal(0, exitCode);
        Assert.Equal(content, text);
    }

    /// <summary>
    ///     Test that the code span rule disabled by a comment with its legacy number is neither fixed nor reported, and cannot be switched off in the configuration.
    /// </summary>
    [Fact]
    public void LintMark_FixFlag_CodeSpanRuleDisabledByAlias_LeavesThePadding()
    {
        // Arrange and Act: fix a padded code span inside a region that disables MD038
        using var directory = new TemporaryDirectory();
        const string content = "<!-- markdownlint-disable MD038 -->\n\na ` x ` b\n";
        var (exitCode, output, text) = FixFile(directory, content, config: null);

        // Assert: unchanged and nothing reported
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("code-span-space", output);
        Assert.Equal(content, text);
    }
}
