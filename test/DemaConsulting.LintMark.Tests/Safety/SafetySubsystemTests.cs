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
using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the three safety checks (rendered HTML, Pandoc structure and idempotency) work together:
///     each catches the damage that the others cannot see, and a correct rewrite passes all of them.
/// </summary>
public class SafetySubsystemTests
{
    /// <summary>
    ///     Formats LF-normalized text with a block formatter: parse, format and patch.
    /// </summary>
    /// <param name="formatter">The block formatter.</param>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatWith(IBlockFormatter formatter, string text)
    {
        var document = DocumentParser.Parse(SourceDocument.FromText("a.md", text));
        var result = new DocumentFormatter([formatter]).Format(document, LintMarkConfig.Default);
        return SourcePatcher.Apply(document.Source, result.Edits);
    }

    /// <summary>
    ///     Test that a rewrap that does not change the page passes the HTML check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_HtmlEquivalence_BenignReflow_Equivalent()
    {
        // Arrange / Act: a paragraph wrapped at different places
        var result = HtmlEquivalenceChecker.Check("alpha beta gamma\ndelta\n", "alpha\nbeta gamma delta\n");

        // Assert: same page
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a rewrite that changes the page fails the HTML check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_HtmlEquivalence_SemanticChange_Detected()
    {
        // Arrange / Act: a rewrite that drops a hard break
        var result = HtmlEquivalenceChecker.Check("a  \nb\n", "a\nb\n");

        // Assert: the check refuses and says why
        Assert.False(result.IsEquivalent);
        Assert.NotNull(result.Description);
    }

    /// <summary>
    ///     Test that a rewrite that joins the lines of a fenced div fails the structure check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_StructureEquivalence_JoinedFence_Detected()
    {
        // Arrange / Act: a fenced div and the same lines joined
        var result = StructuralTokenChecker.Check(":::note\ntext\n:::\n", ":::note text :::\n");

        // Assert: the check refuses
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that the rendered HTML cannot see a joined fence but the structure check can.
    /// </summary>
    [Fact]
    public void SafetySubsystem_StructureEquivalence_HtmlBlindToJoinedFence_ButStructureCatches()
    {
        // Arrange: a fenced div and the same lines joined, which the Markdown renderer treats as one paragraph either way
        const string before = ":::note\ntext\n:::\n";
        const string after = ":::note text :::\n";

        // Act: run both checks on the same pair
        var html = HtmlEquivalenceChecker.Check(before, after);
        var structure = StructuralTokenChecker.Check(before, after);

        // Assert: only the structure check sees the damage
        Assert.True(html.IsEquivalent);
        Assert.False(structure.IsEquivalent);
    }

    /// <summary>
    ///     Test that a formatter that flips between two layouts fails the idempotency check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_Idempotency_FlipFlopFormatter_Detected()
    {
        // Arrange: text after the first pass of a formatter that flips the layout on every pass
        var formatter = new FlipFlopFormatter();
        var firstPass = FormatWith(formatter, "alpha beta\n");

        // Act: check that another pass changes nothing
        var result = IdempotencyChecker.Check(firstPass, text => FormatWith(formatter, text));

        // Assert: the layout flipped again, and the rendered page is the same each time
        Assert.NotEqual("alpha beta\n", firstPass);
        Assert.False(result.IsEquivalent);
        Assert.True(HtmlEquivalenceChecker.Check("alpha beta\n", firstPass).IsEquivalent);
    }

    /// <summary>
    ///     Test that a formatter that keeps growing the text fails the idempotency check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_Idempotency_GrowingFormatter_Detected()
    {
        // Arrange: text after the first pass of a formatter that appends a space on every pass
        var formatter = new GrowingFormatter();
        var firstPass = FormatWith(formatter, "alpha\n");

        // Act: check that another pass changes nothing
        var result = IdempotencyChecker.Check(firstPass, text => FormatWith(formatter, text));

        // Assert: the text grew again, although the rendered page is the same
        Assert.False(result.IsEquivalent);
        Assert.True(HtmlEquivalenceChecker.Check("alpha\n", firstPass).IsEquivalent);
    }

    /// <summary>
    ///     Test that a correct formatter passes every check.
    /// </summary>
    [Fact]
    public void SafetySubsystem_AllChecks_CorrectFormatter_AllPass()
    {
        // Arrange: a document with double spaces and a fenced div, and a formatter that collapses the double spaces
        const string original = "alpha  beta\n\n:::note\ntext\n:::\n\ngamma  delta\n";
        var formatter = new ReplacingFormatter("  ", " ");
        var formatted = FormatWith(formatter, original);

        // Act: run the three checks
        var html = HtmlEquivalenceChecker.Check(original, formatted);
        var structure = StructuralTokenChecker.Check(original, formatted);
        var idempotency = IdempotencyChecker.Check(formatted, text => FormatWith(formatter, text));

        // Assert: the text was changed and every check passes
        Assert.Equal("alpha beta\n\n:::note\ntext\n:::\n\ngamma delta\n", formatted);
        Assert.True(html.IsEquivalent);
        Assert.True(structure.IsEquivalent);
        Assert.True(idempotency.IsEquivalent);
    }

    /// <summary>
    ///     Test that every failed check describes the first difference in one line of bounded length.
    /// </summary>
    [Fact]
    public void SafetySubsystem_ReportsDifference_FailedChecks_DescribeFirstDifferenceInOneBoundedLine()
    {
        // Arrange: one failing pair per check, with long texts so that the description has to be cut
        var longTail = string.Join("\n", Enumerable.Range(0, 500).Select(i => $"word{i}"));
        var flipFlop = new FlipFlopFormatter();
        var firstPass = FormatWith(flipFlop, "alpha beta\n");

        // Act: run the three checks on their failing pairs
        var html = HtmlEquivalenceChecker.Check("a  \nb\n" + longTail, "a\nb\n" + longTail);
        var structure = StructuralTokenChecker.Check(":::note\ntext\n:::\n" + longTail, ":::note text :::\n" + longTail);
        var idempotency = IdempotencyChecker.Check(firstPass, text => FormatWith(flipFlop, text));

        // Assert: every description is present, on one line and bounded
        foreach (var result in new[] { html, structure, idempotency })
        {
            Assert.False(result.IsEquivalent);
            Assert.NotNull(result.Description);
            Assert.DoesNotContain("\n", result.Description);
            Assert.DoesNotContain("\r", result.Description);
            Assert.True(result.Description.Length <= 243, $"length {result.Description.Length}");
        }
    }

    /// <summary>
    ///     Test that a check that found no difference has no description.
    /// </summary>
    [Fact]
    public void SafetySubsystem_ReportsDifference_PassedChecks_HaveNoDescription()
    {
        // Arrange: a rewrite that every check accepts
        const string original = "alpha  beta\n";
        var formatter = new ReplacingFormatter("  ", " ");
        var formatted = FormatWith(formatter, original);

        // Act: run the three checks
        var html = HtmlEquivalenceChecker.Check(original, formatted);
        var structure = StructuralTokenChecker.Check(original, formatted);
        var idempotency = IdempotencyChecker.Check(formatted, text => FormatWith(formatter, text));

        // Assert: none of them describes a difference
        Assert.Null(html.Description);
        Assert.Null(structure.Description);
        Assert.Null(idempotency.Description);
    }
}
