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

using System.Diagnostics;
using System.Globalization;
using DemaConsulting.LintMark.Utilities;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the TextWidth class: measuring the terminal columns a text occupies.
/// </summary>
public class TextWidthTests
{
    /// <summary>
    ///     Test that an empty string has no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_EmptyString_ReturnsZero()
    {
        // Act: measure an empty string
        var width = TextWidth.Of(string.Empty);

        // Assert: no columns
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Test that ASCII text takes one column per character.
    /// </summary>
    [Theory]
    [InlineData("a", 1)]
    [InlineData("abc", 3)]
    [InlineData("Hello, world!", 13)]
    [InlineData(" ", 1)]
    [InlineData("a b", 3)]
    public void TextWidth_Of_AsciiText_ReturnsLength(string text, int expected)
    {
        // Act: measure the text
        var width = TextWidth.Of(text);

        // Assert: one column per character, and the same as the length
        Assert.Equal(expected, width);
        Assert.Equal(text.Length, width);
    }

    /// <summary>
    ///     Test that CJK ideographs take two columns each.
    /// </summary>
    [Fact]
    public void TextWidth_Of_CjkIdeographs_CountsTwoColumnsEach()
    {
        // Act: measure two ideographs and an ideograph from the extension B plane
        var two = TextWidth.Of("漢字");
        var extension = TextWidth.Of("\U00020000");

        // Assert: two columns for each
        Assert.Equal(4, two);
        Assert.Equal(2, extension);
    }

    /// <summary>
    ///     Test that Hiragana and Katakana take two columns each.
    /// </summary>
    [Fact]
    public void TextWidth_Of_HiraganaAndKatakana_CountsTwoColumnsEach()
    {
        // Act: measure Hiragana and Katakana
        var hiragana = TextWidth.Of("ひらがな");
        var katakana = TextWidth.Of("カタカナ");

        // Assert: two columns for each character
        Assert.Equal(8, hiragana);
        Assert.Equal(8, katakana);
    }

    /// <summary>
    ///     Test that Hangul syllables take two columns each.
    /// </summary>
    [Fact]
    public void TextWidth_Of_HangulSyllables_CountsTwoColumnsEach()
    {
        // Act: measure two syllables
        var width = TextWidth.Of("한글");

        // Assert: two columns for each syllable
        Assert.Equal(4, width);
    }

    /// <summary>
    ///     Test that half-width Katakana stays narrow while full-width forms are wide.
    /// </summary>
    [Fact]
    public void TextWidth_Of_NarrowKatakana_CountsOneColumn()
    {
        // Act: measure a half-width Katakana letter
        var width = TextWidth.Of("\uFF71");

        // Assert: one column
        Assert.Equal(1, width);
    }

    /// <summary>
    ///     Test that full-width Latin letters and symbols take two columns each.
    /// </summary>
    [Theory]
    [InlineData("\uFF21", 2)]
    [InlineData("\uFF21\uFF22", 4)]
    [InlineData("\uFFE0", 2)]
    public void TextWidth_Of_WideLatinForms_CountsTwoColumnsEach(string text, int expected)
    {
        // Act: measure the full-width text
        var width = TextWidth.Of(text);

        // Assert: two columns for each character
        Assert.Equal(expected, width);
    }

    /// <summary>
    ///     Test that the ideographic space takes two columns.
    /// </summary>
    [Fact]
    public void TextWidth_Of_IdeographicSpace_ReturnsTwo()
    {
        // Act: measure U+3000
        var width = TextWidth.Of("\u3000");

        // Assert: it is a wide space
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that conjoining Hangul letters are one wide character with their leading consonant.
    /// </summary>
    [Fact]
    public void TextWidth_Of_HangulConjoiningLettersSequence_ReturnsTwoAsOneCluster()
    {
        // Act: measure a leading consonant, a vowel and a final consonant
        var width = TextWidth.Of("\u1100\u1161\u11A8");

        // Assert: one syllable of two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that the boundaries of the conjoining Hangul letter block are measured as the leading consonants are wide
    ///     and the vowels are not.
    /// </summary>
    [Theory]
    [InlineData("\u10FF", 1)]
    [InlineData("\u1100", 2)]
    [InlineData("\u115F", 2)]
    [InlineData("\u1160", 0)]
    public void TextWidth_Of_HangulLetterBoundaries_MeasuresEachRangeEdge(string text, int expected)
    {
        // Act: measure a character at the edge of a range
        var width = TextWidth.Of(text);

        // Assert: the width of its range, also when a letter follows
        Assert.Equal(expected, width);
        Assert.Equal(expected + 1, TextWidth.Of(text + "a"));
    }

    /// <summary>
    ///     Test that an emoji outside the basic plane takes two columns.
    /// </summary>
    [Fact]
    public void TextWidth_Of_EmojiSurrogatePair_ReturnsTwo()
    {
        // Act: measure U+1F600
        var width = TextWidth.Of("\U0001F600");

        // Assert: one wide character, not two code units
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that an emoji that is wide by default in the basic plane takes two columns.
    /// </summary>
    [Fact]
    public void TextWidth_Of_DefaultEmojiInBasicPlane_ReturnsTwo()
    {
        // Act: measure the white heavy check mark
        var width = TextWidth.Of("\u2705");

        // Assert: it is drawn wide
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that an emoji sequence joined with the zero-width joiner is one wide character.
    /// </summary>
    [Fact]
    public void TextWidth_Of_EmojiZwjFamilySequence_ReturnsTwoAsOneCluster()
    {
        // Act: measure man, joiner, woman, joiner, girl
        var width = TextWidth.Of("\U0001F468\u200D\U0001F469\u200D\U0001F467");

        // Assert: one cluster of two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that an emoji with a skin tone modifier is one wide character.
    /// </summary>
    [Fact]
    public void TextWidth_Of_EmojiWithSkinToneModifier_ReturnsTwoAsOneCluster()
    {
        // Act: measure thumbs up with a medium skin tone
        var width = TextWidth.Of("\U0001F44D\U0001F3FD");

        // Assert: one cluster of two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that a flag made of two regional indicators is one wide character.
    /// </summary>
    [Fact]
    public void TextWidth_Of_FlagOfTwoRegionalIndicators_ReturnsTwo()
    {
        // Act: measure regional indicators U and S
        var width = TextWidth.Of("\U0001F1FA\U0001F1F8");

        // Assert: one flag of two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that a key cap sequence takes two columns.
    /// </summary>
    [Fact]
    public void TextWidth_Of_KeyCapSequence_ReturnsTwo()
    {
        // Act: measure the digit one, the emoji presentation selector and the combining enclosing key cap
        var width = TextWidth.Of("1\uFE0F\u20E3");

        // Assert: one cluster of two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that the emoji presentation selector widens a narrow symbol.
    /// </summary>
    [Fact]
    public void TextWidth_Of_EmojiPresentationSequence_ReturnsTwoWhileSymbolAloneIsOne()
    {
        // Act: measure the heavy black heart with and without the emoji presentation selector
        var emoji = TextWidth.Of("\u2764\uFE0F");
        var symbol = TextWidth.Of("\u2764");

        // Assert: the selector makes it wide
        Assert.Equal(2, emoji);
        Assert.Equal(1, symbol);
    }

    /// <summary>
    ///     Test that the text presentation selector keeps a symbol narrow.
    /// </summary>
    [Fact]
    public void TextWidth_Of_TextPresentationSequence_StaysNarrow()
    {
        // Act: measure the heavy black heart with the text presentation selector
        var width = TextWidth.Of("\u2764\uFE0E");

        // Assert: one column
        Assert.Equal(1, width);
    }

    /// <summary>
    ///     Test that a combining mark adds no width to its base character.
    /// </summary>
    [Fact]
    public void TextWidth_Of_BaseWithCombiningMark_ReturnsOne()
    {
        // Act: measure e followed by a combining acute accent, and a single e acute character
        var combining = TextWidth.Of("e\u0301");
        var composed = TextWidth.Of("\u00E9");

        // Assert: one column either way
        Assert.Equal(1, combining);
        Assert.Equal(1, composed);
    }

    /// <summary>
    ///     Test that a combining mark after a wide character leaves it two columns wide.
    /// </summary>
    [Fact]
    public void TextWidth_Of_WideCharacterWithCombiningMark_ReturnsTwo()
    {
        // Act: measure an ideograph followed by a combining acute accent
        var width = TextWidth.Of("漢\u0301");

        // Assert: the mark adds nothing
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that a combining mark with no base has no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_LoneCombiningMark_ReturnsZero()
    {
        // Act: measure a combining acute accent alone
        var width = TextWidth.Of("\u0301");

        // Assert: it is invisible on its own
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Test that zero-width spaces and joiners have no width.
    /// </summary>
    [Theory]
    [InlineData("\u200B")]
    [InlineData("\u200C")]
    [InlineData("\u200D")]
    [InlineData("\u2060")]
    public void TextWidth_Of_ZeroWidthCharacters_ReturnsZero(string text)
    {
        // Act: measure the zero-width character
        var width = TextWidth.Of(text);

        // Assert: no columns
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Test that the zero-width no-break space (U+FEFF, also the byte order mark) has no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_ZeroWidthNoBreakSpace_ReturnsZero()
    {
        // Act: measure the character, given by its decimal code point
        var width = TextWidth.Of(((char)65279).ToString());

        // Assert: no columns
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Test that a zero-width space between letters adds no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_ZeroWidthSpaceBetweenLetters_CountsLettersOnly()
    {
        // Act: measure two letters around a zero-width space
        var width = TextWidth.Of("a\u200Bb");

        // Assert: two columns
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that a variation selector with no base has no width.
    /// </summary>
    [Theory]
    [InlineData("\uFE0F")]
    [InlineData("\uFE0E")]
    public void TextWidth_Of_VariationSelectorAlone_ReturnsZero(string text)
    {
        // Act: measure the selector alone
        var width = TextWidth.Of(text);

        // Assert: no columns, even for the emoji presentation selector, and a letter after it is still counted
        Assert.Equal(0, width);
        Assert.Equal(1, TextWidth.Of(text + "a"));
    }

    /// <summary>
    ///     Test that control characters have no width.
    /// </summary>
    [Theory]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    [InlineData("\u0000")]
    [InlineData("\u001B")]
    [InlineData("\u007F")]
    [InlineData("\u0085")]
    public void TextWidth_Of_ControlCharacters_ReturnsZero(string text)
    {
        // Act: measure the control characters
        var width = TextWidth.Of(text);

        // Assert: no columns, and a letter before and after them is still counted
        Assert.Equal(0, width);
        Assert.Equal(2, TextWidth.Of("a" + text + "b"));
    }

    /// <summary>
    ///     Test that a tab between letters adds no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_TabBetweenLetters_CountsLettersOnly()
    {
        // Act: measure two letters around a tab
        var width = TextWidth.Of("a\tb");

        // Assert: two columns, the tab has no fixed width
        Assert.Equal(2, width);
    }

    /// <summary>
    ///     Test that the line and paragraph separators have no width.
    /// </summary>
    [Theory]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void TextWidth_Of_LineAndParagraphSeparators_ReturnsZero(string text)
    {
        // Act: measure the separator
        var width = TextWidth.Of(text);

        // Assert: no columns, and a letter before it is still counted
        Assert.Equal(0, width);
        Assert.Equal(1, TextWidth.Of("a" + text));
    }

    /// <summary>
    ///     Test that a soft hyphen has no width.
    /// </summary>
    [Fact]
    public void TextWidth_Of_SoftHyphen_ReturnsZero()
    {
        // Act: measure U+00AD
        var width = TextWidth.Of("\u00AD");

        // Assert: it is an invisible format character
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Test that ambiguous-width letters and symbols are measured as narrow.
    /// </summary>
    [Theory]
    [InlineData("α", 1)]
    [InlineData("я", 1)]
    [InlineData("─", 1)]
    [InlineData("é", 1)]
    public void TextWidth_Of_AmbiguousWidthCharacters_CountsOneColumn(string text, int expected)
    {
        // Act: measure the text
        var width = TextWidth.Of(text);

        // Assert: one column, also when an invisible character follows
        Assert.Equal(expected, width);
        Assert.Equal(expected, TextWidth.Of(text + "\u200B"));
    }

    /// <summary>
    ///     Test that a text of mixed scripts adds the width of each part.
    /// </summary>
    [Theory]
    [InlineData("a漢b", 4)]
    [InlineData("abc漢字def", 10)]
    [InlineData("e\u0301漢\U0001F600", 5)]
    [InlineData("x\u200B\t\n", 1)]
    public void TextWidth_Of_MixedText_AddsEachClusterWidth(string text, int expected)
    {
        // Act: measure the mixed text
        var width = TextWidth.Of(text);

        // Assert: the sum of the widths of its parts, and one more when a letter is appended
        Assert.Equal(expected, width);
        Assert.Equal(expected + 1, TextWidth.Of(text + "z"));
    }

    /// <summary>
    ///     Test that a lone surrogate counts as one narrow character.
    /// </summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void TextWidth_Of_LoneSurrogate_ReturnsOne(int codeUnit)
    {
        // Arrange: an unpaired surrogate (built from its code unit, since a test data string would be repaired)
        var text = ((char)codeUnit).ToString();

        // Act: measure it
        var width = TextWidth.Of(text);

        // Assert: one column, and no exception
        Assert.Equal(1, width);
    }

    /// <summary>
    ///     Test that a very long text is measured quickly and gives the same result every time.
    /// </summary>
    [Fact]
    public void TextWidth_Of_VeryLongString_MeasuresQuicklyAndDeterministically()
    {
        // Arrange: 100000 narrow characters and 100000 wide characters
        var narrow = new string('a', 100000);
        var wide = new string('漢', 50000);
        var stopwatch = Stopwatch.StartNew();

        // Act: measure both twice
        var narrowFirst = TextWidth.Of(narrow);
        var narrowSecond = TextWidth.Of(narrow);
        var wideFirst = TextWidth.Of(wide);
        var wideSecond = TextWidth.Of(wide);
        stopwatch.Stop();

        // Assert: the expected width every time, within a generous time limit
        Assert.Equal(100000, narrowFirst);
        Assert.Equal(narrowFirst, narrowSecond);
        Assert.Equal(100000, wideFirst);
        Assert.Equal(wideFirst, wideSecond);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Measuring took {stopwatch.Elapsed}");
    }

    /// <summary>
    ///     Test that a null text is rejected.
    /// </summary>
    [Fact]
    public void TextWidth_Of_NullText_Throws()
    {
        // Act & Assert: null is rejected and the parameter is named
        var exception = Assert.Throws<ArgumentNullException>(() => TextWidth.Of(null!));
        Assert.Equal("text", exception.ParamName);
    }

    /// <summary>
    ///     Test that the measured width does not depend on the current culture.
    /// </summary>
    [Fact]
    public void TextWidth_Of_TurkishCulture_GivesSameResultAsInvariant()
    {
        // Arrange: text with the letters i and I, which a Turkish culture treats specially
        const string text = "IİıiI漢";
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariant = TextWidth.Of(text);

            // Act: measure the same text under a Turkish culture
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var turkish = TextWidth.Of(text);

            // Assert: the same width in both
            Assert.Equal(7, invariant);
            Assert.Equal(invariant, turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
