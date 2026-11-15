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

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     How a segment of a paragraph ends: with nothing, or with a hard line break written in one of its two forms.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. A wrapped paragraph keeps each hard line break in the form it
///     was written in, because the two forms are equal to the parser but the choice between them belongs to the
///     <c>trailing-whitespace</c> rule, not to the wrap.
/// </remarks>
internal enum HardBreakKind
{
    /// <summary>The segment is the end of the paragraph.</summary>
    None,

    /// <summary>The segment ends with a line break written as two or more spaces at the end of the line.</summary>
    Spaces,

    /// <summary>The segment ends with a line break written as a backslash at the end of the line.</summary>
    Backslash
}
