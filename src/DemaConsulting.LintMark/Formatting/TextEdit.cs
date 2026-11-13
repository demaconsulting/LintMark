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
/// <remarks>
///     <para>
///         Formatters express every change as edits against the original text instead of producing new text,
///         because edits can be checked for conflicts (two formatters changing the same characters), ordered
///         deterministically and applied in one pass, and because text that no edit touches is guaranteed to
///         stay byte for byte as it was. A deletion has an empty replacement; an insertion has a length of zero.
///     </para>
///     <para>
///         Immutable value type with value equality; safe for concurrent use. A default instance has no
///         replacement and no rule and is rejected by <see cref="EditSet"/> and <see cref="SourcePatcher"/>.
///     </para>
/// </remarks>
internal readonly record struct TextEdit
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TextEdit"/> struct.
    /// </summary>
    /// <param name="start">Offset of the first replaced character; not negative. May equal the text length (an insertion at the end).</param>
    /// <param name="length">Number of replaced characters; not negative. Zero makes the edit an insertion.</param>
    /// <param name="replacement">The new text; empty for a deletion. Must not be null.</param>
    /// <param name="ruleId">Identifier of the rule that makes the change, for reporting. Must not be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="replacement"/> or <paramref name="ruleId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ruleId"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> or <paramref name="length"/> is negative, or their sum overflows.</exception>
    public TextEdit(int start, int length, string replacement, string ruleId)
    {
        // Validate every argument so that an invalid edit can never be created
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, int.MaxValue - start);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentException.ThrowIfNullOrEmpty(ruleId);

        Start = start;
        Length = length;
        Replacement = replacement;
        RuleId = ruleId;
    }

    /// <summary>
    ///     Gets the offset of the first replaced character.
    /// </summary>
    public int Start { get; }

    /// <summary>
    ///     Gets the number of replaced characters (zero for an insertion).
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Gets the new text (empty for a deletion).
    /// </summary>
    public string Replacement { get; }

    /// <summary>
    ///     Gets the identifier of the rule that makes the change.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    ///     Gets the exclusive end offset of the replaced range.
    /// </summary>
    public int End => Start + Length;
}
