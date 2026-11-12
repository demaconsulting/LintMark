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
///     Counts of the original line terminators found in a source file.
/// </summary>
/// <param name="LfCount">Number of bare line feeds (<c>\n</c> not preceded by <c>\r</c>).</param>
/// <param name="CrLfCount">Number of <c>\r\n</c> pairs.</param>
/// <remarks>
///     The counts (rather than a single style flag) are kept because mixed files exist in
///     practice; the formatter needs them to decide whether a file is mixed and which style is the
///     majority. Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct LineEndingInfo(int LfCount, int CrLfCount)
{
    /// <summary>
    ///     Gets a value indicating whether the file contains both LF and CRLF terminators.
    /// </summary>
    public bool IsMixed => LfCount > 0 && CrLfCount > 0;
}
