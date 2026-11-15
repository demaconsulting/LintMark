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

namespace DemaConsulting.LintMark.Verify.Fuzz;

/// <summary>
///     Deterministic generator of adversarial Markdown documents for the <c>--fuzz</c> option: nests of lists with mixed
///     markers, numbers, spacing and indentation, lines whose meaning depends on their indentation, blank lines at the end,
///     and a few documents of block quotes and inline syntax.
/// </summary>
/// <remarks>
///     The generator is a pure function of its seed (it uses its own arithmetic generator, never <see cref="Random"/>), so the
///     same documents are produced on every platform and runtime. The documents use LF line endings and tabs only in the
///     indentation and after markers.
/// </remarks>
internal static class FuzzDocuments
{
    /// <summary>
    ///     Markers that start a list item.
    /// </summary>
    private static readonly string[] Markers =
    [
        "-", "-", "-", "*", "*", "+", "1.", "1.", "2.", "3.", "9.", "10.", "11.", "1)", "2)", "0.", "01.", "100.", "999999999."
    ];

    /// <summary>
    ///     The space after a marker.
    /// </summary>
    private static readonly string[] MarkerGaps = [" ", " ", " ", " ", "  ", "   ", "    ", "     ", "\t"];

    /// <summary>
    ///     The indentation of a line.
    /// </summary>
    private static readonly string[] Indents =
    [
        string.Empty, string.Empty, string.Empty, " ", "  ", "  ", "   ", "   ", "    ", "    ", "     ", "      ", "        ", "\t", " \t"
    ];

    /// <summary>
    ///     The text of an item or a continuation line.
    /// </summary>
    private static readonly string[] Texts =
    [
        "item", "item text", "second words", "[ ] task", "[x] done", "# Heading", "> quote", "```", "~~~", "<div>", "</div>",
        "<!-- c -->", "---", "***", "===", "| a | b |", "|---|---|", "$$", "x = 1", "    code", "[a]: https://a",
        "[^n]: note", "text _em_ and **st**", "see https://x.com.", "\\", "- nested", "1. nested"
    ];

    /// <summary>
    ///     The text of a line after a quote marker.
    /// </summary>
    private static readonly string[] QuoteTexts =
    [
        "text", "more text", "# Heading", "## H ##", "- item", "1. one", "```", "code", "~~~", "<div>", "<!-- c -->", "| a | b |",
        "|---|---|", "---", "===", "Setext", "[!NOTE]", "[!WARNING]", "    code", "      code", "> nested", "text _em_", "\\", string.Empty,
        "[^n]: note", "- [ ] task"
    ];

    /// <summary>
    ///     The words and punctuation of an inline paragraph.
    /// </summary>
    private static readonly string[] InlineTokens =
    [
        "_a_", "__b__", "*c*", "**d**", "***e***", "_a *b* c_", "a_b", "a_b_", "_a_b", "*a*b", "a*b*c", "https://x.com", "https://a.b/c_d",
        "https://x.com/a(b)c", "`code _x_`", "`", "[l](u)", "[_a_](u)", "<https://x>", "www.x.com", "a@b.com", "_", "*", "**", "__", "\\_",
        "<", ">", "$x_y$", "$$", "~~s~~", "[ref]", "[^1]", "text", "word", "and", "(", ")", ".", ",", "!", "&amp;", "&", "#", "|"
    ];

    /// <summary>
    ///     Generates the documents of a run.
    /// </summary>
    /// <param name="count">The number of documents.</param>
    /// <param name="seed">The seed; the same seed gives the same documents.</param>
    /// <returns>The document texts, in order.</returns>
    public static IEnumerable<string> Generate(int count, uint seed)
    {
        var random = new Lcg(seed);
        for (var i = 0; i < count; i++)
        {
            yield return random.Next(10) switch
            {
                0 => QuoteDocument(random),
                1 => InlineDocument(random),
                _ => ListDocument(random)
            };
        }
    }

    /// <summary>
    ///     Determines whether a file name is the name of a generated document.
    /// </summary>
    /// <param name="fileName">The file name without its folder. Must not be null.</param>
    /// <returns><see langword="true"/> for <c>fuzz-</c>, seven digits and <c>.md</c>.</returns>
    public static bool IsGeneratedName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.Length == "fuzz-0000000.md".Length
            && fileName.StartsWith("fuzz-", StringComparison.Ordinal)
            && fileName.EndsWith(".md", StringComparison.Ordinal)
            && fileName.AsSpan(5, 7).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>
    ///     Writes the documents of a run into a directory, one file each.
    /// </summary>
    /// <param name="directory">The directory, which must exist. Must not be null.</param>
    /// <param name="count">The number of documents.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The number of files written.</returns>
    public static int Write(string directory, int count, uint seed)
    {
        ArgumentNullException.ThrowIfNull(directory);

        // The files of an earlier run (possibly of a larger count) are replaced
        foreach (var stale in Directory.EnumerateFiles(directory, "fuzz-*.md").Where(static path => IsGeneratedName(Path.GetFileName(path))))
        {
            File.Delete(stale);
        }

        var utf8 = new UTF8Encoding(false);
        var number = 0;
        foreach (var text in Generate(count, seed))
        {
            number++;
            File.WriteAllText(Path.Combine(directory, $"fuzz-{number:D7}.md"), text, utf8);
        }

        return number;
    }

    /// <summary>
    ///     Builds one inline paragraph, optionally inside a quote or a list item.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string InlineDocument(Lcg random)
    {
        var prefix = random.Next(6) switch
        {
            0 => "> ",
            1 => "- ",
            2 => "1. ",
            _ => string.Empty
        };
        var continuation = prefix == "> " ? "> " : new string(' ', prefix.Length);
        var builder = new StringBuilder();
        var lines = 1 + random.Next(3);
        for (var line = 0; line < lines; line++)
        {
            builder.Append(line == 0 ? prefix : continuation);
            var tokens = 1 + random.Next(9);
            for (var t = 0; t < tokens; t++)
            {
                builder.Append(random.Pick(InlineTokens));
                if (random.Next(4) != 0)
                {
                    builder.Append(' ');
                }
            }

            builder.Append('\n');
        }

        if (random.Next(5) == 0)
        {
            builder.Append("\n[ref]: https://r.com\n");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds one document with up to three nests of lists, sometimes with blank lines at the end.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string ListDocument(Lcg random)
    {
        var builder = new StringBuilder();
        if (random.Next(4) == 0)
        {
            builder.Append("intro\n\n");
        }

        var nests = 1 + random.Next(3);
        for (var nest = 0; nest < nests; nest++)
        {
            var wrapper = random.Next(8) switch
            {
                0 => "> ",
                1 => "  ",
                _ => string.Empty
            };
            var lines = 1 + random.Next(9);
            var step = random.Pick(new[] { 2, 3, 4 });
            var depth = 0;
            for (var line = 0; line < lines; line++)
            {
                builder.Append(wrapper).Append(ListLine(random, step, ref depth)).Append('\n');
            }

            builder.Append(random.Next(3) switch
            {
                0 => string.Empty,
                1 => "\n",
                _ => "\ntext after\n\n"
            });
        }

        if (random.Next(3) == 0)
        {
            builder.Append('\n', 1 + random.Next(4));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds one line of a list nest: an item or a continuation of the last one.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="step">The indentation of one nesting level.</param>
    /// <param name="depth">The depth of the last item; updated when the line is an item.</param>
    /// <returns>The line without its container prefix.</returns>
    private static string ListLine(Lcg random, int step, ref int depth)
    {
        switch (random.Next(10))
        {
            // An item at the depth of the last, one deeper or one shallower, written as usual or with noise
            case 0 or 1 or 2 or 3 or 4:
                depth = Math.Max(0, depth + random.Next(3) - 1);
                var indent = random.Next(4) == 0 ? random.Pick(Indents) : new string(' ', depth * step);
                var gap = random.Pick(MarkerGaps);
                var text = random.Pick(Texts);

                // A paragraph that starts with a marker, a tab and a break shape confuses the parser's reading of later breaks, and a
                // tab in front of a code span that runs over several lines is rendered inside the span (both are defects of the
                // parser that no rule of the tool can avoid), so such a line is written with a space
                if (gap == "\t" && (text is "---" or "***" or "===" || text.StartsWith('`') || text.StartsWith('~')))
                {
                    gap = " ";
                }

                return indent + random.Pick(Markers) + gap + text;

            // A continuation line, indented to the item or not
            case 5 or 6:
                return (random.Next(3) == 0 ? random.Pick(Indents) : new string(' ', (depth + 1) * step)) + random.Pick(Texts);

            // A blank line, possibly with spaces
            case 7:
                return random.Next(3) == 0 ? "   " : string.Empty;

            // Another bullet with a different marker character at the margin
            case 8:
                depth = 0;
                return random.Pick(Markers) + " " + random.Pick(Texts);
            default:
                return random.Pick(Indents) + random.Pick(Texts);
        }
    }

    /// <summary>
    ///     Builds one document of quote lines.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string QuoteDocument(Lcg random)
    {
        var gaps = new[] { " ", "  ", "   ", "    ", "     ", string.Empty, "\t" };
        var builder = new StringBuilder();
        var lines = 1 + random.Next(10);
        var sticky = random.Next(3) == 0;
        var gap = random.Pick(gaps);
        for (var line = 0; line < lines; line++)
        {
            var depth = random.Next(4) == 0 ? 2 : 1;
            var marker = new StringBuilder();
            for (var level = 0; level < depth; level++)
            {
                marker.Append('>').Append(sticky ? gap : random.Pick(gaps));
            }

            var prefix = random.Next(10) switch
            {
                0 => "- ",
                1 => "  ",
                _ => string.Empty
            };
            builder.Append(prefix).Append(marker).Append(random.Pick(QuoteTexts)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     A small deterministic arithmetic generator.
    /// </summary>
    /// <param name="seed">The seed.</param>
    private sealed class Lcg(uint seed)
    {
        /// <summary>
        ///     The state of the generator.
        /// </summary>
        private uint _state = seed;

        /// <summary>
        ///     Gets the next number.
        /// </summary>
        /// <param name="bound">The exclusive upper bound. Must be positive.</param>
        /// <returns>A number from zero up to <paramref name="bound"/>.</returns>
        public int Next(int bound)
        {
            _state = unchecked((_state * 1664525u) + 1013904223u);
            return (int)((_state >> 8) % (uint)bound);
        }

        /// <summary>
        ///     Picks an element of a pool.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="pool">The pool. Must not be empty.</param>
        /// <returns>A random element.</returns>
        public T Pick<T>(T[] pool) => pool[Next(pool.Length)];
    }
}
