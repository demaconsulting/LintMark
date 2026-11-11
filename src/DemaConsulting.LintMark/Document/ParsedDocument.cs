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

using Markdig.Syntax;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     The result of parsing one <see cref="SourceDocument"/>.
/// </summary>
/// <param name="Source">The LF-normalized source the other members refer to.</param>
/// <param name="Ast">The parser's syntax tree, kept for inline and atom walks.</param>
/// <param name="Blocks">Flattened, filtered and source-ordered blocks (see <see cref="DocumentParser"/>).</param>
/// <param name="Suppressions">Suppression directives found in HTML comments.</param>
/// <param name="References">Link reference definitions.</param>
/// <remarks>
///     Created per file and never shared between workers. Treated as read-only after creation.
/// </remarks>
internal sealed record ParsedDocument(
    SourceDocument Source,
    MarkdownDocument Ast,
    IReadOnlyList<BlockInfo> Blocks,
    SuppressionMap Suppressions,
    ReferenceIndex References);
