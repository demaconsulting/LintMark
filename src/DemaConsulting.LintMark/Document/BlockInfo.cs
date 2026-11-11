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
///     Classification of a block in the flattened, source-ordered block list.
/// </summary>
/// <remarks>
///     The kinds are deliberately coarser than the Markdown parser's class hierarchy and
///     encode the decisions the rest of the tool needs: whether a block may be edited, must be
///     treated verbatim, or only exists because of a parser quirk compensation.
/// </remarks>
internal enum BlockKind
{
    /// <summary>ATX or setext heading.</summary>
    Heading,

    /// <summary>Paragraph.</summary>
    Paragraph,

    /// <summary>Fenced code block whose closing fence was found.</summary>
    FencedCode,

    /// <summary>Fenced code block that was never closed; its effective span runs to the end of its parent (quirk Q-1).</summary>
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

    /// <summary>Footnote definition that is referenced from the text.</summary>
    Footnote,

    /// <summary>Footnote definition without a reference; found by a line scan because the parser drops it (quirk Q-3).</summary>
    UnreferencedFootnote,

    /// <summary>Math block (<c>$$</c> fence) whose closing fence was found.</summary>
    MathBlock,

    /// <summary>Any block type the tool does not specifically model.</summary>
    Other
}

/// <summary>
///     One block of the flattened, filtered and source-ordered block list.
/// </summary>
/// <param name="Node">The parser's block, or <see langword="null"/> for <see cref="BlockKind.UnreferencedFootnote"/> (no node exists).</param>
/// <param name="Kind">Classification of the block.</param>
/// <param name="Start">Offset of the first character of the block (start of the marker for list items).</param>
/// <param name="EndInclusive">
///     Offset of the last character of the block, never a line terminator. This is the
///     <em>effective</em> end: the parser's raw end is corrected for quirks Q-1 and Q-2.
/// </param>
/// <param name="Depth">Nesting depth (0 for top-level blocks).</param>
/// <param name="Parent">The parser's parent block, or <see langword="null"/> for top-level blocks.</param>
/// <remarks>
///     Immutable; the referenced parser objects belong to the per-file parse and are only read.
/// </remarks>
internal sealed record BlockInfo(Block? Node, BlockKind Kind, int Start, int EndInclusive, int Depth, Block? Parent);

/// <summary>
///     One source line of a leaf block (paragraph or heading), as located by the line-rebuilding helper.
/// </summary>
/// <param name="Line">1-based line number.</param>
/// <param name="LineStart">Offset of the first character of the line (including any container prefix).</param>
/// <param name="ContentStart">Offset where the block's own content starts, after container prefixes.</param>
/// <param name="ContentEnd">Exclusive offset of the end of the content (the line terminator or block end).</param>
/// <param name="IsLazy">
///     <see langword="true"/> for a continuation line whose container prefix is shorter than the
///     first line's (lazy continuation, quirk Q-5); always <see langword="false"/> for the first line.
/// </param>
internal sealed record LeafLine(int Line, int LineStart, int ContentStart, int ContentEnd, bool IsLazy);
