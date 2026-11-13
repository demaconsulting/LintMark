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
///         <see langword="null"/> for an equivalent result. Instances can only be obtained through
///         <see cref="Equivalent"/> and <see cref="Different"/>, so a description is present exactly when the
///         result is not equivalent. Immutable value type; safe for concurrent use.
///     </para>
/// </remarks>
internal readonly record struct EquivalenceResult
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
    ///     Initializes a new instance of the <see cref="EquivalenceResult"/> struct.
    /// </summary>
    /// <param name="isEquivalent"><see langword="true"/> when the check passed.</param>
    /// <param name="description">The first difference as a single line; <see langword="null"/> when the check passed.</param>
    private EquivalenceResult(bool isEquivalent, string? description)
    {
        IsEquivalent = isEquivalent;
        Description = description;
    }

    /// <summary>
    ///     Gets the result of a check that passed.
    /// </summary>
    public static EquivalenceResult Equivalent { get; } = new(true, null);

    /// <summary>
    ///     Gets a value indicating whether the check passed.
    /// </summary>
    public bool IsEquivalent { get; }

    /// <summary>
    ///     Gets the first difference as a single line; <see langword="null"/> when the check passed.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    ///     Creates the result of a check that failed.
    /// </summary>
    /// <param name="description">
    ///     What differs. Each line break (carriage return, line feed, form feed, next line, line separator, and
    ///     paragraph separator) is replaced by a space and the text is cut when it is long. Must not be null.
    /// </param>
    /// <returns>A result that is not equivalent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
    public static EquivalenceResult Different(string description)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(description);

        // Keep the description to one line
        var line = description.ReplaceLineEndings(" ");
        if (line.Length <= MaxDescriptionLength)
        {
            return new EquivalenceResult(false, line);
        }

        // Cut it to the limit, without splitting a surrogate pair
        var cut = MaxDescriptionLength;
        if (char.IsHighSurrogate(line[cut - 1]) && char.IsLowSurrogate(line[cut]))
        {
            cut--;
        }

        return new EquivalenceResult(false, line[..cut] + "...");
    }

    /// <summary>
    ///     Shows a piece of text on one line, escaping what would break the line and cutting it when long.
    /// </summary>
    /// <param name="text">The text to show.</param>
    /// <param name="start">Offset of the first character to show; clamped into the text.</param>
    /// <returns>
    ///     An escaped excerpt of at most <see cref="ExcerptLength"/> characters of the text; because line feed,
    ///     carriage return, and tab are each shown as two characters, the excerpt itself is at most twice as long.
    ///     A surrogate pair is never split.
    /// </returns>
    internal static string Excerpt(string text, int start)
    {
        // Take a bounded window of the text, keeping surrogate pairs whole
        var from = Math.Clamp(start, 0, text.Length);
        if (from > 0 && from < text.Length && char.IsLowSurrogate(text[from]) && char.IsHighSurrogate(text[from - 1]))
        {
            from--;
        }

        var length = Math.Min(ExcerptLength, text.Length - from);
        if (length > 0 && from + length < text.Length &&
            char.IsHighSurrogate(text[from + length - 1]) && char.IsLowSurrogate(text[from + length]))
        {
            length--;
        }

        // Escape the characters that would end or distort the line
        var window = text.AsSpan(from, length);
        var builder = new StringBuilder(window.Length);
        foreach (var c in window)
        {
            builder.Append(c switch
            {
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\u2028' or '\u2029' => "?",
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
