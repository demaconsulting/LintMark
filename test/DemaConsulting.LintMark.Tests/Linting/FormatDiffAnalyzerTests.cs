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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the FormatDiffAnalyzer class keeps the findings of the formatter, adds a finding for an always-applied rule
///     that has an edit and no finding, and orders the result.
/// </summary>
public class FormatDiffAnalyzerTests
{
    /// <summary>
    ///     The source document of the tests.
    /// </summary>
    private static readonly SourceDocument Source = SourceDocument.FromText("a.md", "line one\nline two\nline three\n");

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void FormatDiffAnalyzer_Analyze_NullArguments_Throw()
    {
        // Act & Assert: both arguments are checked and named
        var result = new FormatResult([], LineEnding.Lf, []);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => FormatDiffAnalyzer.Analyze(null!, result)).ParamName);
        Assert.Equal("result", Assert.Throws<ArgumentNullException>(() => FormatDiffAnalyzer.Analyze(Source, null!)).ParamName);
    }

    /// <summary>
    ///     Test that the findings of the formatter are kept and ordered by position, rule and message.
    /// </summary>
    [Fact]
    public void FormatDiffAnalyzer_Analyze_FormatterFindings_AreKeptAndOrdered()
    {
        // Arrange: findings in the wrong order, one lint-only
        var diagnostics = ImmutableArray.Create(
            new Diagnostic("max-blank-lines", DiagnosticKind.Formatting, "a.md", 3, 1, "B"),
            new Diagnostic("line-length", DiagnosticKind.LintOnly, "a.md", 1, 5, "C"),
            new Diagnostic("hard-tabs", DiagnosticKind.Formatting, "a.md", 1, 5, "A"));

        // Act: analyze a result without edits
        var found = FormatDiffAnalyzer.Analyze(Source, new FormatResult([], LineEnding.Lf, diagnostics));

        // Assert: all three, by position then rule id
        Assert.Equal(["hard-tabs", "line-length", "max-blank-lines"], found.Select(d => d.RuleId));
        Assert.Equal([true, false, true], found.Select(d => d.Fixable));
    }

    /// <summary>
    ///     Test that an always-applied rule with an edit and no finding gets a fixable finding at its first edit, and one with a finding does not.
    /// </summary>
    [Fact]
    public void FormatDiffAnalyzer_Analyze_EditWithoutFinding_GetsAFixableFinding()
    {
        // Arrange: two edits of one rule without a finding, an edit of a rule that has one, an optional rule and a lint-only rule
        var edits = ImmutableArray.Create(
            new TextEdit(9, 0, " ", RuleCatalog.TrailingWhitespaceRuleId),
            new TextEdit(14, 1, "X", RuleCatalog.TrailingWhitespaceRuleId),
            new TextEdit(0, 1, "L", RuleCatalog.HardTabsRuleId),
            new TextEdit(18, 1, "W", RuleCatalog.ParagraphWrapRuleId),
            new TextEdit(20, 1, "Z", RuleCatalog.LinkEmptyRuleId),
            new TextEdit(22, 1, "Q", "not-a-rule"));
        var diagnostics = ImmutableArray.Create(new Diagnostic(RuleCatalog.HardTabsRuleId, DiagnosticKind.Formatting, "a.md", 1, 1, "Tab"));

        // Act: analyze
        var found = FormatDiffAnalyzer.Analyze(Source, new FormatResult(edits, LineEnding.Lf, diagnostics));

        // Assert: the rule without a finding is reported once at its first edit, the others add nothing
        Assert.Equal(["hard-tabs", "trailing-whitespace"], found.Select(d => d.RuleId));
        var added = found[1];
        Assert.Equal((2, 1), (added.Line, added.Column));
        Assert.True(added.Fixable);
        Assert.Equal("A line ends with trailing whitespace, or a hard break is not exactly two spaces.", added.Message);
    }
}
