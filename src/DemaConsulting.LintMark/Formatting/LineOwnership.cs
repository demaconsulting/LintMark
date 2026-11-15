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
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Which lines of a document belong to which leaf block, which must never be edited, and which lie inside a container.
/// </summary>
/// <remarks>
///     <para>
///         The document-level passes and the block formatters must never propose edits to the same characters.
///         This map is how they agree: a line of code, HTML, math, front matter, a
///         grid table or any block the tool does not model is <em>protected</em> and nobody edits its content; the
///         remaining lines belong to the passes and the block formatters that accept the block.
///     </para>
///     <para>
///         Every range is measured up to the last line with content, not up to the parser's span, because the
///         parser's spans of lists and code blocks may run over trailing lines that hold only whitespace. A rule
///         that treats such a line differently depending on whether it is empty or holds spaces would not be stable
///         (formatting its own output would change it again), so ranges never depend on it. The exceptions are
///         blocks whose trailing blank lines are content (an unclosed fence, and HTML blocks that only the end
///         of the file closes), which keep the parser's span, and a top-level indented code block, which also
///         counts a trailing whitespace-only line wider than its indentation (the rest of that line is code).
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
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <returns>The map.</returns>
    public static LineOwnership Build(ParsedDocument document, LineLayout layout)
    {
        var topLevel = document.Blocks.Where(static block => block.Parent is null && block.Depth == 0).ToImmutableArray();
        var map = new LineOwnership(layout.LineCount, topLevel);
        if (layout.LineCount == 0)
        {
            return map;
        }

        var rawText = RawTextElements.FindProtectedBlocks(document);
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
            var guarded = IsProtectedBlock(block, first, last) || rawText.Contains(block);
            if (block.Kind is BlockKind.UnclosedFence or BlockKind.UnreferencedFootnote)
            {
                last = Math.Max(last, RegionLastLine(layout, topLevel, block));
            }

            for (var line = first; line <= last; line++)
            {
                map._owner[line] = block;
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
    /// <returns>
    ///     The zero-based first and last line. The last line of a top-level indented code block also counts a
    ///     trailing whitespace-only line that is wider than the indentation, because what is left of it after the
    ///     indentation is a line of the code.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static (int First, int Last) LinesOf(LineLayout layout, BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(block);

        var first = layout.LineOf(block.Start);
        var last = Math.Max(first, layout.LineOf(block.EndInclusive));
        if (block.Kind == BlockKind.IndentedCode && block.Parent is null)
        {
            last = LastCodeLine(layout, first, last);
        }
        else if (block.Kind is not (BlockKind.UnclosedFence or BlockKind.HtmlBlock))
        {
            last = layout.LastContentLine(first, last);
        }

        return (first, last);
    }

    /// <summary>
    ///     Finds the last line of a top-level indented code block that belongs to its content.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="first">The first line of the block.</param>
    /// <param name="last">The last line of the parser's span.</param>
    /// <returns>
    ///     The last line that has content, or that holds only whitespace but more than the four columns of
    ///     indentation: the parser keeps what is left after the indentation as a line of the code (an empty rest is
    ///     dropped at the end of the block), so such a line is code that must not be trimmed.
    /// </returns>
    private static int LastCodeLine(LineLayout layout, int first, int last)
    {
        var line = last;
        while (line > first && layout.IsBlank(line) && !HoldsCodeWhitespace(layout, line))
        {
            line--;
        }

        return line;
    }

    /// <summary>
    ///     Determines whether a whitespace-only line is wider than the indentation of an indented code block.
    /// </summary>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="line">The zero-based line, which holds only spaces and tabs.</param>
    /// <returns><see langword="true"/> when the line is more than four columns wide.</returns>
    private static bool HoldsCodeWhitespace(LineLayout layout, int line)
    {
        var column = 0;
        for (var position = layout.LineStart(line); position < layout.LineEnd(line); position++)
        {
            column = layout.Text[position] == '\t' ? LineLayout.NextTabStop(column) : column + 1;
        }

        return column > LineLayout.TabWidth;
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
    /// <param name="layout">The line arithmetic of the document (for the text of the block). Must not be null.</param>
    /// <param name="block">The block. Must not be null.</param>
    /// <param name="contentFollows">
    ///     Whether more content follows the blank lines. A top-level HTML block that the end of the file closes
    ///     cannot have content after it, so one that does is closed and its trailing blank lines are not content.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> for an unclosed fence, for an unreferenced footnote and a block the tool does not
    ///     model, and for an HTML block (a comment, script, style, pre, processing instruction, declaration or CDATA
    ///     section) that its own end condition does not close: only the end of its container closes it, so the blank
    ///     lines after the last line with content are possibly part of the block. An HTML block whose end
    ///     condition is on its last line is closed, and the blank lines after it are not content.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="block"/> is null.</exception>
    public static bool KeepsTrailingBlankLines(LineLayout layout, BlockInfo block, bool contentFollows)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(block);
        if (block.Kind is BlockKind.UnclosedFence or BlockKind.UnreferencedFootnote or BlockKind.Other)
        {
            return true;
        }

        return block.Kind == BlockKind.HtmlBlock
            && block.Node is HtmlBlock { Type: not (HtmlBlockType.InterruptingBlock or HtmlBlockType.NonInterruptingBlock) } html
            && !(contentFollows && block.Parent is null)
            && !IsClosedByEndCondition(layout.Text, block, html.Type);
    }

    /// <summary>
    ///     Determines whether the text of an HTML block of types 1 to 5 contains the end condition of its type.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="block">The HTML block.</param>
    /// <param name="type">The type of the block.</param>
    /// <returns>
    ///     <see langword="true"/> when the text of the block holds <c>--&gt;</c> (comment), <c>?&gt;</c> (processing
    ///     instruction), <c>&gt;</c> (declaration), <c>]]&gt;</c> (CDATA) or a closing <c>script</c>, <c>pre</c>,
    ///     <c>style</c> or <c>textarea</c> tag (compared without regard to case).
    /// </returns>
    /// <remarks>
    ///     A block of these types ends at the first line that holds the end condition, so a block that holds it
    ///     was closed by it and one that does not was closed by the end of its container (or the file).
    /// </remarks>
    private static bool IsClosedByEndCondition(string text, BlockInfo block, HtmlBlockType type)
    {
        var length = Math.Min(block.EndInclusive + 1, text.Length) - block.Start;
        var span = text.AsSpan(block.Start, Math.Max(length, 0));
        return type switch
        {
            HtmlBlockType.Comment => span.Contains("-->", StringComparison.Ordinal),
            HtmlBlockType.CData => span.Contains("]]>", StringComparison.Ordinal),
            HtmlBlockType.ProcessingInstruction => span.Contains("?>", StringComparison.Ordinal),
            HtmlBlockType.DocumentType => span.Contains('>'),
            _ => span.Contains("</script>", StringComparison.OrdinalIgnoreCase)
                || span.Contains("</pre>", StringComparison.OrdinalIgnoreCase)
                || span.Contains("</style>", StringComparison.OrdinalIgnoreCase)
                || span.Contains("</textarea>", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    ///     Gets the leaf block that covers a line.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns>The block, or <see langword="null"/> when no leaf block covers the line.</returns>
    public BlockInfo? LeafAt(int line) => _owner[line];

    /// <summary>
    ///     Determines whether a line lies inside a footnote definition.
    /// </summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns><see langword="true"/> when a footnote definition covers the line.</returns>
    /// <remarks>
    ///     The parser reads the body of a footnote definition inconsistently (a definition inside one may be
    ///     reported as a link reference definition although it is code), so the end of the file is
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

/// <summary>
///     Finds the leaf blocks whose whitespace belongs to a raw HTML element that the Markdown text opens and does not close
///     in the same block.
/// </summary>
/// <remarks>
///     <para>
///         The content of a <c>pre</c>, <c>script</c>, <c>style</c> or <c>textarea</c> element keeps its line breaks and spaces
///         when the page is displayed. When an HTML start tag of one of them sits in an HTML block that ends at a blank line,
///         or in the inline HTML of a paragraph, the Markdown that follows is still read as paragraphs and lists, but the
///         page shows it inside the element. Joining or trimming its lines would change what the page shows, so every leaf
///         block that starts while such an element is open, and every block that holds a start tag, is left as written.
///     </para>
///     <para>
///         Only HTML blocks that no end condition closes by itself and inline HTML count; a tag in code, in a comment or in
///         a processing instruction is text. A closing tag closes the element opened last with the same name, a closing
///         tag with nothing open is ignored. Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class RawTextElements
{
    /// <summary>
    ///     The time limit of a pattern match in milliseconds.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     The names of the elements whose content keeps its whitespace.
    /// </summary>
    private static readonly string[] Names = ["pre", "script", "style", "textarea"];

    /// <summary>
    ///     Matches an HTML start or end tag of one of the elements (group 1 is the slash of an end tag, group 2 the name).
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"<(/?)(pre|script|style|textarea)(?=[\s/>]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex TagRegex();

    /// <summary>
    ///     Finds the leaf blocks that must be left as written because of an open raw HTML element.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <returns>The blocks (compared by reference); empty when no start tag of such an element is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    public static HashSet<BlockInfo> FindProtectedBlocks(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var found = new HashSet<BlockInfo>(ReferenceEqualityComparer.Instance);
        var open = new int[Names.Length];
        foreach (var block in document.Blocks)
        {
            if (LineOwnership.IsContainer(block.Kind))
            {
                continue;
            }

            // The tags of the block in source order: an element that is open at the start protects the block
            var openAtStart = open.Any(static count => count > 0);
            var opens = false;
            foreach (var (name, isStart) in TagsOf(document, block))
            {
                if (isStart)
                {
                    open[name]++;
                    opens = true;
                }
                else if (open[name] > 0)
                {
                    open[name]--;
                }
            }

            if (openAtStart || opens)
            {
                found.Add(block);
            }
        }

        return found;
    }

    /// <summary>
    ///     Lists the start and end tags of the elements that a leaf block holds.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="block">The leaf block.</param>
    /// <returns>The index into <see cref="Names"/> and whether the tag starts the element, in source order.</returns>
    private static List<(int Name, bool IsStart)> TagsOf(ParsedDocument document, BlockInfo block)
    {
        var tags = new List<(int, bool)>();
        switch (block.Node)
        {
            case HtmlBlock { Type: HtmlBlockType.Comment or HtmlBlockType.CData or HtmlBlockType.ProcessingInstruction or HtmlBlockType.DocumentType }:
                break;

            case HtmlBlock:
                var text = document.Source.Text;
                var end = Math.Min(block.EndInclusive + 1, text.Length);
                if (block.Start >= 0 && block.Start < end)
                {
                    foreach (var match in TagRegex().EnumerateMatches(text.AsSpan(block.Start, end - block.Start)))
                    {
                        AddTag(tags, text.AsSpan(block.Start + match.Index, match.Length));
                    }
                }

                break;

            case LeafBlock { Inline: { } inline }:
                foreach (var html in inline.Descendants<HtmlInline>())
                {
                    if (TagRegex().Match(html.Tag) is { Success: true, Index: 0 } match)
                    {
                        AddTag(tags, match.Value);
                    }
                }

                break;
        }

        return tags;
    }

    /// <summary>
    ///     Adds one matched tag to a list.
    /// </summary>
    /// <param name="tags">Receives the tag.</param>
    /// <param name="tag">The matched text: <c>&lt;</c>, an optional slash and the name.</param>
    private static void AddTag(List<(int, bool)> tags, ReadOnlySpan<char> tag)
    {
        var isEnd = tag.Length > 1 && tag[1] == '/';
        var name = tag[(isEnd ? 2 : 1)..];
        for (var i = 0; i < Names.Length; i++)
        {
            if (name.Equals(Names[i], StringComparison.OrdinalIgnoreCase))
            {
                tags.Add((i, !isEnd));
                return;
            }
        }
    }
}

/// <summary>
///     Detects the one situation in which writing a <c>---</c> line elsewhere in a document would turn the
///     document's first line into the opening of YAML front matter.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads a first line of <c>---</c> as the start of front matter only when a closing
///         <c>---</c> line follows; without one it is a thematic break. A rewrite that creates such a line (a break
///         converted to dashes, an indented code block whose content has an unindented <c>---</c> line once it is
///         fenced) would silently swallow everything in between into front matter. The formatters that can create
///         such a line ask this guard first and leave the block as written.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class FrontMatterGuard
{
    /// <summary>
    ///     The line that opens (and closes) YAML front matter.
    /// </summary>
    private const string Fence = "---";

    /// <summary>
    ///     Determines whether a new <c>---</c> line could close a front matter block that the first line opens.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the first line is <c>---</c> and the document has no front matter (so the
    ///     first line is a thematic break that a later <c>---</c> line would turn into front matter).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    public static bool CouldOpenFrontMatter(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = document.Source.Text;
        var end = text.IndexOf('\n', StringComparison.Ordinal);
        var firstLine = end < 0 ? text : text[..end];
        return firstLine.TrimEnd(' ', '\t') == Fence && !document.Blocks.Any(static block => block.Kind == BlockKind.FrontMatter);
    }
}
