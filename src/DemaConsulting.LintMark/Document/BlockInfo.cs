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
///     One block of the flattened, filtered and source-ordered block list.
/// </summary>
/// <param name="Node">
///     The parser's block, or <see langword="null"/> for a <see cref="BlockKind.UnreferencedFootnote"/>
///     found by the line scan (no node exists).
/// </param>
/// <param name="Kind">Classification of the block.</param>
/// <param name="Start">
///     Offset of the first character of the block (start of the marker for list items and footnote definitions).
/// </param>
/// <param name="EndInclusive">
///     Offset of the last character of the block, never a line terminator. This is the
///     <em>effective</em> end: the parser's raw end is corrected for unclosed fences and trailing line feeds.
/// </param>
/// <param name="Depth">Nesting depth (0 for top-level blocks).</param>
/// <param name="Parent">
///     The enclosing block's parser node, or <see langword="null"/> for top-level blocks. For a link
///     reference definition the enclosing block is resolved from containment, because the parser keeps
///     definitions at the document root.
/// </param>
/// <remarks>
///     Immutable; the referenced parser objects belong to the per-file parse and are only read.
/// </remarks>
internal sealed record BlockInfo(Block? Node, BlockKind Kind, int Start, int EndInclusive, int Depth, Block? Parent);
