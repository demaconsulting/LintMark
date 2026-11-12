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

using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Document;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test support that checks the span-tiling and containment properties of a parsed document.
/// </summary>
/// <remarks>
///     "Tiling" is the meaningful reading of "re-emitting every block's span reproduces the file":
///     the effective block spans must be valid, ordered, contained in their parents and must account
///     for all source text, i.e. every character outside the spans is whitespace (top level) or
///     container prefix text (quote markers, list markers) inside containers. A footnote's own range
///     includes its <c>[^label]:</c> marker, so the marker is never an uncovered gap; it only appears
///     as prefix text before the first child of a footnote. Parentage is checked too: a block without a
///     parent is top level and lies in no container, and a block with a parent is exactly one level
///     deeper than that parent. These are the properties a span-patching formatter depends on.
/// </remarks>
internal static class SpanTilingAssertions
{
    /// <summary>
    ///     Maximum time any single expression may take.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Text allowed before a block on its first line: container markers and whitespace.
    /// </summary>
    private static readonly Regex PrefixRegex = new(
        @"^(?:[ \t]*(?:>|(?:[-+*]|\d{1,9}[.)])[ \t]))*[ \t]*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Text allowed before a child of a footnote on its first line: the footnote's own marker, as well as container markers and whitespace.
    /// </summary>
    private static readonly Regex FootnoteChildPrefixRegex = new(
        @"^(?:[ \t]*(?:>|(?:[-+*]|\d{1,9}[.)])[ \t]|\[\^[^\]\s]+\]:))*[ \t]*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Text allowed between the children of a container (and before the first / after the last).
    /// </summary>
    private static readonly Regex GapRegex = new(
        @"^(?:[ \t\r\n>]|[-+*]|\d{1,9}[.)]|\[![A-Za-z]+\])*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Text allowed between the children of a footnote: the gap text plus the footnote's own marker before its first child.
    /// </summary>
    private static readonly Regex FootnoteGapRegex = new(
        @"^(?:[ \t\r\n>]|[-+*]|\d{1,9}[.)]|\[\^[^\]\s]+\]:|\[![A-Za-z]+\])*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Text allowed between top-level blocks: whitespace only.
    /// </summary>
    private static readonly Regex RootGapRegex = new(
        @"^\s*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     A list item must start with its marker.
    /// </summary>
    private static readonly Regex ListMarkerRegex = new(
        @"^(?:[-+*]|\d{1,9}[.)])(?:[ \t]|$|\n)", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     The last line of a setext heading is its underline.
    /// </summary>
    private static readonly Regex SetextUnderlineRegex = new(
        @"^[ \t>]*(?:=+|-+)[ \t]*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Checks all tiling and containment properties of a parsed document.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>One message per violation, formatted <c>path:line:col: description</c>; empty when the document passes.</returns>
    public static IReadOnlyList<string> Check(ParsedDocument document)
    {
        var violations = new List<string>();
        var source = document.Source;
        var text = source.Text;
        void Fail(int offset, string description)
        {
            var (line, column) = source.PositionOf(Math.Clamp(offset, 0, text.Length));
            violations.Add($"{source.Path}:{line}:{column}: {description}");
        }

        // Spans must be valid before anything else can be checked
        var valid = new List<BlockInfo>();
        foreach (var block in document.Blocks)
        {
            if (block.Start < 0 || block.EndInclusive < block.Start || block.EndInclusive >= text.Length)
            {
                Fail(block.Start, $"{block.Kind}: invalid span [{block.Start}..{block.EndInclusive}]");
            }
            else
            {
                valid.Add(block);
            }
        }

        var byNode = new Dictionary<Block, BlockInfo>(ReferenceEqualityComparer.Instance);
        foreach (var block in valid.Where(b => b.Node != null))
        {
            byNode[block.Node!] = block;
        }

        foreach (var block in valid)
        {
            CheckStartAndEnd(source, block, Fail);
            CheckMarkers(text, block, Fail);
            CheckContainment(block, byNode, Fail);
            CheckParentage(block, valid, byNode, Fail);
        }

        CheckSiblingsAndGaps(source, valid, byNode, Fail);
        return violations;
    }

    /// <summary>
    ///     Checks only the nesting properties of a parsed document: spans are valid and ordered, siblings
    ///     are disjoint, every block lies inside its parent and parentage and depth are consistent. Any two
    ///     blocks are disjoint or nested.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>One message per violation, formatted <c>path:line:col: description</c>; empty when the document passes.</returns>
    /// <remarks>
    ///     Unlike <see cref="Check"/> this does not require that all source text is accounted for: adversarial
    ///     documents (text that looks like a footnote, invalid tables) legitimately leave text between blocks.
    /// </remarks>
    public static IReadOnlyList<string> CheckNesting(ParsedDocument document)
    {
        var violations = new List<string>();
        var source = document.Source;
        var text = source.Text;
        void Fail(int offset, string description)
        {
            var (line, column) = source.PositionOf(Math.Clamp(offset, 0, text.Length));
            violations.Add($"{source.Path}:{line}:{column}: {description}");
        }

        // Spans must be valid before anything else can be checked
        var valid = new List<BlockInfo>();
        foreach (var block in document.Blocks)
        {
            if (block.Start < 0 || block.EndInclusive < block.Start || block.EndInclusive >= text.Length)
            {
                Fail(block.Start, $"{block.Kind}: invalid span [{block.Start}..{block.EndInclusive}]");
            }
            else
            {
                valid.Add(block);
            }
        }

        // Blocks must be listed in source order, an enclosing block before the blocks it encloses
        for (var i = 1; i < valid.Count; i++)
        {
            if (valid[i].Start < valid[i - 1].Start || (valid[i].Start == valid[i - 1].Start && valid[i].Depth < valid[i - 1].Depth))
            {
                Fail(valid[i].Start, $"{valid[i].Kind}: blocks are not in source order");
            }
        }

        var byNode = new Dictionary<Block, BlockInfo>(ReferenceEqualityComparer.Instance);
        foreach (var block in valid.Where(b => b.Node != null))
        {
            byNode[block.Node!] = block;
        }

        foreach (var block in valid)
        {
            CheckContainment(block, byNode, Fail);
            CheckParentage(block, valid, byNode, Fail);
        }

        // Siblings (blocks with the same parent) must be disjoint
        foreach (var group in valid.GroupBy(b => b.Parent, ReferenceEqualityComparer.Instance as IEqualityComparer<Block?>))
        {
            BlockInfo? previous = null;
            foreach (var kid in group.OrderBy(b => b.Start).ThenBy(b => b.Depth))
            {
                if (previous != null && kid.Start <= previous.EndInclusive)
                {
                    Fail(kid.Start, $"{previous.Kind}/{kid.Kind}: siblings overlap");
                }

                previous = kid;
            }
        }

        // Any two blocks are disjoint or nested, and the innermost block that is open at a block is its parent
        // (this also catches a block that starts inside a block without a node, which can never be its parent)
        var open = new Stack<BlockInfo>();
        foreach (var block in valid)
        {
            while (open.Count > 0 && open.Peek().EndInclusive < block.Start)
            {
                open.Pop();
            }

            if (open.Count > 0 && block.EndInclusive > open.Peek().EndInclusive)
            {
                Fail(block.Start, $"{open.Peek().Kind}/{block.Kind}: blocks overlap without nesting");
            }
            else if (open.Count > 0 && (open.Peek().Node is null || !ReferenceEquals(open.Peek().Node, block.Parent)))
            {
                Fail(block.Start, $"{block.Kind}: lies inside {open.Peek().Kind}, which is not its parent");
            }
            else if (open.Count == 0 && block.Parent != null)
            {
                Fail(block.Start, $"{block.Kind}: has a parent but lies inside no block");
            }

            open.Push(block);
        }

        return violations;
    }

    /// <summary>
    ///     Checks that a block without a parent is top level and lies in no container, and that a block with a parent is one level deeper than it.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="blocks">All valid blocks.</param>
    /// <param name="byNode">Blocks by parser node.</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckParentage(BlockInfo block, List<BlockInfo> blocks, Dictionary<Block, BlockInfo> byNode, Action<int, string> fail)
    {
        if (block.Parent != null)
        {
            if (!byNode.TryGetValue(block.Parent, out var parent))
            {
                fail(block.Start, $"{block.Kind}: parent is not a block of the document");
            }
            else if (block.Depth != parent.Depth + 1)
            {
                fail(block.Start, $"{block.Kind}: depth {block.Depth} but parent {parent.Kind} has depth {parent.Depth}");
            }

            return;
        }

        // Top level: depth zero, and no container at the same or a shallower level may contain the block
        // (a container deeper than the block is its own child, e.g. the item of a one-item list)
        if (block.Depth != 0)
        {
            fail(block.Start, $"{block.Kind}: has no parent but depth {block.Depth}");
        }

        var container = blocks.FirstOrDefault(other => !ReferenceEquals(other, block)
            && other.Depth <= block.Depth
            && other.Kind is BlockKind.BlockQuote or BlockKind.List or BlockKind.ListItem or BlockKind.Footnote
            && other.Start <= block.Start && block.EndInclusive <= other.EndInclusive);
        if (container != null)
        {
            fail(block.Start, $"{block.Kind}: has no parent but lies inside {container.Kind} [{container.Start}..{container.EndInclusive}]");
        }
    }

    /// <summary>
    ///     Checks that only container prefix precedes a block and that it ends at the end of a line.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block.</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckStartAndEnd(SourceDocument source, BlockInfo block, Action<int, string> fail)
    {
        var text = source.Text;
        var lineStart = source.LineStarts[source.PositionOf(block.Start).Line - 1];
        var prefix = text[lineStart..block.Start];

        // The marker of a footnote precedes only the first child of that footnote
        var prefixRegex = block.Parent is Footnote ? FootnoteChildPrefixRegex : PrefixRegex;
        if (block.Kind != BlockKind.FrontMatter && !prefixRegex.IsMatch(prefix))
        {
            fail(block.Start, $"{block.Kind}: unexpected text before block start \"{Escape(prefix)}\"");
        }

        if (text[block.EndInclusive] == '\n')
        {
            fail(block.EndInclusive, $"{block.Kind}: effective end is a newline");
        }

        var after = block.EndInclusive + 1;
        if (after < text.Length && text[after] != '\n')
        {
            fail(block.EndInclusive, $"{block.Kind}: block ends in the middle of a line");
        }
    }

    /// <summary>
    ///     Checks that list items start on their marker and headings on their marker or underline.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="block">The block.</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckMarkers(string text, BlockInfo block, Action<int, string> fail)
    {
        var raw = text.Substring(block.Start, block.EndInclusive - block.Start + 1);
        if (block.Kind == BlockKind.ListItem && !ListMarkerRegex.IsMatch(raw[..Math.Min(raw.Length, 14)]))
        {
            fail(block.Start, "ListItem: span does not start at the list marker");
        }

        if (block.Kind == BlockKind.Heading && block.Node is HeadingBlock heading)
        {
            var ok = heading.IsSetext
                ? SetextUnderlineRegex.IsMatch(raw.Split('\n')[^1])
                : Regex.IsMatch(raw, "^#{" + heading.Level + @"}(?:[ \t]|$)", RegexOptions.CultureInvariant, RegexTimeout);
            if (!ok)
            {
                fail(block.Start, $"Heading: level {heading.Level} does not match its marker");
            }
        }
    }

    /// <summary>
    ///     Checks that a block lies inside its parent.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="byNode">Blocks by parser node.</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckContainment(BlockInfo block, Dictionary<Block, BlockInfo> byNode, Action<int, string> fail)
    {
        if (block.Parent != null && byNode.TryGetValue(block.Parent, out var parent)
            && (block.Start < parent.Start || block.EndInclusive > parent.EndInclusive))
        {
            fail(block.Start, $"{block.Kind}: [{block.Start}..{block.EndInclusive}] is not contained in parent {parent.Kind} [{parent.Start}..{parent.EndInclusive}]");
        }
    }

    /// <summary>
    ///     Checks that siblings are ordered and disjoint and that the text between them is only container prefix.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="blocks">The valid blocks.</param>
    /// <param name="byNode">Blocks by parser node.</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckSiblingsAndGaps(SourceDocument source, List<BlockInfo> blocks, Dictionary<Block, BlockInfo> byNode, Action<int, string> fail)
    {
        var text = source.Text;
        foreach (var group in blocks.GroupBy(b => b.Parent, ReferenceEqualityComparer.Instance as IEqualityComparer<Block?>))
        {
            var isRoot = group.Key == null;
            var parent = isRoot ? null : byNode.GetValueOrDefault(group.Key!);
            var gapRegex = parent?.Kind == BlockKind.Footnote ? FootnoteGapRegex : GapRegex;
            if (isRoot)
            {
                gapRegex = RootGapRegex;
            }
            var position = parent?.Start ?? 0;
            var end = parent != null ? parent.EndInclusive + 1 : text.Length;
            var parentName = parent?.Kind.ToString() ?? "Document";
            BlockInfo? previous = null;
            foreach (var kid in group.OrderBy(b => b.Start).ThenBy(b => b.Depth))
            {
                if (previous != null && kid.Start <= previous.EndInclusive)
                {
                    fail(kid.Start, $"{parentName}>{previous.Kind}/{kid.Kind}: siblings overlap");
                    continue;
                }

                CheckGap(text, position, Math.Min(kid.Start, end), gapRegex, parentName, kid.Kind.ToString(), fail);
                position = Math.Max(position, kid.EndInclusive + 1);
                previous = kid;
            }

            CheckGap(text, position, end, gapRegex, parentName, "(tail)", fail);
        }
    }

    /// <summary>
    ///     Checks the text of one gap.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="start">Inclusive start of the gap.</param>
    /// <param name="end">Exclusive end of the gap.</param>
    /// <param name="gapRegex">The allowed gap content.</param>
    /// <param name="parentName">Name of the enclosing container (for the message).</param>
    /// <param name="nextName">Name of the following block (for the message).</param>
    /// <param name="fail">Reports a violation.</param>
    private static void CheckGap(string text, int start, int end, Regex gapRegex, string parentName, string nextName, Action<int, string> fail)
    {
        if (end > start && !gapRegex.IsMatch(text.Substring(start, end - start)))
        {
            var gap = text.Substring(start, Math.Min(end - start, 60));
            fail(start, $"{parentName}>{nextName}: source text not covered by any block \"{Escape(gap)}\"");
        }
    }

    /// <summary>
    ///     Escapes control characters for a readable message.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The escaped text.</returns>
    private static string Escape(string value) =>
        value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
}
