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

using System.Buffers;
using System.Collections.Immutable;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Writes the emphasis, strong emphasis, bare URLs and code spans of a paragraph in the canonical form: the configured
///     delimiter character, URLs wrapped in angle brackets, and code spans without redundant padding.
/// </summary>
/// <remarks>
///     <para>
///         Four rules are handled: <c>emphasis</c> (<c>_x_</c> and <c>*x*</c> are the same emphasis; the
///         <c>emphasis</c> setting decides which is written), <c>strong</c> (the same for <c>__x__</c> and
///         <c>**x**</c>), <c>bare-urls</c> (a bare <c>http://</c>, <c>https://</c> or <c>ftp://</c> URL becomes
///         <c>&lt;url&gt;</c>; the setting <c>leave</c> switches the rule off) and <c>code-span-space</c> (a code span
///         padded with one space on each side loses both spaces, unless its content begins or ends with a backtick;
///         the rule has no setting). Only paragraphs are handled: headings
///         and table cells keep their text as written, and so do <c>www.</c> addresses, e-mail addresses and
///         <c>mailto:</c> links, which read differently when they are wrapped.
///     </para>
///     <para>
///         The normalizer is not a block formatter (it claims no lines): <see cref="DocumentFormatter"/> asks it
///         for each paragraph that no block formatter claims, so the whitespace rules keep their lines. Its edits
///         replace characters inside the text (a delimiter, the first and the last character of a URL) or delete the
///         padding inside a code span, never the whitespace at the ends of a line and never insert at a position, so they
///         cannot conflict with the edits of those rules.
///     </para>
///     <para>
///         Candidates come from the inline syntax tree with strict guards (see <see cref="InlineCandidates"/>), which leave a
///         paragraph as written whenever the edits could change how it is read (for example when it holds a literal
///         <c>&lt;</c>). A paragraph is also left alone when the document defines link
///         references and the paragraph holds a square bracket: a rewritten label could then stop matching its definition.
///         A rule that is suppressed anywhere in the paragraph is not applied to it.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class InlineNormalizer
{
    /// <summary>
    ///     Gets the identifiers of the rules this normalizer enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.EmphasisRuleId,
        RuleCatalog.StrongRuleId,
        RuleCatalog.BareUrlsRuleId,
        RuleCatalog.CodeSpanSpaceRuleId
    ];

#pragma warning disable S2325 // An instance is registered with the document formatter, like a block formatter: the contract is instance-based
    /// <summary>
    ///     Proposes the edits that bring the emphasis, strong emphasis and bare URLs of a paragraph into canonical form.
    /// </summary>
    /// <param name="document">The parsed document the paragraph belongs to. Must not be null.</param>
    /// <param name="block">The paragraph. Must be a paragraph block of <paramref name="document"/>.</param>
    /// <param name="config">The effective configuration. Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not a paragraph.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void AddBlockEdits(ParsedDocument document, BlockInfo block, LintMarkConfig config, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(edits);

        // Only a paragraph with inline content can be rewritten, and only by the rules that are not suppressed in it
        if (block.Node is not ParagraphBlock { Inline: { } inline })
        {
            return;
        }

        var emphasis = IsSuppressed(document, block, RuleCatalog.EmphasisRuleId) ? (EmphasisMarker?)null : config.Emphasis;
        var strong = IsSuppressed(document, block, RuleCatalog.StrongRuleId) ? (EmphasisMarker?)null : config.Strong;
        var urls = config.BareUrls == BareUrlStyle.Autolink && !IsSuppressed(document, block, RuleCatalog.BareUrlsRuleId);
        var codeSpaces = !IsSuppressed(document, block, RuleCatalog.CodeSpanSpaceRuleId);
        if (emphasis is null && strong is null && !urls && !codeSpaces)
        {
            return;
        }

        // A label that is rewritten could stop matching its definition, so such a paragraph is left alone
        var text = document.Source.Text;
        if (document.References.Definitions.Count > 0 && text.AsSpan(block.Start, block.EndInclusive + 1 - block.Start).Contains('['))
        {
            return;
        }

        var candidates = InlineCandidates.Find(text, inline, emphasis, strong, urls, codeSpaces);
        if (candidates.IsEmpty)
        {
            return;
        }

        foreach (var edit in candidates)
        {
            edits.Add(edit);
        }
    }
#pragma warning restore S2325

    /// <summary>
    ///     Determines whether a rule is suppressed anywhere in a block.
    /// </summary>
    /// <param name="document">The document that holds the suppressions.</param>
    /// <param name="block">The block.</param>
    /// <param name="ruleId">The rule.</param>
    /// <returns><see langword="true"/> when a suppression covers part of the block.</returns>
    private static bool IsSuppressed(ParsedDocument document, BlockInfo block, string ruleId) =>
        document.Suppressions.IsSuppressedRange(block.Start, block.EndInclusive + 1, ruleId);
}

/// <summary>
///     Finds, in the inline tree of one paragraph, the emphasis nodes, bare URLs and code spans that the normalizer may
///     rewrite, and the edits that rewrite them.
/// </summary>
/// <remarks>
///     <para>
///         The walk only proposes edits for shapes that are safe to rewrite on their own. An emphasis node is a
///         candidate when its source starts and ends with exactly its delimiters, it is not nested in or around
///         another emphasis node, neither delimiter character appears in its content or directly next to it, and
///         the paragraph holds no literal delimiter character anywhere (an unpaired or escaped delimiter changes how
///         the others pair). When the target is the underscore, the node must not stand inside a word. A bare URL
///         is a candidate when it starts with <c>http://</c>, <c>https://</c> or <c>ftp://</c>, holds no character
///         that would end or change an angle-bracket autolink, is not inside another link or the destination of one,
///         and stands between
///         the characters that normally surround a URL in prose. A code span is a candidate when it lies on one line
///         and is padded with exactly one space on each side (see <see cref="AddCodeSpan"/>); its two edits delete
///         the padding. Everything the walk does not recognize is left
///         alone. The edits replace or delete existing characters (never insert at a position), so they cannot collide
///         with the insertions of the block spacing rule.
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class InlineCandidates
{
    /// <summary>
    ///     The schemes of the URLs that are wrapped in angle brackets.
    /// </summary>
    private static readonly string[] Schemes = ["http://", "https://", "ftp://"];

    /// <summary>
    ///     The emphasis delimiter characters.
    /// </summary>
    private static readonly SearchValues<char> LiteralDelimiters = SearchValues.Create("*_");

    /// <summary>
    ///     The character that opens an HTML construct.
    /// </summary>
    private static readonly SearchValues<char> LiteralAngle = SearchValues.Create("<");

    /// <summary>
    ///     Finds the edits for the emphasis, strong emphasis and bare URLs of a paragraph.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="root">The inline content of the paragraph. Must not be null.</param>
    /// <param name="emphasis">The delimiter that emphasis is written with, or <see langword="null"/> when the rule is off.</param>
    /// <param name="strong">The delimiter that strong emphasis is written with, or <see langword="null"/> when the rule is off.</param>
    /// <param name="urls">Whether bare URLs are wrapped in angle brackets.</param>
    /// <param name="codeSpaces">Whether the padding of code spans is removed.</param>
    /// <returns>The edits, ordered by start offset and without overlaps; empty when there is no candidate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="root"/> is null.</exception>
    public static ImmutableArray<TextEdit> Find(
        string text,
        ContainerInline root,
        EmphasisMarker? emphasis,
        EmphasisMarker? strong,
        bool urls,
        bool codeSpaces = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);

        // A delimiter character in any literal of the paragraph makes the pairing of the others uncertain
        var found = ImmutableArray.CreateBuilder<TextEdit>();
        // An unclosed "<" that the parser read as text can be closed by an angle bracket this rewrite writes, which would turn
        // the text after it into an HTML construct; such a paragraph is left alone
        if (HasLiteral(root, LiteralAngle))
        {
            return [];
        }

        var literalDelimiter = HasLiteral(root, LiteralDelimiters);
        Visit(text, root, emphasis, strong, urls, codeSpaces, literalDelimiter, insideEmphasis: false, insideLink: false, found);

        // A tree the parser built with the same inline twice proposes the same edit twice; one stands, and edits that overlap
        // without being equal mean the tree cannot be trusted, so none stands
        var distinct = found.Distinct().OrderBy(static edit => edit.Start).ToList();
        for (var i = 1; i < distinct.Count; i++)
        {
            if (distinct[i - 1].End > distinct[i].Start)
            {
                return [];
            }
        }

        return [.. distinct];
    }

    /// <summary>
    ///     Gets the delimiter character of a marker.
    /// </summary>
    /// <param name="marker">The marker.</param>
    /// <returns><c>*</c> or <c>_</c>.</returns>
    private static char CharOf(EmphasisMarker marker) => marker == EmphasisMarker.Underscore ? '_' : '*';

    /// <summary>
    ///     Determines whether any literal inline of a tree holds one of the given characters.
    /// </summary>
    /// <param name="container">The container to search.</param>
    /// <param name="characters">The characters to look for.</param>
    /// <returns><see langword="true"/> when a literal outside an autolink holds one of the characters.</returns>
    private static bool HasLiteral(ContainerInline container, SearchValues<char> characters)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            // The text of an autolink is part of the link (written bare or in angle brackets alike), never a delimiter
            if (child is LinkInline { IsAutoLink: true })
            {
                continue;
            }

            if (child is LiteralInline literal && literal.Content.AsSpan().ContainsAny(characters))
            {
                return true;
            }

            if (child is ContainerInline nested && HasLiteral(nested, characters))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a tree holds an emphasis node.
    /// </summary>
    /// <param name="container">The container to search.</param>
    /// <returns><see langword="true"/> when an emphasis node is among the descendants.</returns>
    private static bool HasEmphasis(ContainerInline container)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is EmphasisInline || (child is ContainerInline nested && HasEmphasis(nested)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Walks the children of a container and collects the edits of the candidates.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="container">The container.</param>
    /// <param name="emphasis">The emphasis delimiter, or null when the rule is off.</param>
    /// <param name="strong">The strong delimiter, or null when the rule is off.</param>
    /// <param name="urls">Whether bare URLs are wrapped.</param>
    /// <param name="codeSpaces">Whether the padding of code spans is removed.</param>
    /// <param name="literalDelimiter">Whether a literal of the paragraph holds a delimiter character.</param>
    /// <param name="insideEmphasis">Whether the container lies in an emphasis node written with <c>*</c> or <c>_</c>.</param>
    /// <param name="insideLink">Whether the container lies in the text of a link.</param>
    /// <param name="found">Receives the edits.</param>
    private static void Visit(
        string text,
        ContainerInline container,
        EmphasisMarker? emphasis,
        EmphasisMarker? strong,
        bool urls,
        bool codeSpaces,
        bool literalDelimiter,
        bool insideEmphasis,
        bool insideLink,
        ImmutableArray<TextEdit>.Builder found)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case EmphasisInline node:
                    var delimiter = node.DelimiterChar is '*' or '_';
                    if (delimiter && !literalDelimiter && !insideEmphasis)
                    {
                        AddEmphasis(text, node, emphasis, strong, found);
                    }

                    Visit(text, node, emphasis, strong, urls, codeSpaces, literalDelimiter, insideEmphasis || delimiter, insideLink, found);
                    break;

                case LinkInline link when link.IsAutoLink:
                    if (urls && !insideLink)
                    {
                        AddUrl(text, link, found);
                    }

                    break;

                case LinkInline link:
                    Visit(text, link, emphasis, strong, urls, codeSpaces, literalDelimiter, insideEmphasis, insideLink: true, found);
                    break;

                case CodeInline code:
                    if (codeSpaces)
                    {
                        AddCodeSpan(text, code, found);
                    }

                    break;

                case ContainerInline nested:
                    Visit(text, nested, emphasis, strong, urls, codeSpaces, literalDelimiter, insideEmphasis, insideLink, found);
                    break;
            }
        }
    }

    /// <summary>
    ///     Adds the edits that remove the padding of a code span, when it is a safe candidate.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="code">The code span.</param>
    /// <param name="found">Receives the edits.</param>
    /// <remarks>
    ///     A code span that starts and ends with one space (and holds something else) means the same without them, so the
    ///     spaces are redundant. A span is a candidate only when it lies on one line, has exactly one space on each side
    ///     and no more (so asymmetric padding, wider padding and a span of spaces stay as written), and its content does
    ///     not begin or end with a backtick (the padding is what keeps the content apart from the delimiters).
    /// </remarks>
    private static void AddCodeSpan(string text, CodeInline code, ImmutableArray<TextEdit>.Builder found)
    {
        // The source must be the delimiters around the content, on one line
        var count = code.DelimiterCount;
        var start = code.Span.Start;
        var end = code.Span.End;
        if (count < 1 || start < 0 || end >= text.Length || end - start + 1 <= 2 * count)
        {
            return;
        }

        var source = text.AsSpan(start, end - start + 1);
        if (source[..count].IndexOfAnyExcept('`') >= 0 || source[^count..].IndexOfAnyExcept('`') >= 0)
        {
            return;
        }

        // Exactly one space on each side, something else next to them, no backtick at the edges of the content
        var raw = source[count..^count];
        if (raw.Length < 3
            || raw.Contains('\n')
            || raw.Contains('\t')
            || raw[0] != ' '
            || raw[^1] != ' '
            || raw[1] is ' ' or '`'
            || raw[^2] is ' ' or '`')
        {
            return;
        }

        found.Add(new TextEdit(start + count, 1, string.Empty, RuleCatalog.CodeSpanSpaceRuleId));
        found.Add(new TextEdit(end - count, 1, string.Empty, RuleCatalog.CodeSpanSpaceRuleId));
    }

    /// <summary>
    ///     Adds the edits that rewrite the delimiters of an emphasis node, when it is a safe candidate.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="node">The emphasis node.</param>
    /// <param name="emphasis">The emphasis delimiter, or null when the rule is off.</param>
    /// <param name="strong">The strong delimiter, or null when the rule is off.</param>
    /// <param name="found">Receives the edits.</param>
    private static void AddEmphasis(string text, EmphasisInline node, EmphasisMarker? emphasis, EmphasisMarker? strong, ImmutableArray<TextEdit>.Builder found)
    {
        // Emphasis has one delimiter character per side, strong emphasis two; anything else is a longer run
        var count = node.DelimiterCount;
        var (target, rule) = count switch
        {
            1 => (emphasis, RuleCatalog.EmphasisRuleId),
            2 => (strong, RuleCatalog.StrongRuleId),
            _ => ((EmphasisMarker?)null, string.Empty)
        };
        if (target is not { } marker || node.DelimiterChar == CharOf(marker) || HasEmphasis(node))
        {
            return;
        }

        // The source must start and end with exactly the delimiters, with content in between
        var start = node.Span.Start;
        var end = node.Span.End;
        if (start < 0 || end >= text.Length || end - start + 1 <= 2 * count)
        {
            return;
        }

        var source = text.AsSpan(start, end - start + 1);
        for (var i = 0; i < count; i++)
        {
            if (source[i] != node.DelimiterChar || source[^(i + 1)] != node.DelimiterChar)
            {
                return;
            }
        }

        // Neither delimiter character may occur in the content, nor directly next to the node
        if (source[count..^count].ContainsAny('*', '_') || IsDelimiter(text, start - 1) || IsDelimiter(text, end + 1))
        {
            return;
        }

        // The neighbors must be plain text or an inline whose delimiters cannot pair with these
        if (!IsPlainNeighbor(node.PreviousSibling) || !IsPlainNeighbor(node.NextSibling))
        {
            return;
        }

        // An underscore inside a word is not emphasis, so the target must have a non-word character on each side
        var wanted = CharOf(marker);
        if (wanted == '_' && (!IsBoundary(text, start - 1) || !IsBoundary(text, end + 1)))
        {
            return;
        }

        var replacement = new string(wanted, count);
        found.Add(new TextEdit(start, count, replacement, rule));
        found.Add(new TextEdit(end - count + 1, count, replacement, rule));
    }

    /// <summary>
    ///     Adds the edits that wrap a bare URL in angle brackets, when it is a safe candidate.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="link">The autolink inline (bare or written with angle brackets).</param>
    /// <param name="found">Receives the edits.</param>
    private static void AddUrl(string text, LinkInline link, ImmutableArray<TextEdit>.Builder found)
    {
        var start = link.Span.Start;
        var end = link.Span.End;
        if (start < 0 || end >= text.Length || end <= start)
        {
            return;
        }

        // Only the schemes that wrap to the same link; an angle-bracket autolink and an e-mail or www address stay as written.
        // A URL with braces belongs to template or attribute syntax (the parser's end of the URL is a guess there)
        var source = text.AsSpan(start, end - start + 1);
        if (!HasSupportedScheme(source) || source.ContainsAny(" <>|\n\t{}"))
        {
            return;
        }

        // The URL stands where prose puts a URL: after the start, a blank or a parenthesis, before the end, a blank or closing punctuation
        var before = start == 0 ? '\n' : text[start - 1];
        var after = end + 1 >= text.Length ? '\n' : text[end + 1];
        if (!(char.IsWhiteSpace(before) || before == '(') || !(char.IsWhiteSpace(after) || after is '.' or ',' or ';' or ':' or '!' or '?' or ')'))
        {
            return;
        }

        // A URL right after "](" is the destination of a link, even where the parser reports a task list marker followed by the
        // URL (a "[x]" inside a list item), so it is not a bare URL
        if (start >= 2 && text[start - 1] == '(' && text[start - 2] == ']')
        {
            return;
        }

        found.Add(new TextEdit(start, 1, "<" + text[start], RuleCatalog.BareUrlsRuleId));
        found.Add(new TextEdit(end, 1, text[end] + ">", RuleCatalog.BareUrlsRuleId));
    }

    /// <summary>
    ///     Determines whether the text starts with a scheme whose bare URLs are wrapped.
    /// </summary>
    /// <param name="source">The source text of the link.</param>
    /// <returns><see langword="true"/> for <c>http://</c>, <c>https://</c> and <c>ftp://</c>, whatever the letter case.</returns>
    private static bool HasSupportedScheme(ReadOnlySpan<char> source)
    {
        foreach (var scheme in Schemes)
        {
            if (source.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the character at an offset is an emphasis delimiter character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset; may be outside the text.</param>
    /// <returns><see langword="true"/> for <c>*</c> and <c>_</c>.</returns>
    private static bool IsDelimiter(string text, int offset) => offset >= 0 && offset < text.Length && text[offset] is '*' or '_';

    /// <summary>
    ///     Determines whether the character at an offset is not part of a word.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset; may be outside the text, where there is no word.</param>
    /// <returns><see langword="true"/> for a blank or an ASCII character that is not a letter or digit, and outside the text.</returns>
    private static bool IsBoundary(string text, int offset)
    {
        if (offset < 0 || offset >= text.Length)
        {
            return true;
        }

        var c = text[offset];
        return char.IsWhiteSpace(c) || (c < 128 && !char.IsAsciiLetterOrDigit(c));
    }

    /// <summary>
    ///     Determines whether an inline next to an emphasis node is one that cannot take part in its delimiter pairing.
    /// </summary>
    /// <param name="sibling">The previous or next inline, or <see langword="null"/> at the edge of the container.</param>
    /// <returns><see langword="true"/> for no inline, a literal, a line break, a code span or a link.</returns>
    private static bool IsPlainNeighbor(MarkdigInline? sibling) =>
        sibling is null or LiteralInline or LineBreakInline or CodeInline or LinkInline;
}
