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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the Inline subsystem keeps links, code spans and the other inline constructs whole when a paragraph is
///     wrapped, keeps block syntax away from the start of a line, keeps hard breaks, and removes the padding of code spans.
/// </summary>
public class InlineSubsystemWrapTests
{
    /// <summary>
    ///     Creates a configuration with a line length of twenty columns.
    /// </summary>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Narrow() => LintMarkConfig.Default with { LineLength = 20 };

    /// <summary>
    ///     Test that a link, a code span and inline HTML with blanks inside are kept whole when the paragraph is wrapped.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_WrappedParagraph_KeepsInlineConstructsWhole()
    {
        // Arrange: a paragraph in which each construct has blanks inside it and is wider than half the line
        const string input = "ant [bb cc dd](ee) fox `gg hh ii` jam <span a=\"b c\">kk</span> log\n";

        // Act: wrap
        var formatted = BlockFormatterAssertions.AssertSafeAndStable(input, Narrow());

        // Assert: each construct is on one line
        Assert.Contains("[bb cc dd](ee)", formatted, StringComparison.Ordinal);
        Assert.Contains("`gg hh ii`", formatted, StringComparison.Ordinal);
        Assert.Contains("<span a=\"b c\">kk</span>", formatted, StringComparison.Ordinal);
        Assert.NotEqual(input, formatted);
    }

    /// <summary>
    ///     Test that a link whose text continues on the next line is kept whole and its line break becomes a space.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_LinkTextOverTwoLines_IsJoinedAndKeptWhole() =>
        BlockFormatterAssertions.AssertFormats(
            "ant bat cat dog eel [fox\ngnu](hen) ink jam\n",
            "ant bat cat dog eel\n[fox gnu](hen) ink\njam\n",
            Narrow());

    /// <summary>
    ///     Test that no wrapped line starts with a list marker, a number marker or a heading marker.
    /// </summary>
    [Theory]
    [InlineData("ant bat cat dog eel - fox gnu\n", "ant bat cat dog\neel - fox gnu\n")]
    [InlineData("ant bat cat dog eel 2026. fox gnu\n", "ant bat cat dog\neel 2026. fox gnu\n")]
    [InlineData("ant bat cat dog eel # fox gnu\n", "ant bat cat dog\neel # fox gnu\n")]
    public void InlineSubsystem_Format_WrappedParagraph_NeverStartsABlockSyntaxLine(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected, Narrow());

    /// <summary>
    ///     Test that a hard line break written with a backslash stays at its line end, and a path that ends with a backslash is not broken after it.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_WrappedParagraph_KeepsHardBreaksAndNeverBreaksAfterABackslash()
    {
        // Act & Assert: the backslash form stays
        BlockFormatterAssertions.AssertFormats("ant bat cat dog eel fox gnu\\\nhen ink\n", "ant bat cat dog eel\nfox gnu\\\nhen ink\n", Narrow());

        // Act & Assert: the word that ends in a backslash moves together with the word after it
        var formatted = BlockFormatterAssertions.AssertSafeAndStable("ant bat cat dog C:\\temp\\ fox gnu\n", Narrow());
        Assert.DoesNotContain("\\\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that one space on each side of a code span is removed.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_PaddedCodeSpan_LosesItsPadding() =>
        BlockFormatterAssertions.AssertFormats("a ` x ` b\n", "a `x` b\n");

    /// <summary>
    ///     Test that the padding of a code span whose content starts or ends with a backtick is kept.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_CodeSpanWithBacktickAtAnEdge_KeepsItsPadding() =>
        BlockFormatterAssertions.AssertFormats("a `` `x `` b\n", "a `` `x `` b\n");

    /// <summary>
    ///     Test that a code span with padding on one side only or with more than one space is kept.
    /// </summary>
    [Theory]
    [InlineData("a ` x` b\n")]
    [InlineData("a `  x  ` b\n")]
    public void InlineSubsystem_Format_AsymmetricOrDoublePadding_IsKept(string input) =>
        BlockFormatterAssertions.AssertFormats(input, input);

    /// <summary>
    ///     Test that a code span that holds only spaces is kept.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_CodeSpanOfSpaces_IsKept() =>
        BlockFormatterAssertions.AssertFormats("a `   ` b\n", "a `   ` b\n");

    /// <summary>
    ///     Test that the padding of a code span is kept in a paragraph where the rule is suppressed, by identifier or by alias.
    /// </summary>
    [Theory]
    [InlineData("lintmark-disable code-span-space")]
    [InlineData("markdownlint-disable MD038")]
    public void InlineSubsystem_Format_CodeSpanRuleSuppressed_KeepsThePadding(string directive)
    {
        // Arrange: a padded code span in a region where the rule is disabled
        var input = $"<!-- {directive} -->\n\na ` x ` b\n";

        // Act & Assert: the paragraph is kept
        BlockFormatterAssertions.AssertFormats(input, input);
    }

    /// <summary>
    ///     Test that the padding of a code span is removed in the paragraphs of list items and quotes.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Format_PaddedCodeSpanInListAndQuote_LosesItsPadding()
    {
        // Act & Assert: a list item, a nested item and a quote
        BlockFormatterAssertions.AssertFormats("- a ` x ` b\n  - c ` y ` d\n", "- a `x` b\n  - c `y` d\n");
        BlockFormatterAssertions.AssertFormats("> a ` x ` b\n", "> a `x` b\n");
    }
}
