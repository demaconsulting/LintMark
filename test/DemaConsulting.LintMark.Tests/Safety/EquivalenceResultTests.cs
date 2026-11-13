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
///     Test that the EquivalenceResult type reports a passed check without description and a failed check with
///     a single bounded line, and that its helpers excerpt and compare text safely.
/// </summary>
public class EquivalenceResultTests
{
    /// <summary>
    ///     Test that the result of a passed check has no description.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Equivalent_Result_HasNoDescription()
    {
        // Arrange / Act: the shared result of a passed check
        var result = EquivalenceResult.Equivalent;

        // Assert: it is equivalent and says nothing
        Assert.True(result.IsEquivalent);
        Assert.Null(result.Description);
    }

    /// <summary>
    ///     Test that a failed check keeps a short description as it is.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Different_ShortDescription_KeptAsIs()
    {
        // Arrange / Act: a failed check with a short description
        var result = EquivalenceResult.Different("line 3 differs");

        // Assert: it is not equivalent and the text is unchanged
        Assert.False(result.IsEquivalent);
        Assert.Equal("line 3 differs", result.Description);
    }

    /// <summary>
    ///     Test that a null description is rejected.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Different_NullDescription_Throws()
    {
        // Arrange / Act / Assert: the description must not be null
        Assert.Throws<ArgumentNullException>(() => EquivalenceResult.Different(null!));
    }

    /// <summary>
    ///     Test that a long description with line breaks is cut and kept on one line.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Different_LongDescription_IsCutAndSingleLine()
    {
        // Arrange: a description of a thousand characters followed by a line break and more text
        var description = new string('x', 1000) + "\nmore";

        // Act: create the result
        var result = EquivalenceResult.Different(description);

        // Assert: the text is cut to 240 characters plus an ellipsis and has no line break
        Assert.NotNull(result.Description);
        Assert.Equal(new string('x', 240) + "...", result.Description);
        Assert.DoesNotContain("\n", result.Description);
    }

    /// <summary>
    ///     Test that a description is cut only when it is longer than the limit.
    /// </summary>
    /// <param name="length">The length of the description.</param>
    /// <param name="expectedLength">The expected length of the stored description.</param>
    [Theory]
    [InlineData(239, 239)]
    [InlineData(240, 240)]
    [InlineData(241, 243)]
    public void EquivalenceResult_Different_DescriptionAroundLimit_CutOnlyAbove(int length, int expectedLength)
    {
        // Arrange / Act: a description of the given length
        var result = EquivalenceResult.Different(new string('y', length));

        // Assert: it is cut only when longer than 240 characters
        Assert.NotNull(result.Description);
        Assert.Equal(expectedLength, result.Description.Length);
    }

    /// <summary>
    ///     Test that cutting a long description does not split a surrogate pair.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Different_CutInsideSurrogatePair_KeepsPairWhole()
    {
        // Arrange: a description whose 240-character limit falls between the two halves of an emoji
        var description = new string('x', 239) + "\U0001F600" + "tail";

        // Act: create the result
        var result = EquivalenceResult.Different(description);

        // Assert: the emoji is dropped as a whole and no lone surrogate remains
        Assert.Equal(new string('x', 239) + "...", result.Description);
    }

    /// <summary>
    ///     Test that every kind of line break is replaced by a space.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Different_LineBreaks_ReplacedBySpaces()
    {
        // Arrange / Act: a description with CRLF, LF, CR, form feed, next line, and the Unicode line and
        // paragraph separators
        var result = EquivalenceResult.Different("a\r\nb\nc\rd\u000Ce\u0085f\u2028g\u2029h");

        // Assert: each line break became one space
        Assert.Equal("a b c d e f g h", result.Description);
    }

    /// <summary>
    ///     Test that an excerpt escapes characters that would end or distort a line.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_ControlCharacters_AreEscaped()
    {
        // Arrange / Act: a text with a line feed, a carriage return, a tab and another control character
        var excerpt = EquivalenceResult.Excerpt("a\nb\rc\td\u0001e", 0);

        // Assert: the first three are written as escapes, the last one as a question mark
        Assert.Equal("a\\nb\\rc\\td?e", excerpt);
    }

    /// <summary>
    ///     Test that an excerpt shows the Unicode line and paragraph separators as question marks.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_LineAndParagraphSeparators_AreEscaped()
    {
        // Arrange / Act: a text with a line separator and a paragraph separator
        var excerpt = EquivalenceResult.Excerpt("a\u2028b\u2029c", 0);

        // Assert: both are written as question marks so the excerpt stays on one line
        Assert.Equal("a?b?c", excerpt);
    }

    /// <summary>
    ///     Test that an excerpt is cut after the excerpt length.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_LongText_CutAtExcerptLength()
    {
        // Arrange: a text longer than an excerpt
        var text = new string('z', EquivalenceResult.ExcerptLength + 40);

        // Act: take the excerpt from the start
        var excerpt = EquivalenceResult.Excerpt(text, 0);

        // Assert: it has the excerpt length
        Assert.Equal(EquivalenceResult.ExcerptLength, excerpt.Length);
    }

    /// <summary>
    ///     Test that the escapes of an excerpt may double its length but no more.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_ManyLineFeeds_AtMostTwiceExcerptLength()
    {
        // Arrange: a text of line feeds, each of which is shown as a two-character escape
        var text = new string('\n', EquivalenceResult.ExcerptLength + 40);

        // Act: take the excerpt from the start
        var excerpt = EquivalenceResult.Excerpt(text, 0);

        // Assert: sixty characters of the text are shown as one hundred and twenty characters
        Assert.Equal(2 * EquivalenceResult.ExcerptLength, excerpt.Length);
    }

    /// <summary>
    ///     Test that an excerpt does not split a surrogate pair at either end.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_SurrogatePairAtEnds_KeepsPairWhole()
    {
        // Arrange: an emoji that straddles the end of the window, and one that straddles the start offset
        var atEnd = new string('a', EquivalenceResult.ExcerptLength - 1) + "\U0001F600" + "tail";
        var atStart = "a\U0001F600b";

        // Act: take the excerpts
        var endExcerpt = EquivalenceResult.Excerpt(atEnd, 0);
        var startExcerpt = EquivalenceResult.Excerpt(atStart, 2);

        // Assert: the first drops the emoji as a whole, the second starts with the whole emoji
        Assert.Equal(new string('a', EquivalenceResult.ExcerptLength - 1), endExcerpt);
        Assert.Equal("\U0001F600b", startExcerpt);
    }

    /// <summary>
    ///     Test that an excerpt starts at the given offset.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_StartInsideText_StartsThere()
    {
        // Arrange / Act: take the excerpt from offset 3
        var excerpt = EquivalenceResult.Excerpt("abcdef", 3);

        // Assert: it holds the rest of the text
        Assert.Equal("def", excerpt);
    }

    /// <summary>
    ///     Test that a start offset outside the text is clamped into the text.
    /// </summary>
    [Fact]
    public void EquivalenceResult_Excerpt_StartOutsideText_IsClamped()
    {
        // Arrange / Act: a start before the text and one after it
        var before = EquivalenceResult.Excerpt("abc", -5);
        var after = EquivalenceResult.Excerpt("abc", 10);

        // Assert: the first shows the whole text, the second nothing
        Assert.Equal("abc", before);
        Assert.Equal(string.Empty, after);
    }

    /// <summary>
    ///     Test that the first difference is the offset of the first character that differs.
    /// </summary>
    [Fact]
    public void EquivalenceResult_FirstDifference_DifferentTexts_ReturnsOffset()
    {
        // Arrange / Act: texts that differ at offset 2
        var index = EquivalenceResult.FirstDifference("abcd", "abXd");

        // Assert: the offset of X
        Assert.Equal(2, index);
    }

    /// <summary>
    ///     Test that when one text is a prefix of the other the length of the shorter text is returned.
    /// </summary>
    [Fact]
    public void EquivalenceResult_FirstDifference_PrefixOfOther_ReturnsShorterLength()
    {
        // Arrange / Act: a text and an extension of it, in both orders
        var shorterFirst = EquivalenceResult.FirstDifference("abc", "abcdef");
        var shorterSecond = EquivalenceResult.FirstDifference("abcdef", "abc");

        // Assert: the length of the shorter text
        Assert.Equal(3, shorterFirst);
        Assert.Equal(3, shorterSecond);
    }

    /// <summary>
    ///     Test that identical texts have their length as the first difference.
    /// </summary>
    [Fact]
    public void EquivalenceResult_FirstDifference_IdenticalTexts_ReturnsLength()
    {
        // Arrange / Act: the same text twice
        var index = EquivalenceResult.FirstDifference("abc", "abc");

        // Assert: the common prefix is the whole text
        Assert.Equal(3, index);
    }
}
