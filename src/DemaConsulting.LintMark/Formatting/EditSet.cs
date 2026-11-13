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
    ///     Gets the number of edits in the set.
    /// </summary>
    public int Count => _edits.Count;

    /// <summary>
    ///     Gets a value indicating whether the set holds no edit.
    /// </summary>
    public bool IsEmpty => _edits.Count == 0;

    /// <summary>
    ///     Adds an edit.
    /// </summary>
    /// <param name="edit">The edit to add; must have been created with the constructor (a default instance is rejected).</param>
    /// <remarks>
    ///     Only the neighbors of the insertion point need to be compared: the set never holds conflicting
    ///     edits, so an edit that conflicts with any member also conflicts with the member just before or just
    ///     after its position.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="edit"/> is a default instance.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="edit"/> conflicts with an edit already in the set.</exception>
    public void Add(TextEdit edit)
    {
        // A default instance has no replacement and no rule
        if (edit.Replacement is null || edit.RuleId is null)
        {
            throw new ArgumentException("The edit must be created with its constructor.", nameof(edit));
        }

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
