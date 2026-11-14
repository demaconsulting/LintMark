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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Linting;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Formatting.Blocks;

/// <summary>
///     Writes lists in the canonical form: one marker character per bullet list, numbers that count the configured
///     way, items that start in the same column, nested bullet lists indented as configured, and one space after
///     each marker.
/// </summary>
/// <remarks>
///     <para>
///         Four rules are enforced: <c>bullet</c> (the marker character of a bullet list), <c>ordered-style</c> (the numbers
///         of an ordered list), <c>list-indent</c> (the column of the items of a list and the indentation of a nested bullet
///         list) and <c>list-marker-space</c> (two to four spaces after a marker become one). The formatter accepts the
///         outermost list of a nest and handles every list inside it itself, so a nested list is never visited twice, and it
///         claims no lines: the other rules keep editing the text of the lines it changes, and its edits replace only
///         indentation, markers and the spaces after markers.
///     </para>
///     <para>
///         What changes, and what is left as written, is decided by <see cref="ListModel"/> and <see cref="ListPlan"/>: marker
///         characters and same-width numbers change at any depth; columns change only for a nest at the top level of the document
///         whose items hold no lazy line, tab, five-space code start or block the parser does not report reliably. The first
///         number of an ordered list and its delimiter are never changed, because they decide how the list renders. A list that
///         cannot be changed without changing its meaning is left exactly as written and reports no violation.
///     </para>
///     <para>
///         Thread safety: stateless; one instance serves every worker thread.
///     </para>
/// </remarks>
internal sealed class ListFormatter : IBlockFormatter
{
    /// <summary>
    ///     Gets the identifiers of the rules this formatter enforces.
    /// </summary>
    public ImmutableArray<string> RuleIds { get; } =
    [
        RuleCatalog.BulletRuleId,
        RuleCatalog.OrderedStyleRuleId,
        RuleCatalog.ListIndentRuleId,
        RuleCatalog.ListMarkerSpaceRuleId
    ];

    /// <summary>
    ///     Determines whether a block is the outermost list of a nest of lists.
    /// </summary>
    /// <param name="block">The block to test. Must not be null.</param>
    /// <returns><see langword="true"/> for a list that is not inside a list item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    public bool CanFormat(BlockInfo block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Kind == BlockKind.List && block.Node is ListBlock && block.Parent is not ListItemBlock;
    }

    /// <summary>
    ///     Proposes the edits that bring a nest of lists into canonical form.
    /// </summary>
    /// <param name="context">The document and settings. Must not be null.</param>
    /// <param name="block">The outermost list. Must be accepted by <see cref="CanFormat"/>.</param>
    /// <param name="edits">Receives the edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="block"/> is not accepted by <see cref="CanFormat"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an edit conflicts with another (a defect, not a property of the document).</exception>
    public void Format(FormatContext context, BlockInfo block, EditSet edits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(edits);
        if (!CanFormat(block))
        {
            throw new ArgumentException($"The block must be an outermost list, not {block.Kind}.", nameof(block));
        }

        // A nest whose shape is not what the parser promised is left exactly as written
        if (ListModel.Build(context, block) is { } model)
        {
            new ListPlan(context, edits).Apply(model);
        }
    }
}
