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
///     Test that the CodeBlockFormatter class writes code blocks fenced with the configured fence character, keeps
///     their content exactly as written, and never touches an unclosed fence.
/// </summary>
public class CodeBlockFormatterTests
{
    /// <summary>
    ///     The formatter under test.
    /// </summary>
    private static readonly CodeBlockFormatter Formatter = new();

    /// <summary>
    ///     A configuration that asks for tilde fences.
    /// </summary>
    private static readonly LintMarkConfig TildeConfig = LintMarkConfig.Default with { CodeFence = FenceMarker.Tilde };

    /// <summary>
    ///     Asserts that the formatter alone turns the input into the expected text and that the result is safe and stable.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected output.</param>
    /// <param name="config">The configuration, or <see langword="null"/> for the defaults.</param>
    private static void AssertFormats(string input, string expected, LintMarkConfig? config = null) =>
        BlockFormatterAssertions.AssertFormatterOutput(Formatter, input, expected, config);

    /// <summary>
    ///     Test that closed fenced code blocks and indented code blocks are accepted.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n```\n")]
    [InlineData("~~~\ncode\n~~~\n")]
    [InlineData("    code\n")]
    [InlineData("- item\n\n      code\n")]
    public void CodeBlockFormatter_CanFormat_ClosedFencedAndIndentedCode_ReturnsTrue(string text)
    {
        // Arrange: a document with a code block
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: the code block is accepted
        Assert.Contains(document.Blocks, block => Formatter.CanFormat(block));
    }

    /// <summary>
    ///     Test that an unclosed fence and blocks that are not code are not accepted.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n")]
    [InlineData("~~~\ncode\n")]
    [InlineData("text\n")]
    [InlineData("# Heading\n")]
    [InlineData("---\n")]
    [InlineData("<div>\n</div>\n")]
    [InlineData("$$\nx\n$$\n")]
    public void CodeBlockFormatter_CanFormat_UnclosedFenceAndOtherBlocks_ReturnsFalse(string text)
    {
        // Arrange: a document without a closed code block
        var document = BlockFormatterAssertions.Parse(text);

        // Act & Assert: no block is accepted
        Assert.All(document.Blocks, block => Assert.False(Formatter.CanFormat(block)));
    }

    /// <summary>
    ///     Test that a null block is rejected when asking whether it is accepted.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_CanFormat_NullBlock_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => Formatter.CanFormat(null!));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that a missing argument is rejected when formatting.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_NullArguments_Throw()
    {
        // Arrange: a code block, a context and an edit set
        var document = BlockFormatterAssertions.Parse("~~~\ncode\n~~~\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.FencedCode);
        var context = new FormatContext(document, LintMarkConfig.Default);
        var edits = new EditSet();

        // Act & Assert: each argument is checked and named
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Formatter.Format(null!, block, edits)).ParamName);
        Assert.Equal("block", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, null!, edits)).ParamName);
        Assert.Equal("edits", Assert.Throws<ArgumentNullException>(() => Formatter.Format(context, block, null!)).ParamName);
    }

    /// <summary>
    ///     Test that an unclosed fence is rejected when formatting.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_UnclosedFenceBlock_ThrowsArgumentException()
    {
        // Arrange: an unclosed fence
        var document = BlockFormatterAssertions.Parse("```\ncode\n");
        var block = BlockFormatterAssertions.FirstBlock(document, BlockKind.UnclosedFence);
        var context = new FormatContext(document, LintMarkConfig.Default);

        // Act & Assert: the block is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentException>(() => Formatter.Format(context, block, new EditSet()));
        Assert.Equal("block", exception.ParamName);
    }

    /// <summary>
    ///     Test that a paragraph is rejected when formatting.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_ParagraphBlock_ThrowsArgumentException()
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
    ///     Test that the formatter lists the three rules it enforces and that the list cannot be changed.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_RuleIds_Read_ListsTheCodeRulesAndIsImmutable()
    {
        // Act: read the rule ids
        var ruleIds = Formatter.RuleIds;

        // Assert: the code rules and trailing whitespace, each registered, in an immutable array
        Assert.Equal(["code-fence", "code-block-style", "trailing-whitespace"], ruleIds);
        Assert.All(ruleIds, id => Assert.True(RuleCatalog.TryGet(id, out _)));
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<string>)ruleIds);
    }

    /// <summary>
    ///     Test that a tilde fence becomes a backtick fence of the same length with its info string kept.
    /// </summary>
    [Theory]
    [InlineData("~~~\ncode\n~~~\n", "```\ncode\n```\n")]
    [InlineData("~~~js\ncode\n~~~\n", "```js\ncode\n```\n")]
    [InlineData("~~~~\ncode\n~~~~\n", "````\ncode\n````\n")]
    [InlineData("~~~\ncode\n~~~~~\n", "```\ncode\n`````\n")]
    public void CodeBlockFormatter_Format_TildeFence_BecomesBacktickFence(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a fence whose info string starts with a brace (Pandoc attributes or a raw block) keeps its fence character.
    /// </summary>
    [Theory]
    [InlineData("~~~ {.python #id}\nx = 1\n~~~\n")]
    [InlineData("~~~{=html}\n<b>x</b>\n~~~\n")]
    public void CodeBlockFormatter_Format_BracedInfoString_KeepsTheFenceCharacter(string input) =>
        AssertFormats(input, input);

    /// <summary>
    ///     Test that a backtick fence with a braced info string keeps its backticks under the tilde configuration.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_BracedBacktickFenceWithTildeConfiguration_KeepsTheBackticks() =>
        AssertFormats("```{=html}\n<b>x</b>\n```\n", "```{=html}\n<b>x</b>\n```\n", TildeConfig);

    /// <summary>
    ///     Test that a fence that already uses the configured character is left alone.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n```\n")]
    [InlineData("````js\ncode\n````\n")]
    public void CodeBlockFormatter_Format_FenceWithConfiguredCharacter_ProposesNothing(string input)
    {
        // Act: format the block
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: nothing changes
        Assert.Empty(edits);
        Assert.Equal(input, formatted);
    }

    /// <summary>
    ///     Test that the new fence is longer than any run of its character in the content, so that the content cannot close it.
    /// </summary>
    [Theory]
    [InlineData("~~~\n```\ninner\n```\n~~~\n", "````\n```\ninner\n```\n````\n")]
    [InlineData("~~~\n````\ninner\n````\n~~~\n", "`````\n````\ninner\n````\n`````\n")]
    [InlineData("~~~\n``\nshort\n``\n~~~\n", "```\n``\nshort\n``\n```\n")]
    public void CodeBlockFormatter_Format_ContentWithFenceLookalike_LengthensTheFence(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a tilde fence whose info string holds a backtick is not converted to backticks.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_InfoStringWithBacktick_KeepsTheTildeFence() =>
        AssertFormats("~~~ a`b\ncode\n~~~\n", "~~~ a`b\ncode\n~~~\n");

    /// <summary>
    ///     Test that trailing whitespace on the two fence lines is removed and the content lines are kept as written.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_FenceLinesWithTrailingWhitespace_RemovesItOnlyThere() =>
        AssertFormats("```js   \n  code  \n\ttab\t\n```   \n", "```js\n  code  \n\ttab\t\n```\n");

    /// <summary>
    ///     Test that a fence inside a list item or a block quote is converted in place.
    /// </summary>
    [Theory]
    [InlineData("- ~~~\n  code\n  ~~~\n", "- ```\n  code\n  ```\n")]
    [InlineData("> ~~~\n> code\n> ~~~\n", "> ```\n> code\n> ```\n")]
    [InlineData("1. item\n\n   ~~~ js\n   code\n   ~~~\n", "1. item\n\n   ``` js\n   code\n   ```\n")]
    [InlineData("> - ~~~\n>   code\n>   ~~~\n", "> - ```\n>   code\n>   ```\n")]
    public void CodeBlockFormatter_Format_FenceInContainer_ConvertsInPlace(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that a backtick fence becomes a tilde fence when the configuration asks for tildes.
    /// </summary>
    [Theory]
    [InlineData("```\ncode\n```\n", "~~~\ncode\n~~~\n")]
    [InlineData("```js\ncode\n```\n", "~~~js\ncode\n~~~\n")]
    [InlineData("```\n~~~\ncode\n~~~\n```\n", "~~~~\n~~~\ncode\n~~~\n~~~~\n")]
    [InlineData("~~~\ncode\n~~~\n", "~~~\ncode\n~~~\n")]
    public void CodeBlockFormatter_Format_TildeConfiguration_WritesTildeFences(string input, string expected) =>
        AssertFormats(input, expected, TildeConfig);

    /// <summary>
    ///     Test that an indented code block becomes a fenced code block with its content shifted left by four columns.
    /// </summary>
    [Theory]
    [InlineData("    code\n", "```\ncode\n```\n")]
    [InlineData("\tcode\n", "```\ncode\n```\n")]
    [InlineData("    one\n    two\n", "```\none\ntwo\n```\n")]
    [InlineData("    a\n\n    b\n", "```\na\n\nb\n```\n")]
    [InlineData("    a\n      \n    b\n", "```\na\n  \nb\n```\n")]
    [InlineData("        nested\n", "```\n    nested\n```\n")]
    [InlineData("  \tcode\n", "```\ncode\n```\n")]
    [InlineData("    code  \n", "```\ncode  \n```\n")]
    [InlineData("# H\n\n    code\n", "# H\n\n```\ncode\n```\n")]
    public void CodeBlockFormatter_Format_IndentedCode_BecomesFencedCode(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that the blank lines after an indented code block are not part of the replacement.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedCodeFollowedByBlankLines_LeavesThemOutside() =>
        AssertFormats("    code\n\n\ntext\n", "```\ncode\n```\n\n\ntext\n");

    /// <summary>
    ///     Test that the fence around an indented block is longer than any fence-like line in its content.
    /// </summary>
    [Theory]
    [InlineData("    ```\n    x\n", "````\n```\nx\n````\n")]
    [InlineData("    ````\n    x\n", "`````\n````\nx\n`````\n")]
    public void CodeBlockFormatter_Format_IndentedCodeWithFenceLookalike_LengthensTheFence(string input, string expected) =>
        AssertFormats(input, expected);

    /// <summary>
    ///     Test that an indented block uses tildes when the configuration asks for them.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedCodeWithTildeConfiguration_UsesTildes() =>
        AssertFormats("    code\n", "~~~\ncode\n~~~\n", TildeConfig);

    /// <summary>
    ///     Test that an indented code block inside a list item is left as written, because its indentation also carries the container.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedCodeInListItem_StaysIndented() =>
        AssertFormats("- item\n\n      code\n", "- item\n\n      code\n");

    /// <summary>
    ///     Test that an indented block that would put a dashes line under a first line of dashes is left as written,
    ///     because the dashes could then close front matter.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedDashesAfterFirstLineDashes_StaysIndented() =>
        AssertFormats("---\n\n    ---\n", "---\n\n    ---\n");

    /// <summary>
    ///     Test that an indented block with dashes is converted when the document has proper front matter.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedDashesAfterFrontMatter_IsConverted() =>
        AssertFormats("---\ntitle: x\n---\n\n    ---\n", "---\ntitle: x\n---\n\n```\n---\n```\n");

    /// <summary>
    ///     Test that an unclosed fence is not touched.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_UnclosedFence_ProposesNothing()
    {
        // Arrange: an unclosed tilde fence with trailing whitespace
        const string input = "~~~  \ncode  \n";

        // Act: format
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: nothing changes
        Assert.Empty(edits);
        Assert.Equal(input, formatted);
    }

    /// <summary>
    ///     Test that every edit is reported under the rule of the violation it removes.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_NonCanonicalFence_ReportsEachEditUnderItsRule()
    {
        // Arrange: a tilde fence with trailing whitespace on both fence lines
        const string input = "~~~js  \ncode\n~~~  \n";

        // Act: format
        var (edits, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: the fence characters and the whitespace, in order of position
        Assert.Equal("```js\ncode\n```\n", formatted);
        Assert.Equal(["code-fence", "trailing-whitespace", "code-fence", "trailing-whitespace"], edits.Select(edit => edit.RuleId));
    }

    /// <summary>
    ///     Test that an indented block is reported under the code block style rule.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_IndentedCode_ReportsTheCodeBlockStyleRule()
    {
        // Act: format an indented block
        var (edits, _) = BlockFormatterAssertions.ApplyFormatter(Formatter, "    code\n");

        // Assert: one edit, under the code block style rule
        var edit = Assert.Single(edits);
        Assert.Equal("code-block-style", edit.RuleId);
    }

    /// <summary>
    ///     Test that the content of a code block is never changed, whatever it holds.
    /// </summary>
    [Theory]
    [InlineData("~~~\n# not a heading  \n- not a list\n| not | table |\n\t\ttabs\n<div>html</div>\n~~~\n")]
    [InlineData("~~~\n\n\n\nthree blank lines above\n~~~\n")]
    [InlineData("~~~\n---\n~~~\n")]
    public void CodeBlockFormatter_Format_ContentOfFence_IsNeverChanged(string input)
    {
        // Act: format
        var (_, formatted) = BlockFormatterAssertions.ApplyFormatter(Formatter, input);

        // Assert: the content lines between the fences are exactly as written
        var before = input.Split('\n')[1..^2];
        var after = formatted.Split('\n')[1..^2];
        Assert.Equal(before, after);
        Assert.StartsWith("```", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a code block is left as written when a rule of the formatter is suppressed in it, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable code-fence", "~~~\ncode\n~~~\n")]
    [InlineData("markdownlint-disable MD048", "~~~\ncode\n~~~\n")]
    [InlineData("markdownlint-disable MD046", "    code\n")]
    [InlineData("markdownlint-disable MD009", "```  \ncode\n```\n")]
    [InlineData("lintmark-disable", "~~~\ncode\n~~~\n")]
    public void CodeBlockFormatter_Format_RuleSuppressed_LeavesTheBlock(string directive, string block)
    {
        // Arrange: a code block inside a region that disables the rule
        var text = $"<!-- {directive} -->\n\n{block}";

        // Act: format with the code block formatter registered
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([Formatter]), text);

        // Assert: nothing changes and nothing is reported
        Assert.Equal(text, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that CRLF line endings and a byte order mark survive formatting a code block.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_CrlfAndBom_KeepsLineEndingsAndMark() =>
        BlockFormatterAssertions.AssertFormatsOnDisk("~~~\ncode\n~~~\n\n    indented\n", "```\ncode\n```\n\n```\nindented\n```\n");

    /// <summary>
    ///     Test that formatting is deterministic when the same formatter instance serves several threads.
    /// </summary>
    [Fact]
    public void CodeBlockFormatter_Format_SharedInstanceOnManyThreads_GivesTheSameResult()
    {
        // Arrange: tilde fences and an indented block
        const string input = "~~~\none\n~~~\n\ntext\n\n    two\n";

        // Act: format on several threads with one shared instance
        var results = Enumerable.Range(0, 8).AsParallel().Select(_ => BlockFormatterAssertions.ApplyFormatter(Formatter, input).Formatted).ToList();

        // Assert: every thread computed the same text
        Assert.All(results, result => Assert.Equal("```\none\n```\n\ntext\n\n```\ntwo\n```\n", result));
    }
}
