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
/// <param name="Line">1-based line of the first grid-table-looking line that is not part of a valid table (1 when it cannot be located).</param>
/// <param name="Column">1-based column of that line's first border character (1 when it cannot be located).</param>
/// <remarks>
///     The grid table parser leaves damaged blocks behind when a table that looks like a grid table turns
///     out to be invalid, and <see cref="DocumentParser"/> then reads the document again without grid
///     tables. That second reading can turn valid Pandoc grid tables into paragraphs, and comparing the
///     page cannot see such damage, so a document that needed the second reading must never be
///     rewritten. This value carries the line to report. Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct GridTableFallback(int Line, int Column);
