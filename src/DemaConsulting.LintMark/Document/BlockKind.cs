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
