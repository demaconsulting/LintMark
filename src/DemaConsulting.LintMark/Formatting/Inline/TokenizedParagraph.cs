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

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     A paragraph cut into the words (atoms) that a wrap may move, with what a wrap needs to know about the lines it started from.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. Immutable, including its collections; safe for concurrent use.
/// </remarks>
internal sealed record TokenizedParagraph
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TokenizedParagraph"/> class.
    /// </summary>
    /// <param name="segments">The segments between hard line breaks, at least one. Must not be a default array or contain null.</param>
    /// <param name="breakOpportunitiesPerLine">
    ///     For each source line, the number of places inside the line where it could be broken. Must not be a default
    ///     array and must not contain a negative number.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when an array is a default array, <paramref name="segments"/> is empty, or a count is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> holds a null element.</exception>
    public TokenizedParagraph(ImmutableArray<ParagraphSegment> segments, ImmutableArray<int> breakOpportunitiesPerLine)
    {
        // Validate every argument so that an invalid paragraph can never be created
        if (segments.IsDefault)
        {
            throw new ArgumentException("The segments must not be a default array.", nameof(segments));
        }

        if (segments.IsEmpty)
        {
            throw new ArgumentException("The segments must not be empty.", nameof(segments));
        }

        if (segments.Any(static segment => segment is null))
        {
            throw new ArgumentNullException(nameof(segments), "The segments must not contain null.");
        }

        if (breakOpportunitiesPerLine.IsDefault)
        {
            throw new ArgumentException("The break opportunities must not be a default array.", nameof(breakOpportunitiesPerLine));
        }

        if (breakOpportunitiesPerLine.Any(static count => count < 0))
        {
            throw new ArgumentException("The break opportunities must not be negative.", nameof(breakOpportunitiesPerLine));
        }

        Segments = segments;
        BreakOpportunitiesPerLine = breakOpportunitiesPerLine;
    }

    /// <summary>
    ///     Gets the segments of the paragraph between hard line breaks, in order.
    /// </summary>
    public ImmutableArray<ParagraphSegment> Segments { get; }

    /// <summary>
    ///     Gets, for each source line of the paragraph, the number of places inside the line where it could be broken
    ///     (the gaps between two atoms on that line that a wrap may turn into a line break).
    /// </summary>
    public ImmutableArray<int> BreakOpportunitiesPerLine { get; }
}
