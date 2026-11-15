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
///         The <see cref="WrapMode.Sentence"/> and <see cref="WrapMode.Semantic"/> overload lays the atoms out per sentence (see
///         <see cref="SentenceSplitter"/>): every sentence starts a line and one that is too wide is filled as above; the
///         semantic mode splits a sentence that is too wide at its clauses first. Neither depends on how the atoms were laid out
///         before, so wrapping text that was wrapped this way proposes no change.
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
        Validate(atoms, firstWidth, continuationWidth);

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
    ///     Wraps a run of atoms into lines in the layout of a wrap mode.
    /// </summary>
    /// <param name="atoms">The atoms in order. Must not be a default array, empty, or contain null or an empty atom.</param>
    /// <param name="firstWidth">The columns available to the first line. At least 1.</param>
    /// <param name="continuationWidth">The columns available to every other line. At least 1.</param>
    /// <param name="mode">
    ///     <see cref="WrapMode.Fill"/> fills every line up to the width (the same as the overload without a mode);
    ///     <see cref="WrapMode.Sentence"/> starts every sentence on a new line and fills the lines of a sentence that is too
    ///     wide; <see cref="WrapMode.Semantic"/> does the same and splits a sentence that is too wide at its clauses first.
    /// </param>
    /// <returns>The lines and whether a line that holds more than one atom is still wider than its width.</returns>
    /// <remarks>
    ///     A sentence or clause start that the <see cref="BlockSyntaxGuard"/> refuses is not a place for a line break: the
    ///     sentence stays on the line of the one before it. The layout depends on the atoms and the two widths only,
    ///     so the modes are idempotent by construction. Pass a very large width for a line length of 0 (no limit): the
    ///     sentence mode then writes one sentence per line and the semantic mode the same.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array, is empty or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a width is less than 1 or <paramref name="mode"/> is not a defined value.</exception>
    public static WrappedLines Wrap(ImmutableArray<string> atoms, int firstWidth, int continuationWidth, WrapMode mode)
    {
        Validate(atoms, firstWidth, continuationWidth);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined WrapMode value.");
        }

        if (mode == WrapMode.Fill)
        {
            return Wrap(atoms, firstWidth, continuationWidth);
        }

        // Split into sentences, keeping out the starts that the guard refuses
        var widths = atoms.Select(static atom => TextWidth.Of(atom)).ToArray();
        var sentenceStarts = Allowed(atoms, SentenceSplitter.FindSentenceStarts(atoms), 0);
        var lines = new List<string>();
        var overflow = false;
        for (var s = 0; s < sentenceStarts.Count; s++)
        {
            var first = sentenceStarts[s];
            var end = s + 1 < sentenceStarts.Count ? sentenceStarts[s + 1] : atoms.Length;
            var sentence = ImmutableArray.Create(atoms, first, end - first);

            // A sentence that is too wide for its first line is split at its clauses in the semantic mode
            var available = lines.Count == 0 ? firstWidth : continuationWidth;
            if (mode == WrapMode.Semantic && WidthOf(widths, first, end) > available)
            {
                var clauseStarts = Allowed(sentence, SentenceSplitter.FindClauseStarts(sentence), 0);
                for (var c = 0; c < clauseStarts.Count; c++)
                {
                    var clauseEnd = c + 1 < clauseStarts.Count ? clauseStarts[c + 1] : sentence.Length;
                    overflow |= AddRun(lines, ImmutableArray.Create(sentence, clauseStarts[c], clauseEnd - clauseStarts[c]), firstWidth, continuationWidth);
                }
            }
            else
            {
                overflow |= AddRun(lines, sentence, firstWidth, continuationWidth);
            }
        }

        return new WrappedLines([.. lines], overflow);
    }

    /// <summary>
    ///     Validates the arguments of the wrap methods.
    /// </summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="firstWidth">The width of the first line.</param>
    /// <param name="continuationWidth">The width of the other lines.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> is a default array, is empty or holds an empty atom.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atoms"/> holds a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a width is less than 1.</exception>
    private static void Validate(ImmutableArray<string> atoms, int firstWidth, int continuationWidth)
    {
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
    }

    /// <summary>
    ///     Turns the starts that a splitter found into the starts of runs, without the ones the guard refuses.
    /// </summary>
    /// <param name="atoms">The atoms.</param>
    /// <param name="found">The indexes of the atoms that start a new run, as found by the splitter.</param>
    /// <param name="zero">The index where the first run starts.</param>
    /// <returns>The index of the first atom of every run, beginning with <paramref name="zero"/>.</returns>
    private static List<int> Allowed(ImmutableArray<string> atoms, ImmutableArray<int> found, int zero)
    {
        var starts = new List<int> { zero };
        starts.AddRange(found.Where(index => !IsGuarded(atoms, index)));
        return starts;
    }

    /// <summary>
    ///     Fills a run of atoms that starts a new line and adds its lines.
    /// </summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="run">The atoms of the run.</param>
    /// <param name="firstWidth">The width of the first line of the paragraph.</param>
    /// <param name="continuationWidth">The width of the other lines.</param>
    /// <returns>Whether a line with more than one atom is still too wide.</returns>
    private static bool AddRun(List<string> lines, ImmutableArray<string> run, int firstWidth, int continuationWidth)
    {
        var result = Wrap(run, lines.Count == 0 ? firstWidth : continuationWidth, continuationWidth);
        lines.AddRange(result.Lines);
        return result.HasBreakableOverflow;
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
