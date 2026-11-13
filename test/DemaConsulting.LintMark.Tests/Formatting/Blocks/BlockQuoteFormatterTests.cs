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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the BlockQuoteFormatter class writes one space after each block quote marker on the paragraph and
///     heading lines of a quote, and leaves alone every line where the spaces are content or structure.
/// </summary>
public class BlockQuoteFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly BlockQuoteFormatter Formatter = new();

    /// <summary>
    ///     Asserts that the production formatters turn the input into the expected text, safely and stably, in every
    ///     line ending, and that the quote formatter alone gives the same text.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    private static void AssertFormats(string input, string expected)
    {
        BlockFormatterAssertions.AssertFormats(input, expected);
        BlockFormatterAssertions.AssertFormatsOnDisk(input, expected);
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected);
    }

    /// <summary>
    ///     Test that only the outermost block quote of a nest is accepted.
    /// </summary>
    [Theory]
    [InlineData("> a\n", 1)]
    [InlineData("> > a\n", 1)]
    [InlineData("> a\n\ntext\n\n> b\n", 2)]
    [InlineData("- > a\n", 1)]
    [InlineData("text\n", 0)]
    public void BlockQuoteFormatter_CanFormat_Blocks_AcceptsOnlyOutermostQuotes(string text, int accepted)
    {
        // Arrange: a document
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: the number of accepted blocks is the number of outermost quotes
        Assert.Equal(accepted, document.Blocks.Count(block => Formatter.CanFormat(block)));
    }

    /// <summary>
    ///     Test that a missing block is rejected when asking whether it is accepted.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_CanFormat_NullBlock_Throws()
    {
        // Act & Assert: the argument is named
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!)).ParamName);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when formatting.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a quote, a context and an edit set
        var document = BlockFormatterAssertions.Parse(">  a\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.BlockQuote);
        var context = new FormatContext(document, LintMarkConfig.Default);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, block, null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not an outermost block quote is rejected when formatting.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_OtherBlock_ThrowsArgumentException()
    {
        // Arrange: a paragraph, and a nested quote
        var paragraph = BlockFormatterAssertions.Parse("text\n");
        var nested = BlockFormatterAssertions.Parse("> > a\n");
        var inner = nested.Blocks.Last(block => block.Kind == BlockKind.BlockQuote);

        // Act & Assert: both are rejected and the parameter is named
        var first = Assert.Throws<ArgumentException>(() =>
            Formatter.Format(new FormatContext(paragraph, LintMarkConfig.Default), BlockFormatterAssertions.FirstBlock(paragraph, BlockKind.Paragraph), new EditSet()));
        var second = Assert.Throws<ArgumentException>(() => Formatter.Format(new FormatContext(nested, LintMarkConfig.Default), inner, new EditSet()));
        Assert.Equal("block", first.ParamName);
        Assert.Equal("block", second.ParamName);
    }

    /// <summary>
    ///     Test that the formatter lists the one rule it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_RuleIds_Read_ListsTheQuoteRuleAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the quote rule, registered, in an immutable array
        Assert.Equal(["blockquote-space"], ruleIds);
        Assert.True(RuleCatalog.TryGet(ruleIds[0], out _));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that two to four spaces after a marker become one space.
    /// </summary>
    [Theory]
    [InlineData(">  text\n", "> text\n")]
    [InlineData(">   text\n", "> text\n")]
    [InlineData(">    text\n", "> text\n")]
    [InlineData(">   a\n>    b\n", "> a\n> b\n")]
    [InlineData("> a\n>  b\n", "> a\n> b\n")]
    [InlineData(">  a\n> b\n", "> a\n> b\n")]
    public void BlockQuoteFormatter_Format_ExtraSpacesAfterMarker_BecomeOne(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the markers of nested quotes are written with one space each.
    /// </summary>
    [Theory]
    [InlineData(">  >  a\n", "> > a\n")]
    [InlineData(">  > a\n", "> > a\n")]
    [InlineData("> >   a\n", "> > a\n")]
    [InlineData(">  >  >  a\n", "> > > a\n")]
    public void BlockQuoteFormatter_Format_NestedMarkers_AreWrittenWithOneSpace(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the spaces after a marker are collapsed on heading and alert lines.
    /// </summary>
    [Theory]
    [InlineData(">  # Heading\n", "> # Heading\n")]
    [InlineData(">   ## Heading\n>   text\n", "> ## Heading\n> text\n")]
    [InlineData(">  [!NOTE]\n>  text\n", "> [!NOTE]\n> text\n")]
    public void BlockQuoteFormatter_Format_HeadingAndAlertLines_AreNormalized(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a line with five or more spaces after the marker, a tab, or no marker for its quote is left as written.
    /// </summary>
    [Theory]
    [InlineData("> a\n>     b\n")]
    [InlineData(">     code\n")]
    [InlineData(">\ttext\n")]
    [InlineData("> a\nlazy\n")]
    public void BlockQuoteFormatter_Format_FiveSpacesTabAndLazyLines_AreLeftAlone(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a lazy line is left alone while the marked lines of the same paragraph are collapsed.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_LazyLineInParagraph_KeepsItsText() =>
        AssertFormats(">  a\n>  b\nlazy\n", "> a\n> b\nlazy\n");

    /// <summary>
    ///     Test that setext headings, tables, code, HTML and fences in a quote keep the spaces after the marker.
    /// </summary>
    [Theory]
    [InlineData(">  Title\n>  ===\n")]
    [InlineData(">  | a | b |\n>  |---|---|\n>  | 1 | 2 |\n")]
    [InlineData(">  ```\n>  code\n>  ```\n")]
    [InlineData(">  <div>\n>  html\n>  </div>\n")]
    [InlineData(">      indented code\n")]
    [InlineData(">  $$\n>  x = 1\n>  $$\n")]
    public void BlockQuoteFormatter_Format_OtherBlocksInQuote_KeepTheSpaces(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list, a footnote or a table row in a quote keeps the spaces, because they are indentation there.
    /// </summary>
    [Theory]
    [InlineData(">  - a\n")]
    [InlineData(">  1. a\n")]
    [InlineData("> - a\n>   continued\n")]
    [InlineData(">  [^n]: note\n")]
    public void BlockQuoteFormatter_Format_ListOrFootnoteInQuote_KeepsTheSpaces(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a quote inside a list item is left alone.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_QuoteInListItem_KeepsTheSpaces() =>
        AssertFormats("- >  quote\n", "- >  quote\n");

    /// <summary>
    ///     Test that a quote line that holds only the marker is left to the whitespace rule: the quote formatter leaves
    ///     that line as written while it rewrites the lines around it, and the whole tool then trims the line.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_MarkerOnlyLine_IsLeftToTheWhitespaceRule()
    {
        // Arrange: a quote with a marker-only line between two paragraph lines
        const string input = ">  a\n>   \n>  b\n";

        // Act & Assert: the unit leaves the marker-only line as written; the tool's whitespace rule trims it
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, "> a\n>   \n> b\n");
        BlockFormatterAssertions.AssertFormats(input, "> a\n>\n> b\n");
    }

    /// <summary>
    ///     Test that the same edits are proposed for CRLF and mixed line endings, and with a byte order mark.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_LineEndingsAndByteOrderMark_ProposeTheSameEdits()
    {
        // Arrange: a quote with extra spaces
        const string input = ">  a\n>   b\n>  # c\n";

        // Act & Assert: the edits do not depend on how the file is stored
        BlockFormatterAssertions.AssertSameEditsWithCrlf(Formatter, input);
        BlockFormatterAssertions.AssertSameEditsWithByteOrderMark(Formatter, input);
    }

    /// <summary>
    ///     Test that every edit is reported under the quote rule.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_ExtraSpaces_ReportsTheQuoteRule()
    {
        // Act: apply the formatter alone
        var (edits, _) = BlockFormatterAssertions.ApplyFormatter(Formatter, ">  a\n>   b\n");

        // Assert: two edits, both under the rule of the formatter
        Assert.Equal(2, edits.Length);
        Assert.All(edits, edit => Assert.Equal("blockquote-space", edit.RuleId));
    }

    /// <summary>
    ///     Test that a quote is left as it is when the rule is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable blockquote-space")]
    [InlineData("markdownlint-disable MD027")]
    [InlineData("lintmark-disable")]
    public void BlockQuoteFormatter_Format_RuleSuppressed_LeavesTheQuote(string directive)
    {
        // Arrange: a quote inside a region that disables the rule
        var text = $"<!-- {directive} -->\n\n>  a\n";

        // Act: format with the quote formatter registered
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), text);

        // Assert: nothing changes and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that formatting is deterministic when the same formatter instance serves several threads that share one document.
    /// </summary>
    [Fact]
    public void BlockQuoteFormatter_Format_SharedInstanceOnManyThreads_GivesTheSameResult() =>
        BlockFormatterAssertions.AssertConcurrentFormatting(Formatter, ">  a\n>   b\n\n>  >  c\n", "> a\n> b\n\n> > c\n");
}
