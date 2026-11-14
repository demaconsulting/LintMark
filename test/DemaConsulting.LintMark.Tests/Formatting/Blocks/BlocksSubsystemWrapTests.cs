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
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the Blocks subsystem wraps the paragraphs that are wider than the line length inside the production formatter,
///     keeps everything else as written and reports what it cannot wrap.
/// </summary>
public class BlocksSubsystemWrapTests
{
    /// <summary>
    ///     Creates a configuration with a line length of twenty columns.
    /// </summary>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Narrow() => LintMarkConfig.Default with { LineLength = 20 };

    /// <summary>
    ///     Formats a text with the production formatter at twenty columns, checking that the rewrite is safe and stable.
    /// </summary>
    /// <param name="input">The LF-normalized input.</param>
    /// <param name="expected">The expected output.</param>
    private static void AssertWraps(string input, string expected) =>
        BlockFormatterAssertions.AssertFormats(input, expected, Narrow());

    /// <summary>
    ///     Test that a paragraph with a line wider than the line length is wrapped by the paragraph formatter of the production formatter.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_OverflowingParagraph_IsWrapped() =>
        AssertWraps("ant bat cat dog eel fox gnu\n", "ant bat cat dog eel\nfox gnu\n");

    /// <summary>
    ///     Test that a paragraph in which no line is wider than the line length is kept exactly as written, ragged lines included.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_ParagraphWithinTheWidth_IsKeptExactly() =>
        AssertWraps("a\nbat cat\nd e f\n", "a\nbat cat\nd e f\n");

    /// <summary>
    ///     Test that a hard line break stays where it is.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_HardBreaks_StayWhereTheyAre() =>
        AssertWraps("ant bat cat dog eel fox gnu  \nhen ink\n", "ant bat cat dog eel\nfox gnu  \nhen ink\n");

    /// <summary>
    ///     Test that the continuation lines of a list item are written with the indentation of its content.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_ListItem_KeepsTheIndentationOfItsContent() =>
        AssertWraps("1. ant bat cat dog eel fox gnu\n   hen\n2. x\n", "1. ant bat cat dog\n   eel fox gnu hen\n2. x\n");

    /// <summary>
    ///     Test that the continuation lines of a quote are written with the quote markers.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_Quote_KeepsItsMarkers() =>
        AssertWraps("> > ant bat cat dog eel fox\n", "> > ant bat cat dog\n> > eel fox\n");

    /// <summary>
    ///     Test that a lazy continuation line of a quote gets the quote marker when the paragraph is wrapped.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_LazyQuoteLine_GetsTheQuoteMarker() =>
        AssertWraps("> ant bat cat dog\neel fox gnu hen ink jam\n", "> ant bat cat dog\n> eel fox gnu hen\n> ink jam\n");

    /// <summary>
    ///     Test that a lazy continuation line of a list item keeps the paragraph as written, and the overflow is reported as one that cannot be fixed.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_LazyListLine_KeepsTheParagraphAndReportsIt()
    {
        // Arrange: a list item with a lazy line that is too wide
        const string input = "- ant bat cat dog\neel fox gnu hen ink jam\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Narrow());

        // Assert: unchanged, one lint-only finding
        Assert.Equal(input, formatted);
        Assert.Equal(DiagnosticKind.LintOnly, Assert.Single(result.Diagnostics).Kind);
    }

    /// <summary>
    ///     Test that a paragraph with a Pandoc structural line is kept as written, and nothing is reported.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_PandocParagraph_IsKeptAsWritten() =>
        AssertWraps("ant bat cat dog eel fox gnu\n\\section\nhen\n", "ant bat cat dog eel fox gnu\n\\section\nhen\n");

    /// <summary>
    ///     Test that a paragraph inside a footnote definition is kept as written, and nothing is reported.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_FootnoteParagraph_IsKeptAsWritten()
    {
        // Arrange: a footnote with a long paragraph
        const string input = "text[^1]\n\n[^1]: ant bat cat dog eel fox gnu hen\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Narrow());

        // Assert: nothing happens
        Assert.Equal(input, formatted);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a paragraph is kept as written when a rule that edits its text is suppressed in it.
    /// </summary>
    [Theory]
    [InlineData("line-length")]
    [InlineData("emphasis")]
    [InlineData("code-span-space")]
    [InlineData("list-indent")]
    public void BlocksSubsystem_Format_SuppressedRule_KeepsTheParagraphAsWritten(string rule)
    {
        // Arrange: a paragraph that is too wide, in a region where one rule is disabled
        var input = $"<!-- lintmark-disable {rule} -->\n\nant bat cat dog eel fox gnu\n";

        // Act: format
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Narrow());

        // Assert: nothing happens
        Assert.Equal(input, formatted);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a line that cannot be shortened because of block syntax at the wrap point is reported as a problem that rewriting cannot fix.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_NoGoodBreak_ReportsTheOverflowThatCannotBeFixed()
    {
        // Act: format where the dash cannot start a line and nothing before it can move
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), "ant - bat\n", LintMarkConfig.Default with { LineLength = 4 });

        // Assert: wrapped as far as possible, and a finding of each kind
        Assert.Equal("ant -\nbat\n", formatted);
        Assert.Contains(result.Diagnostics, finding => finding.Kind == DiagnosticKind.LintOnly && finding.RuleId == "line-length");
    }

    /// <summary>
    ///     Test that code, HTML, tables and headings that are wider than the line length are kept as written.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_LiteralBlocks_AreKeptAsWritten()
    {
        // Arrange: literal blocks with long lines
        const string input =
            "# a heading with many words in it that is long\n\n"
            + "```text\ncode line with many words in it that is long\n```\n\n"
            + "<div>html block with many words in it that is long</div>\n\n"
            + "| column one is long | column two is long |\n| ------------------ | ------------------ |\n";

        // Act & Assert: nothing changes, nothing is reported
        var (result, formatted) = BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), input, Narrow());
        Assert.Equal(input, formatted);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that wrapping gives the same text on every thread.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Format_WrapOnManyThreads_GivesTheSameText()
    {
        // Arrange: a document with a list, a quote and a paragraph that are too wide
        const string input = "- ant bat cat dog eel fox gnu hen\n\n> jam key log map nut owl pig quo\n\nsun toy urn van web fix yak zoo\n";
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var expected = BlockFormatterAssertions.Apply(formatter, input, Narrow()).Formatted;

        // Act & Assert: every thread produces the same text
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 50; round++)
                {
                    Assert.Equal(expected, BlockFormatterAssertions.Apply(formatter, input, Narrow()).Formatted);
                }
            });
    }
}
