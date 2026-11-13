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

namespace DemaConsulting.LintMark.Safety;

/// <summary>
///     Decides whether formatting already formatted text changes it again, the third safety check.
/// </summary>
/// <remarks>
///     <para>
///         A formatter whose output is not a fixed point of itself (it flips between two layouts, or keeps
///         growing the text) makes every run produce a different file, which no one can review or trust. The
///         check applies the same formatting to the formatted text and requires that nothing changes.
///     </para>
///     <para>
///         The formatting step is passed in as a delegate so the check does not depend on how a document is
///         parsed and formatted. A delegate that throws, or returns null, is reported as not idempotent: a
///         check that cannot be completed refuses the rewrite.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use when the delegate is.
///     </para>
/// </remarks>
internal static class IdempotencyChecker
{
    /// <summary>
    ///     Checks that formatting the formatted text leaves it unchanged.
    /// </summary>
    /// <param name="formattedLf">The formatted LF-normalized text. Must not be null.</param>
    /// <param name="reformat">Formats LF-normalized text and returns the formatted LF-normalized text. Must not be null.</param>
    /// <returns>
    ///     An equivalent result when the second formatting returns the same text; otherwise a result that names
    ///     the first line that differs, or states that the second formatting failed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static EquivalenceResult Check(string formattedLf, Func<string, string> reformat)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(formattedLf);
        ArgumentNullException.ThrowIfNull(reformat);

        // Format the formatted text again; a failure of the second pass means idempotency was not established
        string again;
        try
        {
            again = reformat(formattedLf);
        }

        // A check that cannot be completed must refuse the rewrite, whatever the cause
        catch (Exception ex)
        {
            return EquivalenceResult.Different($"Formatting the formatted text again failed ({ex.GetType().Name})");
        }

        if (again is null)
        {
            return EquivalenceResult.Different("Formatting the formatted text again returned no text");
        }

        if (string.Equals(again, formattedLf, StringComparison.Ordinal))
        {
            return EquivalenceResult.Equivalent;
        }

        // Name the first line that the second pass changed
        var index = EquivalenceResult.FirstDifference(formattedLf, again);
        var line = formattedLf.AsSpan(0, index).Count('\n') + 1;
        var lineStart = index == 0 ? 0 : formattedLf.LastIndexOf('\n', index - 1) + 1;
        return EquivalenceResult.Different(
            $"Formatting the formatted text again changes line {line}: first \"{EquivalenceResult.Excerpt(formattedLf, lineStart)}\" then \"{EquivalenceResult.Excerpt(again, lineStart)}\"");
    }
}
