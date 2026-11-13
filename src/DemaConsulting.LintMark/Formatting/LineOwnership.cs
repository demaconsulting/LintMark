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
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Which lines of a document belong to which leaf block, which of them a block formatter has claimed, which
///     must never be edited, and which lie inside a container.
/// </summary>
/// <remarks>
///     <para>
///         The document-level passes and the block formatters must never propose edits to the same characters.
///         This map is how they agree: a line that a block formatter accepts is <em>claimed</em> (even when the
///         block is suppressed) and only that formatter edits it; a line of code, HTML, math, front matter, a
///         grid table or any block the tool does not model is <em>protected</em> and nobody edits its content; the
///         remaining lines belong to the passes.
///     </para>
///     <para>
///         Every range is measured up to the last line with content, not up to the parser's span, because the
///         parser's spans of lists and code blocks may run over trailing lines that hold only whitespace. A rule
///         that treats such a line differently depending on whether it is empty or holds spaces would not be stable
///         (formatting its own output would change it again), so ranges never depend on it. The exceptions are
///         blocks whose trailing blank lines are content (an unclosed fence, and HTML blocks that only the end
///         of the file closes), which keep the parser's span.
///     </para>
///     <para>
///         Thread safety: immutable after <see cref="Build"/>; created per document.
///     </para>
/// </remarks>
internal sealed class LineOwnership
{
    /// <summary>
    ///     The leaf block of each line, or <see langword="null"/> for a line that no leaf block covers.
    /// </summary>
    private readonly BlockInfo?[] _owner;

    /// <summary>
    ///     Whether a block formatter has claimed the line.
    /// </summary>
    private readonly bool[] _claimed;

    /// <summary>
    ///     Whether the content of the line is protected.
    /// </summary>
    private readonly bool[] _protected;

    /// <summary>
    ///     Whether the line lies inside a list, list item, block quote or footnote.
    /// </summary>
    private readonly bool[] _container;

    /// <summary>
    ///     Whether the line lies inside a footnote definition.
    /// </summary>
    private readonly bool[] _footnote;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LineOwnership"/> class.
    /// </summary>
    /// <param name="lineCount">The number of lines.</param>
    /// <param name="topLevel">The top-level blocks in source order.</param>
    private LineOwnership(int lineCount, ImmutableArray<BlockInfo> topLevel)
    {
        _owner = new BlockInfo?[lineCount];
        _claimed = new bool[lineCount];
        _protected = new bool[lineCount];
        _container = new bool[lineCount];
        _footnote = new bool[lineCount];
        TopLevel = topLevel;
    }

    /// <summary>
    ///     Gets the top-level blocks (no parent) in source order.
    /// </summary>
    public ImmutableArray<BlockInfo> TopLevel { get; }

    /// <summary>
    ///     Builds the ownership map of a document.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="isClaimed">Tells whether a block formatter accepts a block. Must not be null.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static LineOwnership Build(ParsedDocument document, LineLayout layout, Func<BlockInfo, bool> isClaimed)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(isClaimed);

        var topLevel = document.Blocks.Where(static block => block.Parent is null && block.Depth == 0).ToImmutableArray();
        var map = new LineOwnership(layout.LineCount, topLevel);
        if (layout.LineCount == 0)
        {
            return map;
        }

        foreach (var block in document.Blocks)
        {
            var (first, last) = LinesOf(layout, block);
            if (block.Kind is BlockKind.Footnote or BlockKind.UnreferencedFootnote)
            {
                Fill(map._footnote, first, last);
            }

            if (IsContainer(block.Kind))
            {
                Fill(map._container, first, last);
                continue;
            }

            // A leaf block owns its lines; the later (inner) block wins where ranges meet
            var claimed = isClaimed(block);
            var guarded = IsProtectedBlock(block, first, last);
            if (block.Kind is BlockKind.UnclosedFence or BlockKind.UnreferencedFootnote)
            {
                last = Math.Max(last, RegionLastLine(layout, topLevel, block));
            }

            for (var line = first; line <= last; line++)
            {
                map._owner[line] = block;
                map._claimed[line] = claimed;
                map._protected[line] = guarded;
            }
        }

        return map;
    }

    /// <summary>
    ///     Gets the first and last line of a block, the last measured up to its last line with content.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="block">The block. Must not be null.</param>
    /// <returns>The zero-based first and last line.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static (int First, int Last) LinesOf(LineLayout layout, BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(block);

        var first = layout.LineOf(block.Start);
        var last = Math.Max(first, layout.LineOf(block.EndInclusive));
        if (block.Kind is not (BlockKind.UnclosedFence or BlockKind.HtmlBlock))
        {
            last = layout.LastContentLine(first, last);
        }

        return (first, last);
    }

    /// <summary>
    ///     Determines whether a block kind is a container (it holds other blocks).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for list, list item, block quote and footnote.</returns>
    public static bool IsContainer(BlockKind kind) =>
        kind is BlockKind.BlockQuote or BlockKind.List or BlockKind.ListItem or BlockKind.Footnote;

    /// <summary>
    ///     Gets the last line of a block whose extent the parser does not report: the line before the next top-level block, or the last line of the text.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="topLevel">The top-level blocks in source order.</param>
    /// <param name="block">The unclosed fence or unreferenced footnote.</param>
    /// <returns>The zero-based last line that may belong to the block.</returns>
    /// <remarks>
    ///     The parser's span of an unclosed fence inside a container does not reach the content that the fence
    ///     swallows (the container's own span stops early too), and a footnote definition that nothing refers
    ///     to is only found by a line scan that knows where it starts. Everything up to the next top-level block
    ///     can be content of such a block, blank lines included, so all of it is claimed and protected: leaving
    ///     a line alone is always safe, editing a line of code never is.
    /// </remarks>
    private static int RegionLastLine(LineLayout layout, ImmutableArray<BlockInfo> topLevel, BlockInfo block)
    {
        // The top-level ancestor is the last top-level block that starts at or before the block
        var ancestor = -1;
        for (var i = 0; i < topLevel.Length && topLevel[i].Start <= block.Start; i++)
        {
            ancestor = i;
        }

        return ancestor >= 0 && ancestor + 1 < topLevel.Length
            ? Math.Max(layout.LineOf(block.Start), layout.LineOf(topLevel[ancestor + 1].Start) - 1)
            : layout.LineCount - 1;
    }

    /// <summary>
    ///     Determines whether the blank lines that end a block belong to its content.
    /// </summary>
    /// <param name="block">The block. Must not be null.</param>
    /// <param name="contentFollows">
    ///     Whether more content follows the blank lines. A top-level HTML block that the end of the file closes
    ///     cannot have content after it, so one that does is closed and its trailing blank lines are not content.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> for an unclosed fence, for an HTML block that only the end of its container
    ///     closes, and for blocks the tool does not model; the blank lines after the last line with content
    ///     are then possibly part of the block.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public static bool KeepsTrailingBlankLines(BlockInfo block, bool contentFollows)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Kind is BlockKind.UnclosedFence or BlockKind.UnreferencedFootnote or BlockKind.Other)
        {
            return true;
        }

        return block.Kind == BlockKind.HtmlBlock
            && block.Node is HtmlBlock { Type: not (HtmlBlockType.InterruptingBlock or HtmlBlockType.NonInterruptingBlock) }
            && !(contentFollows && block.Parent is null);
    }

    /// <summary>
    ///     Gets the leaf block that covers a line.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns>The block, or <see langword="null"/> when no leaf block covers the line.</returns>
    public BlockInfo? LeafAt(int line) => _owner[line];

    /// <summary>
    ///     Determines whether a block formatter has claimed a line.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when the leaf block of the line is accepted by a block formatter.</returns>
    public bool IsClaimed(int line) => _claimed[line];

    /// <summary>
    ///     Determines whether a line lies inside a footnote definition.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when a footnote definition covers the line.</returns>
    /// <remarks>
    ///     The parser reads the body of a footnote definition inconsistently (a definition inside one may be
    ///     reported as a link reference definition although it renders as code), so the end of the file is
    ///     not normalized after such a block.
    /// </remarks>
    public bool IsInFootnote(int line) => _footnote[line];

    /// <summary>
    ///     Determines whether the content of a line must never be edited.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> for lines of code, HTML, math, front matter, grid tables and blocks the tool does not model.</returns>
    public bool IsProtected(int line) => _protected[line];

    /// <summary>
    ///     Determines whether a line lies inside a list, list item, block quote or footnote.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when a container covers the line.</returns>
    public bool IsInContainer(int line) => _container[line];

    /// <summary>
    ///     Determines whether the lines of a block are never edited by the passes.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="first">Its first line.</param>
    /// <param name="last">Its last line.</param>
    /// <returns><see langword="true"/> for a protected block kind; a link reference definition is protected when it spans several lines.</returns>
    private static bool IsProtectedBlock(BlockInfo block, int first, int last) => block.Kind switch
    {
        BlockKind.FencedCode or BlockKind.UnclosedFence or BlockKind.IndentedCode or BlockKind.HtmlBlock
            or BlockKind.MathBlock or BlockKind.FrontMatter or BlockKind.GridTable or BlockKind.UnreferencedFootnote
            or BlockKind.Other => true,
        BlockKind.LinkReferenceDefinition => first != last,
        _ => false
    };

    /// <summary>
    ///     Sets a range of flags.
    /// </summary>
    /// <param name="flags">The flags.</param>
    /// <param name="first">First index.</param>
    /// <param name="last">Last index (inclusive).</param>
    private static void Fill(bool[] flags, int first, int last)
    {
        for (var line = first; line <= last; line++)
        {
            flags[line] = true;
        }
    }
}
