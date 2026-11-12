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
///     Generates container-nesting fixtures: every chain of up to three containers from a fixed
///     set (quote, bullet, ordered, two-digit ordered, tab-indented bullet) around a set of leaf
///     bodies (including link reference definitions, footnote definitions and a paragraph or setext heading after
///     definitions), in explicit-prefix and lazy form, plus top-level footnote and definition documents.
/// </summary>
/// <remarks>
///     The generator is deterministic and pure; it is the source of the span-tiling fixtures that
///     complement the repository corpus with nesting combinations real files rarely contain.
/// </remarks>
internal static class SyntheticDocuments
{
    /// <summary>
    ///     The containers: name, prefix of the first line, prefix of continuation lines.
    /// </summary>
    private static readonly (string Name, string First, string Continuation)[] Containers =
    [
        ("Q", "> ", "> "),
        ("L", "- ", "  "),
        ("O", "1. ", "   "),
        ("N", "10. ", "    "),
        ("T", "- ", "\t")
    ];

    /// <summary>
    ///     The leaf bodies, one line per entry (an empty entry is a blank line).
    /// </summary>
    private static readonly (string Name, string[] Lines)[] Bodies =
    [
        ("para3", ["para line one", "para line two", "para line three"]),
        ("lazy", ["first line", "lazy line", "lazy two"]),
        ("atx+para", ["# Heading", string.Empty, "para after"]),
        ("fence", ["```", "code  here", string.Empty, "more", "```"]),
        ("hard-breaks", ["a  ", "hard", "b\\", "hard2"]),
        ("setext", ["Setext heading", "=============="]),
        ("table", ["| a | b |", "|---|:-:|", "| 1 | `x|y` |"]),
        ("two-paras", ["para", string.Empty, "second para", "continues"]),
        ("inline-multiline", ["`code", "span` text [lin", "k](http://x \"t\") **bo", "ld**"]),
        ("html", ["<div>", "x", "</div>"]),
        ("quote-then-para", ["text", "> inner quote", "> more"]),
        ("list-then-para", ["intro", "- inner a", "  cont", "- inner b"]),
        ("definition", ["[ref]: http://example.com \"T\""]),
        ("definition+text", ["[ref]: http://example.com", "text after"]),
        ("definitions+text", ["[a]: http://a.example", "[b]: http://b.example \"B\"", "text after", "more"]),
        ("definition+setext", ["[ref]: http://example.com", "Setext heading", "=============="]),
        ("footnote-def", ["[^n]: note"])
    ];

    /// <summary>
    ///     Names of the bodies that consist of one multi-line paragraph.
    /// </summary>
    private static readonly HashSet<string> ParagraphBodies = ["para3", "lazy"];

    /// <summary>
    ///     Top-level documents with footnotes and definitions: name and text.
    /// </summary>
    private static readonly (string Name, string Text)[] FootnoteTexts =
    [
        ("referenced", "Text[^n].\n\n[^n]: Note\n"),
        ("unreferenced", "Text.\n\n[^n]: Note\n"),
        ("duplicate", "T[^n].\n\n[^n]: one\n\n[^n]: two\n"),
        ("duplicate-other-case", "T[^N].\n\n[^N]: one\n\n[^n]: two\n"),
        ("duplicate-adjacent", "[^n]: one\n[^n]: two\n"),
        ("multi-paragraph", "T[^n].\n\n[^n]: first\n    second\n\n    para two\n\nAfter.\n"),
        ("lazy-continuation", "T[^n].\n\n[^n]: first\nsecond\n"),
        ("content-on-next-line", "T[^n].\n\n[^n]:\n    Note\n"),
        ("indented-marker", "T[^n].\n\n   [^n]: Note\n"),
        ("definitions-inside-and-after", "T[^n] [a].\n\n[^n]: x\n\n    [a]: http://y\n\n[a]: http://z\ntext\n"),
        ("then-quote-and-list", "T[^n].\n\n[^n]: x\n\n> quoted\n\n- item\n"),
        ("indented-lookalike", "T[^n].\n\n[^n]: one\n\n    [^n]: two\n"),
        ("code-lookalike", "```\n[^n]: code\n```\n\nText[^n].\n\n[^n]: Note\n"),
        ("definition-then-footnote", "T[^n] [a].\n\n[a]: http://x\ntext\n\n[^n]: Note\n"),
        ("then-indented-html", "T[^n].\n\n[^n]: body\n\n    <div>\n    x\n    </div>\n"),
        ("then-indented-html-and-text", "T[^n].\n\n[^n]: body\n\n    <div>\n    x\n    </div>\n\nAfter.\n"),
        ("then-list-and-indented-html", "T[^n].\n\n[^n]: body\n\n    - item\n\n      <div>\n      x\n      </div>\n")
    ];

    /// <summary>
    ///     Generates all fixtures.
    /// </summary>
    /// <returns>The fixtures, in a stable order.</returns>
    public static IEnumerable<SyntheticDocument> Generate()
    {
        foreach (var chain in Chains())
        {
            foreach (var (bodyName, bodyLines) in Bodies)
            {
                foreach (var lazy in new[] { false, true })
                {
                    var builder = new StringBuilder("Before paragraph\n\n");
                    foreach (var line in BuildLines(chain, bodyLines, lazy))
                    {
                        builder.Append(line).Append('\n');
                    }

                    builder.Append("\nAfter paragraph\n");
                    var chainName = string.Concat(chain.Select(i => Containers[i].Name));
                    var name = $"synth:{chainName}:{bodyName}{(lazy ? ":lazy" : string.Empty)}";
                    yield return new SyntheticDocument(name, chainName, builder.ToString(), lazy, ParagraphBodies.Contains(bodyName) ? bodyLines : null);
                }
            }
        }
    }

    /// <summary>
    ///     Generates the top-level footnote and definition fixtures (no container chain).
    /// </summary>
    /// <returns>The fixtures, in a stable order; <see cref="SyntheticDocument.Chain"/> is empty.</returns>
    public static IEnumerable<SyntheticDocument> GenerateFootnoteDocuments() =>
        FootnoteTexts.Select(f => new SyntheticDocument($"synth-footnote:{f.Name}", string.Empty, f.Text, false, null));

    /// <summary>
    ///     Gets the depth that a leaf body has inside a container chain.
    /// </summary>
    /// <param name="chain">The chain letters (Q quote; L, O, N and T list items).</param>
    /// <returns>One level per quote and two per list (the list and its item).</returns>
    public static int BodyDepth(string chain) => chain.Sum(static c => c == 'Q' ? 1 : 2);

    /// <summary>
    ///     Enumerates every container chain of length one to three.
    /// </summary>
    /// <returns>Indexes into <see cref="Containers"/>.</returns>
    private static IEnumerable<int[]> Chains()
    {
        for (var a = 0; a < Containers.Length; a++)
        {
            yield return [a];
            for (var b = 0; b < Containers.Length; b++)
            {
                yield return [a, b];
                for (var c = 0; c < Containers.Length; c++)
                {
                    yield return [a, b, c];
                }
            }
        }
    }

    /// <summary>
    ///     Applies the container prefixes of a chain to the body lines.
    /// </summary>
    /// <param name="chain">The container chain.</param>
    /// <param name="body">The body lines.</param>
    /// <param name="lazy">Whether non-blank continuation lines omit their prefixes.</param>
    /// <returns>The document lines.</returns>
    private static List<string> BuildLines(int[] chain, string[] body, bool lazy)
    {
        var result = new List<string>();
        for (var i = 0; i < body.Length; i++)
        {
            var builder = new StringBuilder();

            // A lazy continuation line carries no container prefix at all
            if (!(lazy && i > 0 && body[i].Length > 0))
            {
                foreach (var index in chain)
                {
                    var container = Containers[index];
                    var prefix = i == 0 ? container.First : container.Continuation;
                    if (body[i].Length == 0)
                    {
                        prefix = container.Name == "Q" ? ">" : container.Continuation;
                    }

                    builder.Append(prefix);
                }
            }

            builder.Append(body[i]);
            result.Add(builder.ToString().TrimEnd(' ').Length == 0 ? string.Empty : builder.ToString());
        }

        return result;
    }
}
