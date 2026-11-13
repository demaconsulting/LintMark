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

using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;

namespace DemaConsulting.LintMark.Formatting;

/// <summary>
///     The document-level pass that puts a blank line between top-level blocks that must be separated:
///     headings, fenced code blocks, lists and tables.
/// </summary>
/// <remarks>
///     <para>
///         Two top-level blocks that follow each other without a blank line are a violation when one of them is a
///         heading, a fenced (or convertible indented) code block, a list or a pipe table. The decision is made
///         from the parsed blocks, never from the look of the lines: <c>text</c> followed by <c>2. item</c> is
///         one paragraph, a line after a list item is a lazy continuation, and a row after a table is a table row,
///         so none of them is a pair of blocks and none is touched.
///     </para>
///     <para>
///         Only top-level blocks are separated. Inserting a blank line next to a block inside a list item or a
///         block quote can change what the document means (a tight list becomes loose), so a violation inside a
///         container is neither fixed nor reported. One blank line is inserted per boundary, attributed to the
///         first applicable rule that is not suppressed (heading, fence, list, table), because two rules that
///         insert at the same offset would conflict. Only block kinds whose end the parser reports reliably are
///         separated (not an unclosed fence, a grid table, a footnote definition or a block that the tool does not model), and
///         no blank line is inserted next to a line that looks like a grid table border or before a block that
///         is indented by more than three columns (such a block only exists because of what precedes it).
///     </para>
///     <para>
///         Thread safety: static and stateless; safe for concurrent use.
///     </para>
/// </remarks>
internal static class BlockSpacingPass
{
    /// <summary>
    ///     The spacing rules in the order in which they claim a boundary.
    /// </summary>
    private static readonly (string RuleId, Func<BlockInfo, bool> Applies)[] Rules =
    [
        (RuleCatalog.HeadingBlankLinesRuleId, static block => block.Kind == BlockKind.Heading),
        (RuleCatalog.FenceBlankLinesRuleId, static block => block.Kind is BlockKind.FencedCode or BlockKind.IndentedCode),
        (RuleCatalog.ListBlankLinesRuleId, static block => block.Kind == BlockKind.List),
        (RuleCatalog.TableBlankLinesRuleId, static block => block.Kind == BlockKind.Table)
    ];

    /// <summary>
    ///     Adds an insertion of a blank line for every boundary between top-level blocks that needs one.
    /// </summary>
    /// <param name="document">The parsed document (for suppressions). Must not be null.</param>
    /// <param name="layout">The line arithmetic of the document. Must not be null.</param>
    /// <param name="ownership">Which lines belong to which block (supplies the top-level blocks). Must not be null.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Apply(ParsedDocument document, LineLayout layout, LineOwnership ownership, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(edits);

        var blocks = ownership.TopLevel;
        for (var i = 0; i + 1 < blocks.Length; i++)
        {
            var before = blocks[i];
            var after = blocks[i + 1];
            if (!IsSeparable(before) || !IsSeparable(after))
            {
                continue;
            }

            // Only blocks that follow each other directly are a violation
            var beforeLast = LineOwnership.LinesOf(layout, before).Last;
            var afterFirst = LineOwnership.LinesOf(layout, after).First;
            if (afterFirst != beforeLast + 1)
            {
                continue;
            }

            // A blank line next to a line that looks like a grid table border could change how Pandoc reads the table
            // (an indented code block is judged after its conversion, when the line is inside a fence)
            if ((before.Kind != BlockKind.IndentedCode && layout.LooksLikeGridBorder(beforeLast))
                || (after.Kind != BlockKind.IndentedCode && layout.LooksLikeGridBorder(afterFirst)))
            {
                continue;
            }

            // A block indented by more than three columns is only a block of its own because of what precedes it
            if (after.Kind != BlockKind.IndentedCode)
            {
                var indentation = layout.Text.AsSpan(layout.LineStart(afterFirst), after.Start - layout.LineStart(afterFirst));
                if (indentation.Length > 3 || indentation.Contains('\t'))
                {
                    continue;
                }
            }

            // A blank line after a block that keeps its trailing blank lines would become part of that block
            if (ownership.LeafAt(beforeLast) is { } lastLeaf && LineOwnership.KeepsTrailingBlankLines(lastLeaf, contentFollows: true))
            {
                continue;
            }

            // The first applicable rule that is not suppressed on either block inserts the blank line
            foreach (var (ruleId, applies) in Rules)
            {
                if (!applies(before) && !applies(after))
                {
                    continue;
                }

                if (document.Suppressions.IsSuppressedRange(before.Start, after.EndInclusive + 1, ruleId))
                {
                    continue;
                }

                edits.Add(new TextEdit(layout.LineStart(afterFirst), 0, "\n", ruleId));
                break;
            }
        }
    }

    /// <summary>
    ///     Determines whether a blank line may be inserted next to a block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>
    ///     <see langword="true"/> for the block kinds whose end the parser reports reliably; <see langword="false"/> for an
    ///     unclosed fence, a grid table, footnote definitions (their label and body are read differently once a
    ///     blank line separates them from what precedes) and blocks the tool does not model.
    /// </returns>
    private static bool IsSeparable(BlockInfo block) => block.Kind is
        BlockKind.Heading or BlockKind.Paragraph or BlockKind.FencedCode or BlockKind.IndentedCode or BlockKind.HtmlBlock
        or BlockKind.ThematicBreak or BlockKind.BlockQuote or BlockKind.List or BlockKind.Table
        or BlockKind.LinkReferenceDefinition or BlockKind.FrontMatter or BlockKind.MathBlock;
}
