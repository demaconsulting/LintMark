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
