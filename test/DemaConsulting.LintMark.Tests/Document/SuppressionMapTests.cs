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

using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the SuppressionMap class.
/// </summary>
/// <remarks>
///     The map is built from the parsed document's blocks and syntax tree, so the tests obtain
///     that input through <see cref="DocumentParser.Parse"/>, which is the production builder of the map.
/// </remarks>
public class SuppressionMapTests
{
    /// <summary>
    ///     Parses text as a document named <c>t.md</c>.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The parsed document.</returns>
    private static ParsedDocument Parse(string text) => DocumentParser.Parse(SourceDocument.FromText("t.md", text));

    /// <summary>
    ///     Determines whether a rule is suppressed at the first occurrence of a marker word.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="marker">A word that occurs in the text.</param>
    /// <param name="rule">The rule identifier.</param>
    /// <returns><see langword="true"/> when the rule is suppressed at the marker.</returns>
    private static bool At(ParsedDocument document, string marker, string rule)
    {
        var offset = document.Source.Text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"marker {marker} not found");
        return document.Suppressions.IsSuppressed(offset, rule);
    }

    /// <summary>
    ///     Test that a document without directives suppresses nothing.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_NoDirectives_SuppressesNothing()
    {
        // Arrange: a document with an ordinary comment only
        var document = Parse("Text TEXT.\n\n<!-- just a comment -->\n");

        // Act / Assert: nothing is suppressed
        Assert.Empty(document.Suppressions.Ranges);
        Assert.False(At(document, "TEXT", "line-length"));
    }

    /// <summary>
    ///     Test that the exposed ranges and their rule sets cannot be changed through a cast.
    /// </summary>
    [Fact]
    public void SuppressionMap_Ranges_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a document with one region that disables two listed rules
        var document = Parse("<!-- lintmark-disable line-length no-bare-urls -->\n\nINSIDE\n\n<!-- lintmark-enable -->\n");

        // Act: take the ranges and the rule set of the first one
        var ranges = document.Suppressions.Ranges;

        // Assert: neither the list of ranges nor the list of rules can be modified by a caller
        ReadOnlyAssertions.AssertCannotBeModified(ranges);
        ReadOnlyAssertions.AssertCannotBeModified(ranges[0].Rules.Rules);
        Assert.True(At(document, "INSIDE", "line-length"));
    }

    /// <summary>
    ///     Test that disable and enable form a region covering the text between them.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_DisableEnableRegion_CoversTextBetween()
    {
        // Arrange: a region around INSIDE
        var document = Parse("BEFORE\n\n<!-- lintmark-disable -->\n\nINSIDE\n\n<!-- lintmark-enable -->\n\nAFTER\n");

        // Act / Assert: only the text inside the region is suppressed, for every rule
        Assert.False(At(document, "BEFORE", "line-length"));
        Assert.True(At(document, "INSIDE", "line-length"));
        Assert.True(At(document, "INSIDE", "anything-else"));
        Assert.False(At(document, "AFTER", "line-length"));
    }

    /// <summary>
    ///     Test that an unterminated disable runs to the end of the file.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_UnterminatedDisable_RunsToEndOfFile()
    {
        // Arrange: a disable without enable
        var document = Parse("BEFORE\n\n<!-- lintmark-disable -->\n\nINSIDE\n\nEND\n");

        // Act / Assert: everything after the comment is suppressed
        Assert.False(At(document, "BEFORE", "r"));
        Assert.True(At(document, "INSIDE", "r"));
        Assert.True(At(document, "END", "r"));
        Assert.Equal(int.MaxValue, document.Suppressions.Ranges.Single().End);
    }

    /// <summary>
    ///     Test that a disable with rules suppresses only those rules, case-insensitively.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_DisableWithRules_SuppressesOnlyListedRules()
    {
        // Arrange: two rules disabled by a markdownlint-family comment
        var document = Parse("<!-- markdownlint-disable MD013 Line-Length -->\n\nINSIDE\n\n<!-- markdownlint-enable -->\n");

        // Act / Assert: listed rules match ignoring case, others are not suppressed
        Assert.True(At(document, "INSIDE", "MD013"));
        Assert.True(At(document, "INSIDE", "md013"));
        Assert.True(At(document, "INSIDE", "line-length"));
        Assert.False(At(document, "INSIDE", "MD033"));
    }

    /// <summary>
    ///     Test that enable with rules re-enables only those rules while the rest stay disabled.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_EnableWithRules_ReEnablesOnlyListedRules()
    {
        // Arrange: everything disabled, then one rule enabled, then another disable
        var document = Parse(
            "<!-- lintmark-disable -->\n\nONE\n\n<!-- lintmark-enable rule-a -->\n\nTWO\n\n<!-- lintmark-disable rule-a -->\n\nTHREE\n");

        // Act / Assert: rule-a is active only in the middle region
        Assert.True(At(document, "ONE", "rule-a"));
        Assert.False(At(document, "TWO", "rule-a"));
        Assert.True(At(document, "TWO", "rule-b"));
        Assert.True(At(document, "THREE", "rule-a"));
    }

    /// <summary>
    ///     Test that nested disables of different rules are tracked independently.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_OverlappingRuleRegions_TrackedIndependently()
    {
        // Arrange: disable a, disable b, enable a
        var document = Parse(
            "<!-- lintmark-disable a -->\n\nONE\n\n<!-- lintmark-disable b -->\n\nTWO\n\n<!-- lintmark-enable a -->\n\nTHREE\n");

        // Act / Assert: a covers ONE and TWO; b covers TWO and THREE
        Assert.True(At(document, "ONE", "a"));
        Assert.False(At(document, "ONE", "b"));
        Assert.True(At(document, "TWO", "a"));
        Assert.True(At(document, "TWO", "b"));
        Assert.False(At(document, "THREE", "a"));
        Assert.True(At(document, "THREE", "b"));
    }

    /// <summary>
    ///     Test that disable-line covers the line holding the comment only.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_DisableLine_CoversOwnLineOnly()
    {
        // Arrange: an inline comment with a trailing marker, then another line
        var document = Parse("Text <!-- lintmark-disable-line rule-a --> SAME\nNEXT\n");

        // Act / Assert: the same line is suppressed for the rule, the next line is not
        Assert.True(At(document, "SAME", "rule-a"));
        Assert.False(At(document, "SAME", "rule-b"));
        Assert.False(At(document, "NEXT", "rule-a"));
    }

    /// <summary>
    ///     Test that disable-next-line covers the following line only.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_DisableNextLine_CoversFollowingLineOnly()
    {
        // Arrange: a block comment directly above a paragraph line (no blank line)
        var document = Parse("<!-- lintmark-disable-next-line -->\nTARGET line\nOTHER line\n");

        // Act / Assert: only the next line is suppressed
        Assert.True(At(document, "TARGET", "r"));
        Assert.False(At(document, "OTHER", "r"));
    }

    /// <summary>
    ///     Test that disable-next-line on the last line produces no range.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_DisableNextLineOnLastLine_ProducesNoRange()
    {
        // Arrange / Act: the directive has no following line
        var document = Parse("Text\n\n<!-- lintmark-disable-next-line -->\n");

        // Assert: nothing to suppress
        Assert.Empty(document.Suppressions.Ranges);
    }

    /// <summary>
    ///     Test that disable-file covers the whole file, including text before the comment.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_DisableFile_CoversWholeFile()
    {
        // Arrange: a file-level directive in the middle
        var document = Parse("BEFORE\n\n<!-- markdownlint-disable-file rule-a -->\n\nAFTER\n");

        // Act / Assert: both ends are suppressed for the listed rule only
        Assert.True(At(document, "BEFORE", "rule-a"));
        Assert.True(At(document, "AFTER", "rule-a"));
        Assert.False(At(document, "AFTER", "rule-b"));
    }

    /// <summary>
    ///     Test that capture and restore bring back the earlier state.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_CaptureRestore_ReturnsToCapturedState()
    {
        // Arrange: capture, disable, restore
        var document = Parse(
            "<!-- lintmark-capture -->\n\n<!-- lintmark-disable -->\n\nINSIDE\n\n<!-- lintmark-restore -->\n\nAFTER\n");

        // Act / Assert: suppressed inside, restored afterwards
        Assert.True(At(document, "INSIDE", "r"));
        Assert.False(At(document, "AFTER", "r"));
    }

    /// <summary>
    ///     Test that restore without a capture is ignored.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_RestoreWithoutCapture_IsIgnored()
    {
        // Arrange: a lone restore after a disable
        var document = Parse("<!-- lintmark-disable -->\n\n<!-- lintmark-restore -->\n\nINSIDE\n");

        // Act / Assert: the disable stays in force
        Assert.True(At(document, "INSIDE", "r"));
    }

    /// <summary>
    ///     Test that prettier-ignore covers the next block (a table), not just the next line.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_PrettierIgnore_CoversNextBlock()
    {
        // Arrange: prettier-ignore above a multi-line table followed by a paragraph
        var document = Parse("<!-- prettier-ignore -->\n| a | b |\n|---|---|\n| LAST | 2 |\n\nAFTER\n");

        // Act / Assert: every line of the table is suppressed for every rule; the paragraph is not
        Assert.True(At(document, "| a", "any-rule"));
        Assert.True(At(document, "LAST", "any-rule"));
        Assert.False(At(document, "AFTER", "any-rule"));
    }

    /// <summary>
    ///     Test that prettier-ignore before a list covers the whole list.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_PrettierIgnoreBeforeList_CoversWholeList()
    {
        // Arrange: prettier-ignore, blank line, list, paragraph
        var document = Parse("<!-- prettier-ignore -->\n\n- ONE\n- TWO\n\nAFTER\n");

        // Act / Assert: the list is covered, the paragraph is not
        Assert.True(At(document, "ONE", "r"));
        Assert.True(At(document, "TWO", "r"));
        Assert.False(At(document, "AFTER", "r"));
    }

    /// <summary>
    ///     Test that prettier-ignore-start and -end form a region for all rules.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_PrettierIgnoreStartEnd_CoversRegion()
    {
        // Arrange: a prettier region
        var document = Parse("BEFORE\n\n<!-- prettier-ignore-start -->\n\nINSIDE\n\n<!-- prettier-ignore-end -->\n\nAFTER\n");

        // Act / Assert: only the region is suppressed
        Assert.False(At(document, "BEFORE", "r"));
        Assert.True(At(document, "INSIDE", "r"));
        Assert.False(At(document, "AFTER", "r"));
    }

    /// <summary>
    ///     Test that an unterminated prettier-ignore-start runs to the end of the file.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_UnterminatedPrettierStart_RunsToEndOfFile()
    {
        // Arrange: a start without an end
        var document = Parse("<!-- prettier-ignore-start -->\n\nINSIDE\n");

        // Act / Assert: the rest of the file is suppressed
        Assert.True(At(document, "INSIDE", "r"));
    }

    /// <summary>
    ///     Test that an unknown or malformed directive is ignored.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_UnknownDirectives_AreIgnored()
    {
        // Arrange: comments that look similar but are not directives
        var document = Parse(
            "<!-- lintmark-foo -->\n\n<!-- markdownlint-disable-foo -->\n\n<!-- lintmark-disable2 -->\n\n<!-- prettier-ignore-foo -->\n\nTEXT\n");

        // Act / Assert: nothing is suppressed
        Assert.Empty(document.Suppressions.Ranges);
        Assert.False(At(document, "TEXT", "r"));
    }

    /// <summary>
    ///     Test that directives inside code spans, code blocks and front matter are not directives.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_CommentsInCodeAndFrontMatter_AreIgnored()
    {
        // Arrange: comment text inside front matter, a fenced block, an indented block and a code span
        var document = Parse(
            "---\nnote: <!-- lintmark-disable-file -->\n---\n\n```\n<!-- lintmark-disable-file -->\n```\n\n"
            + "    <!-- lintmark-disable-file -->\n\nUse `<!-- lintmark-disable-file -->` here TEXT.\n");

        // Act / Assert: no ranges exist
        Assert.Empty(document.Suppressions.Ranges);
        Assert.False(At(document, "TEXT", "r"));
    }

    /// <summary>
    ///     Test that an inline comment inside a paragraph is a directive for the lintmark family.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressed_InlineComment_IsDirective()
    {
        // Arrange: an inline disable-next-line within a paragraph
        var document = Parse("Text <!-- lintmark-disable-next-line --> end\nNEXT\n");

        // Act / Assert: the second line of the paragraph is suppressed
        Assert.True(At(document, "NEXT", "r"));
    }

    /// <summary>
    ///     Test that prettier directives are only recognized in block-level comments.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_InlinePrettierComment_IsIgnored()
    {
        // Arrange: prettier-ignore as an inline comment
        var document = Parse("Text <!-- prettier-ignore --> end\n\nAFTER\n");

        // Act / Assert: nothing is suppressed
        Assert.Empty(document.Suppressions.Ranges);
    }

    /// <summary>
    ///     Test that a rule identifier alias resolver resolves aliases on both sides of the comparison.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_AliasResolver_ResolvesAliases()
    {
        // Arrange: parse, then rebuild the map with an alias resolver (MD013 is the alias of line-length)
        var document = Parse("<!-- markdownlint-disable MD013 -->\n\nINSIDE\n");
        string Canonicalize(string id) => string.Equals(id, "MD013", StringComparison.OrdinalIgnoreCase) ? "line-length" : id;

        // Act: build with the alias resolver
        var map = SuppressionMap.Build(document.Source, document.Blocks, document.Ast, Canonicalize);
        var offset = document.Source.Text.IndexOf("INSIDE", StringComparison.Ordinal);

        // Assert: the primary id and the alias both match; an unrelated rule does not
        Assert.True(map.IsSuppressed(offset, "line-length"));
        Assert.True(map.IsSuppressed(offset, "MD013"));
        Assert.False(map.IsSuppressed(offset, "no-trailing-spaces"));
    }

    /// <summary>
    ///     Test that a block intersecting a suppressed region is reported as suppressed by the range query.
    /// </summary>
    [Fact]
    public void SuppressionMap_IsSuppressedRange_BlockIntersectingRegion_IsSuppressed()
    {
        // Arrange: a disable-next-line covering the first line of a two-line paragraph
        var document = Parse("<!-- lintmark-disable-next-line -->\nFIRST\nSECOND\n");
        var paragraph = document.Blocks.Single(b => b.Kind == BlockKind.Paragraph);

        // Act: query the whole paragraph and a block-free range after it
        var intersects = document.Suppressions.IsSuppressedRange(paragraph.Start, paragraph.EndInclusive + 1, "r");
        var after = document.Suppressions.IsSuppressedRange(document.Source.Text.Length, document.Source.Text.Length + 1, "r");

        // Assert: the paragraph intersects the suppressed first line; the end of the file does not
        Assert.True(intersects);
        Assert.False(after);
    }

    /// <summary>
    ///     Test that the ranges are sorted by start offset.
    /// </summary>
    [Fact]
    public void SuppressionMap_Ranges_AreSortedByStart()
    {
        // Arrange: directives of several kinds in an order that produces ranges out of source order
        var document = Parse(
            "<!-- lintmark-disable-file x -->\n\n<!-- lintmark-disable -->\n\nA\n\n<!-- lintmark-enable -->\n\n"
            + "<!-- lintmark-disable-next-line -->\nB\n");

        // Act: read the starts
        var starts = document.Suppressions.Ranges.Select(r => r.Start).ToList();

        // Assert: sorted
        Assert.Equal(starts.Order().ToList(), starts);
        Assert.True(starts.Count >= 3);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void SuppressionMap_Build_NullArguments_Throw()
    {
        // Arrange: a parsed document
        var document = Parse("a\n");

        // Act / Assert: nulls are rejected
        Assert.Throws<ArgumentNullException>(() => SuppressionMap.Build(null!, document.Blocks, document.Ast));
        Assert.Throws<ArgumentNullException>(() => SuppressionMap.Build(document.Source, null!, document.Ast));
        Assert.Throws<ArgumentNullException>(() => SuppressionMap.Build(document.Source, document.Blocks, null!));
        Assert.Throws<ArgumentNullException>(() => document.Suppressions.IsSuppressed(0, null!));
    }
}
