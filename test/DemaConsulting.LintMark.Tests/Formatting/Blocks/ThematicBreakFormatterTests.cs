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
///     Test that the ThematicBreakFormatter class writes thematic breaks as three characters of the configured kind,
///     removes trailing whitespace, and never converts a break where the new form would be read differently.
/// </summary>
public class ThematicBreakFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly ThematicBreakFormatter Formatter = new();

    /// <summary>
    ///     A configuration that asks for asterisk breaks.
    /// </summary>
    private static readonly LintMarkConfig AsteriskConfig = LintMarkConfig.Default with { ThematicBreak = ThematicBreakStyle.Asterisks };

    /// <summary>
    ///     A configuration that asks for underscore breaks.
    /// </summary>
    private static readonly LintMarkConfig UnderscoreConfig = LintMarkConfig.Default with { ThematicBreak = ThematicBreakStyle.Underscores };

    /// <summary>
    ///     A configuration that keeps plus sign list markers, so that the break after one is the only thing under test.
    /// </summary>
    private static readonly LintMarkConfig PlusConfig = LintMarkConfig.Default with { Bullet = BulletMarker.Plus };

    /// <summary>
    ///     A configuration that keeps plus sign list markers and asks for asterisk breaks.
    /// </summary>
    private static readonly LintMarkConfig PlusAsteriskConfig = PlusConfig with { ThematicBreak = ThematicBreakStyle.Asterisks };

    /// <summary>
    ///     A configuration that keeps plus sign list markers and asks for underscore breaks.
    /// </summary>
    private static readonly LintMarkConfig PlusUnderscoreConfig = PlusConfig with { ThematicBreak = ThematicBreakStyle.Underscores };

    /// <summary>
    ///     Asserts that the formatter alone turns the input into the expected text and that the result is safe and stable.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertFormats(string input, string expected, LintMarkConfig? config = null) =>
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected, config);

    /// <summary>
    ///     Test that thematic breaks are accepted and a setext underline and other blocks are not.
    /// </summary>
    [Theory]
    [InlineData("text\n\n---\n", true)]
    [InlineData("text\n\n***\n", true)]
    [InlineData("text\n\n- - -\n", true)]
    [InlineData("Title\n---\n", false)]
    [InlineData("text\n", false)]
    [InlineData("- item\n", false)]
    [InlineData("```\n---\n```\n", false)]
    public void ThematicBreakFormatter_CanFormat_Blocks_AcceptsOnlyThematicBreaks(string text, bool accepted)
    {
        // Arrange: a document
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: a break is accepted exactly when the document has one
        Assert.Equal(accepted, document.Blocks.Any(block => Formatter.CanFormat(block)));
        Assert.Equal(accepted, document.Blocks.Any(block => block.Kind == BlockKind.ThematicBreak));
    }

    /// <summary>
    ///     Test that a null block is rejected when asking whether it is accepted.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_CanFormat_NullBlock_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when formatting.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a thematic break, a context and an edit set
        var document = BlockFormatterAssertions.Parse("text\n\n***\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.ThematicBreak);
        var context = new FormatContext(document, LintMarkConfig.Default);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, block, null!)).ParamName);
    }

    /// <summary>
    ///     Test that a block that is not a thematic break is rejected when formatting.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_NonBreakBlock_ThrowsArgumentException()
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
    ///     Test that the formatter lists the two rules it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_RuleIds_Read_ListsTheBreakRuleAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the break rule and trailing whitespace, each registered, in an immutable array
        Assert.Equal(["thematic-break", "trailing-whitespace"], ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that every way of writing a break becomes three dashes by default.
    /// </summary>
    [Theory]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("- - -")]
    [InlineData("-----")]
    [InlineData("* * *")]
    [InlineData("_ _ _")]
    [InlineData("*****")]
    [InlineData("__________")]
    [InlineData("-  -  -")]
    [InlineData("-\t-\t-")]
    public void ThematicBreakFormatter_Format_AnyBreak_BecomesThreeDashes(string written) =>
        AssertFormats($"text\n\n{written}\n", "text\n\n---\n");

    /// <summary>
    ///     Test that a break becomes three asterisks or three underscores when the configuration asks for them.
    /// </summary>
    [Theory]
    [InlineData("---", "***")]
    [InlineData("___", "***")]
    [InlineData("- - -", "***")]
    [InlineData("***", "***")]
    public void ThematicBreakFormatter_Format_AsterisksConfiguration_WritesAsterisks(string written, string expected) =>
        AssertFormats($"text\n\n{written}\n", $"text\n\n{expected}\n", AsteriskConfig);

    /// <summary>
    ///     Test that a break becomes three underscores when the configuration asks for them.
    /// </summary>
    [Theory]
    [InlineData("---", "___")]
    [InlineData("***", "___")]
    [InlineData("* * *", "___")]
    [InlineData("___", "___")]
    public void ThematicBreakFormatter_Format_UnderscoresConfiguration_WritesUnderscores(string written, string expected) =>
        AssertFormats($"text\n\n{written}\n", $"text\n\n{expected}\n", UnderscoreConfig);

    /// <summary>
    ///     Test that a break on the first line of the file is converted to asterisks, which cannot start front matter.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_FirstLineWithAsterisksConfiguration_IsConverted() =>
        AssertFormats("---\n\ntext\n", "***\n\ntext\n", AsteriskConfig);

    /// <summary>
    ///     Test that the indentation of a break is kept.
    /// </summary>
    [Theory]
    [InlineData("text\n\n   ***\n", "text\n\n   ---\n")]
    [InlineData("text\n\n  - - -\n", "text\n\n  ---\n")]
    public void ThematicBreakFormatter_Format_IndentedBreak_KeepsTheIndentation(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that trailing whitespace after a break is removed, also when the break itself is already canonical.
    /// </summary>
    [Theory]
    [InlineData("text\n\n---   \n", "text\n\n---\n")]
    [InlineData("text\n\n***  \t\n", "text\n\n---\n")]
    [InlineData("text\n\n- - -   \n", "text\n\n---\n")]
    public void ThematicBreakFormatter_Format_TrailingWhitespace_IsRemoved(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a break inside a block quote or list item is converted in place.
    /// </summary>
    [Theory]
    [InlineData("> text\n>\n> ***\n", "> text\n>\n> ---\n")]
    [InlineData("- item\n\n  ***\n", "- item\n\n  ---\n")]
    [InlineData("> ***\n", "> ---\n")]
    public void ThematicBreakFormatter_Format_BreakInContainer_ConvertsInPlace(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a break directly under a line of text is not written as dashes, because the dashes would underline the text.
    /// </summary>
    [Theory]
    [InlineData("text\n***\n")]
    [InlineData("text\n- - -\n")]
    [InlineData("text\n___\n")]
    [InlineData("> text\n> ***\n")]
    [InlineData("- text\n  ***\n")]
    [InlineData("[a]: /url\n***\n")]
    public void ThematicBreakFormatter_Format_BreakUnderText_StaysAsWritten(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a break under text that carries trailing whitespace only loses the whitespace.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakUnderTextWithTrailingWhitespace_OnlyLosesTheWhitespace() =>
        AssertFormats("text\n***  \n", "text\n***\n");

    /// <summary>
    ///     Test that a break that is missing from the block list of a hand-built document has no previous sibling, so it
    ///     is not taken for a break under text and nothing is thrown.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakMissingFromBlockList_HasNoPreviousSibling()
    {
        // Arrange: a break directly under text, in a document whose block list lacks the break
        var parsed = BlockFormatterAssertions.Parse("text\n***\n");
        var block = BlockFormatterAssertions.FirstBlock(parsed, BlockKind.ThematicBreak);
        var document = parsed with { Blocks = [.. parsed.Blocks.Where(candidate => !ReferenceEquals(candidate, block))] };
        var edits = new EditSet();

        // Act: format the break that the list does not hold
        Formatter.Format(new FormatContext(document, LintMarkConfig.Default), block, edits);

        // Assert: the paragraph is not taken for a sibling above the break, so the break is converted
        Assert.NotEmpty(edits.ToImmutable());
    }

    /// <summary>
    ///     Test that a break directly under a heading, a code block, a list or a table is converted, because it cannot underline anything there.
    /// </summary>
    [Theory]
    [InlineData("# Heading\n***\n", "# Heading\n---\n")]
    [InlineData("```\ncode\n```\n***\n", "```\ncode\n```\n---\n")]
    [InlineData("    code\n***\n", "    code\n---\n")]
    [InlineData("- item\n***\n", "- item\n---\n")]
    [InlineData("> quote\n***\n", "> quote\n---\n")]
    public void ThematicBreakFormatter_Format_BreakAfterCompleteBlock_IsConverted(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a break on the first line of the file is not written as dashes, because that could start front matter.
    /// </summary>
    [Theory]
    [InlineData("***\n\ntext\n")]
    [InlineData("___\n\ntext\n")]
    [InlineData("- - -\n\ntext\n")]
    public void ThematicBreakFormatter_Format_FirstLineBreak_StaysAsWritten(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a break is not written as dashes when the first line of the file is dashes without front matter,
    ///     because the new dashes line would turn it into front matter.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_DashesLineAfterFirstLineDashes_StaysAsWritten() =>
        AssertFormats("---\n\ntext\n\n***\n", "---\n\ntext\n\n***\n");

    /// <summary>
    ///     Test that a break is converted to dashes when the document has real front matter.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakAfterFrontMatter_IsConverted() =>
        AssertFormats("---\ntitle: x\n---\n\ntext\n\n***\n", "---\ntitle: x\n---\n\ntext\n\n---\n");

    /// <summary>
    ///     Test that a break after a dash list marker is left as written when the configuration asks for dashes, because the converted line would be a break by itself.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakAfterDashListMarker_StaysAsWritten() =>
        AssertFormats("text\n\n- ***\n", "text\n\n- ***\n");

    /// <summary>
    ///     Test that a break after an asterisk list marker is left as written when the configuration asks for asterisks.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakAfterAsteriskListMarker_StaysAsWritten() =>
        AssertFormats("text\n\n* ---\n", "text\n\n* ---\n", AsteriskConfig);

    /// <summary>
    ///     Test that a break after a plus sign list marker is not written as dashes, because <c>+ ---</c> looks like a
    ///     grid table border, and is converted when the configuration wants another character.
    /// </summary>
    [Theory]
    [InlineData("+ ***\n")]
    [InlineData("text\n\n+ ***\n")]
    [InlineData("text\n\n+ * * *\n")]
    [InlineData("+ ___\n")]
    [InlineData("text\n\n+  ___\n")]
    [InlineData("> + ***\n")]
    public void ThematicBreakFormatter_Format_BreakAfterPlusListMarker_StaysAsWrittenWhenDashesAreWanted(string input)
    {
        // Act & Assert: the production formatters leave the break alone (the list marker space rule only closes up the gap after the marker), and the result renders the same and is stable
        BlockFormatterAssertions.AssertFormats(input, input.Replace("+  ", "+ ", StringComparison.Ordinal), PlusConfig);
        AssertFormats(input, input);
    }

    /// <summary>
    ///     Test that a break after a plus sign list marker is converted when asterisks or underscores are wanted.
    /// </summary>
    [Theory]
    [InlineData("text\n\n+ * * *\n", "text\n\n+ ***\n", true)]
    [InlineData("text\n\n+ ***\n", "text\n\n+ ___\n", false)]
    [InlineData("text\n\n+ - - -\n", "text\n\n+ ___\n", false)]
    public void ThematicBreakFormatter_Format_BreakAfterPlusListMarkerWithOtherCharacter_IsConverted(string input, string expected, bool asterisks)
    {
        // Act & Assert: the break becomes the configured characters, safely and stably
        BlockFormatterAssertions.AssertFormats(input, expected, asterisks ? PlusAsteriskConfig : PlusUnderscoreConfig);
        AssertFormats(input, expected, asterisks ? AsteriskConfig : UnderscoreConfig);
    }

    /// <summary>
    ///     Test that the trailing whitespace behind a guarded break is still removed.
    /// </summary>
    [Theory]
    [InlineData("text\n\n+ ***   \n", "text\n\n+ ***\n")]
    [InlineData("text\n\n- ***\t\n", "text\n\n- ***\n")]
    public void ThematicBreakFormatter_Format_GuardedBreakWithTrailingWhitespace_OnlyLosesTheWhitespace(string input, string expected)
    {
        // Act & Assert: the whitespace goes, the break stays
        BlockFormatterAssertions.AssertFormats(input, expected, input.Contains('+', StringComparison.Ordinal) ? PlusConfig : null);
        AssertFormats(input, expected);
    }

    /// <summary>
    ///     Test that a break behind an ordered list marker is converted, because the marker cannot be part of the break.
    /// </summary>
    [Theory]
    [InlineData("text\n\n1. ***\n", "text\n\n1. ---\n")]
    [InlineData("text\n\n2) * * *\n", "text\n\n2) ---\n")]
    public void ThematicBreakFormatter_Format_BreakAfterOrderedListMarker_IsConverted(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected);

    /// <summary>
    ///     Test that a break directly under a pipe table is converted, because a table is a complete block.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakAfterTable_IsConverted() =>
        AssertFormats("| a |\n|---|\n| 1 |\n***\n", "| a |\n|---|\n| 1 |\n---\n");

    /// <summary>
    ///     Test that a break on the line under front matter, HTML or a link reference definition counts as under text and
    ///     is left as written when dashes are wanted (the guard is conservative).
    /// </summary>
    [Theory]
    [InlineData("---\na: b\n---\n***\n")]
    [InlineData("<div>\nx\n</div>\n***\n")]
    [InlineData("[a]: /url\n***\n")]
    public void ThematicBreakFormatter_Format_BreakUnderNonTextBlocksThatCountAsText_StaysAsWritten(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that the first-line break is converted to underscores when the configuration asks for them, and that a
    ///     break after a list marker of the configured character is left as written for asterisks and converted for underscores.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_FirstLineAndListMarkerWithOtherConfigurations_FollowTheGuards()
    {
        // Act & Assert: underscores cannot start front matter and are not a list marker
        AssertFormats("***\n\ntext\n", "___\n\ntext\n", UnderscoreConfig);
        AssertFormats("text\n\n- ***\n", "text\n\n- ___\n", UnderscoreConfig);

        // An asterisk list marker keeps a break of asterisks and underscores as written: converted, the line would be a break by itself
        AssertFormats("text\n\n* ___\n", "text\n\n* ___\n", AsteriskConfig);
    }

    /// <summary>
    ///     Test that a break is left as written, with all of its whitespace, when any rule of the formatter is
    ///     suppressed in it, by identifier or by alias (<c>trailing-whitespace</c> and <c>thematic-break</c> are both rules of the formatter).
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable thematic-break")]
    [InlineData("markdownlint-disable MD035")]
    [InlineData("markdownlint-disable MD009")]
    [InlineData("lintmark-disable trailing-whitespace")]
    [InlineData("lintmark-disable")]
    public void ThematicBreakFormatter_Format_RuleSuppressed_LeavesTheBreak(string directive)
    {
        // Arrange: a break inside a region that disables the rule
        var text = $"<!-- {directive} -->\n\ntext\n\n***  \n";

        // Act: format with the thematic break formatter registered
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), text);

        // Assert: nothing changes, not even the whitespace behind the break, and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a break outside the region that suppresses a rule is formatted.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_BreakAfterEnabledAgain_IsFormatted()
    {
        // Arrange: one break in a disabled region and one after it
        const string input = "<!-- lintmark-disable thematic-break -->\n\ntext\n\n***\n\n<!-- lintmark-enable thematic-break -->\n\n***\n";

        // Act: format
        var (_, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), input);

        // Assert: only the later break changes
        Assert.Equal("<!-- lintmark-disable thematic-break -->\n\ntext\n\n***\n\n<!-- lintmark-enable thematic-break -->\n\n---\n", formatted);
    }

    /// <summary>
    ///     Test that every edit is reported under the rule of the violation it removes.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_NonCanonicalBreak_ReportsEachEditUnderItsRule()
    {
        // Arrange: a break with spaces and trailing whitespace
        const string input = "text\n\n* * *  \n";

        // Act: format
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: the break and the whitespace, in order of position
        Assert.Equal("text\n\n---\n", formatted);
        Assert.Equal(["thematic-break", "trailing-whitespace"], edits.Select(edit => edit.RuleId));
    }

    /// <summary>
    ///     Test that a canonical break produces no edit.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_CanonicalBreak_ProposesNothing()
    {
        // Act: format a canonical break
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, "text\n\n---\n");

        // Assert: nothing changes
        Assert.Empty(edits);
        Assert.Equal("text\n\n---\n", formatted);
    }

    /// <summary>
    ///     Test that CRLF line endings, and mixed line endings, do not change the edits that the formatter proposes.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_CrlfLineEndings_ProposesTheSameEdits() =>
        BlockFormatterAssertions.AssertSameEditsWithCrlf(Formatter, "text\n\n***  \n\nmore\n\n- - -\n");

    /// <summary>
    ///     Test that a byte order mark does not change the edits that the formatter proposes.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_ByteOrderMark_ProposesTheSameEdits() =>
        BlockFormatterAssertions.AssertSameEditsWithByteOrderMark(Formatter, "text\n\n***  \n\nmore\n\n- - -\n");

    /// <summary>
    ///     Test that CRLF line endings and a byte order mark survive formatting a break.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_CrlfAndBom_KeepsLineEndingsAndMark()
    {
        // Act & Assert: the written bytes keep the endings and the mark
        BlockFormatterAssertions.AssertFormatsOnDisk("text\n\n***  \n", "text\n\n---\n");
        BlockFormatterAssertions.AssertFormatsOnDisk("text\n\n***  \n\nmore\n\n- - -\n", "text\n\n---\n\nmore\n\n---\n");
    }

    /// <summary>
    ///     Test that formatting is deterministic when the same formatter instance serves several threads that share one document.
    /// </summary>
    [Fact]
    public void ThematicBreakFormatter_Format_SharedInstanceOnManyThreads_GivesTheSameResult() =>
        BlockFormatterAssertions.AssertConcurrentFormatting(
            Formatter,
            "text\n\n***\n\n___\n\n- - -\n\n+ ***\n",
            "text\n\n---\n\n---\n\n---\n\n+ ***\n");
}
