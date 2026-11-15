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
/// <param name="GridFallback">
///     Where an invalid grid table was found, or <see langword="null"/> when the first reading of the text
///     nested correctly. When present, the document was read without grid tables and is
///     <see cref="IsFormatUnsafe"/>.
/// </param>
/// <remarks>
///     Created per file and never shared between workers. Treated as read-only after creation; the block
///     list is an immutable array, so no holder of the document can change it.
/// </remarks>
internal sealed record ParsedDocument(
    SourceDocument Source,
    MarkdownDocument Ast,
    ImmutableArray<BlockInfo> Blocks,
    SuppressionMap Suppressions,
    ReferenceIndex References,
    GridTableFallback? GridFallback = null)
{
    /// <summary>
    ///     Gets a value indicating whether the document must never be rewritten.
    /// </summary>
    /// <remarks>
    ///     True exactly when the parser had to fall back to reading the text without grid tables (see
    ///     <see cref="GridFallback"/>): the blocks then no longer describe the grid tables of the file, so no
    ///     edit based on them can be trusted. Linting continues to use the fallback reading.
    /// </remarks>
    public bool IsFormatUnsafe => GridFallback is not null;
}

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

/// <summary>
///     Classification of a block in the flattened, source-ordered block list.
/// </summary>
/// <remarks>
///     The kinds are deliberately coarser than the Markdown parser's class hierarchy and
///     encode the decisions the rest of the tool needs: whether a block may be edited, must be
///     treated verbatim, or only exists because the parser does not report it.
/// </remarks>
internal enum BlockKind
{
    /// <summary>ATX or setext heading.</summary>
    Heading,

    /// <summary>Paragraph.</summary>
    Paragraph,

    /// <summary>Fenced code block whose closing fence was found.</summary>
    FencedCode,

    /// <summary>Fenced code block or math block that was never closed; its effective span runs to the end of its parent container, or of the document.</summary>
    UnclosedFence,

    /// <summary>Indented code block.</summary>
    IndentedCode,

    /// <summary>HTML block (including HTML comments).</summary>
    HtmlBlock,

    /// <summary>Thematic break.</summary>
    ThematicBreak,

    /// <summary>Block quote (including GFM alert blocks).</summary>
    BlockQuote,

    /// <summary>List.</summary>
    List,

    /// <summary>List item.</summary>
    ListItem,

    /// <summary>Pipe table.</summary>
    Table,

    /// <summary>Pandoc grid table (shares the parser's table type; recognized by its leading <c>+</c>).</summary>
    GridTable,

    /// <summary>YAML front matter.</summary>
    FrontMatter,

    /// <summary>Link reference definition (<c>[label]: url "title"</c>).</summary>
    LinkReferenceDefinition,

    /// <summary>Footnote definition that the parser reports as a footnote; its range starts at the <c>[^label]:</c> marker.</summary>
    Footnote,

    /// <summary>
    ///     Footnote definition for which the parser produced no <see cref="Footnote"/> block: nothing
    ///     refers to it, its label repeats an earlier definition, or it stands inside a block quote or
    ///     list item. Its range starts at the <c>[^label]:</c> marker. It is found by a line scan,
    ///     except inside a quote or list item, where <see cref="BlockInfo.Node"/> keeps the parser's
    ///     link reference definition node and the range is that definition's own range.
    /// </summary>
    UnreferencedFootnote,

    /// <summary>Math block (<c>$$</c> fence) whose closing fence was found.</summary>
    MathBlock,

    /// <summary>Any block type the tool does not specifically model.</summary>
    Other
}

/// <summary>
///     The place in a document where an invalid grid table was found; its presence marks the document as
///     unsafe to rewrite.
/// </summary>
/// <param name="Line">1-based line of the first grid-table-looking line that is not part of a valid table (1 when it cannot be located).</param>
/// <param name="Column">1-based column of that line's first border character (1 when it cannot be located).</param>
/// <remarks>
///     The grid table parser leaves damaged blocks behind when a table that looks like a grid table turns
///     out to be invalid, and <see cref="DocumentParser"/> then reads the document again without grid
///     tables. That second reading can turn valid Pandoc grid tables into paragraphs, and comparing the
///     page cannot see such damage, so a document that needed the second reading must never be
///     rewritten. This value carries the line to report. Immutable value type; safe for concurrent use.
/// </remarks>
internal readonly record struct GridTableFallback(int Line, int Column);

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
