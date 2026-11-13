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

using DemaConsulting.LintMark.Safety;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the StructuralTokenChecker class refuses rewrites that damage Pandoc structure that the
///     Markdown renderer cannot see, and accepts rewrites that keep it.
/// </summary>
public class StructuralTokenCheckerTests
{
    /// <summary>
    ///     Test that joining the lines of a fenced div into one line is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_JoinedFenceLines_Different()
    {
        // Arrange / Act: a fenced div and the same lines joined into prose
        var result = StructuralTokenChecker.Check(":::note\ntext\n:::\n", ":::note text :::\n");

        // Assert: the first structural line differs
        Assert.False(result.IsEquivalent);
        Assert.NotNull(result.Description);
    }

    /// <summary>
    ///     Test that a fence line that moved into a paragraph is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_FenceMovedIntoParagraph_Different()
    {
        // Arrange / Act: a closing fence on its own line and the same fence appended to the paragraph
        var result = StructuralTokenChecker.Check("para\n:::\n", "para :::\n");

        // Assert: the fence line is gone
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a raw LaTeX command that was joined to its neighbors is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_PageBreakCommandJoined_Different()
    {
        // Arrange / Act: a command on its own line and the same command inside a line (the command name is
        // written in two parts only so that the spell checker does not take it for a misspelled word)
        const string command = "\\new" + "page";
        var result = StructuralTokenChecker.Check($"a\n{command}\nb\n", $"a {command} b\n");

        // Assert: the command line is gone
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a display math delimiter that was joined to its neighbors is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_DisplayMathDelimiterJoined_Different()
    {
        // Arrange / Act: display math with delimiter lines and the same math in one line
        var result = StructuralTokenChecker.Check("$$\nx = 1\n$$\n", "$$ x = 1 $$\n");

        // Assert: the delimiter lines are gone
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a changed grid table border is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_GridBorderChanged_Different()
    {
        // Arrange / Act: a grid table and the same table with shorter borders
        var result = StructuralTokenChecker.Check(
            "+---+---+\n| a | b |\n+---+---+\n",
            "+---+--+\n| a | b |\n+---+--+\n");

        // Assert: the border line differs
        Assert.False(result.IsEquivalent);
        Assert.Contains("+---+---+", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a lost attribute group is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_AttributeGroupLost_Different()
    {
        // Arrange / Act: a heading with an identifier and the same heading without it
        var result = StructuralTokenChecker.Check("# Title {#sec}\n", "# Title\n");

        // Assert: the group is named with its counts
        Assert.False(result.IsEquivalent);
        Assert.Equal("Attribute group \"{#sec}\" occurs 1 time(s) before and 0 after", result.Description);
    }

    /// <summary>
    ///     Test that an attribute group that appeared is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_AttributeGroupAdded_Different()
    {
        // Arrange / Act: a heading without identifier and the same heading with one
        var result = StructuralTokenChecker.Check("# Title\n", "# Title {#sec}\n");

        // Assert: the group is named with its counts
        Assert.False(result.IsEquivalent);
        Assert.Equal("Attribute group \"{#sec}\" occurs 0 time(s) before and 1 after", result.Description);
    }

    /// <summary>
    ///     Test that a duplicated citation is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_CitationDuplicated_Different()
    {
        // Arrange / Act: one citation and the same citation twice
        var result = StructuralTokenChecker.Check("See [@key].\n", "See [@key] [@key].\n");

        // Assert: the citation counts differ
        Assert.False(result.IsEquivalent);
        Assert.Equal("Citation \"[@key]\" occurs 1 time(s) before and 2 after", result.Description);
    }

    /// <summary>
    ///     Test that a lost citation is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_CitationLost_Different()
    {
        // Arrange / Act: a citation and the same sentence without it
        var result = StructuralTokenChecker.Check("See [@key].\n", "See.\n");

        // Assert: the citation counts differ
        Assert.False(result.IsEquivalent);
        Assert.Equal("Citation \"[@key]\" occurs 1 time(s) before and 0 after", result.Description);
    }

    /// <summary>
    ///     Test that a change of indentation alone is not a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_IndentationOnlyChange_Equivalent()
    {
        // Arrange / Act: a fenced div and the same div with indented fence lines
        var result = StructuralTokenChecker.Check(":::note\ntext\n:::\n", "  :::note\ntext\n  :::\n");

        // Assert: the tokens are the same
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that changing the indentation of a list item that holds a fenced div is not a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_ListIndentChangedWithFenceInside_Equivalent()
    {
        // Arrange / Act: a list item with a div indented by two and by four columns
        var result = StructuralTokenChecker.Check(
            "- item\n\n  :::note\n  text\n  :::\n",
            "- item\n\n    :::note\n    text\n    :::\n");

        // Assert: the tokens are the same
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that text without any Pandoc construct is equivalent after any reflow.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_NoSentinels_Equivalent()
    {
        // Arrange / Act: plain prose joined into one line
        var result = StructuralTokenChecker.Check("a\nb\n", "a b\n");

        // Assert: nothing structural to compare
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that sentinel lines inside code that stay as they are keep the texts equivalent.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_SentinelsInsideCodeUnchanged_Equivalent()
    {
        // Arrange / Act: a code block holding a fence line, followed by a paragraph that loses trailing blanks
        var result = StructuralTokenChecker.Check("```\n:::\n```\n\ntext  \n", "```\n:::\n```\n\ntext\n");

        // Assert: the token sequences are equal
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that fence lines in another order are a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_ReorderedFences_Different()
    {
        // Arrange / Act: two fence lines swapped
        var result = StructuralTokenChecker.Check(":::a\n:::b\n", ":::b\n:::a\n");

        // Assert: the sequences differ at the first token
        Assert.False(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that block quote markers do not change a token.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_BlockQuotePrefixChange_Equivalent()
    {
        // Arrange / Act: a div inside a block quote and the same div without the quote markers
        var result = StructuralTokenChecker.Check("> :::note\n> text\n> :::\n", ":::note\ntext\n:::\n");

        // Assert: quote markers are stripped before the tokens are compared
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that trailing blanks on a token line are not a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_TrailingBlanksOnTokenLine_Equivalent()
    {
        // Arrange / Act: a fence line with trailing blanks and the same without
        var result = StructuralTokenChecker.Check(":::note   \ntext\n:::\n", ":::note\ntext\n:::\n");

        // Assert: trailing blanks are stripped before the tokens are compared
        Assert.True(result.IsEquivalent);
    }

    /// <summary>
    ///     Test that a code fence with braces that was joined to its neighbors is a difference.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_BracedCodeFenceJoined_Different()
    {
        // Arrange / Act: a code fence with a braced attribute and the same text in one line
        var result = StructuralTokenChecker.Check("```{r}\ncode\n```\n", "``` {r} code ```\n");

        // Assert: the braced fence line is gone
        Assert.False(result.IsEquivalent);
        Assert.Contains("```{r}", result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the description names the first token that differs, with its position and both versions.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_Difference_DescriptionNamesFirstDifferingToken()
    {
        // Arrange: the second fence line differs
        const string before = ":::a\n:::b\n";
        const string after = ":::a\n:::c\n";

        // Act: compare
        var result = StructuralTokenChecker.Check(before, after);

        // Assert: the position and both tokens are named
        Assert.Equal("Pandoc structural line 2 differs: before \":::b\" after \":::c\"", result.Description);
    }

    /// <summary>
    ///     Test that a token that exists on one side only is described as missing on the other.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_TokenOnOneSideOnly_DescribedAsNone()
    {
        // Arrange / Act: a fence line that disappeared, and one that appeared
        var lost = StructuralTokenChecker.Check(":::a\n", "x\n");
        var added = StructuralTokenChecker.Check("x\n", ":::a\n");

        // Assert: the missing side is named as none
        Assert.Equal("Pandoc structural line 1 differs: before \":::a\" after (none)", lost.Description);
        Assert.Equal("Pandoc structural line 1 differs: before (none) after \":::a\"", added.Description);
    }

    /// <summary>
    ///     Test that a carriage return is rejected in either text.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_CarriageReturn_Throws()
    {
        // Arrange / Act / Assert: only LF-normalized text is accepted
        Assert.Throws<ArgumentException>(() => StructuralTokenChecker.Check(":::\r\n", ":::\n"));
        Assert.Throws<ArgumentException>(() => StructuralTokenChecker.Check(":::\n", ":::\r\n"));
    }

    /// <summary>
    ///     Test that a null argument is rejected.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_NullArguments_Throw()
    {
        // Arrange / Act / Assert: neither text may be null
        Assert.Throws<ArgumentNullException>(() => StructuralTokenChecker.Check(null!, "a\n"));
        Assert.Throws<ArgumentNullException>(() => StructuralTokenChecker.Check("a\n", null!));
    }

    /// <summary>
    ///     Test that a failing token collection is reported as a difference that names the exception type.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_TokenCollectionThrows_ReportsDifferenceNamingExceptionType()
    {
        // Arrange / Act: two different texts whose token collection fails
        var result = StructuralTokenChecker.Check(
            "a\n",
            "b\n",
            _ => throw new InvalidOperationException("collection failed"));

        // Assert: the undecided check is a difference
        Assert.False(result.IsEquivalent);
        Assert.NotNull(result.Description);
        Assert.Contains("could not be compared", result.Description, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), result.Description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a missing token collection is rejected.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_NullTokenCollection_Throws()
    {
        // Arrange / Act / Assert: the collection function may not be null
        Assert.Throws<ArgumentNullException>(() => StructuralTokenChecker.Check("a\n", "b\n", null!));
    }

    /// <summary>
    ///     Test that many threads get the same verdicts.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_ParallelCalls_SameResult()
    {
        // Arrange: one equivalent and one different pair, and the single-threaded results
        var equivalent = StructuralTokenChecker.Check(":::note\ntext\n:::\n", "  :::note\ntext\n  :::\n");
        var different = StructuralTokenChecker.Check(":::note\ntext\n:::\n", ":::note text :::\n");
        var results = new (EquivalenceResult Equivalent, EquivalenceResult Different)[64];

        // Act: check concurrently
        Parallel.For(
            0,
            results.Length,
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            i => results[i] = (
                StructuralTokenChecker.Check(":::note\ntext\n:::\n", "  :::note\ntext\n  :::\n"),
                StructuralTokenChecker.Check(":::note\ntext\n:::\n", ":::note text :::\n")));

        // Assert: every result equals the single-threaded one
        Assert.All(results, r =>
        {
            Assert.Equal(equivalent, r.Equivalent);
            Assert.Equal(different, r.Different);
        });
    }

    /// <summary>
    ///     Test that identical text is equivalent.
    /// </summary>
    [Fact]
    public void StructuralTokenChecker_Check_IdenticalText_Equivalent()
    {
        // Arrange / Act: the same text twice
        var result = StructuralTokenChecker.Check(":::note\ntext {#id} [@key]\n:::\n", ":::note\ntext {#id} [@key]\n:::\n");

        // Assert: equivalent without description
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
    }
}
