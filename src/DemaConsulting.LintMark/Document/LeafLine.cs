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
///     One source line of a leaf block (paragraph or heading), as located by the line-rebuilding helper.
/// </summary>
/// <param name="Line">1-based line number.</param>
/// <param name="LineStart">Offset of the first character of the line (including any container prefix).</param>
/// <param name="ContentStart">Offset where the block's own content starts, after container prefixes.</param>
/// <param name="ContentEnd">Exclusive offset of the end of the content (the line terminator or block end).</param>
/// <param name="IsLazy">
///     <see langword="true"/> for a continuation line whose container prefix is shorter than the
///     first line's (lazy continuation); always <see langword="false"/> for the first line.
/// </param>
internal sealed record LeafLine(int Line, int LineStart, int ContentStart, int ContentEnd, bool IsLazy);
