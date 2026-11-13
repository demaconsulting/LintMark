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

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     The place in a document where an invalid grid table was found; its presence marks the document as
///     unsafe to rewrite.
/// </summary>
/// <remarks>
///     <para>
///         The grid table parser leaves damaged blocks behind when a table that looks like a grid table turns
///         out to be invalid, and <see cref="DocumentParser"/> then reads the document again without grid
///         tables. That second reading can turn valid Pandoc grid tables into paragraphs, and comparing the
///         rendered output cannot see such damage, so a document that needed the second reading must never be
///         rewritten. This value carries the line to report.
///     </para>
///     <para>
///         Immutable value type; safe for concurrent use. The constructor rejects a line or column below 1. A
///         default instance (line and column zero) bypasses that check, is not valid and is never produced by
///         the parser.
///     </para>
/// </remarks>
internal readonly record struct GridTableFallback
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GridTableFallback"/> struct.
    /// </summary>
    /// <param name="line">1-based line of the first grid-table-looking line that is not part of a valid table (1 when it cannot be located).</param>
    /// <param name="column">1-based column of that line's first border character (1 when it cannot be located).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line"/> or <paramref name="column"/> is less than 1.</exception>
    public GridTableFallback(int line, int column)
    {
        // Validate the position so that an invalid one can never be created
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        Line = line;
        Column = column;
    }

    /// <summary>
    ///     Gets the 1-based line of the first grid-table-looking line that is not part of a valid table.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column of that line's first border character.
    /// </summary>
    public int Column { get; }
}
