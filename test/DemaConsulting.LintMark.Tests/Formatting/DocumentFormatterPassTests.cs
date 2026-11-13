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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the DocumentFormatter class applies the document-level rules (trailing whitespace, tabs, blank
///     lines, block spacing and the end of the file) to every document, leaves protected content alone, and
///     reports each edit as a violation.
/// </summary>
public class DocumentFormatterPassTests
{
    /// <summary>
    ///     Asserts that a formatter without block formatters turns the input into the expected text, safely and stably.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertPasses(string input, string expected, LintMarkConfig? config = null) =>
        Assert.Equal(expected, BlockFormatterAssertions.AssertSafeAndStable(input, config, new DocumentFormatter()));

    /// <summary>
    ///     Test that trailing whitespace is removed from paragraph lines.
    /// </summary>
    [Theory]
    [InlineData("text \nmore  \n", "text\nmore\n")]
    [InlineData("text\t\nmore\n", "text\nmore\n")]
    [InlineData("only line   \n", "only line\n")]
    [InlineData("- item  \n- item two \n", "- item\n- item two\n")]
    [InlineData("> quote \n> more\t\n", "> quote\n> more\n")]
    public void DocumentFormatter_Format_TrailingWhitespaceInParagraph_IsRemoved(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a hard break written with spaces keeps exactly two spaces.
    /// </summary>
    [Theory]
    [InlineData("one   \ntwo\n", "one  \ntwo\n")]
    [InlineData("one      \ntwo\n", "one  \ntwo\n")]
    [InlineData("one  \ntwo\n", "one  \ntwo\n")]
    [InlineData("one \ntwo\n", "one\ntwo\n")]
    [InlineData("[link  \ntext](http://example.com)\n", "[link  \ntext](http://example.com)\n")]
    [InlineData("*emphasis   \nacross*\n", "*emphasis  \nacross*\n")]
    public void DocumentFormatter_Format_HardBreakWithSpaces_KeepsExactlyTwo(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a backslash hard break and a backslash before whitespace are kept.
    /// </summary>
    [Theory]
    [InlineData("one\\\ntwo\n")]
    [InlineData("foo\\ \nbar\n")]
    [InlineData("a\t  \nb\n")]
    public void DocumentFormatter_Format_BackslashAndMixedBreaks_AreKept(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that whitespace inside code spans, inline HTML and link titles is kept.
    /// </summary>
    [Theory]
    [InlineData("text `a  \nb` end\n")]
    [InlineData("a <span  \nclass=\"x\">b</span>\n")]
    [InlineData("[a](http://x \"t  \nu\")\n")]
    [InlineData("a $x  \ny$ b\n")]
    public void DocumentFormatter_Format_ProtectedInlineSpans_KeepTheirWhitespace(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that table rows and single-line definitions lose trailing whitespace.
    /// </summary>
    [Theory]
    [InlineData("| a | b |  \n|---|---|\n| 1 | 2 |   \n", "| a | b |\n|---|---|\n| 1 | 2 |\n")]
    [InlineData("[a]: http://x  \n", "[a]: http://x\n")]
    public void DocumentFormatter_Format_TableRowsAndDefinitions_LoseTrailingWhitespace(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a line that holds only a quote marker loses its trailing whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MarkerLines_LoseTrailingWhitespace() =>
        AssertPasses("> text\n>   \n> more\n", "> text\n>\n> more\n");

    /// <summary>
    ///     Test that headings that no block formatter claims lose trailing whitespace too.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_UnclaimedHeading_LosesTrailingWhitespace() =>
        AssertPasses("# Title  \n\nSetext  \n===  \n", "# Title\n\nSetext\n===\n");

    /// <summary>
    ///     Test that code, HTML, math, front matter and grid tables keep their whitespace.
    /// </summary>
    [Theory]
    [InlineData("```\ncode  \n\ttab\n```\n")]
    [InlineData("~~~\ncode  \n~~~  \n")]
    [InlineData("    code  \n")]
    [InlineData("<div>\nx  \n\ty\n</div>\n")]
    [InlineData("$$\nx  \n$$\n")]
    [InlineData("---\ntitle: x  \n---\n\ntext\n")]
    [InlineData("+---+---+\n| a | b |  \n+---+---+\n")]
    public void DocumentFormatter_Format_ProtectedBlocks_KeepTheirWhitespace(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that a tab in plain text becomes spaces up to the next tab stop of four columns.
    /// </summary>
    [Theory]
    [InlineData("a\tb\n", "a   b\n")]
    [InlineData("ab\tc\n", "ab  c\n")]
    [InlineData("abcd\te\n", "abcd    e\n")]
    [InlineData("- a\tb\n", "- a b\n")]
    [InlineData("a\t\tb\n", "a       b\n")]
    [InlineData("text\nmore\ttext\n", "text\nmore    text\n")]
    public void DocumentFormatter_Format_TabInText_ExpandsToTheTabStop(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that tabs in indentation, code spans, link destinations, tables and a grid table lookalike are kept.
    /// </summary>
    [Theory]
    [InlineData("text\n\tmore\n")]
    [InlineData("a `b\tc` d\n")]
    [InlineData("[a](http://x\t\"t\")\n")]
    [InlineData("| a\tb |\n|---|\n| 1 | 2 |\n")]
    [InlineData("+-\t--+\n")]
    [InlineData("```\na\tb\n```\n")]
    public void DocumentFormatter_Format_TabsThatMustStay_AreKept(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that more blank lines than allowed collapse to the configured number.
    /// </summary>
    [Theory]
    [InlineData("a\n\n\n\nb\n", 1, "a\n\nb\n")]
    [InlineData("a\n\n\n\nb\n", 2, "a\n\n\nb\n")]
    [InlineData("a\n\n\n\nb\n", 3, "a\n\n\n\nb\n")]
    [InlineData("a\n\n\nb\n", 2, "a\n\n\nb\n")]
    [InlineData("a\n\n\nb\n\n\n\n\nc\n", 1, "a\n\nb\n\nc\n")]
    [InlineData("\n\n\ntext\n", 1, "\ntext\n")]
    public void DocumentFormatter_Format_BlankRuns_CollapseToTheConfiguredMaximum(string input, int maximum, string expected) =>
        AssertPasses(input, expected, LintMarkConfig.Default with { MaxBlankLines = maximum });

    /// <summary>
    ///     Test that whitespace-only lines between blocks become empty lines.
    /// </summary>
    [Theory]
    [InlineData("a\n  \nb\n", "a\n\nb\n")]
    [InlineData("a\n \t \n  \nb\n", "a\n\nb\n")]
    [InlineData("a\n\t\nb\n", "a\n\nb\n")]
    public void DocumentFormatter_Format_WhitespaceOnlyLines_BecomeEmpty(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that blank lines inside code and HTML blocks are content and stay.
    /// </summary>
    [Theory]
    [InlineData("```\na\n\n\n\nb\n```\n")]
    [InlineData("<!--\n\n\n\nx\n-->\n")]
    [InlineData("$$\na\n\n\n\nb\n$$\n")]
    [InlineData("---\na: 1\n\n\n\nb: 2\n---\n\ntext\n")]
    public void DocumentFormatter_Format_BlankLinesInsideBlocks_AreKept(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that blank lines inside a list or quote are kept, and only their whitespace is removed.
    /// </summary>
    [Theory]
    [InlineData("- a\n\n\n- b\n", "- a\n\n\n- b\n")]
    [InlineData("- a\n  \n  \n- b\n", "- a\n\n\n- b\n")]
    [InlineData("1. a\n\n\n\n2. b\n", "1. a\n\n\n\n2. b\n")]
    public void DocumentFormatter_Format_BlankRunsInContainer_AreKeptWithoutWhitespace(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that the blank lines after an unclosed fence are content of the fence and stay.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_BlankLinesAfterUnclosedFence_AreKept() =>
        AssertPasses("```\ncode\n\n\n\n", "```\ncode\n\n\n\n");

    /// <summary>
    ///     Test that a blank line is inserted between a heading and the paragraph before or after it.
    /// </summary>
    [Theory]
    [InlineData("text\n# H\n", "text\n\n# H\n")]
    [InlineData("# H\ntext\n", "# H\n\ntext\n")]
    [InlineData("a\n# H\nb\n", "a\n\n# H\n\nb\n")]
    [InlineData("# One\n## Two\n", "# One\n\n## Two\n")]
    [InlineData("---\ntitle: x\n---\n# H\n", "---\ntitle: x\n---\n\n# H\n")]
    [InlineData("- a\n# H\n", "- a\n\n# H\n")]
    [InlineData("<!-- c -->\n# H\n", "<!-- c -->\n\n# H\n")]
    public void DocumentFormatter_Format_HeadingWithoutBlankLines_GetsThem(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a blank line is inserted around a fenced code block, and around an indented block that a
    ///     block formatter will turn into one.
    /// </summary>
    [Theory]
    [InlineData("text\n```\ncode\n```\ntext\n", "text\n\n```\ncode\n```\n\ntext\n")]
    [InlineData("- a\n```\ncode\n```\n", "- a\n\n```\ncode\n```\n")]
    [InlineData("```\ncode\n```\n---\n", "```\ncode\n```\n\n---\n")]
    [InlineData("    code\ntext\n", "    code\n\ntext\n")]
    public void DocumentFormatter_Format_CodeBlockWithoutBlankLines_GetsThem(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a blank line is inserted around a list.
    /// </summary>
    [Theory]
    [InlineData("text\n- a\n- b\n", "text\n\n- a\n- b\n")]
    [InlineData("text\n1. a\n2. b\n", "text\n\n1. a\n2. b\n")]
    [InlineData("- a\n> quote\n", "- a\n\n> quote\n")]
    [InlineData("> quote\n- a\n", "> quote\n\n- a\n")]
    public void DocumentFormatter_Format_ListWithoutBlankLines_GetsThem(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a blank line is inserted around a pipe table.
    /// </summary>
    [Theory]
    [InlineData("text\n| a | b |\n|---|---|\n| 1 | 2 |\n", "text\n\n| a | b |\n|---|---|\n| 1 | 2 |\n")]
    [InlineData("| a |\n|---|\n| 1 |\n# H\n", "| a |\n|---|\n| 1 |\n\n# H\n")]
    [InlineData("| a |\n|---|\n| 1 |\n***\n", "| a |\n|---|\n| 1 |\n\n***\n")]
    public void DocumentFormatter_Format_TableWithoutBlankLines_GetsThem(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that text that only looks like it needs a blank line (a lazy continuation, a number that cannot start
    ///     a list there, a table row) is not touched.
    /// </summary>
    [Theory]
    [InlineData("text\n2. a\n")]
    [InlineData("- a\ntext\n")]
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |\nno pipe here\n")]
    [InlineData("text\n    not code\n")]
    public void DocumentFormatter_Format_TextThatIsNotAnotherBlock_IsNotSeparated(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that no blank line is inserted next to blocks inside a list item or a quote.
    /// </summary>
    [Theory]
    [InlineData("- a\n  ```\n  code\n  ```\n- b\n")]
    [InlineData("> # H\n> text\n")]
    [InlineData("- # H\n  text\n")]
    [InlineData("> text\n> - a\n> - b\n")]
    public void DocumentFormatter_Format_BlocksInsideContainers_AreNotSeparated(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that an unclosed fence is not separated from its neighbors.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_UnclosedFence_IsNotSeparated() =>
        AssertPasses("text\n```\ncode\n", "text\n```\ncode\n");

    /// <summary>
    ///     Test that exactly one blank line is inserted at a boundary where two rules apply, and the first rule is credited.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_BoundaryOfTwoRules_GetsOneBlankLineAndOneViolation()
    {
        // Arrange: a heading directly followed by a fenced code block (heading and fence rule)
        const string input = "# H\n```\ncode\n```\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), input);

        // Assert: one insertion, credited to the heading rule, at the start of the second line
        Assert.Equal("# H\n\n```\ncode\n```\n", formatted);
        var edit = Assert.Single(result.Edits);
        Assert.Equal("heading-blank-lines", edit.RuleId);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(("heading-blank-lines", 2, 1), (diagnostic.RuleId, diagnostic.Line, diagnostic.Column));
    }

    /// <summary>
    ///     Test that the next applicable rule inserts the blank line when the first is suppressed.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_HeadingRuleSuppressed_FenceRuleInsertsTheBlankLine()
    {
        // Arrange: the heading rule is disabled by its legacy alias for the whole document
        const string input = "<!-- markdownlint-disable MD022 -->\n\n# H\n```\ncode\n```\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), input);

        // Assert: the fence rule is credited
        Assert.Equal("<!-- markdownlint-disable MD022 -->\n\n# H\n\n```\ncode\n```\n", formatted);
        Assert.Equal("fence-blank-lines", Assert.Single(result.Edits).RuleId);
    }

    /// <summary>
    ///     Test that a missing final line ending is added and extra blank lines at the end are removed.
    /// </summary>
    [Theory]
    [InlineData("text", "text\n")]
    [InlineData("text\n", "text\n")]
    [InlineData("text\n\n\n", "text\n")]
    [InlineData("text\n  \n\t\n", "text\n")]
    [InlineData("# H\n\n- a\n- b\n\n", "# H\n\n- a\n- b\n")]
    [InlineData("```\ncode\n```\n\n\n", "```\ncode\n```\n")]
    [InlineData("    code\n\n\n", "    code\n")]
    public void DocumentFormatter_Format_EndOfFile_GetsExactlyOneLineEnding(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that no final line ending is wanted when the configuration says so.
    /// </summary>
    [Theory]
    [InlineData("text\n", "text")]
    [InlineData("text\n\n\n", "text")]
    [InlineData("text", "text")]
    [InlineData("text  \n\n", "text")]
    public void DocumentFormatter_Format_FinalNewlineOff_RemovesTheLineEnding(string input, string expected) =>
        AssertPasses(input, expected, LintMarkConfig.Default with { FinalNewline = false });

    /// <summary>
    ///     Test that a document without content is left alone.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("  \n\n")]
    [InlineData("\t")]
    public void DocumentFormatter_Format_DocumentWithoutContent_IsLeftAlone(string input)
    {
        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), input);

        // Assert: no edit and no violation
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that the end of the file is left alone when the last block keeps its trailing blank lines.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n\n\n")]
    [InlineData("```\ncode")]
    [InlineData("<!--\nx\n\n\n")]
    public void DocumentFormatter_Format_EndOfBlockThatKeepsBlankLines_IsLeftAlone(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that a suppressed rule leaves its part of the document alone, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable final-newline", "text")]
    [InlineData("markdownlint-disable MD047", "text")]
    [InlineData("markdownlint-disable MD009", "text  \nmore")]
    [InlineData("markdownlint-disable MD010", "a\tb")]
    [InlineData("lintmark-disable trailing-whitespace", "x  \n  \ny")]
    public void DocumentFormatter_Format_RuleSuppressed_LeavesThatPart(string directive, string content)
    {
        // Arrange: a document with a violation of the rule inside a region that disables it, and no other violation
        var text = $"<!-- {directive} -->\n\n{content}";
        var expectedUnchangedLines = content.Split('\n');

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), text);

        // Assert: the violation of the disabled rule is not fixed or reported
        Assert.DoesNotContain(result.Diagnostics, diagnostic => directive.Contains(diagnostic.RuleId, StringComparison.Ordinal));
        Assert.Contains(expectedUnchangedLines[0], formatted, StringComparison.Ordinal);
        Assert.StartsWith($"<!-- {directive} -->\n\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a blank-line rule is not applied where a region disables it.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MaxBlankLinesSuppressed_KeepsTheBlankLines() =>
        AssertPasses(
            "<!-- lintmark-disable max-blank-lines -->\n\na\n\n\n\nb\n",
            "<!-- lintmark-disable max-blank-lines -->\n\na\n\n\n\nb\n");

    /// <summary>
    ///     Test that every edit of an always-applied rule is reported once per rule and line, at the place it changes.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_Edits_AreReportedPerRuleAndLineAtTheirPlace()
    {
        // Arrange: two tabs on the first line (one rule, one line), trailing whitespace on the second, a missing final newline
        const string input = "a\tb\tc\nd \ne";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), input);

        // Assert: three violations in order of position, each with its catalog description
        Assert.Equal("a   b   c\nd\ne\n", formatted);
        Assert.Equal(
            [("hard-tabs", 1, 2), ("trailing-whitespace", 2, 2), ("final-newline", 3, 2)],
            result.Diagnostics.Select(d => (d.RuleId, d.Line, d.Column)));
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticKind.Formatting, diagnostic.Kind);
            Assert.True(RuleCatalog.TryGet(diagnostic.RuleId, out var rule));
            Assert.Equal(rule.Description, diagnostic.Message);
            Assert.Equal("a.md", diagnostic.Path);
        });
    }

    /// <summary>
    ///     Test that the edit of a configurable rule (a block formatter that a test installs) is not reported by the document formatter.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_EditOfConfigurableRule_IsNotReported()
    {
        // Arrange: a formatter that edits paragraphs under the configurable paragraph wrap rule
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(formatter, "a  b\n");

        // Assert: the text changes, and no violation of an always-applied rule exists
        Assert.Equal("a b\n", formatted);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a block that a block formatter accepts is edited by that formatter only, also when the formatter proposes nothing.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ClaimedParagraph_IsNotEditedByThePasses()
    {
        // Arrange: a paragraph with a double space and trailing whitespace, claimed by a formatter that replaces double spaces
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format
        var (_, formatted) = BlockFormatterAssertions.Apply(formatter, "a  b   \n");

        // Assert: the formatter's edits are applied, the trailing whitespace is not removed by a pass
        Assert.Equal("a b  \n", formatted);
    }

    /// <summary>
    ///     Test that a paragraph that a block formatter accepts is claimed even where its formatting rule is suppressed.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ClaimedParagraphWithRuleSuppressed_IsNotEditedByThePasses()
    {
        // Arrange: the paragraph wrap rule is disabled, so the formatter does not edit, and the passes must not either
        const string input = "<!-- lintmark-disable paragraph-wrap -->\n\na  b   \n";
        var formatter = new DocumentFormatter([new ReplacingFormatter("  ", " ")]);

        // Act: format
        var (_, formatted) = BlockFormatterAssertions.Apply(formatter, input);

        // Assert: unchanged
        Assert.Equal(input, formatted);
    }
}
