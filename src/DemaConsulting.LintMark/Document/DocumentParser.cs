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

using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     Parses a <see cref="SourceDocument"/> with LintMark's fixed Markdown pipeline and turns the
///     parser's tree into a flattened, filtered, source-ordered block list that compensates for the
///     parser's known peculiarities.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is a constant of the tool and is not configurable: changing the parse model
///         would change which constructs exist and therefore what "canonical" means. It is built
///         once, is immutable and is shared read-only by all worker threads. Input is always
///         LF-normalized text and the parser's trivia tracking is never used.
///     </para>
///     <para>
///         Parser behaviors compensated here: an unclosed fenced code or math block is recorded as
///         <see cref="BlockKind.UnclosedFence"/> with an effective span running to the end of its
///         parent; effective block ends never include a trailing line feed; reference-definition and
///         footnote group shells and empty-span nodes are dropped and the remaining nodes are sorted by
///         position; link reference definitions are reported at the document root by the parser, so
///         their depth and parent are resolved from containment; a footnote definition's range is
///         extended back to its <c>[^label]:</c> marker; footnote definitions that the parser produces
///         no node for (no reference, a repeated label) or reports as link reference definitions (inside
///         a quote or list item) are reported as <see cref="BlockKind.UnreferencedFootnote"/>; a paragraph
///         that begins with definitions starts after them; parsing always uses LF-normalized text
///         (guaranteed by <see cref="SourceDocument"/>); lazy continuation lines are flagged by
///         <see cref="GetLines"/>; paragraph lines are rebuilt from the source text by
///         <see cref="GetLines"/> because the parser releases them.
///     </para>
///     <para>
///         Thread safety: static and stateless apart from the immutable pipeline; each call works on its
///         own per-file objects.
///     </para>
/// </remarks>
internal static partial class DocumentParser
{
    /// <summary>
    ///     Matches the start of a footnote definition line (<c>[^label]:</c> indented by at most three spaces).
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^(?<indent> {0,3})\[\^(?<label>[^\]\s]+)\]:",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex FootnoteDefinitionRegex();

    /// <summary>
    ///     Matches a footnote marker (<c>[^label]:</c> plus optional blanks) at the end of a text.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"\[\^[^\]\s]+\]:[ \t]*$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex FootnoteMarkerSuffixRegex();

    /// <summary>
    ///     The indentation that continues a footnote body (four spaces).
    /// </summary>
    private const string FootnoteBodyIndent = "    ";

    /// <summary>
    ///     Gets the fixed Markdown pipeline.
    /// </summary>
    /// <remarks>
    ///     Deliberately excluded: auto identifiers (injects synthetic definitions), generic attributes
    ///     (breaks whitespace equivalence), advanced extensions (unpredictable parse changes) and
    ///     column width inference for pipe tables (would make compact/aligned rewrites non-equivalent).
    /// </remarks>
    internal static MarkdownPipeline Pipeline { get; } = CreatePipeline();

    /// <summary>
    ///     Parses a document.
    /// </summary>
    /// <param name="source">The LF-normalized source. Must not be null.</param>
    /// <returns>The parsed document (syntax tree, blocks, suppressions and references).</returns>
    /// <remarks>
    ///     Exceptions raised by the Markdown parser are not caught here; the engine reports them as
    ///     parse failures. Safe to call concurrently for different documents.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static ParsedDocument Parse(SourceDocument source)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(source);

        // Parse LF-normalized text with the fixed pipeline, then derive the corrected views
        var ast = Markdown.Parse(source.Text, Pipeline);
        var blocks = new Flattener(source).Flatten(ast);
        var suppressions = SuppressionMap.Build(source, blocks, ast);
        var references = ReferenceIndex.Build(source, blocks);
        return new ParsedDocument(source, ast, blocks, suppressions, references);
    }

    /// <summary>
    ///     Rebuilds the source lines of a leaf block (paragraph or heading).
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Must not be null.</param>
    /// <param name="leaf">A paragraph or heading block from <paramref name="document"/>. Must not be null.</param>
    /// <returns>One entry per source line, in order.</returns>
    /// <remarks>
    ///     The parser releases paragraph lines after parsing, so they are rebuilt from the
    ///     source text. The content start of a continuation line is the start of the first inline element
    ///     on that line, which is exact; only lines that begin inside a multi-line construct (code span,
    ///     link) or that carry no inline (a setext underline) fall back to stripping one quote marker per
    ///     enclosing block quote and then whitespace. Each continuation line is flagged lazy when its
    ///     container prefix is shorter than the one the container requires (see
    ///     <see cref="GetContinuationPrefix"/>). The kind and range of <paramref name="leaf"/> are
    ///     validated; that it is one of the blocks of <paramref name="document"/> is a precondition that
    ///     is not verified (checking would cost a search of the block list on every call).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="leaf"/> is not a paragraph or heading, or its range does not lie
    ///     inside the text of <paramref name="document"/>.
    /// </exception>
    public static IReadOnlyList<LeafLine> GetLines(ParsedDocument document, BlockInfo leaf)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(leaf);
        ValidateLeaf(document, leaf);

        var source = document.Source;
        var text = source.Text;
        var firstLine = source.PositionOf(leaf.Start).Line;
        var lastLine = source.PositionOf(leaf.EndInclusive).Line;
        var inlineStarts = FirstInlineStarts(source, leaf);
        var quoteDepth = CountQuoteAncestors(leaf);
        var expectedPrefix = RemoveOptionalQuoteSpaces(GetContinuationPrefix(document, leaf));

        var lines = new List<LeafLine>(lastLine - firstLine + 1);
        for (var line = firstLine; line <= lastLine; line++)
        {
            // The line ends at its terminator, or at the end of the block when that comes first
            var lineStart = source.LineStarts[line - 1];
            var terminator = line < source.LineStarts.Count ? source.LineStarts[line] - 1 : text.Length;
            var lineEnd = Math.Min(terminator, leaf.EndInclusive + 1);

            // The first line's content starts at the block start (container prefix and marker precede it)
            if (line == firstLine)
            {
                lines.Add(new LeafLine(line, lineStart, leaf.Start, lineEnd, IsLazy: false));
                continue;
            }

            // Continuation line: exact inline start when available, else prefix stripping
            var contentStart = ResolveContentStart(text, lineStart, lineEnd, inlineStarts.GetValueOrDefault(line, -1), quoteDepth);
            var actualPrefix = RemoveOptionalQuoteSpaces(text[lineStart..contentStart]);
            lines.Add(new LeafLine(line, lineStart, contentStart, lineEnd, IsShorterPrefix(actualPrefix, expectedPrefix)));
        }

        return lines;
    }

    /// <summary>
    ///     Gets the prefix that every continuation line of a leaf block would carry if it were not lazy.
    /// </summary>
    /// <param name="document">The parsed document the block belongs to. Must not be null.</param>
    /// <param name="leaf">A paragraph or heading block from <paramref name="document"/>. Must not be null.</param>
    /// <returns>
    ///     The text before the block on its first line with every character other than <c>&gt;</c>,
    ///     space and tab replaced by a space (so <c>- </c> becomes two spaces and <c>&gt; 1. </c> becomes
    ///     <c>&gt;    </c>). When the text before the block ends with a footnote marker
    ///     (<c>[^label]:</c>), the marker is replaced by the four-space indent that a footnote body
    ///     requires, not by the width of the marker.
    /// </returns>
    /// <remarks>
    ///     Same preconditions as <see cref="GetLines"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="leaf"/> is not a paragraph or heading, or its range does not lie
    ///     inside the text of <paramref name="document"/>.
    /// </exception>
    public static string GetContinuationPrefix(ParsedDocument document, BlockInfo leaf)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(leaf);
        ValidateLeaf(document, leaf);

        // Take the text before the block on its first line
        var source = document.Source;
        var lineStart = source.LineStarts[source.PositionOf(leaf.Start).Line - 1];
        var prefix = source.Text[lineStart..leaf.Start];

        // A footnote body is indented by four spaces whatever the width of its marker
        var marker = FootnoteMarkerSuffixRegex().Match(prefix);
        if (marker.Success)
        {
            var before = prefix[..marker.Index];
            return (before.AsSpan().IsWhiteSpace() ? string.Empty : BlankMarkers(before)) + FootnoteBodyIndent;
        }

        return BlankMarkers(prefix);
    }

    /// <summary>
    ///     Checks the preconditions shared by <see cref="GetLines"/> and <see cref="GetContinuationPrefix"/>.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="leaf">The block to check.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when the block is not a paragraph or heading or its range is outside the document text.
    /// </exception>
    private static void ValidateLeaf(ParsedDocument document, BlockInfo leaf)
    {
        if (leaf.Kind is not (BlockKind.Paragraph or BlockKind.Heading))
        {
            throw new ArgumentException($"The block must be a paragraph or heading, not {leaf.Kind}.", nameof(leaf));
        }

        if (leaf.Start < 0 || leaf.Start > leaf.EndInclusive || leaf.EndInclusive >= document.Source.Text.Length)
        {
            throw new ArgumentException(
                $"The block range [{leaf.Start}..{leaf.EndInclusive}] is outside the document text.",
                nameof(leaf));
        }
    }

    /// <summary>
    ///     Replaces every character other than quote markers and whitespace with a space.
    /// </summary>
    /// <param name="prefix">The text before a block on its first line.</param>
    /// <returns>The blanked text (same length).</returns>
    private static string BlankMarkers(string prefix) =>
        string.Create(prefix.Length, prefix, static (buffer, original) =>
        {
            for (var i = 0; i < original.Length; i++)
            {
                buffer[i] = original[i] is '>' or ' ' or '\t' ? original[i] : ' ';
            }
        });

    /// <summary>
    ///     Builds the fixed Markdown pipeline.
    /// </summary>
    /// <returns>The immutable pipeline.</returns>
    private static MarkdownPipeline CreatePipeline() => new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .UseGridTables()
        .UseFootnotes()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAlertBlocks()
        .UseMathematics()
        .Build();

    /// <summary>
    ///     Finds, for each line, the start of the first inline element that starts on it.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="leaf">The leaf block.</param>
    /// <returns>Map from 1-based line number to the smallest inline start offset on that line.</returns>
    private static Dictionary<int, int> FirstInlineStarts(SourceDocument source, BlockInfo leaf)
    {
        var starts = new Dictionary<int, int>();
        if (leaf.Node is not LeafBlock { Inline: { } inline })
        {
            return starts;
        }

        foreach (var node in inline.Descendants())
        {
            // Line breaks start on the line they terminate; empty spans are synthetic
            var span = node.Span;
            if (node is LineBreakInline || span.End < span.Start || span.Start < leaf.Start || span.Start > leaf.EndInclusive)
            {
                continue;
            }

            var line = source.PositionOf(span.Start).Line;
            if (!starts.TryGetValue(line, out var existing) || span.Start < existing)
            {
                starts[line] = span.Start;
            }
        }

        return starts;
    }

    /// <summary>
    ///     Counts the block quotes that enclose a block.
    /// </summary>
    /// <param name="leaf">The block.</param>
    /// <returns>The number of enclosing <see cref="QuoteBlock"/> ancestors.</returns>
    private static int CountQuoteAncestors(BlockInfo leaf)
    {
        var depth = 0;
        for (var parent = leaf.Node?.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is QuoteBlock)
            {
                depth++;
            }
        }

        return depth;
    }

    /// <summary>
    ///     Determines where the content of a continuation line starts.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="lineStart">Offset of the line start.</param>
    /// <param name="lineEnd">Exclusive offset of the line end.</param>
    /// <param name="inlineStart">Start of the first inline on the line, or -1 when none starts there.</param>
    /// <param name="quoteDepth">Number of enclosing block quotes.</param>
    /// <returns>The content start offset.</returns>
    private static int ResolveContentStart(string text, int lineStart, int lineEnd, int inlineStart, int quoteDepth)
    {
        // Exact: the first inline starts the content, provided only container prefix precedes it
        if (inlineStart >= lineStart && inlineStart <= lineEnd && IsContainerPrefix(text, lineStart, inlineStart))
        {
            return inlineStart;
        }

        // Fallback: strip one quote marker per enclosing quote, then whitespace
        var position = lineStart;
        for (var quote = 0; quote < quoteDepth; quote++)
        {
            position = SkipWhitespace(text, position, lineEnd);
            if (position >= lineEnd || text[position] != '>')
            {
                break;
            }

            position++;
            if (position < lineEnd && text[position] is ' ' or '\t')
            {
                position++;
            }
        }

        return SkipWhitespace(text, position, lineEnd);
    }

    /// <summary>
    ///     Determines whether a range contains only quote markers and whitespace.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Inclusive start offset.</param>
    /// <param name="end">Exclusive end offset.</param>
    /// <returns><see langword="true"/> when every character is <c>&gt;</c>, space or tab.</returns>
    private static bool IsContainerPrefix(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] is not ('>' or ' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Skips spaces and tabs.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="position">Offset to start at.</param>
    /// <param name="limit">Exclusive offset not to pass.</param>
    /// <returns>The offset of the first character that is not a space or tab (or the limit).</returns>
    private static int SkipWhitespace(string text, int position, int limit)
    {
        while (position < limit && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    /// <summary>
    ///     Removes the single optional space or tab after each quote marker so prefixes compare structurally.
    /// </summary>
    /// <param name="prefix">A container prefix.</param>
    /// <returns>The prefix without the optional spaces.</returns>
    private static string RemoveOptionalQuoteSpaces(string prefix)
    {
        var builder = new StringBuilder(prefix.Length);
        var skipNext = false;
        foreach (var c in prefix)
        {
            // The optional space belongs to the marker that precedes it
            if (skipNext && c is ' ' or '\t')
            {
                skipNext = false;
                continue;
            }

            builder.Append(c);
            skipNext = c == '>';
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Determines whether an actual continuation prefix is shorter than the expected one (a lazy line).
    /// </summary>
    /// <param name="actual">The actual prefix (optional quote spaces removed).</param>
    /// <param name="expected">The expected prefix (optional quote spaces removed).</param>
    /// <returns><see langword="true"/> when it has fewer quote markers or a smaller visual width.</returns>
    private static bool IsShorterPrefix(string actual, string expected) =>
        actual.Count(c => c == '>') < expected.Count(c => c == '>') || VisualWidth(actual) < VisualWidth(expected);

    /// <summary>
    ///     Measures the visual width of a prefix, advancing tabs to the next multiple of four.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The width in columns.</returns>
    private static int VisualWidth(string prefix)
    {
        var width = 0;
        foreach (var c in prefix)
        {
            width = c == '\t' ? (width / 4 + 1) * 4 : width + 1;
        }

        return width;
    }

    /// <summary>
    ///     Flattens a syntax tree into the filtered, source-ordered block list.
    /// </summary>
    private sealed class Flattener
    {
        /// <summary>
        ///     The source being flattened.
        /// </summary>
        private readonly SourceDocument _source;

        /// <summary>
        ///     The blocks produced so far.
        /// </summary>
        private readonly List<BlockInfo> _blocks = [];

        /// <summary>
        ///     Effective end offsets by parser block, used to bound unclosed fences by their parent.
        /// </summary>
        private readonly Dictionary<Block, int> _effectiveEnds = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        ///     The link reference definitions found by the walk, resolved once every container is known.
        /// </summary>
        private readonly List<LinkReferenceDefinition> _definitions = [];

        /// <summary>
        ///     Index of the leaf and footnote blocks, built while the dropped footnotes are detected.
        /// </summary>
        private IntervalIndex _leafOrFootnoteCoverage = IntervalIndex.Empty;

        /// <summary>
        ///     Index of all blocks, built while the dropped footnotes are detected.
        /// </summary>
        private IntervalIndex _anyBlockCoverage = IntervalIndex.Empty;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Flattener"/> class.
        /// </summary>
        /// <param name="source">The source document.</param>
        public Flattener(SourceDocument source)
        {
            _source = source;
        }

        /// <summary>
        ///     Flattens a document.
        /// </summary>
        /// <param name="ast">The syntax tree.</param>
        /// <returns>The blocks sorted by start offset (parents before children).</returns>
        public List<BlockInfo> Flatten(MarkdownDocument ast)
        {
            // Walk the tree, add the definitions (their containers are only known now), correct the
            // paragraphs that begin with definitions, then add the footnotes the parser dropped
            Walk(ast, parent: null, depth: 0);
            ExtendContainersToChildren();
            AddDefinitions();
            TrimParagraphsAfterDefinitions();
            AddUnreferencedFootnotes();

            // Sort by position; at the same position the shallower (enclosing) block comes first
            return [.. _blocks.OrderBy(static b => b.Start).ThenBy(static b => b.Depth)];
        }

        /// <summary>
        ///     Walks the children of a container, skipping synthetic shells and empty-span nodes.
        /// </summary>
        /// <param name="container">The container to walk.</param>
        /// <param name="parent">The block reported as the parent of the children (null at top level).</param>
        /// <param name="depth">Nesting depth of the children.</param>
        private void Walk(ContainerBlock container, Block? parent, int depth)
        {
            foreach (var child in container)
            {
                if (child is null)
                {
                    continue;
                }

                switch (child)
                {
                    // Reference definition groups are shells with garbage spans; their definitions carry the real
                    // spans but are kept at document root by the parser, so they are resolved later
                    case LinkReferenceDefinitionGroup group:
                        _definitions.AddRange(group.OfType<LinkReferenceDefinition>().Where(
                            static d => d is not FootnoteLinkReferenceDefinition && d.Span.End >= d.Span.Start));
                        break;

                    // Footnote groups are shells too; their footnotes belong to the group's parent
                    case FootnoteGroup footnotes:
                        Walk(footnotes, parent, depth);
                        break;

                    // Synthetic nodes have an empty span: skip the node but keep any real children
                    case ContainerBlock empty when child.Span.End < child.Span.Start:
                        Walk(empty, parent, depth);
                        break;

                    case Block when child.Span.End < child.Span.Start:
                        break;

                    default:
                        Add(child, parent, depth);

                        // Table rows and cells are not blocks of interest; everything else is walked
                        if (child is ContainerBlock nested and not Table)
                        {
                            Walk(nested, child, depth + 1);
                        }

                        break;
                }
            }
        }

        /// <summary>
        ///     Adds a block with its classification and effective end.
        /// </summary>
        /// <param name="block">The parser block.</param>
        /// <param name="parent">The parent block, or null at top level.</param>
        /// <param name="depth">Nesting depth.</param>
        private void Add(Block block, Block? parent, int depth)
        {
            var text = _source.Text;
            var kind = Classify(block, text);
            var start = Math.Clamp(block.Span.Start, 0, Math.Max(0, text.Length - 1));
            var end = Math.Clamp(block.Span.End, start, Math.Max(0, text.Length - 1));

            // A footnote's parser span starts after its marker; the block starts at the marker
            if (block is Footnote footnote)
            {
                start = FindFootnoteMarkerStart(footnote, start);
            }

            // An unclosed fence extends to the end of its parent container (or the end of the file)
            if (kind == BlockKind.UnclosedFence)
            {
                var parentEnd = parent != null && _effectiveEnds.TryGetValue(parent, out var effective) ? effective : text.Length - 1;
                end = Math.Max(end, parentEnd);
            }

            // Spans of lists, items and indented code may swallow the trailing newline; never trust it
            while (end > start && text[end] == '\n')
            {
                end--;
            }

            _effectiveEnds[block] = end;
            _blocks.Add(new BlockInfo(block, kind, start, end, depth, parent));
        }

        /// <summary>
        ///     Extends the end of every container to the end of its last emitted child.
        /// </summary>
        /// <remarks>
        ///     The parser's span of a container can end before a child that it nests (for example a
        ///     footnote followed by an indented HTML block), which would leave the child partly outside
        ///     its parent. The walk emits every block before its children, so visiting the blocks in
        ///     reverse order finishes each child (and its own children) before its parent is updated,
        ///     and an extension cascades through nested containers. Ends are only ever extended, so
        ///     the end remains the last visible character of the child.
        /// </remarks>
        private void ExtendContainersToChildren()
        {
            var indexes = new Dictionary<Block, int>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < _blocks.Count; i++)
            {
                if (_blocks[i].Node is { } node)
                {
                    indexes[node] = i;
                }
            }

            for (var i = _blocks.Count - 1; i >= 0; i--)
            {
                var child = _blocks[i];
                if (child.Parent is { } parent
                    && indexes.TryGetValue(parent, out var index)
                    && _blocks[index].EndInclusive < child.EndInclusive)
                {
                    _blocks[index] = _blocks[index] with { EndInclusive = child.EndInclusive };
                }
            }
        }

        /// <summary>
        ///     Finds the start of the <c>[^label]:</c> marker of a footnote.
        /// </summary>
        /// <param name="footnote">The parser's footnote block.</param>
        /// <param name="spanStart">The start of the footnote's own span (after the marker).</param>
        /// <returns>The offset of the marker's opening bracket, or <paramref name="spanStart"/> when it cannot be found.</returns>
        /// <remarks>
        ///     The parser reports the span of the label (inside the brackets); the bracket precedes it.
        ///     The result is validated against the text so a wrong span can never move the block.
        /// </remarks>
        private int FindFootnoteMarkerStart(Footnote footnote, int spanStart)
        {
            var text = _source.Text;
            var bracket = footnote.LabelSpan.Start - 1;
            return bracket >= 0 && bracket < spanStart && text[bracket] == '[' ? bracket : spanStart;
        }

        /// <summary>
        ///     Adds the link reference definitions, resolving the depth and parent of each from containment.
        /// </summary>
        /// <remarks>
        ///     The parser keeps every definition in a group at the document root even when the definition
        ///     stands inside a quote or list item, but the definition's range lies inside the container's
        ///     range. The parent is therefore the innermost block quote, list, list item or footnote that
        ///     contains the definition; top level when none does. The containers and the definitions are
        ///     both visited in order of their start with a stack of the containers that are still open, so
        ///     the cost is O(n log n) in the number of blocks instead of one scan of all containers per definition.
        /// </remarks>
        private void AddDefinitions()
        {
            if (_definitions.Count == 0)
            {
                return;
            }

            // Containers are all emitted by now; only they can be parents. Keep the emission order to break ties as a scan would.
            var containers = _blocks
                .Select(static (b, order) => (Block: b, Order: order))
                .Where(static c => c.Block.Node != null && c.Block.Kind is BlockKind.BlockQuote or BlockKind.List or BlockKind.ListItem or BlockKind.Footnote)
                .OrderBy(static c => c.Block.Start)
                .ToList();

            // The effective range of a definition is its own span (it is never a container)
            var limit = Math.Max(0, _source.Text.Length - 1);
            var ranges = new List<(int Start, int End, int Index)>(_definitions.Count);
            for (var i = 0; i < _definitions.Count; i++)
            {
                var start = Math.Clamp(_definitions[i].Span.Start, 0, limit);
                ranges.Add((start, Math.Clamp(_definitions[i].Span.End, start, limit), i));
            }

            ranges.Sort(static (a, b) => a.Start.CompareTo(b.Start));

            // Sweep the definitions in order of start with the stack of containers that start at or before the definition
            var owners = new BlockInfo?[_definitions.Count];
            var open = new List<(BlockInfo Block, int Order)>();
            var next = 0;
            foreach (var (start, end, index) in ranges)
            {
                while (next < containers.Count && containers[next].Block.Start <= start)
                {
                    open.Add(containers[next++]);
                }

                // A container that ended before this definition ends before every later one too
                open.RemoveAll(c => c.Block.EndInclusive < start);

                // Innermost = deepest; the earlier emitted block wins a tie
                (BlockInfo Block, int Order)? owner = null;
                foreach (var candidate in open)
                {
                    if (end <= candidate.Block.EndInclusive
                        && (owner is null || candidate.Block.Depth > owner.Value.Block.Depth
                            || (candidate.Block.Depth == owner.Value.Block.Depth && candidate.Order < owner.Value.Order)))
                    {
                        owner = candidate;
                    }
                }

                owners[index] = owner?.Block;
            }

            // Emit in the order the walk found the definitions
            for (var i = 0; i < _definitions.Count; i++)
            {
                var owner = owners[i];
                Add(_definitions[i], owner?.Node, owner is null ? 0 : owner.Depth + 1);
            }
        }

        /// <summary>
        ///     Moves the start of every paragraph or heading that begins with link reference definitions to the text after them.
        /// </summary>
        /// <remarks>
        ///     The parser keeps the definitions at the start of the paragraph (or setext heading) that
        ///     follows them, so that block's range overlaps the definition blocks. The block keeps its
        ///     parser node. The container prefix of the first text line (whitespace and as many block
        ///     quote markers as enclosing quotes) is stepped over; a further <c>&gt;</c> is content.
        /// </remarks>
        private void TrimParagraphsAfterDefinitions()
        {
            var text = _source.Text;
            var definitions = _blocks.Where(static b => b.Node is LinkReferenceDefinition).OrderBy(static b => b.Start).ToList();
            if (definitions.Count == 0)
            {
                return;
            }

            // The definitions are sorted by start, so those inside a paragraph follow a binary search of its start
            var definitionStarts = definitions.Select(static d => d.Start).ToArray();
            for (var i = 0; i < _blocks.Count; i++)
            {
                var paragraph = _blocks[i];
                if (paragraph.Kind is not (BlockKind.Paragraph or BlockKind.Heading))
                {
                    continue;
                }

                // The paragraph's real text starts at the line after its last definition (past the container prefix)
                var lastEnd = -1;
                for (var d = LowerBound(definitionStarts, paragraph.Start); d < definitions.Count && definitions[d].Start <= paragraph.EndInclusive; d++)
                {
                    if (definitions[d].EndInclusive <= paragraph.EndInclusive)
                    {
                        lastEnd = Math.Max(lastEnd, definitions[d].EndInclusive);
                    }
                }

                var newline = lastEnd < 0 ? -1 : text.IndexOf('\n', lastEnd);
                if (newline < 0)
                {
                    continue;
                }

                // Step over whitespace and at most one marker per enclosing block quote; any later '>' is text
                var quotes = CountQuoteAncestors(paragraph);
                var start = newline + 1;
                while (start < paragraph.EndInclusive)
                {
                    if (text[start] is ' ' or '\t')
                    {
                        start++;
                    }
                    else if (text[start] == '>' && quotes > 0)
                    {
                        quotes--;
                        start++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (start <= paragraph.EndInclusive)
                {
                    _blocks[i] = paragraph with { Start = start };
                }
            }
        }

        /// <summary>
        ///     Classifies a parser block.
        /// </summary>
        /// <param name="block">The parser block.</param>
        /// <param name="text">The document text.</param>
        /// <returns>The block kind.</returns>
        private static BlockKind Classify(Block block, string text) => block switch
        {
            YamlFrontMatterBlock => BlockKind.FrontMatter,
            FencedCodeBlock { ClosingFencedCharCount: 0 } => BlockKind.UnclosedFence,
            MathBlock => BlockKind.MathBlock,
            FencedCodeBlock => BlockKind.FencedCode,
            CodeBlock => BlockKind.IndentedCode,
            HeadingBlock => BlockKind.Heading,
            ParagraphBlock => BlockKind.Paragraph,
            HtmlBlock => BlockKind.HtmlBlock,
            ThematicBreakBlock => BlockKind.ThematicBreak,
            QuoteBlock => BlockKind.BlockQuote,
            ListBlock => BlockKind.List,
            ListItemBlock => BlockKind.ListItem,
            Table => block.Span.Start < text.Length && text[block.Span.Start] == '+' ? BlockKind.GridTable : BlockKind.Table,
            Footnote => BlockKind.Footnote,

            // Inside a quote or list item the parser reads a footnote definition as a link reference definition
            LinkReferenceDefinition { Label: ['^', _, ..] label } when !label.Any(char.IsWhiteSpace) => BlockKind.UnreferencedFootnote,
            LinkReferenceDefinition => BlockKind.LinkReferenceDefinition,
            _ => BlockKind.Other
        };

        /// <summary>
        ///     Adds a block for every footnote definition that no block covers (the parser produces no node for it).
        /// </summary>
        /// <remarks>
        ///     The parser drops a footnote definition that nothing references and a definition that repeats
        ///     an earlier label. Detection does not compare labels: a definition marker that lies neither
        ///     inside a leaf block nor inside a footnote block is not covered and gets a block.
        /// </remarks>
        private void AddUnreferencedFootnotes()
        {
            // Cheap exit for the overwhelmingly common case
            var text = _source.Text;
            if (!text.Contains("[^", StringComparison.Ordinal))
            {
                return;
            }

            var found = new List<BlockInfo>();
            _leafOrFootnoteCoverage = new IntervalIndex(_blocks.Where(static b => IsLeaf(b.Kind) || b.Kind == BlockKind.Footnote));
            _anyBlockCoverage = new IntervalIndex(_blocks);
            var lineStarts = _source.LineStarts;
            for (var line = 0; line < lineStarts.Count; line++)
            {
                var match = MatchDefinition(line);
                if (match is null)
                {
                    continue;
                }

                // The marker must sit in a gap: a definition-looking line inside a leaf block is just text
                var markerStart = lineStarts[line] + match.Groups["indent"].Length;
                if (IsCoveredByLeafOrFootnote(markerStart))
                {
                    continue;
                }

                found.Add(new BlockInfo(null, BlockKind.UnreferencedFootnote, markerStart, FindDefinitionEnd(line), 0, null));
            }

            _blocks.AddRange(found);
        }

        /// <summary>
        ///     Matches a footnote definition marker at the start of a line.
        /// </summary>
        /// <param name="line">Zero-based line index.</param>
        /// <returns>The match, or <see langword="null"/> when the line does not start a definition.</returns>
        private Match? MatchDefinition(int line)
        {
            var (start, end) = LineBounds(line);
            if (end - start < 5)
            {
                return null;
            }

            var match = FootnoteDefinitionRegex().Match(_source.Text, start, end - start);
            return match.Success ? match : null;
        }

        /// <summary>
        ///     Finds the last character of an unreferenced footnote definition.
        /// </summary>
        /// <param name="line">Zero-based index of the line holding the marker.</param>
        /// <returns>
        ///     The offset of the last character: the end of the marker line, extended over following
        ///     lines that are not covered by any block and do not start another definition (the parser
        ///     drops continuation lines and indented paragraphs together with the definition). After a
        ///     blank line only an indented line continues the definition.
        /// </returns>
        private int FindDefinitionEnd(int line)
        {
            var text = _source.Text;
            var end = LineBounds(line).End - 1;
            var afterBlank = false;
            for (var next = line + 1; next < _source.LineStarts.Count; next++)
            {
                var (start, lineEnd) = LineBounds(next);
                if (text.AsSpan(start, lineEnd - start).IsWhiteSpace())
                {
                    afterBlank = true;
                    continue;
                }

                var indented = text[start] == '\t' || (lineEnd - start >= 4 && text.AsSpan(start, 4).SequenceEqual("    "));
                if (IsInsideAnyBlock(start) || MatchDefinition(next) is not null || (afterBlank && !indented))
                {
                    break;
                }

                end = lineEnd - 1;
                afterBlank = false;
            }

            return end;
        }

        /// <summary>
        ///     Gets the offsets of a line without its terminator.
        /// </summary>
        /// <param name="line">Zero-based line index.</param>
        /// <returns>Start (inclusive) and end (exclusive) offsets.</returns>
        private (int Start, int End) LineBounds(int line)
        {
            var starts = _source.LineStarts;
            var end = line + 1 < starts.Count ? starts[line + 1] - 1 : _source.Text.Length;
            return (starts[line], end);
        }

        /// <summary>
        ///     Determines whether an offset lies inside a leaf (non-container) block or a footnote block.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns><see langword="true"/> when such a block covers the offset.</returns>
        private bool IsCoveredByLeafOrFootnote(int offset) => _leafOrFootnoteCoverage.Contains(offset);

        /// <summary>
        ///     Determines whether an offset lies inside any block.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns><see langword="true"/> when a block covers the offset.</returns>
        private bool IsInsideAnyBlock(int offset) => _anyBlockCoverage.Contains(offset);

        /// <summary>
        ///     Determines whether a kind is a leaf (not a container) block.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <returns><see langword="true"/> for leaf kinds.</returns>
        private static bool IsLeaf(BlockKind kind) =>
            kind is not (BlockKind.BlockQuote or BlockKind.List or BlockKind.ListItem or BlockKind.Footnote);

        /// <summary>
        ///     Finds the first position in a sorted array whose value is not less than a key.
        /// </summary>
        /// <param name="sorted">The values in ascending order.</param>
        /// <param name="key">The key.</param>
        /// <returns>The index of the first value that is at least <paramref name="key"/>, or the length when there is none.</returns>
        private static int LowerBound(int[] sorted, int key)
        {
            var low = 0;
            var high = sorted.Length;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (sorted[middle] < key)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        ///     Answers whether an offset lies inside any of a set of inclusive ranges in O(log n).
        /// </summary>
        /// <remarks>
        ///     The ranges are sorted by start and each position keeps the largest end seen up to it, so an
        ///     offset is covered exactly when the largest end among the ranges that start at or before the
        ///     offset reaches it. The ranges may overlap or nest.
        /// </remarks>
        private sealed class IntervalIndex
        {
            /// <summary>
            ///     The starts in ascending order.
            /// </summary>
            private readonly int[] _starts;

            /// <summary>
            ///     For each position, the largest inclusive end of the ranges up to and including it.
            /// </summary>
            private readonly int[] _maxEnds;

            /// <summary>
            ///     Initializes a new instance of the <see cref="IntervalIndex"/> class.
            /// </summary>
            /// <param name="blocks">The blocks whose ranges are indexed.</param>
            public IntervalIndex(IEnumerable<BlockInfo> blocks)
            {
                var ordered = blocks.OrderBy(static b => b.Start).ToArray();
                _starts = new int[ordered.Length];
                _maxEnds = new int[ordered.Length];
                var maxEnd = int.MinValue;
                for (var i = 0; i < ordered.Length; i++)
                {
                    maxEnd = Math.Max(maxEnd, ordered[i].EndInclusive);
                    _starts[i] = ordered[i].Start;
                    _maxEnds[i] = maxEnd;
                }
            }

            /// <summary>
            ///     Gets an index that covers nothing.
            /// </summary>
            public static IntervalIndex Empty { get; } = new([]);

            /// <summary>
            ///     Determines whether an offset lies inside one of the ranges.
            /// </summary>
            /// <param name="offset">The offset.</param>
            /// <returns><see langword="true"/> when a range covers the offset.</returns>
            public bool Contains(int offset)
            {
                // The last range that starts at or before the offset carries the largest end of all candidates
                var last = LowerBound(_starts, offset + 1) - 1;
                return last >= 0 && _maxEnds[last] >= offset;
            }
        }
    }
}
