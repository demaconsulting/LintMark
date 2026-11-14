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

using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the SentenceSplitter class splits only where a sentence or a clause clearly ends, and never after an abbreviation,
///     an initial, a number, an ellipsis, or before a word in lower case.
/// </summary>
public class SentenceSplitterTests
{
    /// <summary>
    ///     Finds the sentence starts of words separated by blanks.
    /// </summary>
    /// <param name="words">The atoms separated by single spaces.</param>
    /// <returns>The indexes of the atoms that start a new sentence.</returns>
    private static int[] Sentences(string words) => [.. SentenceSplitter.FindSentenceStarts([.. words.Split(' ')])];

    /// <summary>
    ///     Finds the clause starts of words separated by blanks.
    /// </summary>
    /// <param name="words">The atoms separated by single spaces.</param>
    /// <returns>The indexes of the atoms that start a new clause.</returns>
    private static int[] Clauses(string words) => [.. SentenceSplitter.FindClauseStarts([.. words.Split(' ')])];

    /// <summary>
    ///     Test that invalid arguments are rejected and named.
    /// </summary>
    [Fact]
    public void SentenceSplitter_Find_InvalidArguments_Throw()
    {
        // Act & Assert: a default array, a null element and an empty atom are rejected for both searches
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => SentenceSplitter.FindSentenceStarts(default)).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentNullException>(() => SentenceSplitter.FindSentenceStarts(["a", null!])).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => SentenceSplitter.FindClauseStarts(["a", string.Empty])).ParamName);
        Assert.Equal("atom", Assert.Throws<ArgumentNullException>(() => SentenceSplitter.EndsSentence(null!, "A")).ParamName);
        Assert.Equal("next", Assert.Throws<ArgumentException>(() => SentenceSplitter.EndsSentence("a.", string.Empty)).ParamName);
        Assert.Equal("atom", Assert.Throws<ArgumentException>(() => SentenceSplitter.EndsClause(string.Empty)).ParamName);
    }

    /// <summary>
    ///     Test that no atom or one atom has no sentence start.
    /// </summary>
    [Fact]
    public void SentenceSplitter_FindSentenceStarts_SingleAtomAndEmpty_HaveNoStart()
    {
        // Act & Assert: nothing to split
        Assert.Empty(SentenceSplitter.FindSentenceStarts([]));
        Assert.Empty(Sentences("One."));
    }

    /// <summary>
    ///     Test that a period, an exclamation mark and a question mark followed by a capital letter or a digit end a sentence.
    /// </summary>
    [Theory]
    [InlineData("One. Two", 1)]
    [InlineData("Stop! Go", 1)]
    [InlineData("Why? Because", 1)]
    [InlineData("Really?! Yes", 1)]
    [InlineData("It ended. 3 files remain", 2)]
    [InlineData("It was 2024. Then it ended", 3)]
    [InlineData("Version v1.2. Upgrade now", 2)]
    [InlineData("Visit example.com. Then", 2)]
    [InlineData("你好。 世界", 1)]
    public void SentenceSplitter_FindSentenceStarts_SentenceEnd_IsFound(string words, int expected) =>
        Assert.Equal([expected], Sentences(words).Take(1));

    /// <summary>
    ///     Test that closing marks after the terminator do not hide it.
    /// </summary>
    [Theory]
    [InlineData("(See above.) Next")]
    [InlineData("He said \"stop.\" Then")]
    [InlineData("It was *done.* Then")]
    [InlineData("It was **done.** Then")]
    [InlineData("It was done.[^1] Then")]
    [InlineData("It was “done.” Then")]
    [InlineData("It was done.' Then")]
    public void SentenceSplitter_FindSentenceStarts_ClosingMarksAfterTheTerminator_StillEndTheSentence(string words) =>
        Assert.Equal([words.Split(' ').Length - 1], Sentences(words));

    /// <summary>
    ///     Test that abbreviations, initials, numbers, ellipses and other uncertain places do not end a sentence.
    /// </summary>
    [Theory]
    [InlineData("Use e.g. Python")]
    [InlineData("That is i.e. Python")]
    [InlineData("Ask Dr. Smith")]
    [InlineData("See Fig. 3")]
    [InlineData("Apples, etc. Pears")]
    [InlineData("By J. Smith")]
    [InlineData("Plan A. Then")]
    [InlineData("In the U.S. Then")]
    [InlineData("Ph.D. Smith")]
    [InlineData("See step 3. Then")]
    [InlineData("Wait... Then")]
    [InlineData("Wait… Then")]
    [InlineData("It is done. then not")]
    [InlineData("It is done. <b>bold</b> next")]
    [InlineData("It is done. [link](x) next")]
    [InlineData("It is done. $x$ next")]
    [InlineData("It is done. 'quote' next")]
    [InlineData("See [the Fig.](u) Then")]
    [InlineData("See [link](u.) Then")]
    [InlineData("See `code.` Then")]
    [InlineData("Is it ? Then")]
    [InlineData(". Then")]
    [InlineData("Version 1.2 Then")]
    public void SentenceSplitter_FindSentenceStarts_UncertainPlaces_AreNotSplit(string words) =>
        Assert.Empty(Sentences(words));

    /// <summary>
    ///     Test that a sentence that is followed by an opening quote or parenthesis and a capital letter is split.
    /// </summary>
    [Theory]
    [InlineData("It ended. \"Then\" it began")]
    [InlineData("It ended. (Then) it began")]
    [InlineData("It ended. *Then* it began")]
    [InlineData("It ended. [Then](u) it began")]
    [InlineData("It ended. `code` begins")]
    public void SentenceSplitter_FindSentenceStarts_OpeningMarksBeforeACapital_StillStartTheSentence(string words) =>
        Assert.Equal([2], Sentences(words));

    /// <summary>
    ///     Test that commas, semicolons, colons and dashes end a clause, and that an emoji code or a scope does not.
    /// </summary>
    [Theory]
    [InlineData("one, two", 1)]
    [InlineData("one; two", 1)]
    [InlineData("one: two", 1)]
    [InlineData("one — two", 2)]
    [InlineData("one, \"two\"", 1)]
    [InlineData("one,\" two", 1)]
    [InlineData("一，二", 0)]
    public void SentenceSplitter_FindClauseStarts_ClausePunctuation_IsFound(string words, int start)
    {
        // Act: find the clause starts (the East Asian example has no blank, so it has none)
        var found = Clauses(words);

        // Assert: the clause starts at the atom after the punctuation, when there is one
        Assert.Equal(start == 0 ? [] : new[] { start }, found);
    }

    /// <summary>
    ///     Test that atoms that merely contain or look like clause punctuation do not end a clause.
    /// </summary>
    [Theory]
    [InlineData("a :smile: b")]
    [InlineData("a std:: b")]
    [InlineData("a 1,000 b")]
    [InlineData("a , b")]
    [InlineData("a `x,` b")]
    [InlineData("a b.c b")]
    public void SentenceSplitter_FindClauseStarts_NotClausePunctuation_IsNotSplit(string words) =>
        Assert.Empty(Clauses(words));

    /// <summary>
    ///     Test that the same atoms always give the same answer on every thread.
    /// </summary>
    [Fact]
    public void SentenceSplitter_FindSentenceStarts_ConcurrentCalls_GiveTheSameAnswers()
    {
        // Arrange: atoms with several kinds of ends and the single-threaded answer
        var atoms = "One. Two! Dr. Smith e.g. this, that; Three? (Four.) Five".Split(' ').ToImmutableArrayForTest();
        var expected = SentenceSplitter.FindSentenceStarts(atoms);

        // Act & Assert: every thread gets the same answer
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 200; round++)
                {
                    Assert.Equal(expected, SentenceSplitter.FindSentenceStarts(atoms));
                }
            });
    }
}

/// <summary>
///     Helper for the tests of the sentence splitter.
/// </summary>
internal static class SentenceSplitterTestExtensions
{
    /// <summary>
    ///     Copies words to an immutable array.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <returns>The array.</returns>
    public static System.Collections.Immutable.ImmutableArray<string> ToImmutableArrayForTest(this IEnumerable<string> words) => [.. words];
}
