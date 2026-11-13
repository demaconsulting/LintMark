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
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the HeadingFormatter class writes headings in ATX form with one space, no closing number signs, no
///     indentation and no trailing whitespace, and leaves everything else about a heading alone.
/// </summary>
public class HeadingFormatterTests
{
    /// <summary>
    ///     The text used to compare the edits for the stored forms of a document: every kind of heading violation, a
    ///     multi-line setext heading, and a tab after the number signs.
    /// </summary>
    private const string StoredFormsInput = "Title\n=====\n\n## Sub ##  \n\n#\tTab\ta\n\nOne  \ntwo   \n===  \n";

    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly HeadingFormatter Formatter = new();

    /// <summary>
    ///     Asserts that the formatter alone turns the input into the expected text and that the result is safe and stable.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected output.</param>
    private static void AssertFormats(string input, string expected) =>
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected);

    /// <summary>
    ///     Test that an ATX heading and a setext heading are accepted.
    /// </summary>
    [Fact]
    public void HeadingFormatter_CanFormat_AtxAndSetextHeadings_ReturnsTrue()
    {
        // Arrange: a document with an ATX and a setext heading
        var document = BlockFormatterAssertions.Parse("# One\n\nTwo\n===\n");

        // Act & Assert: both headings are accepted
        Assert.All(document.Blocks.Where(block => block.Kind == BlockKind.Heading), block => Assert.True(Formatter.CanFormat(block)));
        Assert.Equal(2, document.Blocks.Count(block => block.Kind == BlockKind.Heading));
    }

    /// <summary>
    ///     Test that blocks other than headings are not accepted.
    /// </summary>
    [Theory]
    [InlineData("text\n")]
    [InlineData("```\ncode\n```\n")]
    [InlineData("---\n")]
    [InlineData("- item\n")]
    [InlineData("> quote\n")]
    public void HeadingFormatter_CanFormat_OtherBlocks_ReturnsFalse(string text)
    {
        // Arrange: a document without a heading
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: no block is accepted
        Assert.All(document.Blocks, block => Assert.False(Formatter.CanFormat(block)));
    }

    /// <summary>
    ///     Test that a block of the heading kind whose parser node is not a heading is not accepted.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeadingFormatter_CanFormat_HeadingKindWithoutHeadingNode_ReturnsFalse(bool withNode)
    {
        // Arrange: a block that claims to be a heading but has no heading node (or a paragraph node)
        var block = new BlockInfo(withNode ? new ParagraphBlock() : null, BlockKind.Heading, 0, 0, 0, null);

        // Act & Assert: the block is not accepted
        Assert.False(Formatter.CanFormat(block));
    }

    /// <summary>
    ///     Test that a null block is rejected when asking whether it is accepted.
    /// </summary>
    [Fact]
    public void HeadingFormatter_CanFormat_NullBlock_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when formatting.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a heading, a context and an edit set
        var document = BlockFormatterAssertions.Parse("# One\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading);
        var context = new FormatContext(document, LintMarkConfig.Default);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, block, null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not a heading is rejected when formatting.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_NonHeadingBlock_ThrowsArgumentException()
    {
        // Arrange: a paragraph
        var document = BlockFormatterAssertions.Parse("text\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Paragraph);
        var context = new FormatContext(document, LintMarkConfig.Default);

        // Act & Assert: the block is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentException>(() => Formatter.Format(context, block, new EditSet()));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that the formatter lists the five rules it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void HeadingFormatter_RuleIds_Read_ListsTheHeadingRulesAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the heading rules, whitespace and tabs, each registered, in an immutable array
        Assert.Equal(
            ["heading-style", "heading-space", "heading-start-left", "trailing-whitespace", "hard-tabs"],
            ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that a tab right after the number signs of a heading that is not on the first line is replaced by one space,
    ///     and that the tabs in its text are expanded from where the text lands: the parser reports the inline spans of
    ///     such a heading too small by a fixed amount, which must neither make the edits overlap nor make the result unstable.
    /// </summary>
    [Theory]
    [InlineData("x\n\n#\tTitle\n", "x\n\n# Title\n")]
    [InlineData("para\n#\tTab\ttitle\n", "para\n\n# Tab   title\n")]
    [InlineData("x\n\n#\ta\tb\n", "x\n\n# a b\n")]
    [InlineData("x\n\n##\t`a\tb`\tc\n", "x\n\n## `a\tb`  c\n")]
    [InlineData("x\n\n#\tTitle\t#\n", "x\n\n# Title\n")]
    [InlineData("x\n\n#\t\ta\tb  \n", "x\n\n# a b\n")]
    [InlineData("x\n\n-\t#\tTitle\tx\n", "x\n\n-\t# Title x\n")]
    public void HeadingFormatter_Format_TabAfterMarkerNotOnFirstLine_IsSafeAndStable(string input, string expected)
    {
        // Act & Assert: the production formatters give the expected text, which renders the same and is stable
        BlockFormatterAssertions.AssertFormats(input, expected);
        BlockFormatterAssertions.AssertFormats(expected, expected);

        // The heading formatter alone proposes edits that do not overlap and give the same text for the heading
        var (edits, _) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);
        Assert.NotEmpty(edits);
    }

    /// <summary>
    ///     Test that a heading whose text has a tab and whose inline spans do not fit its line is left alone as a whole.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SpansThatDoNotFitTheLine_LeaveTheHeadingAlone()
    {
        // Arrange: a heading with a tab in its text whose first inline is replaced by one that starts elsewhere
        var document = BlockFormatterAssertions.Parse("x\n\n# a\tb\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading);
        var heading = (HeadingBlock)block.Node!;
        var first = heading.Inline!.FirstChild!;
        first.Span = new SourceSpan(first.Span.Start + 1, first.Span.End);
        var edits = new EditSet();

        // Act: format the heading with the damaged spans
        Formatter.Format(new FormatContext(document, LintMarkConfig.Default), block, edits);

        // Assert: no edit at all, not even for the rules whose edits do not depend on the spans
        Assert.True(edits.IsEmpty);
    }

    /// <summary>
    ///     Test that a heading whose spans were damaged but that has no tab in its text is still formatted.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_NoTabAndDamagedSpans_StillFormatsTheHeading()
    {
        // Arrange: a heading with extra blanks and no tab whose inline span is damaged
        var document = BlockFormatterAssertions.Parse("x\n\n#   Title\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.Heading);
        var first = ((HeadingBlock)block.Node!).Inline!.FirstChild!;
        first.Span = new SourceSpan(0, 0);
        var edits = new EditSet();

        // Act: format the heading
        Formatter.Format(new FormatContext(document, LintMarkConfig.Default), block, edits);

        // Assert: the blanks after the marker are still replaced
        Assert.Equal("x\n\n# Title\n", SourcePatcher.Apply(document.Source, edits.ToImmutable()));
    }

    /// <summary>
    ///     Test that a tab in a heading with a closing sequence, and in a heading inside a container, is expanded.
    /// </summary>
    [Theory]
    [InlineData("# a\tb #\n", "# a b\n")]
    [InlineData("## a\tb \t##  \n", "## a    b\n")]
    [InlineData("> # a\tb #  \n", "> # a   b\n")]
    [InlineData("- # a\tb\n", "- # a   b\n")]
    [InlineData("1. ## Tab\ttitle ##\n", "1. ## Tab   title\n")]
    public void HeadingFormatter_Format_TabInClosedOrNestedHeading_IsExpanded(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that trailing whitespace after a final backslash of a setext heading text line is kept (removing it
    ///     would turn the backslash into a hard break).
    /// </summary>
    [Theory]
    [InlineData("Backslash \\  \n===\n")]
    [InlineData("one\ntwo \\ \n===\n")]
    public void HeadingFormatter_Format_SetextTextEndingWithBackslashAndSpaces_KeepsTheSpaces(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a multi-line setext heading inside a block quote stays setext and loses only its trailing whitespace.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_MultiLineSetextInQuote_StaysSetext() =>
        AssertFormats("> Title\n> more   \n> =====  \n", "> Title\n> more\n> =====\n");

    /// <summary>
    ///     Test that a setext heading is converted when it follows a heading, and its tabs after a non-zero offset are expanded.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SetextTextWithTabNotOnFirstLine_ConvertsAndExpandsTheTab() =>
        AssertFormats("x\n\na\tb\n===\n", "x\n\n# a b\n");

    /// <summary>
    ///     Test that a heading right after a paragraph, with a tab after its number signs, is stable on the second run.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_TabHeadingAfterParagraphWithoutBlankLine_IsStableOnTheSecondRun()
    {
        // Act: format twice with the production formatters
        var once = BlockFormatterAssertions.AssertSafeAndStable("para\n#\tTab\ttitle");
        var twice = BlockFormatterAssertions.AssertSafeAndStable(once);

        // Assert: the second run changes nothing
        Assert.Equal(once, twice);
    }

    /// <summary>
    ///     Test that a setext heading of one line becomes an ATX heading of the same level.
    /// </summary>
    [Theory]
    [InlineData("Title\n=====\n", "# Title\n")]
    [InlineData("Title\n-----\n", "## Title\n")]
    [InlineData("Title\n=\n", "# Title\n")]
    [InlineData("  Title  \n  =====  \n", "# Title\n")]
    [InlineData("Some *emphasized* `code` text\n===\n", "# Some *emphasized* `code` text\n")]
    [InlineData("First\n===\n\nSecond\n---\n", "# First\n\n## Second\n")]
    public void HeadingFormatter_Format_SetextHeading_ConvertsToAtx(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a setext heading of several lines stays a setext heading, keeping its hard break, and loses only trailing whitespace.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_MultiLineSetextHeading_StaysSetextAndKeepsHardBreak()
    {
        // Arrange: two lines of text joined by a hard break, with trailing whitespace on the last line and the underline
        const string input = "Line one  \nline two   \n===  \n";

        // Act & Assert: only the trailing whitespace goes
        AssertFormats(input, "Line one  \nline two\n===\n");
    }

    /// <summary>
    ///     Test that a setext heading inside a block quote or list stays a setext heading.
    /// </summary>
    [Theory]
    [InlineData("> Title  \n> =====  \n", "> Title\n> =====\n")]
    [InlineData("- Title\n  =====\n", "- Title\n  =====\n")]
    public void HeadingFormatter_Format_SetextHeadingInContainer_StaysSetext(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a setext heading whose text ends with a number sign or a backslash is not converted.
    /// </summary>
    [Theory]
    [InlineData("C#\n==\n")]
    [InlineData("Escaped \\#\n==\n")]
    [InlineData("Backslash \\\n===\n")]
    public void HeadingFormatter_Format_SetextTextEndingWithMarker_StaysSetext(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a setext heading whose text starts like a Pandoc structure line is not converted.
    /// </summary>
    [Theory]
    [InlineData("+---+---+\n===\n")]
    [InlineData("::: note\n===\n")]
    [InlineData("\\new" + "page\n===\n")]
    [InlineData("$$ x\n===\n")]
    public void HeadingFormatter_Format_SetextTextStartingLikeStructure_StaysSetext(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that tabs in the text of a converted setext heading become spaces counted from the new position.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SetextTextWithTab_ConvertsAndExpandsTheTab() =>
        AssertFormats("a\tb\n===\n", "# a b\n");

    /// <summary>
    ///     Test that a setext heading with a hard tab in a code span keeps the tab.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SetextTextWithTabInCodeSpan_KeepsTheTab() =>
        AssertFormats("`a\tb`\n===\n", "# `a\tb`\n");

    /// <summary>
    ///     Test that closing number signs are removed.
    /// </summary>
    [Theory]
    [InlineData("# Title #\n", "# Title\n")]
    [InlineData("## Title ##   \n", "## Title\n")]
    [InlineData("# Title   #\n", "# Title\n")]
    [InlineData("### Title ###########\n", "### Title\n")]
    [InlineData("# Title ##\n", "# Title\n")]
    public void HeadingFormatter_Format_ClosedAtxHeading_RemovesTheClosingSequence(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that an empty heading with a closing sequence keeps only its opening number signs.
    /// </summary>
    [Theory]
    [InlineData("### ###\n", "###\n")]
    [InlineData("#   #\n", "#\n")]
    public void HeadingFormatter_Format_EmptyClosedHeading_KeepsTheMarkerOnly(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that number signs that are text and not a closing sequence are kept.
    /// </summary>
    [Theory]
    [InlineData("# C#\n")]
    [InlineData("# foo#\n")]
    [InlineData("# foo \\#\n")]
    [InlineData("# foo #bar\n")]
    public void HeadingFormatter_Format_NumberSignsInText_AreNotAClosingSequence(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that several spaces after the opening number signs become one.
    /// </summary>
    [Theory]
    [InlineData("#   Title\n", "# Title\n")]
    [InlineData("##    Title with  spaces  \n", "## Title with  spaces\n")]
    [InlineData("#\tTitle\n", "# Title\n")]
    [InlineData("# \t Title\n", "# Title\n")]
    public void HeadingFormatter_Format_BlanksAfterMarker_BecomeOneSpace(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the indentation of a top-level heading is removed.
    /// </summary>
    [Theory]
    [InlineData(" # Title\n", "# Title\n")]
    [InlineData("   ## Title\n", "## Title\n")]
    [InlineData("  ### Title ###  \n", "### Title\n")]
    public void HeadingFormatter_Format_IndentedHeading_RemovesTheIndentation(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a heading inside a block quote or list keeps its container prefix.
    /// </summary>
    [Theory]
    [InlineData("> # Title\n", "> # Title\n")]
    [InlineData("> #   Title #  \n", "> # Title\n")]
    [InlineData("- # Title\n", "- # Title\n")]
    [InlineData("1. ## Title ##\n", "1. ## Title\n")]
    [InlineData("-   #  Title\n", "-   # Title\n")]
    public void HeadingFormatter_Format_HeadingInContainer_KeepsThePrefix(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that trailing whitespace of a heading is removed.
    /// </summary>
    [Theory]
    [InlineData("# Title   \n", "# Title\n")]
    [InlineData("# Title\t\n", "# Title\n")]
    [InlineData("# Title \\ \n", "# Title \\\n")]
    public void HeadingFormatter_Format_TrailingWhitespace_IsRemoved(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that an empty heading loses the blanks after its number signs.
    /// </summary>
    [Theory]
    [InlineData("#   \n", "#\n")]
    [InlineData("##\t\n", "##\n")]
    [InlineData("#\n", "#\n")]
    public void HeadingFormatter_Format_EmptyHeading_KeepsTheMarkerOnly(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that tabs in the heading text become spaces up to the next tab stop of four columns.
    /// </summary>
    [Theory]
    [InlineData("# a\tb\n", "# a b\n")]
    [InlineData("# a\t\tb\n", "# a     b\n")]
    [InlineData("# abc\td\n", "# abc   d\n")]
    [InlineData("# ab\tc\n", "# ab    c\n")]
    public void HeadingFormatter_Format_TabInText_ExpandsToTheTabStop(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a tab inside a code span or a link destination is kept.
    /// </summary>
    [Theory]
    [InlineData("# a `b\tc`\n")]
    [InlineData("# [a](http://example.com/a\t\"t\")\n")]
    public void HeadingFormatter_Format_TabInProtectedSpan_IsKept(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a line that merely looks like a heading (no space after the number signs) is a paragraph and is not touched.
    /// </summary>
    [Theory]
    [InlineData("#Title\n")]
    [InlineData("##Title##\n")]
    [InlineData("#hashtag and text\n")]
    public void HeadingFormatter_Format_NoSpaceAfterMarker_LeavesTheParagraph(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a canonical heading produces no edit.
    /// </summary>
    [Theory]
    [InlineData("# Title\n")]
    [InlineData("###### Title with *emphasis*\n")]
    [InlineData("#\n")]
    public void HeadingFormatter_Format_CanonicalHeading_ProposesNothing(string input)
    {
        // Act: format the heading
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: nothing changes
        Assert.Empty(edits);
        Assert.Equal(input, formatted);
    }

    /// <summary>
    ///     Test that every edit is reported under the rule of the violation it removes.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_NonCanonicalHeading_ReportsEachEditUnderItsRule()
    {
        // Arrange: a heading with indentation, many blanks after the marker, a closing sequence and trailing whitespace
        const string input = "   #   Title  ##  \n";

        // Act: format the heading
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: one edit per violation, in order of position
        Assert.Equal("# Title\n", formatted);
        Assert.Equal(
            ["heading-start-left", "heading-space", "heading-style", "trailing-whitespace"],
            edits.Select(edit => edit.RuleId));
    }

    /// <summary>
    ///     Test that a setext heading after a link reference definition is handled safely.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SetextHeadingAfterDefinition_ConvertsSafely() =>
        AssertFormats("[ref]: http://example.com\n\nTitle\n===\n", "[ref]: http://example.com\n\n# Title\n");

    /// <summary>
    ///     Test that a heading is left as written when a rule of the formatter is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable heading-style", "Title\n=====\n")]
    [InlineData("markdownlint-disable MD003", "Title\n=====\n")]
    [InlineData("markdownlint-disable MD021", "# Title #\n")]
    [InlineData("markdownlint-disable MD019", "#   Title\n")]
    [InlineData("markdownlint-disable MD023", "  # Title\n")]
    [InlineData("markdownlint-disable MD009", "# Title  \n")]
    [InlineData("lintmark-disable hard-tabs", "# a\tb\n")]
    [InlineData("lintmark-disable", "Title\n=====\n")]
    public void HeadingFormatter_Format_RuleSuppressed_LeavesTheHeading(string directive, string heading)
    {
        // Arrange: a heading inside a region that disables the rule
        var text = $"<!-- {directive} -->\n\n{heading}";

        // Act: format with the heading formatter registered
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), text);

        // Assert: nothing changes and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a heading after a region that was enabled again is formatted.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_RuleEnabledAgain_FormatsTheLaterHeading()
    {
        // Arrange: one heading inside a disabled region and one after it
        const string input = "<!-- markdownlint-disable MD003 -->\n\nA\n=\n\n<!-- markdownlint-enable MD003 -->\n\nB\n=\n";

        // Act: format
        var (_, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), input);

        // Assert: only the second heading changes
        Assert.Equal("<!-- markdownlint-disable MD003 -->\n\nA\n=\n\n<!-- markdownlint-enable MD003 -->\n\n# B\n", formatted);
    }

    /// <summary>
    ///     Test that CRLF line endings, and mixed line endings, do not change the edits that the formatter proposes.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_CrlfLineEndings_ProposesTheSameEdits() =>
        BlockFormatterAssertions.AssertSameEditsWithCrlf(Formatter, StoredFormsInput);

    /// <summary>
    ///     Test that a byte order mark does not change the edits that the formatter proposes.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_ByteOrderMark_ProposesTheSameEdits() =>
        BlockFormatterAssertions.AssertSameEditsWithByteOrderMark(Formatter, StoredFormsInput);

    /// <summary>
    ///     Test that CRLF line endings and a byte order mark survive formatting a heading.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_CrlfAndBom_KeepsLineEndingsAndMark()
    {
        // Act & Assert: the written bytes keep the endings and the mark, for single-line and multi-line setext headings too
        BlockFormatterAssertions.AssertFormatsOnDisk("Title\n=====\n\n## Sub ##  \n", "# Title\n\n## Sub\n");
        BlockFormatterAssertions.AssertFormatsOnDisk("Line one  \nline two   \n===  \n\n#   Three\n", "Line one  \nline two\n===\n\n# Three\n");
    }

    /// <summary>
    ///     Test that formatting is deterministic when the same formatter instance serves several threads that share one document.
    /// </summary>
    [Fact]
    public void HeadingFormatter_Format_SharedInstanceOnManyThreads_GivesTheSameResult() =>
        BlockFormatterAssertions.AssertConcurrentFormatting(
            Formatter,
            "Title\n=====\n\n  ## Two ##  \n\n#   Three\n\n#\tTab\ta\n",
            "# Title\n\n## Two\n\n# Three\n\n# Tab   a\n");
}
