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
using DemaConsulting.LintMark.Document;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace DemaConsulting.LintMark.Formatting.Inline;

/// <summary>
///     Cuts the text of a paragraph into the words (atoms) that a wrap may move: whitespace-separated words in which
///     links, code spans, inline HTML and the other constructs that must stay whole are never split.
/// </summary>
/// <remarks>
///     <para>
///         The text of the paragraph is rebuilt from its lines (the content of each line joined with line feeds, see
///         <see cref="DocumentParser.GetLines"/>), and the constructs that must stay whole are marked from the inline
///         syntax tree: links and images (with their text and destination), code spans, links in angle brackets and bare URLs, inline
///         HTML tags, character references, inline math, footnote references and the task list marker. Pandoc
///         attribute groups (<c>{#id .class}</c>), citations (<c>[@key]</c>) and LaTeX commands (<c>\cmd{x}</c>) are marked
///         with patterns, because the parser knows nothing about them. Whitespace outside the marked ranges separates
///         the words; a word that is glued to a marked range (<c>x[a b](c)y</c>) is one atom with it.
///     </para>
///     <para>
///         Hard line breaks (two or more spaces, or a backslash, at the end of a line) cut the paragraph into
///         segments that are never joined; each hard line break found is checked against the hard breaks that the
///         parser reports, and a paragraph where the two disagree is not tokenized. A break is never allowed after a
///         word that ends with an odd number of backslashes (the break would become a hard line break) or after the
///         task list marker, so such a word is merged with the next one into one atom.
///     </para>
///     <para>
///         A line break inside a marked construct is replaced by a single space only where that cannot change the
///         meaning: in a code span whose content is not next to a delimiter or a space at the break and is not padded with a
///         space on both sides (joined, such a span would be rewritten by <c>code-span-space</c>), and in the
///         text of a link that is not an image. A line break in any other construct (inline HTML, math, an image, a
///         link destination or title, a nested code span in a link) makes the paragraph one that is refused, because
///         joining it could change how it reads. A paragraph whose source spans cannot be matched to its lines is
///         refused too. Refused paragraphs are left as written by the formatter.
///     </para>
///     <para>
///         Thread safety: static and stateless apart from compiled patterns with a match timeout; safe for
///         concurrent use. The arguments are only read.
///     </para>
/// </remarks>
internal static partial class AtomTokenizer
{
    /// <summary>
    ///     The longest time a pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     Matches a Pandoc attribute group (<c>{#id .class}</c>), a citation (<c>[@key]</c>, <c>[-@key, p. 3]</c>) or a LaTeX
    ///     command with its braced arguments, on one line.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"\{[^{}\n]{0,200}\}|\[-?@[^\]\n]{1,200}\]|\\[A-Za-z]+(?:\{[^}\n]*\})*",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex PandocAtomRegex();

    /// <summary>
    ///     Matches blanks before a line break inside an atom, together with the line break.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"[ \t]*\n",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex LineBreakInAtomRegex();

    /// <summary>
    ///     Cuts a paragraph into atoms and hard-break segments.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document. Must not be null.</param>
    /// <param name="root">The inline content of the paragraph. Must not be null.</param>
    /// <param name="lines">
    ///     The lines of the paragraph (see <see cref="DocumentParser.GetLines"/>), at least one.
    /// </param>
    /// <param name="breakLinkText">
    ///     Whether the text of an inline link may be broken at its blanks; only the destination part (<c>](destination "title")</c>)
    ///     then stays whole. A link whose destination part is not read with certainty stays whole in any case.
    /// </param>
    /// <returns>
    ///     The segments between hard line breaks with their atoms, and for each line the number of places where it could be
    ///     broken; <see langword="null"/> when the paragraph cannot be cut safely (see the remarks of the class).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static TokenizedParagraph? Tokenize(string text, ContainerInline root, IReadOnlyList<LeafLine> lines, bool breakLinkText = false)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(lines);


        try
        {
            return TokenizeCore(text, root, lines, breakLinkText);
        }
        catch (RegexMatchTimeoutException)
        {
            // An undecided pattern means the atoms cannot be trusted
            return null;
        }
    }

    /// <summary>
    ///     Cuts a validated paragraph into atoms.
    /// </summary>
    /// <param name="text">The LF-normalized text of the document.</param>
    /// <param name="root">The inline content of the paragraph.</param>
    /// <param name="lines">The lines of the paragraph.</param>
    /// <param name="breakLinkText">Whether the text of an inline link may be broken.</param>
    /// <returns>The tokenized paragraph, or <see langword="null"/> when it cannot be cut safely.</returns>
    /// <exception cref="RegexMatchTimeoutException">Thrown when a pattern times out.</exception>
    private static TokenizedParagraph? TokenizeCore(string text, ContainerInline root, IReadOnlyList<LeafLine> lines, bool breakLinkText)
    {
        // Rebuild the text of the paragraph; spans that do not fit the lines make it untrustworthy
        var flat = FlatText.Create(text, lines);
        if (flat is null)
        {
            return null;
        }

        // Mark the constructs that stay whole, from the tree first and then by pattern
        var content = flat.Content;
        var state = new MarkState(flat, new bool[content.Length], text, breakLinkText);
        if (!MarkInlineAtoms(root, state))
        {
            return null;
        }

        foreach (var match in PandocAtomRegex().EnumerateMatches(content))
        {
            state.Atom.AsSpan(match.Index, match.Length).Fill(true);
        }

        // Find the hard line breaks and compare them with the ones the parser found
        var hard = FindHardBreaks(content, state.Atom);
        if (hard.Count != state.ParserHardBreaks)
        {
            return null;
        }

        return BuildParagraph(flat, state, hard);
    }

    /// <summary>
    ///     Marks, from the inline tree, the ranges of the text that must stay whole.
    /// </summary>
    /// <param name="container">The container whose children are visited.</param>
    /// <param name="state">The marks being collected.</param>
    /// <returns><see langword="false"/> when a construct cannot be matched to the text or must not be joined.</returns>
    private static bool MarkInlineAtoms(ContainerInline container, MarkState state)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            var ok = child switch
            {
                LinkInline link => MarkLink(link, state),
                CodeInline code => MarkCode(code, state),
                TaskList task => MarkRange(task, state, taskMarker: true),
                HtmlInline or HtmlEntityInline or MathInline or FootnoteLink or AutolinkInline => MarkRange(child, state, taskMarker: false),
                LineBreakInline lineBreak => CountBreak(lineBreak, state),
                ContainerInline nested => MarkInlineAtoms(nested, state),
                _ => true
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Counts a line break reported by the parser.
    /// </summary>
    /// <param name="lineBreak">The line break.</param>
    /// <param name="state">The marks being collected.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool CountBreak(LineBreakInline lineBreak, MarkState state)
    {
        if (lineBreak.IsHard)
        {
            state.ParserHardBreaks++;
        }

        return true;
    }

    /// <summary>
    ///     Marks a construct that must not hold a line break at all.
    /// </summary>
    /// <param name="inline">The construct.</param>
    /// <param name="state">The marks being collected.</param>
    /// <param name="taskMarker">Whether the construct is the task list marker.</param>
    /// <returns><see langword="false"/> when the span does not fit the text or holds a line break.</returns>
    private static bool MarkRange(MarkdigInline inline, MarkState state, bool taskMarker)
    {
        if (!state.Flat.TryRange(inline, out var first, out var last) || state.Flat.ContainsLineBreak(first, last))
        {
            return false;
        }

        state.Atom.AsSpan(first, last - first + 1).Fill(true);
        if (taskMarker)
        {
            state.TaskMarkerEnds.Add(last);
        }

        return true;
    }

    /// <summary>
    ///     Marks a code span, which may hold a line break that is a space to the parser.
    /// </summary>
    /// <param name="code">The code span.</param>
    /// <param name="state">The marks being collected.</param>
    /// <returns>
    ///     <see langword="false"/> when the span does not fit the text, a line break touches a space or a delimiter, or the
    ///     span has a line break and is padded with a space on both sides (joining it would change the content or make
    ///     <c>code-span-space</c> propose a change).
    /// </returns>
    private static bool MarkCode(CodeInline code, MarkState state)
    {
        if (!state.Flat.TryRange(code, out var first, out var last))
        {
            return false;
        }

        // A line break stands for one space; blanks or backticks next to it make the joined text differ or unstable
        var content = state.Flat.Content;
        var multiLine = false;
        for (var i = first; i <= last; i++)
        {
            if (content[i] != '\n')
            {
                continue;
            }

            multiLine = true;
            if (content[i - 1] is ' ' or '\t' or '`' || content[i + 1] is ' ' or '\t' or '`')
            {
                return false;
            }
        }

        // Joined into one line, a span padded with one space on each side would be one that code-span-space rewrites,
        // so the text would not be stable
        if (multiLine
            && last - first + 1 > 2 * code.DelimiterCount + 1
            && content[first + code.DelimiterCount] == ' '
            && content[last - code.DelimiterCount] == ' ')
        {
            return false;
        }

        state.Atom.AsSpan(first, last - first + 1).Fill(true);
        return true;
    }

    /// <summary>
    ///     Marks a link, image or autolink; the text of a link that is not an image may hold line breaks.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="state">The marks being collected.</param>
    /// <returns><see langword="false"/> when the span does not fit the text or holds a line break that must not be joined.</returns>
    private static bool MarkLink(LinkInline link, MarkState state)
    {
        if (!state.Flat.TryRange(link, out var first, out var last))
        {
            return false;
        }

        // With the option on, only the destination part of an inline link stays whole; its text is read like any other text
        if (state.BreakLinkText
            && !link.IsImage
            && InlineLinkTail.TryRead(state.DocumentText, link) is { } tail
            && state.Flat.TryOffset(tail.Start - 1, out var bracket)
            && state.Flat.TryOffset(tail.End - 1, out var closing)
            && first <= bracket
            && bracket < closing
            && closing <= last)
        {
            state.Atom.AsSpan(bracket, closing - bracket + 1).Fill(true);
            return MarkInlineAtoms(link, state);
        }

        // Count the line breaks of the construct; each must be a soft break in the text of a link
        var breaks = state.Flat.CountLineBreaks(first, last);
        if (breaks > 0)
        {
            if (link.IsImage || link.IsAutoLink)
            {
                return false;
            }

            var soft = 0;
            foreach (var descendant in link.Descendants())
            {
                if (descendant is LineBreakInline { IsHard: true })
                {
                    return false;
                }

                if (descendant is LineBreakInline)
                {
                    soft++;
                }
            }

            // A line break that is not a soft break of the text is in the destination, the title or a nested construct
            if (soft != breaks)
            {
                return false;
            }
        }

        state.Atom.AsSpan(first, last - first + 1).Fill(true);
        return true;
    }

    /// <summary>
    ///     Finds the hard line breaks of the text.
    /// </summary>
    /// <param name="content">The text of the paragraph.</param>
    /// <param name="atom">Which characters belong to a construct that stays whole.</param>
    /// <returns>For each line feed outside a construct that ends a line with a hard break, how it is written.</returns>
    private static Dictionary<int, HardBreakKind> FindHardBreaks(string content, bool[] atom)
    {
        var found = new Dictionary<int, HardBreakKind>();
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != '\n' || atom[i])
            {
                continue;
            }

            // Two or more spaces form a hard break; otherwise an odd number of backslashes does
            var spaces = 0;
            while (i - 1 - spaces >= 0 && content[i - 1 - spaces] == ' ' && !atom[i - 1 - spaces])
            {
                spaces++;
            }

            if (spaces >= 2)
            {
                found[i] = HardBreakKind.Spaces;
                continue;
            }

            var backslashes = 0;
            while (i - 1 - backslashes >= 0 && content[i - 1 - backslashes] == '\\' && !atom[i - 1 - backslashes])
            {
                backslashes++;
            }

            if (backslashes % 2 == 1)
            {
                found[i] = HardBreakKind.Backslash;
            }
        }

        return found;
    }

    /// <summary>
    ///     Builds the segments and the break opportunities from the marked text.
    /// </summary>
    /// <param name="flat">The text of the paragraph with its lines.</param>
    /// <param name="state">The marked constructs.</param>
    /// <param name="hard">The hard line breaks by the position of their line feed.</param>
    /// <returns>The tokenized paragraph.</returns>
    private static TokenizedParagraph BuildParagraph(FlatText flat, MarkState state, Dictionary<int, HardBreakKind> hard)
    {
        var content = flat.Content;
        var atom = state.Atom;
        var segments = ImmutableArray.CreateBuilder<ParagraphSegment>();
        var opportunities = new int[flat.LineCount];
        var current = new List<Word>();

        var position = 0;
        while (position < content.Length)
        {
            var c = content[position];
            if (!atom[position] && c is ' ' or '\t' or '\n')
            {
                // A hard break ends the segment
                if (c == '\n' && hard.TryGetValue(position, out var kind))
                {
                    segments.Add(CloseSegment(current, kind, opportunities, flat, state));
                    current = [];
                }

                position++;
                continue;
            }

            // A word runs to the next whitespace outside a marked range
            var start = position;
            while (position < content.Length && (atom[position] || content[position] is not (' ' or '\t' or '\n')))
            {
                position++;
            }

            current.Add(new Word(start, position, current.Count > 0 && HasLineBreak(content, current[^1].End, start)));
        }

        if (current.Count > 0 || segments.Count == 0)
        {
            segments.Add(CloseSegment(current, HardBreakKind.None, opportunities, flat, state));
        }

        return new TokenizedParagraph(segments.ToImmutable(), [.. opportunities]);
    }

    /// <summary>
    ///     Determines whether a line feed lies in a range of the text.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <param name="from">Start of the range.</param>
    /// <param name="to">Exclusive end of the range.</param>
    /// <returns><see langword="true"/> when the range holds a line feed.</returns>
    private static bool HasLineBreak(string content, int from, int to) => content.AsSpan(from, to - from).Contains('\n');

    /// <summary>
    ///     Merges the words that must not be separated and turns a run of words into a segment.
    /// </summary>
    /// <param name="words">The words of the segment in order.</param>
    /// <param name="kind">How the segment ends.</param>
    /// <param name="opportunities">Receives, per line, the number of places where the line could be broken.</param>
    /// <param name="flat">The text of the paragraph with its lines.</param>
    /// <param name="state">The marked constructs.</param>
    /// <returns>The segment.</returns>
    private static ParagraphSegment CloseSegment(List<Word> words, HardBreakKind kind, int[] opportunities, FlatText flat, MarkState state)
    {
        var atoms = ImmutableArray.CreateBuilder<string>();
        var builder = new StringBuilder();
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var text = LineBreakInAtomRegex().Replace(flat.Content[word.Start..word.End], " ");

            // A word that follows a word that forbids a break joins the atom being built; a word that starts an atom
            // after a gap on the same line is a place where that line could be broken
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }
            else if (i > 0 && !word.GapHasLineBreak)
            {
                opportunities[flat.LineOf(word.Start)]++;
            }

            builder.Append(text);

            // The last word of a segment closes its atom (a trailing backslash of a hard break is part of it)
            var forbidden = i < words.Count - 1 && (EndsWithOddBackslashes(text) || state.TaskMarkerEnds.Contains(word.End - 1));
            if (!forbidden)
            {
                atoms.Add(builder.ToString());
                builder.Clear();
            }
        }

        return new ParagraphSegment(atoms.ToImmutable(), kind);
    }

    /// <summary>
    ///     Determines whether a word ends with an odd number of backslashes.
    /// </summary>
    /// <param name="text">The word.</param>
    /// <returns><see langword="true"/> when the last backslash is not itself escaped, so a line break after it would be a hard break.</returns>
    private static bool EndsWithOddBackslashes(string text)
    {
        var count = 0;
        for (var i = text.Length - 1; i >= 0 && text[i] == '\\'; i--)
        {
            count++;
        }

        return count % 2 == 1;
    }

    /// <summary>
    ///     A word of the text: where it starts and ends, and whether a line break separates it from the word before.
    /// </summary>
    /// <param name="Start">Offset of the first character in the paragraph text.</param>
    /// <param name="End">Exclusive offset of the end.</param>
    /// <param name="GapHasLineBreak">Whether a line feed lies between this word and the previous one of the segment.</param>
    private readonly record struct Word(int Start, int End, bool GapHasLineBreak);

    /// <summary>
    ///     The marks collected while the inline tree is visited.
    /// </summary>
    /// <param name="Flat">The text of the paragraph with its lines.</param>
    /// <param name="Atom">Which characters belong to a construct that stays whole.</param>
    /// <param name="DocumentText">The text of the document, for reading the destination of a link.</param>
    /// <param name="BreakLinkText">Whether the text of an inline link may be broken (only its destination part stays whole).</param>
    private sealed record MarkState(FlatText Flat, bool[] Atom, string DocumentText, bool BreakLinkText)
    {
        /// <summary>
        ///     Gets the end offsets of the task list markers.
        /// </summary>
        public HashSet<int> TaskMarkerEnds { get; } = [];

        /// <summary>
        ///     Gets or sets the number of hard line breaks the parser reported outside the constructs that stay whole.
        /// </summary>
        public int ParserHardBreaks { get; set; }
    }

    /// <summary>
    ///     The text of a paragraph rebuilt from its lines, with the arithmetic between document offsets and text offsets.
    /// </summary>
    private sealed class FlatText
    {
        /// <summary>
        ///     The document offsets where the content of each line starts.
        /// </summary>
        private readonly int[] _documentStarts;

        /// <summary>
        ///     The document offsets where the content of each line ends (exclusive).
        /// </summary>
        private readonly int[] _documentEnds;

        /// <summary>
        ///     The offsets in the paragraph text where each line starts.
        /// </summary>
        private readonly int[] _flatStarts;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlatText"/> class.
        /// </summary>
        /// <param name="content">The text of the paragraph.</param>
        /// <param name="documentStarts">The document offsets where the content of each line starts.</param>
        /// <param name="documentEnds">The document offsets where the content of each line ends.</param>
        /// <param name="flatStarts">The offsets in the text where each line starts.</param>
        private FlatText(string content, int[] documentStarts, int[] documentEnds, int[] flatStarts)
        {
            Content = content;
            _documentStarts = documentStarts;
            _documentEnds = documentEnds;
            _flatStarts = flatStarts;
        }

        /// <summary>
        ///     Gets the text of the paragraph: the content of its lines joined with line feeds.
        /// </summary>
        public string Content { get; }

        /// <summary>
        ///     Gets the number of lines.
        /// </summary>
        public int LineCount => _flatStarts.Length;

        /// <summary>
        ///     Rebuilds the text of a paragraph.
        /// </summary>
        /// <param name="text">The text of the document.</param>
        /// <param name="lines">The lines of the paragraph.</param>
        /// <returns>The rebuilt text, or <see langword="null"/> when a line lies outside the text or the lines overlap.</returns>
        public static FlatText? Create(string text, IReadOnlyList<LeafLine> lines)
        {
            var documentStarts = new int[lines.Count];
            var documentEnds = new int[lines.Count];
            var flatStarts = new int[lines.Count];
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.ContentStart < 0
                    || line.ContentEnd < line.ContentStart
                    || line.ContentEnd > text.Length
                    || (i > 0 && line.ContentStart < documentEnds[i - 1]))
                {
                    return null;
                }

                if (i > 0)
                {
                    builder.Append('\n');
                }

                documentStarts[i] = line.ContentStart;
                documentEnds[i] = line.ContentEnd;
                flatStarts[i] = builder.Length;
                builder.Append(text, line.ContentStart, line.ContentEnd - line.ContentStart);
            }

            return new FlatText(builder.ToString(), documentStarts, documentEnds, flatStarts);
        }

        /// <summary>
        ///     Finds the range of the paragraph text that an inline occupies.
        /// </summary>
        /// <param name="inline">The inline.</param>
        /// <param name="first">Receives the offset of its first character.</param>
        /// <param name="last">Receives the offset of its last character.</param>
        /// <returns><see langword="false"/> when an end of the span is not in the content of a line.</returns>
        public bool TryRange(MarkdigInline inline, out int first, out int last)
        {
            first = ToFlat(inline.Span.Start);
            last = ToFlat(inline.Span.End);
            return first >= 0 && last >= first;
        }

        /// <summary>
        ///     Determines whether a range holds a line feed.
        /// </summary>
        /// <param name="first">Offset of the first character.</param>
        /// <param name="last">Offset of the last character (inclusive).</param>
        /// <returns><see langword="true"/> when a line feed lies in the range.</returns>
        public bool ContainsLineBreak(int first, int last) => CountLineBreaks(first, last) > 0;

        /// <summary>
        ///     Counts the line feeds in a range.
        /// </summary>
        /// <param name="first">Offset of the first character.</param>
        /// <param name="last">Offset of the last character (inclusive).</param>
        /// <returns>The number of line feeds.</returns>
        public int CountLineBreaks(int first, int last) => Content.AsSpan(first, last - first + 1).Count('\n');

        /// <summary>
        ///     Finds the line that holds an offset of the paragraph text.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns>The zero-based index of the line.</returns>
        public int LineOf(int offset)
        {
            var index = Array.BinarySearch(_flatStarts, offset);
            return index >= 0 ? index : ~index - 1;
        }

        /// <summary>
        ///     Converts a document offset to an offset of the paragraph text.
        /// </summary>
        /// <param name="documentOffset">The offset in the document.</param>
        /// <param name="offset">Receives the offset in the paragraph text.</param>
        /// <returns><see langword="false"/> when the offset is not in the content of a line.</returns>
        public bool TryOffset(int documentOffset, out int offset)
        {
            offset = ToFlat(documentOffset);
            return offset >= 0;
        }

        /// <summary>
        ///     Converts a document offset to an offset of the paragraph text.
        /// </summary>
        /// <param name="documentOffset">The offset in the document.</param>
        /// <returns>The offset in the paragraph text, or -1 when it is not in the content of a line.</returns>
        private int ToFlat(int documentOffset)
        {
            var index = Array.BinarySearch(_documentStarts, documentOffset);
            index = index >= 0 ? index : ~index - 1;
            return index >= 0 && documentOffset < _documentEnds[index]
                ? _flatStarts[index] + documentOffset - _documentStarts[index]
                : -1;
        }
    }
}

/// <summary>
///     The words (atoms) of a paragraph between two hard line breaks, and how the segment ends.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. A hard line break is a fixed point of the paragraph: the words
///     before it and the words after it are never joined, so each segment is wrapped on its own. Immutable; safe for
///     concurrent use.
/// </remarks>
/// <param name="Atoms">The atoms of the segment in order, none empty.</param>
/// <param name="End">How the segment ends.</param>
internal sealed record ParagraphSegment(ImmutableArray<string> Atoms, HardBreakKind End);

/// <summary>
///     A paragraph cut into the words (atoms) that a wrap may move, with what a wrap needs to know about the lines it started from.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. Immutable, including its collections; safe for concurrent use.
/// </remarks>
/// <param name="Segments">The segments between hard line breaks, at least one, in order.</param>
/// <param name="BreakOpportunitiesPerLine">
///     For each source line of the paragraph, the number of places inside the line where it could be broken (the gaps
///     between two atoms on that line that a wrap may turn into a line break).
/// </param>
internal sealed record TokenizedParagraph(ImmutableArray<ParagraphSegment> Segments, ImmutableArray<int> BreakOpportunitiesPerLine);
/// <summary>
///     How a segment of a paragraph ends: with nothing, or with a hard line break written in one of its two forms.
/// </summary>
/// <remarks>
///     Supporting type of <see cref="AtomTokenizer"/>. A wrapped paragraph keeps each hard line break in the form it
///     was written in, because the two forms are equal to the parser but the choice between them belongs to the
///     <c>trailing-whitespace</c> rule, not to the wrap.
/// </remarks>
internal enum HardBreakKind
{
    /// <summary>The segment is the end of the paragraph.</summary>
    None,

    /// <summary>The segment ends with a line break written as two or more spaces at the end of the line.</summary>
    Spaces,

    /// <summary>The segment ends with a line break written as a backslash at the end of the line.</summary>
    Backslash
}
