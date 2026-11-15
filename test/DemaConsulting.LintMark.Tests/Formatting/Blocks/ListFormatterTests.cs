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
    ///     Test that a marker is kept when the converted marker line would be a thematic break.
    /// </summary>
    [Theory]
    [InlineData("* ---\n", "* ---\n")]
    [InlineData("+ ---\n", "+ ---\n")]
    [InlineData("* - - -\n", "* ---\n")]
    public void ListFormatter_Format_ConvertedMarkerLineWouldBeThematicBreak_LeavesTheMarkers(string input, string toolExpected) =>
        AssertUnitKeepsInput(input, toolExpected);

    /// <summary>
    ///     Test that a marker is kept when the text after it is a thematic break of its own, which the thematic break
    ///     rule may write with the character that the converted marker would have.
    /// </summary>
    [Theory]
    [InlineData("* ___\n", "* ---\n")]
    public void ListFormatter_Format_ItemTextIsThematicBreak_LeavesTheMarkers(string input, string toolExpected) =>
        AssertUnitKeepsInput(input, toolExpected);

    /// <summary>
    ///     Test that a marker is kept when an item of the list is empty.
    /// </summary>
    [Theory]
    [InlineData("* a\n*\n", "* a\n*\n")]
    [InlineData("*\n* a\n", "*\n* a\n")]
    [InlineData("+ a\n+\n+ b\n", "+ a\n+\n+ b\n")]
    public void ListFormatter_Format_EmptyItem_LeavesTheMarkers(string input, string toolExpected) =>
        AssertUnitKeepsInput(input, toolExpected);

    /// <summary>
    ///     Test that a marker is not written before text that has the shape of the border of a grid table when the marker that
    ///     is written, or the one that it replaces, is a plus sign.
    /// </summary>
    [Theory]
    [InlineData("- :--\n", "Plus")]
    [InlineData("- =\n", "Plus")]
    [InlineData("- :-:\n- b\n", "Plus")]
    [InlineData("* ===\n* b\n", "Plus")]
    [InlineData("+ :--\n", "Dash")]
    [InlineData("+ ===\n+ b\n", "Dash")]
    public void ListFormatter_Format_PlusMarkerAndGridBorderLookalike_LeavesTheMarkers(string input, string bullet) =>
        AssertFormats(input, input, LintMarkConfig.Default with { Bullet = Enum.Parse<BulletMarker>(bullet) });

    /// <summary>
    ///     Test that text with the shape of the border of a grid table does not keep a marker from changing when no plus sign is
    ///     involved.
    /// </summary>
    [Theory]
    [InlineData("* :--\n", "- :--\n")]
    [InlineData("* =\n", "- =\n")]
    [InlineData("* :-:\n* b\n", "- :-:\n- b\n")]
    public void ListFormatter_Format_GridBorderLookalikeWithoutPlusMarker_ConvertsTheMarkers(string input, string expected) =>
        AssertFormats(input, expected);

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
    ///     Test that numbers of nine digits are counted up.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_NumbersOfNineDigits_AreCountedUp() =>
        AssertFormats("999999998. a\n999999998. b\n", "999999998. a\n999999999. b\n");

    /// <summary>
    ///     Test that the numbers are left as written when counting would need a tenth digit.
    /// </summary>
    [Theory]
    [InlineData("999999999. a\n999999999. b\n")]
    [InlineData("999999998. a\n999999998. b\n999999998. c\n")]
    public void ListFormatter_Format_NumberWouldHaveTenDigits_LeavesTheNumbers(string input) =>
        AssertFormats(input, input);

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
    ///     Test that a list stays as written when a line continues an item without being indented.
    /// </summary>
    [Theory]
    [InlineData("-   a\n  lazy\n")]
    [InlineData("1.  a\nlazy\n")]
    public void ListFormatter_Format_LazyLine_LeavesTheColumns(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list stays as written when a tab follows a marker.
    /// </summary>
    [Theory]
    [InlineData("-\ta\n")]
    [InlineData("1.\ta\n")]
    public void ListFormatter_Format_TabAfterMarker_LeavesTheColumns(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list stays as written when a tab indents a line of an item.
    /// </summary>
    [Theory]
    [InlineData("-   a\n\t- b\n")]
    [InlineData("-   a\n\n\tcode\n")]
    public void ListFormatter_Format_TabIndentedLine_LeavesTheColumns(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a list stays as written when five spaces after a marker make the content indented code.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_FiveSpacesAfterMarker_LeavesTheColumns() =>
        AssertFormats("-     code\n", "-     code\n");

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
    [InlineData("1. a\n      - b\n")]
    public void ListFormatter_Format_OtherNesting_KeepsTheOffset(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that bullet lists that stand next to each other inside one item keep their markers.
    /// </summary>
    [Theory]
    [InlineData("- a\n  * b\n\n  + c\n")]
    [InlineData("1. a\n   * b\n\n   + c\n")]
    public void ListFormatter_Format_AdjacentNestedBulletLists_KeepTheirMarkers(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the markers are still written when the columns of the nest cannot change without changing the rendering.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ColumnChangeWouldChangeTheRendering_StillWritesTheMarkers()
    {
        // Arrange: an indented bullet list directly followed by an ordered list; moving the bullet list to the margin would
        // pull the second line into its item
        const string input = "  * a\n  1. b\n";

        // Act: the list formatter alone changes the marker and nothing else, and the tool adds the blank line that separates the lists
        var (_, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: the column of the list is as written
        Assert.Equal("  - a\n  1. b\n", formatted);
        BlockFormatterAssertions.AssertFormats(input, "  - a\n\n  1. b\n");
    }

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
            "-   item\n\n    ```text\n    code\n\n    more\n    ```\n\n    <div>\n    html\n    </div>\n\n    > quote\n\n    | a | b |\n    | --- | --- |\n    | 1 | 2 |\n",
            "- item\n\n  ```text\n  code\n\n  more\n  ```\n\n  <div>\n  html\n  </div>\n\n  > quote\n\n  | a | b |\n  | --- | --- |\n  | 1 | 2 |\n");

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
    ///     Test that a list is left as it is when a rule of the formatter is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable bullet")]
    [InlineData("markdownlint-disable MD004")]
    [InlineData("markdownlint-disable MD005")]
    [InlineData("markdownlint-disable MD007")]
    [InlineData("markdownlint-disable MD029")]
    [InlineData("lintmark-disable ordered-style")]
    [InlineData("markdownlint-disable MD030")]
    [InlineData("lintmark-disable list-marker-space")]
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
    ///     Test that the list rules read the text that the other rules produce, so that one run is enough (rule R-1: the line after
    ///     an item that shrinks stays outside it, where the nest after it moves that line to the margin).
    /// </summary>
    [Fact]
    public void ListFormatter_Format_LineAfterMovesToTheMargin_LetsTheItemShrinkInOneRun() =>
        BlockFormatterAssertions.AssertFormats("11. |---|---|\n-   \\\n   +   item\n", "11. |---|---|\n\n- \\\n\n+ item\n");

    /// <summary>
    ///     Test that a nest with a fenced code line below a nested item keeps its columns (rule R-2: in a nested item such a line
    ///     is read from columns that are not the content column), and that the same fence in a plain item moves with it.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_FenceInNestedItem_KeepsTheColumns()
    {
        // Act & Assert: the nested item keeps its spaces, and the plain item with a fence moves
        AssertUnitKeepsInput("-  > quote\n    11. ```\n        11. second words\n", "-  > quote\n    11. ```\n        11. second words\n");
        AssertFormats("-   a\n    - b\n\n    ```\n    code\n    ```\n", "- a\n  - b\n\n  ```\n  code\n  ```\n");
    }

    /// <summary>
    ///     Test that a continuation line that starts with block syntax keeps the columns of its item (rule G-2: the line is text
    ///     only because of its indentation).
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ContinuationLineStartsWithBlockSyntax_KeepsTheColumns() =>
        AssertUnitKeepsInput("-   a\n    b\n        - c\n", "-   a\n    b\n        - c\n");

    /// <summary>
    ///     Test that an item whose marker line would be a thematic break by itself stays where it is (rule G-1), while the marker
    ///     character of the list is still written.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_MarkerLineWouldBeThematicBreak_KeepsTheColumns() =>
        AssertFormats("*   a\n    - ---\n", "-   a\n    - ---\n");

    /// <summary>
    ///     Test that an item whose text mixes the characters of a thematic break is an ordinary item.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_MarkerLineWithMixedBreakCharacters_IsAnOrdinaryItem() =>
        AssertFormats("*   -*-\n", "- -*-\n");

    /// <summary>
    ///     Test that an item that starts with a thematic break and has more lines, or a nest that holds such an item, keeps its columns
    ///     (rule G-3: the reading of a later line at a relative indent of three depends on the absolute column).
    /// </summary>
    [Theory]
    [InlineData("  -  ***\n      - z\n      x = 1\n           # h\n")]
    [InlineData("-  x\n   1. ---\n          code\n")]
    public void ListFormatter_Format_ItemStartsWithThematicBreak_KeepsTheColumns(string input) =>
        BlockFormatterAssertions.AssertFormatsSafely(input, input);

    /// <summary>
    ///     Test that a nested item with a block three columns past its content column keeps its columns (rule G-4), while the same
    ///     nest without such a block moves.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_BlockFarInNestedItem_KeepsTheColumns()
    {
        // Act & Assert: the marker character is written, the columns stay; with the line two columns in, the nest moves
        BlockFormatterAssertions.AssertFormatsSafely("  * x = 1\n     1)  x = 1\n\n            code\n", "  - x = 1\n     1)  x = 1\n\n            code\n");
        BlockFormatterAssertions.AssertFormatsSafely("  * x = 1\n     1)  x = 1\n\n           code\n", "- x = 1\n   1) x = 1\n\n       code\n");
    }

    /// <summary>
    ///     Test that a continuation line that starts with an ordered marker that cannot interrupt a paragraph does not keep the
    ///     columns of its item (rule G-2 only concerns block syntax that the indentation hides).
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ContinuationLineWithNumberOtherThanOne_MovesWithItsItem() =>
        BlockFormatterAssertions.AssertFormatsSafely("-   text\n    10. more\n", "- text\n  10. more\n");

    /// <summary>
    ///     Test that a nested list whose marker is one column past the content column of its item keeps its lines where the parser
    ///     reads them (rule G-5, issue 22): when the marker is indented by fewer than four columns from the item around it, the
    ///     parser counts the content of the nested item from the content column of the outer item, so the content column
    ///     is not the sum of the marker column and the width, and a move by that sum puts a setext underline, a code line or a
    ///     sibling item where the parser reads them as something else. The nest is moved by the columns the parser reads, or kept.
    /// </summary>
    [Theory]
    [InlineData(" + x\n    01. t\n           =\n", "- x\n   01. t\n         =\n")]
    [InlineData("   +  c\n       001.   >\n                 1.\n", "- c\n   001. >\n          1.\n")]
    [InlineData("  -   [\n       01. -\n          1.\n", "  -   [\n       01. -\n          1.\n")]
    [InlineData("+  #\n    1)\n          -\n", "-  #\n    1)\n          -\n")]
    public void ListFormatter_Format_NestedMarkerOffsetByOne_KeepsTheLinesWhereTheParserReadsThem(string input, string expected) =>
        BlockFormatterAssertions.AssertFormatsSafely(input, expected);

    /// <summary>
    ///     Test that the shrinking of an item and the code block of another rule happen in one run: the block that a rule rewrites
    ///     as a fence is no danger to the list, so the list does not have to wait for the second run.
    /// </summary>
    [Fact]
    public void ListFormatter_Format_ItemAndCodeBlockOfAnotherRule_AreSettledInOneRun() =>
        BlockFormatterAssertions.AssertFormats("-    one\n\n     two\n", "- one\n\n  two\n");

    /// <summary>
    ///     Test that a nested ordered marker with two spaces after it that does not move by its own plan (it starts a list on its marker
    ///     line) keeps its spacing and the meaning of its continuation lines while the nest around it moves, or keeps its columns
    ///     (rule G-5; answers the review of the guard, which modeled the width of the marker as written). The rewrite renders the same.
    /// </summary>
    [Theory]
    [InlineData("-   a\n    1.  - x\n          c\n", "- a\n  1.  - x\n        c\n")]
    [InlineData(" -  x\n    1.  - t\n        c\n", "- x\n  1.  - t\n      c\n")]
    [InlineData("  -   x\n       01.  - t\n              =\n", "  -   x\n       01.  - t\n              =\n")]
    [InlineData(" + x\n    01.  - t\n            =\n", " - x\n    01.  - t\n            =\n")]
    public void ListFormatter_Format_NestedMarkerWithTwoSpacesNotMovedByItself_KeepsItsSpacingAndMeaning(string input, string expected)
    {
        // Act
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input);

        // Assert: the verifier-equivalent HTML is identical (checked by AssertSafeAndStable), the spacing of the marker is as written
        Assert.Equal(expected, formatted);
        Assert.Matches(@"\d+\.  - ", formatted);
    }

    /// <summary>
    ///     Test that the content column of a nested item is the one the parser reads: the offset of the marker counts only when the
    ///     marker is indented by four or more columns from the content column of the item around the holder.
    /// </summary>
    [Theory]
    [InlineData(3, 4, 2, 0, 6)]
    [InlineData(2, 4, 2, 0, 6)]
    [InlineData(4, 4, 2, 0, 8)]
    [InlineData(4, 4, 3, 0, 8)]
    [InlineData(5, 4, 4, 2, 8)]
    [InlineData(6, 4, 4, 2, 10)]
    public void ListModel_ParserContentColumn_NestedMarker_FollowsTheRuleOfTheParser(int markerColumn, int width, int holderContent, int holderBase, int expected) =>
        Assert.Equal(expected, ListModel.ParserContentColumn(markerColumn, width, new HolderColumns(holderContent, holderBase)));
}
