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
///     One link reference definition (<c>[label]: url "title"</c>).
/// </summary>
/// <param name="Label">The label as the parser reports it (case kept, internal whitespace already collapsed).</param>
/// <param name="NormalizedLabel">The label with whitespace collapsed and case folded, used for matching.</param>
/// <param name="Url">The link destination.</param>
/// <param name="Title">The optional title, or <see langword="null"/>.</param>
/// <param name="Start">Offset of the first character of the definition.</param>
/// <param name="EndInclusive">Offset of the last character of the definition.</param>
/// <param name="Line">1-based line of the definition.</param>
internal sealed record ReferenceDefinition(
    string Label,
    string NormalizedLabel,
    string Url,
    string? Title,
    int Start,
    int EndInclusive,
    int Line);
