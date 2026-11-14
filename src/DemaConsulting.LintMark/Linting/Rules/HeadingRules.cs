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
using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Document;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DemaConsulting.LintMark.Linting.Rules;

/// <summary>
///     Lint-only rules about headings and about text that is used where a heading is meant: a heading that skips a level,
///     two headings with the same text, a heading that ends with punctuation, and a paragraph that is only emphasis.
/// </summary>
/// <remarks>
///     <para>
///         The rules read the headings that the parser found (setext headings included, headings inside block quotes and list
///         items included) and report in document order. A rule only reports; it never proposes an edit.
///     </para>
///     <list type="bullet">
///         <item><c>heading-increment</c> (MD001): a heading whose level is more than one deeper than the heading before it; the first heading is never reported.</item>
///         <item><c>heading-duplicate</c> (MD024): a heading whose text (blanks collapsed, case kept) is the text of an earlier heading; reported at the later one.</item>
///         <item><c>heading-trailing-punctuation</c> (MD026): a heading whose text ends with one of <c>.,;:!</c> or their full-width forms. A question mark is allowed, and a character reference such as <c>&amp;copy;</c> is not punctuation.</item>
///         <item><c>emphasis-as-heading</c> (MD036): a paragraph at the top level of the document that is one line of emphasis and nothing else, with no punctuation at its end.</item>
///     </list>
///     <para>
///         Thread safety: stateless; one instance serves every worker.
///     </para>
/// </remarks>
internal sealed class HeadingRules : ILintRule
{
    /// <summary>
    ///     The characters that a heading must not end with.
    /// </summary>
    private const string HeadingPunctuation = ".,;:!\u3002\uFF0C\uFF1B\uFF1A\uFF01";

    /// <summary>
    ///     The characters that a paragraph made of emphasis must not end with, if it is not to be read as a heading.
    /// </summary>
    private const string EmphasisPunctuation = ".,;:!?\u3002\uFF0C\uFF1B\uFF1A\uFF01\uFF1F";

    /// <inheritdoc/>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.HeadingIncrementRuleId,
        RuleCatalog.HeadingDuplicateRuleId,
        RuleCatalog.HeadingTrailingPunctuationRuleId,
        RuleCatalog.EmphasisAsHeadingRuleId
    ];

    /// <inheritdoc/>
    public ImmutableArray<Diagnostic> Check(ParsedDocument document, LintMarkConfig config)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(config);

        var source = document.Source;
        var found = ImmutableArray.CreateBuilder<Diagnostic>();
        var previousLevel = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in document.Blocks)
        {
            if (block is { Kind: BlockKind.Heading, Node: HeadingBlock heading })
            {
                CheckHeading(source, block, heading, previousLevel, seen, found);
                previousLevel = heading.Level;
            }
            else if (block is { Kind: BlockKind.Paragraph, Depth: 0, Node: ParagraphBlock paragraph })
            {
                CheckEmphasisAsHeading(source, block, paragraph, found);
            }
        }

        return found.ToImmutable();
    }

    /// <summary>
    ///     Checks one heading.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block of the heading.</param>
    /// <param name="heading">The heading node.</param>
    /// <param name="previousLevel">The level of the heading before, or zero for the first heading.</param>
    /// <param name="seen">The texts of the headings seen so far.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckHeading(
        SourceDocument source,
        BlockInfo block,
        HeadingBlock heading,
        int previousLevel,
        HashSet<string> seen,
        ImmutableArray<Diagnostic>.Builder found)
    {
        // A heading may go any number of levels up, but only one level down at a time; the first heading sets the start
        if (previousLevel > 0 && heading.Level > previousLevel + 1)
        {
            found.Add(RuleSupport.Finding(
                source,
                RuleCatalog.HeadingIncrementRuleId,
                block.Start,
                $"Heading level increases from {previousLevel} to {heading.Level}; expected at most {previousLevel + 1}"));
        }

        // An empty heading has no text to compare or to end with punctuation
        var text = RuleSupport.CollapseWhitespace(RuleSupport.PlainText(heading.Inline));
        if (text.Length == 0)
        {
            return;
        }

        if (!seen.Add(text))
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.HeadingDuplicateRuleId, block.Start, $"Another heading has the text '{text}'"));
        }

        if (HeadingPunctuation.Contains(text[^1]))
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.HeadingTrailingPunctuationRuleId, block.Start, $"Heading ends with the punctuation '{text[^1]}'"));
        }
    }

    /// <summary>
    ///     Checks one paragraph at the top level of the document.
    /// </summary>
    /// <param name="source">The source document.</param>
    /// <param name="block">The block of the paragraph.</param>
    /// <param name="paragraph">The paragraph node.</param>
    /// <param name="found">Receives the findings.</param>
    private static void CheckEmphasisAsHeading(SourceDocument source, BlockInfo block, ParagraphBlock paragraph, ImmutableArray<Diagnostic>.Builder found)
    {
        // Only a single line that is one emphasis inline, made of plain text, can stand in for a heading
        if (paragraph.Inline is not { FirstChild: EmphasisInline emphasis } inline
            || inline.FirstChild != inline.LastChild
            || !IsPlainEmphasis(emphasis)
            || source.PositionOf(block.Start).Line != source.PositionOf(block.EndInclusive).Line)
        {
            return;
        }

        var text = RuleSupport.CollapseWhitespace(RuleSupport.PlainText(inline));
        if (text.Length > 0 && !EmphasisPunctuation.Contains(text[^1]))
        {
            found.Add(RuleSupport.Finding(source, RuleCatalog.EmphasisAsHeadingRuleId, block.Start, $"Emphasis '{text}' is used instead of a heading"));
        }
    }

    /// <summary>
    ///     Determines whether an emphasis holds nothing but text and further emphasis.
    /// </summary>
    /// <param name="emphasis">The emphasis.</param>
    /// <returns><see langword="true"/> when no link, code, HTML or other inline is inside.</returns>
    private static bool IsPlainEmphasis(EmphasisInline emphasis) =>
        emphasis.All(static child => child is LiteralInline || (child is EmphasisInline nested && IsPlainEmphasis(nested)));
}
