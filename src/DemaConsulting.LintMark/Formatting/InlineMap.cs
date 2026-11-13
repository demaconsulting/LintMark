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

using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     What the inline syntax tree of one leaf block says about its characters: which spans are plain text,
///     which spans must never be edited, and where the line breaks are.
/// </summary>
/// <remarks>
///     <para>
///         Whitespace rules may only touch whitespace that the Markdown parser treats as plain text. Code spans,
///         inline HTML, math, autolink URLs, link destinations and titles keep their whitespace as it is, because it
///         is part of the rendered result. The map answers three questions about an offset of the original text:
///         is it plain text, is it protected, and does a line break start there (and is that break a hard break
///         written with spaces).
///     </para>
///     <para>
///         Spans are offsets into the LF-normalized text with inclusive ends, as the parser reports them (the
///         pipeline keeps precise source locations). A line break inline starts at the offset of its line feed;
///         a break that is hard because of trailing spaces is distinguished from a backslash break, which takes no
///         trailing whitespace.
///     </para>
///     <para>
///         Thread safety: immutable after <see cref="Build"/>; safe for concurrent reads.
///     </para>
/// </remarks>
internal sealed class InlineMap
{
    /// <summary>
    ///     Sorted, non-overlapping spans of plain text (inclusive ends).
    /// </summary>
    private readonly (int Start, int End)[] _text;

    /// <summary>
    ///     Sorted, non-overlapping spans that must never be edited (inclusive ends).
    /// </summary>
    private readonly (int Start, int End)[] _protected;

    /// <summary>
    ///     The line breaks by the offset of their line feed; the value says whether the break is a hard break written with spaces.
    /// </summary>
    private readonly Dictionary<int, bool> _breaks;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InlineMap"/> class.
    /// </summary>
    /// <param name="text">Plain text spans, sorted and merged.</param>
    /// <param name="protectedSpans">Protected spans, sorted and merged.</param>
    /// <param name="breaks">The line breaks.</param>
    private InlineMap((int Start, int End)[] text, (int Start, int End)[] protectedSpans, Dictionary<int, bool> breaks)
    {
        _text = text;
        _protected = protectedSpans;
        _breaks = breaks;
    }

    /// <summary>
    ///     Gets an empty map: no text, nothing protected, no breaks.
    /// </summary>
    public static InlineMap Empty { get; } = new([], [], []);

    /// <summary>
    ///     Builds the map of an inline tree.
    /// </summary>
    /// <param name="root">The inline content of a leaf block, or <see langword="null"/> when the block has none.</param>
    /// <param name="shift">A constant added to every offset the parser reports, for the rare block whose spans are known to be off by a fixed amount (zero for none).</param>
    /// <returns>The map; <see cref="Empty"/> when <paramref name="root"/> is null.</returns>
    /// <remarks>
    ///     Literal text and line breaks are the editable parts. Emphasis and similar containers are walked
    ///     into. A link is walked into, but its destination, title and label are protected; the parser reports the
    ///     bracketed text of an inline link as its label, so the whitespace inside link text stays as written.
    ///     Every other inline (code, HTML, math, autolink URLs, footnote references, task markers) is protected as a whole.
    /// </remarks>
    public static InlineMap Build(ContainerInline? root, int shift = 0)
    {
        if (root is null)
        {
            return Empty;
        }

        var text = new List<(int, int)>();
        var guarded = new List<(int, int)>();
        var breaks = new Dictionary<int, bool>();
        Visit(root, shift, text, guarded, breaks);
        return new InlineMap(Merge(text), Merge(guarded), breaks);
    }

    /// <summary>
    ///     Determines whether an offset lies in plain text.
    /// </summary>
    /// <param name="offset">The offset into the text.</param>
    /// <returns><see langword="true"/> when a literal inline covers the offset.</returns>
    public bool IsText(int offset) => Contains(_text, offset);

    /// <summary>
    ///     Determines whether an offset lies in a span that must never be edited.
    /// </summary>
    /// <param name="offset">The offset into the text.</param>
    /// <returns><see langword="true"/> when the offset is inside code, inline HTML, math, a link destination, title or label, or another protected inline.</returns>
    public bool IsProtected(int offset) => Contains(_protected, offset);

    /// <summary>
    ///     Determines whether a line break inline starts at an offset.
    /// </summary>
    /// <param name="lineFeed">The offset of a line feed.</param>
    /// <returns><see langword="true"/> for a soft break, a hard break written with spaces or a backslash break.</returns>
    public bool IsBreakAt(int lineFeed) => _breaks.ContainsKey(lineFeed);

    /// <summary>
    ///     Determines whether a hard break written with trailing spaces starts at an offset.
    /// </summary>
    /// <param name="lineFeed">The offset of a line feed.</param>
    /// <returns><see langword="true"/> for a hard break that two or more trailing spaces make; <see langword="false"/> for any other break or no break.</returns>
    public bool IsSpaceBreakAt(int lineFeed) => _breaks.TryGetValue(lineFeed, out var spaces) && spaces;

    /// <summary>
    ///     Walks the children of an inline container and records what each child contributes.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="shift">The constant added to every offset.</param>
    /// <param name="text">Receives the plain text spans.</param>
    /// <param name="guarded">Receives the protected spans.</param>
    /// <param name="breaks">Receives the line breaks.</param>
    private static void Visit(ContainerInline container, int shift, List<(int, int)> text, List<(int, int)> guarded, Dictionary<int, bool> breaks)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LiteralInline literal:
                    AddSpan(text, literal.Span.Start, literal.Span.End, shift);
                    break;

                case LineBreakInline lineBreak:
                    breaks[lineBreak.Span.Start + shift] = lineBreak.IsHard && !lineBreak.IsBackslash;
                    break;

                // A link keeps its text editable; the destination, title and label are part of the rendered result
                case LinkInline link:
                    AddSpan(guarded, link.UrlSpan.Start, link.UrlSpan.End, shift);
                    AddSpan(guarded, link.TitleSpan.Start, link.TitleSpan.End, shift);
                    AddSpan(guarded, link.LabelSpan.Start, link.LabelSpan.End, shift);
                    Visit(link, shift, text, guarded, breaks);
                    break;

                // Emphasis and other containers only wrap text
                case ContainerInline nested:
                    Visit(nested, shift, text, guarded, breaks);
                    break;

                default:
                    AddSpan(guarded, child.Span.Start, child.Span.End, shift);
                    break;
            }
        }
    }

    /// <summary>
    ///     Adds a span to a list when it is valid.
    /// </summary>
    /// <param name="spans">The list.</param>
    /// <param name="start">First offset of the span.</param>
    /// <param name="end">Last offset of the span (inclusive).</param>
    /// <param name="shift">The constant added to both offsets of a valid span.</param>
    private static void AddSpan(List<(int, int)> spans, int start, int end, int shift)
    {
        if (start >= 0 && end >= start)
        {
            spans.Add((start + shift, end + shift));
        }
    }

    /// <summary>
    ///     Sorts spans and merges those that overlap or touch.
    /// </summary>
    /// <param name="spans">The spans.</param>
    /// <returns>Sorted, non-overlapping spans.</returns>
    private static (int Start, int End)[] Merge(List<(int Start, int End)> spans)
    {
        spans.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        var merged = new List<(int Start, int End)>(spans.Count);
        foreach (var span in spans)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End + 1)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            }
            else
            {
                merged.Add(span);
            }
        }

        return [.. merged];
    }

    /// <summary>
    ///     Determines whether sorted, non-overlapping spans cover an offset.
    /// </summary>
    /// <param name="spans">The spans.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="true"/> when a span covers the offset.</returns>
    private static bool Contains((int Start, int End)[] spans, int offset)
    {
        var low = 0;
        var high = spans.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (offset < spans[middle].Start)
            {
                high = middle - 1;
            }
            else if (offset > spans[middle].End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
