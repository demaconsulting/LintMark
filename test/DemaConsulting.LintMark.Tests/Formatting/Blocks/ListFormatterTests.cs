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
///     Test that the ListFormatter class writes lists in the canonical form: the configured marker character, numbers
///     that count the configured way, items that start in the same column, nested bullet lists indented as configured and
///     one space after each marker, and that it leaves alone what it cannot change without changing the meaning.
/// </summary>
public class ListFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly ListFormatter Formatter = new();

    /// <summary>
    ///     Asserts that the production formatters turn the input into the expected text, safely and stably, in every
    ///     line ending, and that the list formatter alone gives the same text when the document needs nothing else.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertFormats(string input, string expected, LintMarkConfig? config = null)
    {
        BlockFormatterAssertions.AssertFormats(input, expected, config);
        BlockFormatterAssertions.AssertFormatsOnDisk(input, expected, config);
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected, config);
    }

    /// <summary>
    ///     Asserts that the list formatter alone leaves the input as written, and that the production formatters, whose
    ///     other rules may change a different part of the text, give the expected text.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="toolExpected">The text the production formatters give.</param>
    private static void AssertUnitKeepsInput(string input, string toolExpected)
    {
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, input);
        BlockFormatterAssertions.AssertFormats(input, toolExpected);
        BlockFormatterAssertions.AssertFormatsOnDisk(input, toolExpected);
    }

    /// <summary>
    ///     Test that only the outermost list of a nest is accepted.
    /// </summary>
    [Theory]
    [InlineData("- a\n- b\n", 1)]
    [InlineData("- a\n  - b\n", 1)]
    [InlineData("1. a\n   1. b\n", 1)]
    [InlineData("- a\n\n<!-- c -->\n\n- b\n", 2)]
    [InlineData("text\n", 0)]
    [InlineData("> - a\n", 1)]
    public void ListFormatter_CanFormat_Blocks_AcceptsOnlyOutermostLists(string text, int accepted)
    {
        // Arrange: a document
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: the number of accepted blocks is the number of outermost lists
        Assert.Equal(accepted, document.Blocks.Count(block => Formatter.CanFormat(block)));
    }

    /// <summary>
    ///     Test that a missing block is rejected when asking whether it is accepted.
    /// </summary>
    [Fact]
    public void ListFormatter_CanFormat_NullBlock_Throws()
    {
        // Act & Assert: the argument is named
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!)).ParamName);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when formatting.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a list, a context and an edit set
        var document = BlockFormatterAssertions.Parse("* a\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.List);
        var context = new FormatContext(document, LintMarkConfig.Default);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, block, null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not an outermost list is rejected when formatting.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_OtherBlock_ThrowsArgumentException()
    {
        // Arrange: a paragraph, and a nested list
        var paragraph = BlockFormatterAssertions.Parse("text\n");
        var nested = BlockFormatterAssertions.Parse("- a\n  - b\n");
        var inner = nested.Blocks.Last(block => block.Kind == BlockKind.List);

        // Act & Assert: both are rejected and the parameter is named
        var first = Assert.Throws<ArgumentException>(() =>
            Formatter.Format(new FormatContext(paragraph, LintMarkConfig.Default), BlockFormatterAssertions.FirstBlock(paragraph, BlockKind.Paragraph), new EditSet()));
        var second = Assert.Throws<ArgumentException>(() => Formatter.Format(new FormatContext(nested, LintMarkConfig.Default), inner, new EditSet()));
        Assert.Equal("block", first.ParamName);
        Assert.Equal("block", second.ParamName);
    }

    /// <summary>
    ///     Test that the formatter lists the four rules it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void ListFormatter_RuleIds_Read_ListsTheFourRulesAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the four rules, each registered, in an immutable array
        Assert.Equal(["bullet", "ordered-style", "list-indent", "list-marker-space"], ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that every item of a bullet list is written with the default marker.
    /// </summary>
    [Theory]
    [InlineData("* a\n* b\n", "- a\n- b\n")]
    [InlineData("+ a\n+ b\n", "- a\n- b\n")]
    [InlineData("- a\n- b\n", "- a\n- b\n")]
    [InlineData("* a\n  + b\n", "- a\n  - b\n")]
    [InlineData("> * a\n> * b\n", "> - a\n> - b\n")]
    [InlineData("1. a\n   * b\n", "1. a\n   - b\n")]
    public void ListFormatter_Format_BulletMarkers_BecomeTheDefaultCharacter(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that every item of a bullet list is written with the configured marker.
    /// </summary>
    [Theory]
    [InlineData("Asterisk", "- a\n- b\n", "* a\n* b\n")]
    [InlineData("Plus", "- a\n- b\n", "+ a\n+ b\n")]
    [InlineData("Dash", "* a\n* b\n", "- a\n- b\n")]
    [InlineData("Plus", "* a\n  - b\n", "+ a\n  + b\n")]
    public void ListFormatter_Format_ConfiguredBullet_IsUsed(string bullet, string input, string expected) =>
        AssertFormats(input, expected, LintMarkConfig.Default with { Bullet = Enum.Parse<BulletMarker>(bullet) });

    /// <summary>
    ///     Test that two bullet lists that stand next to each other keep their markers, because one marker would merge them.
    /// </summary>
    [Theory]
    [InlineData("- a\n\n* b\n")]
    [InlineData("* a\n\n+ b\n")]
    public void ListFormatter_Format_AdjacentBulletLists_KeepTheirMarkers(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that bullet lists that another block separates are both converted.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_BulletListsSeparatedByAComment_AreBothConverted() =>
        AssertFormats("* a\n\n<!-- x -->\n\n+ b\n", "- a\n\n<!-- x -->\n\n- b\n");

    /// <summary>
    ///     Test that a marker is kept when the converted line would be a thematic break, when an item is empty, and when
    ///     the text after the marker looks like the border of a grid table.
    /// </summary>
    [Theory]
    [InlineData("* ---\n", "* ---\n")]
    [InlineData("* ***\n\ntext\n", "* ***\n\ntext\n")]
    [InlineData("* a\n*\n", "* a\n*\n")]
    [InlineData("+ ---\n", "+ ---\n")]
    [InlineData("* - - -\n", "* ---\n")]
    public void ListFormatter_Format_UnsafeMarkerChange_LeavesTheMarkers(string input, string toolExpected) =>
        AssertUnitKeepsInput(input, toolExpected);

    /// <summary>
    ///     Test that the numbers of an ordered list count up from the first number by default.
    /// </summary>
    [Theory]
    [InlineData("1. a\n1. b\n1. c\n", "1. a\n2. b\n3. c\n")]
    [InlineData("1. a\n3. b\n7. c\n", "1. a\n2. b\n3. c\n")]
    [InlineData("5. a\n5. b\n", "5. a\n6. b\n")]
    [InlineData("0. a\n0. b\n", "0. a\n1. b\n")]
    [InlineData("1) a\n1) b\n", "1) a\n2) b\n")]
    [InlineData("1. a\n2. b\n", "1. a\n2. b\n")]
    [InlineData("> 1. a\n> 1. b\n", "> 1. a\n> 2. b\n")]
    public void ListFormatter_Format_OrderedNumbers_CountUpFromTheFirst(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the numbers of an ordered list repeat the first number when that style is configured.
    /// </summary>
    [Theory]
    [InlineData("1. a\n2. b\n3. c\n", "1. a\n1. b\n1. c\n")]
    [InlineData("5. a\n6. b\n", "5. a\n5. b\n")]
    [InlineData("1) a\n2) b\n", "1) a\n1) b\n")]
    public void ListFormatter_Format_OneStyle_RepeatsTheFirstNumber(string input, string expected) =>
        AssertFormats(input, expected, LintMarkConfig.Default with { OrderedStyle = OrderedStyle.One });

    /// <summary>
    ///     Test that a first number with leading zeros leaves the numbering as written.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_FirstNumberWithLeadingZeros_LeavesTheNumbers() =>
        AssertFormats("01. a\n01. b\n", "01. a\n01. b\n");

    /// <summary>
    ///     Test that a number that gains a digit moves the content of its item with it.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NumberGainsADigit_MovesTheContentOfTheItem() =>
        AssertFormats(
            "1. a\n1. b\n1. c\n1. d\n1. e\n1. f\n1. g\n1. h\n1. i\n1. j\n   more\n",
            "1. a\n2. b\n3. c\n4. d\n5. e\n6. f\n7. g\n8. h\n9. i\n10. j\n    more\n");

    /// <summary>
    ///     Test that a number that loses a digit moves the content of its item with it.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NumberLosesADigit_MovesTheContentOfTheItem() =>
        AssertFormats(
            "1. a\n2. b\n3. c\n4. d\n5. e\n6. f\n7. g\n8. h\n9. i\n10. j\n    more\n",
            "1. a\n1. b\n1. c\n1. d\n1. e\n1. f\n1. g\n1. h\n1. i\n1. j\n   more\n",
            LintMarkConfig.Default with { OrderedStyle = OrderedStyle.One });

    /// <summary>
    ///     Test that the numbers are left as written when a number that changes its width cannot move its item.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NumberGainsADigitInAQuote_LeavesTheNumbers() =>
        AssertFormats(
            "> 1. a\n> 1. b\n> 1. c\n> 1. d\n> 1. e\n> 1. f\n> 1. g\n> 1. h\n> 1. i\n> 1. j\n",
            "> 1. a\n> 1. b\n> 1. c\n> 1. d\n> 1. e\n> 1. f\n> 1. g\n> 1. h\n> 1. i\n> 1. j\n");

    /// <summary>
    ///     Test that more than one space after a marker becomes one space, and the item moves with its marker.
    /// </summary>
    [Theory]
    [InlineData("-   a\n-   b\n", "- a\n- b\n")]
    [InlineData("1.  a\n2.  b\n", "1. a\n2. b\n")]
    [InlineData("-   a\n    b\n", "- a\n  b\n")]
    [InlineData("-   a\n    - b\n", "- a\n  - b\n")]
    [InlineData("-   a\n\n    second\n", "- a\n\n  second\n")]
    [InlineData("-   a\n\n    ```\n    code\n    ```\n", "- a\n\n  ```\n  code\n  ```\n")]
    [InlineData("-   a\n\n        code\n", "- a\n\n      code\n")]
    [InlineData("-   a\n    > quote\n", "- a\n  > quote\n")]
    [InlineData("1.  a\n    - b\n", "1. a\n   - b\n")]
    public void ListFormatter_Format_SpacesAfterMarker_BecomeOne(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a list stays as written when a line is a lazy continuation, a tab follows the marker, or five
    ///     spaces make the content indented code.
    /// </summary>
    [Theory]
    [InlineData("-   a\n  lazy\n")]
    [InlineData("-\ta\n")]
    [InlineData("-     code\n")]
    [InlineData("-   a\n\t- b\n")]
    public void ListFormatter_Format_ColumnsCannotChangeSafely_LeavesTheItems(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the items of a list start in the same column, and a top-level list starts at the margin.
    /// </summary>
    [Theory]
    [InlineData("  - a\n  - b\n", "- a\n- b\n")]
    [InlineData("- a\n - b\n", "- a\n- b\n")]
    [InlineData("   - a\n   - b\n      c\n", "- a\n- b\n   c\n")]
    [InlineData("  1. a\n  2. b\n", "  1. a\n  2. b\n")]
    [InlineData(" 1. a\n  2. b\n", " 1. a\n 2. b\n")]
    public void ListFormatter_Format_ItemColumns_AreAlignedAndTopLevelBulletsStartAtTheMargin(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a bullet list in a bullet item is indented two columns by default and four when configured.
    /// </summary>
    [Theory]
    [InlineData("- a\n    - b\n", "- a\n  - b\n", 2)]
    [InlineData("- a\n  - b\n", "- a\n    - b\n", 4)]
    [InlineData("- a\n    - b\n        - c\n", "- a\n  - b\n    - c\n", 2)]
    [InlineData("- a\n  - b\n    - c\n", "- a\n    - b\n        - c\n", 4)]
    [InlineData("- a\n  - b\n    more\n", "- a\n    - b\n      more\n", 4)]
    public void ListFormatter_Format_NestedBullets_AreIndentedAsConfigured(string input, string expected, int indent) =>
        AssertFormats(input, expected, LintMarkConfig.Default with { ListIndent = indent });

    /// <summary>
    ///     Test that a list that is not a bullet in a bullet item keeps its offset to the content of its item.
    /// </summary>
    [Theory]
    [InlineData("1. a\n   - b\n")]
    [InlineData("- a\n  1. b\n")]
    [InlineData("1. a\n   1. b\n")]
    [InlineData("1. a\n    - b\n")]
    public void ListFormatter_Format_OtherNesting_KeepsTheOffset(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list in a block quote keeps its columns.
    /// </summary>
    [Theory]
    [InlineData("> -   a\n> -   b\n")]
    [InlineData(">   - a\n>   - b\n")]
    public void ListFormatter_Format_ListInQuote_KeepsItsColumns(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list in the body of a referenced footnote is left as written.
    /// </summary>
    [Theory]
    [InlineData("x[^n]\n\n[^n]: text\n\n    * a\n")]
    [InlineData("x[^n]\n\n[^n]: text\n\n    -   a\n")]
    [InlineData("x[^n]\n\n[^n]: text\n\n    1. a\n    1. b\n")]
    public void ListFormatter_Format_ListInFootnote_IsLeftAsWritten(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that loose and tight lists stay loose and tight when they are moved.
    /// </summary>
    [Theory]
    [InlineData("-   a\n-   b\n", "- a\n- b\n")]
    [InlineData("-   a\n\n-   b\n", "- a\n\n- b\n")]
    [InlineData("1.  a\n\n    b\n\n2.  c\n", "1. a\n\n   b\n\n2. c\n")]
    public void ListFormatter_Format_LooseAndTightLists_StayLooseAndTight(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that code, HTML, quotes and a table inside an item move with it.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ItemHoldsCodeHtmlQuoteAndTable_MovesEverythingTogether() =>
        AssertFormats(
            "-   item\n\n    ```text\n    code\n\n    more\n    ```\n\n    <div>\n    html\n    </div>\n\n    > quote\n\n    | a | b |\n    |---|---|\n    | 1 | 2 |\n",
            "- item\n\n  ```text\n  code\n\n  more\n  ```\n\n  <div>\n  html\n  </div>\n\n  > quote\n\n  | a | b |\n  |---|---|\n  | 1 | 2 |\n");

    /// <summary>
    ///     Test that a list whose columns cannot move is not given to a move that would pull a following line into it.
    /// </summary>
    [Theory]
    [InlineData("  - a\n  1. b\n", "  - a\n\n  1. b\n")]
    [InlineData("  - a\n\n   text\n", "  - a\n\n   text\n")]
    [InlineData("-   a\n  \n  text\n", "-   a\n\n  text\n")]
    public void ListFormatter_Format_FollowingLineWouldJoinTheItem_LeavesTheColumns(string input, string toolExpected) =>
        AssertUnitKeepsInput(input, toolExpected);

    /// <summary>
    ///     Test that Pandoc syntax inside an item moves with it and keeps its structure.
    /// </summary>
    [Theory]
    [InlineData("-   item\n\n    ::: note\n    text\n    :::\n", "- item\n\n  ::: note\n  text\n  :::\n")]
    [InlineData("-   item\n\n    $$\n    x = 1\n    $$\n", "- item\n\n  $$\n  x = 1\n  $$\n")]
    [InlineData("-   item [@cite]\n\n    {#id}\n", "- item [@cite]\n\n  {#id}\n")]
    [InlineData("-   item\n\n    \\par\n", "- item\n\n  \\par\n")]
    public void ListFormatter_Format_PandocSyntaxInItem_MovesWithTheItem(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a grid table inside an item keeps the item where it is.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_GridTableInItem_LeavesTheColumns() =>
        AssertFormats(
            "-   item\n\n    +---+---+\n    | a | b |\n    +---+---+\n",
            "-   item\n\n    +---+---+\n    | a | b |\n    +---+---+\n");

    /// <summary>
    ///     Test that task list items, empty items and items with only a nested list are handled.
    /// </summary>
    [Theory]
    [InlineData("* [ ] a\n* [x] b\n", "- [ ] a\n- [x] b\n")]
    [InlineData("-\n- a\n", "-\n- a\n")]
    [InlineData("* - a\n", "- - a\n")]
    public void ListFormatter_Format_TaskAndEmptyItems_AreHandled(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that the same edits are proposed for CRLF and mixed line endings, and with a byte order mark.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_LineEndingsAndByteOrderMark_ProposeTheSameEdits()
    {
        // Arrange: a list that needs a marker, a number, a column and a space
        const string input = "* a\n  *   b\n1. c\n1. d\n";

        // Act & Assert: the edits do not depend on how the file is stored
        BlockFormatterAssertions.AssertSameEditsWithCrlf(Formatter, input);
        BlockFormatterAssertions.AssertSameEditsWithByteOrderMark(Formatter, input);
    }

    /// <summary>
    ///     Test that every edit is reported under the rule of the violation it removes.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NonCanonicalList_ReportsEachEditUnderItsRule()
    {
        // Arrange: a list with a wrong marker, a wrong number, an indented item and extra spaces
        const string input = "* a\n * b\n\n1. c\n1. d\n\n-   e\n";

        // Act: apply the formatter alone and read the rules of the edits
        var (edits, _) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: each rule of the formatter reports, and no other
        var rules = edits.Select(edit => edit.RuleId).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["bullet", "list-indent", "list-marker-space", "ordered-style"], rules);
        Assert.All(rules, rule => Assert.Contains(rule, Formatter.RuleIds));
    }

    /// <summary>
    ///     Test that a list is left as it is when a rule of the formatter is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable bullet")]
    [InlineData("markdownlint-disable MD004")]
    [InlineData("markdownlint-disable MD005")]
    [InlineData("markdownlint-disable MD007")]
    [InlineData("markdownlint-disable MD029")]
    [InlineData("markdownlint-disable MD030")]
    [InlineData("lintmark-disable list-indent")]
    [InlineData("lintmark-disable")]
    public void ListFormatter_Format_RuleSuppressed_LeavesTheList(string directive)
    {
        // Arrange: a list inside a region that disables the rule
        var text = $"<!-- {directive} -->\n\n*   a\n*   b\n\n1. c\n1. d\n";

        // Act: format with the list formatter registered
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), text);

        // Assert: nothing changes and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a list after a region that disabled a rule is formatted again.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ListAfterEnabledAgain_IsFormatted()
    {
        // Arrange: one list in a disabled region and one after it
        const string input = "<!-- lintmark-disable bullet -->\n\n* a\n\n<!-- lintmark-enable bullet -->\n\ntext\n\n* b\n";

        // Act: format
        var (_, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), input);

        // Assert: only the later list changes
        Assert.Equal("<!-- lintmark-disable bullet -->\n\n* a\n\n<!-- lintmark-enable bullet -->\n\ntext\n\n- b\n", formatted);
    }

    /// <summary>
    ///     Test that formatting is deterministic when the same formatter instance serves several threads that share one document.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_SharedInstanceOnManyThreads_GivesTheSameResult() =>
        BlockFormatterAssertions.AssertConcurrentFormatting(
            Formatter,
            "* a\n  +   b\n\n1. c\n1. d\n\n-   e\n",
            "- a\n  - b\n\n1. c\n2. d\n\n- e\n");

    /// <summary>
    ///     Test that a list formatted with an unusual file layout (no final newline, a blank line first) is still safe.
    /// </summary>
    [Theory]
    [InlineData("* a")]
    [InlineData("\n\n* a\n* b\n")]
    [InlineData("* a\n\n\n\n* b\n")]
    [InlineData("text\n* a\n* b\n")]
    public void ListFormatter_Format_UnusualLayout_IsSafeAndStable(string input) =>
        BlockFormatterAssertions.AssertSafeAndStable(input);

    /// <summary>
    ///     Test that a list in every shape of the synthetic nesting documents is safe and stable with every list setting.
    /// </summary>
    [Theory]
    [InlineData("Asterisk", 2, "Incrementing")]
    [InlineData("Plus", 4, "One")]
    public void ListFormatter_Format_SyntheticNestingDocuments_AreSafeAndStable(string bullet, int indent, string style)
    {
        // Arrange: the container chains around every body, and a configuration that changes the list settings
        var config = LintMarkConfig.Default with
        {
            Bullet = Enum.Parse<BulletMarker>(bullet),
            ListIndent = indent,
            OrderedStyle = Enum.Parse<OrderedStyle>(style)
        };
        var fixtures = SyntheticDocuments.Generate().Where(fixture => fixture.Chain.Contains('L') || fixture.Chain.Contains('O')).ToList();

        // Act & Assert: every fixture passes the safety assertions
        Assert.NotEmpty(fixtures);
        foreach (var fixture in fixtures)
        {
            BlockFormatterAssertions.AssertSafeAndStable(fixture.Text, config, checkParallel: false);
        }
    }
}
