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
using DemaConsulting.LintMark.Utilities;

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
    ///     Test that invalid atoms and widths are rejected and named.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_InvalidArguments_Throw()
    {
        // Act & Assert: a default or empty array, a null element and an empty atom are rejected
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => LineWrapper.Wrap(default, 10, 10)).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => LineWrapper.Wrap([], 10, 10)).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentNullException>(() => LineWrapper.Wrap(["a", null!], 10, 10)).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => LineWrapper.Wrap(["a", string.Empty], 10, 10)).ParamName);

        // Act & Assert: widths below one are rejected
        Assert.Equal("firstWidth", Assert.Throws<ArgumentOutOfRangeException>(() => LineWrapper.Wrap(["a"], 0, 10)).ParamName);
        Assert.Equal("continuationWidth", Assert.Throws<ArgumentOutOfRangeException>(() => LineWrapper.Wrap(["a"], 10, 0)).ParamName);
    }

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
    ///     Test that a line never ends or starts with a blank.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_Lines_HaveNoLeadingOrTrailingWhitespace()
    {
        // Act: wrap a text with many words
        var wrapped = Wrap(string.Join(' ', Enumerable.Range(0, 60).Select(i => "w" + i)), 25);

        // Assert: no line has whitespace at its ends
        Assert.All(wrapped.Lines, line => Assert.Equal(line.Trim(), line));
        Assert.All(wrapped.Lines, line => Assert.True(TextWidth.Of(line) <= 25));
    }

    /// <summary>
    ///     Creates the word at an index of the text of the fixed-point test: a few of them are ones that the guard refuses at the start of a line.
    /// </summary>
    /// <param name="index">The index of the word.</param>
    /// <returns>The word.</returns>
    private static string FixedPointWord(int index)
    {
        if (index % 7 == 3)
        {
            return "-";
        }

        return index % 11 == 5 ? "2026." : "word" + index;
    }

    /// <summary>
    ///     Test that wrapping lines that were wrapped this way proposes no change.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(60)]
    public void LineWrapper_Wrap_WrappedLines_AreAFixedPoint(int width)
    {
        // Arrange: words including some that the guard refuses at the start of a line
        var atoms = string.Join(' ', Enumerable.Range(0, 80).Select(FixedPointWord));

        // Act: wrap, split the result into atoms again and wrap again
        var once = Wrap(atoms, width);
        var twice = Wrap(string.Join(' ', once.Lines), width);

        // Assert: the same lines
        Assert.Equal(once.Lines, twice.Lines);
        Assert.Equal(once.HasBreakableOverflow, twice.HasBreakableOverflow);
    }

    /// <summary>
    ///     Test that the result cannot be changed through a cast to a mutable interface.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_Result_IsImmutable()
    {
        // Act: wrap
        var wrapped = Wrap("a b c", 3);

        // Assert: the lines refuse changes
        Assert.True(((IList<string>)wrapped.Lines).IsReadOnly);
        Assert.Throws<ArgumentException>(() => new WrappedLines(default, false));
        Assert.Throws<ArgumentException>(() => new WrappedLines([], false));
        Assert.Throws<ArgumentNullException>(() => new WrappedLines([null!], false));
        Assert.Throws<ArgumentException>(() => new WrappedLines([string.Empty], false));
    }

    /// <summary>
    ///     Test that every thread gets the same lines for the same atoms.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_ConcurrentCalls_GiveTheSameLines()
    {
        // Arrange: atoms and the single-threaded result
        ImmutableArray<string> atoms = [.. Enumerable.Range(0, 100).Select(i => i % 9 == 4 ? "+" : "日本" + i)];
        var reference = LineWrapper.Wrap(atoms, 30, 28);

        // Act & Assert: every thread gets the same lines
        BlockFormatterAssertions.RunConcurrently(
            8,
            _ =>
            {
                for (var round = 0; round < 100; round++)
                {
                    var other = LineWrapper.Wrap(atoms, 30, 28);
                    Assert.Equal(reference.Lines, other.Lines);
                    Assert.Equal(reference.HasBreakableOverflow, other.HasBreakableOverflow);
                }
            });
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
    ///     Test that the fill mode gives the same lines as the overload without a mode, and that an undefined mode is rejected.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_FillModeAndUndefinedMode_AreHandled()
    {
        // Act & Assert: the fill mode is the plain fill and an undefined mode is rejected and named
        Assert.Equal(Wrap("ant bat cat dog", 7).Lines, WrapIn("ant bat cat dog", WrapMode.Fill, 7).Lines);
        Assert.Equal("mode", Assert.Throws<ArgumentOutOfRangeException>(() => LineWrapper.Wrap(["a"], 5, 5, (WrapMode)9)).ParamName);
        Assert.Equal("atoms", Assert.Throws<ArgumentException>(() => LineWrapper.Wrap([], 5, 5, WrapMode.Sentence)).ParamName);
        Assert.Equal("firstWidth", Assert.Throws<ArgumentOutOfRangeException>(() => LineWrapper.Wrap(["a"], 0, 5, WrapMode.Semantic)).ParamName);
    }

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
    ///     Test that the semantic mode keeps a sentence that fits on one line, and splits one that does not at its clauses before it fills.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticMode_SplitsOnlyTooWideSentencesAtClauses()
    {
        // Act: a sentence that fits with a comma in it, then one that does not
        var wrapped = WrapIn("Yes, it fits. However, this longer sentence does not fit; split it, please", WrapMode.Semantic, 40);

        // Assert: the first sentence stays whole, the second is split at its clauses
        Assert.Equal(["Yes, it fits.", "However,", "this longer sentence does not fit;", "split it,", "please"], wrapped.Lines);
    }

    /// <summary>
    ///     Test that a clause that is still too wide is filled.
    /// </summary>
    [Fact]
    public void LineWrapper_Wrap_SemanticModeTooWideClause_IsFilled()
    {
        // Act: a sentence with a clause that does not fit in 12 columns
        var wrapped = WrapIn("ant bat cat dog eel, fox gnu", WrapMode.Semantic, 12);

        // Assert: the first clause is filled, then the second
        Assert.Equal(["ant bat cat", "dog eel,", "fox gnu"], wrapped.Lines);
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
    [InlineData("Semantic", 30)]
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
