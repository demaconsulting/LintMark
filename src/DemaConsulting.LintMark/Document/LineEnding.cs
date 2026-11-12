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
///     Line terminator styles that LintMark can write back to disk.
/// </summary>
/// <remarks>
///     Only LF and CRLF are modeled. A bare carriage return (classic Mac style) is deliberately
///     not representable: <see cref="SourceDocument"/> reports it as a failure instead of silently
///     rewriting it, because the Markdown parser treats it inconsistently.
/// </remarks>
internal enum LineEnding
{
    /// <summary>
    ///     Unix-style line feed (<c>\n</c>).
    /// </summary>
    Lf,

    /// <summary>
    ///     Windows-style carriage return followed by line feed (<c>\r\n</c>).
    /// </summary>
    CrLf
}
