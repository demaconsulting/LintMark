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
///     Subsystem tests of the Blocks subsystem: the block formatters (headings, code blocks, thematic breaks, lists,
///     block quotes, tables and paragraphs) are driven together through the document formatter, with no substitutes.
/// </summary>
public class BlocksSubsystemTests
{
    /// <summary>
    ///     Test that headings, code blocks and thematic breaks are written in their canonical form.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_LeafBlocks_HeadingCodeAndThematicBreak_AreWrittenCanonically()
    {
        // Arrange, Act and Assert: a setext heading, an indented code block and a break of stars are formatted and stable
        BlockFormatterAssertions.AssertFormats(
            "Title\n=====\n\n    indented\n\n***\n",
            "# Title\n\n```\nindented\n```\n\n---\n");
    }

    /// <summary>
    ///     Test that lists and block quotes are written in their canonical form.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Containers_ListAndBlockQuote_AreWrittenCanonically()
    {
        // Arrange, Act and Assert: star bullets with a deep nested item, a repeated ordered number and a wide quote marker are
        // formatted and stable
        BlockFormatterAssertions.AssertFormats(
            "* a\n* b\n    * c\n\n3) x\n3) y\n\n>  quote\n",
            "- a\n- b\n  - c\n\n3) x\n4) y\n\n> quote\n");
    }

    /// <summary>
    ///     Test that a table is written compact by default and aligned by configuration.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Tables_PipeTable_IsWrittenCompactOrAligned()
    {
        // Arrange: a table without pipes at its ends
        const string table = "a|b\n-|-\nlonger cell|x\n";

        // Act and Assert: the default layout is compact, the configured layout pads the cells, and both are stable
        BlockFormatterAssertions.AssertFormats(table, "| a | b |\n| --- | --- |\n| longer cell | x |\n");
        BlockFormatterAssertions.AssertFormats(
            table,
            "| a           | b   |\n| ----------- | --- |\n| longer cell | x   |\n",
            LintMarkConfig.Default with { Tables = TableStyle.Aligned });
    }

    /// <summary>
    ///     Test that a paragraph with a line wider than the limit is filled up to the limit, and a paragraph that fits is
    ///     left exactly as it was written.
    /// </summary>
    [Fact]
    public void BlocksSubsystem_Paragraphs_OverflowOnlyFill_RewrapsOnlyTheParagraphThatOverflows()
    {
        // Arrange: a limit of 20 columns
        var config = LintMarkConfig.Default with { LineLength = 20 };

        // Act and Assert: the paragraph of 27 columns is filled, the ragged paragraph that fits is not touched
        BlockFormatterAssertions.AssertFormats("ant bat cat dog eel fox gnu\n", "ant bat cat dog eel\nfox gnu\n", config);
        BlockFormatterAssertions.AssertUnchanged("a\nbat cat\nd e f\n", config);
    }
}
