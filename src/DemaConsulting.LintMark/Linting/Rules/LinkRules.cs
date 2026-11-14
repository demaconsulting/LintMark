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
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Linting.Rules;

/// <summary>
///     Lint-only rules about links, images and reference definitions.
/// </summary>
/// <remarks>
///     <para>
///         The rules read the inline syntax tree of the parser, plus the plain text that the parser left alone (an unresolved
///         reference is plain text). A rule only reports; it never proposes an edit, because every fix would change what the
///         document says or how it renders.
///     </para>
///     <list type="bullet">
///         <item><c>image-alt-text</c> (MD045): an image whose alternative text is empty or blank, and an HTML <c>img</c> tag without an <c>alt</c> attribute.</item>
///         <item><c>link-empty</c> (MD042): a link whose destination is empty or only <c>#</c>.</item>
///         <item><c>link-fragment</c> (MD051): a link to <c>#fragment</c> that no heading (slugified here, the way GitHub does, with <c>-1</c> suffixes for repeats), explicit <c>{#id}</c> or HTML <c>id</c>/<c>name</c> provides. <c>#top</c> and line anchors such as <c>#L10</c> are always accepted, and the comparison ignores letter case.</item>
///         <item><c>link-reference-undefined</c> (MD052): <c>[text][label]</c> or <c>[label][]</c> whose label has no definition. A shortcut <c>[label]</c> is never reported, and neither is a footnote reference.</item>
///         <item><c>link-definition-unused</c> (MD053): a definition that no link or image uses, and a label that is defined twice. The comment convention <c>[//]: # (text)</c> is ignored. A definition is never deleted by the fix option.</item>
///         <item><c>link-reversed</c> (MD011): <c>(text)[url]</c> where <c>[text](url)</c> was meant.</item>
///         <item><c>link-space</c> (MD039): the text of an inline, reference or collapsed link starts or ends with a space or tab; the message shows the corrected text. Images are not reported, a shortcut reference is not reported, and a break at the edge (a wrapped line) is not a space.</item>
///         <item><c>descriptive-link-text</c> (MD059, off by default): a link text that is only <c>click here</c>, <c>here</c>, <c>link</c> or <c>more</c>.</item>
///         <item><c>footnote-unused</c>: a footnote definition that no reference uses (the parser drops it from the tree).</item>
///     </list>
///     <para>
///         Thread safety: stateless; one instance serves every worker.
///     </para>
/// </remarks>
internal sealed partial class LinkRules : ILintRule
{
    /// <summary>
    ///     The link texts that say nothing about the destination, compared after lower-casing and removing punctuation.
    /// </summary>
    private static readonly HashSet<string> NonDescriptiveTexts = new(StringComparer.Ordinal) { "click here", "here", "link", "more" };

    /// <summary>
    ///     The placeholder that stands for a character that must not take part in a text match.
    /// </summary>
    private const char Masked = '\u0001';

    /// <summary>
    ///     Matches <c>[text][label]</c> and <c>[label][]</c> in text where the characters that are not plain text are masked.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\[(?<text>[^\[\]\n\u0001]*)\]\[(?<label>[^\[\]\n\u0001]*)\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceUseRegex();

    /// <summary>
    ///     Matches <c>(text)[url]</c> where the brackets are not followed by a destination.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"(?<![\w)\]\\])\((?<text>[^()\n\u0001]+)\)\[(?<url>[^\]^\n\u0001][^\]\n\u0001]*)\](?!\()", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReversedLinkRegex();

    /// <summary>
    ///     Matches an HTML <c>img</c> tag.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ImageTagRegex();

    /// <summary>
    ///     Matches an <c>alt</c> attribute in a tag.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\salt\s*=", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AltAttributeRegex();

    /// <summary>
    ///     Matches an <c>id</c> or <c>name</c> attribute of an HTML tag.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\s(?:id|name)\s*=\s*[""'](?<id>[^""']+)[""']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlIdRegex();

    /// <summary>
    ///     Matches an explicit heading identifier in the Pandoc form <c>{#id}</c>.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\{#(?<id>[^\s}]+)[^}]*\}\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExplicitIdRegex();

    /// <summary>
    ///     Matches a line anchor of a source file view, which no heading provides.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^L\d+(C\d+)?(-L\d+(C\d+)?)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineAnchorRegex();

    /// <summary>
    ///     Matches the label of a footnote definition at the start of a line.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^\[\^(?<label>[^\]\s]+)\]:", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FootnoteLabelRegex();

    /// <inheritdoc/>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.ImageAltTextRuleId,
        RuleCatalog.LinkEmptyRuleId,
        RuleCatalog.LinkFragmentRuleId,
        RuleCatalog.LinkReferenceUndefinedRuleId,
        RuleCatalog.LinkDefinitionUnusedRuleId,
        RuleCatalog.LinkReversedRuleId,
        RuleCatalog.LinkSpaceRuleId,
        RuleCatalog.DescriptiveLinkTextRuleId,
        RuleCatalog.FootnoteUnusedRuleId
    ];

    /// <inheritdoc/>
    public ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        var found = ImmutableArray.CreateBuilder<Diagnostic>();
        var source = document.Source;
        var anchors = CollectAnchors(document);
        var usedLabels = new HashSet<string>(StringComparer.Ordinal);

        // Every link and image of the tree, in document order
        foreach (var link in document.Ast.Descendants<LinkInline>())
        {
            if (link.Reference is { } reference)
            {
                usedLabels.Add(ReferenceIndex.NormalizeLabel(reference.Label ?? string.Empty));
            }

            CheckLink(source, link, anchors, found);
        }

        CheckHtmlImages(document, found);
        CheckDefinitions(document, usedLabels, found);
        CheckPlainTextLinks(document, found);
        CheckFootnotes(document, found);
        return found.ToImmutable();
    }

    /// <summary>
    ///     Checks one link or image of the syntax tree.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="link">The link or image.</param>
    /// <param name="anchors">The fragments that the document provides.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckLink(SourceDocument source, LinkInline link, HashSet<string> anchors, ImmutableArray<Diagnostic>.Builder found)
    {
        var start = link.Span.Start;
        if (link.IsImage)
        {
            if (string.IsNullOrWhiteSpace(RuleSupport.PlainText(link)))
            {
                found.Add(RuleSupport.Finding(source, RuleCatalog.ImageAltTextRuleId, start, "Image has no alternative text"));
            }

            return;
        }

        // An automatic link has no text of its own to check
        if (link.IsAutoLink)
        {
            return;
        }

        var url = link.Url ?? string.Empty;
        if (url.Length == 0 || url == "#")
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.LinkEmptyRuleId, start, "Link has no destination"));
        }
        else if (url[0] == '#' && !IsKnownFragment(url[1..], anchors))
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.LinkFragmentRuleId, start, $"Link fragment '{url}' matches no heading or anchor"));
        }

        CheckLinkSpace(source, link, found);
        if (NonDescriptiveTexts.Contains(NormalizeLinkText(RuleSupport.PlainText(link))))
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.DescriptiveLinkTextRuleId, start, $"Link text '{RuleSupport.CollapseWhitespace(RuleSupport.PlainText(link))}' does not describe the destination"));
        }
    }

    /// <summary>
    ///     Reports an inline, reference or collapsed link whose text starts or ends with a space or tab.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="link">The link (not an image).</param>
    /// <param name="found">Receives the finding.</param>
    private static void CheckLinkSpace(SourceDocument source, LinkInline link, ImmutableArray<Diagnostic>.Builder found)
    {
        // A shortcut reference is not covered, and the text must be located exactly: from the opening bracket to a closing bracket
        var text = source.Text;
        var open = link.Span.Start;
        if (link.IsShortcut || link.LastChild is null || open < 0 || open + 1 >= text.Length || text[open] != '[')
        {
            return;
        }

        var last = link.LastChild.Span.End;
        if (last <= open || last + 1 >= text.Length || text[last + 1] != ']')
        {
            return;
        }

        var raw = text[(open + 1)..(last + 1)];
        var leading = raw[0] is ' ' or '\t';
        var trailing = raw[^1] is ' ' or '\t';
        var corrected = raw.Trim(' ', '\t');
        if ((leading || trailing) && corrected.Length > 0 && corrected.IndexOf('\n') < 0)
        {
            string where;
            if (leading && trailing)
            {
                where = "start and end";
            }
            else
            {
                where = leading ? "start" : "end";
            }
            found.Add(RuleSupport.Finding(
                source,
                RuleCatalog.LinkSpaceRuleId,
                open,
                $"Link text has a space at its {where}; use [{Shorten(corrected)}] instead of [{Shorten(raw)}]"));
        }
    }

    /// <summary>
    ///     Reports HTML <c>img</c> tags without an <c>alt</c> attribute, inline and in HTML blocks.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckHtmlImages(ParsedDocument document, ImmutableArray<Diagnostic>.Builder found)
    {
        var source = document.Source;
        foreach (var span in HtmlSpans(document))
        {
            foreach (var tag in ImageTagRegex().Matches(source.Text.Substring(span.Start, span.Length)).Where(static tag => !AltAttributeRegex().IsMatch(tag.Value)))
            {
                found.Add(RuleSupport.Finding(source, RuleCatalog.ImageAltTextRuleId, span.Start + tag.Index, "Image has no alt attribute"));
            }
        }
    }

    /// <summary>
    ///     Reports link reference definitions that are never used or are defined twice.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="usedLabels">The normalized labels that a link or image uses.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckDefinitions(ParsedDocument document, HashSet<string> usedLabels, ImmutableArray<Diagnostic>.Builder found)
    {
        var source = document.Source;
        var firstLine = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var definition in document.References.Definitions)
        {
            // The comment convention [//]: # (text) is not a link definition
            if (definition.NormalizedLabel == "//")
            {
                continue;
            }

            if (firstLine.TryGetValue(definition.NormalizedLabel, out var earlier))
            {
                found.Add(RuleSupport.Finding(
                    source,
                    RuleCatalog.LinkDefinitionUnusedRuleId,
                    definition.Start,
                    $"Link reference definition '{definition.Label}' repeats the one at line {earlier}"));
                continue;
            }

            firstLine[definition.NormalizedLabel] = definition.Line;
            if (!usedLabels.Contains(definition.NormalizedLabel))
            {
                found.Add(RuleSupport.Finding(
                    source,
                    RuleCatalog.LinkDefinitionUnusedRuleId,
                    definition.Start,
                    $"Link reference definition '{definition.Label}' is never used"));
            }
        }
    }

    /// <summary>
    ///     Reports unresolved reference links and reversed links in the plain text of paragraphs and headings.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckPlainTextLinks(ParsedDocument document, ImmutableArray<Diagnostic>.Builder found)
    {
        var source = document.Source;
        foreach (var block in document.Blocks)
        {
            // A setext heading of several lines has inline spans that are not exact, so only paragraphs and ATX headings are read
            var inline = block switch
            {
                { Kind: BlockKind.Paragraph, Node: ParagraphBlock paragraph } => paragraph.Inline,
                { Kind: BlockKind.Heading, Node: HeadingBlock { HeaderChar: '#' } heading } => heading.Inline,
                _ => null
            };
            if (inline is null || block.Start < 0 || block.EndInclusive >= source.Text.Length)
            {
                continue;
            }

            var map = InlineMap.Build(inline);

            // A reference that resolves is never reported (its label is defined), so what is protected (code, HTML, link
            // destinations and labels) is the only part that has to be left out of both searches
            var plain = MaskedText(source.Text, block, offset => !map.IsProtected(offset));
            foreach (Match match in ReferenceUseRegex().Matches(plain))
            {
                var text = match.Groups["text"].Value;
                var label = match.Groups["label"].Value;
                var name = string.IsNullOrWhiteSpace(label) ? text : label;
                if (string.IsNullOrWhiteSpace(name) || name[0] == '^' || document.References.TryGet(name, out _))
                {
                    continue;
                }

                found.Add(RuleSupport.Finding(
                    source,
                    RuleCatalog.LinkReferenceUndefinedRuleId,
                    block.Start + match.Index,
                    $"Reference link uses the label '{RuleSupport.CollapseWhitespace(name)}', which has no definition"));
            }

            foreach (Match match in ReversedLinkRegex().Matches(plain))
            {
                found.Add(RuleSupport.Finding(
                    source,
                    RuleCatalog.LinkReversedRuleId,
                    block.Start + match.Index,
                    $"Link is written backwards; use [{Shorten(match.Groups["text"].Value)}]({Shorten(match.Groups["url"].Value)})"));
            }
        }
    }

    /// <summary>
    ///     Reports footnote definitions that no reference uses.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckFootnotes(ParsedDocument document, ImmutableArray<Diagnostic>.Builder found)
    {
        var source = document.Source;
        foreach (var block in document.Blocks)
        {
            if (block.Kind != BlockKind.UnreferencedFootnote || block.Start < 0 || block.Start >= source.Text.Length)
            {
                continue;
            }

            var lineEnd = source.Text.IndexOf('\n', block.Start);
            var line = source.Text[block.Start..(lineEnd < 0 ? source.Text.Length : lineEnd)];
            var label = FootnoteLabelRegex().Match(line) is { Success: true } match ? match.Groups["label"].Value : "?";
            found.Add(RuleSupport.Finding(source, RuleCatalog.FootnoteUnusedRuleId, block.Start, $"Footnote '[^{label}]' is defined but never referenced"));
        }
    }

    /// <summary>
    ///     Gets the text of a block with every character that fails a test, and every escaped bracket, replaced by the mask.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document.</param>
    /// <param name="block">The block.</param>
    /// <param name="keep">Decides, by offset, whether a character takes part in a match.</param>
    /// <returns>The masked text of the block; an index into it plus the block start is an offset into the document.</returns>
    private static string MaskedText(string text, BlockInfo block, Func<int, bool> keep)
    {
        var builder = new StringBuilder(block.EndInclusive - block.Start + 1);
        for (var offset = block.Start; offset <= block.EndInclusive; offset++)
        {
            var c = text[offset];
            var escaped = c is '[' or ']' && IsEscaped(text, offset);
            builder.Append(c == '\n' || (keep(offset) && !escaped) ? c : Masked);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Determines whether the character at an offset is escaped by a backslash.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset of the character.</param>
    /// <returns><see langword="true"/> when an odd number of backslashes precedes it.</returns>
    internal static bool IsEscaped(string text, int offset)
    {
        var count = 0;
        for (var i = offset - 1; i >= 0 && text[i] == '\\'; i--)
        {
            count++;
        }

        return count % 2 == 1;
    }

    /// <summary>
    ///     Normalizes the text of a link for the comparison with the texts that say nothing: lower case, punctuation removed, blanks collapsed.
    /// </summary>
    /// <param name="text">The link text.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeLinkText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return RuleSupport.CollapseWhitespace(builder.ToString());
    }

    /// <summary>
    ///     Shortens a text for a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or its start followed by three dots when it is longer than 40 characters.</returns>
    private static string Shorten(string text) => text.Length <= 40 ? text : string.Concat(text.AsSpan(0, 40), "...");

    /// <summary>
    ///     Gets the spans of the document that hold inline HTML or an HTML block, as offsets into the text.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The spans, with a start and a length.</returns>
    private static List<(int Start, int Length)> HtmlSpans(ParsedDocument document)
    {
        var text = document.Source.Text;
        var spans = new List<(int, int)>();
        foreach (var span in document.Ast.Descendants<HtmlInline>().Select(static inline => inline.Span))
        {
            if (span.Start >= 0 && span.End >= span.Start && span.End < text.Length)
            {
                spans.Add((span.Start, span.Length));
            }
        }

        foreach (var block in document.Blocks)
        {
            if (block is { Kind: BlockKind.HtmlBlock, Node: HtmlBlock { Type: not HtmlBlockType.Comment } }
                && block.Start >= 0 && block.EndInclusive >= block.Start && block.EndInclusive < text.Length)
            {
                spans.Add((block.Start, block.EndInclusive - block.Start + 1));
            }
        }

        return spans;
    }

    /// <summary>
    ///     Collects the fragments that the document provides: the slug of every heading, explicit identifiers and HTML anchors.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The fragments in lower case.</returns>
    private static HashSet<string> CollectAnchors(ParsedDocument document)
    {
        var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repeats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in document.Blocks)
        {
            if (block is not { Kind: BlockKind.Heading, Node: HeadingBlock heading })
            {
                continue;
            }

            // The text of the heading as the hosting site reads it: an image has no text, and blanks around it stay
            var text = RuleSupport.PlainText(heading.Inline, includeImages: false);
            var explicitId = ExplicitIdRegex().Match(text);
            if (explicitId.Success)
            {
                anchors.Add(explicitId.Groups["id"].Value);
            }

            // The same slug again gets the next number as a suffix, the way the hosting sites number repeated headings
            var slug = Slug(text);
            var seen = repeats.GetValueOrDefault(slug);
            repeats[slug] = seen + 1;
            anchors.Add(seen == 0 ? slug : $"{slug}-{seen}");
        }

        var source = document.Source.Text;
        foreach (var (start, length) in HtmlSpans(document))
        {
            foreach (Match match in HtmlIdRegex().Matches(source.Substring(start, length)))
            {
                anchors.Add(match.Groups["id"].Value);
            }
        }

        return anchors;
    }

    /// <summary>
    ///     Turns the text of a heading into the fragment that GitHub gives it: lower case, letters, digits, marks, hyphens and
    ///     underscores kept, blanks turned into hyphens and everything else dropped.
    /// </summary>
    /// <param name="text">The text of the heading.</param>
    /// <returns>The fragment.</returns>
    internal static string Slug(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                builder.Append('-');
                continue;
            }

            var category = char.GetUnicodeCategory(c);
            if (c is '-' or '_'
                || category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber
                    or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.LetterNumber)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Determines whether a fragment is provided by the document or is one that is always valid.
    /// </summary>
    /// <param name="fragment">The fragment without the number sign, as written in the link.</param>
    /// <param name="anchors">The fragments that the document provides.</param>
    /// <returns><see langword="true"/> when the fragment is known, <c>top</c> or a line anchor.</returns>
    private static bool IsKnownFragment(string fragment, HashSet<string> anchors)
    {
        if (anchors.Contains(fragment) || string.Equals(fragment, "top", StringComparison.OrdinalIgnoreCase) || LineAnchorRegex().IsMatch(fragment))
        {
            return true;
        }

        // A fragment is often written percent-encoded
        try
        {
            return anchors.Contains(Uri.UnescapeDataString(fragment));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
