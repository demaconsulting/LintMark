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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax.Inlines;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Finds, in the inline tree of one paragraph, the emphasis nodes and bare URLs that the normalizer may
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
///         the characters that normally surround a URL in prose. Everything the walk does not recognize is left
///         alone. The edits replace existing characters (never insert at a position), so they cannot collide
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
    ///     Finds the edits for the emphasis, strong emphasis and bare URLs of a paragraph.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="root">The inline content of the paragraph. Must not be null.</param>
    /// <param name="emphasis">The delimiter that emphasis is written with, or <see langword="null"/> when the rule is off.</param>
    /// <param name="strong">The delimiter that strong emphasis is written with, or <see langword="null"/> when the rule is off.</param>
    /// <param name="urls">Whether bare URLs are wrapped in angle brackets.</param>
    /// <returns>The edits, ordered by start offset and without overlaps; empty when there is no candidate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="root"/> is null.</exception>
    public static ImmutableArray<TextEdit> Find(
        string text,
        ContainerInline root,
        EmphasisMarker? emphasis,
        EmphasisMarker? strong,
        bool urls)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);

        // A delimiter character in any literal of the paragraph makes the pairing of the others uncertain
        var found = ImmutableArray.CreateBuilder<TextEdit>();
        var literalDelimiter = HasLiteralDelimiter(root);
        Visit(text, root, emphasis, strong, urls, literalDelimiter, insideEmphasis: false, insideLink: false, found);

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
    ///     Determines whether any literal inline of a tree holds an emphasis delimiter character.
    /// </summary>
    /// <param name="container">The container to search.</param>
    /// <returns><see langword="true"/> when a literal holds <c>*</c> or <c>_</c>.</returns>
    private static bool HasLiteralDelimiter(ContainerInline container)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            // The text of an autolink is part of the link (written bare or in angle brackets alike), never a delimiter
            if (child is LinkInline { IsAutoLink: true })
            {
                continue;
            }

            if (child is LiteralInline literal && literal.Content.AsSpan().ContainsAny('*', '_'))
            {
                return true;
            }

            if (child is ContainerInline nested && HasLiteralDelimiter(nested))
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

                    Visit(text, node, emphasis, strong, urls, literalDelimiter, insideEmphasis || delimiter, insideLink, found);
                    break;

                case LinkInline link when link.IsAutoLink:
                    if (urls && !insideLink)
                    {
                        AddUrl(text, link, found);
                    }

                    break;

                case LinkInline link:
                    Visit(text, link, emphasis, strong, urls, literalDelimiter, insideEmphasis, insideLink: true, found);
                    break;

                case ContainerInline nested:
                    Visit(text, nested, emphasis, strong, urls, literalDelimiter, insideEmphasis, insideLink, found);
                    break;
            }
        }
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

        // Only the schemes that wrap to the same link; an angle-bracket autolink and an e-mail or www address stay as written
        var source = text.AsSpan(start, end - start + 1);
        if (!HasSupportedScheme(source) || source.ContainsAny(" <>|\n\t"))
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
