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
using DemaConsulting.LintMark.Utilities;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Fills lines with atoms: puts as many atoms on a line as fit in the width, never starts a line with block syntax, and never splits an atom.
/// </summary>
/// <remarks>
///     <para>
///         The wrap is a greedy fill on display columns (see <see cref="TextWidth"/>): atoms are joined with one space
///         and a new line is started before the atom that does not fit. An atom that is wider than the width (a long
///         link or URL) is never cut: it stands alone on its line and that line is accepted over width. A break
///         before an atom that the <see cref="BlockSyntaxGuard"/> refuses is moved earlier, to the nearest atom that
///         may start a line, when the atoms from there on still fit; when no such break exists the atom stays on the
///         line it was on, over width, and the result reports that a line that could be broken is still too wide.
///     </para>
///     <para>
///         The result depends on the atoms and the two widths only (not on how the atoms were laid out before), so
///         wrapping text that was wrapped this way proposes no change. Trailing whitespace is never produced.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use. Arguments are not modified.
///     </para>
/// </remarks>
internal static class LineWrapper
{
    /// <summary>
    ///     Wraps a run of atoms into lines.
    /// </summary>
    /// <param name="atoms">The atoms in order. Must not be a default array, empty, or contain null or an empty atom.</param>
    /// <param name="firstWidth">The columns available to the first line (the width minus the prefix of the first line). At least 1.</param>
    /// <param name="continuationWidth">The columns available to every other line (the width minus the continuation prefix). At least 1.</param>
    /// <returns>The lines and whether a line that holds more than one atom is still wider than its width.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array, is empty or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a width is less than 1.</exception>
    public static WrappedLines Wrap(ImmutableArray<string> atoms, int firstWidth, int continuationWidth)
    {
        // Validate input before any measurement
        if (atoms.IsDefault || atoms.IsEmpty)
        {
            throw new ArgumentException("The atoms must not be a default or empty array.", nameof(atoms));
        }

        if (atoms.Any(static atom => atom is null))
        {
            throw new ArgumentNullException(nameof(atoms), "The atoms must not contain null.");
        }

        if (atoms.Any(static atom => atom.Length == 0))
        {
            throw new ArgumentException("The atoms must not contain an empty atom.", nameof(atoms));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(firstWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(continuationWidth, 1);

        // Measure every atom once; the width of a line is the widths of its atoms plus one column for each space between them
        var widths = new int[atoms.Length];
        for (var i = 0; i < atoms.Length; i++)
        {
            widths[i] = TextWidth.Of(atoms[i]);
        }

        // Fill: each line is the range of atoms [start, end) of its first and one past its last atom
        var lines = new List<(int Start, int End)>();
        var start = 0;
        var lineWidth = widths[0];
        for (var i = 1; i < atoms.Length; i++)
        {
            var available = lines.Count == 0 ? firstWidth : continuationWidth;
            if (lineWidth + 1 + widths[i] <= available)
            {
                lineWidth += 1 + widths[i];
                continue;
            }

            // A break is needed before atom i; the guard may refuse it
            var breakAt = IsGuarded(atoms, i) ? FindEarlierBreak(atoms, widths, start, i, continuationWidth) : i;
            if (breakAt < 0)
            {
                // No better break exists: the atom stays on the line, over width
                lineWidth += 1 + widths[i];
                continue;
            }

            lines.Add((start, breakAt));
            start = breakAt;
            lineWidth = WidthOf(widths, start, i + 1);
        }

        lines.Add((start, atoms.Length));

        // Build the lines and find out whether a line with more than one atom is still over its width
        var built = ImmutableArray.CreateBuilder<string>(lines.Count);
        var overflow = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var (first, end) = lines[i];
            built.Add(string.Join(' ', atoms.Skip(first).Take(end - first)));
            overflow |= end - first > 1 && WidthOf(widths, first, end) > (i == 0 ? firstWidth : continuationWidth);
        }

        return new WrappedLines(built.MoveToImmutable(), overflow);
    }

    /// <summary>
    ///     Determines whether the atom at an index must not start a line.
    /// </summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="index">The index of the atom.</param>
    /// <returns><see langword="true"/> when the guard refuses the atom as the start of a line.</returns>
    private static bool IsGuarded(ImmutableArray<string> atoms, int index) => BlockSyntaxGuard.IsGuardedLineStart(atoms[index]);

    /// <summary>
    ///     Finds the nearest break before an atom that may start a line, so that the guarded atom is not at the start of its line.
    /// </summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="widths">The widths of the atoms.</param>
    /// <param name="start">The index of the first atom of the current line.</param>
    /// <param name="guarded">The index of the guarded atom that does not fit; it is not yet on the line.</param>
    /// <param name="width">The width available to the line that starts at the break.</param>
    /// <returns>
    ///     The index of the atom that starts the new line (greater than <paramref name="start"/>, not greater than
    ///     <paramref name="guarded"/>) when the atoms from there up to and including the guarded atom fit in
    ///     <paramref name="width"/>; otherwise -1.
    /// </returns>
    private static int FindEarlierBreak(ImmutableArray<string> atoms, int[] widths, int start, int guarded, int width)
    {
        // Walk back to the nearest atom that may start a line; moving further back only makes the new line wider
        for (var candidate = guarded - 1; candidate > start; candidate--)
        {
            if (!IsGuarded(atoms, candidate))
            {
                return WidthOf(widths, candidate, guarded + 1) <= width ? candidate : -1;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Computes the width of a run of atoms joined with single spaces.
    /// </summary>
    /// <param name="widths">The widths of the atoms.</param>
    /// <param name="first">Index of the first atom of the run.</param>
    /// <param name="end">One past the index of the last atom of the run.</param>
    /// <returns>The sum of the widths plus one column for each space between two atoms.</returns>
    private static int WidthOf(int[] widths, int first, int end)
    {
        var total = end - first - 1;
        for (var i = first; i < end; i++)
        {
            total += widths[i];
        }

        return total;
    }
}
