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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The outcome of formatting one document: the text edits, the line ending to write, and the formatting
///     violations that rewriting would remove.
/// </summary>
/// <param name="Edits">The edits to the LF-normalized text, ordered and conflict-free; empty when nothing changes.</param>
/// <param name="LineEnding">The line ending the file is written with.</param>
/// <param name="Diagnostics">The formatting violations found (the findings that applying the result removes).</param>
internal sealed record FormatResult(ImmutableArray<TextEdit> Edits, LineEnding LineEnding, ImmutableArray<Diagnostic> Diagnostics);
