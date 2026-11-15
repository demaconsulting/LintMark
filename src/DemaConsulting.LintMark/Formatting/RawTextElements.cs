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

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     Finds the leaf blocks whose whitespace belongs to a raw HTML element that the Markdown text opens and does not close
///     in the same block.
/// </summary>
/// <remarks>
///     <para>
///         The content of a <c>pre</c>, <c>script</c>, <c>style</c> or <c>textarea</c> element keeps its line breaks and spaces
///         when the page is displayed. When an HTML start tag of one of them sits in an HTML block that ends at a blank line,
///         or in the inline HTML of a paragraph, the Markdown that follows is still read as paragraphs and lists, but the
///         page shows it inside the element. Joining or trimming its lines would change what the page shows, so every leaf
///         block that starts while such an element is open, and every block that holds a start tag, is left as written.
///     </para>
///     <para>
///         Only HTML blocks that no end condition closes by itself and inline HTML count; a tag in code, in a comment or in
///         a processing instruction is text. A closing tag closes the element opened last with the same name, a closing
///         tag with nothing open is ignored. Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class RawTextElements
{
    /// <summary>
    ///     The time limit of a pattern match in milliseconds.
    /// </summary>
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     The names of the elements whose content keeps its whitespace.
    /// </summary>
    private static readonly string[] Names = ["pre", "script", "style", "textarea"];

    /// <summary>
    ///     Matches an HTML start or end tag of one of the elements (group 1 is the slash of an end tag, group 2 the name).
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"<(/?)(pre|script|style|textarea)(?=[\s/>]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex TagRegex();

    /// <summary>
    ///     Finds the leaf blocks that must be left as written because of an open raw HTML element.
    /// </summary>
    /// <param name="document">The parsed document. Must not be null.</param>
    /// <returns>The blocks (compared by reference); empty when no start tag of such an element is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    public static HashSet<BlockInfo> FindProtectedBlocks(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var found = new HashSet<BlockInfo>(ReferenceEqualityComparer.Instance);
        var open = new int[Names.Length];
        foreach (var block in document.Blocks)
        {
            if (LineOwnership.IsContainer(block.Kind))
            {
                continue;
            }

            // The tags of the block in source order: an element that is open at the start protects the block
            var openAtStart = open.Any(static count => count > 0);
            var opens = false;
            foreach (var (name, isStart) in TagsOf(document, block))
            {
                if (isStart)
                {
                    open[name]++;
                    opens = true;
                }
                else if (open[name] > 0)
                {
                    open[name]--;
                }
            }

            if (openAtStart || opens)
            {
                found.Add(block);
            }
        }

        return found;
    }

    /// <summary>
    ///     Lists the start and end tags of the elements that a leaf block holds.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="block">The leaf block.</param>
    /// <returns>The index into <see cref="Names"/> and whether the tag starts the element, in source order.</returns>
    private static List<(int Name, bool IsStart)> TagsOf(ParsedDocument document, BlockInfo block)
    {
        var tags = new List<(int, bool)>();
        switch (block.Node)
        {
            case HtmlBlock { Type: HtmlBlockType.Comment or HtmlBlockType.CData or HtmlBlockType.ProcessingInstruction or HtmlBlockType.DocumentType }:
                break;

            case HtmlBlock:
                var text = document.Source.Text;
                var end = Math.Min(block.EndInclusive + 1, text.Length);
                if (block.Start >= 0 && block.Start < end)
                {
                    foreach (var match in TagRegex().EnumerateMatches(text.AsSpan(block.Start, end - block.Start)))
                    {
                        AddTag(tags, text.AsSpan(block.Start + match.Index, match.Length));
                    }
                }

                break;

            case LeafBlock { Inline: { } inline }:
                foreach (var html in inline.Descendants<HtmlInline>())
                {
                    if (TagRegex().Match(html.Tag) is { Success: true, Index: 0 } match)
                    {
                        AddTag(tags, match.Value);
                    }
                }

                break;
        }

        return tags;
    }

    /// <summary>
    ///     Adds one matched tag to a list.
    /// </summary>
    /// <param name="tags">Receives the tag.</param>
    /// <param name="tag">The matched text: <c>&lt;</c>, an optional slash and the name.</param>
    private static void AddTag(List<(int, bool)> tags, ReadOnlySpan<char> tag)
    {
        var isEnd = tag.Length > 1 && tag[1] == '/';
        var name = tag[(isEnd ? 2 : 1)..];
        for (var i = 0; i < Names.Length; i++)
        {
            if (name.Equals(Names[i], StringComparison.OrdinalIgnoreCase))
            {
                tags.Add((i, !isEnd));
                return;
            }
        }
    }
}
