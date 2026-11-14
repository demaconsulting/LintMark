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
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Linting.Rules;

/// <summary>
///     Lint-only rules about the structure of a document and about marker characters that the parser read as plain text:
///     code fences, block quotes, shell prompts, headings without the space that makes them headings, and emphasis with a
///     stray space.
/// </summary>
/// <remarks>
///     <para>
///         A rule only reports; it never proposes an edit, because the fix of each of these would change what the document
///         renders or what it means.
///     </para>
///     <list type="bullet">
///         <item><c>code-fence-language</c> (MD040): a fenced code block without an info string.</item>
///         <item><c>fence-unclosed</c>: a fenced code or math block that is never closed (the parser runs it to the end of its container).</item>
///         <item><c>blockquote-blank-line</c> (MD028): a blank line between two block quotes of the same container.</item>
///         <item><c>dollar-sign-in-shell-code</c> (MD014, off by default): a code block in which every non-blank line starts with a dollar sign and a blank.</item>
///         <item><c>heading-missing-space</c> (MD018): the first line of a paragraph that starts at the start of a line with one to six number signs directly followed by a letter. It is a paragraph, not a heading. After a list marker or a quote marker it is left alone (an issue number or a tag).</item>
///         <item><c>heading-closed-missing-space</c> (MD020): the same, when the line also ends with number signs that follow a non-blank character (<c>##Title##</c>).</item>
///         <item>
///             <c>emphasis-space</c> (MD037): emphasis whose delimiters have a space inside on one side only, on a single line
///             (<c>*text *</c>, <c>* text*</c>, and the same with <c>_</c>, <c>**</c> and <c>__</c>). The rule reads the delimiter
///             characters that the parser left as plain text, so it never reports a delimiter that is escaped, in a code span, a
///             link destination or HTML. It does not report spaces on both sides (<c>* text *</c>) or a literal asterisk between
///             words (<c>a * b * c</c>), and it needs a letter next to the delimiter on its flanking side and a word character at
///             the other end, which keeps wildcards and arithmetic out of the reports.
///         </item>
///     </list>
///     <para>
///         Thread safety: stateless; one instance serves every worker.
///     </para>
/// </remarks>
internal sealed partial class StructureRules : ILintRule
{
    /// <summary>
    ///     Matches the first line of a paragraph that looks like a heading without the space after its number signs.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^#{1,6}(?=\p{L})", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MissingSpaceRegex();

    /// <summary>
    ///     Matches the first line of a paragraph that looks like a closed heading without the spaces inside its number signs.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^#{1,6}(?=\p{L}).*[^\s#\\]#{1,6}[ \t]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ClosedMissingSpaceRegex();

    /// <summary>
    ///     Matches a line of a code block that starts like a shell prompt.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^[ \t>]*\$[ \t]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PromptRegex();

    /// <inheritdoc/>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.CodeFenceLanguageRuleId,
        RuleCatalog.FenceUnclosedRuleId,
        RuleCatalog.BlockQuoteBlankLineRuleId,
        RuleCatalog.DollarSignInShellCodeRuleId,
        RuleCatalog.HeadingMissingSpaceRuleId,
        RuleCatalog.HeadingClosedMissingSpaceRuleId,
        RuleCatalog.EmphasisSpaceRuleId
    ];

    /// <inheritdoc/>
    public ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        var source = document.Source;
        var found = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var block in document.Blocks)
        {
            // A damaged span cannot be read; the parser's other views of the document are still checked
            if (block.Start < 0 || block.EndInclusive < block.Start || block.EndInclusive >= source.Text.Length)
            {
                continue;
            }

            switch (block)
            {
                case { Kind: BlockKind.FencedCode, Node: FencedCodeBlock fence }:
                    if (string.IsNullOrWhiteSpace(fence.Info))
                    {
                        found.Add(RuleSupport.Finding(source, RuleCatalog.CodeFenceLanguageRuleId, block.Start, "Fenced code block has no language"));
                    }

                    CheckShellPrompts(source, block, skipClosingFence: true, found);
                    break;

                case { Kind: BlockKind.IndentedCode }:
                    CheckShellPrompts(source, block, skipClosingFence: false, found);
                    break;

                case { Kind: BlockKind.UnclosedFence }:
                    found.Add(RuleSupport.Finding(
                        source,
                        RuleCatalog.FenceUnclosedRuleId,
                        block.Start,
                        block.Node is MathBlock ? "Math block is never closed" : "Fenced code block is never closed"));
                    break;

                case { Kind: BlockKind.Paragraph, Node: ParagraphBlock paragraph }:
                    CheckHeadingSpace(source, block, found);
                    CheckEmphasisSpace(source, block, paragraph, found);
                    break;

                default:
                    break;
            }
        }

        CheckBlockQuotes(source, document.Ast, found);
        return found.ToImmutable();
    }

    /// <summary>
    ///     Reports two block quotes that are only separated by a blank line, in a container and in the containers inside it.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="container">The container.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckBlockQuotes(SourceDocument source, ContainerBlock container, ImmutableArray<Diagnostic>.Builder found)
    {
        QuoteBlock? previous = null;
        foreach (var child in container)
        {
            if (child is QuoteBlock quote)
            {
                if (previous is not null && IsBlankGap(source.Text, previous.Span.End + 1, quote.Span.Start))
                {
                    var line = source.PositionOf(previous.Span.End).Line + 1;
                    found.Add(RuleSupport.Finding(source, RuleCatalog.BlockQuoteBlankLineRuleId, source.OffsetOf(line, 1), "Blank line separates two block quotes"));
                }

                previous = quote;
            }
            else
            {
                previous = null;
            }

            if (child is ContainerBlock inner)
            {
                CheckBlockQuotes(source, inner, found);
            }
        }
    }

    /// <summary>
    ///     Determines whether the text between two blocks is only blank space and quote markers.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="start">The offset of the first character between the blocks.</param>
    /// <param name="end">The offset of the first character of the second block.</param>
    /// <returns><see langword="true"/> when the range is inside the text and holds only blanks, line feeds and <c>&gt;</c>.</returns>
    private static bool IsBlankGap(string text, int start, int end) =>
        start >= 0 && end > start && end <= text.Length && text.AsSpan(start, end - start).IndexOfAnyExcept(" \t\n>") < 0;

    /// <summary>
    ///     Reports a code block in which every non-blank line starts with a shell prompt.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block.</param>
    /// <param name="skipClosingFence">Whether the first and the last line of the block are fence lines.</param>
    /// <param name="found">Receives the finding.</param>
    private static void CheckShellPrompts(SourceDocument source, BlockInfo block, bool skipClosingFence, ImmutableArray<Diagnostic>.Builder found)
    {
        var firstLine = source.PositionOf(block.Start).Line;
        var lastLine = source.PositionOf(block.EndInclusive).Line;
        if (skipClosingFence)
        {
            firstLine++;
            lastLine--;
        }

        var firstPrompt = -1;
        for (var line = firstLine; line <= lastLine; line++)
        {
            var start = source.LineStarts[line - 1];
            var end = line < source.LineStarts.Count ? source.LineStarts[line] - 1 : source.Text.Length;
            var content = source.Text[start..end];
            if (content.AsSpan().IndexOfAnyExcept(" \t>") < 0)
            {
                continue;
            }

            if (PromptRegex().Match(content) is not { Success: true } prompt)
            {
                return;
            }

            if (firstPrompt < 0)
            {
                firstPrompt = start + prompt.Value.IndexOf('$', StringComparison.Ordinal);
            }
        }

        if (firstPrompt >= 0)
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.DollarSignInShellCodeRuleId, firstPrompt, "Every command in the code block starts with a dollar sign"));
        }
    }

    /// <summary>
    ///     Reports a paragraph whose first line looks like a heading that is missing a space.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block of the paragraph.</param>
    /// <param name="found">Receives the finding.</param>
    private static void CheckHeadingSpace(SourceDocument source, BlockInfo block, ImmutableArray<Diagnostic>.Builder found)
    {
        // Only a line that starts the line is meant as a heading; after a list marker or a quote marker it is an issue number or a tag
        if (source.PositionOf(block.Start).Column != 1)
        {
            return;
        }

        var lineEnd = source.Text.IndexOf('\n', block.Start);
        var first = source.Text[block.Start..(lineEnd < 0 || lineEnd > block.EndInclusive ? block.EndInclusive + 1 : lineEnd)];
        if (ClosedMissingSpaceRegex().IsMatch(first))
        {
            found.Add(RuleSupport.Finding(
                source,
                RuleCatalog.HeadingClosedMissingSpaceRuleId,
                block.Start,
                "Closed heading has no space inside its number signs, so it is a paragraph"));
        }
        else if (MissingSpaceRegex().IsMatch(first))
        {
            found.Add(RuleSupport.Finding(
                source,
                RuleCatalog.HeadingMissingSpaceRuleId,
                block.Start,
                "Heading has no space after its number signs, so it is a paragraph"));
        }
    }

    /// <summary>
    ///     Reports emphasis with a space inside one delimiter only, line by line.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block of the paragraph.</param>
    /// <param name="paragraph">The paragraph node.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckEmphasisSpace(SourceDocument source, BlockInfo block, ParagraphBlock paragraph, ImmutableArray<Diagnostic>.Builder found)
    {
        // Nothing to read without a delimiter character in the block
        var text = source.Text;
        if (paragraph.Inline is null || text.AsSpan(block.Start, block.EndInclusive - block.Start + 1).IndexOfAny('*', '_') < 0)
        {
            return;
        }

        var map = InlineMap.Build(paragraph.Inline);
        var lineStart = block.Start;
        while (lineStart <= block.EndInclusive)
        {
            var lineFeed = text.IndexOf('\n', lineStart);
            var lineEnd = lineFeed < 0 || lineFeed > block.EndInclusive ? block.EndInclusive + 1 : lineFeed;
            CheckEmphasisLine(source, map, lineStart, lineEnd, found);
            lineStart = lineEnd + 1;
        }
    }

    /// <summary>
    ///     Reports the emphasis with a space on one side only in one line of a paragraph.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="map">The map of plain text of the paragraph.</param>
    /// <param name="start">The offset of the first character of the line.</param>
    /// <param name="end">The offset after the last character of the line.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckEmphasisLine(SourceDocument source, InlineMap map, int start, int end, ImmutableArray<Diagnostic>.Builder found)
    {
        // The delimiter runs that the parser left as plain text and that no backslash escapes
        var text = source.Text;
        var runs = new List<(char Mark, int Start, int End)>();
        var i = start;
        while (i < end)
        {
            if (text[i] is not ('*' or '_') || !map.IsText(i) || LinkRules.IsEscaped(text, i))
            {
                i++;
                continue;
            }

            var j = i;
            while (j < end && text[j] == text[i] && map.IsText(j))
            {
                j++;
            }

            runs.Add((text[i], i, j));
            i = j;
        }

        // Each run is compared with the next run of the same character and length; a pair is reported once
        var used = new bool[runs.Count];
        for (var k = 0; k < runs.Count - 1; k++)
        {
            var open = runs[k];
            var close = runs[k + 1];
            if (used[k] || used[k + 1] || open.Mark != close.Mark || open.End - open.Start != close.End - close.Start || open.End - open.Start > 2)
            {
                continue;
            }

            var inner = text[open.End..close.Start];
            if (!AllText(map, open.End, close.Start))
            {
                continue;
            }
            var spaceAfterOpen = IsBlank(text, open.End, end);
            var spaceBeforeClose = close.Start > start && char.IsWhiteSpace(text[close.Start - 1]);
            var spaceAfterClose = IsBlank(text, close.End, end);
            var punctuationAfterClose = close.End < end && IsPunctuation(text[close.End]);
            var spaceBeforeOpen = open.Start == start || char.IsWhiteSpace(text[open.Start - 1]);
            var openAfterPunctuation = open.Start > start && IsPunctuation(text[open.Start - 1]);

            // Case 1: *text *  the opener is flush against a letter, the closer stands alone
            var trailingSpace = !spaceAfterOpen && (spaceBeforeOpen || openAfterPunctuation)
                && open.End < end && char.IsLetter(text[open.End])
                && spaceBeforeClose && (spaceAfterClose || punctuationAfterClose);

            // Case 2: * text*  the opener stands alone (not after a word), the closer is flush against a word character
            var leadingSpace = spaceAfterOpen && (spaceBeforeOpen || openAfterPunctuation) && !FollowsWord(text, start, open.Start)
                && inner.Trim().Length > 0 && char.IsLetter(inner.TrimStart()[0])
                && !spaceBeforeClose && close.Start > start && char.IsLetterOrDigit(text[close.Start - 1])
                && (spaceAfterClose || punctuationAfterClose);

            if (!trailingSpace && !leadingSpace)
            {
                continue;
            }

            used[k] = used[k + 1] = true;
            var marker = text[open.Start..open.End];
            var snippet = text[open.Start..close.End];
            found.Add(RuleSupport.Finding(
                source,
                RuleCatalog.EmphasisSpaceRuleId,
                open.Start,
                $"Emphasis has a space inside one of its delimiters; use {marker}{Trimmed(inner)}{marker} instead of {Trimmed(snippet)}"));
        }
    }

    /// <summary>
    ///     Determines whether every character of a range is plain text.
    /// </summary>
    /// <param name="map">The map of plain text.</param>
    /// <param name="start">The offset of the first character.</param>
    /// <param name="end">The offset after the last character.</param>
    /// <returns><see langword="true"/> when no code, HTML, link part, emphasis delimiter or other construct is inside the range.</returns>
    private static bool AllText(InlineMap map, int start, int end)
    {
        for (var offset = start; offset < end; offset++)
        {
            if (!map.IsText(offset))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether the character at an offset is a blank, or the offset is the end of the line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="end">The offset after the last character of the line.</param>
    /// <returns><see langword="true"/> at the end of the line and before white space.</returns>
    private static bool IsBlank(string text, int offset, int end) => offset >= end || char.IsWhiteSpace(text[offset]);

    /// <summary>
    ///     Determines whether a character is punctuation or a symbol.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for punctuation and symbols.</returns>
    private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>
    ///     Determines whether the last non-blank character before an offset on the same line is a letter or digit.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The offset of the start of the line.</param>
    /// <param name="offset">The offset to look before.</param>
    /// <returns><see langword="true"/> when a word character comes before.</returns>
    private static bool FollowsWord(string text, int start, int offset)
    {
        var i = offset - 1;
        while (i >= start && char.IsWhiteSpace(text[i]))
        {
            i--;
        }

        return i >= start && char.IsLetterOrDigit(text[i]);
    }

    /// <summary>
    ///     Trims a text and shortens it to 40 characters for a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text, with three dots in place of what is cut off.</returns>
    private static string Trimmed(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 40 ? trimmed : string.Concat(trimmed.AsSpan(0, 40), "...");
    }
}
