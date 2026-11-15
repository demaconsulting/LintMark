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
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     The rules that decide, before a paragraph is wrapped, that its text must stay exactly as written because joining
///     or moving its lines could change how it is read.
/// </summary>
/// <remarks>
///     <para>
///         Wrapping replaces the blanks between words with line breaks and the line breaks with blanks. The tokenizer and
///         the <see cref="BlockSyntaxGuard"/> keep the words whole and keep block syntax away from the start of the lines
///         the wrap writes. These rules cover the shapes that neither sees, each one a deterministic test of the
///         text and of the parser's tree (no page is compared):
///     </para>
///     <list type="bullet">
///         <item><description><b>W-1</b> a GitHub alert marker (<c>[!NOTE]</c>) that starts a block quote joins the text after it.</description></item>
///         <item><description><b>W-2</b> a line (the first included) that starts with block syntax: its text is a paragraph only because of indentation or laziness.</description></item>
///         <item><description><b>W-3</b> a line break at the start or the end of the text of a link (the parser drops a trailing one), or a blank there when links may be broken.</description></item>
///         <item><description><b>W-4</b> a literal <c>&lt;</c> with a line break before the next <c>&gt;</c>, or two unmarked <c>$</c> with a line break between: joining the lines could close a tag or a math span.</description></item>
///         <item><description><b>W-5</b> a continuation line that is indented more than its container needs: it may hold block syntax that only the indentation hides.</description></item>
///         <item><description><b>W-6</b> a task marker look-alike (<c>[</c>, blanks that are not one space, <c>]</c>) that the tokenizer would turn into <c>[ ]</c>.</description></item>
///         <item><description><b>W-7</b> a first line that would be one complete HTML tag, or one word of block syntax (<c>---</c>, <c>+---+</c>, <c>#</c>): it would start a block by itself (see <see cref="OpensBlockByItself"/>).</description></item>
///         <item><description><b>W-8</b> a quote marker followed by a character that is not a blank: whether the text after it is an autolink depends on the layout.</description></item>
///         <item><description><b>W-9</b> a line that ends with the shape of a grid table border (<c>+---+</c>, <c>+ ---</c>): the parser reads the text next to it by rules of its own.</description></item>
///     </list>
///     <para>
///         Every scan is linear in the size of the paragraph. A pattern that times out counts as a match, the safe answer.
///         Thread safety: static and stateless apart from compiled patterns; safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class WrapGuard
{
    /// <summary>
    ///     The longest time a pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     Matches a line that is one complete HTML open or close tag and nothing else.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^</?[A-Za-z][A-Za-z0-9-]*(?:\s[^<>]*)?/?>$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex LoneTagRegex();

    /// <summary>
    ///     Determines whether a paragraph must stay as written whatever the layout of its lines.
    /// </summary>
    /// <param name="document">The parsed document the paragraph belongs to. Must not be null.</param>
    /// <param name="block">The paragraph block. Must not be null.</param>
    /// <param name="inline">The inline content of the paragraph. Must not be null.</param>
    /// <param name="lines">The lines of the paragraph as written. Must not be null or empty.</param>
    /// <param name="continuationPrefix">The prefix that every continuation line of the paragraph needs. Must not be null.</param>
    /// <param name="breakLinkText">Whether the text of a link may be broken at its blanks.</param>
    /// <returns><see langword="true"/> when one of the rules W-1 to W-6 and W-8 applies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static bool KeepsAsWritten(
        ParsedDocument document,
        BlockInfo block,
        ContainerInline inline,
        IReadOnlyList<LeafLine> lines,
        string continuationPrefix,
        bool breakLinkText)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(inline);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(continuationPrefix);

        var text = document.Source.Text;
        var end = block.EndInclusive + 1;
        return StartsAlert(text, block, lines)
            || HasBlockStartWord(text, lines)
            || HasLinkTextBreak(text, inline, breakLinkText)
            || HasConstructAcrossBreak(text, block.Start, end, inline)
            || HasExtraIndent(text, lines, continuationPrefix)
            || HasBlankTaskMarker(text, block.Start, end)
            || HasQuoteMarkerWithoutBlank(text, lines)
            || EndsInBorder(text, lines);
    }

    /// <summary>
    ///     W-9: a line that ends with something shaped like the border of a grid table.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns>
    ///     <see langword="true"/> when a line ends with a plus sign followed only by dashes, equal signs, colons, plus signs and blanks,
    ///     at least one of them a dash or an equal sign (<c>+---+</c>, <c>+ ---</c>): the parser reads text next to such a line by rules
    ///     of its own (a table, an underline), and joining or moving the lines changes which one applies.
    /// </returns>
    private static bool EndsInBorder(string text, IReadOnlyList<LeafLine> lines)
    {
        foreach (var line in lines)
        {
            var content = text.AsSpan(line.ContentStart, line.ContentEnd - line.ContentStart);

            // The border characters at the end of the line, and a plus sign among them with a dash or an equal sign after it
            var tail = content[(content.LastIndexOfAnyExcept("-=:+ \t") + 1)..];
            var plus = tail.IndexOf('+');
            if (plus >= 0 && tail[(plus + 1)..].ContainsAny('-', '='))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a wrapped first line would start a block by itself.
    /// </summary>
    /// <param name="firstLine">The first line the wrap writes, without its container prefix. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the line, without trailing blanks, is one complete HTML open or close tag, or one word that
    ///     <see cref="BlockSyntaxGuard"/> keeps from the start of a line (a thematic break, a grid table border, a heading marker).
    /// </returns>
    /// <remarks>
    ///     Rule W-7. The paragraph as written has text after such a word on its first line, which is why it is a paragraph; the
    ///     lines after the first are protected by <see cref="BlockSyntaxGuard"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="firstLine"/> is null.</exception>
    public static bool OpensBlockByItself(string firstLine)
    {
        ArgumentNullException.ThrowIfNull(firstLine);

        var line = firstLine.Trim(' ', '\t');
        if (line.Length == 0)
        {
            return false;
        }

        try
        {
            return LoneTagRegex().IsMatch(line)
                || (line.AsSpan().IndexOfAny(' ', '\t') < 0 && BlockSyntaxGuard.IsGuardedLineStart(line));
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    /// <summary>
    ///     W-1: a paragraph of a block quote that starts with an alert marker.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="block">The paragraph.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns><see langword="true"/> when the quote holds the paragraph and its first line starts with <c>[!</c>.</returns>
    private static bool StartsAlert(string text, BlockInfo block, IReadOnlyList<LeafLine> lines) =>
        block.Node?.Parent is QuoteBlock
        && text.AsSpan(lines[0].ContentStart, lines[0].ContentEnd - lines[0].ContentStart).StartsWith("[!", StringComparison.Ordinal);

    /// <summary>
    ///     W-2: a line that starts with block syntax.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns><see langword="true"/> when the first word of any line could start another block.</returns>
    private static bool HasBlockStartWord(string text, IReadOnlyList<LeafLine> lines)
    {
        foreach (var line in lines)
        {
            var content = text.AsSpan(line.ContentStart, line.ContentEnd - line.ContentStart).TrimStart(" \t");
            var blank = content.IndexOfAny(' ', '\t');
            var word = blank < 0 ? content : content[..blank];
            if (!word.IsEmpty && IsInterruptingBlockStart(word.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a word starts a block that a line of a paragraph could hide by indentation or laziness.
    /// </summary>
    /// <param name="word">The first blank-delimited word of a line. Must not be empty.</param>
    /// <returns>
    ///     <see langword="true"/> when the word starts a CommonMark block, except an ordered list marker with a number other than 1:
    ///     such a marker cannot interrupt a paragraph, so it is only text wherever the line is.
    /// </returns>
    internal static bool IsInterruptingBlockStart(string word)
    {
        if (!BlockSyntaxGuard.IsCommonMarkBlockStart(word))
        {
            return false;
        }

        var digits = word.TrimEnd('.', ')');
        return !(digits.Length > 0 && digits.All(char.IsAsciiDigit) && digits.TrimStart('0') != "1" && digits.Length < word.Length);
    }

    /// <summary>
    ///     W-3: a line break at the start or the end of the text of a link.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="inline">The inline content of the paragraph.</param>
    /// <param name="breakLinkText">Whether the text of a link may be broken at its blanks.</param>
    /// <returns><see langword="true"/> when a link would read differently with a blank in place of such a line break.</returns>
    private static bool HasLinkTextBreak(string text, ContainerInline inline, bool breakLinkText)
    {
        foreach (var link in inline.Descendants<LinkInline>())
        {
            if (link.IsAutoLink || link.IsImage || link.FirstChild is not { } first || link.LastChild is not { } last)
            {
                continue;
            }

            // A line break node at either end of the text is the case itself
            if (first is LineBreakInline || last is LineBreakInline)
            {
                return true;
            }

            // Characters between the bracket and the first node, and between the last node and the closing bracket
            var open = link.Span.Start;
            if (open < 0 || first.Span.Start <= open || last.Span.End + 1 >= text.Length)
            {
                continue;
            }

            if (text.AsSpan(open + 1, first.Span.Start - open - 1).Contains('\n')
                || (breakLinkText && text[open + 1] is ' ' or '\t'))
            {
                return true;
            }

            var position = last.Span.End + 1;
            var sawBreak = false;
            while (position < text.Length && text[position] is ' ' or '\t' or '\n' or '>')
            {
                sawBreak |= text[position] == '\n';
                position++;
            }

            if (position < text.Length && text[position] == ']' && (sawBreak || (breakLinkText && position > last.Span.End + 1)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     W-4: a literal angle bracket or a pair of unmarked dollar signs with a line break inside.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="start">The offset where the paragraph starts.</param>
    /// <param name="end">The offset where the paragraph ends (exclusive).</param>
    /// <param name="inline">The inline content of the paragraph.</param>
    /// <returns>
    ///     <see langword="true"/> when a <c>&lt;</c> that the parser read as text has a line break before the next <c>&gt;</c>, or
    ///     the first and the last <c>$</c> that the parser read as text have a line break between them.
    /// </returns>
    private static bool HasConstructAcrossBreak(string text, int start, int end, ContainerInline inline)
    {
        // The literals of the paragraph: the places where such characters are plain text
        var angles = new SortedSet<int>();
        var firstDollar = -1;
        var lastDollar = -1;
        foreach (var literal in inline.Descendants<LiteralInline>())
        {
            if (literal.Parent is LinkInline { IsAutoLink: true } || literal.Span.Start < start || literal.Span.End >= end)
            {
                continue;
            }

            for (var position = literal.Span.Start; position <= literal.Span.End; position++)
            {
                switch (text[position])
                {
                    case '<':
                        angles.Add(position);
                        break;
                    case '$':
                        firstDollar = firstDollar < 0 ? position : Math.Min(firstDollar, position);
                        lastDollar = Math.Max(lastDollar, position);
                        break;
                }
            }
        }

        // Two dollar signs that the parser did not pair, with a line break between them
        if (firstDollar >= 0 && lastDollar > firstDollar && text.AsSpan(firstDollar, lastDollar - firstDollar).Contains('\n'))
        {
            return true;
        }

        // One pass over the paragraph: an angle bracket stays open until the next closing bracket, and a line break while it is open is the case
        if (angles.Count == 0)
        {
            return false;
        }

        var open = false;
        for (var position = angles.Min; position < end; position++)
        {
            if (angles.Contains(position))
            {
                open = true;
            }
            else if (text[position] == '>')
            {
                open = false;
            }
            else if (open && text[position] == '\n')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     W-5: a continuation line that is indented more than its container needs.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <param name="continuationPrefix">The prefix that every continuation line needs.</param>
    /// <returns><see langword="true"/> when the text before the content of a continuation line is wider than that prefix.</returns>
    private static bool HasExtraIndent(string text, IReadOnlyList<LeafLine> lines, string continuationPrefix)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            if (DocumentParser.IsLongerPrefix(text[lines[i].LineStart..lines[i].ContentStart], continuationPrefix))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     W-6: square brackets around blanks that are not exactly one space.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="start">The offset where the paragraph starts.</param>
    /// <param name="end">The offset where the paragraph ends (exclusive).</param>
    /// <returns><see langword="true"/> when <c>[</c>, a run of blanks that is not one space, and <c>]</c> follow each other.</returns>
    private static bool HasBlankTaskMarker(string text, int start, int end)
    {
        for (var position = start; position < end; position++)
        {
            if (text[position] != '[')
            {
                continue;
            }

            var close = position + 1;
            while (close < end && text[close] is ' ' or '\t')
            {
                close++;
            }

            var blanks = close - position - 1;
            if (close < end && text[close] == ']' && blanks > 0 && (blanks > 1 || text[position + 1] == '\t'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     W-8: a quote marker with no blank after it.
    /// </summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <returns><see langword="true"/> when the text before the content of any line ends with a quote marker.</returns>
    private static bool HasQuoteMarkerWithoutBlank(string text, IReadOnlyList<LeafLine> lines)
    {
        foreach (var line in lines)
        {
            if (line.ContentStart > line.LineStart && text[line.ContentStart - 1] == '>')
            {
                return true;
            }
        }

        return false;
    }
}
