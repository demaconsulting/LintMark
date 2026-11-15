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

using System.Collections.Immutable;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests of the Inline subsystem: the inline normalizer, the atom tokenizer, the line wrapper and the
///     link refactorer are driven together through the document formatter and the wrapper, with no substitutes.
/// </summary>
public class InlineSubsystemTests
{
    /// <summary>
    ///     Test that emphasis, strong text, a bare URL and a padded code span are normalized, and that the same
    ///     constructs inside code, inside an autolink and inside a link destination are left as written.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Normalization_EmphasisStrongBareUrlAndCodeSpan_AreNormalizedOutsideCode()
    {
        // Arrange, Act and Assert: underscores, a bare URL and a padded code span are normalized, and the result is stable
        BlockFormatterAssertions.AssertFormats(
            "some __strong__ and _em_ text https://example.com/x and ` code ` end\n",
            "some **strong** and *em* text <https://example.com/x> and `code` end\n");

        // Arrange, Act and Assert: emphasis markers in a code span, an autolink and a link destination are not touched
        BlockFormatterAssertions.AssertUnchanged("`__x__` and <https://example.com/a_b_c> and [text](https://example.com/__x__)\n");
    }

    /// <summary>
    ///     Test that wrapping fills lines by display width, keeps a link and a code span whole, and keeps block syntax
    ///     away from the start of a line.
    /// </summary>
    [Fact]
    public void InlineSubsystem_Wrapping_AtomsLayoutAndGuard_KeepConstructsWholeAndAvoidBlockSyntax()
    {
        // Arrange: a limit of 30 columns for a paragraph with a link and a code span, and atoms where "1." would start a line
        var config = LintMarkConfig.Default with { LineLength = 30 };
        var atoms = new[] { "aaa", "bbb", "ccc", "1.", "ddd" }.ToImmutableArray();

        // Act: wrap the atoms to 11 columns, and the wide characters to 20 columns
        var guarded = LineWrapper.Wrap(atoms, 11, 11);
        var wide = LineWrapper.Wrap(["日本語日本語", "日本語", "abc"], 20, 20);

        // Assert: the link and the code span are never split; "1." does not start a line; width counts display columns
        BlockFormatterAssertions.AssertFormats(
            "see [a long link text here](https://example.com/a/very/long/url) and `code span here` ok done now more\n",
            "see\n[a long link text here](https://example.com/a/very/long/url)\nand `code span here` ok done\nnow more\n",
            config);
        Assert.Equal(["aaa bbb", "ccc 1. ddd"], guarded.Lines);
        Assert.Equal(["日本語日本語 日本語", "abc"], wide.Lines);
        Assert.False(guarded.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that an inline link longer than the threshold becomes a reference link with a derived label and one
    ///     definitions block, and that the result is stable.
    /// </summary>
    [Fact]
    public void InlineSubsystem_LinkRefactor_LongInlineLinks_BecomeReferenceLinksWithOneDefinitionsBlock()
    {
        // Arrange: the conversion enabled with a threshold of 40, and two long links in one paragraph
        var config = LintMarkConfig.Default with { LinkRefactor = LinkRefactorOptions.Disabled with { Enabled = true, Threshold = 40 } };

        // Act and Assert: both links get a label and one definitions block follows the paragraph; a second run changes nothing
        BlockFormatterAssertions.AssertFormats(
            "Read [the documentation page](https://example.com/docs/tool/overview) and [the guide](https://example.com/guide/start/here) now.\n",
            "Read [the documentation page][the-documentation-page] and [the guide][the-guide] now.\n\n"
            + "<!-- Link References -->\n[the-documentation-page]: https://example.com/docs/tool/overview\n[the-guide]: https://example.com/guide/start/here\n",
            config);
    }
}
