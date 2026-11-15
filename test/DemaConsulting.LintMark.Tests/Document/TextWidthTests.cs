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
using DemaConsulting.LintMark.Document;

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
    ///     Test that half-width Katakana stays narrow.
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
    ///     Test that the edges of the half-width forms block, which lies between the wide full-width forms, are narrow.
    /// </summary>
    [Theory]
    [InlineData(0xFF61)]
    [InlineData(0xFF9F)]
    [InlineData(0xFFDC)]
    [InlineData(0xFFE8)]
    [InlineData(0xFFEE)]
    public void TextWidth_Of_HalfWidthFormEdges_CountsOneColumn(int codePoint)
    {
        // Act: measure the half-width character
        var text = char.ConvertFromUtf32(codePoint);
        var width = TextWidth.Of(text);

        // Assert: one column, also when a letter follows
        Assert.Equal(1, width);
        Assert.Equal(2, TextWidth.Of(text + "a"));
    }

    /// <summary>
    ///     The inclusive code point ranges that the test expects to be two columns wide: the East Asian Wide and
    ///     full-width ranges of Unicode 18.0.0 (merged where adjacent) and the regional indicators. The code points
    ///     just before and after each range are expected not to be wide. A range edge that is a combining mark
    ///     (U+3099, U+16FE4, and the spacing combining marks U+16FF0 and U+16FF1) is moved inward, because a
    ///     combining mark has no width of its own.
    /// </summary>
    private static readonly (int First, int Last)[] ExpectedWideRanges =
    [
        (0x1100, 0x115F), (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0), (0x23F3, 0x23F3),
        (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2630, 0x2637), (0x2648, 0x2653), (0x267F, 0x267F), (0x268A, 0x268F),
        (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5), (0x26CE, 0x26CE),
        (0x26D4, 0x26D4), (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5), (0x26FA, 0x26FA), (0x26FD, 0x26FD),
        (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728), (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755),
        (0x2757, 0x2757), (0x2795, 0x2797), (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C), (0x2B50, 0x2B50),
        (0x2B55, 0x2B55), (0x2E80, 0x2E99), (0x2E9B, 0x2EF3), (0x2F00, 0x2FD5), (0x2FF0, 0x303E), (0x3041, 0x3096),
        (0x309B, 0x30FF), (0x3105, 0x312F), (0x3131, 0x318E), (0x3190, 0x31E5), (0x31EF, 0x321E), (0x3220, 0x3247),
        (0x3250, 0xA48C), (0xA490, 0xA4C6), (0xA960, 0xA97C), (0xAC00, 0xD7A3), (0xF900, 0xFAFF), (0xFE10, 0xFE19),
        (0xFE30, 0xFE52), (0xFE54, 0xFE66), (0xFE68, 0xFE6B), (0xFF01, 0xFF60), (0xFFE0, 0xFFE6),
        (0x16FE0, 0x16FE3), (0x16FF2, 0x16FF6), (0x17000, 0x18CDA), (0x18CFF, 0x18D20), (0x18D80, 0x18DF2),
        (0x18E00, 0x19191), (0x191A0, 0x191D2), (0x1AFF0, 0x1AFF3), (0x1AFF5, 0x1AFFB), (0x1AFFD, 0x1AFFE),
        (0x1B000, 0x1B128), (0x1B132, 0x1B132), (0x1B150, 0x1B152), (0x1B155, 0x1B155), (0x1B164, 0x1B168),
        (0x1B170, 0x1B2FB), (0x1D300, 0x1D356), (0x1D360, 0x1D376), (0x1F004, 0x1F004), (0x1F0CF, 0x1F0CF),
        (0x1F18E, 0x1F18E), (0x1F191, 0x1F19A), (0x1F1AE, 0x1F1AE), (0x1F1E6, 0x1F202), (0x1F210, 0x1F23B),
        (0x1F240, 0x1F248), (0x1F250, 0x1F251), (0x1F260, 0x1F265), (0x1F300, 0x1F320), (0x1F32D, 0x1F335),
        (0x1F337, 0x1F37C), (0x1F37E, 0x1F393), (0x1F3A0, 0x1F3CA), (0x1F3CF, 0x1F3D3), (0x1F3E0, 0x1F3F0),
        (0x1F3F4, 0x1F3F4), (0x1F3F8, 0x1F43E), (0x1F440, 0x1F440), (0x1F442, 0x1F4FC), (0x1F4FF, 0x1F53D),
        (0x1F54B, 0x1F54E), (0x1F550, 0x1F567), (0x1F57A, 0x1F57A), (0x1F595, 0x1F596), (0x1F5A4, 0x1F5A4),
        (0x1F5FB, 0x1F64F), (0x1F680, 0x1F6C5), (0x1F6CC, 0x1F6CC), (0x1F6D0, 0x1F6D2), (0x1F6D5, 0x1F6D9),
        (0x1F6DC, 0x1F6DF), (0x1F6EB, 0x1F6EC), (0x1F6F4, 0x1F6FC), (0x1F7DA, 0x1F7DA), (0x1F7E0, 0x1F7EB),
        (0x1F7F0, 0x1F7F0), (0x1F90C, 0x1F93A), (0x1F93C, 0x1F945), (0x1F947, 0x1F9FF), (0x1FA70, 0x1FA7C),
        (0x1FA80, 0x1FAC6), (0x1FAC8, 0x1FAC8), (0x1FACC, 0x1FADD), (0x1FADF, 0x1FAEB), (0x1FAEF, 0x1FAFA),
        (0x20000, 0x2FFFD), (0x30000, 0x3FFFD)
    ];

    /// <summary>
    ///     Supplies the first and last code point of each range that is two columns wide.
    /// </summary>
    /// <returns>The rows of first and last code point.</returns>
    public static TheoryData<int, int> WideRangeEdges()
    {
        var data = new TheoryData<int, int>();
        foreach (var (first, last) in ExpectedWideRanges)
        {
            data.Add(first, last);
        }

        return data;
    }

    /// <summary>
    ///     Test that a combining kana voiced mark, which lies inside a wide range, has no width on its own and does
    ///     not widen the kana it follows.
    /// </summary>
    [Fact]
    public void TextWidth_Of_CombiningMarkInsideWideRange_ReturnsZeroAlone()
    {
        // Act: measure the combining voiced sound mark alone and after a Hiragana letter
        var alone = TextWidth.Of("\u3099");
        var combined = TextWidth.Of("\u304B\u3099");

        // Assert: no width of its own, and the letter keeps two columns
        Assert.Equal(0, alone);
        Assert.Equal(2, combined);
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
    ///     Test that the conjoining Hangul vowels and final consonants of the extended block of conjoining Hangul
    ///     letters take no width of their own, as those of the first block, and extend the leading consonant before them.
    /// </summary>
    [Theory]
    [InlineData("\uD7A3", 2)]
    [InlineData("\uD7A4", 1)]
    [InlineData("\uD7AF", 1)]
    [InlineData("\uD7B0", 0)]
    [InlineData("\uD7C6", 0)]
    [InlineData("\uD7FB", 0)]
    [InlineData("\uD7FF", 0)]
    public void TextWidth_Of_HangulExtendedBLetterBoundaries_MeasuresEachRangeEdge(string text, int expected)
    {
        // Act: measure a character at the edge of a range, alone and followed by a letter
        var width = TextWidth.Of(text);
        var widthWithLetter = TextWidth.Of(text + "a");

        // Assert: the width of its range, also when a letter follows
        Assert.Equal(expected, width);
        Assert.Equal(expected + 1, widthWithLetter);
    }

    /// <summary>
    ///     Test that a leading consonant followed by an extended vowel and final consonant is one wide character.
    /// </summary>
    [Fact]
    public void TextWidth_Of_HangulExtendedBLettersAfterLeadingConsonant_ReturnsTwo()
    {
        // Act: measure a leading consonant, an extended vowel and an extended final consonant
        var width = TextWidth.Of("\u1100\uD7B0\uD7CB");

        // Assert: one syllable of two columns
        Assert.Equal(2, width);
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
    ///     Test that a cluster or text that starts with a spacing combining mark is measured by its first visible character.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="expected">The expected width.</param>
    [Theory]
    [InlineData("\u0903\u0301", 0)]
    [InlineData("\u0903A", 1)]
    [InlineData("\u093E\u0915", 1)]
    [InlineData("\u0903\u6F22", 2)]
    public void TextWidth_Of_SpacingCombiningMarkAtStart_UsesFirstVisibleCharacter(string text, int expected)
    {
        // Act: measure the text alone and followed by a narrow letter
        var width = TextWidth.Of(text);
        var withLetter = TextWidth.Of(text + "x");

        // Assert: the leading mark itself adds no column, and the next cluster is counted as usual
        Assert.Equal(expected, width);
        Assert.Equal(expected + 1, withLetter);
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

}
