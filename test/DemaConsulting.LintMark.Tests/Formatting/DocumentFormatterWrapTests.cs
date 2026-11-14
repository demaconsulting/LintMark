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
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the DocumentFormatter class wraps the paragraphs that are still too wide after every other rule has been
///     applied, composes the wrap with the edits of those rules, and reports the line length findings.
/// </summary>
public class DocumentFormatterWrapTests
{
    /// <summary>
    ///     Creates a configuration with a line length.
    /// </summary>
    /// <param name="width">The line length.</param>
    /// <returns>The configuration.</returns>
    private static LintMarkConfig Width(int width) => LintMarkConfig.Default with { LineLength = width };

    /// <summary>
    ///     Formats a text with the shipped formatters and the given width.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="width">The line length.</param>
    /// <returns>The result and the patched text.</returns>
    private static (FormatResult Result, string Formatted) Format(string text, int width = 20) =>
        BlockFormatterAssertions.Apply(BlockFormatterAssertions.ProductionFormatter(), text, Width(width));

    /// <summary>
    ///     Test that a paragraph that is too wide is wrapped and reported where its first line exceeds the width.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_OverflowingParagraph_IsWrappedAndReportedAtTheFirstColumnBeyondTheWidth()
    {
        // Act: format a paragraph of 27 columns with a limit of 20
        var (result, formatted) = Format("# T\n\nant bat cat dog eel fox gnu\n");

        // Assert: wrapped, and one fixable finding at line 3 column 21
        Assert.Equal("# T\n\nant bat cat dog eel\nfox gnu\n", formatted);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal("line-length", finding.RuleId);
        Assert.Equal(DiagnosticKind.Formatting, finding.Kind);
        Assert.Equal((3, 21), (finding.Line, finding.Column));
        Assert.Equal("Line is 27 columns wide; the limit is 20", finding.Message);
        Assert.All(result.Edits, edit => Assert.Equal("line-length", edit.RuleId));
    }

    /// <summary>
    ///     Test that two paragraphs that are too wide are both wrapped and each gets its own finding.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_TwoOverflowingParagraphs_AreWrappedAndReportedSeparately()
    {
        // Act: format two paragraphs
        var (result, formatted) = Format("ant bat cat dog eel fox\n\ngnu hen ink jam key log\n");

        // Assert: both wrapped, two findings on the lines where each paragraph starts
        Assert.Equal("ant bat cat dog eel\nfox\n\ngnu hen ink jam key\nlog\n", formatted);
        Assert.Equal([1, 3], result.Diagnostics.Select(finding => finding.Line));
    }

    /// <summary>
    ///     Test that paragraphs that fit are left byte for byte as written, while the paragraphs around them are wrapped.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ParagraphsThatFit_AreLeftAsWritten()
    {
        // Act: format a paragraph that fits with short ragged lines between two that do not
        var (_, formatted) = Format("ant bat cat dog eel fox\n\nx\ny z\nw\n\ngnu hen ink jam key log\n");

        // Assert: the middle one is untouched
        Assert.Equal("ant bat cat dog eel\nfox\n\nx\ny z\nw\n\ngnu hen ink jam key\nlog\n", formatted);
    }

    /// <summary>
    ///     Test that the wrap sees the result of the other rules: the delimiters, the padding of a code span and the spacing of a quote.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_OtherRulesChangeTheLines_TheWrapSeesTheirResult()
    {
        // Act: format a quote with extra spaces after the marker and a padded code span, so that the line fits only after the other rules
        var (_, formatted) = Format(">   ant bat cat ` x ` dog\n", 21);

        // Assert: the quote spacing and the padding are fixed and the paragraph then fits, so nothing is wrapped
        Assert.Equal("> ant bat cat `x` dog\n", formatted);
    }

    /// <summary>
    ///     Test that a list that needs to move and has a paragraph that is too wide reaches its final form in one run.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ListThatMovesAndOverflows_ReachesItsFinalFormInOneRun()
    {
        // Arrange: a list with three spaces after the marker, which the list rule reduces to one, and a long item
        const string input = "-   ant bat cat dog eel fox gnu\n    hen ink\n-   x\n";

        // Act: format and format the result again
        var (result, formatted) = Format(input);
        var (second, same) = Format(formatted);

        // Assert: the marker spacing and the wrap come in one run and nothing is left for a second one
        Assert.Equal("- ant bat cat dog\n  eel fox gnu hen\n  ink\n- x\n", formatted);
        Assert.Equal(formatted, same);
        Assert.Empty(second.Edits);
        Assert.Empty(second.Diagnostics);
        Assert.Contains(result.Diagnostics, finding => finding.RuleId == "list-marker-space");
        Assert.Contains(result.Diagnostics, finding => finding.RuleId == "line-length");
    }

    /// <summary>
    ///     Test that a violation of a rule whose edit the wrap absorbs is still reported.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_EditAbsorbedByTheWrap_IsStillReported()
    {
        // Act: format a paragraph in which an underscore emphasis lies in the lines that are rewritten
        var (result, formatted) = Format("ant bat cat dog _eel_ fox gnu\n");

        // Assert: the emphasis is rewritten and wrapped, and both violations are reported
        Assert.Equal("ant bat cat dog\n*eel* fox gnu\n", formatted);
        Assert.Contains(result.Diagnostics, finding => finding.RuleId == "emphasis");
        Assert.Contains(result.Diagnostics, finding => finding.RuleId == "line-length");
        Assert.All(result.Edits, edit => Assert.True(edit.Start >= 0 && edit.End <= "ant bat cat dog _eel_ fox gnu\n".Length));
    }

    /// <summary>
    ///     Test that a line that cannot be shortened is reported as a finding that rewriting cannot fix, in addition to the wrap.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_NoGoodBreak_ReportsAFindingThatCannotBeFixed()
    {
        // Act: format where a lone dash cannot start a line and the first atom is too wide for its line
        var (result, formatted) = Format("ant - bat\n", 4);

        // Assert: wrapped, with a fixable finding and a lint-only finding at the same place
        Assert.Equal("ant -\nbat\n", formatted);
        Assert.Equal(
            [DiagnosticKind.Formatting, DiagnosticKind.LintOnly],
            result.Diagnostics.Where(finding => finding.RuleId == "line-length").Select(finding => finding.Kind));
        Assert.Contains("cannot be wrapped automatically", result.Diagnostics.Single(finding => finding.Kind == DiagnosticKind.LintOnly).Message, StringComparison.Ordinal);

        // Act & Assert: formatting the result again reports only the one that cannot be fixed
        var (second, _) = Format(formatted, 4);
        Assert.Empty(second.Edits);
        Assert.Equal(DiagnosticKind.LintOnly, Assert.Single(second.Diagnostics).Kind);
    }

    /// <summary>
    ///     Test that a paragraph that cannot be rewritten safely is reported as a finding that rewriting cannot fix and is not changed.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_LazyLineInListItem_IsReportedAndNotChanged()
    {
        // Act: format a list item with a lazy continuation line that is too wide
        const string input = "- ant bat cat dog\neel fox gnu hen ink jam\n";
        var (result, formatted) = Format(input);

        // Assert: unchanged, one lint-only finding on the line that is too wide
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal((DiagnosticKind.LintOnly, 2, 21), (finding.Kind, finding.Line, finding.Column));
    }

    /// <summary>
    ///     Test that a wrap after which another rule would change the paragraph again is not made, and the line is reported as one that cannot be wrapped.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_WrapThatOtherRulesWouldChangeAgain_IsWithdrawnAndReported()
    {
        // Arrange: a URL that directly follows a quote marker, where the URL rule leaves it alone, and that a wrap would move behind a blank
        const string input = ">ant\n>http://a.b/c cat dog eel fox gnu\n";

        // Act: format
        var (result, formatted) = Format(input);

        // Assert: nothing is rewritten, one finding on the line that is too wide, and the result is stable
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        var finding = Assert.Single(result.Diagnostics);
        Assert.Equal((DiagnosticKind.LintOnly, "line-length", 2, 21), (finding.Kind, finding.RuleId, finding.Line, finding.Column));
        BlockFormatterAssertions.AssertSafeAndStable(input, Width(20));
    }

    /// <summary>
    ///     Test that nothing is wrapped when the line length is zero or the rule is off, and that the paragraph-wrap rule does nothing in the fill mode.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_SettingsThatDisableWrapping_ReportAndChangeNothing()
    {
        // Arrange: a paragraph that is too wide for twenty columns
        const string input = "ant bat cat dog eel fox gnu\n";
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var configs = new[]
        {
            Width(0),
            Width(20) with { Rules = LintMarkConfig.Default.Rules.SetItem("line-length", false) },
            Width(0) with { Rules = LintMarkConfig.Default.Rules.SetItem("paragraph-wrap", true) }
        };

        // Act & Assert: no edit and no finding for any of them
        foreach (var config in configs)
        {
            var (result, formatted) = BlockFormatterAssertions.Apply(formatter, input, config);
            Assert.Equal(input, formatted);
            Assert.Empty(result.Edits);
            Assert.Empty(result.Diagnostics);
        }
    }

    /// <summary>
    ///     Test that a document that is format-unsafe is not wrapped.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_FormatUnsafeDocument_IsNotWrapped()
    {
        // Arrange: a document with an invalid grid table, which the parser can only read without grid tables
        const string input = "para  x\n+---+---+\n\nant bat cat dog eel fox gnu hen\n";
        var document = BlockFormatterAssertions.Parse(input);

        // Act: format
        var result = BlockFormatterAssertions.ProductionFormatter().Format(document, Width(20));

        // Assert: nothing is proposed or reported when the parser fell back
        Assert.True(document.IsFormatUnsafe);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that a document formatter without a paragraph formatter neither wraps nor reports the line length.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_NoParagraphFormatter_NeitherWrapsNorReports()
    {
        // Act: format a paragraph that is too wide with a formatter that has no paragraph formatter
        const string input = "ant bat cat dog eel fox gnu\n";
        var (result, formatted) = BlockFormatterAssertions.Apply(new DocumentFormatter([], new InlineNormalizer()), input, Width(20));

        // Assert: nothing happens
        Assert.Equal(input, formatted);
        Assert.Empty(result.Edits);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that the finding of a line that is too wide is at the position in the original file, also when other edits come before it.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_EditsBeforeTheParagraph_DoNotMoveTheReportedPosition()
    {
        // Act: format a text where a heading edit and a blank-line edit come before the long paragraph
        var (result, _) = Format("# T  \n\n\n\nant bat cat dog eel fox gnu\n");

        // Assert: the finding is on line 5 of the original, column 21
        var finding = Assert.Single(result.Diagnostics, candidate => candidate.RuleId == "line-length");
        Assert.Equal((5, 21), (finding.Line, finding.Column));
    }

    /// <summary>
    ///     Test that the result is the same for every thread that formats the same document.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Format_ConcurrentCalls_GiveTheSameResult()
    {
        // Arrange: a document with lists, quotes and long paragraphs, and the single-threaded text
        const string input = "- ant bat cat dog eel fox gnu hen\n  ink\n\n> jam key log map nut owl pig quo rat\n\nsun toy urn van web fix yak zoo\n";
        var formatter = BlockFormatterAssertions.ProductionFormatter();
        var reference = BlockFormatterAssertions.Apply(formatter, input, Width(20));

        // Act & Assert: every thread gets the same edits, text and findings
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 50; round++)
                {
                    var other = BlockFormatterAssertions.Apply(formatter, input, Width(20));
                    Assert.Equal(reference.Result.Edits, other.Result.Edits);
                    Assert.Equal(reference.Formatted, other.Formatted);
                    Assert.Equal(reference.Result.Diagnostics, other.Result.Diagnostics);
                }
            });
    }

    /// <summary>
    ///     Test that the paragraph formatter is optional: the constructor accepts none, and the shipped formatter has one.
    /// </summary>
    [Fact]
    public void DocumentFormatter_Constructor_ParagraphFormatterOptional_AcceptsNone()
    {
        // Act & Assert: formatters with and without a paragraph formatter can be created
        Assert.NotNull(new DocumentFormatter());
        Assert.NotNull(new DocumentFormatter([], null, null));
        Assert.NotNull(new DocumentFormatter([], new InlineNormalizer(), new ParagraphFormatter()));
    }
}
