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
    ///     Test that whitespace inside code spans, inline HTML, and math is kept.
    /// </summary>
    [Theory]
    [InlineData("text `a  \nb` end\n")]
    [InlineData("a <span  \nclass=\"x\">b</span>\n")]
    [InlineData("a $x  \ny$ b\n")]
    public void DocumentFormatter_Format_LiteralInlineSpans_KeepTheirWhitespace(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that whitespace inside a link title is kept.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_LinkTitle_KeepsItsWhitespace() =>
        AssertPasses("[a](http://x \"t  \nu\")\n", "[a](http://x \"t  \nu\")\n");

    /// <summary>
    ///     Test that table rows lose trailing whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_TableRows_LoseTrailingWhitespace() =>
        AssertPasses("| a | b |  \n|---|---|\n| 1 | 2 |   \n", "| a | b |\n|---|---|\n| 1 | 2 |\n");

    /// <summary>
    ///     Test that a single-line link reference definition loses trailing whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_Definitions_LoseTrailingWhitespace() =>
        AssertPasses("[a]: http://x  \n", "[a]: http://x\n");

    /// <summary>
    ///     Test that a thematic break that no block formatter accepts loses trailing whitespace.
    /// </summary>
    [Theory]
    [InlineData("text\n\n***  \n", "text\n\n***\n")]
    [InlineData("text\n\n- - -\t\n", "text\n\n- - -\n")]
    public void DocumentFormatter_Format_UnclaimedThematicBreak_LosesTrailingWhitespace(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a line that holds only a quote marker loses its trailing whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MarkerLines_LoseTrailingWhitespace() =>
        AssertPasses("> text\n>   \n> more\n", "> text\n>\n> more\n");

    /// <summary>
    ///     Test that headings that no block formatter claims lose trailing whitespace too, also on a setext underline.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_UnclaimedHeading_LosesTrailingWhitespace() =>
        AssertPasses("# Title  \n\nSetext  \n===  \n", "# Title\n\nSetext\n===\n");

    /// <summary>
    ///     Test that code and HTML blocks and display math keep their whitespace.
    /// </summary>
    [Theory]
    [InlineData("```\ncode  \n\ttab\n```\n")]
    [InlineData("~~~\ncode  \n~~~  \n")]
    [InlineData("    code  \n")]
    [InlineData("<div>\nx  \n\ty\n</div>\n")]
    [InlineData("$$\nx  \n$$\n")]
    public void DocumentFormatter_Format_LiteralBlocks_KeepTheirWhitespace(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that front matter keeps its whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_FrontMatter_KeepsItsWhitespace() =>
        AssertPasses("---\ntitle: x  \n---\n\ntext\n", "---\ntitle: x  \n---\n\ntext\n");

    /// <summary>
    ///     Test that a grid table keeps its whitespace.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_GridTable_KeepsItsWhitespace() =>
        AssertPasses("+---+---+\n| a | b |  \n+---+---+\n", "+---+---+\n| a | b |  \n+---+---+\n");

    /// <summary>
    ///     Test that a whitespace-only line that ends a top-level indented code block and is wider than its indentation is code and stays.
    /// </summary>
    [Theory]
    [InlineData("x\n\n    code\n      \n", "x\n\n    code\n      \n")]
    [InlineData("    code\n     \n\ny\n", "    code\n     \n\ny\n")]
    [InlineData("x\n\n    code\n\t  \n\ny\n", "x\n\n    code\n\t  \n\ny\n")]
    public void DocumentFormatter_Format_WideWhitespaceLineEndingIndentedCode_IsKeptAsCode(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a whitespace-only line after an indented code block that is not wider than the indentation is a
    ///     blank line and is emptied.
    /// </summary>
    [Theory]
    [InlineData("x\n\n    code\n      \n    \n", "x\n\n    code\n      \n")]
    [InlineData("x\n\n    code\n    \n", "x\n\n    code\n")]
    public void DocumentFormatter_Format_WhitespaceLineNotWiderThanTheIndentation_AfterIndentedCode_IsEmptied(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a whitespace-only line wider than the indentation, inside a list item, is not code and is emptied as before.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_WideWhitespaceLineAfterNestedCode_IsEmptied() =>
        AssertPasses("- a\n\n      code\n          \n", "- a\n\n      code\n");

    /// <summary>
    ///     Test that a line that looks like the border of a grid table is not edited.
    /// </summary>
    [Theory]
    [InlineData("- +---+  \n- b\n")]
    [InlineData("> +---+\t\n> b\n")]
    public void DocumentFormatter_Format_GridBorderLookalike_IsNotEdited(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that no blank line is inserted between a line that looks like the border of a grid table and its neighbor.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_GridBorderLookalike_IsNotSeparated() =>
        AssertPasses("text\n- +---+\n", "text\n- +---+\n");

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
    ///     Test that text followed by a line that looks like a list item but cannot start a list there is not separated.
    /// </summary>
    [Theory]
    [InlineData("text\n2. a\n")]
    [InlineData("text\n    not code\n")]
    public void DocumentFormatter_Format_TextBeforeListLookalike_IsNotSeparated(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that a block that the parser reports after more than three columns or a tab on its first line is not
    ///     separated from the text before it.
    /// </summary>
    [Theory]
    [InlineData("# H\n[^]:[a]:x\n")]
    [InlineData("  1.\n\t-\n")]
    public void DocumentFormatter_Format_BlockStartingAfterPrefixOnItsLine_IsNotSeparated(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that the lazy continuation line of a list item is not separated from the item.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_LazyContinuationOfListItem_IsNotSeparated() =>
        AssertPasses("- a\ntext\n", "- a\ntext\n");

    /// <summary>
    ///     Test that a line without pipes after the rows of a table is a row of the table and is not separated.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_RowWithoutPipesAfterTable_IsNotSeparated() =>
        AssertPasses("| a | b |\n|---|---|\n| 1 | 2 |\nno pipe here\n", "| a | b |\n|---|---|\n| 1 | 2 |\nno pipe here\n");

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
    ///     Test that the end of the file is left alone after a fence that is never closed, because only the end of
    ///     the file closes it and the blank lines belong to the code.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n\n\n")]
    [InlineData("```\ncode")]
    public void DocumentFormatter_Format_EndOfUnclosedFence_IsLeftAlone(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that the end of the file is left alone after an HTML block that no line with its end condition
    ///     closes, because the blank lines belong to the block.
    /// </summary>
    [Theory]
    [InlineData("<!--\nx\n\n\n")]
    [InlineData("<!-- c")]
    [InlineData("<script>\nx\n\n\n")]
    [InlineData("text\n\n<![CDATA[\nx\n\n")]
    public void DocumentFormatter_Format_EndOfUnclosedHtmlBlock_IsLeftAlone(string input) =>
        AssertPasses(input, input);

    /// <summary>
    ///     Test that the end of the file is normalized after an HTML block of types 1 to 5 that its own end condition
    ///     closes: a missing final line ending is added and extra blank lines are removed.
    /// </summary>
    [Theory]
    [InlineData("text\n\n<!-- c -->", "text\n\n<!-- c -->\n")]
    [InlineData("<!-- c -->\n\n\n", "<!-- c -->\n")]
    [InlineData("<script>\nx\n</script>\n\n\n", "<script>\nx\n</script>\n")]
    [InlineData("<pre>\nx\n</pre>", "<pre>\nx\n</pre>\n")]
    [InlineData("<style>\nx\n</style>\n  \n", "<style>\nx\n</style>\n")]
    [InlineData("<?php\n?>\n\n\n", "<?php\n?>\n")]
    [InlineData("<![CDATA[\nx\n]]>\n\n", "<![CDATA[\nx\n]]>\n")]
    [InlineData("<!DOCTYPE html>\n\n\n", "<!DOCTYPE html>\n")]
    [InlineData("<!-- a -->\n\n\ntext\n", "<!-- a -->\n\ntext\n")]
    public void DocumentFormatter_Format_EndOfClosedHtmlBlock_GetsExactlyOneLineEnding(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that the closing HTML comment of a document at the end of the file is no reason to leave the file
    ///     without its final line ending, and that the violation is reported.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_FileEndingWithClosedComment_ReportsTheFinalNewline()
    {
        // Arrange: a document that ends with a closed comment and no line ending
        const string input = "text\n\n<!-- markdownlint-enable -->";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), input);

        // Assert: the line ending is added and reported once, on the last line
        Assert.Equal(input + "\n", formatted);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(("final-newline", 3), (diagnostic.RuleId, diagnostic.Line));
    }

    /// <summary>
    ///     Test that a rule suppressed by its identifier leaves its part of the document alone, while the same
    ///     content without the directive is changed.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable final-newline", "text")]
    [InlineData("lintmark-disable trailing-whitespace", "x   \n  \ny\n")]
    [InlineData("lintmark-disable hard-tabs", "a\tb\n")]
    public void DocumentFormatter_Format_RuleSuppressedById_LeavesThatPart(string directive, string content) =>
        AssertSuppressedRuleLeavesContent(directive, content);

    /// <summary>
    ///     Test that a rule suppressed by an alias leaves its part of the document alone, while the same content
    ///     without the directive is changed.
    /// </summary>
    [Theory]
    [InlineData("markdownlint-disable MD047", "text")]
    [InlineData("markdownlint-disable MD009", "text   \nmore\n")]
    [InlineData("markdownlint-disable MD010", "a\tb\n")]
    public void DocumentFormatter_Format_RuleSuppressedByAlias_LeavesThatPart(string directive, string content) =>
        AssertSuppressedRuleLeavesContent(directive, content);

    /// <summary>
    ///     Assert that a document with a violation inside a region that disables its rule is left alone, and that
    ///     the same content without the directive is changed.
    /// </summary>
    /// <param name="directive">Suppression directive.</param>
    /// <param name="content">Content with a violation of the suppressed rule.</param>
    private static void AssertSuppressedRuleLeavesContent(string directive, string content)
    {
        // Arrange: a document with a violation of the rule inside a region that disables it, and no other violation
        var text = $"<!-- {directive} -->\n\n{content}";

        // Act: format it, and the same content without the directive
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), text);
        var (_, control) = BlockFormatterAssertions.Apply(new DocumentFormatter(), content);

        // Assert: nothing is fixed or reported, and without the directive the content does change
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
        Assert.NotEqual(content, control);
    }

    /// <summary>
    ///     Test that a tab at the end of a line is kept when the trailing whitespace rule is suppressed: expanding
    ///     it could leave two trailing spaces, which are a hard break.
    /// </summary>
    [Theory]
    [InlineData("abc\t\nnext\n")]
    [InlineData("ab\t\nnext\n")]
    [InlineData("abc\t")]
    public void DocumentFormatter_Format_TrailingTabWithTrailingWhitespaceSuppressed_IsKept(string content)
    {
        // Arrange: a trailing tab in a region that disables the trailing whitespace rule
        var text = $"<!-- lintmark-disable trailing-whitespace -->\n\n{content}";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter(), text);

        // Assert: the tab stays and no hard tab is reported (at most the missing final line ending is)
        Assert.Equal(content.EndsWith('\n') ? text : text + "\n", formatted);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.RuleId == "hard-tabs");
        BlockFormatterAssertions.AssertSafeAndStable(text, null, new DocumentFormatter());
    }

    /// <summary>
    ///     Test that no blank line is inserted between a next-line suppression directive and the block it covers, by
    ///     either directive family, because the blank line would take the directive's place and the suppressed rule would then fire.
    /// </summary>
    [Theory]
    [InlineData("<!-- markdownlint-disable-next-line MD019 -->\n#  Heading  one\n")]
    [InlineData("<!-- lintmark-disable-next-line heading-space -->\n#  Heading  one\n")]
    [InlineData("Intro\n\n<!-- markdownlint-disable-next-line MD009 -->\n- a  \n- b\n")]
    [InlineData("Intro\n\n<!-- lintmark-disable-next-line trailing-whitespace -->\n- a   \n- b\n")]
    [InlineData("Intro\n\n<!-- markdownlint-disable-next-line MD031 -->\n```\ncode\n```\n")]
    [InlineData("Intro\n\n<!-- markdownlint-disable-next-line MD013 -->\n| a | b |\n|---|---|\n| 1 | 2 |\n")]
    [InlineData("Intro\n\n<!-- markdownlint-disable-next-line MD013 -->\n    code\n")]
    public void DocumentFormatter_Format_NextLineDirectiveBeforeBlock_KeepsTheDirectiveWithItsLine(string input)
    {
        // Act: format with the production formatters
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input);

        // Assert: the directive still stands directly above the line it covers
        var lines = formatted.Split('\n');
        var directive = Array.FindIndex(lines, line => line.Contains("-disable-next-line", StringComparison.Ordinal));
        Assert.True(directive >= 0);
        Assert.False(string.IsNullOrWhiteSpace(lines[directive + 1]), $"A blank line follows the directive:\n{formatted}");
    }

    /// <summary>
    ///     Test that a block without any suppression directive before it still gets its blank line, as before.
    /// </summary>
    [Theory]
    [InlineData("<!-- note -->\n# H\n", "<!-- note -->\n\n# H\n")]
    [InlineData("<!-- markdownlint-disable MD013 -->\n# H\n", "<!-- markdownlint-disable MD013 -->\n\n# H\n")]
    [InlineData("<!-- markdownlint-disable-next-line MD013 -->\n\n# H\n", "<!-- markdownlint-disable-next-line MD013 -->\n\n# H\n")]
    public void DocumentFormatter_Format_NoNextLineDirectiveBeforeBlock_GetsTheBlankLine(string input, string expected) =>
        AssertPasses(input, expected);

    /// <summary>
    ///     Test that a blank-line rule is not applied where a region disables it.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_MaxBlankLinesSuppressed_KeepsTheBlankLines() =>
        AssertPasses(
            "<!-- lintmark-disable max-blank-lines -->\n\na\n\n\n\nb\n",
            "<!-- lintmark-disable max-blank-lines -->\n\na\n\n\n\nb\n");

    /// <summary>
    ///     Test that the whitespace of the blank lines of a run stays where the trailing whitespace rule is disabled, and that the
    ///     surplus lines still go.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_TrailingWhitespaceSuppressedOverBlankRun_KeepsTheWhitespaceOnly() =>
        AssertPasses(
            "<!-- lintmark-disable trailing-whitespace -->\n\na\n  \n\n\nb\n",
            "<!-- lintmark-disable trailing-whitespace -->\n\na\n  \nb\n",
            LintMarkConfig.Default with { MaxBlankLines = 1 });

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
