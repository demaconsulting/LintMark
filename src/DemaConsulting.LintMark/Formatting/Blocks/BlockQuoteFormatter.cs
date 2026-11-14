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
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes block quotes in the canonical form: exactly one space after each quote marker.
/// </summary>
/// <remarks>
///     <para>
///         Two to four spaces after a quote marker become one, on the paragraph and ATX heading lines of a quote
///         (a nested quote's markers included, <c>&gt;  &gt;  text</c> becomes <c>&gt; &gt; text</c>). The formatter
///         accepts the outermost quote of a nest and handles the leaves of the nested quotes itself, and it claims
///         no lines: the other rules keep editing the text of the lines it changes, and its edits replace only the
///         run of spaces after a marker.
///     </para>
///     <para>
///         Guards keep a rewrite from changing the meaning of the document. Only a leaf whose every enclosing
///         block is a block quote is handled (after a list marker or inside a footnote the spaces are indentation of the
///         container), and only a paragraph or an ATX heading: indentation after the marker is content in code, HTML and
///         math blocks and is part of the structure of setext headings and tables. A line that has a tab in its prefix, a
///         run of five or more spaces (the line would be indented code), no content, or that continues the paragraph
///         lazily (it has fewer markers than its quote) is left as written. At most three spaces beyond the one that the
///         marker owns change, and a block start needs fewer than four, so which block the line starts cannot change.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class BlockQuoteFormatter : IBlockFormatter
{
    /// <summary>
    ///     The fewest spaces after a marker that are collapsed (one space is canonical).
    /// </summary>
    private const int MinRun = 2;

    /// <summary>
    ///     The most spaces after a marker that are collapsed (five would be indented code).
    /// </summary>
    private const int MaxRun = 4;

    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } = [RuleCatalog.BlockQuoteSpaceRuleId];

    /// <summary>
    ///     Determines whether a block is the outermost block quote of a nest.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for a block quote that is not inside another block quote.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Kind == BlockKind.BlockQuote && block.Node is QuoteBlock && block.Parent is not QuoteBlock;
    }

    /// <summary>
    ///     Proposes the edits that bring a block quote into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The block quote. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not accepted by <see cref="CanFormat"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be an outermost block quote, not {block.Kind}.", nameof(block));
        }

        // A footnote definition in a quote is read inconsistently (its body depends on columns), so such a quote stays as written
        var document = context.Document;
        var layout = context.Layout;
        var firstLine = layout.LineOf(block.Start);
        var lastLine = layout.LineOf(block.EndInclusive);
        for (var line = firstLine; line <= lastLine; line++)
        {
            if (LineLayout.HasFootnoteDefinitionMarker(layout.Text.AsSpan(layout.LineStart(line), layout.LineEnd(line) - layout.LineStart(line))))
            {
                return;
            }
        }

        // Visit the leaves that lie inside the quote; the blocks are in source order, so a bisection finds the first
        var blocks = document.Blocks;
        var low = 0;
        var high = blocks.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (blocks[middle].Start < block.Start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var i = low; i < blocks.Length && blocks[i].Start <= block.EndInclusive; i++)
        {
            var leaf = blocks[i];
            if (leaf.EndInclusive <= block.EndInclusive && IsHandledLeaf(leaf))
            {
                FormatLeaf(document, leaf, edits);
            }
        }
    }

    /// <summary>
    ///     Determines whether a block is a paragraph or ATX heading whose every enclosing block is a block quote.
    /// </summary>
    /// <param name="leaf">The block.</param>
    /// <returns><see langword="true"/> when the spaces after the quote markers of its lines may be collapsed.</returns>
    private static bool IsHandledLeaf(BlockInfo leaf)
    {
        // Only paragraphs and ATX headings: everywhere else the spaces after a marker can be content or structure
        if (leaf.Node is not (ParagraphBlock or HeadingBlock { IsSetext: false }) || leaf.Kind is not (BlockKind.Paragraph or BlockKind.Heading))
        {
            return false;
        }

        // Every enclosing block up to the document must be a block quote (a list, footnote or table owns the spaces)
        for (var parent = leaf.Node.Parent; parent is not null and not MarkdownDocument; parent = parent.Parent)
        {
            if (parent is not QuoteBlock)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Adds the edits for the lines of one leaf block.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="leaf">The paragraph or ATX heading.</param>
    /// <param name="edits">Receives the edits.</param>
    private static void FormatLeaf(ParsedDocument document, BlockInfo leaf, EditSet edits)
    {
        // The number of markers a line needs to be part of the quote is the number of quotes around the leaf
        var depth = 0;
        for (var parent = leaf.Node!.Parent; parent is QuoteBlock; parent = parent.Parent)
        {
            depth++;
        }

        var text = document.Source.Text;
        foreach (var line in DocumentParser.GetLines(document, leaf))
        {
            // A line without content is the whitespace rule's
            if (line.ContentStart >= line.ContentEnd)
            {
                continue;
            }

            // A lazy line has fewer markers than quotes around it, and no marker to collapse the spaces after (the number of
            // markers, not the width of the prefix, decides: the width depends on the spaces this rule removes)
            var prefix = text.AsSpan(line.LineStart, line.ContentStart - line.LineStart);
            if (prefix.Contains('\t') || prefix.Count('>') != depth)
            {
                continue;
            }

            // Collapse the run after each marker of the prefix that is two to four spaces long
            var position = 0;
            while (position < prefix.Length)
            {
                if (prefix[position] != '>')
                {
                    position++;
                    continue;
                }

                var end = position + 1;
                while (end < prefix.Length && prefix[end] == ' ')
                {
                    end++;
                }

                var run = end - position - 1;
                if (run is >= MinRun and <= MaxRun)
                {
                    edits.Add(new TextEdit(line.LineStart + position + 1, run, " ", RuleCatalog.BlockQuoteSpaceRuleId));
                }

                position = end;
            }
        }
    }
}
