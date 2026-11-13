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
///     Applies an ordered set of edits to the text of a document.
/// </summary>
/// <remarks>
///     <para>
///         The patcher is a pure function of its arguments: the result is built in one ascending pass, so
///         characters that no edit covers are copied unchanged and the cost is linear in the text length
///         plus the replacement lengths. It re-checks the edits it is given (range, order, conflicts) instead
///         of trusting them, because a wrong edit would silently corrupt the user's file; the checks are
///         cheap next to the cost of reading the file.
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
    /// <param name="source">The document whose LF-normalized text is patched. Must not be null.</param>
    /// <param name="edits">
    ///     The edits, ordered by start offset and then length, without conflicts (the order that
    ///     <see cref="EditSet.ToImmutable"/> produces). Must be initialized.
    /// </param>
    /// <returns>The patched text; the very same string instance as the source text when there are no edits.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="edits"/> is the uninitialized default array, an edit is a default
    ///     instance, an edit lies outside the text, or the edits are not ordered or conflict.
    /// </exception>
    public static string Apply(SourceDocument source, ImmutableArray<TextEdit> edits)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(source);
        if (edits.IsDefault)
        {
            throw new ArgumentException("The edits must be initialized.", nameof(edits));
        }

        // Without edits the text is returned as it is
        var text = source.Text;
        if (edits.IsEmpty)
        {
            return text;
        }

        // Copy the untouched text between edits and write each replacement, checking every edit on the way
        var builder = new StringBuilder(text.Length);
        var position = 0;
        TextEdit? previous = null;
        foreach (var edit in edits)
        {
            var problem = FindProblem(edit, previous, text.Length);
            if (problem is not null)
            {
                throw new ArgumentException(problem, nameof(edits));
            }

            builder.Append(text, position, edit.Start - position);
            builder.Append(edit.Replacement);
            position = edit.End;
            previous = edit;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    /// <summary>
    ///     Checks one edit against the text length and the edit before it.
    /// </summary>
    /// <param name="edit">The edit to check.</param>
    /// <param name="previous">The edit that precedes it in the array, or <see langword="null"/> for the first.</param>
    /// <param name="textLength">Length of the text.</param>
    /// <returns>A description of what is wrong with the edit, or <see langword="null"/> when it is valid.</returns>
    private static string? FindProblem(TextEdit edit, TextEdit? previous, int textLength)
    {
        if (edit.Replacement is null || edit.RuleId is null)
        {
            return "An edit was not created with its constructor.";
        }

        if (edit.End > textLength)
        {
            return $"The edit at {edit.Start}..{edit.End} lies outside the text of length {textLength}.";
        }

        // Order and conflicts: an edit must start at or after the end of the one before; two insertions at one offset have no order
        if (previous is { } before
            && (edit.Start < before.End || (edit.Start == before.Start && edit.Length == 0 && before.Length == 0)))
        {
            return $"The edit at {edit.Start}..{edit.End} is out of order or conflicts with the edit at {before.Start}..{before.End}.";
        }

        return null;
    }
}
