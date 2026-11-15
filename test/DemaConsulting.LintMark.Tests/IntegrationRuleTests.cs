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

using System.Text.RegularExpressions;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     System-level tests of the formatting rule families and of the paragraph wrap, which run the LintMark
///     application via dotnet with the fix option on real files.
/// </summary>
/// <remarks>
///     Every test writes a small document, fixes it, and compares the whole result, so a rule that changes more or
///     less than it should is found. Each test also checks that a second run has nothing left to do.
/// </remarks>
[Collection("Sequential")]
public class IntegrationRuleTests
{
    /// <summary>
    ///     The path of the application DLL.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationRuleTests"/> class by locating the LintMark DLL.
    /// </summary>
    public IntegrationRuleTests()
    {
        _dllPath = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.LintMark.dll");
        Assert.True(File.Exists(_dllPath), $"Could not find LintMark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Fixes one file and returns the exit code, the output and the text of the file afterwards; a second run must find nothing.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="config">The content of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The exit code, the output and the text of the file after the first run.</returns>
    private (int ExitCode, string Output, string Text) Fix(string content, string? config = null)
    {
        // Write the file and, when given, its configuration into a directory of their own
        using var directory = new TemporaryDirectory();
        if (config is not null)
        {
            directory.WriteFile(".lintmark.yaml", config);
        }

        var file = directory.WriteFile("a.md", content);

        // Fix the file, then run the fix again over the result
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);
        var text = File.ReadAllText(file);
        Runner.Run(out var secondOutput, "dotnet", _dllPath, "--fix", file);

        // The second run changes nothing and reports nothing that the first run did not
        Assert.Equal(text, File.ReadAllText(file));
        Assert.Equal(CountFindings(output), CountFindings(secondOutput));
        return (exitCode, output, text);
    }

    /// <summary>
    ///     Counts the reported findings in an output.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <returns>The number of lines that start with a path, a line and a column.</returns>
    private static int CountFindings(string output) =>
        output.Split('\n').Count(line => Regex.IsMatch(line, @"\.md:\d+:\d+: ", RegexOptions.None, TimeSpan.FromSeconds(1)));

    /// <summary>
    ///     Test that trailing whitespace, tabs, runs of blank lines and a missing final line ending are fixed.
    /// </summary>
    [Fact]
    public void LintMark_Whitespace_TrailingTabsBlankLinesAndFinalNewline_AreFixed()
    {
        // Arrange and Act: fix a text with trailing spaces, a tab, three blank lines in a row and no final line ending
        var (exitCode, output, text) = Fix("text  \n\n\n\nwith\ttab\n\nlast");

        // Assert: one blank line is kept, the tab is spaces, and the file ends with one line ending
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("a.md:", output);
        Assert.Equal("text\n\nwith    tab\n\nlast\n", text);
    }

    /// <summary>
    ///     Test that headings are written in ATX style at the left margin with one space and surrounding blank lines.
    /// </summary>
    [Fact]
    public void LintMark_Headings_SetextClosedIndentedAndSpacing_AreNormalized()
    {
        // Arrange and Act: fix a setext heading, extra spaces, an indented heading, a missing blank line and a closed heading
        var (exitCode, _, text) = Fix("Title\n=====\n\n##  Two spaces\n\n   ## Indented\ntext\n\n## Closed ##\n");

        // Assert: every heading is an ATX heading with one space, no indent and no closing number signs
        Assert.Equal(0, exitCode);
        Assert.Equal("# Title\n\n## Two spaces\n\n## Indented\n\ntext\n\n## Closed\n", text);
    }

    /// <summary>
    ///     Test that fences, lists and tables get blank lines around them.
    /// </summary>
    [Fact]
    public void LintMark_BlockSpacing_FencesListsAndTables_GetSurroundingBlankLines()
    {
        // Arrange and Act: fix a fence, a list and a table that touch the text around them
        var (exitCode, _, text) = Fix("text\n```text\ncode\n```\n- a\n- b\n\nx\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\nafter\n");

        // Assert: a blank line separates every block
        Assert.Equal(0, exitCode);
        Assert.Equal("text\n\n```text\ncode\n```\n\n- a\n- b\n\nx\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\nafter\n", text);
    }

    /// <summary>
    ///     Test that an indented code block and a tilde fence are written as backtick fences.
    /// </summary>
    [Fact]
    public void LintMark_CodeBlocks_IndentedAndTildeFence_AreWrittenAsBacktickFences()
    {
        // Arrange and Act: fix an indented code block and a tilde fence
        var (exitCode, output, text) = Fix("    indented\n\n~~~text\nx\n~~~\n");

        // Assert: both are backtick fences, and the converted block that has no language is reported
        Assert.Equal(1, exitCode);
        Assert.Equal("```\nindented\n```\n\n```text\nx\n```\n", text);
        Assert.Contains("a.md:1:1: code-fence-language", output);
    }

    /// <summary>
    ///     Test that thematic breaks are written as three dashes.
    /// </summary>
    [Fact]
    public void LintMark_ThematicBreaks_StarsAndUnderscores_AreWrittenAsDashes()
    {
        // Arrange and Act: fix a break of stars and a break of underscores
        var (exitCode, _, text) = Fix("a\n\n***\n\n___\n\nb\n");

        // Assert: both are dashes
        Assert.Equal(0, exitCode);
        Assert.Equal("a\n\n---\n\n---\n\nb\n", text);
    }

    /// <summary>
    ///     Test that the bullet marker, the numbering, the indentation and the space after the marker are normalized.
    /// </summary>
    [Fact]
    public void LintMark_Lists_BulletOrderedIndentAndMarkerSpace_AreNormalized()
    {
        // Arrange and Act: fix star bullets, a nested list indented by four, an ordered list with a repeated number and a wide marker space
        var (exitCode, _, text) = Fix("* a\n* b\n    * c\n\n3) x\n3) y\n\n-   spaced\n");

        // Assert: dashes, nesting by two, incrementing numbers that keep their start and delimiter, one space after the marker
        Assert.Equal(0, exitCode);
        Assert.Equal("- a\n- b\n  - c\n\n3) x\n4) y\n\n- spaced\n", text);
    }

    /// <summary>
    ///     Test that two lists that a different marker keeps apart are never merged into one list.
    /// </summary>
    [Fact]
    public void LintMark_Lists_AdjacentListsWithDifferentMarkers_AreNeverMerged()
    {
        // Arrange and Act: fix a dash list followed by a star list
        var (exitCode, _, text) = Fix("- a\n- b\n\n* c\n* d\n");

        // Assert: the two lists keep their own markers, so they are still two lists
        Assert.Equal(0, exitCode);
        Assert.Equal("- a\n- b\n\n* c\n* d\n", text);
    }

    /// <summary>
    ///     Test that exactly one space follows the block quote marker.
    /// </summary>
    [Fact]
    public void LintMark_BlockQuotes_ExtraSpacesAfterMarker_AreReducedToOne()
    {
        // Arrange and Act: fix a quote with two and three spaces after the marker
        var (exitCode, _, text) = Fix(">  more\n>   text\n");

        // Assert: one space after each marker
        Assert.Equal(0, exitCode);
        Assert.Equal("> more\n> text\n", text);
    }

    /// <summary>
    ///     Test that emphasis, strong text, a bare URL and a padded code span are written in the canonical form.
    /// </summary>
    [Fact]
    public void LintMark_InlineStyle_EmphasisStrongBareUrlAndCodeSpan_AreNormalized()
    {
        // Arrange and Act: fix underscore emphasis and strong text, a bare URL and a padded code span
        var (exitCode, _, text) = Fix("some __strong__ and _em_ text https://example.com/x and ` code ` end\n");

        // Assert: asterisks, an autolink and a code span without padding
        Assert.Equal(0, exitCode);
        Assert.Equal("some **strong** and *em* text <https://example.com/x> and `code` end\n", text);
    }

    /// <summary>
    ///     Test that a table is written compact by default and aligned when the configuration asks for it.
    /// </summary>
    [Fact]
    public void LintMark_Tables_DefaultCompactAndAlignedSetting_AreLaidOut()
    {
        // Arrange and Act: fix the same table without pipes at its ends, with the default layout and with the aligned layout
        const string table = "a|b\n-|-\nlonger cell|x\n\ntext\n";
        var (compactExit, _, compact) = Fix(table);
        var (alignedExit, _, aligned) = Fix(table, "tables: aligned\n");

        // Assert: both have pipes at the ends; only the aligned layout pads the cells
        Assert.Equal(0, compactExit);
        Assert.Equal("| a | b |\n| --- | --- |\n| longer cell | x |\n\ntext\n", compact);
        Assert.Equal(0, alignedExit);
        Assert.Equal("| a           | b   |\n| ----------- | --- |\n| longer cell | x   |\n\ntext\n", aligned);
    }

    /// <summary>
    ///     Test that an inline link longer than the threshold becomes a reference link when the conversion is enabled, and
    ///     is left alone by default.
    /// </summary>
    [Fact]
    public void LintMark_LinkRefactor_LongInlineLink_BecomesReferenceLinkOnlyWhenEnabled()
    {
        // Arrange and Act: fix the same paragraph with the conversion enabled and with the default
        const string paragraph = "Read the [documentation page for the tool](https://example.com/docs/tool/overview) first.\n";
        var (enabledExit, _, enabled) = Fix(paragraph, "link-refactor:\n  enabled: true\n  threshold: 40\n");
        var (defaultExit, _, byDefault) = Fix(paragraph);

        // Assert: the enabled run moves the destination to a definition block with a label; the default run keeps the link
        Assert.Equal(0, enabledExit);
        Assert.Equal(
            "Read the [documentation page for the tool][documentation-page-for-the-tool] first.\n\n<!-- Link References -->\n[documentation-page-for-the-tool]: https://example.com/docs/tool/overview\n",
            enabled);
        Assert.Equal(0, defaultExit);
        Assert.Equal(paragraph, byDefault);
    }

    /// <summary>
    ///     Test that the sentence mode puts every sentence on its own line and the semantic mode breaks after clauses.
    /// </summary>
    [Fact]
    public void LintMark_WrapModes_SentenceAndSemantic_PlaceLineBreaksAtBoundaries()
    {
        // Arrange and Act: fix a paragraph of three sentences in the sentence mode and one of three clauses in the semantic mode
        var (sentenceExit, _, sentence) = Fix("First sentence here. Second one is here. Third.\n", "wrap: sentence\n");
        var (semanticExit, _, semantic) = Fix("First clause here, second clause there; third part and more.\n", "wrap: semantic\nline-length: 30\n");

        // Assert: a line per sentence, and a line per clause
        Assert.Equal(0, sentenceExit);
        Assert.Equal("First sentence here.\nSecond one is here.\nThird.\n", sentence);
        Assert.Equal(0, semanticExit);
        Assert.Equal("First clause here,\nsecond clause there;\nthird part and more.\n", semantic);
    }

    /// <summary>
    ///     Test that the paragraph-wrap rule reports a paragraph that is not laid out as asked, and can be switched off.
    /// </summary>
    [Fact]
    public void LintMark_WrapModes_ParagraphWrapRule_ReportsLayoutAndCanBeDisabled()
    {
        // Arrange: a paragraph on one line, configured for the sentence mode, and the same with the rule switched off
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "wrap: sentence\n");
        var file = directory.WriteFile("a.md", "First sentence here. Second one is here. Third.\n");

        // Act: check with the rule on, then with the rule switched off by the command line
        var enabledExit = Runner.Run(out var enabledOutput, "dotnet", _dllPath, "--check", file);
        var disabledExit = Runner.Run(out var disabledOutput, "dotnet", _dllPath, "--check", "--disable", "paragraph-wrap", file);

        // Assert: the layout is reported once and not when the rule is off
        Assert.Equal(1, enabledExit);
        Assert.Contains("a.md:1:1: paragraph-wrap", enabledOutput);
        Assert.Equal(0, disabledExit);
        Assert.DoesNotContain("a.md:", disabledOutput);
    }

    /// <summary>
    ///     Test that wrapping keeps links and code spans whole, keeps hard breaks, and never starts a line with block syntax.
    /// </summary>
    [Fact]
    public void LintMark_WrapSafety_AtomsHardBreaksAndBlockSyntax_AreKeptIntact()
    {
        // Arrange and Act: fix a paragraph with a link and a code span, one with a hard break, and one with words that would start a block
        var (linkExit, _, links) = Fix("see [a long link text here](https://example.com/a/very/long/url) and `code span here` ok done now more\n", "line-length: 30\n");
        var (breakExit, _, hardBreak) = Fix("word word word word  \nnext line is here and goes on and on past the limit yes\n", "line-length: 20\n");
        var (guardExit, _, guarded) = Fix("aaa bbb ccc 1. ddd eee - fff # ggg > hhh\n", "line-length: 11\n");

        // Assert: the link and the code span stay on one line, the hard break stays, and no line starts a list, heading or quote
        Assert.Equal(0, linkExit);
        Assert.Equal("see\n[a long link text here](https://example.com/a/very/long/url)\nand `code span here` ok done\nnow more\n", links);
        Assert.Equal(0, breakExit);
        Assert.StartsWith("word word word word  \nnext line is here\n", hardBreak, StringComparison.Ordinal);
        Assert.Equal(0, guardExit);
        Assert.Equal("aaa bbb ccc 1. ddd eee - fff # ggg > hhh", string.Join(' ', guarded.Split('\n', StringSplitOptions.RemoveEmptyEntries)));
        Assert.All(
            guarded.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.DoesNotMatch(@"^(\d+\.|-|#|>)(\s|$)", line));
    }

    /// <summary>
    ///     Test that an overflow that cannot be fixed safely is reported and the paragraph is left as written.
    /// </summary>
    [Fact]
    public void LintMark_WrapSafety_LazyListContinuation_IsReportedAndLeftAsWritten()
    {
        // Arrange: a list item with a lazy continuation line that is wider than the limit
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        const string content = "- item one is long enough\nto wrap around the limit set here\n";
        var file = directory.WriteFile("a.md", content);

        // Act: fix the file
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--fix", file);

        // Assert: the overflow is reported with the reason and the file is as written
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md:1:21: line-length Line is 25 columns wide; the limit is 20 and the paragraph cannot be wrapped automatically", output);
        Assert.Equal(content, File.ReadAllText(file));
    }

    /// <summary>
    ///     Test that the line length counts display columns, so wide characters count as two.
    /// </summary>
    [Fact]
    public void LintMark_DisplayWidth_WideCharacters_CountAsTwoColumns()
    {
        // Arrange: nine wide characters, a space and three letters: 13 characters but 22 columns, with a limit of 20
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 20\n");
        var file = directory.WriteFile("a.md", "日本語日本語日本語 abc\n");

        // Act: check, then fix the file
        Runner.Run(out var checkOutput, "dotnet", _dllPath, "--check", file);
        Runner.Run(out _, "dotnet", _dllPath, "--fix", file);

        // Assert: the width is 22 columns and the last word moves to its own line
        Assert.Contains("a.md:1:12: line-length Line is 22 columns wide; the limit is 20", checkOutput);
        Assert.Equal("日本語日本語日本語\nabc\n", File.ReadAllText(file));
    }
}
