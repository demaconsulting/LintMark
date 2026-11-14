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
using System.Globalization;
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Inline;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Safety;
using DemaConsulting.LintMark.Utilities;
using Markdig;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Wraps a paragraph that has a line wider than the line length, filling its lines up to the width and leaving
///     every paragraph that does not overflow exactly as written.
/// </summary>
/// <remarks>
///     <para>
///         This is the <c>line-length</c> rule in its <c>fill</c> mode, and the <c>paragraph-wrap</c> rule in the
///         <c>sentence</c> and <c>semantic</c> modes. In the <c>fill</c> mode a paragraph is rewritten only when one of its lines
///         is wider (in display columns, container prefix included) than the line length and could be broken, so
///         hand-wrapped text that fits is never reflowed. In the <c>sentence</c> and <c>semantic</c> modes (an opt-in) every
///         eligible paragraph has one canonical layout (see <see cref="LineWrapper"/>): a paragraph that is not laid out that way is
///         a finding of the <c>paragraph-wrap</c> rule and is rewritten, while a line that is too wide and could be broken stays a
///         finding of the <c>line-length</c> rule; the two rules are switched on and off, and suppressed, independently, and either
///         one is enough to rewrite a paragraph. A line length of 0 means no limit, and a layout without width.
///         The words of the paragraph come from the
///         <see cref="AtomTokenizer"/> (a link, a code span or an inline HTML tag is never split), are laid out by the
///         <see cref="LineWrapper"/> (which keeps block syntax away from the start of a line, see
///         <see cref="BlockSyntaxGuard"/>), and are written back with the container prefix of the paragraph on every line.
///         A line that has only one atom (a long link or URL) is never reported or changed. A hard line break stays
///         where it is.
///     </para>
///     <para>
///         The formatter is not an <see cref="IBlockFormatter"/>: it claims no lines, because it works on the text that the
///         other rules produce (the document formatter gives it that text) and a claim would silence their rules for every
///         paragraph. It keeps a paragraph as written when any rule that edits that text is suppressed in it
///         (<see cref="RuleIds"/>), when the paragraph holds Pandoc structure (a line that starts a fenced div, a LaTeX
///         command or a display math fence), lies in a footnote, or holds a tab. It also leaves a paragraph as written, and
///         reports its overflow as a problem that cannot be fixed, when it cannot be rewritten safely: the tokenizer
///         refuses it, a continuation line of a list item is lazy (rewriting would let the list rule move the item on
///         the next run), a line after a hard break would start with block syntax, or the stand-alone paragraph does not
///         render the same after the wrap.
///     </para>
///     <para>
///         Thread safety: the instance holds only immutable data; <see cref="Format"/> keeps its state in local variables, so one
///         instance may serve every worker thread.
///     </para>
/// </remarks>
internal sealed class ParagraphFormatter
{
    /// <summary>
    ///     The identifier of the rule that wraps paragraphs.
    /// </summary>
    private const string LineLengthRule = RuleCatalog.LineLengthRuleId;

    /// <summary>
    ///     The width that stands for no limit (a line length of 0), large enough that no line reaches it.
    /// </summary>
    private const int Unlimited = int.MaxValue / 2;

    /// <summary>
    ///     The characters that separate the words of a line and that never count towards its width at its end.
    /// </summary>
    private static readonly char[] Blanks = [' ', '\t'];

    /// <summary>
    ///     Decides whether the paragraph as written and the wrapped paragraph, each read on its own, render the same.
    /// </summary>
    private readonly Func<string, string, bool> _rendersTheSame;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParagraphFormatter"/> class.
    /// </summary>
    public ParagraphFormatter()
        : this(RenderTheSame)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParagraphFormatter"/> class with the proof of a wrap supplied by the caller.
    /// </summary>
    /// <param name="rendersTheSame">
    ///     Tells whether the paragraph as written (first argument) and the wrapped paragraph (second argument), each as stand-alone
    ///     LF-normalized text, render the same. Must not be null.
    /// </param>
    /// <remarks>
    ///     A seam for tests, so that a wrap that the proof refuses can be simulated; the shipped tool uses the proof of the
    ///     parameterless constructor, which renders both texts with the pipeline of the tool and compares the pages.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rendersTheSame"/> is null.</exception>
    internal ParagraphFormatter(Func<string, string, bool> rendersTheSame)
    {
        ArgumentNullException.ThrowIfNull(rendersTheSame);
        _rendersTheSame = rendersTheSame;
    }

    /// <summary>
    ///     Gets the identifiers of the rules that edit the text of a paragraph, so that a suppression of any of them keeps the
    ///     paragraph as written.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.LineLengthRuleId,
        RuleCatalog.TrailingWhitespaceRuleId,
        RuleCatalog.HardTabsRuleId,
        RuleCatalog.EmphasisRuleId,
        RuleCatalog.StrongRuleId,
        RuleCatalog.BareUrlsRuleId,
        RuleCatalog.CodeSpanSpaceRuleId,
        RuleCatalog.ListIndentRuleId,
        RuleCatalog.ListMarkerSpaceRuleId,
        RuleCatalog.BlockQuoteSpaceRuleId,
        RuleCatalog.ParagraphWrapRuleId
    ];

    /// <summary>
    ///     Determines whether the settings ask for paragraphs to be wrapped.
    /// </summary>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the line length is not 0 and the <c>line-length</c> rule is switched on, or when the wrap mode
    ///     is <c>sentence</c> or <c>semantic</c> and the <c>paragraph-wrap</c> rule is switched on.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static bool IsEnabled(LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(config);

        return (config.LineLength > 0 && config.Rules[RuleCatalog.LineLengthRuleId])
            || (config.Wrap != WrapMode.Fill && config.Rules[RuleCatalog.ParagraphWrapRuleId]);
    }

    /// <summary>
    ///     Determines whether the settings ask for every paragraph to be laid out the way the wrap mode says.
    /// </summary>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the wrap mode is <c>sentence</c> or <c>semantic</c> and the <c>paragraph-wrap</c> rule is
    ///     switched on: then a paragraph is checked whatever the width of its lines.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static bool IsLayoutEnforced(LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(config);

        return config.Wrap != WrapMode.Fill && config.Rules[RuleCatalog.ParagraphWrapRuleId];
    }

    /// <summary>
    ///     Decides what to do about the lines of one paragraph that are wider than the line length.
    /// </summary>
    /// <param name="context">The document, its settings and line arithmetic. Must not be null.</param>
    /// <param name="block">A paragraph block of the document of <paramref name="context"/>. Must not be null.</param>
    /// <returns>
    ///     The edit that wraps the paragraph (against the text of the document) and where its first line that is too wide
    ///     is; <see cref="ParagraphOutcome.None"/> when the paragraph needs no report and no edit.
    /// </returns>
    /// <remarks>
    ///     Pure: nothing is written and the document is not changed. That <paramref name="block"/> is one of the blocks of
    ///     the document is a precondition that is not verified.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a paragraph.</exception>
    public ParagraphOutcome Format(FormatContext context, BlockInfo block)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        if (block.Kind != BlockKind.Paragraph)
        {
            throw new ArgumentException($"The block must be a paragraph, not {block.Kind}.", nameof(block));
        }

        // Only a paragraph with inline content, outside a footnote and with no suppressed rule that edits its text
        var document = context.Document;
        var config = context.Config;
        if (!IsEnabled(config) || block.Node is not ParagraphBlock { Inline: { } inline } || IsExempt(context, block))
        {
            return ParagraphOutcome.None;
        }

        // The rules that ask for a wrap: the line length (a line that is too wide) and the layout of the wrap mode (every paragraph)
        var suppressions = document.Suppressions;
        var limit = config.LineLength;
        var lengthActive = limit > 0
            && config.Rules[LineLengthRule]
            && !suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, LineLengthRule);
        var layoutActive = IsLayoutEnforced(config)
            && !suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, RuleCatalog.ParagraphWrapRuleId);

        // Measure every line; a paragraph in which no line is too wide needs nothing unless its layout is enforced
        var text = document.Source.Text;
        var lines = DocumentParser.GetLines(document, block);
        var widths = MeasureLines(text, lines);
        var tooWide = lengthActive && widths.Any(width => width > limit);
        if (!tooWide && !layoutActive)
        {
            return ParagraphOutcome.None;
        }

        // Tabs cannot be measured, and a Pandoc structural line must stay where it is
        var end = block.EndInclusive + 1;
        var layout = context.Layout;
        var firstLineStart = layout.LineStart(layout.LineOf(block.Start));
        var continuationPrefix = DocumentParser.GetContinuationPrefix(document, block);
        if (text.AsSpan(firstLineStart, end - firstLineStart).Contains('\t')
            || continuationPrefix.Contains('\t')
            || lines.Any(line => BlockSyntaxGuard.IsPandocStructuralLine(text[line.ContentStart..line.ContentEnd])))
        {
            return ParagraphOutcome.None;
        }

        // Find the first line that is too wide and has a place to break; without the atoms any line with a blank inside will do
        var tokenized = AtomTokenizer.Tokenize(text, inline, lines, config.LinkRefactor.BreakLinkText);
        var overflowLine = tooWide ? FindOverflowLine(text, lines, widths, limit, tokenized) : -1;
        if (overflowLine < 0 && !layoutActive)
        {
            return ParagraphOutcome.None;
        }

        var offset = overflowLine >= 0 ? FindOffsetBeyond(text, lines[overflowLine], limit) : -1;
        var overflowWidth = overflowLine >= 0 ? widths[overflowLine] : 0;

        // A paragraph that cannot be rewritten safely is reported and left as written (its layout is not reported: it is not known)
        if (tokenized is null || IsLazyInListItem(block, lines) || StartsLineWithBlockSyntax(tokenized))
        {
            return overflowLine >= 0 ? new ParagraphOutcome(null, offset, overflowWidth, hasResidualOverflow: true) : ParagraphOutcome.None;
        }

        // Wrap every segment in the layout of the mode, each with the widths of the lines it starts
        var firstPrefix = text[firstLineStart..block.Start];
        var firstWidth = limit > 0 ? Math.Max(1, limit - TextWidth.Of(firstPrefix)) : Unlimited;
        var continuationWidth = limit > 0 ? Math.Max(1, limit - TextWidth.Of(continuationPrefix)) : Unlimited;
        var wrapped = new List<string>();
        var overflow = false;
        foreach (var segment in tokenized.Segments)
        {
            if (segment.Atoms.IsEmpty)
            {
                continue;
            }

            var result = LineWrapper.Wrap(segment.Atoms, wrapped.Count == 0 ? firstWidth : continuationWidth, continuationWidth, config.Wrap);
            overflow |= result.HasBreakableOverflow;
            var segmentLines = result.Lines.ToArray();
            if (segment.End == HardBreakKind.Spaces)
            {
                segmentLines[^1] += "  ";
            }

            wrapped.AddRange(segmentLines);
        }

        // Nothing changes when the lines are already what the wrap would write
        var original = text[block.Start..end];
        var replacement = string.Join("\n" + continuationPrefix, wrapped);
        if (string.Equals(replacement, original, StringComparison.Ordinal))
        {
            return overflowLine >= 0 ? new ParagraphOutcome(null, offset, overflowWidth, overflow) : ParagraphOutcome.None;
        }

        // A paragraph that is not in the layout of the mode is a finding of its own, at the first line that differs
        var wrapOffset = layoutActive ? FindFirstDifference(text, lines, wrapped) : -1;
        var residual = overflowLine >= 0;

        // The wrap stands only when the stand-alone paragraph renders the same afterwards, and so does the paragraph inside
        // its list or quote (a parser may read the first line of a quote differently when it is followed by more text)
        if (!_rendersTheSame(StandAloneText(text, lines), string.Join('\n', wrapped))
            || !RenderTheSameInContainer(text[firstLineStart..end], firstPrefix + replacement))
        {
            return new ParagraphOutcome(null, offset, overflowWidth, residual, wrapOffset);
        }

        var rule = wrapOffset >= 0 ? RuleCatalog.ParagraphWrapRuleId : LineLengthRule;
        return new ParagraphOutcome(new TextEdit(block.Start, end - block.Start, replacement, rule), offset, overflowWidth, residual && overflow, wrapOffset);
    }

    /// <summary>
    ///     Finds the start of the first line of a paragraph that differs from the lines the wrap would write.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph as written.</param>
    /// <param name="wrapped">The lines the wrap writes, without container prefix.</param>
    /// <returns>The offset of the start of the first line that differs (or, when the lines only differ in count, of the last line).</returns>
    private static int FindFirstDifference(string text, IReadOnlyList<LeafLine> lines, List<string> wrapped)
    {
        for (var i = 0; i < lines.Count && i < wrapped.Count; i++)
        {
            if (!string.Equals(text[lines[i].ContentStart..lines[i].ContentEnd], wrapped[i], StringComparison.Ordinal))
            {
                return lines[i].LineStart;
            }
        }

        return lines[Math.Min(Math.Min(lines.Count, wrapped.Count), lines.Count - 1)].LineStart;
    }

    /// <summary>
    ///     Determines whether a paragraph is outside the rule: it lies in a footnote, in a protected block, or a rule that edits its text is suppressed in it.
    /// </summary>
    /// <param name="context">The document and its line arithmetic.</param>
    /// <param name="block">The paragraph.</param>
    /// <returns><see langword="true"/> when the paragraph is left alone and nothing is reported.</returns>
    private bool IsExempt(FormatContext context, BlockInfo block)
    {
        var firstLine = context.Layout.LineOf(block.Start);
        if (context.Ownership.IsInFootnote(firstLine) || context.Ownership.IsProtected(firstLine))
        {
            return true;
        }

        var suppressions = context.Document.Suppressions;
        return RuleIds.Any(rule => rule is not (LineLengthRule or RuleCatalog.ParagraphWrapRuleId)
            && suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, rule));
    }

    /// <summary>
    ///     Measures the display width of every line of a paragraph, container prefix included and trailing blanks excluded.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns>The widths in order.</returns>
    private static int[] MeasureLines(string text, IReadOnlyList<LeafLine> lines)
    {
        var widths = new int[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            widths[i] = TextWidth.Of(text[lines[i].LineStart..lines[i].ContentEnd].TrimEnd(Blanks));
        }

        return widths;
    }

    /// <summary>
    ///     Finds the first line that is wider than the line length and has a place where it could be broken.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <param name="widths">The widths of the lines.</param>
    /// <param name="limit">The line length.</param>
    /// <param name="tokenized">The tokenized paragraph, or <see langword="null"/> when it could not be tokenized.</param>
    /// <returns>
    ///     The index of the line; -1 when no line qualifies. Without a tokenization a line qualifies when its content
    ///     holds a blank between two other characters.
    /// </returns>
    private static int FindOverflowLine(string text, IReadOnlyList<LeafLine> lines, int[] widths, int limit, TokenizedParagraph? tokenized)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (widths[i] <= limit)
            {
                continue;
            }

            var breakable = tokenized is not null
                ? tokenized.BreakOpportunitiesPerLine[i] > 0
                : text.AsSpan(lines[i].ContentStart, lines[i].ContentEnd - lines[i].ContentStart).Trim(Blanks).IndexOfAny(Blanks) >= 0;
            if (breakable)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Finds the offset of the first character that lies beyond the line length on a line.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="line">The line, which is wider than <paramref name="limit"/>.</param>
    /// <param name="limit">The line length.</param>
    /// <returns>The offset of the first character (grapheme cluster) whose right edge is beyond the limit.</returns>
    private static int FindOffsetBeyond(string text, LeafLine line, int limit)
    {
        var position = line.LineStart;
        var column = 0;
        while (position < line.ContentEnd)
        {
            var length = StringInfo.GetNextTextElementLength(text.AsSpan(position, line.ContentEnd - position));
            column += TextWidth.Of(text.Substring(position, length));
            if (column > limit)
            {
                return position;
            }

            position += length;
        }

        return Math.Max(line.LineStart, line.ContentEnd - 1);
    }

    /// <summary>
    ///     Determines whether a continuation line that is lazy lies in a list item.
    /// </summary>
    /// <param name="block">The paragraph.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns>
    ///     <see langword="true"/> when a line lacks the prefix its container requires and a list item contains the paragraph:
    ///     writing the prefix would make the list rule treat the item differently on the next run.
    /// </returns>
    private static bool IsLazyInListItem(BlockInfo block, IReadOnlyList<LeafLine> lines)
    {
        if (!lines.Any(static line => line.IsLazy))
        {
            return false;
        }

        for (var parent = block.Node?.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ListItemBlock)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a line that follows a hard line break would start with block syntax.
    /// </summary>
    /// <param name="tokenized">The tokenized paragraph.</param>
    /// <returns>
    ///     <see langword="true"/> when the first atom of a segment after the first is refused by the guard: such a line start
    ///     cannot be moved, so the paragraph is not rewritten.
    /// </returns>
    private static bool StartsLineWithBlockSyntax(TokenizedParagraph tokenized) =>
        tokenized.Segments.Skip(1).Any(static segment => !segment.Atoms.IsEmpty && BlockSyntaxGuard.IsGuardedLineStart(segment.Atoms[0]));

    /// <summary>
    ///     Rebuilds the paragraph as written as a stand-alone text: the content of each line, without container prefix, joined with line feeds.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns>The text.</returns>
    private static string StandAloneText(string text, IReadOnlyList<LeafLine> lines)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            builder.Append(i > 0 ? "\n" : string.Empty).Append(text, lines[i].ContentStart, lines[i].ContentEnd - lines[i].ContentStart);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Proves that the wrapped paragraph, read inside its list or block quote, renders the same as the paragraph it replaces.
    /// </summary>
    /// <param name="original">The lines of the paragraph as written, with their container prefixes.</param>
    /// <param name="wrapped">The wrapped lines, with their container prefixes.</param>
    /// <returns>
    ///     <see langword="false"/> when the original is read as one list or one block quote and the wrapped text is read as
    ///     anything else or renders differently; <see langword="true"/> otherwise (a paragraph that is not inside a list or a
    ///     quote, or whose prefixes make the lines read as code on their own, is decided by the stand-alone proof).
    /// </returns>
    private static bool RenderTheSameInContainer(string original, string wrapped)
    {
        var pipeline = DocumentParser.Pipeline;
        var before = Markdown.Parse(original, pipeline);
        if (before is not ([QuoteBlock] or [ListBlock]))
        {
            return true;
        }

        var after = Markdown.Parse(wrapped, pipeline);
        return after.Count == 1
            && after[0].GetType() == before[0].GetType()
            && HtmlEquivalenceChecker.Check(original, wrapped).IsEquivalent;
    }

    /// <summary>
    ///     Proves that the wrapped paragraph, read on its own, renders the same as the paragraph it replaces.
    /// </summary>
    /// <param name="original">The paragraph as written, as stand-alone text.</param>
    /// <param name="wrapped">The wrapped paragraph, as stand-alone text.</param>
    /// <returns>
    ///     <see langword="true"/> when both texts are read as exactly one paragraph and render to the same HTML after
    ///     whitespace is normalized.
    /// </returns>
    private static bool RenderTheSame(string original, string wrapped)
    {
        var pipeline = DocumentParser.Pipeline;
        if (Markdown.Parse(original, pipeline) is not [ParagraphBlock] || Markdown.Parse(wrapped, pipeline) is not [ParagraphBlock])
        {
            return false;
        }

        return HtmlEquivalenceChecker.Check(original, wrapped).IsEquivalent;
    }
}
