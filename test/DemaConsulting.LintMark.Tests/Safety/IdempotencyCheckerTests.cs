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
///     Test that the IdempotencyChecker class accepts formatters whose output is a fixed point of themselves
///     and refuses formatters that flip, grow or fail when run on their own output.
/// </summary>
public class IdempotencyCheckerTests
{
    /// <summary>
    ///     Creates the reformat delegate of a block formatter: parse, format and patch.
    /// </summary>
    /// <param name="formatter">The block formatter.</param>
    /// <returns>A delegate that formats LF-normalized text.</returns>
    private static Func<string, string> Reformat(IBlockFormatter formatter)
    {
        var documentFormatter = new DocumentFormatter([formatter]);
        return text =>
        {
            var document = DocumentParser.Parse(SourceDocument.FromText("a.md", text));
            var result = documentFormatter.Format(document, LintMarkConfig.Default);
            return SourcePatcher.Apply(document.Source, result.Edits);
        };
    }

    /// <summary>
    ///     Test that a formatter that leaves its own output alone is idempotent.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_StableFormatter_Idempotent()
    {
        // Arrange: text that a double-space formatter has already formatted
        var reformat = Reformat(new ReplacingFormatter("  ", " "));

        // Act: format the formatted text again
        var result = IdempotencyChecker.Check("a b c\n", reformat);

        // Assert: nothing changes
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
    }

    /// <summary>
    ///     Test that a formatter that flips between two layouts is not idempotent.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_FlipFlopFormatter_NotIdempotent()
    {
        // Arrange: text in the first of the two layouts
        var reformat = Reformat(new FlipFlopFormatter());

        // Act: format the formatted text again
        var result = IdempotencyChecker.Check("alpha beta\n", reformat);

        // Assert: the second pass changed the first line
        Assert.False(result.IsEquivalent);
        Assert.Contains("line 1", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a formatter that keeps growing the text is not idempotent.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_GrowingFormatter_NotIdempotent()
    {
        // Arrange: a paragraph and a formatter that appends a space on every pass
        var reformat = Reformat(new GrowingFormatter());

        // Act: format the formatted text again
        var result = IdempotencyChecker.Check("a\n", reformat);

        // Assert: the second pass changed the text
        Assert.False(result.IsEquivalent);
        Assert.Contains("changes line 1", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a delegate that throws makes the text not idempotent and names the exception type.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_DelegateThrows_NotIdempotent()
    {
        // Arrange: a delegate that fails
        Func<string, string> failing = _ => throw new InvalidOperationException("boom");

        // Act: check
        var result = IdempotencyChecker.Check("a\n", failing);

        // Assert: the check refuses and names the exception type
        Assert.False(result.IsEquivalent);
        Assert.Equal("Formatting the formatted text again failed (InvalidOperationException)", result.Description);
    }

    /// <summary>
    ///     Test that a delegate that returns null makes the text not idempotent.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_DelegateReturnsNull_NotIdempotent()
    {
        // Arrange: a delegate that returns no text
        Func<string, string> noText = _ => null!;

        // Act: check
        var result = IdempotencyChecker.Check("a\n", noText);

        // Assert: the check refuses
        Assert.False(result.IsEquivalent);
        Assert.Equal("Formatting the formatted text again returned no text", result.Description);
    }

    /// <summary>
    ///     Test that a null argument is rejected.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_NullArguments_Throw()
    {
        // Arrange / Act / Assert: neither the text nor the delegate may be null
        Assert.Throws<ArgumentNullException>(() => IdempotencyChecker.Check(null!, text => text));
        Assert.Throws<ArgumentNullException>(() => IdempotencyChecker.Check("a\n", null!));
    }

    /// <summary>
    ///     Test that the description names the line that the second pass changed first.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_FirstDifference_ReportsLine()
    {
        // Arrange: a delegate that changes the third line only
        static string ChangeThirdLine(string text) => text.Replace("l3", "L3", StringComparison.Ordinal);

        // Act: check
        var result = IdempotencyChecker.Check("l1\nl2\nl3\nl4\n", ChangeThirdLine);

        // Assert: the third line is named
        Assert.False(result.IsEquivalent);
        Assert.Contains("line 3", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a difference on the first line is reported as line one.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_DifferenceOnFirstLine_ReportsLineOne()
    {
        // Arrange: a delegate that changes the first line
        static string ChangeFirstLine(string text) => text.Replace("l1", "L1", StringComparison.Ordinal);

        // Act: check
        var result = IdempotencyChecker.Check("l1\nl2\n", ChangeFirstLine);

        // Assert: the first line is named
        Assert.False(result.IsEquivalent);
        Assert.StartsWith("Formatting the formatted text again changes line 1:", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text appended at the very end is reported on the last line.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_DifferenceAtEnd_ReportsLastLine()
    {
        // Arrange: text without final line ending and a delegate that appends a character
        static string Append(string text) => text + "d";

        // Act: check
        var result = IdempotencyChecker.Check("a\nb\nc", Append);

        // Assert: the third and last line is named with both versions
        Assert.False(result.IsEquivalent);
        Assert.Equal("Formatting the formatted text again changes line 3: first \"c\" then \"cd\"", result.Description);
    }

    /// <summary>
    ///     Test that formatting empty text with a delegate that adds text is reported on line one.
    /// </summary>
    [Fact]
    public void IdempotencyChecker_Check_EmptyTextGrows_ReportsLineOne()
    {
        // Arrange: empty text and a delegate that adds a character
        static string Append(string text) => text + "x";

        // Act: check
        var result = IdempotencyChecker.Check(string.Empty, Append);

        // Assert: the difference is at offset zero of line one
        Assert.False(result.IsEquivalent);
        Assert.Equal("Formatting the formatted text again changes line 1: first \"\" then \"x\"", result.Description);
    }
}
