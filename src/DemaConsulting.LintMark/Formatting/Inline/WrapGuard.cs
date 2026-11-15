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
    /// <returns><see langword="true"/> when one of the rules W-1 to W-6, W-8 and W-9 applies.</returns>
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

/// <summary>
///     Decides which text must never start a line of a wrapped paragraph, because Markdown (or Pandoc) would read
///     the line as the start of another block.
/// </summary>
/// <remarks>
///     <para>
///         A paragraph is wrapped by replacing the spaces between its words with line breaks. A line break in the
///         wrong place turns the next word into block syntax: a <c>-</c>, <c>+</c> or <c>*</c> becomes a list item, a
///         number followed by a period (<c>2026.</c>) an ordered list item, a run of <c>=</c> or <c>-</c> a setext
///         underline, a <c>#</c> a heading, a <c>&gt;</c> a quote, three backticks a fence, a row of pipes and dashes a
///         table delimiter, a <c>[label]:</c> a definition, an HTML block tag an HTML block, and the Pandoc
///         constructs (<c>:::</c>, a LaTeX command, <c>$$</c>, a grid table border) their own blocks. The wrapper
///         asks this guard about the first word of every line it starts and moves the break elsewhere when the
///         answer is yes. The guard never escapes anything: escaping would change the text, and a wrap that needs
///         it keeps the word on the previous line instead.
///     </para>
///     <para>
///         The test is deliberately wider than what the parser accepts (it is applied to a whole word, never to the
///         context), so it can keep a harmless word off the start of a line but never lets a harmful one through.
///         A word such as <c>-foo</c>, <c>*foo*</c> or <c>1.5</c> is not block syntax and is not guarded. The
///         paragraph-level test <see cref="IsPandocStructuralLine"/> finds the lines that mark a paragraph as Pandoc
///         structure, which is then left as written.
///     </para>
///     <para>
///         Thread safety: static and stateless apart from compiled patterns with a match timeout; safe for
///         concurrent use. A pattern that times out counts as a match (the safe answer).
///     </para>
/// </remarks>
internal static partial class BlockSyntaxGuard
{
    /// <summary>
    ///     The longest time a pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     A pattern for a roman numeral in one case (at least one letter), as used by a fancy list marker.
    /// </summary>
    private const string RomanPattern = @"(?:(?=[ivxlcdm])m{0,3}(?:cm|cd|d?c{0,3})(?:xc|xl|l?x{0,3})(?:ix|iv|v?i{0,3})|(?=[IVXLCDM])M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3}))";

    /// <summary>
    ///     Matches a word that is a bullet marker, a thematic break or a setext underline: a plus sign, or only dashes,
    ///     asterisks, underscores or equal signs.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^(?:\+|[-*_=]+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex BulletOrRuleRegex();

    /// <summary>
    ///     Matches a word that is an ordered list marker: one to nine digits and a period or a parenthesis.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^\d{1,9}[.)]$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex NumberMarkerRegex();

    /// <summary>
    ///     Matches a word that is the number signs of a heading (one to six).
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^#{1,6}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex HeadingMarkerRegex();

    /// <summary>
    ///     Matches a word that starts a reference or footnote definition (<c>[label]:</c>).
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^\[\^?[^\]]+\]:", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex DefinitionRegex();

    /// <summary>
    ///     Matches a word that starts a block-level HTML construct: a tag of the block-level list, a raw text
    ///     element, a comment, a processing instruction, a declaration or a CDATA section.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^<(?:/?(?:address|article|aside|base|basefont|blockquote|body|caption|center|col|colgroup|dd|details|dialog|dir|div|dl|dt|fieldset|figcaption|figure|footer|form|frame|frameset|h[1-6]|head|header|hr|html|iframe|legend|li|link|main|menu|menuitem|nav|noframes|ol|optgroup|option|p|param|section|source|summary|table|tbody|td|tfoot|th|thead|title|tr|track|ul)(?:[\s/>]|$)|pre|script|style|textarea|!--|\?|!\[CDATA\[|![A-Za-z])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex HtmlBlockRegex();

    /// <summary>
    ///     Matches a word that is a table delimiter row or a part of one: only dashes, colons and pipes with at least one dash.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^[-:|]*-[-:|]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex TableDelimiterRegex();

    /// <summary>
    ///     Matches a word that is a Pandoc construct at the start of a line: a fenced div, a lone colon or tilde (definition list), a
    ///     LaTeX command, a display math fence, a raw attribute, a lone percent sign (title block) or a grid table border.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^(?::::|:$|~$|\$\$|\{=|%$|\\[A-Za-z]|\+[-=:+]*\+?$)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex PandocWordRegex();

    /// <summary>
    ///     Matches a word that is the marker of a fancy list or an example list: a letter, a roman numeral, a number
    ///     or a <c>#</c> or <c>@</c> with a period or a closing parenthesis (and an opening one for the parenthesized
    ///     forms): <c>a.</c>, <c>a)</c>, <c>(a)</c>, <c>(1)</c>, <c>(@)</c>, <c>(@label)</c>, <c>#.</c>, <c>iv.</c>.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^(?:(?:#|[A-Za-z]|" + RomanPattern + @")[.)]|\((?:#|@[A-Za-z0-9_:.+-]*|[0-9]+|[A-Za-z]|" + RomanPattern + @")\))$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex FancyMarkerWordRegex();

    /// <summary>
    ///     Matches a content line that marks its paragraph as Pandoc structure: a fenced div, a LaTeX command, a display math
    ///     fence, a raw attribute, a title block line, a grid table border, a definition list marker (a colon or a tilde
    ///     and a blank), a line block (a bar and a blank), or a fancy or example list marker followed by a blank.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"^(?::::|\\[A-Za-z]|\$\$|\{=|%(?:\s|$)|\+[-=:+ ]*\+$|[:~](?:[ \t]|$)|\|(?:[ \t]|$)|(?:(?:#|[A-Za-z]|" + RomanPattern + @")[.)]|\((?:#|@[A-Za-z0-9_:.+-]*|[0-9]+|[A-Za-z]|" + RomanPattern + @")\))(?:[ \t]|$))",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex PandocLineRegex();

    /// <summary>
    ///     Determines whether a word must not start a line of a wrapped paragraph.
    /// </summary>
    /// <param name="atom">
    ///     The first word (atom) of the line that would be started. Must not be null or empty. A word that holds spaces
    ///     (a link, a code span) is tested by its first characters.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the word could be read as a list marker, ordered list marker (<c>1.</c>,
    ///     <c>1)</c>, <c>2026.</c>), thematic break or setext underline, heading marker, quote, code fence, reference or
    ///     footnote definition, block-level HTML, table row or delimiter, or Pandoc construct; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     A pattern that times out is treated as a match, so the answer errs on the side of keeping the word off the line start.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atom"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atom"/> is empty.</exception>
    public static bool IsGuardedLineStart(string atom)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(atom);

        try
        {
            // The quick tests on the first characters decide most words, the patterns the rest
            return atom[0] is '>' or '|'
                || atom.StartsWith("```", StringComparison.Ordinal)
                || atom.StartsWith("~~~", StringComparison.Ordinal)
                || BulletOrRuleRegex().IsMatch(atom)
                || NumberMarkerRegex().IsMatch(atom)
                || HeadingMarkerRegex().IsMatch(atom)
                || DefinitionRegex().IsMatch(atom)
                || HtmlBlockRegex().IsMatch(atom)
                || TableDelimiterRegex().IsMatch(atom)
                || PandocWordRegex().IsMatch(atom)
                || FancyMarkerWordRegex().IsMatch(atom);
        }
        catch (RegexMatchTimeoutException)
        {
            // An undecided test keeps the word away from the start of a line
            return true;
        }
    }

    /// <summary>
    ///     Determines whether a word starts a CommonMark or GFM block: the test of <see cref="IsGuardedLineStart"/> without the
    ///     Pandoc and fancy list words.
    /// </summary>
    /// <param name="word">The first blank-delimited word of a line. Must not be null or empty.</param>
    /// <returns>
    ///     <see langword="true"/> when the word could be read as a bullet or ordered list marker, thematic break or setext
    ///     underline, heading marker, quote, code fence, definition, block-level HTML, or table row or delimiter.
    /// </returns>
    /// <remarks>A pattern that times out counts as a match, the safe answer.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="word"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="word"/> is empty.</exception>
    public static bool IsCommonMarkBlockStart(string word)
    {
        // Validate input
        ArgumentException.ThrowIfNullOrEmpty(word);

        try
        {
            return word[0] is '>' or '|'
                || word.StartsWith("```", StringComparison.Ordinal)
                || word.StartsWith("~~~", StringComparison.Ordinal)
                || BulletOrRuleRegex().IsMatch(word)
                || NumberMarkerRegex().IsMatch(word)
                || HeadingMarkerRegex().IsMatch(word)
                || DefinitionRegex().IsMatch(word)
                || HtmlBlockRegex().IsMatch(word)
                || TableDelimiterRegex().IsMatch(word);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    /// <summary>
    ///     Determines whether a content line makes its paragraph Pandoc structure that must be left as written.
    /// </summary>
    /// <param name="line">The content of one line of a paragraph, without its container prefix. Must not be null.</param>
    /// <returns>
    ///     <see langword="true"/> when the line, ignoring leading blanks, starts with <c>:::</c>, a backslash and a letter,
    ///     <c>$$</c>, <c>{=</c>, a percent sign followed by a blank or the end of the line, a colon, a tilde or a bar followed
    ///     by a blank (definition list, line block), or a fancy or example list marker (<c>a.</c>, <c>(a)</c>, <c>(@)</c>,
    ///     <c>#.</c>, <c>iv)</c>) followed by a blank, or is a grid table border.
    /// </returns>
    /// <remarks>
    ///     A pattern that times out counts as structure, so an undecided line leaves the paragraph alone.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="line"/> is null.</exception>
    public static bool IsPandocStructuralLine(string line)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(line);

        try
        {
            return PandocLineRegex().IsMatch(line.AsSpan().TrimStart(" \t"));
        }
        catch (RegexMatchTimeoutException)
        {
            // An undecided line leaves its paragraph alone
            return true;
        }
    }
}
