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

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     One line of a pipe table: where its content is in the text and the cells it holds.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="PipeTable"/>. Immutable; safe for concurrent use.
/// </remarks>
internal sealed record PipeTableRow
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PipeTableRow"/> class.
    /// </summary>
    /// <param name="line">The zero-based line of the row.</param>
    /// <param name="prefix">The text of the line before its content: quote markers, list markers and indentation.</param>
    /// <param name="contentStart">The offset where the content of the line starts, just after the prefix.</param>
    /// <param name="contentEnd">The offset just after the last character of the content that is not a blank.</param>
    /// <param name="lineEnd">The offset where the line ends (its line feed or the end of the text).</param>
    /// <param name="cells">The text of every cell, without the blanks around it and without the delimiting pipes.</param>
    /// <param name="hasLeadingPipe">Whether the content starts with a pipe.</param>
    /// <param name="hasTrailingPipe">Whether the content ends with a pipe that does not start it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> or an element of <paramref name="cells"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cells"/> is a default array.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an offset or the line is negative or the offsets are not in order.</exception>
    public PipeTableRow(
        int line,
        string prefix,
        int contentStart,
        int contentEnd,
        int lineEnd,
        ImmutableArray<string> cells,
        bool hasLeadingPipe,
        bool hasTrailingPipe)
    {
        // Validate every argument so that an inconsistent row can never be created
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentOutOfRangeException.ThrowIfNegative(contentStart);
        ArgumentOutOfRangeException.ThrowIfLessThan(contentEnd, contentStart);
        ArgumentOutOfRangeException.ThrowIfLessThan(lineEnd, contentEnd);
        if (cells.IsDefault)
        {
            throw new ArgumentException("The cells must not be a default array.", nameof(cells));
        }

        if (cells.Any(static cell => cell is null))
        {
            throw new ArgumentNullException(nameof(cells), "The cells must not contain null.");
        }

        Line = line;
        Prefix = prefix;
        ContentStart = contentStart;
        ContentEnd = contentEnd;
        LineEnd = lineEnd;
        Cells = cells;
        HasLeadingPipe = hasLeadingPipe;
        HasTrailingPipe = hasTrailingPipe;
    }

    /// <summary>
    ///     Gets the zero-based line of the row.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the text of the line before its content.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    ///     Gets the offset where the content of the line starts.
    /// </summary>
    public int ContentStart { get; }

    /// <summary>
    ///     Gets the offset just after the last character of the content that is not a blank.
    /// </summary>
    public int ContentEnd { get; }

    /// <summary>
    ///     Gets the offset where the line ends.
    /// </summary>
    public int LineEnd { get; }

    /// <summary>
    ///     Gets the text of every cell, trimmed of blanks.
    /// </summary>
    public ImmutableArray<string> Cells { get; }

    /// <summary>
    ///     Gets a value indicating whether the content has no pipe at all (a line of text that the parser takes as a row of one cell).
    /// </summary>
    public bool HasNoPipe => !HasLeadingPipe && !HasTrailingPipe && Cells.Length == 1;

    /// <summary>
    ///     Gets a value indicating whether the content starts with a pipe.
    /// </summary>
    public bool HasLeadingPipe { get; }

    /// <summary>
    ///     Gets a value indicating whether the content ends with a pipe that is not also its leading pipe.
    /// </summary>
    public bool HasTrailingPipe { get; }
}
