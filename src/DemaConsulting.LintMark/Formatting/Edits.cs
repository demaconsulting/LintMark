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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The edits that formatters propose for one document, checked for conflicts as they are added.
/// </summary>
/// <remarks>
///     <para>
///         Two edits conflict when they would change a character in common, or when both insert at the same
///         offset (their relative order would be arbitrary). Edits that merely touch (one ends where the next
///         starts) and an insertion at the start or end of a replaced range do not conflict. A conflict is a
///         defect of a formatter, never of the document, so it is reported by an exception rather than
///         resolved silently: silently dropping or merging a change would make the output depend on the order
///         in which formatters ran.
///     </para>
///     <para>
///         The set keeps its edits ordered by start offset, then length, so the snapshot returned by
///         <see cref="ToImmutable"/> is identical for any order of <see cref="Add"/> calls. An insertion sorts
///         before a replacement that starts at the same offset.
///     </para>
///     <para>
///         Thread safety: a set is a per-file builder used by one thread; it is not safe for concurrent use.
///         Snapshots are immutable.
///     </para>
/// </remarks>
internal sealed class EditSet
{
    /// <summary>
    ///     Orders edits by start offset, then length.
    /// </summary>
    private static readonly Comparer<TextEdit> PositionOrder = Comparer<TextEdit>.Create(static (a, b) =>
        a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));

    /// <summary>
    ///     The edits added so far, ordered by start offset and length, without conflicts.
    /// </summary>
    private readonly List<TextEdit> _edits = [];

    /// <summary>
    ///     Gets a value indicating whether the set holds no edit.
    /// </summary>
    public bool IsEmpty => _edits.Count == 0;

    /// <summary>
    ///     Adds an edit.
    /// </summary>
    /// <param name="edit">The edit to add.</param>
    /// <remarks>
    ///     Only the neighbors of the insertion point need to be compared: the set never holds conflicting
    ///     edits, so an edit that conflicts with any member also conflicts with the member just before or just
    ///     after its position.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="edit"/> conflicts with an edit already in the set.</exception>
    public void Add(TextEdit edit)
    {
        // Find the insertion point and compare with the neighbors
        var index = _edits.BinarySearch(edit, PositionOrder);
        if (index < 0)
        {
            index = ~index;
        }

        if (index > 0)
        {
            ThrowIfConflict(_edits[index - 1], edit);
        }

        if (index < _edits.Count)
        {
            ThrowIfConflict(edit, _edits[index]);
        }

        // An equal position (two insertions at one offset, or identical ranges) is a conflict found above
        _edits.Insert(index, edit);
    }

    /// <summary>
    ///     Gets an immutable snapshot of the edits, ordered by start offset and then length.
    /// </summary>
    /// <returns>The edits; empty when none were added.</returns>
    public ImmutableArray<TextEdit> ToImmutable() => [.. _edits];

    /// <summary>
    ///     Finds an edit that starts at an offset.
    /// </summary>
    /// <param name="start">The offset.</param>
    /// <param name="accept">Tells whether an edit that starts there is the one looked for.</param>
    /// <returns>The first edit (in set order) that starts at <paramref name="start"/> and is accepted; <see langword="null"/> when there is none.</returns>
    public TextEdit? FindStartingAt(int start, Func<TextEdit, bool> accept)
    {
        // Binary search for the first edit that starts at the offset or later, then look at the edits that start there
        var low = 0;
        var high = _edits.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_edits[middle].Start < start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var i = low; i < _edits.Count && _edits[i].Start == start; i++)
        {
            if (accept(_edits[i]))
            {
                return _edits[i];
            }
        }

        return null;
    }

    /// <summary>
    ///     Throws when two edits conflict.
    /// </summary>
    /// <param name="first">The edit that sorts first.</param>
    /// <param name="second">The edit that sorts second.</param>
    /// <exception cref="InvalidOperationException">Thrown when the edits change a character in common or insert at the same offset.</exception>
    private static void ThrowIfConflict(TextEdit first, TextEdit second)
    {
        var conflict = (first.Length, second.Length) switch
        {
            // Two insertions at one offset have no defined order
            (0, 0) => first.Start == second.Start,

            // An insertion conflicts with a replacement only strictly inside it
            (0, _) => second.Start < first.Start && first.Start < second.End,
            (_, 0) => first.Start < second.Start && second.Start < first.End,

            // Two replacements conflict when their ranges share a character
            _ => first.Start < second.End && second.Start < first.End
        };

        if (conflict)
        {
            throw new InvalidOperationException(
                $"Edit of rule '{second.RuleId}' at {second.Start}..{second.End} conflicts with edit of rule '{first.RuleId}' at {first.Start}..{first.End}.");
        }
    }
}

/// <summary>
///     One change to the LF-normalized text of a document: replace <see cref="Length"/> characters starting
///     at <see cref="Start"/> with <see cref="Replacement"/>.
/// </summary>
/// <param name="Start">Offset of the first replaced character. May equal the text length (an insertion at the end).</param>
/// <param name="Length">Number of replaced characters. Zero makes the edit an insertion.</param>
/// <param name="Replacement">The new text; empty for a deletion.</param>
/// <param name="RuleId">Identifier of the rule that makes the change, for reporting.</param>
/// <remarks>
///     <para>
///         Formatters express every change as edits against the original text instead of producing new text,
///         because edits can be checked for conflicts (two formatters changing the same characters), ordered
///         deterministically and applied in one pass, and because text that no edit touches is guaranteed to
///         stay byte for byte as it was. A deletion has an empty replacement; an insertion has a length of zero.
///     </para>
///     <para>
///         Immutable value type with value equality; safe for concurrent use.
///     </para>
/// </remarks>
internal readonly record struct TextEdit(int Start, int Length, string Replacement, string RuleId)
{
    /// <summary>
    ///     Gets the exclusive end offset of the replaced range.
    /// </summary>
    public int End => Start + Length;
}

/// <summary>
///     Applies an ordered set of edits to the text of a document.
/// </summary>
/// <remarks>
///     <para>
///         The patcher is a pure function of its arguments: the result is built in one ascending pass, so
///         characters that no edit covers are copied unchanged and the cost is linear in the text length
///         plus the replacement lengths. It refuses an edit that lies outside the text or starts inside the edit before
///         it, because a wrong edit would silently corrupt the user's file.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class SourcePatcher
{
    /// <summary>
    ///     Applies edits to the text of a document.
    /// </summary>
    /// <param name="source">The document whose LF-normalized text is patched.</param>
    /// <param name="edits">
    ///     The edits, ordered by start offset and then length, without conflicts (the order that
    ///     <see cref="EditSet.ToImmutable"/> produces).
    /// </param>
    /// <returns>The patched text; the very same string instance as the source text when there are no edits.</returns>
    /// <exception cref="ArgumentException">Thrown when an edit lies outside the text or the edits are not ordered or conflict.</exception>
    public static string Apply(SourceDocument source, ImmutableArray<TextEdit> edits)
    {
        // Without edits the text is returned as it is
        var text = source.Text;
        if (edits.IsEmpty)
        {
            return text;
        }

        // Copy the untouched text between edits and write each replacement
        var builder = new StringBuilder(text.Length);
        var position = 0;
        foreach (var edit in edits)
        {
            if (edit.End > text.Length || edit.Start < position)
            {
                throw new ArgumentException($"The edit at {edit.Start}..{edit.End} lies outside the text or conflicts with the edit before it.", nameof(edits));
            }

            builder.Append(text, position, edit.Start - position);
            builder.Append(edit.Replacement);
            position = edit.End;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }
}
