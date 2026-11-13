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

using System.Globalization;
using System.Text;

namespace DemaConsulting.LintMark.Utilities;

/// <summary>
///     Measures how many terminal columns a text occupies.
/// </summary>
/// <remarks>
///     <para>
///         Line wrapping and table layout must agree with what a reader sees, and the number of UTF-16 code units
///         is a poor measure: East Asian characters and emoji take two columns, and combining marks, joiners and
///         variation selectors take none. The text is therefore cut into grapheme clusters (what a reader perceives
///         as one character, with <see cref="StringInfo"/>) and each cluster is measured as a unit:
///     </para>
///     <list type="bullet">
///         <item>a cluster made only of control, format or combining characters has width 0 (this includes tab,
///         which has no fixed width without a tab stop, line breaks, zero-width spaces and joiners);</item>
///         <item>a cluster whose first visible character is East Asian wide or full-width, or an emoji, has width 2;</item>
///         <item>a cluster that carries the emoji presentation selector (U+FE0F) has width 2, which makes
///         emoji sequences joined with the zero-width joiner and flags one wide character;</item>
///         <item>every other cluster has width 1.</item>
///     </list>
///     <para>
///         The width of a cluster is that of its first visible character, so combining marks and joiners never
///         add to it. The table of wide characters follows the East Asian Width property of Unicode; characters
///         whose width is ambiguous (such as Greek and Cyrillic letters) are measured as narrow, the choice
///         western terminals make. A lone surrogate counts as one narrow character.
///     </para>
///     <para>
///         Thread safety: static and stateless over immutable tables; safe for concurrent use.
///     </para>
/// </remarks>
internal static class TextWidth
{
    /// <summary>
    ///     The code point of the variation selector that requests emoji presentation.
    /// </summary>
    private const int EmojiPresentationSelector = 0xFE0F;

    /// <summary>
    ///     The inclusive code point ranges of East Asian wide and full-width characters and of emoji that
    ///     terminals draw two columns wide, sorted by start.
    /// </summary>
    /// <remarks>
    ///     The ranges are the Wide (W) and full-width (F) values of the EastAsianWidth property of Unicode 18.0.0,
    ///     with adjacent ranges merged, plus the regional indicator letters U+1F1E6 to U+1F1FF (which the property
    ///     keeps neutral but terminals draw wide, as flags). Code points that the property leaves out, such as the
    ///     unassigned ones between ranges, are narrow. A character that is a combining mark (such as U+3099) is
    ///     inside a range but has width 0 because <see cref="RuneWidth"/> tests the category first.
    /// </remarks>
    private static readonly (int First, int Last)[] WideRanges =
    [
        (0x1100, 0x115F), (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0), (0x23F3, 0x23F3),
        (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2630, 0x2637), (0x2648, 0x2653), (0x267F, 0x267F), (0x268A, 0x268F),
        (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5), (0x26CE, 0x26CE),
        (0x26D4, 0x26D4), (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5), (0x26FA, 0x26FA), (0x26FD, 0x26FD),
        (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728), (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755),
        (0x2757, 0x2757), (0x2795, 0x2797), (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C), (0x2B50, 0x2B50),
        (0x2B55, 0x2B55), (0x2E80, 0x2E99), (0x2E9B, 0x2EF3), (0x2F00, 0x2FD5), (0x2FF0, 0x303E), (0x3041, 0x3096),
        (0x3099, 0x30FF), (0x3105, 0x312F), (0x3131, 0x318E), (0x3190, 0x31E5), (0x31EF, 0x321E), (0x3220, 0x3247),
        (0x3250, 0xA48C), (0xA490, 0xA4C6), (0xA960, 0xA97C), (0xAC00, 0xD7A3), (0xF900, 0xFAFF), (0xFE10, 0xFE19),
        (0xFE30, 0xFE52), (0xFE54, 0xFE66), (0xFE68, 0xFE6B), (0xFF01, 0xFF60), (0xFFE0, 0xFFE6),
        (0x16FE0, 0x16FE4), (0x16FF0, 0x16FF6), (0x17000, 0x18CDA), (0x18CFF, 0x18D20), (0x18D80, 0x18DF2),
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
    ///     Measures the display width of a text.
    /// </summary>
    /// <param name="text">The text to measure. Must not be null; may be empty.</param>
    /// <returns>The number of terminal columns the text occupies; zero for an empty text.</returns>
    /// <remarks>
    ///     The text is measured as one line: a line break or a tab has width zero, so a caller that measures
    ///     wrapped lines passes one line at a time and accounts for tabs itself.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static int Of(string text)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);

        // Measure one grapheme cluster at a time
        var width = 0;
        var position = 0;
        while (position < text.Length)
        {
            var length = StringInfo.GetNextTextElementLength(text.AsSpan(position));
            width += ClusterWidth(text.AsSpan(position, length));
            position += length;
        }

        return width;
    }

    /// <summary>
    ///     Measures one grapheme cluster.
    /// </summary>
    /// <param name="cluster">The characters of the cluster.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int ClusterWidth(ReadOnlySpan<char> cluster)
    {
        // The width is that of the first visible character; the emoji presentation selector widens any cluster
        var width = 0;
        var emojiPresentation = false;
        var remaining = cluster;
        while (!remaining.IsEmpty)
        {
            Rune.DecodeFromUtf16(remaining, out var rune, out var consumed);
            emojiPresentation |= rune.Value == EmojiPresentationSelector;
            if (width == 0)
            {
                width = RuneWidth(rune);
            }

            remaining = remaining[consumed..];
        }

        // A cluster with no visible character has no width, whatever its selectors say
        if (width == 0)
        {
            return 0;
        }

        return emojiPresentation ? 2 : width;
    }

    /// <summary>
    ///     Measures one character.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns>0 for control, format and combining characters, 2 for wide characters, otherwise 1.</returns>
    private static int RuneWidth(Rune rune)
    {
        // Control, format (zero-width space and joiners, bidirectional marks) and combining characters (non-spacing,
        // spacing and enclosing marks) are invisible
        switch (Rune.GetUnicodeCategory(rune))
        {
            case UnicodeCategory.Control:
            case UnicodeCategory.Format:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.LineSeparator:
            case UnicodeCategory.ParagraphSeparator:
                return 0;
        }

        // Conjoining Hangul vowels and final consonants (the two blocks of conjoining Hangul letters)
        // only extend a syllable that starts with a leading consonant
        if (rune.Value is (>= 0x1160 and <= 0x11FF) or (>= 0xD7B0 and <= 0xD7FF))
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    /// <summary>
    ///     Determines whether a code point is East Asian wide or full-width, or an emoji drawn wide.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the character takes two columns.</returns>
    private static bool IsWide(int codePoint)
    {
        // Binary search over the sorted ranges
        var low = 0;
        var high = WideRanges.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var (first, last) = WideRanges[middle];
            if (codePoint < first)
            {
                high = middle - 1;
            }
            else if (codePoint > last)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
