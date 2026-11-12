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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Deterministic generator of adversarial Markdown documents: footnotes that are not footnotes in quotes and lists,
///     footnote definitions with every kind of child, footnote markers followed on the same line by another
///     definition or marker, definitions followed by lines that start like a title, and a broad mixture of block
///     constructs including lines that look like grid table rows.
/// </summary>
/// <remarks>
///     <para>
///         The generator is a pure function of its seed: it uses its own simple arithmetic generator
///         (never <see cref="Random"/>), so the same documents are produced on every platform and runtime.
///         The documents use only LF and tab characters; tests re-encode them to other line endings.
///     </para>
///     <para>
///         The documents are meant to be hostile to the parser. They are checked for the nesting
///         guarantee only (see <see cref="SpanTilingAssertions.CheckNesting"/>), not for tiling, because
///         adversarial input legitimately leaves text between blocks.
///     </para>
/// </remarks>
internal static class RandomDocuments
{
    /// <summary>
    ///     Container prefixes placed before generated lines.
    /// </summary>
    private static readonly string[] Prefixes =
    [
        string.Empty, string.Empty, string.Empty, "> ", "> ", ">", "- ", "  ", "    ", "\t", ">> ", "1. ",
        "> - ", "- > ", "   ", "-   ", "> > ", ">     ", "- - "
    ];

    /// <summary>
    ///     Prefixes of the broad mixture family.
    /// </summary>
    private static readonly string[] GeneralPrefixes =
    [
        string.Empty, string.Empty, string.Empty, "> ", "- ", "  ", "    ", "\t", "> > ", "1. ", "   ", "> - ",
        "- - ", "> 1. "
    ];

    /// <summary>
    ///     Lines of the footnote-heavy mixture family.
    /// </summary>
    private static readonly string[] FootnoteLines =
    [
        "para", "text more", string.Empty, string.Empty, string.Empty, "# Head", "```", "~~~", "<div>", "</div>",
        "<!-- c -->", "| a | b |", "|---|---|", "| 1 | 2 |", "[a]: http://a", "[b]: http://b \"T\"",
        "Ref[^n] and[^m].", "[^n]: note", "[^n]: dup", "[^m]: other", "[^m]:", "[^x]: lone", "[^n]:    spaced",
        "   [^n]: indented3", "    [^n]: indented4", "  [^q]: two", "    code", "- item", "1. one", "> quote",
        "---", "===", "Setext", "$$", "    <div>", "    - li", "    > q", "    ```", "    | a | b |",
        "    |---|---|", "[^ n]: space", "[^]: empty", "[^n]:text"
    ];

    /// <summary>
    ///     Lines of the broad mixture family, including lines that look like grid table rows.
    /// </summary>
    private static readonly string[] GeneralLines =
    [
        "a", "b  ", "c\\", string.Empty, string.Empty, "# H", "## H ##", "Setext", "===", "---", "***", "```",
        "```js", "~~~", "    code", "\tcode", "<div>", "</div>", "<!-- x -->", "<!-- lintmark-disable -->",
        "| a | b |", "|---|---|", "+---+---+", "| x | y |", "+===+===+", "$$", "x = 1", "- item", "* item",
        "1. one", "2) two", "- [ ] task", "> quote", ">> deep", ">", "[a]: http://a", "[b]: <http://b> \"T\"",
        "[c]:", "[^n]: note", "[^n]", "text [a] [^n] [^m]", "[^m]: m", "    [^m]: m4", "  - nested", "    more",
        "\t- tabbed", "title: x"
    ];

    /// <summary>
    ///     Wrappers that put a footnote or look-alike inside a quote or list.
    /// </summary>
    private static readonly string[] Wrappers = [string.Empty, "> ", "- ", "1. ", "> - ", "- > ", "> > ", "  ", "\t"];

    /// <summary>
    ///     Indents of generated look-alike lines.
    /// </summary>
    private static readonly string[] Indents = [string.Empty, " ", "  ", "   ", "    ", "     ", "      ", "\t", " \t"];

    /// <summary>
    ///     Labels used by generated footnotes.
    /// </summary>
    private static readonly string[] Labels = ["n", "m", "x"];

    /// <summary>
    ///     Child blocks of a generated footnote definition (before indentation).
    /// </summary>
    private static readonly string[][] Children =
    [
        ["<div>", "text", "</div>"],
        ["```", "code", "```"],
        ["| a | b |", "|---|---|", "| 1 | 2 |"],
        ["- item", "- item2"],
        ["> quote"],
        ["second paragraph"],
        ["[a]: http://a"],
        ["[^n]: nested"],
        ["<div>", "open"],
        ["```", "code"],
        ["    code", string.Empty, "    more"]
    ];

    /// <summary>
    ///     Gaps between a generated footnote marker and what follows it on the same line.
    /// </summary>
    private static readonly string[] MarkerGaps = [" ", " ", " ", "  ", "\t", string.Empty];

    /// <summary>
    ///     Indents of generated footnote markers.
    /// </summary>
    private static readonly string[] MarkerIndents = [string.Empty, string.Empty, string.Empty, " ", "   "];

    /// <summary>
    ///     What follows a footnote marker on its own line: a definition or another marker, possibly continued on the next lines.
    /// </summary>
    private static readonly string[] SameLineBodies =
    [
        "[a]: http://x", "[a]: http://x \"t\"", "[l]:", "[l]:\n    /u", "[l]:\n    /u\n  > [!NOTE]", "[a]: /u\n    more",
        "[a]: /u\n[b]: /v", "[a]: /u\n\n    para", "[a]: /u\n\n[^m]: [b]: /v", "[^n]: x", "[^m]: x\n    cont", "[^n]:",
        "[^n]: [^m]: x", "[^m]: [a]: /u", "[^m]: [^n]: [^x]: deep", "> [^n]: x", "- [^n]: x", "[a]: /u\n[^m]: [b]: /v",
        "[a]: /u\n\n    ```\n    code\n    ```"
    ];

    /// <summary>
    ///     Lines of the mixture family rich in footnote markers followed by definitions.
    /// </summary>
    private static readonly string[] SameLineMixtureLines =
    [
        "para", string.Empty, string.Empty, string.Empty, "Ref[^n] and[^m].", "[^n]: [a]: http://x", "[^m]: [^n]: x", "[^n]: [l]:",
        "    /u", "  > [!NOTE]", "[^n]: [a]: /u \"t\"", "[^m]: [^n]: [^x]: deep", "[a]: http://a", "[^n]: note", "[^n]: dup",
        "\t[^n]: [a]: /u", "   [^m]: [^n]: x", "> quote", "- item", "| a | b |", "|---|---|", "+---+---+", "+---+", "# H", "```",
        "<div>", "[^n]:  [a]: /u", "[^x]: [^n]:"
    ];

    /// <summary>
    ///     Lines of the family of definitions followed by lines that start like a title.
    /// </summary>
    private static readonly string[] DefinitionTitleLines =
    [
        "[a]: /u", "[b]: <http://b>", "[c]:", "/u", "\"t\" x", "(t) y", "'t' z", "\"t\"", "(t)", "\"\"", "\"t\"  ", "\"t\" \"u\"",
        "\"t", "x\"", "para", "text", string.Empty, string.Empty, "[^n]: [^n]:", "[^n]: [a]: /u", "[a]: /u \"t\"", "[d]: /v\n(w) q",
        "# H", "```", "- item", "> quote"
    ];

    /// <summary>
    ///     Valid grid tables.
    /// </summary>
    private static readonly string[] GridTables =
    [
        "+---+---+\n| a | b |\n+---+---+",
        "+---+---+\n| a | b |\n+===+===+\n| 1 | 2 |\n+---+---+",
        "+---+\n| a |\n+---+",
        "+---+---+\n| a | b |\n+---+---+\n| c | d |\n+---+---+"
    ];

    /// <summary>
    ///     The number of families that <see cref="Generate"/> produces.
    /// </summary>
    public const int FamilyCount = 7;

    /// <summary>
    ///     Generates the documents of all families.
    /// </summary>
    /// <param name="seed">The seed of the generator.</param>
    /// <param name="countPerFamily">The number of documents per family.</param>
    /// <returns>
    ///     <paramref name="countPerFamily"/> documents of each family in turn: footnote-heavy mixtures,
    ///     footnotes with children, text that looks like a footnote, a broad mixture, footnote markers followed
    ///     on the same line by a definition or marker, a mixture rich in such lines, and definitions followed by
    ///     lines that start like a title (<see cref="FamilyCount"/> times <paramref name="countPerFamily"/> in all).
    /// </returns>
    public static IEnumerable<SyntheticDocument> Generate(uint seed, int countPerFamily)
    {
        var random = new Lcg(seed);
        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("mix", i, Mixture(random, FootnoteLines, Prefixes, 2, 15));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("footnote-children", i, FootnoteChildren(random));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("lookalikes", i, Lookalikes(random));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("general", i, Mixture(random, GeneralLines, GeneralPrefixes, 3, 20));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("same-line", i, SameLineDefinitions(random));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("same-line-mix", i, Mixture(random, SameLineMixtureLines, Prefixes, 3, 14));
        }

        for (var i = 0; i < countPerFamily; i++)
        {
            yield return Fixture("definition-titles", i, Mixture(random, DefinitionTitleLines, GeneralPrefixes, 3, 14));
        }
    }

    /// <summary>
    ///     Generates documents of valid grid tables next to footnote markers that are followed by definitions.
    /// </summary>
    /// <param name="seed">The seed of the generator.</param>
    /// <param name="count">The number of documents.</param>
    /// <returns>
    ///     The documents with the number of grid tables each holds. Every table is valid, stands at top level
    ///     and is separated by blank lines, so a parser that reads the document without grid tables loses it.
    /// </returns>
    public static IEnumerable<(SyntheticDocument Fixture, int GridTables)> GenerateWithValidGridTables(uint seed, int count)
    {
        var random = new Lcg(seed);
        for (var i = 0; i < count; i++)
        {
            var builder = new StringBuilder();
            var tables = 0;
            var segments = 2 + random.Next(5);
            for (var s = 0; s < segments; s++)
            {
                switch (random.Next(4))
                {
                    case 0:
                        builder.Append(random.Pick(GridTables)).Append("\n\n");
                        tables++;
                        break;

                    case 1:
                        builder.Append("Ref[^n] and[^m].\n\n");
                        break;

                    default:
                        AppendSameLineShape(random, builder);
                        builder.Append('\n');
                        break;
                }
            }

            yield return (Fixture("valid-grid", i, builder.ToString()), tables);
        }
    }

    /// <summary>
    ///     Wraps generated text in a fixture record.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <param name="index">The index of the document in its family.</param>
    /// <param name="text">The LF document text.</param>
    /// <returns>The fixture.</returns>
    private static SyntheticDocument Fixture(string family, int index, string text) =>
        new($"random:{family}:{index}", string.Empty, text, false, null);

    /// <summary>
    ///     Builds a document from random lines, each behind a random prefix.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="lines">The pool of lines.</param>
    /// <param name="prefixes">The pool of prefixes.</param>
    /// <param name="minLines">Smallest number of lines.</param>
    /// <param name="maxLines">Largest number of lines.</param>
    /// <returns>The document text.</returns>
    private static string Mixture(Lcg random, string[] lines, string[] prefixes, int minLines, int maxLines)
    {
        // One document in three keeps the same prefix on all lines (a quote or list that really nests)
        var sticky = random.Next(3) == 0 ? random.Pick(prefixes) : null;
        var count = minLines + random.Next(maxLines - minLines + 1);
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var line = random.Pick(lines);
            builder.Append(line.Length == 0 ? string.Empty : (sticky ?? random.Pick(prefixes)) + line).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds a document of footnote definitions that have children of every kind.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string FootnoteChildren(Lcg random)
    {
        var builder = new StringBuilder();
        if (random.Next(2) == 0)
        {
            builder.Append("Ref[^n] and[^m].\n\n");
        }

        var footnotes = 1 + random.Next(3);
        for (var f = 0; f < footnotes; f++)
        {
            var wrapper = random.Pick(Wrappers);
            var continuation = ContinuationPrefix(wrapper);
            var markerIndent = random.Next(2) == 0 ? string.Empty : "   ";
            builder.Append(wrapper).Append(markerIndent).Append("[^").Append(random.Pick(Labels)).Append("]: text\n");

            var children = 1 + random.Next(3);
            for (var c = 0; c < children; c++)
            {
                if (random.Next(2) == 0)
                {
                    builder.Append(continuation.TrimEnd()).Append('\n');
                }

                foreach (var line in random.Pick(Children))
                {
                    builder.Append(line.Length == 0 ? continuation.TrimEnd() : continuation + "    " + line).Append('\n');
                }
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds a document of footnotes that are not footnotes at assorted indents inside a quote or list.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string Lookalikes(Lcg random)
    {
        var wrapper = random.Pick(Wrappers);
        var continuation = ContinuationPrefix(wrapper);
        var builder = new StringBuilder();
        var count = 1 + random.Next(6);
        for (var i = 0; i < count; i++)
        {
            var prefix = i == 0 ? wrapper : continuation;
            var line = random.Next(5) switch
            {
                0 or 1 => random.Pick(Indents) + "[^" + random.Pick(Labels) + "]: look-alike",
                2 => "continued text",
                3 => string.Empty,
                _ => random.Pick(Indents) + "[^" + random.Pick(Labels) + "]:"
            };
            builder.Append(line.Length == 0 ? prefix.TrimEnd() : prefix + line).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Builds a document of footnote markers that are followed on the same line by a definition or another marker.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <returns>The document text.</returns>
    private static string SameLineDefinitions(Lcg random)
    {
        var builder = new StringBuilder();
        if (random.Next(3) == 0)
        {
            builder.Append("Ref[^n] and[^m].\n\n");
        }

        var count = 1 + random.Next(4);
        for (var i = 0; i < count; i++)
        {
            AppendSameLineShape(random, builder);
            if (random.Next(3) == 0)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Appends a footnote marker with a definition or marker behind it on the same line, inside a random wrapper.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="builder">The builder that receives the lines.</param>
    private static void AppendSameLineShape(Lcg random, StringBuilder builder)
    {
        var wrapper = random.Pick(Wrappers);
        var continuation = ContinuationPrefix(wrapper);
        var shape = "[^" + random.Pick(Labels) + "]:" + random.Pick(MarkerGaps) + random.Pick(SameLineBodies);
        var lines = shape.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var prefix = i == 0 ? wrapper + random.Pick(MarkerIndents) : continuation;
            builder.Append(lines[i].Length == 0 ? prefix.TrimEnd() : prefix + lines[i]).Append('\n');
        }
    }

    /// <summary>
    ///     Gets the prefix that continues a wrapper on the following lines.
    /// </summary>
    /// <param name="wrapper">The wrapper of the first line.</param>
    /// <returns>The continuation prefix.</returns>
    private static string ContinuationPrefix(string wrapper) => wrapper switch
    {
        "- " => "  ",
        "1. " => "   ",
        "> - " => ">   ",
        "- > " => "  > ",
        _ => wrapper
    };

    /// <summary>
    ///     A simple arithmetic generator that gives the same sequence on every platform.
    /// </summary>
    private sealed class Lcg
    {
        /// <summary>
        ///     The current state.
        /// </summary>
        private uint _state;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Lcg"/> class.
        /// </summary>
        /// <param name="seed">The seed.</param>
        public Lcg(uint seed) => _state = seed;

        /// <summary>
        ///     Gets the next number.
        /// </summary>
        /// <param name="bound">The exclusive upper bound. Must be positive.</param>
        /// <returns>A number from zero up to but excluding <paramref name="bound"/>.</returns>
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
