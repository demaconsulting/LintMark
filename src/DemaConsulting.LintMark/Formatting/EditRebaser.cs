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
using System.Text;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Composes two lists of edits, where the second list was computed on the text that the first list produces, into one
///     list against the original text, and maps offsets of the produced text back to the original.
/// </summary>
/// <remarks>
///     <para>
///         Paragraphs are wrapped on the text that the other rules produce (so that the wrap sees the final prefixes,
///         delimiters and spacing), but the edits of a document are always expressed against the original text. The
///         rebaser closes that gap: for edits <c>A</c> against the text <c>T0</c> and edits <c>B</c> against <c>T1 =
///         Apply(T0, A)</c>, <see cref="Rebase"/> returns the edits <c>C</c> against <c>T0</c> with
///         <c>Apply(T0, C) == Apply(T1, B)</c>.
///     </para>
///     <para>
///         An edit of <c>B</c> that starts or ends strictly inside text that an edit of <c>A</c> wrote is extended to cover
///         all of that text (the extension is the text <c>A</c> wrote, which is known without the original), and every edit of
///         <c>A</c> whose text the extended edit replaces is dropped, because the replacement already contains its effect.
///         Edits of <c>B</c> whose extended ranges overlap are merged into one. An edit of <c>A</c> that only touches
///         the boundary of an edit of <c>B</c> (a deletion at the boundary, or text written just before or after it) stays;
///         an insertion of <c>B</c> that touches an insertion of <c>A</c> is merged with it, because the order of two
///         insertions at one offset is not defined. An edit that replaces nothing with nothing is dropped. The result is built
///         through <see cref="EditSet"/>, so a conflict cannot go unnoticed.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use. The arguments are not modified.
///     </para>
/// </remarks>
internal static class EditRebaser
{
    /// <summary>
    ///     Composes two lists of edits into one list against the original text.
    /// </summary>
    /// <param name="first">The edits against the original text, ordered and without conflicts.</param>
    /// <param name="second">The edits against the text that <paramref name="first"/> produces, ordered and without conflicts.</param>
    /// <returns>The edits against the original text, ordered by start offset and then length.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the composition produces conflicting edits (a defect of the caller's edits).</exception>
    public static ImmutableArray<TextEdit> Rebase(ImmutableArray<TextEdit> first, ImmutableArray<TextEdit> second)
    {
        // An edit that replaces nothing with nothing changes nothing and only gets in the way of the translation
        var changes = first.RemoveAll(IsNoOp);
        var followUps = second.RemoveAll(IsNoOp);

        // Edits against the same text need no translation
        if (followUps.IsEmpty)
        {
            return changes;
        }

        if (changes.IsEmpty)
        {
            return followUps;
        }

        // Extend each edit of the second list to whole outputs of the first, and merge the ones that overlap
        var outputs = new Outputs(changes);
        var groups = BuildGroups(outputs, followUps);

        // Drop the edits of the first list whose output a group replaces, and translate the groups to the original text
        var result = new EditSet();
        var absorbed = new bool[changes.Length];
        foreach (var group in groups)
        {
            outputs.MarkInside(absorbed, group.Start, group.End);
        }

        for (var k = 0; k < changes.Length; k++)
        {
            if (!absorbed[k])
            {
                result.Add(changes[k]);
            }
        }

        foreach (var group in groups)
        {
            var start = outputs.ToOriginalStart(group.Start);
            var end = group.Start == group.End ? start : outputs.ToOriginalEnd(group.End);
            result.Add(new TextEdit(start, end - start, group.Replacement, group.RuleId));
        }

        return result.ToImmutable();
    }

    /// <summary>
    ///     Creates a function that maps offsets of the text that a list of edits produces back to offsets of the original text.
    /// </summary>
    /// <param name="first">The edits against the original text, ordered and without conflicts.</param>
    /// <returns>
    ///     A function from an offset in the produced text to the offset of the same character in the original text when the
    ///     character was not written by an edit; for an offset inside text that an edit wrote, the start of the range that edit replaced.
    /// </returns>
    /// <remarks>
    ///     The arithmetic is prepared once, so that mapping many offsets costs a binary search each.
    /// </remarks>
    public static Func<int, int> CreateOffsetMapper(ImmutableArray<TextEdit> first)
    {
        var outputs = new Outputs(first);
        return offset =>
        {
            var inside = outputs.FindStrictlyInside(offset);
            return inside >= 0 ? first[inside].Start : outputs.ToOriginalStart(offset);
        };
    }

    /// <summary>
    ///     Determines whether an edit replaces nothing with nothing.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <returns><see langword="true"/> when the edit has no length and no replacement.</returns>
    private static bool IsNoOp(TextEdit edit) => edit.Length == 0 && edit.Replacement.Length == 0;

    /// <summary>
    ///     Extends the edits of the second list to whole outputs of the first list and merges those that overlap.
    /// </summary>
    /// <param name="outputs">The outputs of the first list.</param>
    /// <param name="second">The edits of the second list.</param>
    /// <returns>The groups in order; each is a range of the produced text and the text that replaces it.</returns>
    private static List<Group> BuildGroups(Outputs outputs, ImmutableArray<TextEdit> second)
    {
        var groups = new List<Group>();
        var open = false;
        var groupStart = 0;
        var groupEnd = 0;
        var lastEnd = 0;
        var ruleId = string.Empty;
        var text = new StringBuilder();
        foreach (var edit in second)
        {
            // A start or end strictly inside text that the first list wrote is moved out to the ends of that text
            var (startOutput, endOutput) = outputs.FindExtensions(edit);
            var start = startOutput >= 0 ? outputs.OutputStart(startOutput) : edit.Start;
            var end = endOutput >= 0 ? outputs.OutputEnd(endOutput) : edit.End;

            // Edits whose extended ranges overlap are one edit; the text between them was written by the first list
            if (open && start < groupEnd)
            {
                text.Append(outputs.TextBetween(lastEnd, edit.Start)).Append(edit.Replacement);
                groupEnd = Math.Max(groupEnd, end);
            }
            else
            {
                if (open)
                {
                    groups.Add(new Group(groupStart, groupEnd, text.ToString() + outputs.TextBetween(lastEnd, groupEnd), ruleId));
                }

                text.Clear().Append(outputs.TextBetween(start, edit.Start)).Append(edit.Replacement);
                groupStart = start;
                groupEnd = end;
                ruleId = edit.RuleId;
                open = true;
            }

            lastEnd = edit.End;
        }

        if (open)
        {
            groups.Add(new Group(groupStart, groupEnd, text.ToString() + outputs.TextBetween(lastEnd, groupEnd), ruleId));
        }

        return groups;
    }

    /// <summary>
    ///     A range of the produced text and the text that replaces it.
    /// </summary>
    /// <param name="Start">Offset of the first replaced character in the produced text.</param>
    /// <param name="End">Exclusive end offset in the produced text.</param>
    /// <param name="Replacement">The new text.</param>
    /// <param name="RuleId">The rule of the first edit of the group.</param>
    private readonly record struct Group(int Start, int End, string Replacement, string RuleId);

    /// <summary>
    ///     Where the edits of the first list put their text in the produced text, and the arithmetic between the two texts.
    /// </summary>
    private sealed class Outputs
    {
        /// <summary>
        ///     The edits of the first list.
        /// </summary>
        private readonly ImmutableArray<TextEdit> _edits;

        /// <summary>
        ///     The offset in the produced text where each edit's text starts.
        /// </summary>
        private readonly int[] _starts;

        /// <summary>
        ///     The offset in the produced text where each edit's text ends (exclusive).
        /// </summary>
        private readonly int[] _ends;

        /// <summary>
        ///     For each count of edits, the total change in length that these edits make.
        /// </summary>
        private readonly int[] _shift;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Outputs"/> class.
        /// </summary>
        /// <param name="edits">The edits of the first list, ordered and without conflicts.</param>
        public Outputs(ImmutableArray<TextEdit> edits)
        {
            _edits = edits;
            _starts = new int[edits.Length];
            _ends = new int[edits.Length];
            _shift = new int[edits.Length + 1];
            for (var k = 0; k < edits.Length; k++)
            {
                _starts[k] = edits[k].Start + _shift[k];
                _ends[k] = _starts[k] + edits[k].Replacement.Length;
                _shift[k + 1] = _shift[k] + edits[k].Replacement.Length - edits[k].Length;
            }
        }

        /// <summary>
        ///     Gets the start of the text of an edit in the produced text.
        /// </summary>
        /// <param name="index">The index of the edit.</param>
        /// <returns>The offset.</returns>
        public int OutputStart(int index) => _starts[index];

        /// <summary>
        ///     Gets the end of the text of an edit in the produced text.
        /// </summary>
        /// <param name="index">The index of the edit.</param>
        /// <returns>The exclusive offset.</returns>
        public int OutputEnd(int index) => _ends[index];

        /// <summary>
        ///     Gets the text of the produced text between two offsets, which must lie inside text that one edit wrote.
        /// </summary>
        /// <param name="from">Offset where the text starts.</param>
        /// <param name="to">Exclusive offset where the text ends.</param>
        /// <returns>The text; empty when the range is empty.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the range is not inside the text of one edit.</exception>
        public string TextBetween(int from, int to)
        {
            if (to <= from)
            {
                return string.Empty;
            }

            // Only the last edit that starts at or before the offset and writes some text can contain the range; the texts are disjoint
            var k = CountWhere(_starts, start => start <= from) - 1;
            while (k >= 0 && _ends[k] == _starts[k])
            {
                k--;
            }

            if (k >= 0 && to <= _ends[k])
            {
                return _edits[k].Replacement.Substring(from - _starts[k], to - from);
            }

            throw new InvalidOperationException("Edits overlap in text that no edit wrote.");
        }

        /// <summary>
        ///     Finds the edit whose text a position lies strictly inside.
        /// </summary>
        /// <param name="position">A position in the produced text.</param>
        /// <returns>The index of the edit, or -1 when no edit's text contains the position strictly inside.</returns>
        public int FindStrictlyInside(int position)
        {
            // The last edit that starts before the position is the only one that can contain it
            var index = CountWhere(_starts, start => start < position) - 1;
            return index >= 0 && position < _ends[index] ? index : -1;
        }

        /// <summary>
        ///     Finds the edits of the first list whose text the two ends of an edit of the second list fall in.
        /// </summary>
        /// <param name="edit">The edit of the second list.</param>
        /// <returns>
        ///     The index of the edit that the start falls in and of the edit that the end falls in, -1 for none. An end
        ///     that lies in the text of an edit strictly inside, or, for an insertion, anywhere in or at the edge of
        ///     the text of an insertion of the first list.
        /// </returns>
        public (int Start, int End) FindExtensions(TextEdit edit)
        {
            var start = FindStrictlyInside(edit.Start);
            var end = FindStrictlyInside(edit.End);
            if (edit.Length == 0 && start < 0)
            {
                // The order of two insertions at one offset is not defined, so the insertion joins the one it touches
                // Both arrays ascend, so the search starts at the first text that ends at or after the insertion
                for (var k = CountWhere(_ends, end => end < edit.Start); k < _edits.Length && _starts[k] <= edit.Start; k++)
                {
                    if (_edits[k].Length == 0 && _ends[k] > _starts[k])
                    {
                        return (k, k);
                    }
                }
            }

            return (start, end);
        }

        /// <summary>
        ///     Determines whether the text of an edit lies inside a range of the produced text, so that replacing the range replaces it.
        /// </summary>
        /// <param name="index">The index of the edit.</param>
        /// <param name="start">Start of the range.</param>
        /// <param name="end">Exclusive end of the range.</param>
        /// <returns>
        ///     <see langword="true"/> when the text of the edit is not empty and lies in the range, or the edit deletes text at a
        ///     position strictly inside the range.
        /// </returns>
        public bool IsInside(int index, int start, int end) =>
            _ends[index] > _starts[index]
                ? start <= _starts[index] && _ends[index] <= end
                : start < _starts[index] && _starts[index] < end;

        /// <summary>
        ///     Marks the edits whose text lies inside a range of the produced text (see <see cref="IsInside"/>).
        /// </summary>
        /// <param name="absorbed">One flag per edit; receives <see langword="true"/> for each edit inside the range.</param>
        /// <param name="start">Start of the range.</param>
        /// <param name="end">Exclusive end of the range.</param>
        public void MarkInside(bool[] absorbed, int start, int end)
        {
            // An edit inside the range starts in it, and the starts ascend, so only the edits that start in the range are looked at
            for (var k = CountWhere(_starts, value => value < start); k < _edits.Length && _starts[k] <= end; k++)
            {
                absorbed[k] |= IsInside(k, start, end);
            }
        }

        /// <summary>
        ///     Maps the start of a range of the produced text to the original text.
        /// </summary>
        /// <param name="position">A position in the produced text that is not strictly inside the text of an edit.</param>
        /// <returns>The offset in the original text; edits that end at the position are before it.</returns>
        public int ToOriginalStart(int position) => position - _shift[CountWhere(_ends, end => end <= position)];

        /// <summary>
        ///     Maps the end of a range of the produced text to the original text.
        /// </summary>
        /// <param name="position">A position in the produced text that is not strictly inside the text of an edit.</param>
        /// <returns>The offset in the original text; edits that start at the position are after it.</returns>
        public int ToOriginalEnd(int position) => position - _shift[CountWhere(_starts, start => start < position)];

        /// <summary>
        ///     Counts the leading elements of a sorted array that satisfy a condition that holds for a prefix of the array.
        /// </summary>
        /// <param name="values">The values, in ascending order.</param>
        /// <param name="holds">The condition; true for the first elements and false for the rest.</param>
        /// <returns>The number of elements for which the condition holds.</returns>
        private static int CountWhere(int[] values, Func<int, bool> holds)
        {
            var low = 0;
            var high = values.Length;
            while (low < high)
            {
                var middle = (low + high) >>> 1;
                if (holds(values[middle]))
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
