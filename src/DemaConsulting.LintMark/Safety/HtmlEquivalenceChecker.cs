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

using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.LintMark.Document;
using Markdig;

namespace DemaConsulting.LintMark.Safety;

/// <summary>
///     Decides whether two versions of a document render to the same HTML, the first safety check before a
///     rewritten file may be written.
/// </summary>
/// <remarks>
///     <para>
///         Both texts are rendered with the tool's fixed pipeline and the HTML is normalized so that
///         differences which cannot change the rendered page are ignored: whitespace runs collapse to one
///         space and whitespace next to a block-level tag disappears. Whitespace between inline tags is
///         never touched (otherwise <c>*a* *b*</c> becoming <c>*a**b*</c> would go unnoticed), the content of
///         <c>pre</c>, <c>script</c>, <c>style</c> and <c>textarea</c> elements is compared exactly, and the
///         content of inline <c>code</c> is compared exactly except that a line ending counts as a space (the
///         CommonMark rule). Heading identifiers are dropped, except inside those verbatim elements. Only
///         HTML whitespace (space, tab, line feed, carriage return, form feed) is collapsed, so a changed
///         no-break space is a difference.
///     </para>
///     <para>
///         Rendering is done on LF-normalized text only, because the Markdown parser renders a multi-line code
///         span differently for CRLF input; text that contains a carriage return is rejected. A check that
///         cannot be completed (the renderer throws, a pattern times out) reports a difference, so an
///         undecided check refuses the rewrite.
///     </para>
///     <para>
///         Thread safety: static and stateless apart from the immutable shared pipeline and compiled patterns;
///         safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class HtmlEquivalenceChecker
{
    /// <summary>
    ///     The longest time a normalization pattern may run, as a guard against pathological input.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The character that brackets verbatim parts of the normalized text so they cannot merge with their surroundings.
    /// </summary>
    private const char VerbatimMarker = '\u0001';

    /// <summary>
    ///     The marker for an opening-tag position that has not been searched for yet.
    /// </summary>
    private const int UnknownPosition = -1;

    /// <summary>
    ///     How far before the first difference an excerpt starts, so the reader sees some context.
    /// </summary>
    private const int ContextBefore = 20;

    /// <summary>
    ///     The elements whose content is whitespace-sensitive, with their closing tag and whether a line ending
    ///     counts as a space inside them.
    /// </summary>
    private static readonly (string Name, string Close, bool LineEndingIsSpace)[] VerbatimElements =
    [
        ("pre", "</pre>", false),
        ("code", "</code>", true),
        ("script", "</script>", false),
        ("style", "</style>", false),
        ("textarea", "</textarea>", false)
    ];

    /// <summary>
    ///     Matches the identifier of a heading, written with double or single quotes directly after the tag name
    ///     (as the renderer writes it) once whitespace has been collapsed.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"(<h[1-6]) id=(?:""[^""]*""|'[^']*')",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex HeadingIdRegex();

    /// <summary>
    ///     Matches a run of HTML whitespace.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @"[ \t\r\n\f]+",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    ///     Matches a block-level tag together with the whitespace around it. The tag ends at the first
    ///     <c>&gt;</c> and may not contain a <c>&lt;</c>, so an opening tag that never ends is not searched to the
    ///     end of the text (which would be quadratic for many such tags); it only keeps its surrounding spaces.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(
        @" ?(</?(?:p|li|ul|ol|h[1-6]|blockquote|table|thead|tbody|tr|td|th|pre|hr|div|section|details|summary|dl|dt|dd)\b[^<>]*>) ?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex BlockTagRegex();

    /// <summary>
    ///     Checks that two versions of a document render to the same page.
    /// </summary>
    /// <param name="beforeLf">The original LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="afterLf">The rewritten LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <returns>
    ///     An equivalent result when the normalized HTML is identical; otherwise a result that names the first
    ///     difference with some context, or states that the check could not be completed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an argument contains a carriage return.</exception>
    public static EquivalenceResult Check(string beforeLf, string afterLf) =>
        Check(beforeLf, afterLf, text => Markdown.ToHtml(text, DocumentParser.Pipeline));

    /// <summary>
    ///     Checks that two versions of a document render to the same page, using the given renderer.
    /// </summary>
    /// <param name="beforeLf">The original LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="afterLf">The rewritten LF-normalized text. Must not be null or contain a carriage return.</param>
    /// <param name="render">The function that renders a text to HTML. Must not be null.</param>
    /// <returns>
    ///     An equivalent result when the normalized HTML is identical; otherwise a result that names the first
    ///     difference with some context, or states that the check could not be completed.
    /// </returns>
    /// <remarks>
    ///     This overload is a seam for tests, so that a renderer that fails can be simulated; the public
    ///     overload uses the tool's fixed pipeline.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a text contains a carriage return.</exception>
    internal static EquivalenceResult Check(string beforeLf, string afterLf, Func<string, string> render)
    {
        // Validate input: only LF-normalized text is rendered
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(beforeLf);
        ArgumentNullException.ThrowIfNull(afterLf);
        if (beforeLf.Contains('\r'))
        {
            throw new ArgumentException("The text must be LF-normalized.", nameof(beforeLf));
        }

        if (afterLf.Contains('\r'))
        {
            throw new ArgumentException("The text must be LF-normalized.", nameof(afterLf));
        }

        // Identical text renders identically
        if (string.Equals(beforeLf, afterLf, StringComparison.Ordinal))
        {
            return EquivalenceResult.Equivalent;
        }

        // Render and normalize both sides; anything that goes wrong means the equivalence is not established
        try
        {
            var before = Normalize(render(beforeLf));
            var after = Normalize(render(afterLf));
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return EquivalenceResult.Equivalent;
            }

            var index = EquivalenceResult.FirstDifference(before, after);
            var from = Math.Max(0, index - ContextBefore);
            return EquivalenceResult.Different(
                $"Rendered output differs at offset {index}: before \"{EquivalenceResult.Excerpt(before, from)}\" after \"{EquivalenceResult.Excerpt(after, from)}\"");
        }
        // A check that cannot be completed must refuse the rewrite, whatever the cause
        catch (Exception ex)
        {
            return EquivalenceResult.Different($"The rendered output could not be compared ({ex.GetType().Name})");
        }
    }

    /// <summary>
    ///     Normalizes rendered HTML so that only differences that can change the page remain.
    /// </summary>
    /// <param name="html">The rendered HTML.</param>
    /// <returns>The normalized text.</returns>
    /// <remarks>
    ///     The text is cut into plain parts and verbatim elements by a single left-to-right scan. An element
    ///     without a closing tag runs to the end of the text (as a browser reads it), so everything after it is
    ///     compared exactly. An opening tag that never ends is plain text, and once one such tag is seen no
    ///     later tag of the same kind is searched for. The position of the next opening tag of each kind is
    ///     remembered and searched for again only after the scan has passed it. These rules keep the scan
    ///     linear even for input made of thousands of unclosed tags or of very many code spans.
    /// </remarks>
    private static string Normalize(string html)
    {
        var builder = new StringBuilder(html.Length);
        var nextOpening = new int[VerbatimElements.Length];
        Array.Fill(nextOpening, UnknownPosition);
        var plainStart = 0;
        while (TryFindVerbatim(html, plainStart, nextOpening, out var start, out var end, out var element))
        {
            // The plain text before the element is normalized, the element itself is kept (almost) exactly
            builder.Append(NormalizePlain(html.AsSpan(plainStart, start - plainStart)));
            var verbatim = html.AsSpan(start, end - start);
            builder.Append(VerbatimMarker);
            builder.Append(element.LineEndingIsSpace ? verbatim.ToString().Replace('\n', ' ') : verbatim.ToString());
            builder.Append(VerbatimMarker);
            plainStart = end;
        }

        builder.Append(NormalizePlain(html.AsSpan(plainStart)));
        return builder.ToString().Trim(' ');
    }

    /// <summary>
    ///     Finds the next verbatim element (opening tag, content and closing tag) at or after a position.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="from">The offset to search from.</param>
    /// <param name="nextOpening">
    ///     Per element kind, the remembered offset of its next opening tag, <see cref="UnknownPosition"/> when it
    ///     must be searched for, or <see cref="int.MaxValue"/> when there is none left; updated here.
    /// </param>
    /// <param name="start">Receives the offset of the opening tag.</param>
    /// <param name="end">Receives the exclusive end offset of the closing tag (the end of the text when there is none).</param>
    /// <param name="element">Receives the kind of element found.</param>
    /// <returns><see langword="true"/> when an element was found.</returns>
    private static bool TryFindVerbatim(
        string html,
        int from,
        int[] nextOpening,
        out int start,
        out int end,
        out (string Name, string Close, bool LineEndingIsSpace) element)
    {
        while (true)
        {
            // Pick the kind whose opening tag comes first
            var best = -1;
            var bestStart = int.MaxValue;
            for (var i = 0; i < VerbatimElements.Length; i++)
            {
                // A remembered position stays right until the scan has passed it
                if (nextOpening[i] < from)
                {
                    var found = FindOpeningTag(html, VerbatimElements[i].Name, from);
                    nextOpening[i] = found >= 0 ? found : int.MaxValue;
                }

                if (nextOpening[i] < bestStart)
                {
                    best = i;
                    bestStart = nextOpening[i];
                }
            }

            if (best < 0)
            {
                start = end = 0;
                element = default;
                return false;
            }

            // The element needs the end of its opening tag; without a closing tag it runs to the end of the
            // text, as a browser reads it, so that nothing after it can be compared loosely
            element = VerbatimElements[best];
            var openEnd = html.IndexOf('>', bestStart + element.Name.Length + 1);
            if (openEnd >= 0)
            {
                var close = html.IndexOf(element.Close, openEnd + 1, StringComparison.OrdinalIgnoreCase);
                start = bestStart;
                end = close >= 0 ? close + element.Close.Length : html.Length;
                return true;
            }

            // An opening tag that never ends is plain text, and so is every later one of this kind
            nextOpening[best] = int.MaxValue;
        }
    }

    /// <summary>
    ///     Finds the next opening tag of an element, ignoring case, that is not the prefix of a longer name.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="name">The element name.</param>
    /// <param name="from">The offset to search from.</param>
    /// <returns>The offset of the <c>&lt;</c>, or -1.</returns>
    private static int FindOpeningTag(string html, string name, int from)
    {
        var tag = "<" + name;
        var index = html.IndexOf(tag, from, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            // The name must end here: the next character is not part of a longer word
            var next = index + tag.Length;
            if (next >= html.Length || !(char.IsAsciiLetterOrDigit(html[next]) || html[next] == '_'))
            {
                return index;
            }

            index = html.IndexOf(tag, next, StringComparison.OrdinalIgnoreCase);
        }

        return -1;
    }

    /// <summary>
    ///     Normalizes HTML outside whitespace-sensitive elements.
    /// </summary>
    /// <param name="plain">The plain part.</param>
    /// <returns>
    ///     The part with whitespace collapsed, heading identifiers dropped, and spaces removed next to
    ///     block-level tags.
    /// </returns>
    private static string NormalizePlain(ReadOnlySpan<char> plain)
    {
        // Heading identifiers are dropped here only, so that one inside a verbatim element stays exact
        var collapsed = WhitespaceRegex().Replace(plain.ToString(), " ");
        var withoutIds = HeadingIdRegex().Replace(collapsed, "$1");
        return BlockTagRegex().Replace(withoutIds, "$1");
    }
}
