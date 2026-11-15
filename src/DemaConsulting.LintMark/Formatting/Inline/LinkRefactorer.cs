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
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting.Blocks;
using DemaConsulting.LintMark.Linting;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting.Inline;

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

/// <summary>
///     What the link refactoring decided for a document: the edits, where each link was converted and whether a table changed.
/// </summary>
/// <param name="Edits">The edits against the text the plan was made for, ordered and without conflicts.</param>
/// <param name="Conversions">One entry for each link that is converted, in document order.</param>
/// <param name="ChangesTables">Whether a link in a table cell is converted (the table must be laid out again).</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed record LinkRefactorPlan(ImmutableArray<TextEdit> Edits, ImmutableArray<LinkConversion> Conversions, bool ChangesTables)
{
    /// <summary>
    ///     Gets the plan that changes nothing.
    /// </summary>
    public static LinkRefactorPlan Empty { get; } = new([], [], false);

    /// <summary>
    ///     Gets a value indicating whether the plan changes anything.
    /// </summary>
    public bool IsEmpty => Edits.IsDefaultOrEmpty;
}

/// <summary>
///     One link that is converted to a reference link.
/// </summary>
/// <param name="Offset">Offset of the link in the text the plan was made for.</param>
/// <param name="Message">What is reported about the link.</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed record LinkConversion(int Offset, string Message);

/// <summary>
///     Chooses the labels of reference links: derives a label from the text of a link, reuses the label of a definition that
///     already has the same destination and title, and keeps every new label different from every label and bracketed text
///     that the document already has.
/// </summary>
/// <remarks>
///     <para>
///         A label is the link text in lower case with every run of characters that are not letters or digits replaced by one
///         hyphen, cut to the maximum length at a hyphen; when nothing is left, the host and first path segment of the
///         destination are used the same way, and <c>link</c> when even that is empty. A label that is taken gets <c>-2</c>,
///         <c>-3</c> and so on, the first that is free. A label counts as taken when the document has a definition with it
///         (any case, any blanks), or the text <c>[label]</c> anywhere, except as the text of a link the parser read: a definition would turn
///         such text into a link.
///     </para>
///     <para>
///         A definition that already exists (the first one that has its label, as the parser reads it) is reused for a link with
///         the same destination and title when its label is made of letters, digits, blanks and simple punctuation only; the
///         label is then written as the definition has it. Existing definitions are never changed.
///     </para>
///     <para>
///         Thread safety: an instance is a per-document builder used by one thread; <see cref="DeriveBase"/> is static and pure.
///     </para>
/// </remarks>
internal sealed partial class LinkLabelAllocator
{
    /// <summary>
    ///     A pair of brackets without brackets inside, and the parenthesis that follows it, if any. A pair that no parenthesis
    ///     follows is text that is a label by itself; a pair that one follows is the text of an inline link only when the parser
    ///     read a link there (see <see cref="LinkLabelAllocator(ParsedDocument, int)"/>).
    /// </summary>
    private const string BracketPattern = @"\[([^\[\]]*)\](\()?";

    /// <summary>
    ///     The characters other than letters and digits that a reused label may hold.
    /// </summary>
    private const string SimpleLabelPunctuation = " -_.:/#+";

    /// <summary>
    ///     The normalized labels that must not be used for a new definition.
    /// </summary>
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    ///     The label of every destination and title that has a definition, by destination and title.
    /// </summary>
    private readonly Dictionary<(string Url, string? Title), string> _targets = [];

    /// <summary>
    ///     The new definitions: label and the text of the definition line.
    /// </summary>
    private readonly List<(string Label, string Line)> _definitions = [];

    /// <summary>
    ///     The longest label that is generated.
    /// </summary>
    private readonly int _maxLength;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinkLabelAllocator"/> class for a document.
    /// </summary>
    /// <param name="document">The document whose labels and brackets must not be used. Must not be null.</param>
    /// <param name="maxLength">The longest label that is generated; at least 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
    public LinkLabelAllocator(ParsedDocument document, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        _maxLength = maxLength;

        // Every definition takes its label, whether or not the parser lets it win; only the one the parser uses can be reused
        foreach (var definition in document.References.Definitions)
        {
            _taken.Add(definition.NormalizedLabel);
            if (document.References.TryGet(definition.Label, out var effective)
                && effective.Start == definition.Start
                && IsSimpleLabel(definition.Label))
            {
                _targets.TryAdd((definition.Url, definition.Title), definition.Label);
            }
        }

        // Any bracketed text is a label to the parser as soon as a definition with it exists. Brackets followed by a parenthesis
        // are exempt only where the parser read an inline link or image: the same brackets before a destination that is not
        // one (for example with a blank in it) are plain text, which a new definition would turn into a link
        var linkOpenings = LinkOpenings(document);
        foreach (Match match in BracketRegex().Matches(document.Source.Text))
        {
            if (!match.Groups[2].Success || !linkOpenings.Contains(match.Index))
            {
                _taken.Add(ReferenceIndex.NormalizeLabel(match.Groups[1].Value));
            }
        }
    }

    /// <summary>
    ///     Finds where the links and images that the parser produced open their brackets.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The offsets of the opening brackets of every link and image that is not an autolink.</returns>
    private static HashSet<int> LinkOpenings(ParsedDocument document)
    {
        var openings = new HashSet<int>();
        foreach (var link in document.Ast.Descendants<LinkInline>())
        {
            if (!link.IsAutoLink)
            {
                openings.Add(link.Span.Start + (link.IsImage ? 1 : 0));
            }
        }

        return openings;
    }

    /// <summary>
    ///     Gets the new definition lines, sorted by label.
    /// </summary>
    public IReadOnlyList<string> DefinitionLines =>
        [.. _definitions.OrderBy(static definition => definition.Label, StringComparer.Ordinal).Select(static definition => definition.Line)];

    /// <summary>
    ///     Gets the labels of the new definitions, sorted.
    /// </summary>
    public IReadOnlyList<string> NewLabels =>
        [.. _definitions.Select(static definition => definition.Label).Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Derives the label a link would get when no other label is in the way.
    /// </summary>
    /// <param name="text">The plain text of the link. Must not be null.</param>
    /// <param name="url">The destination of the link. Must not be null.</param>
    /// <param name="maxLength">The longest label; at least 1.</param>
    /// <returns>The label: never empty, lower case, letters, digits and single hyphens.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
    public static string DeriveBase(string text, string url, int maxLength)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        var label = Slugify(text, maxLength);
        if (label.Length > 0)
        {
            return label;
        }

        // The text has no letters or digits: the host and the first path segment say what the link is
        string basis;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            basis = host + " " + uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        else
        {
            var cut = url.AsSpan().IndexOfAny('?', '#');
            basis = cut < 0 ? url : url[..cut];
        }

        label = Slugify(basis, maxLength);
        return label.Length > 0 ? label : "link";
    }

    /// <summary>
    ///     Gets the label of a link, defining a new label when the destination and title have none yet.
    /// </summary>
    /// <param name="baseLabel">The label derived from the text (see <see cref="DeriveBase"/>). Must not be null or empty.</param>
    /// <param name="tail">The destination part of the link. Must not be null.</param>
    /// <returns>The label to write: that of an existing or an earlier definition with the same destination and title, or a new one.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseLabel"/> is empty.</exception>
    public string Allocate(string baseLabel, InlineLinkTail tail)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(baseLabel);
        ArgumentNullException.ThrowIfNull(tail);

        var key = (tail.Url, tail.TitleText);
        if (_targets.TryGetValue(key, out var existing))
        {
            return existing;
        }

        // The first free label: the base, then the base with -2, -3 and so on (kept within the maximum length)
        var label = baseLabel;
        var number = 1;
        while (_taken.Contains(ReferenceIndex.NormalizeLabel(label)))
        {
            number++;
            var suffix = "-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            label = (_maxLength > suffix.Length ? Truncate(baseLabel, _maxLength - suffix.Length) : baseLabel) + suffix;
        }

        _taken.Add(ReferenceIndex.NormalizeLabel(label));
        _targets[key] = label;
        _definitions.Add((label, $"[{label}]: {tail.Destination}{(tail.Title is null ? string.Empty : " " + tail.Title)}"));
        return label;
    }

    /// <summary>
    ///     Turns text into a label: lower case, runs of other characters replaced by one hyphen, cut to a length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The longest result.</param>
    /// <returns>The label; empty when the text has no letter or digit.</returns>
    private static string Slugify(string text, int maxLength)
    {
        var builder = new StringBuilder(text.Length);
        var pending = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune))
            {
                pending = true;
                continue;
            }

            if (pending && builder.Length > 0)
            {
                builder.Append('-');
            }

            pending = false;
            builder.Append(rune.ToString().ToLowerInvariant());
        }

        return Truncate(builder.ToString(), maxLength);
    }

    /// <summary>
    ///     Cuts a label to a length, at a hyphen when it has one in the part that is kept.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="maxLength">The longest result; at least 1.</param>
    /// <returns>The label, shortened when it is longer than the maximum.</returns>
    private static string Truncate(string label, int maxLength)
    {
        if (label.Length <= maxLength)
        {
            return label;
        }

        var cut = label[..maxLength];
        if (label[maxLength] != '-')
        {
            var hyphen = cut.LastIndexOf('-');
            if (hyphen > 0)
            {
                cut = cut[..hyphen];
            }
        }

        // A cut must not split a surrogate pair
        if (cut.Length > 0 && char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return cut.TrimEnd('-');
    }

    /// <summary>
    ///     Determines whether a label of an existing definition is plain enough to be written into a link as it is.
    /// </summary>
    /// <param name="label">The label as the parser reports it.</param>
    /// <returns><see langword="true"/> when it starts and ends with a letter or digit and holds only letters, digits and simple punctuation.</returns>
    private static bool IsSimpleLabel(string label) =>
        label.Length > 0
        && char.IsLetterOrDigit(label[0])
        && char.IsLetterOrDigit(label[^1])
        && label.All(static c => char.IsLetterOrDigit(c) || SimpleLabelPunctuation.Contains(c, StringComparison.Ordinal));

    /// <summary>
    ///     Matches every pair of brackets that holds no brackets.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(BracketPattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex BracketRegex();
}

/// <summary>
///     The destination part of an inline link as it is written: <c>(destination "title")</c>, with the positions of
///     its first and last character.
/// </summary>
/// <param name="Start">Offset of the opening parenthesis.</param>
/// <param name="End">Offset just after the closing parenthesis.</param>
/// <param name="Destination">The destination as written (with its angle brackets when it has them).</param>
/// <param name="Title">The title as written (with its quotes or parentheses), or <see langword="null"/> when there is none.</param>
/// <param name="Url">The destination as the parser reports it.</param>
/// <param name="TitleText">The title as the parser reports it, or <see langword="null"/> when there is none.</param>
/// <remarks>
///     Immutable; safe for concurrent use.
/// </remarks>
internal sealed partial record InlineLinkTail(int Start, int End, string Destination, string? Title, string Url, string? TitleText)
{
    /// <summary>
    ///     Reads the destination part of an inline link, or finds out that the tool is not certain how it is written.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="link">An inline link or image of the document. Must not be null.</param>
    /// <returns>
    ///     The destination part; <see langword="null"/> when the link is not an inline link, when its destination part is not
    ///     on one line, when it uses a backslash or an entity (the tool copies text it can compare with the parser's reading,
    ///     and nothing else), or when the parser's reading differs from the text.
    /// </returns>
    /// <remarks>
    ///     Only the text from the opening parenthesis to the closing one is read. The text of the link, with all its brackets,
    ///     emphasis and code, is never looked at, so it cannot confuse the reading.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static InlineLinkTail? TryRead(string text, LinkInline link)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(link);

        // Only an inline link has a destination part of its own
        if (link.IsAutoLink || link.IsShortcut || link.Reference is not null || string.IsNullOrEmpty(link.Url) || link.UrlSpan.Start <= 0)
        {
            return null;
        }

        // The parser says where the destination is; the opening parenthesis (after "]") is found by stepping back over blanks and "<"
        var end = link.Span.End;
        if (end <= 0 || end >= text.Length || text[end] != ')' || link.UrlSpan.Start >= text.Length)
        {
            return null;
        }

        var open = SkipBlanksBackwards(text, link.UrlSpan.Start - 1);
        if (open >= 0 && text[open] == '<')
        {
            open = SkipBlanksBackwards(text, open - 1);
        }

        if (open < 1 || text[open] != '(' || text[open - 1] != ']')
        {
            return null;
        }

        var tail = text.AsSpan(open, end + 1 - open);
        return tail.Contains('\n') ? null : Read(tail.ToString(), open, link);
    }

    /// <summary>
    ///     Reads <c>(destination "title")</c> and compares it with what the parser says.
    /// </summary>
    /// <param name="tail">The text from the opening parenthesis to the closing parenthesis.</param>
    /// <param name="open">The offset of the opening parenthesis.</param>
    /// <param name="link">The link the parser read.</param>
    /// <returns>The destination part, or <see langword="null"/> when it is not certain.</returns>
    private static InlineLinkTail? Read(string tail, int open, LinkInline link)
    {
        var last = tail.Length - 1;
        var i = SkipBlanks(tail, 1);
        if (i >= last)
        {
            return null;
        }

        // The destination: in angle brackets, or a run without blanks whose parentheses balance
        int destinationEnd;
        string url;
        if (tail[i] == '<')
        {
            destinationEnd = tail.IndexOf('>', i + 1);
            if (destinationEnd < 0 || tail.AsSpan(i + 1, destinationEnd - i - 1).IndexOfAny('<', '\\') >= 0)
            {
                return null;
            }

            url = tail.Substring(i + 1, destinationEnd - i - 1);
            destinationEnd++;
        }
        else
        {
            destinationEnd = i;
            var depth = 0;
            while (destinationEnd < last && tail[destinationEnd] is not (' ' or '\t') && !char.IsControl(tail[destinationEnd]))
            {
                if (tail[destinationEnd] == '(')
                {
                    depth++;
                }
                else if (tail[destinationEnd] == ')')
                {
                    depth--;
                }

                destinationEnd++;
            }

            if (depth != 0 || destinationEnd == i || tail.AsSpan(i, destinationEnd - i).Contains('\\'))
            {
                return null;
            }

            url = tail[i..destinationEnd];
        }

        var destination = tail[i..destinationEnd];
        i = SkipBlanks(tail, destinationEnd);
        string? title = null;
        string? titleText = null;
        if (i < last)
        {
            // A title needs a blank before it, and is closed by the same delimiter
            if (i == destinationEnd || !TryReadTitle(tail, i, out var titleEnd))
            {
                return null;
            }

            title = tail[i..titleEnd];
            titleText = title[1..^1];
            i = SkipBlanks(tail, titleEnd);
        }

        // Nothing but the closing parenthesis may be left, and the parser must agree with what was read
        if (i != last || EntityRegex().IsMatch(url) || (titleText is not null && EntityRegex().IsMatch(titleText)))
        {
            return null;
        }

        if (!string.Equals(link.Url, url, StringComparison.Ordinal)
            || !string.Equals(link.Title ?? string.Empty, titleText ?? string.Empty, StringComparison.Ordinal))
        {
            return null;
        }

        return new InlineLinkTail(open, open + tail.Length, destination, title, url, titleText);
    }

    /// <summary>
    ///     Reads a title that starts at an index, in double quotes, single quotes or parentheses.
    /// </summary>
    /// <param name="tail">The text of the destination part, ending with its closing parenthesis.</param>
    /// <param name="start">The index of the opening delimiter.</param>
    /// <param name="end">Receives the index just after the closing delimiter.</param>
    /// <returns><see langword="true"/> when a title that holds no backslash and no nested parenthesis was read.</returns>
    private static bool TryReadTitle(string tail, int start, out int end)
    {
        end = -1;
        var closing = tail[start] switch
        {
            '"' => '"',
            '\'' => '\'',
            '(' => ')',
            _ => '\0'
        };
        if (closing == '\0')
        {
            return false;
        }

        // The last character is the parenthesis that closes the destination part, never the end of a title
        var close = tail.IndexOf(closing, start + 1, tail.Length - 1 - (start + 1));
        if (close < 0)
        {
            return false;
        }

        var inner = tail.AsSpan(start + 1, close - start - 1);
        if (inner.Contains('\\') || (closing == ')' && inner.Contains('(')))
        {
            return false;
        }

        end = close + 1;
        return true;
    }

    /// <summary>
    ///     Finds the first character at or after an index that is not a space or a tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index of the character; the length of the text when there is none.</returns>
    private static int SkipBlanks(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        return index;
    }

    /// <summary>
    ///     Finds the first character at or before an index that is not a space or a tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index of the character; -1 when there is none.</returns>
    private static int SkipBlanksBackwards(string text, int index)
    {
        while (index >= 0 && text[index] is ' ' or '\t')
        {
            index--;
        }

        return index;
    }

    /// <summary>
    ///     Matches text that a parser may read as a character reference.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"&[#A-Za-z0-9]+;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EntityRegex();
}
