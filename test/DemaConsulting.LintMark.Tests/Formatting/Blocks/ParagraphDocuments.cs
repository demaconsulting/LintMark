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
using DemaConsulting.LintMark.Formatting.Inline;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Deterministic generator of adversarial paragraphs for the paragraph wrap: words of every script and width, very long
///     words, links with delimiters in their text, code spans, inline HTML, entities, footnote references, Pandoc constructs,
///     hard breaks at any place, words that Markdown reads as block syntax when they start a line, and every container
///     prefix (nested lists, quotes, task items) with normal, lazy and tab-indented continuation lines.
/// </summary>
/// <remarks>
///     The generator is a pure function of its seed (it uses the arithmetic generator of
///     <see cref="ListQuoteInlineDocuments"/>, never <see cref="Random"/>), so the same documents are produced on every
///     platform and runtime. The documents use LF line endings; tests re-encode them to other forms.
/// </remarks>
internal static class ParagraphDocuments
{
    /// <summary>
    ///     Ordinary words.
    /// </summary>
    private static readonly string[] Words =
    [
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa", "lambda", "mu", "nu", "xi", "omicron",
        "a", "an", "the", "of", "to", "is", "in", "it", "on", "as", "by", "text,", "end.", "word;", "(see", "below)", "\"quoted\"", "it's"
    ];

    /// <summary>
    ///     Words in other scripts, with wide characters, emoji and combining marks.
    /// </summary>
    private static readonly string[] Wide =
    [
        "日本語", "日本語のテキスト", "한국어", "中文文本", "😀", "😀😀😀", "👨‍👩‍👧", "🇯🇵", "e\u0301", "e\u0301e\u0301e\u0301", "a\u0308b", "ひらがな",
        "çà", "ｱｲｳｴｵ"
    ];

    /// <summary>
    ///     Constructs that must stay whole when a paragraph is wrapped.
    /// </summary>
    private static readonly string[] Atoms =
    [
        "[link text here](http://example.org/a_b*c)", "[a *b* c](http://x.org \"title here\")", "[`code in link`](u)", "[a_b_c](u)", "[*](u)",
        "`code span here`", "`a`", "`` ` ``", "`` a ` b ``", "<span class=\"a b\">", "</span>", "<b>x y</b>", "<br/>", "&amp;", "&#35;", "&copy;",
        "$x + y$", "[^1]", "{#id .cls}", "[@key, p. 3]", "\\cmd{a b}", "![alt text](img.png)", "<http://x.org/a>", "http://x.com/a_b",
        "www.example.org", "*emphasis here*", "**strong here**", "_under score_", "~~struck text~~", "\\*", "\\_", "\\\\", "\\#", "C:\\temp\\", "a\\",
        "[ref label]", "[ref]", "<!-- c -->"
    ];

    /// <summary>
    ///     Words that Markdown reads as the start of another block when they stand at the start of a line.
    /// </summary>
    private static readonly string[] Hazards =
    [
        "-", "+", "*", "2026.", "1.", "1)", "10.", "#", "##", ">", ">>", "---", "===", "___", "|", "|-|", ":--", "```", "~~~", "[x]:", "[^1]:", "<div>",
        ":::", "$$", "{=html}", "\\section", "%", "+---+"
    ];

    /// <summary>
    ///     Pieces of emphasis that span line breaks.
    /// </summary>
    private static readonly string[] Spans = ["*span", "words*", "_span", "words_", "**bold", "text**", "~~strike", "text~~"];

    /// <summary>
    ///     The prefix of the first line and the prefix of a regular continuation line of each container.
    /// </summary>
    private static readonly (string First, string Next)[] Containers =
    [
        (string.Empty, string.Empty), (string.Empty, string.Empty), (string.Empty, string.Empty), ("- ", "  "), ("* ", "  "), ("+ ", "  "), ("1. ", "   "),
        ("10. ", "    "), ("> ", "> "), ("> > ", "> > "), (">", ">"), ("- > ", "  > "), ("- [ ] ", "  "), ("   ", "   "), ("1. - ", "     "), ("- 1. ", "     ")
    ];

    /// <summary>
    ///     Generates documents.
    /// </summary>
    /// <param name="seed">The seed of the generator.</param>
    /// <param name="count">The number of documents.</param>
    /// <returns>The documents, each with a unique name.</returns>
    public static IEnumerable<SyntheticDocument> Generate(uint seed, int count)
    {
        var random = new ListQuoteInlineDocuments.Lcg(seed);
        for (var i = 0; i < count; i++)
        {
            yield return new SyntheticDocument($"paragraph-{seed}-{i}", "P", Document(random), false, null);
        }
    }

    /// <summary>
    ///     Generates one document: some paragraphs, with a heading or a footnote definition now and then.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The text.</returns>
    private static string Document(ListQuoteInlineDocuments.Lcg random)
    {
        var builder = new StringBuilder();
        if (random.Next(6) == 0)
        {
            builder.Append("# Title\n\n");
        }

        var paragraphs = 1 + random.Next(3);
        for (var i = 0; i < paragraphs; i++)
        {
            builder.Append(Paragraph(random)).Append('\n');
            if (i < paragraphs - 1 || random.Next(2) == 0)
            {
                builder.Append('\n');
            }
        }

        if (random.Next(4) == 0)
        {
            builder.Append("\n[^1]: A footnote that is long enough to need wrapping when the width is small.\n");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Generates one paragraph with a container around it.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The lines of the paragraph, each ended with a line feed except the last.</returns>
    private static string Paragraph(ListQuoteInlineDocuments.Lcg random)
    {
        var (first, next) = random.Pick(Containers);
        var lines = 1 + random.Next(6);
        var builder = new StringBuilder();
        for (var line = 0; line < lines; line++)
        {
            // A continuation line has the prefix of the container, none (lazy), or a tab
            var prefix = line == 0 ? first : random.Next(8) switch
            {
                0 => string.Empty,
                1 => "\t",
                _ => next
            };
            builder.Append(prefix).Append(Line(random, line == lines - 1));
            if (line < lines - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Generates the content of one line.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="last">Whether this is the last line of the paragraph, which has no hard break.</param>
    /// <returns>The content of the line.</returns>
    private static string Line(ListQuoteInlineDocuments.Lcg random, bool last)
    {
        var tokens = 1 + random.Next(14);
        var builder = new StringBuilder();
        for (var i = 0; i < tokens; i++)
        {
            if (i > 0)
            {
                builder.Append(random.Next(12) == 0 ? "   " : " ");
            }

            builder.Append(Token(random));
        }

        // A hard break at the end of the line, in either form, now and then
        if (!last)
        {
            builder.Append(random.Next(10) switch
            {
                0 => "  ",
                1 => "\\",
                2 => "   ",
                _ => string.Empty
            });
        }

        // A line that starts a Pandoc construct makes the paragraph one that no rule may edit (the structure check refuses any
        // change of such a line), so the words that start one are only generated inside a line
        var line = builder.ToString();
        return BlockSyntaxGuard.IsPandocStructuralLine(line) ? "word " + line : line;
    }

    /// <summary>
    ///     Picks one token.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The token.</returns>
    private static string Token(ListQuoteInlineDocuments.Lcg random) => random.Next(20) switch
    {
        0 => random.Pick(Hazards),
        1 => random.Pick(Hazards),
        2 or 3 => random.Pick(Wide),
        4 or 5 or 6 or 7 => random.Pick(Atoms),
        8 => random.Pick(Spans),
        9 => new string('x', 30 + random.Next(120)),
        10 => "http://example.org/" + new string('p', 20 + random.Next(100)),
        11 => "ab" + ZeroWidthSpace + "cd",
        _ => random.Pick(Words)
    };

    /// <summary>
    ///     The zero-width space, a character that has no width and is not whitespace.
    /// </summary>
    private static readonly string ZeroWidthSpace = char.ConvertFromUtf32(0x200B);
}
