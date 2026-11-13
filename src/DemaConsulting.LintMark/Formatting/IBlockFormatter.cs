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

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Formats one kind of block by proposing edits to the document text.
/// </summary>
/// <remarks>
///     <para>
///         The interface is the extension point of the formatting subsystem: <see cref="DocumentFormatter"/>
///         walks the blocks of a document and hands each block to the first formatter that accepts it. A block
///         that no formatter accepts is left to the document-level rules that apply to every document
///         (whitespace, blank lines, block spacing, the end of the file), which never touch protected content such
///         as code. The text of a document is only ever changed through edits, so everything that no edit touches
///         is preserved by construction.
///     </para>
///     <para>
///         A formatter must be a pure function of the block, the document and the configuration: no state kept
///         between calls and no side effects other than adding edits, so that formatting is deterministic and
///         the safety checks can run it again. Implementations must be safe for concurrent use, because one
///         instance serves every worker thread.
///     </para>
/// </remarks>
internal interface IBlockFormatter
{
    /// <summary>
    ///     Gets the identifiers of the rules that this formatter enforces.
    /// </summary>
    /// <remarks>
    ///     A block is left verbatim when any of these rules is suppressed anywhere inside the block, so that a
    ///     suppression comment is honored at block granularity.
    /// </remarks>
    ImmutableArray<string> RuleIds { get; }

    /// <summary>
    ///     Determines whether this formatter formats a block.
    /// </summary>
    /// <param name="block">The block to test.</param>
    /// <returns><see langword="true"/> when <see cref="Format"/> should be called for the block.</returns>
    bool CanFormat(BlockInfo block);

    /// <summary>
    ///     Proposes the edits that bring a block into canonical form.
    /// </summary>
    /// <param name="context">The document and settings.</param>
    /// <param name="block">The block to format; <see cref="CanFormat"/> returned true for it.</param>
    /// <param name="edits">Receives the edits; adding an edit that conflicts with another throws <see cref="InvalidOperationException"/>.</param>
    void Format(FormatContext context, BlockInfo block, EditSet edits);
}
