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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the LineWrapper class fills lines by display width, never splits an atom, and keeps block syntax away from the start of a line.
/// </summary>
public class LineWrapperTests
{
    /// <summary>
    ///     Wraps atoms given as one string separated by blanks.
    /// </summary>
    /// <param name="atoms">The atoms separated by single spaces.</param>
    /// <param name="firstWidth">The width of the first line.</param>
    /// <param name="width">The width of the other lines; the first width when omitted.</param>
    /// <returns>The wrapped lines.</returns>
    private static WrappedLines Wrap(string atoms, int firstWidth, int? width = null) =>
        LineWrapper.Wrap([.. atoms.Split(' ')], firstWidth, width ?? firstWidth);

    /// <summary>
    ///     Test that atoms are put on a line as long as they fit.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_AtomsThatDoNotFit_GoToTheNextLine()
    {
        // Act: wrap four words of three letters into lines of seven columns
        var wrapped = Wrap("ant bat cat dog", 7);

        // Assert: two atoms per line
        Assert.Equal(["ant bat", "cat dog"], wrapped.Lines);
        Assert.False(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that a line that is exactly as wide as the width stays, and one column less splits it.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_LineExactlyAsWideAsTheWidth_Stays()
    {
        // Act: wrap two words that need seven columns into seven and into six columns
        var exact = Wrap("ant bat", 7);
        var narrow = Wrap("ant bat", 6);

        // Assert: seven columns hold both, six split them
        Assert.Equal(["ant bat"], exact.Lines);
        Assert.Equal(["ant", "bat"], narrow.Lines);
    }

    /// <summary>
    ///     Test that an atom wider than the width stands alone on its line and is never cut.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_AtomWiderThanTheWidth_StandsAloneOnItsLine()
    {
        // Act: wrap around a link that is wider than the width
        var wrapped = LineWrapper.Wrap(["a", "[a long link](http://example.org/a/long/path)", "b"], 10, 10);

        // Assert: the link has its own line, which is not reported because it holds one atom
        Assert.Equal(["a", "[a long link](http://example.org/a/long/path)", "b"], wrapped.Lines);
        Assert.False(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that atoms that are all wider than the width get one line each.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_OnlyWideAtoms_GetOneLineEach()
    {
        // Act: wrap three atoms of ten columns into five columns
        var wrapped = Wrap("aaaaaaaaaa bbbbbbbbbb cccccccccc", 5);

        // Assert: one atom per line
        Assert.Equal(["aaaaaaaaaa", "bbbbbbbbbb", "cccccccccc"], wrapped.Lines);
        Assert.False(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that the width is counted in display columns: two for East Asian and emoji characters, none for combining marks.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_WideAndCombiningCharacters_AreCountedInColumns()
    {
        // Act: wrap CJK words of four columns into nine and into eight columns
        var nine = Wrap("日本 日本 日本", 9);
        var eight = Wrap("日本 日本 日本", 8);

        // Act: wrap emoji (two columns each) and combining marks (zero columns)
        var emoji = Wrap("😀😀 😀😀 x", 9);
        var combining = LineWrapper.Wrap(["e\u0301e\u0301e\u0301", "x", "e\u0301"], 7, 7);

        // Assert: columns decide
        Assert.Equal(["日本 日本", "日本"], nine.Lines);
        Assert.Equal(["日本", "日本", "日本"], eight.Lines);
        Assert.Equal(["😀😀 😀😀", "x"], emoji.Lines);
        Assert.Equal(["e\u0301e\u0301e\u0301 x e\u0301"], combining.Lines);
        Assert.Equal(7, TextWidth.Of(combining.Lines[0]));
    }

    /// <summary>
    ///     Test that a break before an atom that would start block syntax is moved to the atom before it.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_BreakBeforeGuardedAtom_MovesEarlier()
    {
        // Act: wrap where the natural break is before a lone dash
        var wrapped = Wrap("ant bat - cat", 8);

        // Assert: the dash is not at the start of a line
        Assert.Equal(["ant", "bat -", "cat"], wrapped.Lines);
        Assert.False(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that two guarded atoms in a row are both kept off the start of a line when no earlier break fits.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_GuardedAtomsInARow_KeepTheLineAndReportTheOverflow()
    {
        // Act: wrap where the natural break is before the second of two dashes
        var wrapped = Wrap("ant - - bat", 5);

        // Assert: neither dash starts a line
        Assert.Equal(["ant - -", "bat"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a number followed by a period, the plus sign and the asterisk never start a line.
    /// </summary>
    [Theory]
    [InlineData("in the year 2026. was good", 15, "in the|year 2026. was|good")]
    [InlineData("one two three + four", 13, "one two|three + four")]
    [InlineData("one two three * four", 13, "one two|three * four")]
    [InlineData("one two three # four", 13, "one two|three # four")]
    [InlineData("one two three > four", 13, "one two|three > four")]
    public void LineWrapper_Wrap_BlockSyntaxAtTheBreak_IsKeptOffTheLineStart(string atoms, int width, string expected)
    {
        // Act: wrap
        var wrapped = Wrap(atoms, width);

        // Assert: the expected lines, and no line starts with the guarded atom
        Assert.Equal(expected, string.Join('|', wrapped.Lines));
        Assert.All(wrapped.Lines, line => Assert.False(BlockSyntaxGuard.IsGuardedLineStart(line.Split(' ')[0])));
    }

    /// <summary>
    ///     Test that an atom that cannot be moved from the start of a line without breaking the width stays on the previous line over width and is reported.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_NoBreakAvailable_KeepsTheAtomOverWidthAndReportsIt()
    {
        // Act: wrap where a lone dash follows the only atom of the line
        var wrapped = Wrap("ant - bat", 4);

        // Assert: the dash stays on the first line, which is too wide, and the rest follows
        Assert.Equal(["ant -", "bat"], wrapped.Lines);
        Assert.True(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that the first line has its own width.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_FirstWidthSmallerThanTheRest_AppliesToTheFirstLineOnly()
    {
        // Act: wrap with five columns on the first line and ten on the others
        var wrapped = Wrap("ant bat cat dog", 5, 10);

        // Assert: the first line holds one atom, the others as many as fit
        Assert.Equal(["ant", "bat cat", "dog"], wrapped.Lines);
    }

    /// <summary>
    ///     Wraps atoms given as one string separated by blanks in a wrap mode.
    /// </summary>
    /// <param name="atoms">The atoms separated by single spaces.</param>
    /// <param name="mode">The wrap mode.</param>
    /// <param name="firstWidth">The width of the first line.</param>
    /// <param name="width">The width of the other lines; the first width when omitted.</param>
    /// <returns>The wrapped lines.</returns>
    private static WrappedLines WrapIn(string atoms, WrapMode mode, int firstWidth, int? width = null) =>
        LineWrapper.Wrap([.. atoms.Split(' ')], firstWidth, width ?? firstWidth, mode);

    /// <summary>
    ///     Test that the sentence mode puts every sentence on its own line, however short.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SentenceMode_PutsEverySentenceOnItsOwnLine()
    {
        // Act: wrap three short sentences into a width that would hold them all
        var wrapped = WrapIn("One two. Three four. Five six", WrapMode.Sentence, 100);

        // Assert: one line each
        Assert.Equal(["One two.", "Three four.", "Five six"], wrapped.Lines);
        Assert.False(wrapped.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that the sentence mode fills the lines of a sentence that is too wide, with the continuation width after the first line.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SentenceModeTooWideSentence_IsFilled()
    {
        // Act: wrap a sentence of 27 columns into 11 and 12 columns, then a short one
        var wrapped = WrapIn("ant bat cat dog eel fox gnu. Hen ink", WrapMode.Sentence, 11, 12);

        // Assert: the first sentence is filled and the second starts a new line
        Assert.Equal(["ant bat cat", "dog eel fox", "gnu.", "Hen ink"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that the semantic mode keeps a sentence that fits on one line, and breaks one that does not at the latest clause start that fits.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticMode_BreaksATooWideSentenceAtTheLatestFittingClause()
    {
        // Act: a sentence that fits with a comma in it, then one that does not
        var wrapped = WrapIn("Yes, it fits. However, this longer sentence does not fit; split it, please", WrapMode.Semantic, 40);

        // Assert: the first sentence stays whole; the second breaks only where it must: before "this" and before "split"
        Assert.Equal(["Yes, it fits.", "However,", "this longer sentence does not fit;", "split it, please"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a sentence without a clause start is filled like the fill mode does.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeWithoutClauseStart_IsFilled()
    {
        // Act: a sentence that does not fit and has no clause punctuation
        var wrapped = WrapIn("ant bat cat dog eel fox gnu", WrapMode.Semantic, 12);

        // Assert: the same lines as the fill
        Assert.Equal(["ant bat cat", "dog eel fox", "gnu"], wrapped.Lines);
        Assert.Equal(Wrap("ant bat cat dog eel fox gnu", 12).Lines, wrapped.Lines);
    }

    /// <summary>
    ///     Test that a clause start whose new line would be too wide is not used, and the line is filled instead.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeClauseBreakBeyondTheWidth_IsFilled()
    {
        // Act: breaking at the comma would give a line of 25 columns for a width of 20
        var wrapped = WrapIn("aaaa, bbbbbbbbbbbb cccccccccccc", WrapMode.Semantic, 20);

        // Assert: the fill break is used
        Assert.Equal(["aaaa, bbbbbbbbbbbb", "cccccccccccc"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a clause start that the guard refuses is not a place for a break.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeGuardedClauseStart_IsSkipped()
    {
        // Act: the dash after the comma would start a list item
        var wrapped = WrapIn("one two, - three four", WrapMode.Semantic, 12);

        // Assert: the break is made before "three" instead
        Assert.Equal(["one two, -", "three four"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a clause break is measured in display columns, so wide characters count two.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeWideCharacters_AreMeasuredInColumns()
    {
        // Act: the atoms are 5 and 4 columns wide in a width of 10
        var wrapped = WrapIn("日本, 日本 日本 日本", WrapMode.Semantic, 10);

        // Assert: the line breaks at the clause start rather than after the second atom
        Assert.Equal(["日本,", "日本 日本", "日本"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that an atom with a comma inside is never a clause break, and is never split.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeAtomWithInnerComma_IsKeptWhole()
    {
        // Act: a code span atom contains a comma and a space
        var wrapped = LineWrapper.Wrap(["see", "`x, y`", "and", "more", "text,", "end"], 14, 14, WrapMode.Semantic);

        // Assert: the code span is on one line and the break is the fill break before "more"
        Assert.Equal(["see `x, y` and", "more text, end"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a sentence start that the guard refuses stays on the line before it.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SentenceStartThatIsBlockSyntax_StaysOnTheLineBefore()
    {
        // Act: the second sentence would start with a number marker
        var wrapped = WrapIn("Done. 12. Then more. Next", WrapMode.Sentence, 100);

        // Assert: "12." is not a place to break, so the lines break only where the next atom may start a line
        Assert.Equal(["Done. 12. Then more.", "Next"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that an unlimited width writes one sentence per line in both modes.
    /// </summary>
    [Theory]
    [InlineData("Sentence")]
    [InlineData("Semantic")]
    public void LineWrapper_Wrap_UnlimitedWidth_WritesOneSentencePerLine(string modeName)
    {
        var mode = Enum.Parse<WrapMode>(modeName);
        // Act: wrap with a width that no line reaches
        var wrapped = WrapIn("One, two. Three, four. Five", mode, int.MaxValue / 2);

        // Assert: one sentence per line
        Assert.Equal(["One, two.", "Three, four.", "Five"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that wrapping the lines that a mode wrote gives the same lines, for both modes and several widths.
    /// </summary>
    [Theory]
    [InlineData("Sentence", 10)]
    [InlineData("Sentence", 30)]
    [InlineData("Semantic", 10)]
    [InlineData("Semantic", 20)]
    [InlineData("Semantic", 30)]
    [InlineData("Semantic", 40)]
    [InlineData("Semantic", 80)]
    public void LineWrapper_Wrap_ModesAreIdempotent(string modeName, int width)
    {
        var mode = Enum.Parse<WrapMode>(modeName);
        // Arrange: a text with sentences, clauses, abbreviations and block syntax at possible breaks
        const string text = "Dr. Smith, who e.g. wrote this, said: stop! Then 12. went; away. - Next is 2026. Done? Yes, really long clauses follow, again and again; stop";
        var first = WrapIn(text, mode, width, width - 2);

        // Act: wrap the words of the result again
        var again = WrapIn(string.Join(' ', first.Lines), mode, width, width - 2);

        // Assert: the same lines
        Assert.Equal(first.Lines, again.Lines);
    }
}

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
