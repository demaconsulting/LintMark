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

using System.Text;

namespace DemaConsulting.LintMark.Safety;

/// <summary>
///     The verdict of one safety check.
/// </summary>
/// <remarks>
///     <para>
///         Shared by the three checkers so the engine treats them uniformly. A result is equivalent only when
///         the check positively established it; every check that cannot decide (an exception, a timeout)
///         returns a result that is not equivalent, so an undecided check refuses the rewrite.
///     </para>
///     <para>
///         The description of a failure is a single bounded line that names the first difference; it is
///         <see langword="null"/> for an equivalent result. Immutable value type; safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="IsEquivalent"><see langword="true"/> when the check passed.</param>
/// <param name="Description">The first difference as a single line; <see langword="null"/> when the check passed.</param>
internal readonly record struct EquivalenceResult(bool IsEquivalent, string? Description)
{
    /// <summary>
    ///     The longest description kept, in characters; longer text is cut so a report line stays readable.
    /// </summary>
    private const int MaxDescriptionLength = 240;

    /// <summary>
    ///     The longest excerpt of text shown inside a description, in characters.
    /// </summary>
    internal const int ExcerptLength = 60;

    /// <summary>
    ///     Gets the result of a check that passed.
    /// </summary>
    public static EquivalenceResult Equivalent { get; } = new(true, null);

    /// <summary>
    ///     Creates the result of a check that failed.
    /// </summary>
    /// <param name="description">What differs. Line breaks are replaced by spaces and the text is cut when it is long. Must not be null.</param>
    /// <returns>A result that is not equivalent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
    public static EquivalenceResult Different(string description)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(description);

        // Keep the description to one bounded line
        var line = description.ReplaceLineEndings(" ");
        return new EquivalenceResult(false, line.Length <= MaxDescriptionLength ? line : line[..MaxDescriptionLength] + "...");
    }

    /// <summary>
    ///     Shows a piece of text on one line, escaping what would break the line and cutting it when long.
    /// </summary>
    /// <param name="text">The text to show.</param>
    /// <param name="start">Offset of the first character to show; clamped into the text.</param>
    /// <returns>An escaped excerpt of at most <see cref="ExcerptLength"/> characters of the text.</returns>
    internal static string Excerpt(string text, int start)
    {
        // Take a bounded window, then escape the characters that would end or distort the line
        var from = Math.Clamp(start, 0, text.Length);
        var window = text.AsSpan(from, Math.Min(ExcerptLength, text.Length - from));
        var builder = new StringBuilder(window.Length);
        foreach (var c in window)
        {
            builder.Append(c switch
            {
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) => "?",
                _ => c.ToString()
            });
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Finds the offset of the first character at which two texts differ.
    /// </summary>
    /// <param name="first">The first text.</param>
    /// <param name="second">The second text.</param>
    /// <returns>The offset of the first difference; the length of the shorter text when one is a prefix of the other.</returns>
    internal static int FirstDifference(string first, string second) =>
        first.AsSpan().CommonPrefixLength(second.AsSpan());
}
