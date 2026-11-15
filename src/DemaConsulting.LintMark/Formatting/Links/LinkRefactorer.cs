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
using System.Text;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Linting;
using DemaConsulting.LintMark.Linting.Rules;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting.Links;

/// <summary>
///     Converts inline links and images to reference links (the opt-in <c>link-refactor</c> setting), keeping every
///     definition in one block at the end of the document.
/// </summary>
/// <remarks>
///     <para>
///         <b>Which links.</b> A link in a paragraph is converted when it is written inline (<c>[text](destination "title")</c>), its
///         whole source is wider than the <c>threshold</c> (in display columns), and its destination part is longer than the
///         reference that replaces it. A link in a table cell is converted only when <c>in-tables</c> is on and its row, in the
///         compact layout, is wider than the line length, and only when the conversion of every convertible link of the row
///         makes it fit; one row is judged on its own. Reference links, autolinks, bare URLs, links in headings, code,
///         HTML, footnotes and protected blocks, links whose destination part is not on one line or uses a backslash or an
///         entity, links in an image description, and links in a block with the rule suppressed are left as they are.
///         The decision depends only on the text of the link and of its row, never on the labels that happen to be
///         free, so a second run converts nothing more.
///     </para>
///     <para>
///         <b>What is written.</b> Only the destination part <c>(destination "title")</c> is replaced, by <c>[label]</c>; the
///         text of the link, with its brackets, emphasis and code, stays byte for byte. The label comes from the
///         <see cref="LinkLabelAllocator"/>. The definitions are one line each, sorted by label, and written below the
///         marker comment <c>&lt;!-- Link References --&gt;</c> (configurable) in one block at the end of the document, after
///         one blank line. When the document already has that marker, the new definitions are added after the last definition
///         that follows it. Other definition blocks (such as a badge block) and every existing definition stay exactly as
///         they are, and no definition is ever removed.
///     </para>
///     <para>
///         <b>Safety.</b> The labels never collide with a label or bracketed text that the parser does not read as a link
///         (see <see cref="LinkLabelAllocator"/>), and the definitions are written only where a block can be added safely.
///         Otherwise nothing is converted.
///     </para>
///     <para>
///         Thread safety: the instance holds only immutable data; <see cref="Plan"/> keeps its state in local variables, so one
///         instance may serve every worker thread.
///     </para>
/// </remarks>
internal sealed class LinkRefactorer
{
    /// <summary>
    ///     The width of the brackets around a label.
    /// </summary>
    private const int BracketWidth = 2;

    /// <summary>
    ///     Determines whether the settings ask for links to be converted.
    /// </summary>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <returns><see langword="true"/> when <c>link-refactor.enabled</c> is on and the <c>link-refactor</c> rule is switched on.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static bool IsEnabled(LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(config);

        return config.LinkRefactor.Enabled && config.Rules[RuleCatalog.LinkRefactorRuleId];
    }

#pragma warning disable S2325 // An instance is registered with the document formatter, like a block formatter: the contract is instance-based
    /// <summary>
    ///     Decides which links of a document are converted and writes the edits.
    /// </summary>
    /// <param name="document">The parsed document, as the other rules leave it. Must not be null.</param>
    /// <param name="config">The effective configuration of the file. Must not be null.</param>
    /// <returns>
    ///     The plan: edits against the text of the document. <see cref="LinkRefactorPlan.Empty"/> when the feature is off, nothing
    ///     qualifies, or the definitions cannot be placed safely.
    /// </returns>
    /// <remarks>Pure: nothing is written and the document is not changed.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public LinkRefactorPlan Plan(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        if (!IsEnabled(config) || document.IsFormatUnsafe)
        {
            return LinkRefactorPlan.Empty;
        }

        var source = document.Source;
        var layout = new LineLayout(source);
        var ownership = LineOwnership.Build(document, layout);
        var units = CollectUnits(document, config, layout, ownership);
        if (units.Count == 0)
        {
            return LinkRefactorPlan.Empty;
        }

        // Label every converted link in document order
        var options = config.LinkRefactor;
        var labels = new LinkLabelAllocator(document, options.LabelMaxLength);
        var edits = new EditSet();
        var conversions = new List<LinkConversion>();
        foreach (var unit in units)
        {
            foreach (var candidate in unit.Items)
            {
                var label = labels.Allocate(candidate.BaseLabel, candidate.Tail);
                edits.Add(new TextEdit(candidate.Tail.Start, candidate.Tail.End - candidate.Tail.Start, $"[{label}]", RuleCatalog.LinkRefactorRuleId));
            }

            conversions.AddRange(unit.Items.Select(candidate => new LinkConversion(candidate.Offset, unit.Message)));
        }

        // The new definitions go after the last definition of the marker block, or into a new block at the end of the document
        if (labels.NewLabels.Count > 0)
        {
            if (TryPlaceDefinitions(document, layout, ownership, config, labels.DefinitionLines) is not { } placement)
            {
                return LinkRefactorPlan.Empty;
            }

            edits.Add(placement);
        }

        return new LinkRefactorPlan(
            edits.ToImmutable(),
            [.. conversions.OrderBy(static conversion => conversion.Offset)],
            units.Any(static unit => unit.IsTable));
    }
#pragma warning restore S2325

    /// <summary>
    ///     Finds the links that are converted, grouped the way they are decided.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines are protected or in a footnote.</param>
    /// <returns>One unit for each link of a paragraph and one for each table row that needs its links converted, in document order.</returns>
    private static List<Unit> CollectUnits(ParsedDocument document, LintMarkConfig config, LineLayout layout, LineOwnership ownership)
    {
        var units = new List<Unit>();
        var options = config.LinkRefactor;
        var text = document.Source.Text;
        foreach (var block in document.Blocks)
        {
            if (block.Kind is not (BlockKind.Paragraph or BlockKind.Table)
                || IsExempt(document, layout, ownership, block))
            {
                continue;
            }

            if (block.Node is ParagraphBlock { Inline: { } inline })
            {
                foreach (var link in inline.Descendants<LinkInline>())
                {
                    if (TryCreate(text, link, options.LabelMaxLength) is not { } candidate)
                    {
                        continue;
                    }

                    var width = TextWidth.Of(Flatten(text.AsSpan(link.Span.Start, candidate.Tail.End - link.Span.Start)));
                    if (width > options.Threshold)
                    {
                        units.Add(new Unit([candidate], false, $"Inline link is {width} columns wide (threshold {options.Threshold}); use a reference link"));
                    }
                }
            }
            else if (block.Node is Table table && options.InTables && config.LineLength > 0
                     && !document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, RuleCatalog.TableColumnStyleRuleId))
            {
                AddTableUnits(text, layout, block, table, config, units);
            }
        }

        return units;
    }

    /// <summary>
    ///     Finds the rows of a table that are too wide in the compact layout and can be made to fit by converting their links.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="block">The table block.</param>
    /// <param name="table">The parser's table.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="units">Receives one unit for each row that qualifies.</param>
    private static void AddTableUnits(string text, LineLayout layout, BlockInfo block, Table table, LintMarkConfig config, List<Unit> units)
    {
        if (PipeTable.TryParse(layout, block) is not { } pipe)
        {
            return;
        }

        var compact = pipe.ComposeCompact();
        var links = table.Descendants<LinkInline>().ToList();
        for (var index = 0; index < pipe.Rows.Length; index++)
        {
            // Only a row that does not fit when compact is considered, and it is judged by what its links would save
            var row = pipe.Rows[index];
            var width = TableFormatter.PrefixWidth(row.Prefix) + TextWidth.Of(compact[index]);
            if (width <= config.LineLength)
            {
                continue;
            }

            var candidates = links
                .Where(link => link.Span.Start >= row.ContentStart && link.Span.End < row.ContentEnd)
                .Select(link => TryCreate(text, link, config.LinkRefactor.LabelMaxLength))
                .OfType<Candidate>()
                .ToImmutableArray();
            var rest = width - candidates.Sum(static candidate => candidate.Saving);
            if (!candidates.IsEmpty && rest <= config.LineLength)
            {
                units.Add(new Unit(candidates, true, $"Table row is {width} columns wide (limit {config.LineLength}); use reference links"));
            }
        }
    }

    /// <summary>
    ///     Determines whether a block is left alone: in a footnote, protected, or with the rule suppressed.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines are protected or in a footnote.</param>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> when no link of the block is converted.</returns>
    private static bool IsExempt(ParsedDocument document, LineLayout layout, LineOwnership ownership, BlockInfo block)
    {
        var line = layout.LineOf(block.Start);
        return ownership.IsInFootnote(line)
               || ownership.IsProtected(line)
               || document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, RuleCatalog.LinkRefactorRuleId);
    }

    /// <summary>
    ///     Reads a link as a candidate for conversion.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="link">The link or image.</param>
    /// <param name="maxLabel">The longest label.</param>
    /// <returns>
    ///     The candidate; <see langword="null"/> when the link is not an inline link that is read with certainty, lies in the
    ///     description of an image, or its destination part is not longer than the reference that would replace it.
    /// </returns>
    private static Candidate? TryCreate(string text, LinkInline link, int maxLabel)
    {
        // A link inside an image description is part of a description, not a link to convert
        for (var parent = link.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline { IsImage: true })
            {
                return null;
            }
        }

        if (InlineLinkTail.TryRead(text, link) is not { } tail)
        {
            return null;
        }

        // The conversion has to make the link shorter: compared with the label that the text alone gives, which is the same on every run
        var label = LinkLabelAllocator.DeriveBase(RuleSupport.PlainText(link), tail.Url, maxLabel);
        var saving = TextWidth.Of(text.Substring(tail.Start, tail.End - tail.Start)) - (BracketWidth + TextWidth.Of(label));
        return saving > 0 ? new Candidate(tail, label, link.Span.Start, saving) : null;
    }

    /// <summary>
    ///     Reduces the source of a link to one line: every line break and the container marker after it become one blank.
    /// </summary>
    /// <param name="source">The source of the link.</param>
    /// <returns>The text on one line.</returns>
    private static string Flatten(ReadOnlySpan<char> source)
    {
        if (!source.Contains('\n'))
        {
            return source.ToString();
        }

        var builder = new StringBuilder(source.Length);
        var afterBreak = false;
        foreach (var c in source)
        {
            // The container markers and blanks that start the next line are not part of the link
            if (afterBreak && c is '>' or ' ' or '\t')
            {
                continue;
            }

            afterBreak = c == '\n';
            builder.Append(c == '\n' ? ' ' : c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Finds where the new definitions are written.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="layout">The line arithmetic of the document.</param>
    /// <param name="ownership">Which lines are protected or in a footnote.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="lines">The definition lines, sorted.</param>
    /// <returns>The edit, or <see langword="null"/> when the end of the document is not a place where a block can be added safely.</returns>
    private static TextEdit? TryPlaceDefinitions(
        ParsedDocument document,
        LineLayout layout,
        LineOwnership ownership,
        LintMarkConfig config,
        IReadOnlyList<string> lines)
    {
        var text = layout.Text;
        var marker = config.LinkRefactor.DefinitionsMarker;
        var body = string.Join('\n', lines);

        // A marker that is already there takes the definitions after the last definition that follows it
        var topLevel = document.Blocks.Where(static block => block.Parent is null && block.Depth == 0).ToList();
        var existing = topLevel.LastOrDefault(block => block.Kind == BlockKind.HtmlBlock
            && string.Equals(text.AsSpan(block.Start, block.EndInclusive + 1 - block.Start).Trim().ToString(), marker, StringComparison.Ordinal));
        if (existing is not null)
        {
            var lastLine = layout.LineOf(existing.EndInclusive);
            foreach (var block in topLevel.Where(block => block.Start > existing.EndInclusive))
            {
                if (block.Kind != BlockKind.LinkReferenceDefinition || layout.LineOf(block.Start) != lastLine + 1)
                {
                    break;
                }

                lastLine = layout.LineOf(block.EndInclusive);
            }

            return new TextEdit(layout.LineEnd(lastLine), 0, "\n" + body, RuleCatalog.LinkRefactorRuleId);
        }

        // Otherwise a new block follows the last line with content, which must be a line a block may follow
        var lastContent = layout.LineCount - 1;
        while (lastContent >= 0 && layout.IsBlank(lastContent) && ownership.LeafAt(lastContent) is null)
        {
            lastContent--;
        }

        if (lastContent < 0)
        {
            return null;
        }

        var owner = ownership.LeafAt(lastContent);
        if ((owner is not null && LineOwnership.KeepsTrailingBlankLines(layout, owner, contentFollows: false))
            || ownership.IsInFootnote(lastContent))
        {
            return null;
        }

        // The end of the text must be the final line ending as configured: anything else is left to the other rules
        var trailerStart = layout.LineEnd(lastContent);
        var finalEnding = config.FinalNewline ? "\n" : string.Empty;
        if (!text.AsSpan(trailerStart).SequenceEqual(finalEnding))
        {
            return null;
        }

        return new TextEdit(trailerStart, text.Length - trailerStart, "\n\n" + marker + "\n" + body + finalEnding, RuleCatalog.LinkRefactorRuleId);
    }

    /// <summary>
    ///     A link that can be converted.
    /// </summary>
    /// <param name="Tail">Its destination part.</param>
    /// <param name="BaseLabel">The label its text gives.</param>
    /// <param name="Offset">Where the link starts.</param>
    /// <param name="Saving">How many columns the conversion saves, with the label that the text gives.</param>
    private sealed record Candidate(InlineLinkTail Tail, string BaseLabel, int Offset, int Saving);

    /// <summary>
    ///     Links that are converted together, or not at all.
    /// </summary>
    /// <param name="Items">The links.</param>
    /// <param name="IsTable">Whether they are the links of a table row.</param>
    /// <param name="Message">What is reported about each of them.</param>
    private sealed record Unit(ImmutableArray<Candidate> Items, bool IsTable, string Message);
}
