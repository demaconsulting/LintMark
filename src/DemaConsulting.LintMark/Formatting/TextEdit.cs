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

namespace DemaConsulting.LintMark.Formatting;

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
