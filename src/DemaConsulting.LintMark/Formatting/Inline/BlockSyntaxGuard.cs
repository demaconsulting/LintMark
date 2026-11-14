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

namespace DemaConsulting.LintMark.Formatting.Inline;

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
