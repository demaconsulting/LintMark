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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     A generated Markdown document made of a chain of containers around a known body.
/// </summary>
/// <param name="Name">Unique name encoding the chain, body and laziness (for failure messages).</param>
/// <param name="Chain">The container chain as letters: Q quote, L bullet, O ordered, N two-digit ordered, T tab-indented bullet.</param>
/// <param name="Text">The LF document text.</param>
/// <param name="Lazy">Whether continuation lines omit their container prefixes (lazy continuation).</param>
/// <param name="ParagraphLines">
///     The body lines when the body is a plain multi-line paragraph, otherwise <see langword="null"/>.
/// </param>
internal sealed record SyntheticDocument(string Name, string Chain, string Text, bool Lazy, IReadOnlyList<string>? ParagraphLines);
