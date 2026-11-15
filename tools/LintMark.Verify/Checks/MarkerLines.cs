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

namespace DemaConsulting.LintMark.Verify.Checks;

/// <summary>
///     Takes the marker comment that the link refactoring adds out of a rewrite, so that the rewrite can be compared with the
///     original by what it renders.
/// </summary>
/// <remarks>
///     <para>
///         The new definitions are introduced by a comment, and an HTML comment is part of the HTML that a Markdown file renders to
///         (and is compared exactly), although no reader sees it. It is the one thing the link refactoring writes that the page
///         contains. The verification compares the original with the rewrite without that line; every other character of the
///         rewrite, the definitions and the converted links included, is compared as before.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class MarkerLines
{
    /// <summary>
    ///     Removes the marker comment that the link refactoring added to a rewrite.
    /// </summary>
    /// <param name="original">The text before the rewrite, with LF or CRLF line endings. Must not be null.</param>
    /// <param name="rewritten">The text after the rewrite, with the same kind of line endings. Must not be null.</param>
    /// <param name="marker">The marker comment of the configuration. Must not be null.</param>
    /// <returns>
    ///     The rewrite without its last line that holds the marker (and the line ending of that line) when the rewrite has exactly
    ///     one such line more than the original; otherwise the rewrite unchanged.
    /// </returns>
    /// <remarks>
    ///     Only a line that holds nothing but the marker counts, so a marker that the original already had is never removed, and a
    ///     rewrite that holds other new text is not changed.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static string WithoutAddedMarker(string original, string rewritten, string marker)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(rewritten);
        ArgumentNullException.ThrowIfNull(marker);

        var (before, _) = FindMarkerLines(original, marker);
        var (after, lastStart) = FindMarkerLines(rewritten, marker);
        if (after != before + 1)
        {
            return rewritten;
        }

        // The new block is written at the end of the document, so the added marker is the last one
        var end = rewritten.IndexOf('\n', lastStart);
        return rewritten.Remove(lastStart, (end < 0 ? rewritten.Length : end + 1) - lastStart);
    }

    /// <summary>
    ///     Counts the lines of a text that hold only a marker and finds where the last of them starts.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="marker">The marker.</param>
    /// <returns>The number of such lines and the offset where the last starts (-1 when there is none).</returns>
    private static (int Count, int LastStart) FindMarkerLines(string text, string marker)
    {
        var count = 0;
        var last = -1;
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            var lineEnd = end < 0 ? text.Length : end;
            if (text.AsSpan(start, lineEnd - start).TrimEnd('\r').SequenceEqual(marker))
            {
                count++;
                last = start;
            }

            if (end < 0)
            {
                break;
            }

            start = end + 1;
        }

        return (count, last);
    }
}
