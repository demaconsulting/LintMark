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
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Markdig.Syntax;

namespace DemaConsulting.LintMark.Document;

/// <summary>
///     Index of the link reference definitions in a document.
/// </summary>
/// <remarks>
///     <para>
///         Definitions are taken from the definition blocks' own spans, never from the parser's
///         group shells or tree order, so positions are exact and in source order. Definitions inside
///         block quotes and list items are included. Footnote definitions are not link reference
///         definitions and are excluded, wherever they stand.
///     </para>
///     <para>
///         Matching follows CommonMark: labels are compared after collapsing internal whitespace and
///         case folding, and when a label is defined more than once the first definition wins while
///         the duplicates remain visible in <see cref="Definitions"/>. Instances are immutable after
///         <see cref="Build"/>; safe for concurrent reads.
///     </para>
/// </remarks>
internal sealed class ReferenceIndex
{
    /// <summary>
    ///     Lookup from normalized label to the first definition with that label.
    /// </summary>
    private readonly Dictionary<string, ReferenceDefinition> _byLabel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceIndex"/> class.
    /// </summary>
    /// <param name="definitions">All definitions in source order.</param>
    /// <param name="byLabel">First-definition-wins lookup.</param>
    private ReferenceIndex(IEnumerable<ReferenceDefinition> definitions, Dictionary<string, ReferenceDefinition> byLabel)
    {
        Definitions = definitions.ToImmutableArray();
        _byLabel = byLabel;
    }

    /// <summary>
    ///     Gets every definition (including duplicates) in source order.
    /// </summary>
    /// <remarks>
    ///     The list is backed by an immutable array, so a caller cannot change the index through it,
    ///     not even by casting.
    /// </remarks>
    public IReadOnlyList<ReferenceDefinition> Definitions { get; }

    /// <summary>
    ///     Builds the index from the flattened blocks of a document.
    /// </summary>
    /// <param name="source">The source document, used to compute line numbers. Must not be null.</param>
    /// <param name="blocks">The flattened, source-ordered blocks. Must not be null.</param>
    /// <returns>The index (empty when the document has no definitions).</returns>
    /// <remarks>Pure with respect to its inputs; thread-safe.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static ReferenceIndex Build(SourceDocument source, IReadOnlyList<BlockInfo> blocks)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(blocks);

        var definitions = new List<ReferenceDefinition>();
        var byLabel = new Dictionary<string, ReferenceDefinition>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            if (block is not { Kind: BlockKind.LinkReferenceDefinition, Node: LinkReferenceDefinition node })
            {
                continue;
            }

            // Record every definition; the lookup keeps only the first one per label (CommonMark)
            var label = node.Label ?? string.Empty;
            var definition = new ReferenceDefinition(
                label,
                NormalizeLabel(label),
                node.Url ?? string.Empty,
                node.Title,
                block.Start,
                block.EndInclusive,
                source.PositionOf(block.Start).Line);
            definitions.Add(definition);
            byLabel.TryAdd(definition.NormalizedLabel, definition);
        }

        return new ReferenceIndex(definitions, byLabel);
    }

    /// <summary>
    ///     Normalizes a label the way CommonMark matches references.
    /// </summary>
    /// <param name="label">The label as written. Must not be null.</param>
    /// <returns>The label trimmed, with runs of whitespace collapsed to one space, and case folded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
    public static string NormalizeLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        // Collapse every run of whitespace to a single space
        var builder = new StringBuilder(label.Length);
        var pendingSpace = false;
        foreach (var c in label)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        // Case fold (invariant lower-casing is the closest approximation available)
        return builder.ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Looks up a definition by label.
    /// </summary>
    /// <param name="label">The label to find (case and whitespace insensitive). Must not be null.</param>
    /// <param name="definition">Receives the first definition with that label.</param>
    /// <returns><see langword="true"/> when a definition exists.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
    public bool TryGet(string label, [NotNullWhen(true)] out ReferenceDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(label);
        return _byLabel.TryGetValue(NormalizeLabel(label), out definition);
    }
}
